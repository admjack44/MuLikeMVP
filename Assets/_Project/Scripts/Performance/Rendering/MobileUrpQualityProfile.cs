using UnityEngine;

namespace MuLike.Performance.Rendering
{
    [CreateAssetMenu(fileName = "MobileUrpQualityProfile", menuName = "MuLike/Performance/Mobile URP Quality Profile")]
    public sealed class MobileUrpQualityProfile : ScriptableObject
    {
        [Header("Frame")]
        [Range(30, 120)] public int targetFrameRate = 60;
        [Range(0, 1)] public int vSyncCount = 0;

        [Header("Quality")]
        [Range(0f, 100f)] public float shadowDistance = 24f;
        [Range(0f, 8f)] public float lodBias = 0.7f;
        [Range(0, 4)] public int antiAliasing = 0;
        [Range(0f, 1f)] public float renderScale = 0.9f;
        public bool supportsHDR = false;
        public bool supportsSoftParticles = false;
    }
}