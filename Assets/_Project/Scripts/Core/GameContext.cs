using System;
using System.Collections.Generic;
using MuLike.Bootstrap;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Core
{
    /// <summary>
    /// Central runtime service registry for client composition.
    /// </summary>
    public static class GameContext
    {
        private enum RuntimeState
        {
            NotInitialized,
            Initializing,
            Initialized,
            ShuttingDown
        }

        private static RuntimeState _state = RuntimeState.NotInitialized;
        private static readonly Dictionary<Type, object> _runtimeSystems = new();
        private static ClientRuntimeInstaller _runtimeInstaller;

        public static bool IsInitialized { get; private set; }

        public static CatalogResolver CatalogResolver { get; private set; }
        public static StatsClientSystem StatsClientSystem { get; private set; }
        public static InventoryClientSystem InventoryClientSystem { get; private set; }
        public static EquipmentClientSystem EquipmentClientSystem { get; private set; }
        public static PetClientSystem PetClientSystem { get; private set; }
        public static ChatClientSystem ChatClientSystem { get; private set; }
        public static SessionStateClient SessionStateClient { get; private set; }
        public static SceneFlowService SceneFlowService { get; private set; }

        // Kept for backward compatibility with current runtime integrations.
        public static CharacterSessionSystem CharacterSessionSystem { get; private set; }
        public static SkillBookClientSystem SkillBookClientSystem { get; private set; }
        public static WorldStateSystem WorldStateSystem { get; private set; }

        public static void Initialize()
        {
            Initialize(new ClientRuntimeInstaller.RuntimeDependencies());
        }

        public static void Initialize(ClientRuntimeInstaller.RuntimeDependencies dependencies)
        {
            if (_state == RuntimeState.Initialized)
            {
                Debug.Log("[GameContext] Initialize skipped: already initialized.");
                return;
            }

            if (_state == RuntimeState.Initializing)
            {
                Debug.LogWarning("[GameContext] Initialize skipped: initialization already in progress.");
                return;
            }

            _state = RuntimeState.Initializing;
            Debug.Log("[GameContext] Bootstrap started. Building client runtime...");

            try
            {
                _runtimeSystems.Clear();

                _runtimeInstaller = new ClientRuntimeInstaller();
                ClientRuntimeInstaller.RuntimeComposition composition = _runtimeInstaller.Install(dependencies);

                CatalogResolver = composition.CatalogResolver;
                StatsClientSystem = composition.StatsClientSystem;
                InventoryClientSystem = composition.InventoryClientSystem;
                EquipmentClientSystem = composition.EquipmentClientSystem;
                PetClientSystem = composition.PetClientSystem;
                ChatClientSystem = composition.ChatClientSystem;
                SessionStateClient = composition.SessionStateClient;
                SceneFlowService = composition.SceneFlowService;

                CharacterSessionSystem = composition.CharacterSessionSystem;
                SkillBookClientSystem = composition.SkillBookClientSystem;
                WorldStateSystem = composition.WorldStateSystem;

                RegisterSystem(CatalogResolver);
                RegisterSystem(StatsClientSystem);
                RegisterSystem(InventoryClientSystem);
                RegisterSystem(EquipmentClientSystem);
                RegisterSystem(PetClientSystem);
                RegisterSystem(ChatClientSystem);
                RegisterSystem(SessionStateClient);
                RegisterSystem(SceneFlowService);
                RegisterSystem(CharacterSessionSystem);
                RegisterSystem(SkillBookClientSystem);
                RegisterSystem(WorldStateSystem);

                IsInitialized = true;
                _state = RuntimeState.Initialized;
                Debug.Log($"[GameContext] Bootstrap completed. Registered services: {_runtimeSystems.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameContext] Bootstrap failed: {ex.Message}");
                Shutdown();
                throw;
            }
        }

        public static void Shutdown()
        {
            if (_state == RuntimeState.NotInitialized)
                return;

            if (_state == RuntimeState.ShuttingDown)
            {
                Debug.LogWarning("[GameContext] Shutdown skipped: shutdown already in progress.");
                return;
            }

            _state = RuntimeState.ShuttingDown;
            Debug.Log("[GameContext] Shutdown started. Releasing runtime services...");

            WorldStateSystem?.Clear();
            WorldStateSystem = null;

            SkillBookClientSystem?.Clear();
            SkillBookClientSystem = null;

            ChatClientSystem?.Clear();
            ChatClientSystem = null;

            PetClientSystem = null;
            SessionStateClient = null;
            SceneFlowService = null;

            EquipmentClientSystem?.ApplySnapshot(new EquipmentClientSystem.EquipmentSnapshot());
            EquipmentClientSystem = null;

            InventoryClientSystem?.ApplySnapshot(new InventoryClientSystem.InventorySnapshot());
            InventoryClientSystem = null;

            CharacterSessionSystem?.Clear();
            CharacterSessionSystem = null;

            StatsClientSystem?.ApplySnapshot(default(StatsClientSystem.PlayerStatsSnapshot));
            StatsClientSystem = null;

            CatalogResolver?.Clear();
            CatalogResolver = null;

            _runtimeInstaller = null;

            _runtimeSystems.Clear();
            IsInitialized = false;
            _state = RuntimeState.NotInitialized;

            Debug.Log("[GameContext] Shutdown completed. Runtime is clean.");
        }

        [Obsolete("Use Shutdown() instead. Reset() is kept for backward compatibility.")]
        public static void Reset()
        {
            Shutdown();
        }

        public static void RegisterSystem<TSystem>(TSystem system) where TSystem : class
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            _runtimeSystems[typeof(TSystem)] = system;
        }

        public static bool TryGetSystem<TSystem>(out TSystem system) where TSystem : class
        {
            if (_runtimeSystems.TryGetValue(typeof(TSystem), out object value) && value is TSystem typed)
            {
                system = typed;
                return true;
            }

            system = null;
            return false;
        }
    }
}
