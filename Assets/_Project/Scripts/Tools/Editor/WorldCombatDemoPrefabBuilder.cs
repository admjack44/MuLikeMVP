#if UNITY_EDITOR
using MuLike.Gameplay.Entities;
using UnityEditor;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Generates lightweight reusable prefabs for combat demo iteration.
    /// </summary>
    public static class WorldCombatDemoPrefabBuilder
    {
        public const string RootFolder = "Assets/_Project/Prefabs/Gameplay/Demo";
        public const string MobPrefabPath = RootFolder + "/DemoMob.prefab";
        public const string LootPrefabPath = RootFolder + "/DemoLootDrop.prefab";

        [MenuItem("MuLike/Build/Create Combat Demo Prefabs")]
        public static void CreatePrefabs()
        {
            EnsureFolders();
            CreateMobPrefab();
            CreateLootPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WorldCombatDemoPrefabBuilder] Demo prefabs generated.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Gameplay"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Gameplay");

            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Gameplay", "Demo");
        }

        private static void CreateMobPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MobPrefabPath) != null)
                return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "DemoMob";

            if (go.GetComponent<EntityView>() == null)
                go.AddComponent<MonsterView>();

            if (go.GetComponent<TargetHudRuntimeData>() == null)
                go.AddComponent<TargetHudRuntimeData>();

            if (go.GetComponent<DemoMobRuntime>() == null)
                go.AddComponent<DemoMobRuntime>();

            PrefabUtility.SaveAsPrefabAsset(go, MobPrefabPath);
            Object.DestroyImmediate(go);
        }

        private static void CreateLootPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LootPrefabPath) != null)
                return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DemoLootDrop";
            go.transform.localScale = Vector3.one * 0.45f;

            if (go.GetComponent<EntityView>() == null)
                go.AddComponent<DropView>();

            PrefabUtility.SaveAsPrefabAsset(go, LootPrefabPath);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
