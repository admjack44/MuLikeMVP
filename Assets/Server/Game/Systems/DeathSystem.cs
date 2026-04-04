using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class DeathSystem
    {
        public bool IsDead(Entity entity)
        {
            return entity.HpCurrent <= 0;
        }
    }
}
