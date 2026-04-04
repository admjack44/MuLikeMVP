using System;
using MuLike.Core;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.Inventory
{
    /// <summary>
    /// Scene composition root for inventory drag and drop MVP.
    /// </summary>
    public class InventoryFlowController : MonoBehaviour
    {
        [Serializable]
        private struct DemoInventorySlotSeed
        {
            public int SlotIndex;
            public int ItemId;
            public int Quantity;
            public int MaxStack;
        }

        [SerializeField] private InventoryView _view;
        [SerializeField] private bool _seedDemoDataOnAwake = true;
        [SerializeField] private DemoInventorySlotSeed[] _demoSlots =
        {
            new DemoInventorySlotSeed { SlotIndex = 0, ItemId = 1001, Quantity = 3, MaxStack = 20 },
            new DemoInventorySlotSeed { SlotIndex = 1, ItemId = 1001, Quantity = 5, MaxStack = 20 },
            new DemoInventorySlotSeed { SlotIndex = 2, ItemId = 2001, Quantity = 1, MaxStack = 1 }
        };

        private InventoryPresenter _presenter;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<InventoryView>();

            if (_view == null)
            {
                Debug.LogError("[InventoryFlowController] InventoryView missing.");
                enabled = false;
                return;
            }

            InventoryClientSystem inventorySystem = ResolveOrCreateInventorySystem();
            if (_seedDemoDataOnAwake)
                SeedDemoInventory(inventorySystem);

            _presenter = new InventoryPresenter(_view, inventorySystem, GameContext.CatalogResolver);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();
        }

        private static InventoryClientSystem ResolveOrCreateInventorySystem()
        {
            if (GameContext.TryGetSystem(out InventoryClientSystem inventory))
                return inventory;

            inventory = new InventoryClientSystem(catalogResolver: GameContext.CatalogResolver);
            GameContext.RegisterSystem(inventory);
            return inventory;
        }

        private void SeedDemoInventory(InventoryClientSystem inventorySystem)
        {
            if (inventorySystem == null || _demoSlots == null)
                return;

            for (int i = 0; i < _demoSlots.Length; i++)
            {
                DemoInventorySlotSeed seed = _demoSlots[i];

                var slot = new InventoryClientSystem.InventorySlot
                {
                    SlotIndex = seed.SlotIndex,
                    ItemId = seed.ItemId,
                    Quantity = seed.Quantity,
                    MaxStack = Mathf.Max(1, seed.MaxStack),
                    DurabilityCurrent = 0,
                    DurabilityMax = 0,
                    Flags = InventoryClientSystem.InventoryItemFlags.None
                };

                inventorySystem.UpdateSlot(slot, out _);
            }
        }
    }
}
