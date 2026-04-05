using System;
using System.Collections.Generic;
using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Repositories;

namespace MuLike.Server.Game.Systems
{
    public sealed class EquipmentSystem
    {
        private readonly ItemDatabase _itemDatabase;

        public EquipmentSystem(ItemDatabase itemDatabase)
        {
            _itemDatabase = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
        }

        public bool TryEquip(EquipmentRepository repository, int characterId, int playerLevel, string playerClass, string slotName, InventoryItemRecord item, out EquippedItemRecord replaced)
        {
            replaced = null;
            if (item == null || string.IsNullOrWhiteSpace(slotName))
                return false;

            if (!_itemDatabase.TryGet(item.ItemId, out var definition))
                return false;

            if (definition.IsStackable)
                return false;

            if (playerLevel < definition.RequiredLevel)
                return false;

            if (!IsClassAllowed(definition, playerClass))
                return false;

            if (!CanEquipInSlot(definition, slotName))
                return false;

            var equipment = repository.Load(characterId);
            if (equipment.TryGetValue(slotName, out var existing))
            {
                replaced = existing;
            }

            equipment[slotName] = new EquippedItemRecord
            {
                ItemInstanceId = item.ItemInstanceId,
                ItemId = item.ItemId,
                Options = item.Options
            };

            return true;
        }

        public bool TryUnequip(EquipmentRepository repository, int characterId, string slotName, out EquippedItemRecord equipped)
        {
            equipped = null;
            if (string.IsNullOrWhiteSpace(slotName))
                return false;

            var equipment = repository.Load(characterId);
            if (!equipment.TryGetValue(slotName, out equipped))
                return false;

            equipment.Remove(slotName);
            return true;
        }

        public (int attack, int defense, int hp) BuildEquipmentBonus(EquipmentRepository repository, int characterId)
        {
            var equipment = repository.Load(characterId);
            int attack = 0;
            int defense = 0;
            int hp = 0;

            foreach (var entry in equipment.Values)
            {
                if (!_itemDatabase.TryGet(entry.ItemId, out var definition))
                    continue;

                attack += definition.BonusAttack;
                defense += definition.BonusDefense;
                hp += definition.BonusHp;

                int excellentFlags = entry.Options?.ExcellentFlags ?? 0;
                if ((excellentFlags & (int)ExcellentOptionFlags.BonusDamage) != 0)
                    attack += 5;
                if ((excellentFlags & (int)ExcellentOptionFlags.BonusDefense) != 0)
                    defense += 5;
                if ((excellentFlags & (int)ExcellentOptionFlags.BonusHp) != 0)
                    hp += 25;
            }

            return (attack, defense, hp);
        }

        private static bool CanEquipInSlot(ItemDefinition definition, string slotName)
        {
            string requiredSlot = definition.EquipSlot;
            if (string.IsNullOrWhiteSpace(requiredSlot))
            {
                requiredSlot = MapTypeToDefaultSlot(definition.Type);
            }

            if (string.IsNullOrWhiteSpace(requiredSlot))
                return false;

            if (string.Equals(requiredSlot, slotName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Rings are interchangeable across left/right slots.
            bool isRingItem = string.Equals(requiredSlot, "RingLeft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requiredSlot, "RingRight", StringComparison.OrdinalIgnoreCase);
            bool isRingSlot = string.Equals(slotName, "RingLeft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(slotName, "RingRight", StringComparison.OrdinalIgnoreCase);

            return isRingItem && isRingSlot;
        }

        private static string MapTypeToDefaultSlot(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return string.Empty;

            if (string.Equals(itemType, "weapon", StringComparison.OrdinalIgnoreCase))
                return "WeaponMain";

            if (string.Equals(itemType, "shield", StringComparison.OrdinalIgnoreCase))
                return "WeaponOffhand";

            if (string.Equals(itemType, "accessory", StringComparison.OrdinalIgnoreCase))
                return "Pendant";

            return string.Empty;
        }

        private static bool IsClassAllowed(ItemDefinition definition, string playerClass)
        {
            if (definition.ClassRestrictions == null || definition.ClassRestrictions.Length == 0)
                return true;

            if (string.IsNullOrWhiteSpace(playerClass))
                playerClass = "Warrior";

            for (int i = 0; i < definition.ClassRestrictions.Length; i++)
            {
                string restriction = definition.ClassRestrictions[i];
                if (string.Equals(restriction, "Any", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(restriction, playerClass, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
