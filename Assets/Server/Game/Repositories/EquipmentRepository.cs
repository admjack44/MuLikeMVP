using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class EquipmentRepository
    {
        private readonly Dictionary<int, Dictionary<string, int>> _storage = new();

        public Dictionary<string, int> Load(int characterId)
        {
            if (!_storage.TryGetValue(characterId, out var value))
            {
                value = new Dictionary<string, int>();
                _storage[characterId] = value;
            }

            return value;
        }
    }
}
