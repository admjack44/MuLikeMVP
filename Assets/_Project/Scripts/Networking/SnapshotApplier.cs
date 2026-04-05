using System;
using System.Collections.Generic;
using MuLike.Gameplay.Entities;
using MuLike.Gameplay.Controllers;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Applies server world snapshots into cached entity states and updates visuals with interpolation.
    /// </summary>
    public class SnapshotApplier
    {
        public struct RuntimeMetrics
        {
            public int PacketsReceived;
            public int SnapshotsReceived;
            public int SnapshotsApplied;
            public int SnapshotsDroppedOutOfOrder;
            public int SnapshotsDroppedOld;
            public int Teleports;
            public int Despawns;
            public int ExtrapolatedFrames;
            public float CurrentInterpolationBackTimeMs;
            public float CurrentExtrapolationMs;
            public float LocalReconciliationError;
        }

        public enum EntityType
        {
            Unknown = 0,
            RemotePlayer = 1,
            Monster = 2,
            Pet = 3,
            Drop = 4
        }

        public struct EntitySnapshot
        {
            public int EntityId;
            public EntityType Type;
            public Vector3 Position;
            public float RotationY;
            public int HpCurrent;
            public int HpMax;
            public bool IsAlive;
            public string DisplayName;
            public int OwnerEntityId;
        }

        public struct EntityRuntimeState
        {
            public EntitySnapshot Snapshot;
            public EntityView View;
        }

        private struct TimedEntitySnapshot
        {
            public uint Sequence;
            public double ServerTimeSec;
            public EntitySnapshot Snapshot;
            public Vector3 Velocity;
        }

        private sealed class SnapshotTrack
        {
            private readonly TimedEntitySnapshot[] _frames;
            private int _head;
            private int _count;

            public uint LastSequence { get; private set; }
            public double LastServerTimeSec { get; private set; }

            public int Count => _count;

            public SnapshotTrack(int capacity)
            {
                _frames = new TimedEntitySnapshot[Mathf.Max(4, capacity)];
            }

            public bool TryAdd(TimedEntitySnapshot next, out bool droppedOldest)
            {
                droppedOldest = false;

                if (_count > 0)
                {
                    TimedEntitySnapshot last = GetByIndex(_count - 1);
                    bool isOlderSequence = next.Sequence != 0 && last.Sequence != 0 && next.Sequence < last.Sequence;
                    bool isOlderServerTime = next.ServerTimeSec + 0.00001d < last.ServerTimeSec;
                    bool isSameSample = next.Sequence == last.Sequence && Math.Abs(next.ServerTimeSec - last.ServerTimeSec) < 0.00001d;

                    if (isOlderSequence || isOlderServerTime || isSameSample)
                        return false;

                    double dt = next.ServerTimeSec - last.ServerTimeSec;
                    if (dt > 0.0001d)
                        next.Velocity = (next.Snapshot.Position - last.Snapshot.Position) / (float)dt;
                }

                if (_count >= _frames.Length)
                {
                    _head = (_head + 1) % _frames.Length;
                    _count--;
                    droppedOldest = true;
                }

                int tail = (_head + _count) % _frames.Length;
                _frames[tail] = next;
                _count++;
                LastSequence = next.Sequence;
                LastServerTimeSec = next.ServerTimeSec;
                return true;
            }

            public bool TrySample(
                double renderServerTimeSec,
                bool allowExtrapolation,
                float maxExtrapolationSec,
                out EntitySnapshot sampled,
                out bool usedExtrapolation,
                out float extrapolationTimeSec)
            {
                sampled = default;
                usedExtrapolation = false;
                extrapolationTimeSec = 0f;

                if (_count <= 0)
                    return false;

                TimedEntitySnapshot first = GetByIndex(0);
                TimedEntitySnapshot last = GetByIndex(_count - 1);

                if (_count == 1 || renderServerTimeSec <= first.ServerTimeSec)
                {
                    sampled = first.Snapshot;
                    return true;
                }

                if (renderServerTimeSec >= last.ServerTimeSec)
                {
                    sampled = last.Snapshot;
                    if (!allowExtrapolation || !last.Snapshot.IsAlive)
                        return true;

                    float dt = (float)(renderServerTimeSec - last.ServerTimeSec);
                    dt = Mathf.Clamp(dt, 0f, Mathf.Max(0f, maxExtrapolationSec));
                    if (dt <= 0f)
                        return true;

                    sampled.Position += last.Velocity * dt;
                    usedExtrapolation = true;
                    extrapolationTimeSec = dt;
                    return true;
                }

                TimedEntitySnapshot older = first;
                TimedEntitySnapshot newer = last;
                bool found = false;

                for (int i = 1; i < _count; i++)
                {
                    TimedEntitySnapshot current = GetByIndex(i);
                    if (current.ServerTimeSec < renderServerTimeSec)
                    {
                        older = current;
                        continue;
                    }

                    newer = current;
                    found = true;
                    break;
                }

                if (!found)
                {
                    sampled = last.Snapshot;
                    return true;
                }

                float span = (float)(newer.ServerTimeSec - older.ServerTimeSec);
                float t = span <= 0.0001f
                    ? 1f
                    : Mathf.Clamp01((float)((renderServerTimeSec - older.ServerTimeSec) / span));

                sampled = newer.Snapshot;
                sampled.Position = Vector3.Lerp(older.Snapshot.Position, newer.Snapshot.Position, t);
                sampled.RotationY = Mathf.LerpAngle(older.Snapshot.RotationY, newer.Snapshot.RotationY, t);
                sampled.HpCurrent = t >= 0.5f ? newer.Snapshot.HpCurrent : older.Snapshot.HpCurrent;
                sampled.HpMax = t >= 0.5f ? newer.Snapshot.HpMax : older.Snapshot.HpMax;
                sampled.IsAlive = t >= 0.5f ? newer.Snapshot.IsAlive : older.Snapshot.IsAlive;
                return true;
            }

            private TimedEntitySnapshot GetByIndex(int index)
            {
                int slot = (_head + index) % _frames.Length;
                return _frames[slot];
            }
        }

        private readonly IEntityViewFactory _viewFactory;
        private readonly Dictionary<int, EntitySnapshot> _lastSnapshots = new();
        private readonly Dictionary<int, EntityRuntimeState> _runtimeStates = new();
        private readonly Dictionary<int, SnapshotTrack> _tracks = new();
        private readonly HashSet<int> _snapshotIds = new();

        private readonly float _positionLerpSpeed;
        private readonly float _rotationLerpSpeed;
        private readonly float _teleportThreshold;
        private readonly float _interpolationBackTimeSec;
        private readonly float _maxExtrapolationSec;
        private readonly int _trackCapacity;
        private readonly bool _enableExtrapolation;
        private readonly bool _skipLocalPlayerView;

        private int _localPlayerEntityId;
        private Transform _localPlayerTransform;
        private CharacterMotor _localPlayerMotor;
        private RuntimeMetrics _metrics;

        public event System.Action<EntityRuntimeState> OnEntitySpawned;
        public event System.Action<EntityRuntimeState> OnEntityUpdated;
        public event System.Action<int> OnEntityDespawned;

        public SnapshotApplier(
            IEntityViewFactory viewFactory,
            float positionLerpSpeed = 12f,
            float rotationLerpSpeed = 16f,
            float hardSnapDistance = 4f,
            float interpolationBackTimeMs = 100f,
            float maxExtrapolationMs = 120f,
            int perEntityBufferSize = 20,
            bool enableExtrapolation = true,
            bool skipLocalPlayerView = true)
        {
            _viewFactory = viewFactory;
            _positionLerpSpeed = Mathf.Max(1f, positionLerpSpeed);
            _rotationLerpSpeed = Mathf.Max(1f, rotationLerpSpeed);
            _teleportThreshold = Mathf.Max(0.5f, hardSnapDistance);
            _interpolationBackTimeSec = Mathf.Clamp(interpolationBackTimeMs / 1000f, 0.03f, 0.35f);
            _maxExtrapolationSec = Mathf.Clamp(maxExtrapolationMs / 1000f, 0f, 0.25f);
            _trackCapacity = Mathf.Clamp(perEntityBufferSize, 8, 64);
            _enableExtrapolation = enableExtrapolation;
            _skipLocalPlayerView = skipLocalPlayerView;
            _metrics.CurrentInterpolationBackTimeMs = _interpolationBackTimeSec * 1000f;
        }

        public void Apply(IReadOnlyList<EntitySnapshot> snapshots)
        {
            Apply(snapshots, true);
        }

        public void Apply(IReadOnlyList<EntitySnapshot> snapshots, bool isFullSnapshot)
        {
            Apply(
                snapshots,
                isFullSnapshot,
                packetSequence: 0,
                packetTimestampMs: 0,
                arrivalTimeSec: Time.unscaledTimeAsDouble);
        }

        public void Apply(
            IReadOnlyList<EntitySnapshot> snapshots,
            bool isFullSnapshot,
            uint packetSequence,
            long packetTimestampMs,
            double arrivalTimeSec)
        {
            if (snapshots == null)
                return;

            _metrics.PacketsReceived++;
            _metrics.SnapshotsReceived += snapshots.Count;

            _snapshotIds.Clear();
            double packetServerTimeSec = packetTimestampMs > 0 ? packetTimestampMs / 1000d : arrivalTimeSec;

            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot raw = snapshots[i];
                if (raw.EntityId <= 0)
                    continue;

                // Delta despawn marker from server: remove runtime/view immediately.
                if (raw.Type == EntityType.Unknown && !raw.IsAlive)
                {
                    _snapshotIds.Add(raw.EntityId);
                    Despawn(raw.EntityId);
                    continue;
                }

                EntitySnapshot normalized = Normalize(raw);
                _snapshotIds.Add(normalized.EntityId);
                _lastSnapshots[normalized.EntityId] = normalized;

                if (!_tracks.TryGetValue(normalized.EntityId, out SnapshotTrack track))
                {
                    track = new SnapshotTrack(_trackCapacity);
                    _tracks[normalized.EntityId] = track;
                }

                var timed = new TimedEntitySnapshot
                {
                    Sequence = packetSequence,
                    ServerTimeSec = packetServerTimeSec,
                    Snapshot = normalized,
                    Velocity = Vector3.zero
                };

                bool accepted = track.TryAdd(timed, out bool droppedOldest);
                if (!accepted)
                {
                    _metrics.SnapshotsDroppedOutOfOrder++;
                    continue;
                }

                if (droppedOldest)
                    _metrics.SnapshotsDroppedOld++;

                _metrics.SnapshotsApplied++;
                UpsertRuntimeState(normalized);
            }

            if (isFullSnapshot)
                DespawnMissingEntities();
        }

        public void SetLocalPlayerContext(int localPlayerEntityId, Transform localPlayerTransform, CharacterMotor localPlayerMotor)
        {
            _localPlayerEntityId = localPlayerEntityId;
            _localPlayerTransform = localPlayerTransform;
            _localPlayerMotor = localPlayerMotor;
        }

        public RuntimeMetrics GetMetrics()
        {
            return _metrics;
        }

        public bool TryGetLast(int entityId, out EntitySnapshot snapshot)
        {
            return _lastSnapshots.TryGetValue(entityId, out snapshot);
        }

        public bool TryGetRuntimeState(int entityId, out EntityRuntimeState state)
        {
            return _runtimeStates.TryGetValue(entityId, out state);
        }

        public void TickVisuals(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            double nowSec = Time.unscaledTimeAsDouble;
            double renderTimeSec = nowSec - _interpolationBackTimeSec;

            foreach (var pair in _runtimeStates)
            {
                int entityId = pair.Key;
                EntityRuntimeState state = pair.Value;
                if (state.View == null)
                    continue;

                if (!_tracks.TryGetValue(entityId, out SnapshotTrack track) || track.Count <= 0)
                    continue;

                if (!track.TrySample(
                        renderTimeSec,
                        allowExtrapolation: _enableExtrapolation,
                        maxExtrapolationSec: _maxExtrapolationSec,
                        out EntitySnapshot sampled,
                        out bool usedExtrapolation,
                        out float extrapolationTimeSec))
                {
                    continue;
                }

                if (usedExtrapolation)
                {
                    _metrics.ExtrapolatedFrames++;
                    _metrics.CurrentExtrapolationMs = extrapolationTimeSec * 1000f;
                }
                else
                {
                    _metrics.CurrentExtrapolationMs = Mathf.Lerp(_metrics.CurrentExtrapolationMs, 0f, 0.15f);
                }

                state.Snapshot = sampled;
                _runtimeStates[entityId] = state;
                ApplyVisuals(state, deltaTime);
            }

            ReconcileLocalPlayer(renderTimeSec);
        }

        public void ClearAll()
        {
            var ids = new List<int>(_runtimeStates.Keys);
            for (int i = 0; i < ids.Count; i++)
                Despawn(ids[i]);

            _lastSnapshots.Clear();
            _runtimeStates.Clear();
            _snapshotIds.Clear();
            _tracks.Clear();
        }

        private void UpsertRuntimeState(EntitySnapshot snapshot)
        {
            if (!_runtimeStates.TryGetValue(snapshot.EntityId, out EntityRuntimeState state))
            {
                bool skipSpawnForLocal = _skipLocalPlayerView
                    && _localPlayerEntityId > 0
                    && snapshot.EntityId == _localPlayerEntityId;

                state = new EntityRuntimeState
                {
                    Snapshot = snapshot,
                    View = skipSpawnForLocal ? null : SpawnView(snapshot)
                };

                _runtimeStates[snapshot.EntityId] = state;
                OnEntitySpawned?.Invoke(state);
                return;
            }

            state.Snapshot = snapshot;
            _runtimeStates[snapshot.EntityId] = state;
            OnEntityUpdated?.Invoke(state);
        }

        private EntityView SpawnView(EntitySnapshot snapshot)
        {
            if (_viewFactory == null)
            {
                Debug.LogWarning("[SnapshotApplier] IEntityViewFactory not configured. Snapshots will be cached without visuals.");
                return null;
            }

            EntityView view = _viewFactory.CreateView(snapshot);
            if (view == null)
                return null;

            view.Initialize(snapshot.EntityId);
            view.SetPosition(snapshot.Position);
            view.SetRotation(snapshot.RotationY);
            return view;
        }

        private void DespawnMissingEntities()
        {
            var keys = new List<int>(_runtimeStates.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int entityId = keys[i];
                if (_snapshotIds.Contains(entityId))
                    continue;

                Despawn(entityId);
            }
        }

        private void Despawn(int entityId)
        {
            if (!_runtimeStates.TryGetValue(entityId, out EntityRuntimeState state))
                return;

            if (state.View != null)
                _viewFactory?.DestroyView(state.View);

            _runtimeStates.Remove(entityId);
            _lastSnapshots.Remove(entityId);
            _tracks.Remove(entityId);
            _metrics.Despawns++;
            OnEntityDespawned?.Invoke(entityId);
        }

        private void ApplyVisuals(EntityRuntimeState state, float deltaTime)
        {
            EntityView view = state.View;
            EntitySnapshot snapshot = state.Snapshot;

            Vector3 current = view.transform.position;
            Vector3 target = snapshot.Position;

            float drift = Vector3.Distance(current, target);
            if (drift >= _teleportThreshold)
            {
                view.SetPosition(target);
                _metrics.Teleports++;
            }
            else
            {
                float positionT = 1f - Mathf.Exp(-_positionLerpSpeed * deltaTime);
                view.SetPosition(Vector3.Lerp(current, target, positionT));
            }

            float angleT = 1f - Mathf.Exp(-_rotationLerpSpeed * deltaTime);
            float currentY = view.transform.eulerAngles.y;
            float nextY = Mathf.LerpAngle(currentY, snapshot.RotationY, angleT);
            view.SetRotation(nextY);

            if (!snapshot.IsAlive || snapshot.HpCurrent <= 0)
                view.OnDeath();
        }

        private void ReconcileLocalPlayer(double renderTimeSec)
        {
            if (_localPlayerEntityId <= 0 || _localPlayerTransform == null)
                return;

            if (!_tracks.TryGetValue(_localPlayerEntityId, out SnapshotTrack track) || track.Count <= 0)
                return;

            if (!track.TrySample(
                    renderTimeSec,
                    allowExtrapolation: false,
                    maxExtrapolationSec: 0f,
                    out EntitySnapshot sampled,
                    out _,
                    out _))
            {
                return;
            }

            Vector3 localPos = _localPlayerTransform.position;
            float drift = Vector3.Distance(localPos, sampled.Position);
            _metrics.LocalReconciliationError = Mathf.Lerp(_metrics.LocalReconciliationError, drift, 0.1f);

            if (drift <= 0.03f)
                return;

            if (_localPlayerMotor != null)
            {
                _localPlayerMotor.ApplyServerCorrection(sampled.Position);
                return;
            }

            float t = drift >= _teleportThreshold ? 1f : Mathf.Clamp01(drift / Mathf.Max(0.2f, _teleportThreshold));
            _localPlayerTransform.position = Vector3.Lerp(localPos, sampled.Position, Mathf.Max(0.2f, t));
        }

        private static EntitySnapshot Normalize(EntitySnapshot snapshot)
        {
            if (snapshot.HpMax < 0) snapshot.HpMax = 0;
            if (snapshot.HpCurrent < 0) snapshot.HpCurrent = 0;
            if (snapshot.HpCurrent > snapshot.HpMax) snapshot.HpCurrent = snapshot.HpMax;

            if (snapshot.HpMax > 0 && snapshot.HpCurrent > 0 && !snapshot.IsAlive)
                snapshot.IsAlive = true;

            return snapshot;
        }
    }
}
