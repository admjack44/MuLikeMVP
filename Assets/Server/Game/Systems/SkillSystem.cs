namespace MuLike.Server.Game.Systems
{
    public sealed class SkillSystem
    {
        public bool CanCast(int manaCurrent, int manaCost)
        {
            return manaCurrent >= manaCost;
        }
    }
}
