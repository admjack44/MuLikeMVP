using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Entities;

namespace MuLike.Server.Game.World
{
    public sealed class SpawnManager
    {
        private int _nextEntityId = 1;

        public MonsterEntity SpawnMonster(MonsterDefinition definition, float x, float y, float z)
        {
            return new MonsterEntity(_nextEntityId++, definition.MonsterId, definition.Name, x, y, z);
        }

        public DropEntity SpawnDrop(int itemId, int quantity, float x, float y, float z)
        {
            return new DropEntity(_nextEntityId++, itemId, quantity, x, y, z);
        }
    }
}
