using MuLike.Server.Game.Definitions;

namespace MuLike.Server.Game.Entities
{
    public sealed class MonsterEntity : Entity
    {
        public int MonsterTypeId { get; }
        public string Name { get; }
        public int? TargetId { get; set; }
        public float SpawnX { get; }
        public float SpawnY { get; }
        public float SpawnZ { get; }
        public float AggroRadius { get; }
        public float ChaseRadius { get; }
        public float LeashRadius { get; }
        public float AttackRange { get; }
        public float MoveSpeed { get; }
        public int ExpReward { get; }
        public MonsterDropDefinition[] Drops { get; }
        public int? LastDamagedByPlayerId { get; private set; }

        public MonsterEntity(int id, MonsterDefinition definition, float x, float y, float z)
            : base(id, x, y, z)
        {
            MonsterTypeId = definition.MonsterId;
            Name = definition.Name;
            SpawnX = x;
            SpawnY = y;
            SpawnZ = z;
            AggroRadius = definition.AggroRadius;
            ChaseRadius = definition.ChaseRadius;
            LeashRadius = definition.LeashRadius;
            AttackRange = definition.AttackRange;
            MoveSpeed = definition.MoveSpeed;
            ExpReward = definition.ExpReward;
            Drops = definition.Drops ?? System.Array.Empty<MonsterDropDefinition>();

            Attack = definition.Attack;
            Defense = definition.Defense;
            CriticalChance = 0.05f;
            HitChance = 0.85f;
            AttackSpeed = 0.8f;
            HpMax = definition.HpMax;
            HpCurrent = HpMax;
        }

        public void RegisterDamageFromPlayer(int playerId)
        {
            LastDamagedByPlayerId = playerId;
        }

        public void ClearCombatState()
        {
            TargetId = null;
            LastDamagedByPlayerId = null;
        }

        public void ReturnToSpawn()
        {
            SetPosition(SpawnX, SpawnY, SpawnZ);
            ClearCombatState();
        }
    }
}
