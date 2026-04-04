using System.Collections.Generic;
using MuLike.Data.Catalogs;
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
        private readonly InventoryClientSystem _inventorySystem;
        private readonly CatalogResolver _catalogResolver;
        private readonly Dictionary<string, Sprite> _iconCache = new();

        public InventoryPresenter(InventoryView view, InventoryClientSystem inventorySystem, CatalogResolver catalogResolver)
        {
            _view = view;
            _inventorySystem = inventorySystem;
            _catalogResolver = catalogResolver;
        }

        public void Bind()
        {
            _view.SlotDropRequested += HandleSlotDropRequested;

            if (_inventorySystem != null)
                _inventorySystem.OnInventoryChanged += HandleInventoryChanged;

            RenderInventory();
            _view.SetStatus("Inventory ready.");
        }

        public void Unbind()
        {
            _view.SlotDropRequested -= HandleSlotDropRequested;

            if (_inventorySystem != null)
                _inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged()
        {
            RenderInventory();
        }

        private void HandleSlotDropRequested(int fromSlotIndex, int toSlotIndex)
        {
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
        }

        private void RenderInventory()
        {
            var viewData = BuildViewData();
            _view.Render(viewData);
        }

        private List<InventorySlotViewData> BuildViewData()
        {
            int slotCapacity = _inventorySystem != null ? _inventorySystem.SlotCapacity : 0;
            var data = new List<InventorySlotViewData>(slotCapacity);

            for (int slotIndex = 0; slotIndex < slotCapacity; slotIndex++)
            {
                _inventorySystem.TryGetSlot(slotIndex, out InventoryClientSystem.InventorySlot slot);
                data.Add(BuildSlotViewData(slotIndex, slot));
            }

            return data;
        }

        private InventorySlotViewData BuildSlotViewData(int slotIndex, InventoryClientSystem.InventorySlot slot)
        {
            if (slot.IsEmpty)
                return new InventorySlotViewData(slotIndex, 0, string.Empty, 0, null, true);

            string itemName = $"Item {slot.ItemId}";
            Sprite icon = null;

            if (_catalogResolver != null && _catalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition itemDefinition))
            {
                if (!string.IsNullOrWhiteSpace(itemDefinition.Name))
                    itemName = itemDefinition.Name;

                icon = LoadIcon(itemDefinition.Icon);
            }

            return new InventorySlotViewData(slotIndex, slot.ItemId, itemName, slot.Quantity, icon, false);
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
