using System.Collections.Generic;

namespace MuLike.Server.Game.Systems
{
    public sealed class InventorySystem
    {
        private readonly Dictionary<int, Dictionary<int, int>> _inventories = new();

        public void AddItem(int characterId, int itemId, int amount)
        {
            if (!_inventories.TryGetValue(characterId, out var inv))
            {
                inv = new Dictionary<int, int>();
                _inventories[characterId] = inv;
            }

            if (!inv.ContainsKey(itemId)) inv[itemId] = 0;
            inv[itemId] += amount;
        }
    }
}
