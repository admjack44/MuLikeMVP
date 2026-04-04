using MuLike.Core;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.HUD
{
    /// <summary>
    /// Subscribes to gameplay/network systems and updates HUDView.
    /// Contains no gameplay authority logic.
    /// </summary>
    public sealed class HUDPresenter
    {
        public sealed class Dependencies
        {
            public HUDView View;
            public StatsClientSystem StatsSystem;
            public InventoryClientSystem InventorySystem;
            public EquipmentClientSystem EquipmentSystem;
            public ConsumableClientSystem ConsumableSystem;
            public TargetingController TargetingController;
            public NetworkGameClient NetworkClient;
        }

        private readonly Dependencies _deps;
        private readonly string _characterName;
        private readonly bool _enableDebugConsole;

        public HUDPresenter(Dependencies dependencies, string characterName, bool enableDebugConsole)
        {
            _deps = dependencies;
            _characterName = characterName;
            _enableDebugConsole = enableDebugConsole;
        }

        public void Bind()
        {
            _deps.View.ChatToggleRequested += HandleChatToggle;
            _deps.View.InventoryRequested += HandleInventoryOpen;
            _deps.View.EquipmentRequested += HandleEquipmentOpen;
            _deps.View.QuickConsumeRequested += HandleQuickConsume;

            if (_deps.StatsSystem != null)
            {
                _deps.StatsSystem.OnStatsSnapshotApplied += HandleStatsSnapshot;
                _deps.StatsSystem.OnStatsDeltaApplied += HandleStatsDelta;
                _deps.StatsSystem.OnLevelChanged += HandleLevelChanged;
            }

            if (_deps.InventorySystem != null)
                _deps.InventorySystem.OnInventoryChanged += HandleInventoryChanged;

            if (_deps.EquipmentSystem != null)
                _deps.EquipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

            if (_deps.TargetingController != null)
                _deps.TargetingController.OnTargetChanged += HandleTargetChanged;

            if (_deps.NetworkClient != null)
                _deps.NetworkClient.OnClientLog += HandleNetworkLog;

            _deps.View.SetCharacterName(_characterName);
            _deps.View.SetDebugConsoleVisible(_enableDebugConsole);

            if (_deps.StatsSystem != null)
                HandleStatsSnapshot(_deps.StatsSystem.Snapshot);

            if (_deps.TargetingController != null)
                HandleTargetChanged(_deps.TargetingController.CurrentTarget);
        }

        public void Unbind()
        {
            _deps.View.ChatToggleRequested -= HandleChatToggle;
            _deps.View.InventoryRequested -= HandleInventoryOpen;
            _deps.View.EquipmentRequested -= HandleEquipmentOpen;
            _deps.View.QuickConsumeRequested -= HandleQuickConsume;

            if (_deps.StatsSystem != null)
            {
                _deps.StatsSystem.OnStatsSnapshotApplied -= HandleStatsSnapshot;
                _deps.StatsSystem.OnStatsDeltaApplied -= HandleStatsDelta;
                _deps.StatsSystem.OnLevelChanged -= HandleLevelChanged;
            }

            if (_deps.InventorySystem != null)
                _deps.InventorySystem.OnInventoryChanged -= HandleInventoryChanged;

            if (_deps.EquipmentSystem != null)
                _deps.EquipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

            if (_deps.TargetingController != null)
                _deps.TargetingController.OnTargetChanged -= HandleTargetChanged;

            if (_deps.NetworkClient != null)
                _deps.NetworkClient.OnClientLog -= HandleNetworkLog;
        }

        private void HandleStatsSnapshot(StatsClientSystem.PlayerStatsSnapshot snapshot)
        {
            _deps.View.SetLevel(snapshot.Primary.Level);
            _deps.View.SetHp(snapshot.Resources.Hp.Current, snapshot.Resources.Hp.Max);
            _deps.View.SetMana(snapshot.Resources.Mana.Current, snapshot.Resources.Mana.Max);
            _deps.View.SetShield(snapshot.Resources.Shield.Current, snapshot.Resources.Shield.Max);
            _deps.View.SetStamina(snapshot.Resources.Stamina.Current, snapshot.Resources.Stamina.Max);
            _deps.View.SetExperience(snapshot.Primary.Experience, snapshot.Primary.ExperienceNextLevel);
        }

        private void HandleStatsDelta(StatsClientSystem.PlayerStatsDelta _)
        {
            HandleStatsSnapshot(_deps.StatsSystem.Snapshot);
        }

        private void HandleLevelChanged(int previousLevel, int newLevel)
        {
            _deps.View.SetStatus($"Level up: {previousLevel} -> {newLevel}");
        }

        private void HandleInventoryChanged()
        {
            int count = _deps.InventorySystem != null ? _deps.InventorySystem.Slots.Count : 0;
            _deps.View.SetStatus($"Inventory updated ({count} slots used).");
        }

        private void HandleEquipmentChanged()
        {
            int count = _deps.EquipmentSystem != null ? _deps.EquipmentSystem.Equipped.Count : 0;
            _deps.View.SetStatus($"Equipment updated ({count} equipped).");
        }

        private void HandleTargetChanged(EntityView target)
        {
            _deps.View.SetTargetName(target != null ? target.name : "No target");
        }

        private void HandleChatToggle()
        {
            _deps.View.SetStatus("Chat toggle requested.");
        }

        private void HandleInventoryOpen()
        {
            _deps.View.SetStatus("Inventory panel requested.");
        }

        private void HandleEquipmentOpen()
        {
            _deps.View.SetStatus("Equipment panel requested.");
        }

        private void HandleQuickConsume()
        {
            if (_deps.ConsumableSystem == null)
            {
                _deps.View.SetStatus("Consumable system not available.");
                return;
            }

            if (_deps.ConsumableSystem.TryUseFirstRestorative(out int slotIndex, out int itemId, out string error))
            {
                _deps.View.SetStatus($"Used consumable item {itemId} from slot {slotIndex}.");
                return;
            }

            _deps.View.SetStatus(error);
        }

        private void HandleNetworkLog(string line)
        {
            if (!_enableDebugConsole)
                return;

            _deps.View.AppendDebugLine(line);
        }

        public static StatsClientSystem ResolveOrCreateStatsSystem()
        {
            if (GameContext.TryGetSystem(out StatsClientSystem stats))
                return stats;

            stats = new StatsClientSystem();
            GameContext.RegisterSystem(stats);
            return stats;
        }

        public static InventoryClientSystem ResolveOrCreateInventorySystem()
        {
            if (GameContext.TryGetSystem(out InventoryClientSystem inventory))
                return inventory;

            inventory = new InventoryClientSystem(catalogResolver: GameContext.CatalogResolver);
            GameContext.RegisterSystem(inventory);
            return inventory;
        }

        public static EquipmentClientSystem ResolveOrCreateEquipmentSystem()
        {
            if (GameContext.TryGetSystem(out EquipmentClientSystem equipment))
                return equipment;

            equipment = new EquipmentClientSystem(GameContext.CatalogResolver);
            GameContext.RegisterSystem(equipment);
            return equipment;
        }

        public static ConsumableClientSystem ResolveOrCreateConsumableSystem(
            InventoryClientSystem inventory,
            StatsClientSystem stats)
        {
            if (GameContext.TryGetSystem(out ConsumableClientSystem consumable))
                return consumable;

            consumable = new ConsumableClientSystem(GameContext.CatalogResolver, inventory, stats);
            GameContext.RegisterSystem(consumable);
            return consumable;
        }
    }
}
