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
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureEventSystemInputModule();
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            GameContext.Initialize();
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
