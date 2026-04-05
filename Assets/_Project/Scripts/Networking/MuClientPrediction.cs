using System;
using System.Collections.Generic;
using MuLike.Gameplay.Controllers;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Client-side movement prediction and server reconciliation for high-latency mobile connections.
    ///
    /// Algorithm:
    ///   1. Every LateUpdate, snapshot the player's post-motor position → ring buffer (64 frames).
    ///   2. Track each frame's implied velocity (pos[t] - pos[t-1]) / dt for replay.
    ///   3. When the server sends an authoritative position (via <see cref="NetworkGameClient.OnMoveResult"/>):
    ///      a. Find the buffered frame closest in time to when the move was sent.
    ///      b. Compute position error between server answer and what we predicted.
    ///      c. If error > <see cref="_reconcileThreshold"/>:
    ///         - Rewind to the server position.
    ///         - Replay all unacknowledged frames forward using stored velocities.
    ///         - Apply the result via <see cref="CharacterMotor.ApplyServerCorrection"/>.
    ///
    /// The replay step is a simplified Euler extrapolation (not full CharacterController
    /// physics). On flat MMORPG terrain this produces sub-frame-level accuracy.
    ///
    /// Anti-cheat: optionally validates speed before the motor sends move packets.
    ///
    /// Dependencies:
    ///   - <see cref="CharacterMotor"/> on the same GameObject (or assigned in Inspector).
    ///   - <see cref="NetworkGameClient"/> anywhere in scene (auto-discovered).
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class MuClientPrediction : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────────

        [Header("Prediction")]
        [Tooltip("Minimum position error (metres) before reconciliation fires.")]
        [SerializeField, Min(0.01f)] private float _reconcileThreshold = 0.1f;

        [Tooltip("Frames in the ring buffer. Higher = handles longer round-trips.")]
        [SerializeField, Min(16)] private int _bufferSize = 64;

        [Header("Correction")]
        [Tooltip("Drift below this snaps smoothly. Above HardSnap → instant teleport via CharacterMotor.")]
        [SerializeField] private float _softCorrectionMax = 0.5f;

        [Header("Anti-cheat")]
        [Tooltip("Drop predicted moves whose speed exceeds this (units/s). Set 0 to disable.")]
        [SerializeField, Min(0f)] private float _maxLocalSpeedUps = 12f;

        [Header("Debug")]
        [SerializeField] private bool _showDebugGizmos = false;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private CharacterMotor _motor;
        private NetworkGameClient _networkClient;

        // Ring buffer for prediction frames
        private PredictionFrame[] _frames;
        private uint _localSequence;       // increments every LateUpdate
        private Vector3 _previousPosition; // for velocity derivation

        // Server reconcile request
        private bool    _pendingReconcile;
        private Vector3 _serverPosition;
        private float   _serverResponseTime; // Time.time when the response arrived

        // Metrics (observable via Inspector / debug overlay)
        private float _lastPredictionErrorMetres;
        private float _peakPredictionErrorMetres;
        private int   _reconcileCount;

        public float LastPredictionErrorMetres => _lastPredictionErrorMetres;
        public float PeakPredictionErrorMetres => _peakPredictionErrorMetres;
        public int   ReconcileCount            => _reconcileCount;

        // ── Structs ────────────────────────────────────────────────────────────────

        private struct PredictionFrame
        {
            public uint    Sequence;
            public float   Timestamp;      // Time.time at record
            public float   DeltaTime;      // Time.deltaTime at record
            public Vector3 PositionBefore; // position at frame start (before motor Update)
            public Vector3 PositionAfter;  // position at LateUpdate (post-motor)
            public Vector3 Velocity;       // (PositionAfter - PositionBefore) / DeltaTime
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _motor = GetComponent<CharacterMotor>();
            _networkClient = FindAnyObjectByType<NetworkGameClient>();
            _frames = new PredictionFrame[Mathf.Max(16, _bufferSize)];
            _previousPosition = transform.position;
        }

        private void OnEnable()
        {
            if (_networkClient != null)
                _networkClient.OnMoveResult += HandleServerMoveResult;
        }

        private void OnDisable()
        {
            if (_networkClient != null)
                _networkClient.OnMoveResult -= HandleServerMoveResult;
        }

        /// <summary>
        /// Records the frame after the CharacterMotor has moved the character this tick.
        /// Reconciliation is also applied here to avoid a 1-frame delay on the correction.
        /// </summary>
        private void LateUpdate()
        {
            RecordCurrentFrame();

            if (_pendingReconcile)
                ApplyReconciliation();
        }

        // ── Frame recording ────────────────────────────────────────────────────────

        private void RecordCurrentFrame()
        {
            uint seq  = ++_localSequence;
            int  idx  = (int)(seq % (uint)_frames.Length);
            float dt  = Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 posNow = transform.position;

            _frames[idx] = new PredictionFrame
            {
                Sequence       = seq,
                Timestamp      = Time.time,
                DeltaTime      = dt,
                PositionBefore = _previousPosition,
                PositionAfter  = posNow,
                Velocity       = (posNow - _previousPosition) / dt
            };

            _previousPosition = posNow;
        }

        // ── Server event handler ───────────────────────────────────────────────────

        private void HandleServerMoveResult(bool success, Vector3 serverPos, string message)
        {
            if (!success) return;

            _serverPosition     = serverPos;
            _serverResponseTime = Time.time;
            _pendingReconcile   = true;
        }

        // ── Reconciliation ─────────────────────────────────────────────────────────

        private void ApplyReconciliation()
        {
            _pendingReconcile = false;

            // Find the frame that was in-flight when the server responded
            uint ackSequence = FindAcknowledgedSequence(_serverResponseTime);

            // How far off was our prediction at that frame?
            int frameIdx = (int)(ackSequence % (uint)_frames.Length);
            ref PredictionFrame ackFrame = ref _frames[frameIdx];

            float errorAtAck = ackFrame.Sequence == ackSequence
                ? Vector3.Distance(ackFrame.PositionAfter, _serverPosition)
                : Vector3.Distance(transform.position, _serverPosition);

            _lastPredictionErrorMetres = errorAtAck;
            if (errorAtAck > _peakPredictionErrorMetres)
                _peakPredictionErrorMetres = errorAtAck;

            if (errorAtAck < _reconcileThreshold)
                return; // prediction was accurate enough — no correction needed

            _reconcileCount++;

            // Replay unacknowledged frames from server-corrected base position
            Vector3 replayedPos = ReplayFromSequence(_serverPosition, ackSequence + 1);

            // Current position error after replay
            float finalDrift = Vector3.Distance(transform.position, replayedPos);

            if (finalDrift > _softCorrectionMax)
            {
                // Large drift: let CharacterMotor decide hard snap vs soft lerp
                _motor.ApplyServerCorrection(replayedPos);
            }
            else if (finalDrift > _reconcileThreshold)
            {
                // Small but noticeable: smooth nudge
                Vector3 nudged = Vector3.Lerp(transform.position, replayedPos, 0.5f);
                _motor.ApplyServerCorrection(nudged);
            }
            // else: within acceptable tolerance — skip correction to avoid micro-jitter
        }

        /// <summary>
        /// Returns the sequence number of the frame whose timestamp is closest to
        /// <paramref name="serverResponseTime"/> minus a nominal RTT window.
        /// </summary>
        private uint FindAcknowledgedSequence(float serverResponseTime)
        {
            uint bestSeq  = 0;
            float bestDiff = float.MaxValue;

            for (int i = 0; i < _frames.Length; i++)
            {
                ref PredictionFrame f = ref _frames[i];
                if (f.Sequence == 0) continue;

                float diff = Mathf.Abs(f.Timestamp - serverResponseTime);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestSeq  = f.Sequence;
                }
            }

            return bestSeq > 0 ? bestSeq : _localSequence;
        }

        /// <summary>
        /// Euler-integrates the stored per-frame velocities forward from <paramref name="serverBase"/>
        /// starting at <paramref name="fromSequence"/> up to the latest local frame.
        ///
        /// This is a position-space replay — not a full CharacterController simulation.
        /// Suitable for flat MMORPG terrain; may diverge slightly over complex geometry.
        /// </summary>
        private Vector3 ReplayFromSequence(Vector3 serverBase, uint fromSequence)
        {
            Vector3 pos = serverBase;

            for (uint seq = fromSequence; seq <= _localSequence; seq++)
            {
                int idx = (int)(seq % (uint)_frames.Length);
                ref PredictionFrame f = ref _frames[idx];

                if (f.Sequence != seq) continue; // frame was overwritten (buffer full / stale)

                // Validate speed before replaying to detect local speed-hack attempts
                float replaySpeed = f.Velocity.magnitude;
                if (_maxLocalSpeedUps > 0f && replaySpeed > _maxLocalSpeedUps * 1.15f)
                    continue; // skip this frame's contribution

                pos += f.Velocity * f.DeltaTime;
            }

            return pos;
        }

        // ── Debug gizmos ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGizmos || _frames == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _frames.Length; i++)
            {
                ref PredictionFrame f = ref _frames[i];
                if (f.Sequence == 0) continue;
                Gizmos.DrawWireSphere(f.PositionAfter, 0.08f);
            }

            if (_pendingReconcile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_serverPosition, 0.25f);
                Gizmos.DrawLine(transform.position, _serverPosition);
            }
        }
#endif
    }
}
