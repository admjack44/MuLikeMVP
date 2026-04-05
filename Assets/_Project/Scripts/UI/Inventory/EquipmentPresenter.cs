using System;
using System.Collections.Generic;
using MuLike.Data.Catalogs;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.Inventory
{
    public readonly struct EquipmentSlotViewData
    {
        public readonly EquipmentClientSystem.EquipSlot Slot;
        public readonly int ItemId;
        public readonly string ItemName;
        public readonly ItemCategory Category;
        public readonly ItemRarity Rarity;
        public readonly Sprite Icon;
        public readonly bool IsEmpty;

        public EquipmentSlotViewData(
            EquipmentClientSystem.EquipSlot slot,
            int itemId,
            string itemName,
            ItemCategory category,
            ItemRarity rarity,
            Sprite icon,
            bool isEmpty)
        {
            Slot = slot;
            ItemId = Mathf.Max(0, itemId);
            ItemName = itemName ?? string.Empty;
            Category = category;
            Rarity = rarity;
            Icon = icon;
            IsEmpty = isEmpty;
        }
    }

    public interface IEquipmentView
    {
        event Action<EquipmentClientSystem.EquipSlot> SlotSelected;
        event Action UnequipPressed;

        void RenderSlots(IReadOnlyList<EquipmentSlotViewData> slots, EquipmentClientSystem.EquipSlot selectedSlot);
        void SetStatus(string message);
    }

    /// <summary>
    /// Presenter for equipment panel rendering and unequip intents.
    /// </summary>
    public sealed class EquipmentPresenter
    {
        private readonly IEquipmentView _view;
        private readonly EquipmentClientSystem _equipmentSystem;
        private readonly InventoryEquipmentService _service;
        private readonly CatalogResolver _catalogResolver;
        private readonly Dictionary<string, Sprite> _iconCache = new();

        private EquipmentClientSystem.EquipSlot _selectedSlot;

        public EquipmentPresenter(
            IEquipmentView view,
            EquipmentClientSystem equipmentSystem,
            InventoryEquipmentService service,
            CatalogResolver catalogResolver)
        {
            _view = view;
            _equipmentSystem = equipmentSystem;
            _service = service;
            _catalogResolver = catalogResolver;
            _selectedSlot = EquipmentClientSystem.EquipSlot.WeaponMain;
        }

        public void Bind()
        {
            if (_view != null)
            {
                _view.SlotSelected += HandleSlotSelected;
                _view.UnequipPressed += HandleUnequipPressed;
            }

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

            if (_service != null)
            {
                _service.OperationFailed += HandleOperationFailed;
                _service.OperationSucceeded += HandleOperationSucceeded;
            }

            Render();
        }

        public void Unbind()
        {
            if (_view != null)
            {
                _view.SlotSelected -= HandleSlotSelected;
                _view.UnequipPressed -= HandleUnequipPressed;
            }

            if (_equipmentSystem != null)
                _equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

            if (_service != null)
            {
                _service.OperationFailed -= HandleOperationFailed;
                _service.OperationSucceeded -= HandleOperationSucceeded;
            }
        }

        private void HandleSlotSelected(EquipmentClientSystem.EquipSlot slot)
        {
            _selectedSlot = slot;
            Render();
        }

        private async void HandleUnequipPressed()
        {
            if (_service != null)
            {
                bool ok = await _service.UnequipAsync(_selectedSlot, preferredInventorySlot: 0, default);
                _view?.SetStatus(ok ? $"Unequipped {_selectedSlot}." : $"Could not unequip {_selectedSlot}.");
                return;
            }

            if (_equipmentSystem == null)
                return;

            bool localResult = _equipmentSystem.Unequip(_selectedSlot);
            _view?.SetStatus(localResult ? $"Unequipped {_selectedSlot}." : $"Could not unequip {_selectedSlot}.");
        }

        private void HandleEquipmentChanged()
        {
            Render();
        }

        private void HandleOperationFailed(string message)
        {
            _view?.SetStatus(message);
        }

        private void HandleOperationSucceeded(string message)
        {
            _view?.SetStatus(message);
        }

        private void Render()
        {
            if (_view == null)
                return;

            var slots = new List<EquipmentSlotViewData>();
            Array allSlots = Enum.GetValues(typeof(EquipmentClientSystem.EquipSlot));
            for (int i = 0; i < allSlots.Length; i++)
            {
                EquipmentClientSystem.EquipSlot slot = (EquipmentClientSystem.EquipSlot)allSlots.GetValue(i);
                slots.Add(BuildSlotViewData(slot));
            }

            _view.RenderSlots(slots, _selectedSlot);
        }

        private EquipmentSlotViewData BuildSlotViewData(EquipmentClientSystem.EquipSlot slot)
        {
            if (_equipmentSystem == null || !_equipmentSystem.TryGetEquippedState(slot, out EquipmentClientSystem.EquipmentSlotState state) || state.IsEmpty)
            {
                return new EquipmentSlotViewData(slot, 0, string.Empty, ItemCategory.Unknown, ItemRarity.Common, null, true);
            }

            int itemId = state.Item.ItemId;
            string itemName = $"Item {itemId}";
            ItemCategory category = ItemCategory.Unknown;
            ItemRarity rarity = ItemRarity.Common;
            Sprite icon = null;

            if (_catalogResolver != null && _catalogResolver.TryGetItemDefinition(itemId, out ItemDefinition definition) && definition != null)
            {
                if (!string.IsNullOrWhiteSpace(definition.Name))
                    itemName = definition.Name;

                category = definition.Category;
                rarity = definition.Rarity;
                icon = LoadIcon(definition.Icon);
            }

            return new EquipmentSlotViewData(slot, itemId, itemName, category, rarity, icon, false);
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
