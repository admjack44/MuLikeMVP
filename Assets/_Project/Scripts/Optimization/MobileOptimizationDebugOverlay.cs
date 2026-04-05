using UnityEngine;
using UnityEngine.Profiling;

namespace MuLike.Optimization
{
    /// <summary>
    /// Runtime QA overlay for mobile optimization verification.
    /// Shows live metrics and allows switching device profiles on the fly.
    /// </summary>
    public sealed class MobileOptimizationDebugOverlay : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MobileQaProfileApplier _qaProfileApplier;
        [SerializeField] private QualityManager _qualityManager;
        [SerializeField] private MemoryManager _memoryManager;

        [Header("UI")]
        [SerializeField] private bool _startVisible;
        [SerializeField, Min(220f)] private float _panelWidth = 460f;

        private bool _visible;
        private float _fpsEma = 60f;

        private void Awake()
        {
            if (_qaProfileApplier == null)
                _qaProfileApplier = FindAnyObjectByType<MobileQaProfileApplier>();
            if (_qualityManager == null)
                _qualityManager = FindAnyObjectByType<QualityManager>();
            if (_memoryManager == null)
                _memoryManager = FindAnyObjectByType<MemoryManager>();

            _visible = _startVisible;
        }

        private void Update()
        {
            float frameFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsEma = Mathf.Lerp(_fpsEma, frameFps, 0.08f);
        }

        private void OnGUI()
        {
            const int margin = 12;
            const int buttonW = 68;
            const int rowH = 28;

            GUI.depth = -1000;

            if (!_visible)
            {
                if (GUI.Button(new Rect(margin, margin, 64f, 36f), "OPT"))
                    _visible = true;
                return;
            }

            float h = 420f;
            Rect panel = new Rect(margin, margin, _panelWidth, h);
            GUI.Box(panel, "Mobile Optimization QA");

            float x = panel.x + 12f;
            float y = panel.y + 30f;
            float w = panel.width - 24f;

            GUI.Label(new Rect(x, y, w, rowH), $"FPS (EMA): {_fpsEma:F1} | Smoothed: {(_qualityManager != null ? _qualityManager.SmoothedFps.ToString("F1") : "n/a")}");
            y += rowH;

            string quality = _qualityManager != null ? _qualityManager.CurrentLevel.ToString() : "n/a";
            string tier = _qaProfileApplier != null ? _qaProfileApplier.ActiveTier.ToString() : "n/a";
            GUI.Label(new Rect(x, y, w, rowH), $"Quality: {quality} | QA Tier: {tier}");
            y += rowH;

            int textureBudget = _memoryManager != null ? _memoryManager.TextureBudgetMb : 0;
            float textureUsage = EstimateTextureMemoryMb();
            GUI.Label(new Rect(x, y, w, rowH), $"Texture Memory: {textureUsage:F1}MB / Budget {textureBudget}MB");
            y += rowH;

            float battery = SystemInfo.batteryLevel;
            string batteryText = battery < 0f ? "unknown" : $"{battery * 100f:F0}%";
            GUI.Label(new Rect(x, y, w, rowH), $"Battery: {batteryText} | Temp: {ReadBatteryTemperatureText()}");
            y += rowH;

            GUI.Label(new Rect(x, y, w, rowH), $"Target FPS: {Application.targetFrameRate} | RAM: {SystemInfo.systemMemorySize}MB");
            y += rowH + 6f;

            GUI.Label(new Rect(x, y, w, rowH), "QA Profiles:");
            y += rowH;

            if (GUI.Button(new Rect(x, y, buttonW, rowH), "Auto"))
                _qaProfileApplier?.ApplyAutoProfile();
            if (GUI.Button(new Rect(x + 74f, y, buttonW, rowH), "Low"))
                _qaProfileApplier?.ApplyForcedProfile(MobileQaDeviceTier.Low);
            if (GUI.Button(new Rect(x + 148f, y, buttonW, rowH), "Medium"))
                _qaProfileApplier?.ApplyForcedProfile(MobileQaDeviceTier.Medium);
            if (GUI.Button(new Rect(x + 222f, y, buttonW, rowH), "High"))
                _qaProfileApplier?.ApplyForcedProfile(MobileQaDeviceTier.High);
            y += rowH + 8f;

            if (GUI.Button(new Rect(x, y, 140f, rowH), "Force GC Now"))
                _memoryManager?.ForceCollectNow();
            if (GUI.Button(new Rect(x + 148f, y, 140f, rowH), "Hide Panel"))
                _visible = false;
            y += rowH + 8f;

            GUI.Label(new Rect(x, y, w, rowH * 3f), "Tip: usa Auto en dispositivos reales y Force Low/Medium/High para pruebas A/B rápidas.");
        }

        private static float EstimateTextureMemoryMb()
        {
            Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();
            long bytes = 0;
            for (int i = 0; i < textures.Length; i++)
            {
                Texture tex = textures[i];
                if (tex == null)
                    continue;

                bytes += Profiler.GetRuntimeMemorySizeLong(tex);
            }

            return bytes / (1024f * 1024f);
        }

        private static string ReadBatteryTemperatureText()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                AndroidJavaObject batteryStatus = activity.Call<AndroidJavaObject>("registerReceiver", null, intentFilter);
                if (batteryStatus == null)
                    return "n/a";

                int tempTenths = batteryStatus.Call<int>("getIntExtra", "temperature", -1);
                return tempTenths > 0 ? $"{(tempTenths / 10f):F1}C" : "n/a";
            }
            catch
            {
                return "n/a";
            }
#else
            return "n/a";
#endif
        }
    }
}
