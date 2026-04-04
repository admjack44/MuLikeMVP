using System.Collections.Generic;

namespace MuLike.Server.Game.Systems
{
    public sealed class EquipmentSystem
    {
        private readonly Dictionary<int, Dictionary<string, int>> _equipment = new();

        public void Equip(int characterId, string slot, int itemId)
        {
            if (!_equipment.TryGetValue(characterId, out var slots))
            {
                slots = new Dictionary<string, int>();
                _equipment[characterId] = slots;
            }

            slots[slot] = itemId;
        }
    }
}
