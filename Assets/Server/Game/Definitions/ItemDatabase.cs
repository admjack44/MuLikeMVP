using System.Collections.Generic;
using MuLike.Shared.Items;

namespace MuLike.Server.Game.Definitions
{
    public sealed class ItemDatabase
    {
        private readonly Dictionary<int, ItemDefinition> _items = new();

        public int CatalogVersion => ItemCatalogSyncPolicy.CatalogVersion;
        public string LayoutId => ItemCatalogSyncPolicy.LayoutId;
        public int Count => _items.Count;

        public ItemDatabase()
        {
            SeedDefaults();
        }

        public bool TryGet(int itemId, out ItemDefinition definition)
        {
            return _items.TryGetValue(itemId, out definition);
        }

        /// <summary>
        /// Replaces all entries with the provided definitions.
        /// Call from the server composition root after loading the shared catalog JSON.
        /// </summary>
        public void Populate(IReadOnlyList<ItemDefinition> definitions)
        {
            if (definitions == null) return;
            _items.Clear();
            for (int i = 0; i < definitions.Count; i++)
            {
                ItemDefinition def = definitions[i];
                if (def != null)
                    _items[def.ItemId] = def;
            }
        }

        /// <summary>
        /// Adds or replaces a single definition. Use for incremental updates or test overrides.
        /// </summary>
        public void Register(ItemDefinition definition)
        {
            if (definition != null)
                _items[definition.ItemId] = definition;
        }

        private void SeedDefaults()
        {
            // Materials (range 1000–1999)
            _items[1001] = new ItemDefinition
            {
                ItemId = 1001,
                Name = "Spider Silk",
                Type = "material",
                Rarity = 1,
                RequiredLevel = 1,
                ClassRestrictions = new[] { "Any" },
                IsStackable = true,
                MaxStack = 99,
                StackRule = ItemStackRule.ByItemId,
                SellValue = 20
            };

            _items[1002] = new ItemDefinition
            {
                ItemId = 1002,
                Name = "Goblin Fang",
                Type = "material",
                Rarity = 1,
                RequiredLevel = 1,
                ClassRestrictions = new[] { "Any" },
                IsStackable = true,
                MaxStack = 99,
                StackRule = ItemStackRule.ByItemId,
                SellValue = 25
            };

            // Accessories (range 1000–1999, pendants etc.)
            _items[1003] = new ItemDefinition
            {
                ItemId = 1003,
                Name = "Old Pendant",
                Type = "accessory",
                Rarity = 2,
                RequiredLevel = 3,
                ClassRestrictions = new[] { "Any" },
                IsStackable = false,
                MaxStack = 1,
                StackRule = ItemStackRule.None,
                EquipSlot = "Pendant",
                EquipSlots = new[] { "Pendant" },
                BonusAttack = 2,
                BonusDefense = 1,
                BonusHp = 5,
                AllowedExcellentOptions = ExcellentOptionFlags.BonusHp | ExcellentOptionFlags.BonusDefense,
                AllowSockets = false,
                MaxSockets = 0,
                SellValue = 200
            };

            // Equipment (range 3000–7999)
            _items[3001] = new ItemDefinition
            {
                ItemId = 3001,
                Name = "Short Sword",
                Type = "weapon",
                Rarity = 2,
                RequiredLevel = 2,
                ClassRestrictions = new[] { "Warrior", "DarkLord" },
                IsStackable = false,
                MaxStack = 1,
                StackRule = ItemStackRule.None,
                EquipSlot = "WeaponMain",
                EquipSlots = new[] { "WeaponMain" },
                MinDamage = 4,
                MaxDamage = 8,
                AttackSpeed = 10,
                RequiredStrength = 20,
                BonusAttack = 6,
                AllowedExcellentOptions = ExcellentOptionFlags.BonusDamage | ExcellentOptionFlags.BonusCritChance,
                AllowSockets = true,
                MaxSockets = 2,
                SellValue = 350
            };
        }
    }
}
