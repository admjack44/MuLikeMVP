using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Reusable target portrait block (placeholder icon + target name).
    /// </summary>
    public sealed class TargetPortraitView : MonoBehaviour
    {
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TMP_Text _targetNameText;
        [SerializeField] private Sprite _defaultPortrait;

        public void SetTarget(string targetName, Sprite portrait = null)
        {
            if (_targetNameText != null)
                _targetNameText.text = string.IsNullOrWhiteSpace(targetName) ? "No target" : targetName;

            if (_portraitImage != null)
                _portraitImage.sprite = portrait != null ? portrait : _defaultPortrait;
        }
    }
}
