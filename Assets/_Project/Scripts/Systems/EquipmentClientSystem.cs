using System.Collections.Generic;

namespace MuLike.Systems
{
    /// <summary>
    /// Tracks currently equipped items per slot and notifies listeners on changes.
    /// </summary>
    public class EquipmentClientSystem
    {
        public enum EquipSlot
        {
            Helm, Armor, Gloves, Boots, Weapon, Shield, Ring, Pendant, Wings
        }

        private readonly Dictionary<EquipSlot, int> _equipped = new();

        public IReadOnlyDictionary<EquipSlot, int> Equipped => _equipped;

        public event System.Action OnEquipmentChanged;

        public void SetEquipped(EquipSlot slot, int itemId)
        {
            _equipped[slot] = itemId;
            OnEquipmentChanged?.Invoke();
        }

        public void Unequip(EquipSlot slot)
        {
            if (_equipped.Remove(slot))
                OnEquipmentChanged?.Invoke();
        }

        public bool TryGetEquipped(EquipSlot slot, out int itemId)
        {
            return _equipped.TryGetValue(slot, out itemId);
        }

        public void ApplySnapshot(IEnumerable<(EquipSlot slot, int itemId)> snapshot)
        {
            _equipped.Clear();
            foreach (var (slot, itemId) in snapshot)
                _equipped[slot] = itemId;

            OnEquipmentChanged?.Invoke();
        }
    }
}
