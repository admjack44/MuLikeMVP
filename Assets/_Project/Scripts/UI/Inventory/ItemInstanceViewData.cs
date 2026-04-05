using MuLike.Data.Catalogs;
using UnityEngine;

namespace MuLike.UI.Inventory
{
    public readonly struct ItemInstanceViewData
    {
        public readonly int SlotIndex;
        public readonly string InstanceId;
        public readonly int ItemId;
        public readonly string ItemName;
        public readonly ItemCategory Category;
        public readonly ItemRarity Rarity;
        public readonly int Quantity;
        public readonly int MaxStack;
        public readonly int EnhancementLevel;
        public readonly int DurabilityCurrent;
        public readonly int DurabilityMax;
        public readonly bool IsStackable;
        public readonly bool IsEquipped;
        public readonly bool IsEmpty;
        public readonly Sprite Icon;

        public ItemInstanceViewData(
            int slotIndex,
            string instanceId,
            int itemId,
            string itemName,
            ItemCategory category,
            ItemRarity rarity,
            int quantity,
            int maxStack,
            int enhancementLevel,
            int durabilityCurrent,
            int durabilityMax,
            bool isStackable,
            bool isEquipped,
            bool isEmpty,
            Sprite icon)
        {
            SlotIndex = slotIndex;
            InstanceId = instanceId ?? string.Empty;
            ItemId = Mathf.Max(0, itemId);
            ItemName = itemName ?? string.Empty;
            Category = category;
            Rarity = rarity;
            Quantity = Mathf.Max(0, quantity);
            MaxStack = Mathf.Max(1, maxStack);
            EnhancementLevel = Mathf.Max(0, enhancementLevel);
            DurabilityCurrent = Mathf.Max(0, durabilityCurrent);
            DurabilityMax = Mathf.Max(0, durabilityMax);
            IsStackable = isStackable;
            IsEquipped = isEquipped;
            IsEmpty = isEmpty;
            Icon = icon;
        }

        public static ItemInstanceViewData Empty(int slotIndex)
        {
            return new ItemInstanceViewData(
                slotIndex,
                string.Empty,
                0,
                string.Empty,
                ItemCategory.Unknown,
                ItemRarity.Common,
                0,
                1,
                0,
                0,
                0,
                false,
                false,
                true,
                null);
        }
    }
}
