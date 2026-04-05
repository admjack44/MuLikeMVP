using UnityEngine;

namespace MuLike.Performance.Profiling
{
    [CreateAssetMenu(fileName = "MobileRenderBudgetProfile", menuName = "MuLike/Performance/Mobile Render Budget Profile")]
    public sealed class MobileRenderBudgetProfile : ScriptableObject
    {
        [Header("Android Mid-Tier Budget")]
        [Min(30)] public int targetFps = 60;
        [Min(1)] public int maxMainCameraBatches = 220;
        [Min(1)] public int maxMainCameraSetPassCalls = 120;
        [Range(0.2f, 1f)] public float maxGpuFrameMsAt60Fps = 16.6f;
        [Range(0.2f, 2f)] public float maxCpuFrameMsAt60Fps = 16.6f;

        [Header("HUD Budget")]
        [Min(1)] public int maxHudBatches = 45;
        [Min(1)] public int maxHudCanvasRebuildsPerSecond = 10;
    }
}
