using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MuLike.UI.Inventory
{
    /// <summary>
    /// Pure inventory view. Renders slot list and emits user drag/drop intent.
    /// </summary>
    public class InventoryView : MonoBehaviour
    {
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private TMP_Text _statusText;

        private readonly List<InventorySlotView> _spawnedSlots = new();

        public event Action<int, int> SlotDropRequested;

        private void OnDestroy()
        {
            ClearAllSlots();
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
                Destroy(slot.gameObject);
            }

            _spawnedSlots.Clear();
        }

        private void HandleSlotDropRequested(int fromSlotIndex, int toSlotIndex)
        {
            SlotDropRequested?.Invoke(fromSlotIndex, toSlotIndex);
        }
    }
}
