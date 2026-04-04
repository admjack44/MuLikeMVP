using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MuLike.Core
{
    /// <summary>
    /// Entry point of the application. Initializes all core systems on startup.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;
        private bool _ownsRuntime;

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
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            Debug.Log("[GameBootstrap] Initializing runtime systems...");
            GameContext.Initialize();
            Debug.Log("[GameBootstrap] Runtime systems ready.");
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            _instance = null;

            // Only the bootstrap that started the runtime should tear it down.
            if (_ownsRuntime)
            {
                Debug.Log("[GameBootstrap] Shutting down runtime systems...");
                GameContext.Shutdown();
            }
        }

        private static void EnsureEventSystemInputModule()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null) return;

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType == null) return;

            if (eventSystem.gameObject.GetComponent(inputSystemModuleType) == null)
                eventSystem.gameObject.AddComponent(inputSystemModuleType);

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Destroy(legacy);
        }
    }
}
