using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.Systems
{
    public sealed class MovementSystem
    {
        public void Move(Entity entity, float x, float y, float z)
        {
            entity.SetPosition(x, y, z);
        }
    }
}
