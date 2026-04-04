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
    }
}
