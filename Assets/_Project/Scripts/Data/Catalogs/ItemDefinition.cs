using System;
using System.Collections.Generic;
using MuLike.Data.DTO;

namespace MuLike.Data.Catalogs
{
    public enum ItemCategory
    {
        Unknown,
        Weapon,
        Shield,
        Armor,
        Accessory,
        Consumable,
        Material,
        Quest,
        Wings,
        Pet,
        Costume
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    public enum ItemEquipSlot
    {
        None,
        Helm,
        Armor,
        Pants,
        Gloves,
        Boots,
        WeaponMain,
        WeaponOffhand,
        RingLeft,
        RingRight,
        Pendant,
        Wings,
        Pet
    }

    [Serializable]
    public struct ItemBasicStats
    {
        public int MinDamage;
        public int MaxDamage;
        public int AttackSpeed;
        public int MagicPower;
        public int Defense;
        public int BlockRate;
        public int MoveBonus;

        public bool IsEmpty => MinDamage == 0
            && MaxDamage == 0
            && AttackSpeed == 0
            && MagicPower == 0
            && Defense == 0
            && BlockRate == 0
            && MoveBonus == 0;
    }

    [Serializable]
    public struct ItemRestoreEffect
    {
        public int Hp;
        public int Mana;

        public bool IsEmpty => Hp == 0 && Mana == 0;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public int ItemId;
        public string Name;
        public ItemCategory Category;
        public string Type;
        public string Subtype;
        public string Family;
        public ItemRarity Rarity;
        public int Level;

        public bool Stackable;
        public int MaxStack;
        public bool IsTwoHanded;

        public string Icon;
        public ItemBasicStats BasicStats;
        public ItemRestoreEffect Restore;
        public List<ItemEquipSlot> AllowedEquipSlots = new();

        public bool CanEquipIn(ItemEquipSlot slot)
        {
            if (AllowedEquipSlots == null || AllowedEquipSlots.Count == 0)
                return false;

            return AllowedEquipSlots.Contains(slot);
        }

        public ItemDto ToDto()
        {
            var slotNames = new string[AllowedEquipSlots?.Count ?? 0];
            for (int i = 0; i < slotNames.Length; i++)
                slotNames[i] = AllowedEquipSlots[i].ToString();

            return new ItemDto
            {
                id = ItemId,
                name = Name,
                category = Category.ToString(),
                type = Type,
                subtype = Subtype,
                family = Family,
                rarity = Rarity.ToString(),
                level = Level,
                twoHanded = IsTwoHanded,
                equipSlots = slotNames,
                minDamage = BasicStats.MinDamage,
                maxDamage = BasicStats.MaxDamage,
                attackSpeed = BasicStats.AttackSpeed,
                magicPower = BasicStats.MagicPower,
                defense = BasicStats.Defense,
                blockRate = BasicStats.BlockRate,
                moveBonus = BasicStats.MoveBonus,
                stackable = Stackable,
                maxStack = MaxStack,
                icon = Icon,
                restore = new ItemRestoreDto
                {
                    hp = Restore.Hp,
                    mana = Restore.Mana
                }
            };
        }

        public static ItemDefinition FromDto(ItemDto dto)
        {
            var definition = new ItemDefinition
            {
                ItemId = dto.id,
                Name = dto.name,
                Category = ParseCategory(dto.category, dto.type),
                Type = dto.type,
                Subtype = dto.subtype,
                Family = dto.family,
                Rarity = ParseRarity(dto.rarity),
                Level = dto.level,
                Stackable = dto.stackable,
                MaxStack = dto.maxStack,
                IsTwoHanded = dto.twoHanded,
                Icon = dto.icon,
                BasicStats = new ItemBasicStats
                {
                    MinDamage = dto.minDamage,
                    MaxDamage = dto.maxDamage,
                    AttackSpeed = dto.attackSpeed,
                    MagicPower = dto.magicPower,
                    Defense = dto.defense,
                    BlockRate = dto.blockRate,
                    MoveBonus = dto.moveBonus
                },
                Restore = new ItemRestoreEffect
                {
                    Hp = dto.restore != null ? dto.restore.hp : 0,
                    Mana = dto.restore != null ? dto.restore.mana : 0
                }
            };

            definition.AllowedEquipSlots = ParseEquipSlots(dto.equipSlots);
            return definition;
        }

        private static ItemCategory ParseCategory(string rawCategory, string fallbackType)
        {
            if (Enum.TryParse(rawCategory, true, out ItemCategory parsedCategory))
                return parsedCategory;

            if (string.IsNullOrWhiteSpace(fallbackType))
                return ItemCategory.Unknown;

            string normalized = fallbackType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "weapon" => ItemCategory.Weapon,
                "shield" => ItemCategory.Shield,
                "armor" => ItemCategory.Armor,
                "accessory" => ItemCategory.Accessory,
                "consumable" => ItemCategory.Consumable,
                "material" => ItemCategory.Material,
                "quest" => ItemCategory.Quest,
                "special" => ItemCategory.Costume,
                _ => ItemCategory.Unknown
            };
        }

        private static ItemRarity ParseRarity(string rawRarity)
        {
            if (Enum.TryParse(rawRarity, true, out ItemRarity parsedRarity))
                return parsedRarity;

            return ItemRarity.Common;
        }

        private static List<ItemEquipSlot> ParseEquipSlots(string[] rawSlots)
        {
            var parsedSlots = new List<ItemEquipSlot>();
            if (rawSlots == null)
                return parsedSlots;

            for (int i = 0; i < rawSlots.Length; i++)
            {
                if (Enum.TryParse(rawSlots[i], true, out ItemEquipSlot slot) && slot != ItemEquipSlot.None)
                    parsedSlots.Add(slot);
            }

            return parsedSlots;
        }
    }

    public enum ItemCatalogValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct ItemCatalogValidationIssue
    {
        public readonly ItemCatalogValidationSeverity Severity;
        public readonly int ItemId;
        public readonly string Message;

        public ItemCatalogValidationIssue(ItemCatalogValidationSeverity severity, int itemId, string message)
        {
            Severity = severity;
            ItemId = itemId;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{Severity}] Item {ItemId}: {Message}";
        }
    }
}
