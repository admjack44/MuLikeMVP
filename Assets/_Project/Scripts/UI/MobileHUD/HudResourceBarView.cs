using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Reusable bar widget for HUD resources (HP/MP/SD/COMBO).
    /// </summary>
    public sealed class HudResourceBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Slider _fillBar;
        [SerializeField] private TMP_Text _valueText;
        [SerializeField, Range(5f, 60f)] private float _maxUpdatesPerSecond = 20f;

        private float _nextAllowedUpdateAt;
        private int _lastCurrent = int.MinValue;
        private int _lastMax = int.MinValue;

        public void SetLabel(string label)
        {
            if (_labelText != null)
                _labelText.text = label ?? string.Empty;
        }

        public void SetValue(int current, int max)
        {
            int safeMax = Mathf.Max(0, max);
            int safeCurrent = Mathf.Clamp(current, 0, safeMax > 0 ? safeMax : int.MaxValue);

            if (safeCurrent == _lastCurrent && safeMax == _lastMax)
                return;

            float now = Time.unscaledTime;
            float minInterval = 1f / Mathf.Clamp(_maxUpdatesPerSecond, 5f, 60f);
            if (now < _nextAllowedUpdateAt)
                return;

            _nextAllowedUpdateAt = now + minInterval;
            _lastCurrent = safeCurrent;
            _lastMax = safeMax;

            float normalized = safeMax > 0 ? Mathf.Clamp01((float)safeCurrent / safeMax) : 0f;

            if (_fillBar != null)
                _fillBar.value = normalized;

            if (_valueText != null)
                _valueText.text = $"{safeCurrent}/{safeMax}";
        }
    }
}
