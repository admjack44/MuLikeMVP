namespace MuLike.Server.Game.Definitions
{
    public sealed class MonsterDropDefinition
    {
        public int ItemId { get; set; }
        public int ChancePercent { get; set; }
        public int MinQuantity { get; set; } = 1;
        public int MaxQuantity { get; set; } = 1;
    }
}
