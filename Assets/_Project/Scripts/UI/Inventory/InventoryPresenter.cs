using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Data.Catalogs;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.Inventory
{
    /// <summary>
    /// Orchestrates inventory system state into a visual grid and processes drag/drop intents.
    /// </summary>
    public sealed class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly InventoryEquipmentService _inventoryService;
        private readonly LootPickupSystem _lootPickup;
        private readonly InventoryClientSystem _inventorySystem;
        private readonly EquipmentClientSystem _equipmentSystem;
        private readonly CatalogResolver _catalogResolver;
        private readonly Dictionary<string, Sprite> _iconCache = new();

        private readonly CancellationTokenSource _lifetimeCts = new();
        private int _selectedSlotIndex = -1;
        private bool _isBusy;

        public InventoryPresenter(
            InventoryView view,
            InventoryEquipmentService inventoryService,
            LootPickupSystem lootPickup,
            InventoryClientSystem inventorySystem,
            EquipmentClientSystem equipmentSystem,
            CatalogResolver catalogResolver)
        {
            _view = view;
            _inventoryService = inventoryService;
            _lootPickup = lootPickup;
            _inventorySystem = inventorySystem;
            _equipmentSystem = equipmentSystem;
            _catalogResolver = catalogResolver;
        }

        public void Bind()
        {
            _view.SlotDropRequested += HandleSlotDropRequested;
            _view.SlotTapped += HandleSlotTapped;
            _view.EquipRequested += HandleEquipRequested;
            _view.UnequipRequested += HandleUnequipRequested;
            _view.DropRequested += HandleDropRequested;
            _view.AutoPickupToggled += HandleAutoPickupToggled;

            if (_inventorySystem != null)
            {
                _inventorySystem.OnInventoryChanged += HandleInventoryChanged;
                _inventorySystem.OnQuickSlotsChanged += HandleQuickSlotsChanged;
            }

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

            if (_inventoryService != null)
            {
                _inventoryService.OperationFailed += HandleOperationFailed;
                _inventoryService.OperationSucceeded += HandleOperationSucceeded;
            }

            if (_lootPickup != null)
                _lootPickup.PickupMessage += HandlePickupMessage;

            RenderInventory();
            PreparePotionQuickSlots();
            _view.SetStatus("Inventory ready.");
            _view.SetTooltip("Tap an item slot to inspect.");
            _view.SetPowerScore(_inventoryService != null ? _inventoryService.CalculatePowerScore() : 0);
        }

        public void Unbind()
        {
            _view.SlotDropRequested -= HandleSlotDropRequested;
            _view.SlotTapped -= HandleSlotTapped;
            _view.EquipRequested -= HandleEquipRequested;
            _view.UnequipRequested -= HandleUnequipRequested;
            _view.DropRequested -= HandleDropRequested;
            _view.AutoPickupToggled -= HandleAutoPickupToggled;

            if (_inventorySystem != null)
            {
                _inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
                _inventorySystem.OnQuickSlotsChanged -= HandleQuickSlotsChanged;
            }

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

            if (_inventoryService != null)
            {
                _inventoryService.OperationFailed -= HandleOperationFailed;
                _inventoryService.OperationSucceeded -= HandleOperationSucceeded;
            }

            if (_lootPickup != null)
                _lootPickup.PickupMessage -= HandlePickupMessage;

            _lifetimeCts.Cancel();
        }

        private void HandleInventoryChanged()
        {
            PreparePotionQuickSlots();
            RenderInventory();
        }

        private void HandleQuickSlotsChanged()
        {
            RenderInventory();
        }

        private void HandleEquipmentChanged()
        {
            _view.SetPowerScore(_inventoryService != null ? _inventoryService.CalculatePowerScore() : 0);
        }

        private void HandleSlotDropRequested(int fromSlotIndex, int toSlotIndex)
        {
            if (_isBusy)
                return;

            if (_inventorySystem == null)
            {
                _view.SetStatus("Inventory system missing.");
                return;
            }

            if (!_inventorySystem.TryGetSlot(fromSlotIndex, out InventoryClientSystem.InventorySlot fromSlot) || fromSlot.IsEmpty)
            {
                _view.SetStatus("Source slot is empty.");
                return;
            }

            _inventorySystem.TryGetSlot(toSlotIndex, out InventoryClientSystem.InventorySlot toSlot);

            if (toSlot.IsEmpty)
            {
                bool moved = _inventorySystem.TryMoveItem(fromSlotIndex, toSlotIndex, fromSlot.Quantity, out string moveError);
                _view.SetStatus(moved ? "Item moved." : moveError);
                return;
            }

            if (fromSlot.CanStackWith(toSlot))
            {
                int availableSpace = Mathf.Max(0, toSlot.MaxStack - toSlot.Quantity);
                if (availableSpace == 0)
                {
                    _view.SetStatus("Target stack is full.");
                    return;
                }

                int transferQuantity = Mathf.Min(fromSlot.Quantity, availableSpace);
                bool merged = _inventorySystem.TryMoveItem(fromSlotIndex, toSlotIndex, transferQuantity, out string mergeError);
                _view.SetStatus(merged ? "Stack merged." : mergeError);
                return;
            }

            bool swapped = _inventorySystem.TrySwapSlots(fromSlotIndex, toSlotIndex, out string swapError);
            _view.SetStatus(swapped ? "Slots swapped." : swapError);

            if (swapped)
                _selectedSlotIndex = toSlotIndex;
        }

        private void HandleSlotTapped(int slotIndex)
        {
            _selectedSlotIndex = slotIndex;

            if (!_inventorySystem.TryGetSlot(slotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
            {
                _view.SetTooltip("Empty slot");
                _view.SetStatus($"Slot {slotIndex} is empty.");
                return;
            }

            string tooltip = _inventoryService != null
                ? _inventoryService.BuildTooltip(slot)
                : $"Item {slot.ItemId} x{slot.Quantity}";

            _view.SetTooltip(tooltip);
            _view.SetStatus($"Selected slot {slotIndex}.");
        }

        private async void HandleEquipRequested()
        {
            if (_isBusy)
                return;

            if (_selectedSlotIndex < 0)
            {
                _view.SetStatus("Select an item first.");
                return;
            }

            if (!_inventorySystem.TryGetSlot(_selectedSlotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
            {
                _view.SetStatus("Selected slot is empty.");
                return;
            }

            EquipmentClientSystem.EquipSlot equipSlot = GuessEquipSlot(slot.ItemId);
            await RunBusyAsync(async () =>
            {
                bool ok = await _inventoryService.EquipAsync(_selectedSlotIndex, equipSlot, _lifetimeCts.Token);
                _view.SetStatus(ok ? "Item equipped." : "Equip failed.");
            });
        }

        private async void HandleUnequipRequested()
        {
            if (_isBusy)
                return;

            EquipmentClientSystem.EquipSlot slot = FindFirstEquippedSlot();
            await RunBusyAsync(async () =>
            {
                bool ok = await _inventoryService.UnequipAsync(slot, _selectedSlotIndex >= 0 ? _selectedSlotIndex : 0, _lifetimeCts.Token);
                _view.SetStatus(ok ? "Item unequipped." : "Unequip failed.");
            });
        }

        private async void HandleDropRequested()
        {
            if (_isBusy)
                return;

            if (_selectedSlotIndex < 0)
            {
                _view.SetStatus("Select an item first.");
                return;
            }

            if (!_inventorySystem.TryGetSlot(_selectedSlotIndex, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
            {
                _view.SetStatus("Selected slot is empty.");
                return;
            }

            await RunBusyAsync(async () =>
            {
                bool ok = await _inventoryService.DropAsync(_selectedSlotIndex, 1, _lifetimeCts.Token);
                _view.SetStatus(ok ? "Item dropped." : "Drop failed.");
            });
        }

        private void HandleAutoPickupToggled(bool enabled)
        {
            if (_lootPickup != null)
                _lootPickup.AutoPickupEnabled = enabled;

            _view.SetStatus(enabled ? "Auto-pickup enabled." : "Auto-pickup disabled.");
        }

        private void HandleOperationFailed(string message)
        {
            _view.SetStatus(message);
        }

        private void HandleOperationSucceeded(string message)
        {
            _view.SetStatus(message);
            _view.SetPowerScore(_inventoryService != null ? _inventoryService.CalculatePowerScore() : 0);
        }

        private void HandlePickupMessage(string message)
        {
            _view.SetStatus(message);
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            _isBusy = true;
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus("Operation cancelled.");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void RenderInventory()
        {
            List<ItemInstanceViewData> items = BuildItemViewData();
            var slots = new List<InventorySlotViewData>(items.Count);
            for (int i = 0; i < items.Count; i++)
                slots.Add(InventorySlotViewData.FromItemInstance(items[i]));

            _view.Render(slots);
        }

        private List<ItemInstanceViewData> BuildItemViewData()
        {
            int slotCapacity = _inventorySystem != null ? _inventorySystem.SlotCapacity : 0;
            var data = new List<ItemInstanceViewData>(slotCapacity);

            for (int slotIndex = 0; slotIndex < slotCapacity; slotIndex++)
            {
                _inventorySystem.TryGetSlot(slotIndex, out InventoryClientSystem.InventorySlot slot);
                data.Add(BuildItemViewData(slotIndex, slot));
            }

            return data;
        }

        private EquipmentClientSystem.EquipSlot GuessEquipSlot(int itemId)
        {
            if (_catalogResolver != null && _catalogResolver.TryGetItemDefinition(itemId, out ItemDefinition item))
            {
                if (item.AllowedEquipSlots != null && item.AllowedEquipSlots.Count > 0)
                    return MapSlot(item.AllowedEquipSlots[0]);
            }

            return EquipmentClientSystem.EquipSlot.WeaponMain;
        }

        private static EquipmentClientSystem.EquipSlot MapSlot(ItemEquipSlot slot)
        {
            return slot switch
            {
                ItemEquipSlot.Helm => EquipmentClientSystem.EquipSlot.Helm,
                ItemEquipSlot.Armor => EquipmentClientSystem.EquipSlot.Armor,
                ItemEquipSlot.Pants => EquipmentClientSystem.EquipSlot.Pants,
                ItemEquipSlot.Gloves => EquipmentClientSystem.EquipSlot.Gloves,
                ItemEquipSlot.Boots => EquipmentClientSystem.EquipSlot.Boots,
                ItemEquipSlot.WeaponMain => EquipmentClientSystem.EquipSlot.WeaponMain,
                ItemEquipSlot.WeaponOffhand => EquipmentClientSystem.EquipSlot.WeaponOffhand,
                ItemEquipSlot.RingLeft => EquipmentClientSystem.EquipSlot.RingLeft,
                ItemEquipSlot.RingRight => EquipmentClientSystem.EquipSlot.RingRight,
                ItemEquipSlot.Pendant => EquipmentClientSystem.EquipSlot.Pendant,
                ItemEquipSlot.Wings => EquipmentClientSystem.EquipSlot.Wings,
                _ => EquipmentClientSystem.EquipSlot.WeaponMain
            };
        }

        private EquipmentClientSystem.EquipSlot FindFirstEquippedSlot()
        {
            if (_equipmentSystem != null)
            {
                foreach (var pair in _equipmentSystem.Equipped)
                {
                    if (!pair.Value.IsEmpty)
                        return pair.Key;
                }
            }

            return EquipmentClientSystem.EquipSlot.WeaponMain;
        }

        private ItemInstanceViewData BuildItemViewData(int slotIndex, InventoryClientSystem.InventorySlot slot)
        {
            if (slot.IsEmpty)
                return ItemInstanceViewData.Empty(slotIndex);

            string itemName = $"Item {slot.ItemId}";
            Sprite icon = null;
            ItemCategory category = ItemCategory.Unknown;
            ItemRarity rarity = ItemRarity.Common;
            bool isStackable = slot.MaxStack > 1;

            if (_catalogResolver != null && _catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition itemDefinition))
            {
                if (!string.IsNullOrWhiteSpace(itemDefinition.Name))
                    itemName = itemDefinition.Name;

                icon = LoadIcon(itemDefinition.Icon);
                category = itemDefinition.Category;
                rarity = itemDefinition.Rarity;
                isStackable = itemDefinition.Stackable;
            }

            bool isEquipped = (slot.Flags & InventoryClientSystem.InventoryItemFlags.Equipped) != 0;

            return new ItemInstanceViewData(
                slotIndex,
                string.Empty,
                slot.ItemId,
                itemName,
                category,
                rarity,
                slot.Quantity,
                slot.MaxStack,
                enhancementLevel: 0,
                slot.DurabilityCurrent,
                slot.DurabilityMax,
                isStackable,
                isEquipped,
                isEmpty: false,
                icon);
        }

        private void PreparePotionQuickSlots()
        {
            if (_inventorySystem == null || _catalogResolver == null)
                return;

            if (!_inventorySystem.TryGetQuickSlot(InventoryClientSystem.QuickSlotKind.HpPotion, out _))
            {
                if (TryFindPotionSlot(requireHp: true, requireMp: false, out int hpSlotIndex))
                    _inventorySystem.SetQuickSlot(InventoryClientSystem.QuickSlotKind.HpPotion, hpSlotIndex);
            }

            if (!_inventorySystem.TryGetQuickSlot(InventoryClientSystem.QuickSlotKind.MpPotion, out _))
            {
                if (TryFindPotionSlot(requireHp: false, requireMp: true, out int mpSlotIndex))
                    _inventorySystem.SetQuickSlot(InventoryClientSystem.QuickSlotKind.MpPotion, mpSlotIndex);
            }
        }

        private bool TryFindPotionSlot(bool requireHp, bool requireMp, out int slotIndex)
        {
            slotIndex = -1;

            IReadOnlyList<InventoryClientSystem.InventorySlot> slots = _inventorySystem.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!_catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition definition) || definition == null)
                    continue;

                if (definition.Category != ItemCategory.Consumable)
                    continue;

                if (requireHp && definition.Restore.Hp <= 0)
                    continue;

                if (requireMp && definition.Restore.Mana <= 0)
                    continue;

                slotIndex = slot.SlotIndex;
                return true;
            }

            return false;
        }

        private Sprite LoadIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            if (_iconCache.TryGetValue(iconPath, out Sprite cached))
                return cached;

            Sprite loaded = Resources.Load<Sprite>(iconPath);
            _iconCache[iconPath] = loaded;
            return loaded;
        }
    }
}
