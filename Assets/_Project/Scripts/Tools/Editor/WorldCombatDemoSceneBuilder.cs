#if UNITY_EDITOR
using MuLike.Bootstrap;
using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Networking;
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
    /// Builds a minimal playable combat slice scene for mobile-first iteration.
    /// </summary>
    public static class WorldCombatDemoSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/World_Demo/World_CombatSlice_Demo.unity";

        [MenuItem("MuLike/Build/Setup Combat Vertical Slice Demo")]
        public static void SetupScene()
        {
            EnsureFolders();
            WorldCombatDemoPrefabBuilder.CreatePrefabs();

            Scene scene = ResolveOrCreateScene();
            EnsureEventSystem(scene);
            EnsureGround(scene);
            EnsureMainCamera(scene);
            GameObject player = EnsurePlayer(scene);
            EnsureNetworkClient(scene);
            EnsureWorldInstallers(scene, player.transform);
            EnsureEnemySpawnPoints(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldCombatDemoSceneBuilder] Scene ready: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes/World_Demo"))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "World_Demo");
        }

        private static Scene ResolveOrCreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
                return;

            GameObject go = new("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static void EnsureGround(Scene scene)
        {
            GameObject ground = FindByName(scene, "Ground");
            if (ground != null)
                return;

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            SceneManager.MoveGameObjectToScene(ground, scene);
        }

        private static void EnsureMainCamera(Scene scene)
        {
            Camera camera = Camera.main;
            if (camera != null)
                return;

            GameObject camGo = new("Main Camera");
            camera = camGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 11f, -10f);
            camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            SceneManager.MoveGameObjectToScene(camGo, scene);

            if (camGo.GetComponent<CameraFollowController>() == null)
                camGo.AddComponent<CameraFollowController>();
        }

        private static GameObject EnsurePlayer(Scene scene)
        {
            GameObject player = FindByName(scene, "Player_Demo");
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "Player_Demo";
                player.transform.position = new Vector3(0f, 1f, 0f);
                SceneManager.MoveGameObjectToScene(player, scene);
            }

            if (player.GetComponent<CharacterController>() == null)
                player.AddComponent<CharacterController>();

            if (player.GetComponent<CharacterMotor>() == null)
                player.AddComponent<CharacterMotor>();

            if (player.GetComponent<TargetingController>() == null)
                player.AddComponent<TargetingController>();

            if (player.GetComponent<CombatController>() == null)
                player.AddComponent<CombatController>();

            return player;
        }

        private static void EnsureNetworkClient(Scene scene)
        {
            NetworkGameClient client = Object.FindFirstObjectByType<NetworkGameClient>();
            if (client == null)
            {
                GameObject go = new("NetworkGameClient");
                client = go.AddComponent<NetworkGameClient>();
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            SerializedObject so = new(client);
            so.FindProperty("_useInMemoryGateway").boolValue = true;
            so.FindProperty("_autoConnectOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (client.GetComponent<DemoAutoAuthController>() == null)
                client.gameObject.AddComponent<DemoAutoAuthController>();
        }

        private static void EnsureWorldInstallers(Scene scene, Transform player)
        {
            WorldVerticalSliceInstaller vertical = Object.FindFirstObjectByType<WorldVerticalSliceInstaller>();
            if (vertical == null)
            {
                GameObject go = new("WorldVerticalSliceInstaller");
                vertical = go.AddComponent<WorldVerticalSliceInstaller>();
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            WorldCombatDemoInstaller combatDemo = Object.FindFirstObjectByType<WorldCombatDemoInstaller>();
            if (combatDemo == null)
            {
                GameObject go = new("WorldCombatDemoInstaller");
                combatDemo = go.AddComponent<WorldCombatDemoInstaller>();
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            GameObject spawn = FindByName(scene, "PlayerSpawn");
            if (spawn == null)
            {
                spawn = new GameObject("PlayerSpawn");
                spawn.transform.position = new Vector3(0f, 1f, 0f);
                SceneManager.MoveGameObjectToScene(spawn, scene);
            }

            SerializedObject so = new(combatDemo);
            GameObject mobPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldCombatDemoPrefabBuilder.MobPrefabPath);
            so.FindProperty("_playerMotor").objectReferenceValue = player.GetComponent<CharacterMotor>();
            so.FindProperty("_playerSpawnPoint").objectReferenceValue = spawn.transform;
            so.FindProperty("_mobPrefab").objectReferenceValue = mobPrefab != null ? mobPrefab.GetComponent<DemoMobRuntime>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureEnemySpawnPoints(Scene scene)
        {
            GameObject root = FindByName(scene, "EnemySpawns");
            if (root == null)
            {
                root = new GameObject("EnemySpawns");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            for (int i = 0; i < 5; i++)
            {
                string name = $"Spawn_{i + 1}";
                Transform child = root.transform.Find(name);
                if (child != null)
                    continue;

                var spawn = new GameObject(name).transform;
                spawn.SetParent(root.transform);
                float angle = i / 5f * Mathf.PI * 2f;
                spawn.position = new Vector3(Mathf.Cos(angle) * 7f, 1f, Mathf.Sin(angle) * 7f);
            }

            WorldCombatDemoInstaller installer = Object.FindFirstObjectByType<WorldCombatDemoInstaller>();
            if (installer == null)
                return;

            var points = new Transform[root.transform.childCount];
            for (int i = 0; i < points.Length; i++)
                points[i] = root.transform.GetChild(i);

            SerializedObject so = new(installer);
            so.FindProperty("_enemySpawnPoints").arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
                so.FindProperty("_enemySpawnPoints").GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];

                Transform found = roots[i].transform.Find(name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }
    }
}
#endif
