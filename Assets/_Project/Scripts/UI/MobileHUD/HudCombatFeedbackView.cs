using TMPro;
using MuLike.Performance.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    public sealed class HudCombatFeedbackView : MonoBehaviour
    {
        [SerializeField] private Image _damageFlash;
        [SerializeField] private Image _lowHpVignette;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private Color _damageFlashColor = new(0.95f, 0.15f, 0.15f, 0.55f);
        [SerializeField] private Color _lowHpColor = new(0.9f, 0f, 0f, 0.35f);
        [SerializeField] private float _damageFlashDecay = 4f;
        [SerializeField] private float _damageTextLifetime = 0.8f;
        [SerializeField] private float _lowHpPulseSpeed = 3f;
        [SerializeField] private float _maxUiUpdatesPerSecond = 24f;

        private float _damageFlashAlpha;
        private float _damageTextTimer;
        private bool _lowHp;
        private UiUpdateThrottler _uiThrottler;

        private void Awake()
        {
            _uiThrottler = new UiUpdateThrottler(_maxUiUpdatesPerSecond);
        }

        private void Update()
        {
            if (_uiThrottler != null && !_uiThrottler.ShouldRunNow(Time.unscaledTime))
                return;

            if (_damageFlash != null)
            {
                _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, _damageFlashDecay * Time.unscaledDeltaTime);
                Color c = _damageFlashColor;
                c.a = _damageFlashAlpha;
                _damageFlash.color = c;
                _damageFlash.enabled = c.a > 0.001f;
            }

            if (_damageText != null)
            {
                _damageTextTimer -= Time.unscaledDeltaTime;
                if (_damageTextTimer <= 0f)
                    _damageText.text = string.Empty;
            }

            if (_lowHpVignette != null)
            {
                if (!_lowHp)
                {
                    _lowHpVignette.enabled = false;
                    return;
                }

                _lowHpVignette.enabled = true;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _lowHpPulseSpeed);
                Color c = _lowHpColor;
                c.a *= Mathf.Lerp(0.45f, 1f, pulse);
                _lowHpVignette.color = c;
            }
        }

        public void ShowDamageTaken(int amount)
        {
            if (amount <= 0)
                return;

            _damageFlashAlpha = Mathf.Clamp01(_damageFlashAlpha + 0.5f);
            if (_damageText != null)
            {
                _damageText.text = $"-{amount}";
                _damageTextTimer = _damageTextLifetime;
            }
        }

        public void SetLowHpState(bool lowHp)
        {
            _lowHp = lowHp;
        }
    }
}
