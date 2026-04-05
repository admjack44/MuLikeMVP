using UnityEngine;

namespace MuLike.Performance.Runtime
{
    public enum MobileDeviceTier
    {
        Low = 0,
        Mid = 1,
        High = 2
    }

    [CreateAssetMenu(fileName = "MobileDeviceProfile", menuName = "MuLike/Performance/Mobile Device Profile")]
    public sealed class MobileDeviceProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "android-mid";
        public MobileDeviceTier tier = MobileDeviceTier.Mid;

        [Header("Frame")]
        [Range(30, 120)] public int targetFrameRate = 60;
        [Range(30, 120)] public int fallbackFrameRate = 45;
        [Range(0, 1)] public int vSyncCount = 0;

        [Header("Rendering")]
        [Range(0.6f, 1f)] public float renderScale = 0.9f;
        public bool supportsHdr = false;
        public bool supportsSoftParticles = false;
        public bool supportsOpaqueTexture = false;
        [Range(0f, 4f)] public float lodBias = 0.75f;
        [Range(0, 4)] public int antiAliasing = 0;

        [Header("Shadows")]
        public bool enableShadows = true;
        [Range(0f, 100f)] public float shadowDistance = 22f;
        public ShadowQuality shadowQuality = ShadowQuality.All;
        public ShadowResolution shadowResolution = ShadowResolution.Medium;

        [Header("Post Process")]
        public bool enablePostProcess = false;

        [Header("Textures")]
        [Range(0, 3)] public int masterTextureLimit = 0;
        public AnisotropicFiltering anisotropicFiltering = AnisotropicFiltering.Disable;

        [Header("Batching")]
        public bool enableSrpBatcher = true;

        [Header("GC")]
        public bool enableScheduledGcCollection;
        [Range(10f, 120f)] public float scheduledGcIntervalSeconds = 45f;
        [Range(20f, 60f)] public int minFpsForScheduledGc = 50;
    }
}
