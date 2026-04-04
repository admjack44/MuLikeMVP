#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Replaces StandaloneInputModule with InputSystemUIInputModule in the active scene.
    /// </summary>
    public static class InputSystemEventSystemFixer
    {
        [MenuItem("MuLike/Fix/Use Input System UI Module (Current Scene)")]
        public static void FixCurrentSceneEventSystems()
        {
            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType == null)
            {
                Debug.LogWarning("[InputSystemEventSystemFixer] Unity Input System package not found. Nothing to fix.");
                return;
            }

            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);
            if (eventSystems.Length == 0)
            {
                Debug.LogWarning("[InputSystemEventSystemFixer] No EventSystem found in current scene.");
                return;
            }

            int fixedCount = 0;
            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem eventSystem = eventSystems[i];
                GameObject go = eventSystem.gameObject;

                if (go.GetComponent(inputSystemModuleType) == null)
                    go.AddComponent(inputSystemModuleType);

                StandaloneInputModule legacy = go.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacy, true);
                    fixedCount++;
                }
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[InputSystemEventSystemFixer] Fixed {fixedCount} EventSystem(s). Save the scene.");
        }
    }
}
#endif
