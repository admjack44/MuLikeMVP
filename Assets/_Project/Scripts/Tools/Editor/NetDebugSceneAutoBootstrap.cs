#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Auto-creates Net_Debug scene once after scripts reload if it does not exist yet.
    /// </summary>
    [InitializeOnLoad]
    public static class NetDebugSceneAutoBootstrap
    {
        private const string ScenePath = "Assets/_Project/Scenes/Net_Debug.unity";
        private const string SessionKey = "MuLike.NetDebugSceneAutoBootstrap.Done";

        static NetDebugSceneAutoBootstrap()
        {
            EditorApplication.delayCall += TryCreate;
        }

        private static void TryCreate()
        {
            if (EditorPrefs.GetBool(SessionKey, false)) return;
            EditorPrefs.SetBool(SessionKey, true);

            if (AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(ScenePath) != null)
            {
                return;
            }

            try
            {
                NetworkDebugSceneBuilder.CreateNetDebugScene();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetDebugSceneAutoBootstrap] Failed to auto-create scene: {ex.Message}");
            }
        }
    }
}
#endif
