#if UNITY_EDITOR
using MuLike.Networking;
using MuLike.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Creates a ready-to-play Net_Debug scene with NetworkGameClient and debug UI.
    /// </summary>
    public static class NetworkDebugSceneBuilder
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/Net_Debug.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/NetworkDebugPanel.prefab";

        [MenuItem("MuLike/Build/Create Net Debug Scene")]
        public static void CreateNetDebugScene()
        {
            EnsureSceneFolder();
            EnsureDebugPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var clientGo = new GameObject("NetworkGameClient");
            var client = clientGo.AddComponent<NetworkGameClient>();
            var clientSo = new SerializedObject(client);
            clientSo.FindProperty("_useInMemoryGateway").boolValue = true;
            clientSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                var uiInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var panel = uiInstance.GetComponent<NetworkDebugPanel>();
                if (panel != null)
                {
                    var panelSo = new SerializedObject(panel);
                    panelSo.FindProperty("_client").objectReferenceValue = client;
                    panelSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkDebugSceneBuilder] Prefab not found at {PrefabPath}. Scene created without debug panel.");
            }

            EnsureEventSystem(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[NetworkDebugSceneBuilder] Scene created: {ScenePath}");
        }

        private static void EnsureSceneInBuildSettings()
        {
            var existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == ScenePath)
                {
                    if (!existing[i].enabled)
                    {
                        existing[i] = new EditorBuildSettingsScene(ScenePath, true);
                        EditorBuildSettings.scenes = existing;
                    }
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            for (int i = 0; i < existing.Length; i++)
            {
                updated[i] = existing[i];
            }

            updated[existing.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");

            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
        }

        private static void EnsureDebugPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                NetworkDebugUIPrefabBuilder.CreatePrefab();
            }
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem existing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                existing = root.GetComponentInChildren<EventSystem>(true);
                if (existing != null)
                    break;
            }

            GameObject eventSystemGo;
            if (existing == null)
            {
                eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventSystemGo, scene);
            }
            else
            {
                eventSystemGo = existing.gameObject;
            }

            EnsureUiInputModule(eventSystemGo);
        }

        private static void EnsureUiInputModule(GameObject eventSystemGo)
        {
#if ENABLE_INPUT_SYSTEM
            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var legacyModule = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }
#else
            if (eventSystemGo.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }
#endif
        }
    }
}
#endif
