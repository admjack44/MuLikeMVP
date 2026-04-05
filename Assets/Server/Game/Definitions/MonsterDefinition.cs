namespace MuLike.Server.Game.Definitions
{
    public sealed class MonsterDefinition
    {
        public int MonsterId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int HpMax { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public float AggroRadius { get; set; } = 10f;
        public float ChaseRadius { get; set; } = 20f;
        public float LeashRadius { get; set; } = 25f;
        public float AttackRange { get; set; } = 2f;
        public float MoveSpeed { get; set; } = 3f;
        public float RespawnSeconds { get; set; } = 8f;
        public int ExpReward { get; set; } = 25;
        public MonsterDropDefinition[] Drops { get; set; } = System.Array.Empty<MonsterDropDefinition>();
    }
}
