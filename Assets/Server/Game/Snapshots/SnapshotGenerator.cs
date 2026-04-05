using System;
using System.Collections.Generic;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.World;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Game.Snapshots
{
    public sealed class SnapshotGenerator
    {
        private readonly WorldManager _worldManager;
        private readonly IAreaOfInterest _aoi;
        private uint _sequenceNumber = 0;

        public SnapshotGenerator(WorldManager worldManager, IAreaOfInterest aoi)
        {
            _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
            _aoi = aoi ?? throw new ArgumentNullException(nameof(aoi));
        }

        public SnapshotData CreateFullSnapshot(int observerEntityId, int mapId = 1)
        {
            var entities = new List<SnapshotEntityData>();

            if (!_worldManager.TryGetMap(mapId, out MapInstance map))
                return new SnapshotData
                {
                    SequenceNumber = ++_sequenceNumber,
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Entities = entities
                };

            IReadOnlyCollection<Entity> allEntities = map.GetEntities();
            foreach (Entity entity in allEntities)
            {
                if (entity.Id == observerEntityId)
                    continue;

                if (!_aoi.IsInView(observerEntityId, entity))
                    continue;

                SnapshotEntityData snapshot = CreateSnapshotEntity(entity);
                entities.Add(snapshot);
            }

            return new SnapshotData
            {
                SequenceNumber = ++_sequenceNumber,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Entities = entities
            };
        }

        public SnapshotData CreateDeltaSnapshot(
            int observerEntityId,
            Dictionary<int, SnapshotEntityData> lastSnapshot,
            int mapId = 1)
        {
            var entities = new List<SnapshotEntityData>();

            if (!_worldManager.TryGetMap(mapId, out MapInstance map))
                return new SnapshotData
                {
                    SequenceNumber = ++_sequenceNumber,
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Entities = entities
                };

            IReadOnlyCollection<Entity> allEntities = map.GetEntities();
            foreach (Entity entity in allEntities)
            {
                if (entity.Id == observerEntityId)
                    continue;

                bool inView = _aoi.IsInView(observerEntityId, entity);

                if (!inView)
                {
                    if (lastSnapshot?.ContainsKey(entity.Id) == true)
                    {
                        var despawn = new SnapshotEntityData { EntityId = entity.Id, EntityType = 0, IsAlive = false };
                        entities.Add(despawn);
                    }
                    continue;
                }

                SnapshotEntityData current = CreateSnapshotEntity(entity);

                if (lastSnapshot == null || !lastSnapshot.TryGetValue(entity.Id, out SnapshotEntityData last))
                {
                    entities.Add(current);
                    continue;
                }

                if (HasChanged(last, current))
                {
                    entities.Add(current);
                }
            }

            var snapshot = new SnapshotData
            {
                SequenceNumber = ++_sequenceNumber,
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Entities = entities
            };

            return snapshot;
        }

        private SnapshotEntityData CreateSnapshotEntity(Entity entity)
        {
            byte entityType = DetermineEntityType(entity);
            string displayName = GetEntityDisplayName(entity);
            int ownerEntityId = GetOwnerEntityId(entity);

            return new SnapshotEntityData
            {
                EntityId = entity.Id,
                EntityType = entityType,
                PosX = entity.X,
                PosY = entity.Y,
                PosZ = entity.Z,
                RotationY = 0f,
                HpCurrent = entity.HpCurrent,
                HpMax = entity.HpMax,
                IsAlive = !entity.IsDead(),
                DisplayName = displayName,
                OwnerEntityId = ownerEntityId
            };
        }

        private bool HasChanged(SnapshotEntityData last, SnapshotEntityData current)
        {
            const float positionThreshold = 0.01f;
            const float rotationThreshold = 1f;

            bool posChanged = 
                Math.Abs(last.PosX - current.PosX) > positionThreshold ||
                Math.Abs(last.PosY - current.PosY) > positionThreshold ||
                Math.Abs(last.PosZ - current.PosZ) > positionThreshold;

            bool healthChanged = last.HpCurrent != current.HpCurrent || last.HpMax != current.HpMax;
            bool stateChanged = last.IsAlive != current.IsAlive;

            return posChanged || healthChanged || stateChanged;
        }

        private byte DetermineEntityType(Entity entity)
        {
            if (entity is PlayerEntity)
                return 1;
            if (entity is MonsterEntity)
                return 2;
            if (entity is PetEntity)
                return 3;
            if (entity is DropEntity)
                return 4;

            return 0;
        }

        private string GetEntityDisplayName(Entity entity)
        {
            if (entity is PlayerEntity player)
                return player.Name;
            if (entity is MonsterEntity monster)
                return monster.Name;
            if (entity is PetEntity pet)
                return $"Pet_{pet.Id}";
            if (entity is DropEntity drop)
                return $"Drop_{drop.Id}";

            return $"Entity_{entity.Id}";
        }

        private int GetOwnerEntityId(Entity entity)
        {
            if (entity is PetEntity pet)
                return pet.OwnerPlayerId;

            return 0;
        }
    }
}
