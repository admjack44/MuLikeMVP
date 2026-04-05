using MuLike.Core;
using MuLike.ContentPipeline.Runtime;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Installs persistent client services and registers them into the service registry.
    /// </summary>
    public sealed class PersistentGameContextInstaller
    {
        public sealed class Result
        {
            public NetworkGameClient NetworkClient;
            public SnapshotApplier SnapshotApplier;
            public SceneController SceneController;
        }

        public Result Install(
            IClientServiceRegistry registry,
            Transform persistentRoot,
            NetworkGameClient networkClientPrefab,
            bool tryFindSceneNetworkClient,
            int mobileTargetFrameRate)
        {
            if (registry == null)
            {
                Debug.LogError("[PersistentGameContextInstaller] Registry is null.");
                return null;
            }

            if (persistentRoot == null)
            {
                Debug.LogError("[PersistentGameContextInstaller] Persistent root is null.");
                return null;
            }

            ApplyMobileRuntimeDefaults(mobileTargetFrameRate);
            InstallCoreSystems(registry);

            var contentAddressablesService = new ContentAddressablesService();
            registry.Register<IContentAddressablesService>(contentAddressablesService);

            SceneController sceneController = EnsureSceneController(persistentRoot);
            registry.Register(sceneController);

            NetworkGameClient networkClient = EnsureNetworkClient(
                persistentRoot,
                networkClientPrefab,
                tryFindSceneNetworkClient);

            if (networkClient != null)
                registry.Register(networkClient);

            IEntityViewFactory viewFactory = ResolveEntityViewFactory();
            if (viewFactory == null)
                Debug.LogWarning("[PersistentGameContextInstaller] No IEntityViewFactory found. Snapshot visuals will stay disabled until one exists in scene.");

            var snapshotApplier = new SnapshotApplier(viewFactory);
            registry.Register(snapshotApplier);

            Debug.Log("[PersistentGameContextInstaller] Initialization order: 1) Runtime defaults, 2) GameContext, 3) Core systems, 4) SceneController, 5) NetworkClient, 6) SnapshotApplier.");

            return new Result
            {
                NetworkClient = networkClient,
                SnapshotApplier = snapshotApplier,
                SceneController = sceneController
            };
        }

        private static IEntityViewFactory ResolveEntityViewFactory()
        {
            PooledEntityViewFactory pooledFactory = Object.FindObjectOfType<PooledEntityViewFactory>();
            if (pooledFactory != null)
            {
                Debug.Log("[PersistentGameContextInstaller] Using PooledEntityViewFactory for entity views.");
                return pooledFactory;
            }

            PrefabEntityViewFactory prefabFactory = Object.FindObjectOfType<PrefabEntityViewFactory>();
            if (prefabFactory != null)
            {
                Debug.Log("[PersistentGameContextInstaller] Using PrefabEntityViewFactory for entity views.");
                return prefabFactory;
            }

            return null;
        }

        private static void ApplyMobileRuntimeDefaults(int mobileTargetFrameRate)
        {
            QualitySettings.vSyncCount = 0;
            if (mobileTargetFrameRate > 0)
                Application.targetFrameRate = mobileTargetFrameRate;
        }

        private static void InstallCoreSystems(IClientServiceRegistry registry)
        {
            if (!GameContext.IsInitialized)
                GameContext.Initialize();

            StatsClientSystem stats = ResolveOrCreateStats();
            InventoryClientSystem inventory = ResolveOrCreateInventory();
            EquipmentClientSystem equipment = ResolveOrCreateEquipment();
            ConsumableClientSystem consumable = ResolveOrCreateConsumable(inventory, stats);
            PetClientSystem pets = ResolveOrCreatePet();
            IClientSessionState sessionState = new RuntimeClientSessionState();

            registry.Register(stats);
            registry.Register(inventory);
            registry.Register(equipment);
            registry.Register(consumable);
            registry.Register(pets);
            registry.Register(sessionState);
        }

        private static SceneController EnsureSceneController(Transform persistentRoot)
        {
            if (SceneController.Instance != null)
                return SceneController.Instance;

            var sceneControllerGo = new GameObject("SceneController");
            sceneControllerGo.transform.SetParent(persistentRoot, false);
            return sceneControllerGo.AddComponent<SceneController>();
        }

        private static NetworkGameClient EnsureNetworkClient(
            Transform persistentRoot,
            NetworkGameClient networkClientPrefab,
            bool tryFindSceneNetworkClient)
        {
            NetworkGameClient found = null;
            if (tryFindSceneNetworkClient)
                found = Object.FindObjectOfType<NetworkGameClient>();

            if (found != null)
            {
                Object.DontDestroyOnLoad(found.gameObject);
                return found;
            }

            if (networkClientPrefab != null)
            {
                NetworkGameClient instance = Object.Instantiate(networkClientPrefab, persistentRoot);
                instance.gameObject.name = "NetworkGameClient";
                Object.DontDestroyOnLoad(instance.gameObject);
                return instance;
            }

            var networkGo = new GameObject("NetworkGameClient");
            networkGo.transform.SetParent(persistentRoot, false);
            NetworkGameClient runtimeClient = networkGo.AddComponent<NetworkGameClient>();
            Object.DontDestroyOnLoad(networkGo);
            Debug.LogWarning("[PersistentGameContextInstaller] NetworkGameClient prefab not assigned. Created runtime instance with default values.");
            return runtimeClient;
        }

        private static StatsClientSystem ResolveOrCreateStats()
        {
            if (GameContext.TryGetSystem(out StatsClientSystem existing))
                return existing;

            var created = new StatsClientSystem();
            GameContext.RegisterSystem(created);
            return created;
        }

        private static InventoryClientSystem ResolveOrCreateInventory()
        {
            if (GameContext.TryGetSystem(out InventoryClientSystem existing))
                return existing;

            var created = new InventoryClientSystem(catalogResolver: GameContext.CatalogResolver);
            GameContext.RegisterSystem(created);
            return created;
        }

        private static EquipmentClientSystem ResolveOrCreateEquipment()
        {
            if (GameContext.TryGetSystem(out EquipmentClientSystem existing))
                return existing;

            var created = new EquipmentClientSystem(GameContext.CatalogResolver);
            GameContext.RegisterSystem(created);
            return created;
        }

        private static ConsumableClientSystem ResolveOrCreateConsumable(InventoryClientSystem inventory, StatsClientSystem stats)
        {
            if (GameContext.TryGetSystem(out ConsumableClientSystem existing))
                return existing;

            var created = new ConsumableClientSystem(GameContext.CatalogResolver, inventory, stats);
            GameContext.RegisterSystem(created);
            return created;
        }

        private static PetClientSystem ResolveOrCreatePet()
        {
            if (GameContext.TryGetSystem(out PetClientSystem existing))
                return existing;

            var created = new PetClientSystem();
            GameContext.RegisterSystem(created);
            return created;
        }
    }
}
