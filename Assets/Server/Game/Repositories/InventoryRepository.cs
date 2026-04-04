using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class InventoryRepository
    {
        private readonly Dictionary<int, Dictionary<int, int>> _storage = new();

        public Dictionary<int, int> Load(int characterId)
        {
            if (!_storage.TryGetValue(characterId, out var value))
            {
                value = new Dictionary<int, int>();
                _storage[characterId] = value;
            }

            return value;
        }
    }
}
