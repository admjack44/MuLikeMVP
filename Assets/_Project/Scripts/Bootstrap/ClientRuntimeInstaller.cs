using MuLike.Core;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Explicit composition root for client runtime services and systems.
    /// </summary>
    public sealed class ClientRuntimeInstaller
    {
        public sealed class RuntimeDependencies
        {
            public NetworkGameClient NetworkClient;
            public SceneController SceneController;
            public FrontendFlowDirector FrontendFlowDirector;
        }

        public sealed class RuntimeComposition
        {
            public CatalogResolver CatalogResolver;
            public StatsClientSystem StatsClientSystem;
            public InventoryClientSystem InventoryClientSystem;
            public EquipmentClientSystem EquipmentClientSystem;
            public PetClientSystem PetClientSystem;
            public ChatClientSystem ChatClientSystem;
            public CharacterSessionSystem CharacterSessionSystem;
            public SkillBookClientSystem SkillBookClientSystem;
            public WorldStateSystem WorldStateSystem;
            public SessionStateClient SessionStateClient;
            public SceneFlowService SceneFlowService;
        }

        public RuntimeComposition Install(RuntimeDependencies dependencies = null)
        {
            dependencies ??= new RuntimeDependencies();

            Debug.Log("[ClientRuntimeInstaller] Building client runtime composition...");

            CatalogResolver catalogResolver = new CatalogResolver();
            CatalogResolver.ItemCatalogLoadSummary catalogSummary = catalogResolver.LoadItemCatalog();
            Debug.Log($"[ClientRuntimeInstaller] Catalog loaded. Mode={catalogSummary.Mode} Items={catalogSummary.LoadedCount}");

            var statsSystem = new StatsClientSystem();
            var inventorySystem = new InventoryClientSystem(catalogResolver: catalogResolver);
            var equipmentSystem = new EquipmentClientSystem(catalogResolver);
            var petSystem = new PetClientSystem();
            var chatSystem = new ChatClientSystem();
            var characterSessionSystem = new CharacterSessionSystem();
            var skillBookSystem = new SkillBookClientSystem();
            var worldStateSystem = new WorldStateSystem();
            var sessionStateClient = new SessionStateClient();

            SceneController sceneController = dependencies.SceneController != null
                ? dependencies.SceneController
                : SceneController.EnsureInstance();

            FrontendFlowDirector flowDirector = dependencies.FrontendFlowDirector != null
                ? dependencies.FrontendFlowDirector
                : FrontendFlowDirector.EnsureInstance();

            var sceneFlowService = new SceneFlowService(sceneController, flowDirector, sessionStateClient);

            var composition = new RuntimeComposition
            {
                CatalogResolver = catalogResolver,
                StatsClientSystem = statsSystem,
                InventoryClientSystem = inventorySystem,
                EquipmentClientSystem = equipmentSystem,
                PetClientSystem = petSystem,
                ChatClientSystem = chatSystem,
                CharacterSessionSystem = characterSessionSystem,
                SkillBookClientSystem = skillBookSystem,
                WorldStateSystem = worldStateSystem,
                SessionStateClient = sessionStateClient,
                SceneFlowService = sceneFlowService
            };

            Debug.Log("[ClientRuntimeInstaller] Client runtime composition ready.");
            return composition;
        }
    }
}