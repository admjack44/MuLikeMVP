using MuLike.Networking;
using UnityEngine;

namespace MuLike.Optimization
{
    /// <summary>
    /// Applies QA tuning presets across quality, memory, battery/thermal and networking systems.
    /// Supports auto-detection by RAM/platform and manual override for test passes.
    /// </summary>
    public sealed class MobileQaProfileApplier : MonoBehaviour
    {
        public enum SelectionMode
        {
            AutoByDevice,
            ForceLow,
            ForceMedium,
            ForceHigh
        }

        [Header("Mode")]
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private SelectionMode _selectionMode = SelectionMode.AutoByDevice;

        [Header("Dependencies")]
        [SerializeField] private QualityManager _qualityManager;
        [SerializeField] private MemoryManager _memoryManager;
        [SerializeField] private BatterySaver _batterySaver;
        [SerializeField] private MobileNetworkOptimizer _networkOptimizer;
        [SerializeField] private MuNetworkManager _networkManager;

        [Header("Detection")]
        [SerializeField, Min(1024)] private int _lowTierMaxRamMb = 3072;
        [SerializeField, Min(2048)] private int _mediumTierMaxRamMb = 6144;

        public MobileQaDeviceTier ActiveTier { get; private set; } = MobileQaDeviceTier.Medium;

        private void Awake()
        {
            AutoResolveDependencies();

            if (_applyOnAwake)
                ApplySelectedProfile();
        }

        [ContextMenu("Apply Selected QA Profile")]
        public void ApplySelectedProfile()
        {
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            ActiveTier = ResolveTier();
            ApplyProfile(ActiveTier, ios);

            Debug.Log($"[MobileQaProfileApplier] Applied QA profile {ActiveTier} on {(ios ? "iOS" : "Android/Other")} (RAM={SystemInfo.systemMemorySize}MB).");
        }

        public void ApplyAutoProfile()
        {
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            ActiveTier = ResolveByRam(SystemInfo.systemMemorySize);
            ApplyProfile(ActiveTier, ios);
        }

        public void ApplyForcedProfile(MobileQaDeviceTier tier)
        {
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            ActiveTier = tier;
            ApplyProfile(ActiveTier, ios);
        }

        private void ApplyProfile(MobileQaDeviceTier tier, bool ios)
        {
            ActiveTier = tier;

            _qualityManager?.ApplyQaProfile(ActiveTier, ios);
            _memoryManager?.ApplyQaProfile(ActiveTier);
            _batterySaver?.ApplyQaProfile(ActiveTier, ios);
            _networkOptimizer?.ApplyQaProfile(ActiveTier);
        }

        private void AutoResolveDependencies()
        {
            if (_qualityManager == null)
                _qualityManager = FindAnyObjectByType<QualityManager>();
            if (_memoryManager == null)
                _memoryManager = FindAnyObjectByType<MemoryManager>();
            if (_batterySaver == null)
                _batterySaver = FindAnyObjectByType<BatterySaver>();
            if (_networkOptimizer == null)
                _networkOptimizer = FindAnyObjectByType<MobileNetworkOptimizer>();
            if (_networkManager == null)
                _networkManager = FindAnyObjectByType<MuNetworkManager>();
        }

        private MobileQaDeviceTier ResolveTier()
        {
            return _selectionMode switch
            {
                SelectionMode.ForceLow => MobileQaDeviceTier.Low,
                SelectionMode.ForceHigh => MobileQaDeviceTier.High,
                SelectionMode.ForceMedium => MobileQaDeviceTier.Medium,
                _ => ResolveByRam(SystemInfo.systemMemorySize)
            };
        }

        private MobileQaDeviceTier ResolveByRam(int ramMb)
        {
            int ram = Mathf.Max(1024, ramMb);
            if (ram <= Mathf.Max(1024, _lowTierMaxRamMb))
                return MobileQaDeviceTier.Low;
            if (ram <= Mathf.Max(_lowTierMaxRamMb + 1, _mediumTierMaxRamMb))
                return MobileQaDeviceTier.Medium;
            return MobileQaDeviceTier.High;
        }

    }
}
