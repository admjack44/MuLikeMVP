using System;
using System.Threading.Tasks;
using MuLike.ContentPipeline.Runtime;
using MuLike.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Persistent composition root for the client runtime.
    /// </summary>
    public class ClientBootstrap : MonoBehaviour
    {
        [Serializable]
        public sealed class SceneFlowSettings
        {
            public string BootSceneName = "Boot";
            public string LoginSceneName = "Login";
            public string CharacterSelectSceneName = "CharacterSelect";
            public string DefaultWorldSceneName = "World_Dev";
            public string TownSceneName = "Town_01";
            public string FieldSceneName = "Field_01";
            public string DefaultCharacterName = "Player";
            public bool EnableHudDebugConsole = true;
        }

        private static ClientBootstrap _instance;

        [Header("Persistence")]
        [SerializeField] private bool _persistAcrossScenes = true;

        [Header("Install")]
        [SerializeField] private NetworkGameClient _networkClientPrefab;
        [SerializeField] private bool _tryFindSceneNetworkClientAsFallback = true;
        [SerializeField] private int _mobileTargetFrameRate = 60;

        [Header("Flow")]
        [SerializeField] private SceneFlowSettings _flow = new();

        private ClientServiceRegistry _registry;
        private SceneFlowCoordinator _sceneFlowCoordinator;
        private bool _isBootstrapped;

        public static ClientBootstrap Instance => _instance;
        public bool IsBootstrapped => _isBootstrapped;
        public IClientServiceRegistry Services => _registry;
        public ISceneFlowCoordinator SceneFlow => _sceneFlowCoordinator;

        public event Action OnBootstrapped;
        public event Action OnLoginReady;
        public event Action OnWorldReady;

        public static ClientBootstrap EnsureInstance()
        {
            if (_instance != null)
                return _instance;

            ClientBootstrap found = FindObjectOfType<ClientBootstrap>();
            if (found != null)
                return found;

            var go = new GameObject("ClientBootstrap");
            return go.AddComponent<ClientBootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ClientBootstrap] Duplicate instance found. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (_persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _registry?.Clear();
            _registry = null;
            _sceneFlowCoordinator = null;
            _isBootstrapped = false;
            _instance = null;
        }

        public bool BootstrapClient()
        {
            if (_isBootstrapped)
                return true;

            _registry = new ClientServiceRegistry();

            var installer = new PersistentGameContextInstaller();
            PersistentGameContextInstaller.Result installResult = installer.Install(
                _registry,
                transform,
                _networkClientPrefab,
                _tryFindSceneNetworkClientAsFallback,
                _mobileTargetFrameRate);

            if (installResult == null)
            {
                Debug.LogError("[ClientBootstrap] Installation failed.");
                return false;
            }

            _sceneFlowCoordinator = new SceneFlowCoordinator(_registry, _flow);
            _sceneFlowCoordinator.OnLoginReady += () => OnLoginReady?.Invoke();
            _sceneFlowCoordinator.OnWorldReady += () => OnWorldReady?.Invoke();

            _ = PreloadBootDependenciesAsync();

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneFlowCoordinator.ConfigureForScene(SceneManager.GetActiveScene());

            _isBootstrapped = true;
            OnBootstrapped?.Invoke();
            Debug.Log("[ClientBootstrap] Client bootstrapped successfully.");
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (!_isBootstrapped)
                return;

            _sceneFlowCoordinator?.ConfigureForScene(scene);
        }

        private async Task PreloadBootDependenciesAsync()
        {
            if (!_registry.TryResolve(out IContentAddressablesService contentService) || contentService == null)
            {
                Debug.LogWarning("[ClientBootstrap] ContentAddressablesService is unavailable. Boot preload skipped.");
                return;
            }

            try
            {
                await contentService.PreloadGroupAsync(
                    groupName: "boot.core",
                    labels: ContentAddressablesLabels.LoginPreload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientBootstrap] Boot preload failed: {ex.Message}");
            }
        }
    }
}
