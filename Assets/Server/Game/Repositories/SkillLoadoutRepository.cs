using System.Collections.Generic;

namespace MuLike.Server.Game.Repositories
{
    public sealed class SkillLoadoutRepository
    {
        private readonly Dictionary<int, Dictionary<int, int>> _storage = new();

        public Dictionary<int, int> Load(int characterId)
        {
            if (!_storage.TryGetValue(characterId, out Dictionary<int, int> value))
            {
                value = new Dictionary<int, int>();
                _storage[characterId] = value;
            }

            return value;
        }

        public void Replace(int characterId, IReadOnlyDictionary<int, int> loadout)
        {
            Dictionary<int, int> slots = Load(characterId);
            slots.Clear();

            if (loadout == null)
                return;

            foreach (KeyValuePair<int, int> entry in loadout)
                slots[entry.Key] = entry.Value;
        }
    }
}
