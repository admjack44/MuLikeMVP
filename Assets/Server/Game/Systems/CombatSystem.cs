using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class CombatSystem
    {
        public int ApplyDamage(Entity source, Entity target, int damage)
        {
            if (source == null || target == null) return 0;
            return target.ApplyDamage(damage);
        }
    }
}
