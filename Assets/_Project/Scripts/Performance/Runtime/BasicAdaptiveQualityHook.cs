using UnityEngine;

namespace MuLike.Performance.Runtime
{
    /// <summary>
    /// Minimal optional adaptive quality hook for frame-rate fallback.
    /// </summary>
    public sealed class BasicAdaptiveQualityHook : MonoBehaviour, IMobileAdaptiveQualityHook
    {
        [SerializeField, Range(10f, 60f)] private float _lowFpsThreshold = 40f;
        [SerializeField, Range(0.5f, 0.95f)] private float _renderScaleFallback = 0.82f;
        [SerializeField] private bool _disablePostProcessOnLowFps = true;

        private bool _fallbackApplied;

        public void EvaluateAndApply(MobilePerformanceConfigurator configurator)
        {
            if (configurator == null || configurator.ActiveProfile == null)
                return;

            if (configurator.SmoothedFps >= _lowFpsThreshold)
                return;

            if (_fallbackApplied)
                return;

            configurator.FallbackToFrameRate();

            MobileDeviceProfile profile = configurator.ActiveProfile;
            profile.renderScale = Mathf.Min(profile.renderScale, _renderScaleFallback);
            if (_disablePostProcessOnLowFps)
                profile.enablePostProcess = false;

            configurator.ApplyDefaults();
            _fallbackApplied = true;
        }
    }
}
