using System.Collections.Generic;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Applies server world snapshots into cached entity states and updates visuals with interpolation.
    /// </summary>
    public class SnapshotApplier
    {
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

        private readonly IEntityViewFactory _viewFactory;
        private readonly Dictionary<int, EntitySnapshot> _lastSnapshots = new();
        private readonly Dictionary<int, EntityRuntimeState> _runtimeStates = new();
        private readonly HashSet<int> _snapshotIds = new();

        private readonly float _positionLerpSpeed;
        private readonly float _rotationLerpSpeed;
        private readonly float _hardSnapDistance;

        public event System.Action<EntityRuntimeState> OnEntitySpawned;
        public event System.Action<EntityRuntimeState> OnEntityUpdated;
        public event System.Action<int> OnEntityDespawned;

        public SnapshotApplier(
            IEntityViewFactory viewFactory,
            float positionLerpSpeed = 12f,
            float rotationLerpSpeed = 16f,
            float hardSnapDistance = 4f)
        {
            _viewFactory = viewFactory;
            _positionLerpSpeed = Mathf.Max(1f, positionLerpSpeed);
            _rotationLerpSpeed = Mathf.Max(1f, rotationLerpSpeed);
            _hardSnapDistance = Mathf.Max(0.5f, hardSnapDistance);
        }

        public void Apply(IReadOnlyList<EntitySnapshot> snapshots)
        {
            Apply(snapshots, true);
        }

        public void Apply(IReadOnlyList<EntitySnapshot> snapshots, bool isFullSnapshot)
        {
            if (snapshots == null)
                return;

            _snapshotIds.Clear();

            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot raw = snapshots[i];
                if (raw.EntityId <= 0)
                    continue;

                EntitySnapshot normalized = Normalize(raw);
                _snapshotIds.Add(normalized.EntityId);
                _lastSnapshots[normalized.EntityId] = normalized;
                UpsertRuntimeState(normalized);
            }

            if (isFullSnapshot)
                DespawnMissingEntities();
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

            foreach (var pair in _runtimeStates)
            {
                EntityRuntimeState state = pair.Value;
                if (state.View == null)
                    continue;

                ApplyVisuals(state, deltaTime);
            }
        }

        public void ClearAll()
        {
            var ids = new List<int>(_runtimeStates.Keys);
            for (int i = 0; i < ids.Count; i++)
                Despawn(ids[i]);

            _lastSnapshots.Clear();
            _runtimeStates.Clear();
            _snapshotIds.Clear();
        }

        private void UpsertRuntimeState(EntitySnapshot snapshot)
        {
            if (!_runtimeStates.TryGetValue(snapshot.EntityId, out EntityRuntimeState state))
            {
                state = new EntityRuntimeState
                {
                    Snapshot = snapshot,
                    View = SpawnView(snapshot)
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
            OnEntityDespawned?.Invoke(entityId);
        }

        private void ApplyVisuals(EntityRuntimeState state, float deltaTime)
        {
            EntityView view = state.View;
            EntitySnapshot snapshot = state.Snapshot;

            Vector3 current = view.transform.position;
            Vector3 target = snapshot.Position;

            float drift = Vector3.Distance(current, target);
            if (drift >= _hardSnapDistance)
            {
                view.SetPosition(target);
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

        private static EntitySnapshot Normalize(EntitySnapshot snapshot)
        {
            if (snapshot.HpMax < 0) snapshot.HpMax = 0;
            if (snapshot.HpCurrent < 0) snapshot.HpCurrent = 0;
            if (snapshot.HpCurrent > snapshot.HpMax) snapshot.HpCurrent = snapshot.HpMax;

            if (snapshot.Type == EntityType.Unknown)
                snapshot.Type = EntityType.RemotePlayer;

            if (snapshot.HpMax > 0 && snapshot.HpCurrent > 0 && !snapshot.IsAlive)
                snapshot.IsAlive = true;

            return snapshot;
        }
    }
}
