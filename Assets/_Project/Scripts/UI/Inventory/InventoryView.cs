using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Inventory
{
    /// <summary>
    /// Pure inventory view. Renders slot list and emits user drag/drop intent.
    /// </summary>
    public class InventoryView : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private GameObject _modalRoot;

        [SerializeField] private Transform _slotContainer;
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _tooltipText;
        [SerializeField] private TMP_Text _powerScoreText;

        [Header("Actions")]
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _unequipButton;
        [SerializeField] private Button _dropButton;
        [SerializeField] private Toggle _autoPickupToggle;

        private readonly List<InventorySlotView> _spawnedSlots = new();
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public event Action<int, int> SlotDropRequested;
        public event Action<int> SlotTapped;
        public event Action EquipRequested;
        public event Action UnequipRequested;
        public event Action DropRequested;
        public event Action<bool> AutoPickupToggled;

        private void OnDestroy()
        {
            ClearAllSlots();

            if (_equipButton != null)
                _equipButton.onClick.RemoveAllListeners();

            if (_unequipButton != null)
                _unequipButton.onClick.RemoveAllListeners();

            if (_dropButton != null)
                _dropButton.onClick.RemoveAllListeners();

            if (_autoPickupToggle != null)
                _autoPickupToggle.onValueChanged.RemoveAllListeners();
        }

        private void Awake()
        {
            if (_equipButton != null)
                _equipButton.onClick.AddListener(() => EquipRequested?.Invoke());

            if (_unequipButton != null)
                _unequipButton.onClick.AddListener(() => UnequipRequested?.Invoke());

            if (_dropButton != null)
                _dropButton.onClick.AddListener(() => DropRequested?.Invoke());

            if (_autoPickupToggle != null)
                _autoPickupToggle.onValueChanged.AddListener(enabled => AutoPickupToggled?.Invoke(enabled));

            SetVisible(false);
        }

        public void Render(IReadOnlyList<InventorySlotViewData> slots)
        {
            if (_slotContainer == null || _slotPrefab == null || slots == null)
                return;

            EnsureSlotCount(slots.Count);

            for (int i = 0; i < slots.Count; i++)
                _spawnedSlots[i].Bind(slots[i]);
        }

        public void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.text = text ?? string.Empty;
        }

        public void SetTooltip(string text)
        {
            if (_tooltipText != null)
                _tooltipText.text = text ?? string.Empty;
        }

        public void SetPowerScore(int score)
        {
            if (_powerScoreText != null)
                _powerScoreText.text = $"Power {Mathf.Max(0, score)}";
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_modalRoot != null)
                _modalRoot.SetActive(visible);
        }

        public void ToggleVisible()
        {
            SetVisible(!_isVisible);
        }

        private void EnsureSlotCount(int required)
        {
            while (_spawnedSlots.Count < required)
                SpawnSlot();

            while (_spawnedSlots.Count > required)
                RemoveLastSlot();
        }

        private void SpawnSlot()
        {
            InventorySlotView slot = Instantiate(_slotPrefab, _slotContainer);
            slot.DropRequested += HandleSlotDropRequested;
            slot.SlotTapped += HandleSlotTapped;
            _spawnedSlots.Add(slot);
        }

        private void RemoveLastSlot()
        {
            int index = _spawnedSlots.Count - 1;
            InventorySlotView slot = _spawnedSlots[index];
            _spawnedSlots.RemoveAt(index);

            if (slot != null)
            {
                slot.DropRequested -= HandleSlotDropRequested;
                slot.SlotTapped -= HandleSlotTapped;
                Destroy(slot.gameObject);
            }
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < _spawnedSlots.Count; i++)
            {
                InventorySlotView slot = _spawnedSlots[i];
                if (slot == null)
                    continue;

                slot.DropRequested -= HandleSlotDropRequested;
                slot.SlotTapped -= HandleSlotTapped;
                Destroy(slot.gameObject);
            }

            _spawnedSlots.Clear();
        }

        private void HandleSlotDropRequested(int fromSlotIndex, int toSlotIndex)
        {
            SlotDropRequested?.Invoke(fromSlotIndex, toSlotIndex);
        }

        private void HandleSlotTapped(int slotIndex)
        {
            SlotTapped?.Invoke(slotIndex);
        }
    }
}
