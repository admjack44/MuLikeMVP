using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI
{
    /// <summary>
    /// Rotates loading tips text in Loading scene.
    /// Supports both TextMeshProUGUI and legacy UI Text.
    /// </summary>
    public sealed class LoadingTipsRotator : MonoBehaviour
    {
        [TextArea]
        [SerializeField] private string[] _tips =
        {
            "Tip: Mejora equipo antes de cambiar de mapa para evitar picos de dificultad.",
            "Tip: Activa Auto para farmeo y manual para bosses.",
            "Tip: El party balanceado gana bonus extra de experiencia.",
            "Tip: Usa el minimapa para fast travel y eventos activos.",
            "Tip: Revisa subasta para vender drops de alto valor."
        };

        [SerializeField, Min(1f)] private float _rotationIntervalSeconds = 4f;

        private TMP_Text _tmpText;
        private Text _legacyText;
        private int _index;
        private float _nextAt;

        private void Awake()
        {
            _tmpText = GetComponent<TMP_Text>();
            _legacyText = _tmpText == null ? GetComponent<Text>() : null;

            if (_tmpText == null && _legacyText == null)
            {
                _tmpText = gameObject.AddComponent<TextMeshProUGUI>();
                _tmpText.alignment = TextAlignmentOptions.Center;
                _tmpText.fontSize = 34f;
                _tmpText.color = Color.white;
            }

            RenderCurrent();
            _nextAt = Time.unscaledTime + _rotationIntervalSeconds;
        }

        private void Update()
        {
            if (_tips == null || _tips.Length == 0)
                return;

            if (Time.unscaledTime < _nextAt)
                return;

            _nextAt = Time.unscaledTime + Mathf.Max(1f, _rotationIntervalSeconds);
            _index = (_index + 1) % _tips.Length;
            RenderCurrent();
        }

        private void RenderCurrent()
        {
            string text = (_tips != null && _tips.Length > 0) ? _tips[Mathf.Clamp(_index, 0, _tips.Length - 1)] : string.Empty;
            if (_tmpText != null)
                _tmpText.text = text;
            if (_legacyText != null)
                _legacyText.text = text;
        }
    }
}
