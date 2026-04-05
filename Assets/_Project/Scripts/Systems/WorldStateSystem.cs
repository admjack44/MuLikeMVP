using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Systems
{
    /// <summary>
    /// Stores lightweight world/session runtime state for map, entities and local player position.
    /// </summary>
    public sealed class WorldStateSystem
    {
        [Serializable]
        public struct WorldEntityState
        {
            public int EntityId;
            public int EntityType;
            public bool IsAlive;
            public float X;
            public float Y;
            public float Z;
            public float RotationY;
            public int HpCurrent;
            public int HpMax;
            public string DisplayName;
        }

        [Serializable]
        public sealed class WorldStateSnapshot
        {
            public int MapId;
            public string MapName;
            public long ServerTimestampMs;
            public float LocalPlayerX;
            public float LocalPlayerY;
            public float LocalPlayerZ;
            public List<WorldEntityState> Entities = new();
        }

        [Serializable]
        public struct WorldStateDelta
        {
            public bool HasMap;
            public int MapId;
            public string MapName;

            public bool HasServerTimestamp;
            public long ServerTimestampMs;

            public bool HasLocalPlayerPosition;
            public float LocalPlayerX;
            public float LocalPlayerY;
            public float LocalPlayerZ;

            public bool HasUpsertEntity;
            public WorldEntityState UpsertEntity;

            public bool HasRemoveEntity;
            public int RemoveEntityId;
        }

        private readonly Dictionary<int, WorldEntityState> _entitiesById = new();

        public int MapId { get; private set; }
        public string MapName { get; private set; } = string.Empty;
        public long ServerTimestampMs { get; private set; }
        public Vector3 LocalPlayerPosition { get; private set; }
        public IReadOnlyDictionary<int, WorldEntityState> EntitiesById => _entitiesById;

        public event Action<WorldStateSnapshot> OnWorldSnapshotApplied;
        public event Action<WorldStateDelta> OnWorldDeltaApplied;
        public event Action<int, string> OnMapChanged;
        public event Action<Vector3> OnLocalPlayerPositionChanged;
        public event Action<WorldEntityState> OnEntityUpserted;
        public event Action<int> OnEntityRemoved;
        public event Action OnWorldStateChanged;

        public void ApplySnapshot(WorldStateSnapshot snapshot)
        {
            int previousMapId = MapId;
            string previousMapName = MapName;

            _entitiesById.Clear();

            if (snapshot != null)
            {
                MapId = Math.Max(0, snapshot.MapId);
                MapName = snapshot.MapName ?? string.Empty;
                ServerTimestampMs = Math.Max(0, snapshot.ServerTimestampMs);
                LocalPlayerPosition = new Vector3(snapshot.LocalPlayerX, snapshot.LocalPlayerY, snapshot.LocalPlayerZ);

                if (snapshot.Entities != null)
                {
                    for (int i = 0; i < snapshot.Entities.Count; i++)
                    {
                        WorldEntityState entity = Normalize(snapshot.Entities[i]);
                        if (entity.EntityId <= 0)
                            continue;

                        _entitiesById[entity.EntityId] = entity;
                    }
                }
            }
            else
            {
                MapId = 0;
                MapName = string.Empty;
                ServerTimestampMs = 0;
                LocalPlayerPosition = Vector3.zero;
            }

            if (previousMapId != MapId || !string.Equals(previousMapName, MapName, StringComparison.Ordinal))
                OnMapChanged?.Invoke(MapId, MapName);

            OnLocalPlayerPositionChanged?.Invoke(LocalPlayerPosition);
            OnWorldSnapshotApplied?.Invoke(CreateSnapshot());
            OnWorldStateChanged?.Invoke();
        }

        public void ApplyDelta(WorldStateDelta delta)
        {
            if (delta.HasMap)
            {
                int nextMapId = Math.Max(0, delta.MapId);
                string nextMapName = delta.MapName ?? string.Empty;

                bool mapChanged = MapId != nextMapId || !string.Equals(MapName, nextMapName, StringComparison.Ordinal);
                MapId = nextMapId;
                MapName = nextMapName;

                if (mapChanged)
                    OnMapChanged?.Invoke(MapId, MapName);
            }

            if (delta.HasServerTimestamp)
                ServerTimestampMs = Math.Max(0, delta.ServerTimestampMs);

            if (delta.HasLocalPlayerPosition)
            {
                LocalPlayerPosition = new Vector3(delta.LocalPlayerX, delta.LocalPlayerY, delta.LocalPlayerZ);
                OnLocalPlayerPositionChanged?.Invoke(LocalPlayerPosition);
            }

            if (delta.HasUpsertEntity)
            {
                WorldEntityState entity = Normalize(delta.UpsertEntity);
                if (entity.EntityId > 0)
                {
                    _entitiesById[entity.EntityId] = entity;
                    OnEntityUpserted?.Invoke(entity);
                }
            }

            if (delta.HasRemoveEntity)
            {
                int removeId = Math.Max(0, delta.RemoveEntityId);
                if (_entitiesById.Remove(removeId))
                    OnEntityRemoved?.Invoke(removeId);
            }

            OnWorldDeltaApplied?.Invoke(delta);
            OnWorldStateChanged?.Invoke();
        }

        public WorldStateSnapshot CreateSnapshot()
        {
            var snapshot = new WorldStateSnapshot
            {
                MapId = MapId,
                MapName = MapName,
                ServerTimestampMs = ServerTimestampMs,
                LocalPlayerX = LocalPlayerPosition.x,
                LocalPlayerY = LocalPlayerPosition.y,
                LocalPlayerZ = LocalPlayerPosition.z
            };

            foreach (KeyValuePair<int, WorldEntityState> pair in _entitiesById)
                snapshot.Entities.Add(pair.Value);

            snapshot.Entities.Sort((a, b) => a.EntityId.CompareTo(b.EntityId));
            return snapshot;
        }

        public bool TryGetEntity(int entityId, out WorldEntityState entity)
        {
            return _entitiesById.TryGetValue(entityId, out entity);
        }

        public void Clear()
        {
            ApplySnapshot(new WorldStateSnapshot());
        }

        private static WorldEntityState Normalize(WorldEntityState entity)
        {
            entity.EntityId = Math.Max(0, entity.EntityId);
            entity.EntityType = Math.Max(0, entity.EntityType);
            entity.HpMax = Math.Max(0, entity.HpMax);
            entity.HpCurrent = Math.Clamp(entity.HpCurrent, 0, entity.HpMax);
            entity.DisplayName = entity.DisplayName ?? string.Empty;
            return entity;
        }
    }
}
