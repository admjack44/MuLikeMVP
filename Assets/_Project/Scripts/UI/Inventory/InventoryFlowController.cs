using System;
using MuLike.Bootstrap;
using MuLike.Core;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;
using System.Threading;

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
        [SerializeField] private DropViewPool _dropPool;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private bool _seedDemoDataOnAwake = true;
        [SerializeField] private bool _autoPickupEnabledByDefault = false;
        [SerializeField] private string _characterId = "char-local";
        [SerializeField] private DemoInventorySlotSeed[] _demoSlots =
        {
            new DemoInventorySlotSeed { SlotIndex = 0, ItemId = 1001, Quantity = 3, MaxStack = 20 },
            new DemoInventorySlotSeed { SlotIndex = 1, ItemId = 1001, Quantity = 5, MaxStack = 20 },
            new DemoInventorySlotSeed { SlotIndex = 2, ItemId = 2001, Quantity = 1, MaxStack = 1 }
        };

        private InventoryPresenter _presenter;
        private InventoryEquipmentService _inventoryService;
        private LootPickupSystem _lootPickupSystem;
        private CancellationTokenSource _lifetimeCts;
        private float _nextAutoPickupAt;

        /// <summary>
        /// Runtime override entrypoint for character-bound inventory sessions.
        /// </summary>
        public void SetCharacterIdRuntime(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            _characterId = characterId.Trim();
            _inventoryService?.SetCharacterId(_characterId);
        }

        private void Awake()
        {
            IClientSessionState session = ClientBootstrap.Instance != null
                ? ClientBootstrap.Instance.Services.ResolveOrNull<IClientSessionState>()
                : null;

            if (session != null && session.SelectedCharacterId > 0)
                _characterId = session.SelectedCharacterId.ToString();

            if (_view == null)
                _view = FindAnyObjectByType<InventoryView>();

            if (_dropPool == null)
                _dropPool = FindAnyObjectByType<DropViewPool>();

            if (_view == null)
            {
                Debug.LogError("[InventoryFlowController] InventoryView missing.");
                enabled = false;
                return;
            }

            InventoryClientSystem inventorySystem = ResolveOrCreateInventorySystem();
            EquipmentClientSystem equipmentSystem = ResolveOrCreateEquipmentSystem();
            if (_seedDemoDataOnAwake)
                SeedDemoInventory(inventorySystem);

            var transport = new MockInventoryEquipmentTransport(inventorySystem, equipmentSystem);
            _inventoryService = new InventoryEquipmentService(
                inventorySystem,
                equipmentSystem,
                ResolveOrCreateStatsSystem(),
                GameContext.CatalogResolver,
                transport);

            _inventoryService.SetCharacterId(_characterId);

            if (_dropPool == null)
            {
                var poolGo = new GameObject("DropViewPool");
                _dropPool = poolGo.AddComponent<DropViewPool>();
            }

            _lootPickupSystem = new LootPickupSystem(_inventoryService, _dropPool)
            {
                AutoPickupEnabled = _autoPickupEnabledByDefault,
                AutoPickupRadius = 3.2f
            };

            _inventoryService.WorldDropsUpdated += _lootPickupSystem.ApplyWorldDrops;

            _presenter = new InventoryPresenter(
                _view,
                _inventoryService,
                _lootPickupSystem,
                inventorySystem,
                equipmentSystem,
                GameContext.CatalogResolver);

            _lifetimeCts = new CancellationTokenSource();
            _ = _inventoryService.RefreshSnapshotAsync(_lifetimeCts.Token);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
            if (_view != null)
                _view.SetVisible(false);
        }

        private void OnDisable()
        {
            _presenter?.Unbind();

            if (_lifetimeCts != null && !_lifetimeCts.IsCancellationRequested)
                _lifetimeCts.Cancel();
        }

        private void OnDestroy()
        {
            if (_inventoryService != null && _lootPickupSystem != null)
                _inventoryService.WorldDropsUpdated -= _lootPickupSystem.ApplyWorldDrops;

            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
                _lifetimeCts.Dispose();
                _lifetimeCts = null;
            }
        }

        private async void Update()
        {
            if (_lootPickupSystem == null || _playerTransform == null)
                return;

            if (Time.time < _nextAutoPickupAt)
                return;

            _nextAutoPickupAt = Time.time + 0.25f;
            try
            {
                await _lootPickupSystem.TryAutoPickupAsync(_playerTransform, _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static InventoryClientSystem ResolveOrCreateInventorySystem()
        {
            if (GameContext.TryGetSystem(out InventoryClientSystem inventory))
                return inventory;

            inventory = new InventoryClientSystem(catalogResolver: GameContext.CatalogResolver);
            GameContext.RegisterSystem(inventory);
            return inventory;
        }

        private static EquipmentClientSystem ResolveOrCreateEquipmentSystem()
        {
            if (GameContext.TryGetSystem(out EquipmentClientSystem equipment))
                return equipment;

            equipment = new EquipmentClientSystem(GameContext.CatalogResolver);
            GameContext.RegisterSystem(equipment);
            return equipment;
        }

        private static StatsClientSystem ResolveOrCreateStatsSystem()
        {
            if (GameContext.TryGetSystem(out StatsClientSystem stats))
                return stats;

            stats = new StatsClientSystem();
            GameContext.RegisterSystem(stats);
            return stats;
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
