using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace MuLike.Performance.Profiling
{
    /// <summary>
    /// Runtime budget monitor with optional on-screen diagnostics.
    /// </summary>
    public sealed class MobileRenderBudgetMonitor : MonoBehaviour
    {
        [SerializeField] private MobileRenderBudgetProfile _budget;
        [SerializeField] private bool _showOverlay = true;

        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _setPassRecorder;
        private readonly Queue<float> _fpsWindow = new();
        private float _fpsAccumulator;

        private void OnEnable()
        {
            _batchesRecorder = CreateRecorder(ProfilerCategory.Render, "Batches Count");
            _setPassRecorder = CreateRecorder(ProfilerCategory.Render, "SetPass Calls Count");
        }

        private void OnDisable()
        {
            _batchesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _fpsWindow.Clear();
            _fpsAccumulator = 0f;
        }

        private void Update()
        {
            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _fpsWindow.Enqueue(fps);
            _fpsAccumulator += fps;
            if (_fpsWindow.Count > 30)
                _fpsAccumulator -= _fpsWindow.Dequeue();
        }

        private void OnGUI()
        {
            if (!_showOverlay || _budget == null)
                return;

            float avgFps = _fpsWindow.Count > 0 ? _fpsAccumulator / _fpsWindow.Count : 0f;
            long batches = _batchesRecorder.Valid ? _batchesRecorder.LastValue : -1;
            long setPass = _setPassRecorder.Valid ? _setPassRecorder.LastValue : -1;

            string fpsState = avgFps >= _budget.targetFps ? "OK" : "WARN";
            string batchState = batches >= 0 && batches <= _budget.maxMainCameraBatches ? "OK" : "WARN";
            string setPassState = setPass >= 0 && setPass <= _budget.maxMainCameraSetPassCalls ? "OK" : "WARN";

            GUILayout.BeginArea(new Rect(12f, 260f, 430f, 130f), GUI.skin.box);
            GUILayout.Label("Mobile Render Budget Monitor");
            GUILayout.Label($"FPS avg(30): {avgFps:F1} [{fpsState}] Target {_budget.targetFps}");
            GUILayout.Label($"Batches: {batches} [{batchState}] Budget <= {_budget.maxMainCameraBatches}");
            GUILayout.Label($"SetPass: {setPass} [{setPassState}] Budget <= {_budget.maxMainCameraSetPassCalls}");
            GUILayout.EndArea();
        }

        private static ProfilerRecorder CreateRecorder(ProfilerCategory category, string statName)
        {
            if (!ProfilerRecorderHandle.TryGet(category, statName, out ProfilerRecorderHandle handle))
                return default;

            return ProfilerRecorder.StartNew(handle);
        }
    }
}
