using System;
using System.Collections.Generic;
using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.World
{
    public sealed class SpawnManager
    {
        private sealed class SpawnPointState
        {
            public int SpawnPointId { get; set; }
            public int MapId { get; set; }
            public MonsterDefinition Definition { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float RespawnSeconds { get; set; }
            public int? ActiveMonsterId { get; set; }
            public float RemainingRespawnSeconds { get; set; }
        }

        private int _nextEntityId = 1_000_000;
        private int _nextSpawnPointId = 1;
        private readonly Dictionary<int, SpawnPointState> _spawnPoints = new();
        private readonly Dictionary<int, int> _monsterToSpawnPoint = new();

        public int RegisterMonsterSpawnPoint(int mapId, MonsterDefinition definition, float x, float y, float z)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            int spawnPointId = _nextSpawnPointId++;
            _spawnPoints[spawnPointId] = new SpawnPointState
            {
                SpawnPointId = spawnPointId,
                MapId = mapId,
                Definition = definition,
                X = x,
                Y = y,
                Z = z,
                RespawnSeconds = definition.RespawnSeconds,
                ActiveMonsterId = null,
                RemainingRespawnSeconds = 0f
            };

            return spawnPointId;
        }

        public MonsterEntity SpawnMonster(MonsterDefinition definition, float x, float y, float z)
        {
            return new MonsterEntity(_nextEntityId++, definition, x, y, z);
        }

        public DropEntity SpawnDrop(int itemId, int quantity, float x, float y, float z)
        {
            return new DropEntity(_nextEntityId++, itemId, quantity, x, y, z);
        }

        public void SpawnInitialMonsters(WorldManager worldManager)
        {
            foreach (var state in _spawnPoints.Values)
            {
                TrySpawnAtPoint(worldManager, state);
            }
        }

        public void UpdateRespawns(float deltaTime, WorldManager worldManager)
        {
            foreach (var state in _spawnPoints.Values)
            {
                if (state.ActiveMonsterId.HasValue)
                    continue;

                state.RemainingRespawnSeconds -= deltaTime;
                if (state.RemainingRespawnSeconds > 0f)
                    continue;

                TrySpawnAtPoint(worldManager, state);
            }
        }

        public void NotifyMonsterDeath(MonsterEntity monster)
        {
            if (monster == null)
                return;

            if (!_monsterToSpawnPoint.TryGetValue(monster.Id, out int spawnPointId))
                return;

            _monsterToSpawnPoint.Remove(monster.Id);

            if (_spawnPoints.TryGetValue(spawnPointId, out var state))
            {
                state.ActiveMonsterId = null;
                state.RemainingRespawnSeconds = state.RespawnSeconds;
            }
        }

        private void TrySpawnAtPoint(WorldManager worldManager, SpawnPointState state)
        {
            if (!worldManager.TryGetMap(state.MapId, out var map))
                return;

            var monster = SpawnMonster(state.Definition, state.X, state.Y, state.Z);
            monster.ClearCombatState();
            map.AddEntity(monster);

            state.ActiveMonsterId = monster.Id;
            state.RemainingRespawnSeconds = 0f;
            _monsterToSpawnPoint[monster.Id] = state.SpawnPointId;
        }
    }
}
