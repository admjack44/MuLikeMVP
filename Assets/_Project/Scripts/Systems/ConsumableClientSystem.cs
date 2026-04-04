using System;
using MuLike.Data.Catalogs;

namespace MuLike.Systems
{
    /// <summary>
    /// Applies consumable item effects from inventory onto player resources.
    /// </summary>
    public sealed class ConsumableClientSystem
    {
        private readonly CatalogResolver _catalogResolver;
        private readonly InventoryClientSystem _inventorySystem;
        private readonly StatsClientSystem _statsSystem;

        public event Action<int, int> OnConsumableUsed;

        public ConsumableClientSystem(
            CatalogResolver catalogResolver,
            InventoryClientSystem inventorySystem,
            StatsClientSystem statsSystem)
        {
            _catalogResolver = catalogResolver;
            _inventorySystem = inventorySystem;
            _statsSystem = statsSystem;
        }

        public bool TryUseBySlot(int slotIndex, out string error)
        {
            error = string.Empty;

            if (_inventorySystem == null || _statsSystem == null || _catalogResolver == null)
            {
                error = "Consumable system dependencies are missing.";
                return false;
            }

            if (!_inventorySystem.TryGetSlot(slotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
            {
                error = "Slot is empty.";
                return false;
            }

            if (!_catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition definition))
            {
                error = $"Item definition not found for itemId {slot.ItemId}.";
                return false;
            }

            if (!IsUsableConsumable(definition))
            {
                error = $"Item {slot.ItemId} is not a usable consumable.";
                return false;
            }

            if (!_inventorySystem.TryConsumeFromSlot(slot.SlotIndex, 1, out _, out error))
                return false;

            ApplyRestore(definition.Restore);
            OnConsumableUsed?.Invoke(definition.ItemId, slot.SlotIndex);
            return true;
        }

        public bool TryUseFirstRestorative(out int usedSlotIndex, out int usedItemId, out string error)
        {
            usedSlotIndex = -1;
            usedItemId = 0;
            error = string.Empty;

            if (_inventorySystem == null)
            {
                error = "Inventory system is not available.";
                return false;
            }

            for (int i = 0; i < _inventorySystem.Slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = _inventorySystem.Slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!_catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition definition))
                    continue;

                if (!IsUsableConsumable(definition))
                    continue;

                if (!TryUseBySlot(slot.SlotIndex, out error))
                    return false;

                usedSlotIndex = slot.SlotIndex;
                usedItemId = slot.ItemId;
                return true;
            }

            error = "No restorative consumable item found in inventory.";
            return false;
        }

        private static bool IsUsableConsumable(ItemDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.Category != ItemCategory.Consumable)
                return false;

            if (definition.Restore.IsEmpty)
                return false;

            return true;
        }

        private void ApplyRestore(ItemRestoreEffect restore)
        {
            StatsClientSystem.PlayerStatsSnapshot snapshot = _statsSystem.Snapshot;

            int hpTarget = snapshot.Resources.Hp.Current + Math.Max(0, restore.Hp);
            int manaTarget = snapshot.Resources.Mana.Current + Math.Max(0, restore.Mana);

            _statsSystem.ApplyDelta(new StatsClientSystem.PlayerStatsDelta
            {
                HasHp = true,
                HpCurrent = hpTarget,
                HpMax = snapshot.Resources.Hp.Max,
                HasMana = true,
                ManaCurrent = manaTarget,
                ManaMax = snapshot.Resources.Mana.Max
            });
        }
    }
}
