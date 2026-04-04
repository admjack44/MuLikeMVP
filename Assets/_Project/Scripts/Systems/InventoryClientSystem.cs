using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Systems
{
    /// <summary>
    /// Maintains the client-side inventory state and notifies UI on changes.
    /// </summary>
    public class InventoryClientSystem
    {
        public struct ItemSlot
        {
            public int SlotIndex;
            public int ItemId;
            public int Quantity;
        }

        private readonly List<ItemSlot> _slots = new();

        public IReadOnlyList<ItemSlot> Slots => _slots;

        public event System.Action OnInventoryChanged;

        public void ApplySnapshot(IEnumerable<ItemSlot> slots)
        {
            _slots.Clear();
            _slots.AddRange(slots);
            OnInventoryChanged?.Invoke();
        }

        public void UpdateSlot(ItemSlot slot)
        {
            int index = _slots.FindIndex(s => s.SlotIndex == slot.SlotIndex);
            if (index >= 0) _slots[index] = slot;
            else _slots.Add(slot);

            OnInventoryChanged?.Invoke();
        }

        public void RemoveSlot(int slotIndex)
        {
            _slots.RemoveAll(s => s.SlotIndex == slotIndex);
            OnInventoryChanged?.Invoke();
        }
    }
}
