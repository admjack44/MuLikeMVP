using System;
using MuLike.Bootstrap;
using MuLike.Networking;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MuLike.Core
{
    /// <summary>
    /// Client entry point that owns runtime bootstrap and shutdown lifecycle.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;
        private bool _ownsRuntime;

        [Header("Frontend Flow")]
        [SerializeField] private bool _autoEnterBootFlow = true;

        [Header("Runtime Dependencies")]
        [SerializeField] private NetworkGameClient _networkClient;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameBootstrap] Duplicate bootstrap detected. Destroying duplicate instance.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _ownsRuntime = !GameContext.IsInitialized;

            DontDestroyOnLoad(gameObject);
            EnsureEventSystemInputModule();
            EnsurePersistentFlowInfrastructure();
            InitializeRuntime();
        }

        private void Start()
        {
            if (!_autoEnterBootFlow || !GameContext.IsInitialized)
                return;

            if (GameContext.SceneFlowService != null)
            {
                GameContext.SceneFlowService.EnterBoot();
                return;
            }

            FrontendFlowDirector director = FrontendFlowDirector.EnsureInstance();
            director.EnterBoot();
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            _instance = null;

            if (_ownsRuntime)
            {
                Debug.Log("[GameBootstrap] Destroying owner bootstrap. Triggering runtime shutdown...");
                GameContext.Shutdown();
            }
        }

        private void InitializeRuntime()
        {
            if (!_ownsRuntime)
            {
                Debug.Log("[GameBootstrap] Runtime already initialized by another owner.");
                return;
            }

            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();

            var dependencies = new ClientRuntimeInstaller.RuntimeDependencies
            {
                NetworkClient = _networkClient,
                SceneController = SceneController.Instance != null ? SceneController.Instance : SceneController.EnsureInstance(),
                FrontendFlowDirector = FrontendFlowDirector.Instance != null ? FrontendFlowDirector.Instance : FrontendFlowDirector.EnsureInstance()
            };

            Debug.Log("[GameBootstrap] Initializing client runtime composition...");
            GameContext.Initialize(dependencies);
            Debug.Log("[GameBootstrap] Client runtime composition initialized.");
        }

        private static void EnsureEventSystemInputModule()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
                return;

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType == null)
                return;

            if (eventSystem.gameObject.GetComponent(inputSystemModuleType) == null)
                eventSystem.gameObject.AddComponent(inputSystemModuleType);

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Destroy(legacy);
        }

        private static void EnsurePersistentFlowInfrastructure()
        {
            SceneController.EnsureInstance();
            FrontendFlowDirector.EnsureInstance();
            Debug.Log("[GameBootstrap] Persistent flow infrastructure ready.");
        }
    }
}
