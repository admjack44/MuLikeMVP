using System.Collections.Generic;
using MuLike.Gameplay.Entities;
using MuLike.Gameplay.Controllers;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Runtime glue example for SnapshotApplier. Feed snapshots here when network world updates arrive.
    /// </summary>
    public class SnapshotSyncDriver : MonoBehaviour
    {
        [SerializeField] private PrefabEntityViewFactory _viewFactory;
        [SerializeField] private bool _demoFeedOnStart = false;

        [Header("Interpolation")]
        [SerializeField] private float _interpolationBackTimeMs = 100f;
        [SerializeField] private float _maxExtrapolationMs = 120f;
        [SerializeField] private float _teleportThreshold = 4f;
        [SerializeField] private int _snapshotBufferSizePerEntity = 20;
        [SerializeField] private bool _enableShortExtrapolation = true;

        [Header("Local Reconciliation")]
        [SerializeField] private int _localPlayerEntityId;
        [SerializeField] private Transform _localPlayerTransform;
        [SerializeField] private CharacterMotor _localPlayerMotor;

        [Header("Debug")]
        [SerializeField] private bool _showDebugOverlay;
        [SerializeField] private bool _showDebugLogs;
        [SerializeField] private KeyCode _toggleOverlayKey = KeyCode.F10;

        private SnapshotApplier _snapshotApplier;
        private NetworkGameClient _networkClient;
        private uint _lastSequence;
        private double _lastArrivalSec;
        private float _snapshotIntervalMs;
        private float _snapshotJitterMs;

        public SnapshotApplier.RuntimeMetrics Metrics => _snapshotApplier != null
            ? _snapshotApplier.GetMetrics()
            : default;

        private void Awake()
        {
            if (_viewFactory == null)
                _viewFactory = FindAnyObjectByType<PrefabEntityViewFactory>();

            if (_localPlayerMotor == null)
                _localPlayerMotor = FindAnyObjectByType<CharacterMotor>();

            if (_localPlayerTransform == null && _localPlayerMotor != null)
                _localPlayerTransform = _localPlayerMotor.transform;

            _networkClient = FindAnyObjectByType<NetworkGameClient>();

            _snapshotApplier = new SnapshotApplier(
                _viewFactory,
                hardSnapDistance: _teleportThreshold,
                interpolationBackTimeMs: _interpolationBackTimeMs,
                maxExtrapolationMs: _maxExtrapolationMs,
                perEntityBufferSize: _snapshotBufferSizePerEntity,
                enableExtrapolation: _enableShortExtrapolation,
                skipLocalPlayerView: true);

            _snapshotApplier.SetLocalPlayerContext(_localPlayerEntityId, _localPlayerTransform, _localPlayerMotor);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleOverlayKey))
                _showDebugOverlay = !_showDebugOverlay;

            _snapshotApplier?.TickVisuals(Time.deltaTime);
        }

        /// <summary>
        /// Call this method from network message handlers once world snapshots are decoded.
        /// </summary>
        public void ApplyWorldSnapshot(
            IReadOnlyList<SnapshotApplier.EntitySnapshot> snapshots,
            bool isFullSnapshot = true,
            uint sequenceNumber = 0,
            long serverTimestampMs = 0)
        {
            if (_snapshotApplier == null)
                return;

            if (sequenceNumber != 0 && _lastSequence != 0 && sequenceNumber <= _lastSequence)
            {
                if (_showDebugLogs)
                    Debug.Log($"[SnapshotSyncDriver] Dropped out-of-order packet seq={sequenceNumber} <= {_lastSequence}");
                return;
            }

            double nowSec = Time.unscaledTimeAsDouble;
            if (_lastArrivalSec > 0d)
            {
                float intervalMs = (float)((nowSec - _lastArrivalSec) * 1000d);
                if (_snapshotIntervalMs <= 0.001f)
                {
                    _snapshotIntervalMs = intervalMs;
                }
                else
                {
                    float delta = Mathf.Abs(intervalMs - _snapshotIntervalMs);
                    _snapshotIntervalMs = Mathf.Lerp(_snapshotIntervalMs, intervalMs, 0.15f);
                    _snapshotJitterMs = Mathf.Lerp(_snapshotJitterMs, delta, 0.15f);
                }
            }

            _lastArrivalSec = nowSec;
            if (sequenceNumber != 0)
                _lastSequence = sequenceNumber;

            _snapshotApplier.Apply(
                snapshots,
                isFullSnapshot,
                packetSequence: sequenceNumber,
                packetTimestampMs: serverTimestampMs,
                arrivalTimeSec: nowSec);
        }

        public void SetLocalPlayerEntityId(int localPlayerEntityId)
        {
            _localPlayerEntityId = localPlayerEntityId;
            _snapshotApplier?.SetLocalPlayerContext(_localPlayerEntityId, _localPlayerTransform, _localPlayerMotor);
        }

        public void SetLocalPlayerTransform(Transform localTransform, CharacterMotor localMotor = null)
        {
            _localPlayerTransform = localTransform;
            _localPlayerMotor = localMotor != null ? localMotor : _localPlayerMotor;
            _snapshotApplier?.SetLocalPlayerContext(_localPlayerEntityId, _localPlayerTransform, _localPlayerMotor);
        }

        private void Start()
        {
            if (!_demoFeedOnStart) return;

            var demo = new List<SnapshotApplier.EntitySnapshot>
            {
                new SnapshotApplier.EntitySnapshot
                {
                    EntityId = 1001,
                    Type = SnapshotApplier.EntityType.RemotePlayer,
                    Position = new Vector3(2f, 0f, 3f),
                    RotationY = 180f,
                    HpCurrent = 120,
                    HpMax = 120,
                    IsAlive = true,
                    DisplayName = "RemoteKnight",
                    OwnerEntityId = 0
                },
                new SnapshotApplier.EntitySnapshot
                {
                    EntityId = 2001,
                    Type = SnapshotApplier.EntityType.Monster,
                    Position = new Vector3(-4f, 0f, 6f),
                    RotationY = 40f,
                    HpCurrent = 85,
                    HpMax = 100,
                    IsAlive = true,
                    DisplayName = "Goblin",
                    OwnerEntityId = 0
                }
            };

            ApplyWorldSnapshot(demo, true);
        }

        private void OnGUI()
        {
            if (!_showDebugOverlay)
                return;

            SnapshotApplier.RuntimeMetrics m = Metrics;
            float rtt = _networkClient != null ? _networkClient.SmoothedRttMs : 0f;
            float rttJitter = _networkClient != null ? _networkClient.RttJitterMs : 0f;

            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 240f), GUI.skin.box);
            GUILayout.Label("Snapshot Net Debug");
            GUILayout.Label($"RTT: {rtt:F1}ms | RTT jitter: {rttJitter:F1}ms");
            GUILayout.Label($"Snapshot interval: {_snapshotIntervalMs:F1}ms | Snapshot jitter: {_snapshotJitterMs:F1}ms");
            GUILayout.Label($"Packets: {m.PacketsReceived} | Snapshots in: {m.SnapshotsReceived} | Applied: {m.SnapshotsApplied}");
            GUILayout.Label($"Dropped OOO: {m.SnapshotsDroppedOutOfOrder} | Dropped old: {m.SnapshotsDroppedOld}");
            GUILayout.Label($"Extrapolated frames: {m.ExtrapolatedFrames} | Extrapolation: {m.CurrentExtrapolationMs:F1}ms");
            GUILayout.Label($"Teleports: {m.Teleports} | Despawns: {m.Despawns}");
            GUILayout.Label($"Interp backtime: {m.CurrentInterpolationBackTimeMs:F1}ms | Local error: {m.LocalReconciliationError:F3}");
            GUILayout.EndArea();
        }
    }
}
