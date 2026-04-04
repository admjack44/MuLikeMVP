namespace MuLike.Server.Game.Definitions
{
    public sealed class SkillDefinition
    {
        public int SkillId { get; set; }
        public string Name { get; set; }
        public int ManaCost { get; set; }
        public int CooldownMs { get; set; }
    }
}
