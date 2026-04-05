using System;
using System.Collections.Generic;
using UnityEngine;

namespace MuLike.Optimization
{
    /// <summary>
    /// Battery and thermal safeguards for long mobile sessions.
    /// </summary>
    public sealed class BatterySaver : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private QualityManager _qualityManager;

        [Header("Battery saver")]
        [SerializeField] private bool _enableBatterySaver = true;
        [SerializeField, Range(0.05f, 0.50f)] private float _batteryThreshold = 0.20f;
        [SerializeField] private bool _force30FpsInBatterySaver = true;
        [SerializeField] private bool _lowerQualityInBatterySaver = true;

        [Header("Thermal")]
        [SerializeField] private bool _enableThermalProtection = true;
        [SerializeField, Range(35f, 50f)] private float _highTempCelsius = 42f;
        [SerializeField, Range(30f, 45f)] private float _recoverTempCelsius = 39f;
        [SerializeField, Min(2f)] private float _temperaturePollInterval = 6f;

        [Header("Background")]
        [SerializeField] private bool _pauseSimulationInBackground = true;
        [SerializeField] private MonoBehaviour[] _systemsToPause = Array.Empty<MonoBehaviour>();
        [SerializeField] private MonoBehaviour[] _notificationSystems = Array.Empty<MonoBehaviour>();

        private float _cachedTargetFps = 60f;
        private bool _batterySaverApplied;
        private bool _thermalThrottled;
        private bool _pausedByBackground;
        private float _nextTempPollAt;

        private readonly List<MonoBehaviour> _pausedSystems = new List<MonoBehaviour>(16);

        private void Awake()
        {
            if (_qualityManager == null)
                _qualityManager = FindAnyObjectByType<QualityManager>();

            _cachedTargetFps = Mathf.Max(30, Application.targetFrameRate > 0 ? Application.targetFrameRate : 60);
        }

        private void Update()
        {
            if (_enableBatterySaver)
                TickBatterySaver();

            if (_enableThermalProtection && Time.unscaledTime >= _nextTempPollAt)
            {
                _nextTempPollAt = Time.unscaledTime + Mathf.Max(1f, _temperaturePollInterval);
                TickThermalProtection();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_pauseSimulationInBackground)
                return;

            if (paused)
                PauseBackgroundSystems();
            else
                ResumeBackgroundSystems();
        }

        private void TickBatterySaver()
        {
            float battery = SystemInfo.batteryLevel;
            if (battery < 0f)
                return;

            bool shouldSave = battery <= _batteryThreshold;
            if (shouldSave == _batterySaverApplied)
                return;

            _batterySaverApplied = shouldSave;
            if (shouldSave)
            {
                if (_force30FpsInBatterySaver)
                    Application.targetFrameRate = 30;

                if (_lowerQualityInBatterySaver && _qualityManager != null)
                {
                    _qualityManager.SetBatterySafeMode(true);
                    _qualityManager.ApplyQuality(MobileQualityLevel.Low);
                }

                Debug.Log("[BatterySaver] Battery saver mode enabled.");
            }
            else
            {
                Application.targetFrameRate = Mathf.RoundToInt(_cachedTargetFps);

                if (_qualityManager != null)
                    _qualityManager.SetBatterySafeMode(false);

                Debug.Log("[BatterySaver] Battery saver mode disabled.");
            }
        }

        private void TickThermalProtection()
        {
            float temp = ReadBatteryTemperatureCelsius();
            if (temp < 0f)
                return;

            if (!_thermalThrottled && temp >= _highTempCelsius)
            {
                _thermalThrottled = true;
                if (_qualityManager != null)
                    _qualityManager.SetThermalThrottle(true);

                Application.targetFrameRate = Mathf.Min(Application.targetFrameRate > 0 ? Application.targetFrameRate : 60, 30);
                Debug.LogWarning($"[BatterySaver] Thermal throttle enabled at {temp:F1}C.");
                return;
            }

            if (_thermalThrottled && temp <= _recoverTempCelsius)
            {
                _thermalThrottled = false;
                if (_qualityManager != null)
                    _qualityManager.SetThermalThrottle(false);

                Debug.Log($"[BatterySaver] Thermal throttle disabled at {temp:F1}C.");
            }
        }

        private void PauseBackgroundSystems()
        {
            if (_pausedByBackground)
                return;

            _pausedByBackground = true;
            _pausedSystems.Clear();

            for (int i = 0; i < _systemsToPause.Length; i++)
            {
                MonoBehaviour behaviour = _systemsToPause[i];
                if (behaviour == null || !behaviour.enabled)
                    continue;

                if (IsNotificationSystem(behaviour))
                    continue;

                behaviour.enabled = false;
                _pausedSystems.Add(behaviour);
            }

            Time.timeScale = 0f;
            Debug.Log("[BatterySaver] Background pause applied.");
        }

        private void ResumeBackgroundSystems()
        {
            if (!_pausedByBackground)
                return;

            _pausedByBackground = false;
            Time.timeScale = 1f;

            for (int i = 0; i < _pausedSystems.Count; i++)
            {
                MonoBehaviour behaviour = _pausedSystems[i];
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            _pausedSystems.Clear();
            Debug.Log("[BatterySaver] Background pause released.");
        }

        private bool IsNotificationSystem(MonoBehaviour behaviour)
        {
            for (int i = 0; i < _notificationSystems.Length; i++)
            {
                if (_notificationSystems[i] == behaviour)
                    return true;
            }

            return false;
        }

        private static float ReadBatteryTemperatureCelsius()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                AndroidJavaObject batteryStatus = activity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter);
                if (batteryStatus == null)
                    return -1f;

                int tempTenths = batteryStatus.Call<int>("getIntExtra", "temperature", -1);
                if (tempTenths <= 0)
                    return -1f;

                return tempTenths / 10f;
            }
            catch
            {
                return -1f;
            }
#else
            return -1f;
#endif
        }

        public void ApplyQaProfile(MobileQaDeviceTier tier, bool ios)
        {
            switch (tier)
            {
                case MobileQaDeviceTier.Low:
                    _batteryThreshold = 0.30f;
                    _highTempCelsius = ios ? 41f : 40f;
                    _recoverTempCelsius = ios ? 38f : 37f;
                    _temperaturePollInterval = 4f;
                    break;
                case MobileQaDeviceTier.High:
                    _batteryThreshold = 0.18f;
                    _highTempCelsius = ios ? 44f : 43f;
                    _recoverTempCelsius = ios ? 40f : 39f;
                    _temperaturePollInterval = 8f;
                    break;
                default:
                    _batteryThreshold = 0.22f;
                    _highTempCelsius = ios ? 43f : 42f;
                    _recoverTempCelsius = ios ? 39f : 38f;
                    _temperaturePollInterval = 6f;
                    break;
            }
        }
    }
}
