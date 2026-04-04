using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Data.Catalogs
{
    [Serializable]
    public struct ItemDefinitionRecord
    {
        public int itemId;
        public string name;
        public ItemCategory category;
        public string type;
        public string subtype;
        public string family;
        public ItemRarity rarity;
        public int level;

        public bool stackable;
        public int maxStack;
        public bool twoHanded;

        public string icon;
        public ItemBasicStats basicStats;
        public ItemEquipSlot[] allowedEquipSlots;

        public ItemDefinition ToDefinition()
        {
            return new ItemDefinition
            {
                ItemId = itemId,
                Name = name,
                Category = category,
                Type = type,
                Subtype = subtype,
                Family = family,
                Rarity = rarity,
                Level = level,
                Stackable = stackable,
                MaxStack = maxStack,
                IsTwoHanded = twoHanded,
                Icon = icon,
                BasicStats = basicStats,
                AllowedEquipSlots = allowedEquipSlots != null ? new List<ItemEquipSlot>(allowedEquipSlots) : new List<ItemEquipSlot>()
            };
        }
    }

    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "MuLike/Catalogs/Item Definition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [SerializeField] private ItemDefinitionRecord definition;

        public ItemDefinitionRecord Definition => definition;

        public ItemDefinition ToDefinition()
        {
            return definition.ToDefinition();
        }
    }

    [CreateAssetMenu(fileName = "ItemCatalogDatabase", menuName = "MuLike/Catalogs/Item Catalog Database")]
    public sealed class ItemCatalogDatabase : ScriptableObject
    {
        [Tooltip("Optional standalone ItemDefinition assets.")]
        public List<ItemDefinitionAsset> itemAssets = new();

        [Tooltip("Inline records if you prefer one asset only.")]
        public List<ItemDefinitionRecord> inlineItems = new();

        public List<ItemDefinition> BuildDefinitions()
        {
            var items = new List<ItemDefinition>();

            for (int i = 0; i < itemAssets.Count; i++)
            {
                if (itemAssets[i] == null) continue;
                items.Add(itemAssets[i].ToDefinition());
            }

            for (int i = 0; i < inlineItems.Count; i++)
                items.Add(inlineItems[i].ToDefinition());

            return items;
        }
    }
}
