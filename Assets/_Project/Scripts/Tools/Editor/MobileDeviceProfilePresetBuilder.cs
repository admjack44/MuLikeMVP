#if UNITY_EDITOR
using MuLike.Performance.Runtime;
using UnityEditor;
using UnityEngine;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Creates low/mid/high Android mobile device profile preset assets.
    /// </summary>
    public static class MobileDeviceProfilePresetBuilder
    {
        private const string Folder = "Assets/_Project/ScriptableObjects/Performance";

        [MenuItem("MuLike/Performance/Create Mobile Device Presets")]
        public static void CreatePresets()
        {
            EnsureFolder();

            CreateLow();
            CreateMid();
            CreateHigh();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MobileDeviceProfilePresetBuilder] Presets created/updated.");
        }

        private static void CreateLow()
        {
            MobileDeviceProfile profile = CreateOrLoad("MobileDeviceProfile_AndroidLow.asset");
            profile.profileId = "android-low";
            profile.tier = MobileDeviceTier.Low;
            profile.targetFrameRate = 45;
            profile.fallbackFrameRate = 30;
            profile.renderScale = 0.82f;
            profile.enableShadows = true;
            profile.shadowDistance = 16f;
            profile.shadowResolution = ShadowResolution.Low;
            profile.enablePostProcess = false;
            profile.masterTextureLimit = 1;
            profile.lodBias = 0.65f;
            profile.antiAliasing = 0;
            profile.enableScheduledGcCollection = true;
            profile.scheduledGcIntervalSeconds = 35f;
            profile.minFpsForScheduledGc = 35;
            EditorUtility.SetDirty(profile);
        }

        private static void CreateMid()
        {
            MobileDeviceProfile profile = CreateOrLoad("MobileDeviceProfile_AndroidMid.asset");
            profile.profileId = "android-mid";
            profile.tier = MobileDeviceTier.Mid;
            profile.targetFrameRate = 60;
            profile.fallbackFrameRate = 45;
            profile.renderScale = 0.9f;
            profile.enableShadows = true;
            profile.shadowDistance = 22f;
            profile.shadowResolution = ShadowResolution.Medium;
            profile.enablePostProcess = false;
            profile.masterTextureLimit = 0;
            profile.lodBias = 0.75f;
            profile.antiAliasing = 0;
            profile.enableScheduledGcCollection = true;
            profile.scheduledGcIntervalSeconds = 45f;
            profile.minFpsForScheduledGc = 50;
            EditorUtility.SetDirty(profile);
        }

        private static void CreateHigh()
        {
            MobileDeviceProfile profile = CreateOrLoad("MobileDeviceProfile_AndroidHigh.asset");
            profile.profileId = "android-high";
            profile.tier = MobileDeviceTier.High;
            profile.targetFrameRate = 60;
            profile.fallbackFrameRate = 45;
            profile.renderScale = 0.95f;
            profile.enableShadows = true;
            profile.shadowDistance = 30f;
            profile.shadowResolution = ShadowResolution.High;
            profile.enablePostProcess = true;
            profile.masterTextureLimit = 0;
            profile.lodBias = 1f;
            profile.antiAliasing = 2;
            profile.enableScheduledGcCollection = false;
            EditorUtility.SetDirty(profile);
        }

        private static MobileDeviceProfile CreateOrLoad(string fileName)
        {
            string path = Folder + "/" + fileName;
            MobileDeviceProfile existing = AssetDatabase.LoadAssetAtPath<MobileDeviceProfile>(path);
            if (existing != null)
                return existing;

            MobileDeviceProfile created = ScriptableObject.CreateInstance<MobileDeviceProfile>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Performance");
        }
    }
}
#endif
