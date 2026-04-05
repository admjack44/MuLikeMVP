using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MuLike.Performance.Rendering
{
    /// <summary>
    /// Applies Android mid-range URP and Quality settings from a profile.
    /// </summary>
    public sealed class MobileUrpQualityApplier : MonoBehaviour
    {
        [SerializeField] private MobileUrpQualityProfile _profile;
        [SerializeField] private bool _applyOnAwake = true;

        private void Awake()
        {
            if (_applyOnAwake)
                Apply();
        }

        [ContextMenu("Apply Mobile URP Quality")]
        public void Apply()
        {
            if (_profile == null)
            {
                Debug.LogWarning("[MobileUrpQualityApplier] Profile missing.");
                return;
            }

            QualitySettings.vSyncCount = _profile.vSyncCount;
            Application.targetFrameRate = _profile.targetFrameRate;
            QualitySettings.shadowDistance = _profile.shadowDistance;
            QualitySettings.lodBias = _profile.lodBias;
            QualitySettings.antiAliasing = _profile.antiAliasing;

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = _profile.renderScale;
                urp.supportsHDR = _profile.supportsHDR;
                TrySetUrpBoolProperty(urp, "supportsCameraOpaqueTexture", false);
                TrySetUrpBoolProperty(urp, "supportsSoftParticles", _profile.supportsSoftParticles);
            }
            else
            {
                Debug.LogWarning("[MobileUrpQualityApplier] Current render pipeline is not URP. URP-specific settings were skipped.");
            }

            Debug.Log($"[MobileUrpQualityApplier] Applied profile '{_profile.name}' with target FPS {_profile.targetFrameRate}.");
        }

        private static void TrySetUrpBoolProperty(UniversalRenderPipelineAsset urp, string propertyName, bool value)
        {
            var property = typeof(UniversalRenderPipelineAsset).GetProperty(propertyName);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
                property.SetValue(urp, value, null);
        }
    }
}