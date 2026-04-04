#if UNITY_EDITOR
using MuLike.Core;
using MuLike.Gameplay.Controllers;
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
    /// One-click setup for World_Dev scene with bootstrap, networking debug tools and player placeholder.
    /// </summary>
    public static class WorldDevSceneSetupBuilder
    {
        private const string WorldDevPrimaryPath = "Assets/_Project/Scenes/World_Dev/World_Dev.unity";
        private const string WorldDevFallbackPath = "Assets/_Project/Scenes/World_Dev.unity";
        private const string DebugPrefabPath = "Assets/_Project/Prefabs/UI/NetworkDebugPanel.prefab";

        [MenuItem("MuLike/Build/Setup World_Dev Scene")]
        public static void SetupWorldDevScene()
        {
            EnsureDebugPrefab();

            string scenePath = ResolveOrCreateWorldDevScenePath();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EnsureBootstrap(scene);
            NetworkGameClient client = EnsureNetworkClient(scene);
            EnsureDebugPanel(scene, client);
            EnsureEventSystem(scene);
            EnsureGround(scene);
            EnsurePlayerPlaceholder(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            EnsureSceneInBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldDevSceneSetupBuilder] World_Dev setup completed: {scenePath}");
        }

        private static string ResolveOrCreateWorldDevScenePath()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldDevPrimaryPath) != null)
                return WorldDevPrimaryPath;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldDevFallbackPath) != null)
                return WorldDevFallbackPath;

            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, WorldDevPrimaryPath);
            return WorldDevPrimaryPath;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes/World_Dev"))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "World_Dev");
        }

        private static void EnsureDebugPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugPrefabPath);
            if (prefab == null)
                NetworkDebugUIPrefabBuilder.CreatePrefab();
        }

        private static void EnsureBootstrap(Scene scene)
        {
            GameBootstrap bootstrap = FindInScene<GameBootstrap>(scene);
            if (bootstrap != null) return;

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static NetworkGameClient EnsureNetworkClient(Scene scene)
        {
            NetworkGameClient client = FindInScene<NetworkGameClient>(scene);
            if (client == null)
            {
                var go = new GameObject("NetworkGameClient");
                client = go.AddComponent<NetworkGameClient>();
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            var so = new SerializedObject(client);
            so.FindProperty("_useInMemoryGateway").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return client;
        }

        private static void EnsureDebugPanel(Scene scene, NetworkGameClient client)
        {
            NetworkDebugPanel panel = FindInScene<NetworkDebugPanel>(scene);
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    panel = instance != null ? instance.GetComponent<NetworkDebugPanel>() : null;
                }
            }

            if (panel != null)
            {
                var so = new SerializedObject(panel);
                so.FindProperty("_client").objectReferenceValue = client;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem existing = FindInScene<EventSystem>(scene);
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
                eventSystemGo.AddComponent<InputSystemUIInputModule>();

            var legacyModule = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
                Object.DestroyImmediate(legacyModule);
#else
            if (eventSystemGo.GetComponent<StandaloneInputModule>() == null)
                eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void EnsureGround(Scene scene)
        {
            GameObject ground = FindGameObjectInScene(scene, "Ground");
            if (ground != null) return;

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            SceneManager.MoveGameObjectToScene(ground, scene);
        }

        private static void EnsurePlayerPlaceholder(Scene scene)
        {
            GameObject player = FindGameObjectInScene(scene, "PlayerPlaceholder");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "PlayerPlaceholder";
                player.transform.position = new Vector3(0f, 1f, 0f);
                SceneManager.MoveGameObjectToScene(player, scene);
            }

            if (player.GetComponent<CharacterController>() == null)
                player.AddComponent<CharacterController>();

            if (player.GetComponent<CharacterMotor>() == null)
                player.AddComponent<CharacterMotor>();

            TargetingController targeting = player.GetComponent<TargetingController>();
            if (targeting == null)
                targeting = player.AddComponent<TargetingController>();

            SkillCastController skillCast = player.GetComponent<SkillCastController>();
            if (skillCast == null)
                skillCast = player.AddComponent<SkillCastController>();

            var so = new SerializedObject(skillCast);
            so.FindProperty("_targeting").objectReferenceValue = targeting;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject FindGameObjectInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] children = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    if (children[j].name == name)
                        return children[j].gameObject;
                }
            }

            return null;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == scenePath)
                {
                    if (!existing[i].enabled)
                    {
                        existing[i] = new EditorBuildSettingsScene(scenePath, true);
                        EditorBuildSettings.scenes = existing;
                    }
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            for (int i = 0; i < existing.Length; i++)
                updated[i] = existing[i];

            updated[existing.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
