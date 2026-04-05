using UnityEditor;
using UnityEngine;
using MuLike.Crafting;
using MuLike.Economy;
using MuLike.UI.Economy;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Creates a minimal runtime setup in the active scene for testing economy systems.
    /// Adds managers if missing and instantiates the EconomyUI prefab when available.
    /// </summary>
    public static class EconomyDemoSetupBuilder
    {
        private const string EconomyPrefabPath = "Assets/_Project/Prefabs/UI/EconomyUI.prefab";

        [MenuItem("MuLike/Build/Create Economy Demo Setup In Scene")]
        public static void BuildInScene()
        {
            GameObject root = GameObject.Find("EconomyRuntime") ?? new GameObject("EconomyRuntime");

            EnsureComponent<CurrencyManager>(root);
            EnsureComponent<TradeSystem>(root);
            EnsureComponent<AuctionHouse>(root);
            EnsureComponent<ChaosMachine>(root);
            EnsureComponent<EconomyNetworkBridge>(root);
            EnsureComponent<EconomyDemoBootstrap>(root);

            if (Object.FindAnyObjectByType<EconomyHubView>(FindObjectsInactive.Include) == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EconomyPrefabPath);
                if (prefab != null)
                {
                    PrefabUtility.InstantiatePrefab(prefab);
                }
                else
                {
                    Debug.LogWarning($"[EconomyDemoSetupBuilder] Economy UI prefab not found at {EconomyPrefabPath}. Build it first.");
                }
            }

            Selection.activeGameObject = root;
            Debug.Log("[EconomyDemoSetupBuilder] Economy runtime added to active scene.");
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }
    }
}