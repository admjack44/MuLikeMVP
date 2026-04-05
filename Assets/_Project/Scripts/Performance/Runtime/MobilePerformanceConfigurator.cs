using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting;

namespace MuLike.Performance.Runtime
{
    public interface IMobileAdaptiveQualityHook
    {
        void EvaluateAndApply(MobilePerformanceConfigurator configurator);
    }

    /// <summary>
    /// Applies mobile-friendly runtime defaults and optional adaptive quality hook.
    /// </summary>
    public sealed class MobilePerformanceConfigurator : MonoBehaviour
    {
        [Header("Apply")]
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _mobileOnly = true;

        [Header("Profiles")]
        [SerializeField] private bool _autoChooseProfileByRam = true;
        [SerializeField] private MobileDeviceProfile _lowTierProfile;
        [SerializeField] private MobileDeviceProfile _midTierProfile;
        [SerializeField] private MobileDeviceProfile _highTierProfile;
        [SerializeField] private int _lowTierMaxRamMb = 3072;
        [SerializeField] private int _midTierMaxRamMb = 6144;

        [Header("Adaptive Quality")]
        [SerializeField] private bool _enableAdaptiveQualityHook;
        [SerializeField] private MonoBehaviour _adaptiveQualityHookBehaviour;
        [SerializeField] private float _adaptiveHookIntervalSeconds = 2f;

        private MobileDeviceProfile _activeProfile;
        private IMobileAdaptiveQualityHook _adaptiveHook;
        private float _nextAdaptiveTickAt;
        private float _nextScheduledGcAt;
        private float _fpsEma = 60f;

        public MobileDeviceProfile ActiveProfile => _activeProfile;
        public float SmoothedFps => _fpsEma;

        private void Awake()
        {
            ResolveAdaptiveHook();

            if (_applyOnAwake)
                ApplyDefaults();
        }

        private void Update()
        {
            float frameFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsEma = Mathf.Lerp(_fpsEma, frameFps, 0.075f);

            TickAdaptiveHook();
            TickScheduledGc();
        }

        [ContextMenu("Apply Mobile Defaults")]
        public void ApplyDefaults()
        {
            if (_mobileOnly && !IsMobileRuntime())
                return;

            _activeProfile = ResolveProfile();
            if (_activeProfile == null)
            {
                Debug.LogWarning("[MobilePerformanceConfigurator] No MobileDeviceProfile assigned.");
                return;
            }

            QualitySettings.vSyncCount = Mathf.Clamp(_activeProfile.vSyncCount, 0, 1);
            Application.targetFrameRate = Mathf.Max(30, _activeProfile.targetFrameRate);

            QualitySettings.lodBias = Mathf.Max(0.1f, _activeProfile.lodBias);
            QualitySettings.antiAliasing = Mathf.Clamp(_activeProfile.antiAliasing, 0, 4);
            QualitySettings.masterTextureLimit = Mathf.Clamp(_activeProfile.masterTextureLimit, 0, 3);
            QualitySettings.anisotropicFiltering = _activeProfile.anisotropicFiltering;

            QualitySettings.shadows = _activeProfile.enableShadows ? _activeProfile.shadowQuality : ShadowQuality.Disable;
            QualitySettings.shadowDistance = _activeProfile.enableShadows ? Mathf.Max(0f, _activeProfile.shadowDistance) : 0f;
            QualitySettings.shadowResolution = _activeProfile.shadowResolution;

            GraphicsSettings.useScriptableRenderPipelineBatching = _activeProfile.enableSrpBatcher;

            ApplyUrpSettings(_activeProfile);
            ApplyPostProcessState(_activeProfile.enablePostProcess);

            _nextScheduledGcAt = Time.unscaledTime + Mathf.Max(10f, _activeProfile.scheduledGcIntervalSeconds);

            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;

            Debug.Log($"[MobilePerformanceConfigurator] Applied profile '{_activeProfile.profileId}' (tier={_activeProfile.tier}, ram={SystemInfo.systemMemorySize}MB).");
        }

        public void FallbackToFrameRate()
        {
            if (_activeProfile == null)
                return;

            Application.targetFrameRate = Mathf.Max(30, _activeProfile.fallbackFrameRate);
        }

        private void TickAdaptiveHook()
        {
            if (!_enableAdaptiveQualityHook || _adaptiveHook == null)
                return;

            if (Time.unscaledTime < _nextAdaptiveTickAt)
                return;

            _nextAdaptiveTickAt = Time.unscaledTime + Mathf.Max(0.5f, _adaptiveHookIntervalSeconds);
            _adaptiveHook.EvaluateAndApply(this);
        }

        private void TickScheduledGc()
        {
            if (_activeProfile == null || !_activeProfile.enableScheduledGcCollection)
                return;

            if (Time.unscaledTime < _nextScheduledGcAt)
                return;

            _nextScheduledGcAt = Time.unscaledTime + Mathf.Max(10f, _activeProfile.scheduledGcIntervalSeconds);
            if (_fpsEma < Mathf.Max(20, _activeProfile.minFpsForScheduledGc))
                return;

            GC.Collect();
        }

        private MobileDeviceProfile ResolveProfile()
        {
            if (!_autoChooseProfileByRam)
                return _midTierProfile != null ? _midTierProfile : _highTierProfile;

            int ram = Math.Max(1, SystemInfo.systemMemorySize);
            if (ram <= Math.Max(1024, _lowTierMaxRamMb))
                return _lowTierProfile != null ? _lowTierProfile : _midTierProfile;

            if (ram <= Math.Max(_lowTierMaxRamMb + 1, _midTierMaxRamMb))
                return _midTierProfile != null ? _midTierProfile : _highTierProfile;

            return _highTierProfile != null ? _highTierProfile : _midTierProfile;
        }

        private static void ApplyUrpSettings(MobileDeviceProfile profile)
        {
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
                return;

            urp.renderScale = Mathf.Clamp(profile.renderScale, 0.5f, 1f);
            urp.supportsHDR = profile.supportsHdr;
            urp.supportsSoftParticles = profile.supportsSoftParticles;
            urp.supportsCameraOpaqueTexture = profile.supportsOpaqueTexture;
        }

        private static void ApplyPostProcessState(bool enabled)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                UniversalAdditionalCameraData camData = cameras[i].GetUniversalAdditionalCameraData();
                if (camData != null)
                    camData.renderPostProcessing = enabled;
            }
        }

        private void ResolveAdaptiveHook()
        {
            if (_adaptiveQualityHookBehaviour == null)
            {
                _adaptiveHook = GetComponent<IMobileAdaptiveQualityHook>();
                return;
            }

            _adaptiveHook = _adaptiveQualityHookBehaviour as IMobileAdaptiveQualityHook;
            if (_adaptiveHook == null)
                Debug.LogWarning("[MobilePerformanceConfigurator] Adaptive hook does not implement IMobileAdaptiveQualityHook.");
        }

        private static bool IsMobileRuntime()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }
}
