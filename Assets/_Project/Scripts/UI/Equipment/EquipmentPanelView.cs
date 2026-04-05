using System;
using System.Collections.Generic;
using MuLike.Systems;
using MuLike.UI.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Equipment
{
    public sealed class EquipmentPanelView : MonoBehaviour, IEquipmentView
    {
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private TMP_Dropdown _slotDropdown;
        [SerializeField] private TMP_Text _itemNameText;
        [SerializeField] private TMP_Text _itemMetaText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Button _unequipButton;

        private readonly List<EquipmentSlotViewData> _renderedSlots = new();
        private bool _isVisible;

        public event Action<EquipmentClientSystem.EquipSlot> SlotSelected;
        public event Action UnequipPressed;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            if (_slotDropdown != null)
                _slotDropdown.onValueChanged.AddListener(HandleSlotChanged);

            if (_unequipButton != null)
                _unequipButton.onClick.AddListener(() => UnequipPressed?.Invoke());

            SetVisible(false);
            SetStatus("Equipment ready.");
        }

        private void OnDestroy()
        {
            if (_slotDropdown != null)
                _slotDropdown.onValueChanged.RemoveListener(HandleSlotChanged);

            if (_unequipButton != null)
                _unequipButton.onClick.RemoveAllListeners();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_modalRoot != null)
                _modalRoot.SetActive(visible);
        }

        public void RenderSlots(IReadOnlyList<EquipmentSlotViewData> slots, EquipmentClientSystem.EquipSlot selectedSlot)
        {
            _renderedSlots.Clear();
            if (slots != null)
                _renderedSlots.AddRange(slots);

            if (_slotDropdown != null)
            {
                _slotDropdown.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>(_renderedSlots.Count);
                int selectedIndex = 0;

                for (int i = 0; i < _renderedSlots.Count; i++)
                {
                    EquipmentSlotViewData slot = _renderedSlots[i];
                    string itemLabel = slot.IsEmpty ? "Empty" : slot.ItemName;
                    options.Add(new TMP_Dropdown.OptionData($"{slot.Slot}: {itemLabel}"));

                    if (slot.Slot == selectedSlot)
                        selectedIndex = i;
                }

                _slotDropdown.AddOptions(options);
                _slotDropdown.SetValueWithoutNotify(selectedIndex);
            }

            if (_renderedSlots.Count == 0)
            {
                if (_itemNameText != null)
                    _itemNameText.text = "No equipment data";

                if (_itemMetaText != null)
                    _itemMetaText.text = string.Empty;

                if (_itemIcon != null)
                    _itemIcon.enabled = false;
                return;
            }

            ShowSlot(Mathf.Clamp(GetIndexBySlot(selectedSlot), 0, _renderedSlots.Count - 1));
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        private void HandleSlotChanged(int index)
        {
            if (_renderedSlots.Count == 0)
                return;

            int safeIndex = Mathf.Clamp(index, 0, _renderedSlots.Count - 1);
            ShowSlot(safeIndex);
            SlotSelected?.Invoke(_renderedSlots[safeIndex].Slot);
        }

        private void ShowSlot(int index)
        {
            if (_renderedSlots.Count == 0)
                return;

            EquipmentSlotViewData slot = _renderedSlots[Mathf.Clamp(index, 0, _renderedSlots.Count - 1)];

            if (_itemNameText != null)
                _itemNameText.text = slot.IsEmpty ? slot.Slot.ToString() : slot.ItemName;

            if (_itemMetaText != null)
                _itemMetaText.text = slot.IsEmpty ? "Empty slot" : $"{slot.Rarity} / {slot.Category}";

            if (_itemIcon != null)
            {
                _itemIcon.sprite = slot.Icon;
                _itemIcon.enabled = slot.Icon != null;
            }

            if (_unequipButton != null)
                _unequipButton.interactable = !slot.IsEmpty;
        }

        private int GetIndexBySlot(EquipmentClientSystem.EquipSlot slot)
        {
            for (int i = 0; i < _renderedSlots.Count; i++)
            {
                if (_renderedSlots[i].Slot == slot)
                    return i;
            }

            return 0;
        }
    }
}
