#if UNITY_EDITOR
using MuLike.Performance.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    public sealed class MobilePerformanceToolkitWindow : EditorWindow
    {
        [MenuItem("MuLike/Performance/Mobile Toolkit")]
        public static void Open()
        {
            GetWindow<MobilePerformanceToolkitWindow>("Mobile Toolkit");
        }

        private void OnGUI()
        {
            GUILayout.Label("Mobile Performance Toolkit", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Profiler"))
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");

            if (GUILayout.Button("Open Frame Debugger"))
                EditorApplication.ExecuteMenuItem("Window/Analysis/Frame Debugger");

            if (GUILayout.Button("Apply Mid-Tier Android Quality"))
                ApplyMidTierDefaults();

            GUILayout.Space(8f);
            GUILayout.Label("Current Scene Quick Stats", EditorStyles.boldLabel);
            GUILayout.Label($"Batches: {UnityStats.batches}");
            GUILayout.Label($"SetPass Calls: {UnityStats.setPassCalls}");
            GUILayout.Label($"Triangles: {UnityStats.triangles}");
            GUILayout.Label($"Vertices: {UnityStats.vertices}");

            GUILayout.Space(8f);
            if (GUILayout.Button("Create MobileRenderBudgetProfile Asset"))
                CreateBudgetAsset();
        }

        private static void ApplyMidTierDefaults()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 25f;
            QualitySettings.antiAliasing = 0;
            Debug.Log("[MobilePerformanceToolkit] Applied baseline mid-tier Android defaults.");
        }

        private static void CreateBudgetAsset()
        {
            var asset = CreateInstance<MobileRenderBudgetProfile>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/ScriptableObjects/MobileRenderBudgetProfile.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
#endif
