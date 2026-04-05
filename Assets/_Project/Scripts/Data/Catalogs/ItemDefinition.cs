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

    public enum CharacterClassRestriction
    {
        Any,
        Warrior,
        Mage,
        Ranger,
        Paladin,
        DarkLord
    }

    public enum ItemStackRule
    {
        None,
        ByItemId,
        ByItemAndEnhancement
    }

    [Flags]
    public enum ExcellentOptionFlags
    {
        None = 0,
        BonusDamage = 1 << 0,
        BonusDefense = 1 << 1,
        BonusHp = 1 << 2,
        BonusCritChance = 1 << 3
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

    // Passive stat bonuses granted while the item is equipped (not base weapon/armor values).
    [Serializable]
    public struct ItemStatBonuses
    {
        public int AttackRate;
        public int Hp;
        public int Mana;
        public int SpellPower;
        public int MoveSpeed;
        public int DamageAbsorb;
        public int DamageBoost;
        public int PetDamage;
        public int PetDefense;
        public bool AutoLoot;

        public bool IsEmpty => AttackRate == 0 && Hp == 0 && Mana == 0 && SpellPower == 0
            && MoveSpeed == 0 && DamageAbsorb == 0 && DamageBoost == 0
            && PetDamage == 0 && PetDefense == 0 && !AutoLoot;
    }

    // Attribute requirements the character must meet to equip the item.
    [Serializable]
    public struct ItemStatRequirements
    {
        public int Strength;
        public int Agility;
        public int Energy;
        public int Command;

        public bool IsEmpty => Strength == 0 && Agility == 0 && Energy == 0 && Command == 0;
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
        public int RequiredLevel;
        public int SellValue;
        public ItemStackRule StackRule;

        public bool Stackable;
        public int MaxStack;
        public bool IsTwoHanded;

        public string Icon;
        public ItemBasicStats BasicStats;
        public ItemRestoreEffect Restore;
        public List<ItemEquipSlot> AllowedEquipSlots = new();
        public List<CharacterClassRestriction> AllowedClasses = new();
        public ExcellentOptionFlags AllowedExcellentOptions;
        public int MaxSockets;
        public bool AllowSockets;
        public ItemStatBonuses StatBonuses;
        public ItemStatRequirements StatRequirements;

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
                requiredLevel = RequiredLevel,
                twoHanded = IsTwoHanded,
                equipSlots = slotNames,
                classRestrictions = ToClassRestrictionNames(AllowedClasses),
                minDamage = BasicStats.MinDamage,
                maxDamage = BasicStats.MaxDamage,
                attackSpeed = BasicStats.AttackSpeed,
                magicPower = BasicStats.MagicPower,
                defense = BasicStats.Defense,
                blockRate = BasicStats.BlockRate,
                moveBonus = BasicStats.MoveBonus,
                stackable = Stackable,
                maxStack = MaxStack,
                stackRule = StackRule.ToString(),
                sellPrice = SellValue,
                excellentFlags = (int)AllowedExcellentOptions,
                allowSockets = AllowSockets,
                maxSockets = MaxSockets,
                icon = Icon,
                restore = new ItemRestoreDto
                {
                    hp = Restore.Hp,
                    mana = Restore.Mana
                },
                bonuses = StatBonuses.IsEmpty ? null : new ItemBonusesDto
                {
                    attackRate = StatBonuses.AttackRate,
                    hp = StatBonuses.Hp,
                    mana = StatBonuses.Mana,
                    spellPower = StatBonuses.SpellPower,
                    moveSpeed = StatBonuses.MoveSpeed,
                    damageAbsorb = StatBonuses.DamageAbsorb,
                    damageBoost = StatBonuses.DamageBoost,
                    petDamage = StatBonuses.PetDamage,
                    petDefense = StatBonuses.PetDefense,
                    autoLoot = StatBonuses.AutoLoot
                },
                requirements = StatRequirements.IsEmpty ? null : new ItemRequirementsDto
                {
                    strength = StatRequirements.Strength,
                    agility = StatRequirements.Agility,
                    energy = StatRequirements.Energy,
                    command = StatRequirements.Command
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
                RequiredLevel = dto.requiredLevel,
                Stackable = dto.stackable,
                MaxStack = dto.maxStack,
                StackRule = ParseStackRule(dto.stackRule, dto.stackable),
                IsTwoHanded = dto.twoHanded,
                Icon = dto.icon,
                SellValue = dto.sellPrice,
                AllowedExcellentOptions = ParseExcellentFlags(dto.excellentFlags),
                AllowSockets = dto.allowSockets,
                MaxSockets = dto.maxSockets,
                StatBonuses = dto.bonuses == null ? default : new ItemStatBonuses
                {
                    AttackRate = dto.bonuses.attackRate,
                    Hp = dto.bonuses.hp,
                    Mana = dto.bonuses.mana,
                    SpellPower = dto.bonuses.spellPower,
                    MoveSpeed = dto.bonuses.moveSpeed,
                    DamageAbsorb = dto.bonuses.damageAbsorb,
                    DamageBoost = dto.bonuses.damageBoost,
                    PetDamage = dto.bonuses.petDamage,
                    PetDefense = dto.bonuses.petDefense,
                    AutoLoot = dto.bonuses.autoLoot
                },
                StatRequirements = dto.requirements == null ? default : new ItemStatRequirements
                {
                    Strength = dto.requirements.strength,
                    Agility = dto.requirements.agility,
                    Energy = dto.requirements.energy,
                    Command = dto.requirements.command
                },
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
            definition.AllowedClasses = ParseClassRestrictions(dto.classRestrictions);
            return definition;
        }

        private static string[] ToClassRestrictionNames(List<CharacterClassRestriction> restrictions)
        {
            var list = restrictions ?? new List<CharacterClassRestriction>();
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                names[i] = list[i].ToString();

            return names;
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

        private static List<CharacterClassRestriction> ParseClassRestrictions(string[] rawClasses)
        {
            var parsed = new List<CharacterClassRestriction>();
            if (rawClasses == null || rawClasses.Length == 0)
            {
                parsed.Add(CharacterClassRestriction.Any);
                return parsed;
            }

            for (int i = 0; i < rawClasses.Length; i++)
            {
                if (Enum.TryParse(rawClasses[i], true, out CharacterClassRestriction parsedClass))
                    parsed.Add(parsedClass);
            }

            if (parsed.Count == 0)
                parsed.Add(CharacterClassRestriction.Any);

            return parsed;
        }

        private static ItemStackRule ParseStackRule(string rawRule, bool stackable)
        {
            if (Enum.TryParse(rawRule, true, out ItemStackRule parsedRule))
                return parsedRule;

            return stackable ? ItemStackRule.ByItemId : ItemStackRule.None;
        }

        private static ExcellentOptionFlags ParseExcellentFlags(int rawFlags)
        {
            if (rawFlags < 0)
                return ExcellentOptionFlags.None;

            return (ExcellentOptionFlags)rawFlags;
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
