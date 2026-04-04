using UnityEngine;

namespace MuLike.UI.Inventory
{
    public readonly struct InventorySlotViewData
    {
        public readonly int SlotIndex;
        public readonly int ItemId;
        public readonly string ItemName;
        public readonly int Quantity;
        public readonly Sprite Icon;
        public readonly bool IsEmpty;

        public InventorySlotViewData(int slotIndex, int itemId, string itemName, int quantity, Sprite icon, bool isEmpty)
        {
            SlotIndex = slotIndex;
            ItemId = itemId;
            ItemName = itemName;
            Quantity = quantity;
            Icon = icon;
            IsEmpty = isEmpty;
        }
    }
}
