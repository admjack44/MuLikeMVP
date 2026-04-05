using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Reusable target portrait block (portrait + name + level + hp%).
    /// </summary>
    public sealed class TargetPortraitView : MonoBehaviour
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TMP_Text _targetNameText;
        [SerializeField] private TMP_Text _targetLevelText;
        [SerializeField] private Slider _targetHpBar;
        [SerializeField] private TMP_Text _targetHpPercentText;
        [SerializeField] private Sprite _defaultPortrait;

        public void SetTarget(string targetName, Sprite portrait = null)
        {
            bool hasTarget = !string.IsNullOrWhiteSpace(targetName);
            SetTargetInfo(
                hasTarget ? targetName : "No target",
                level: hasTarget ? 1 : 0,
                hpCurrent: hasTarget ? 100 : 0,
                hpMax: hasTarget ? 100 : 0,
                hasTarget: hasTarget,
                portrait: portrait);
        }

        public void SetTargetInfo(string targetName, int level, int hpCurrent, int hpMax, bool hasTarget, Sprite portrait = null)
        {
            string safeName = hasTarget && !string.IsNullOrWhiteSpace(targetName)
                ? targetName
                : "No target";

            int safeLevel = hasTarget ? Mathf.Max(1, level) : 0;
            int safeHpMax = hasTarget ? Mathf.Max(0, hpMax) : 0;
            int safeHpCurrent = hasTarget
                ? Mathf.Clamp(hpCurrent, 0, safeHpMax > 0 ? safeHpMax : int.MaxValue)
                : 0;

            if (_targetNameText != null)
                _targetNameText.text = safeName;

            if (_targetLevelText != null)
                _targetLevelText.text = hasTarget ? $"Lv. {safeLevel}" : "Lv. -";

            float normalized = safeHpMax > 0
                ? Mathf.Clamp01((float)safeHpCurrent / safeHpMax)
                : 0f;

            if (_targetHpBar != null)
                _targetHpBar.value = normalized;

            if (_targetHpPercentText != null)
            {
                int percent = Mathf.RoundToInt(normalized * 100f);
                _targetHpPercentText.text = hasTarget ? $"{percent}%" : "-";
            }

            if (_portraitImage != null)
                _portraitImage.sprite = portrait != null ? portrait : _defaultPortrait;
        }

        public void ClearTarget()
        {
            SetTargetInfo("No target", 0, 0, 0, false, null);
        }
    }
}
