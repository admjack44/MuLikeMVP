using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Character card/row used in list mode (mobile portrait or landscape).
    /// </summary>
    public sealed class CharacterSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _classText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _powerScoreText;
        [SerializeField] private TMP_Text _mapText;
        [SerializeField] private Image _selectionHighlight;
        [SerializeField] private Button _button;

        private int _characterId;

        public event Action<int> Selected;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(CharacterSummaryViewData dto, bool selected)
        {
            _characterId = dto.CharacterId;

            if (_nameText != null) _nameText.text = string.IsNullOrWhiteSpace(dto.Name) ? "-" : dto.Name;
            if (_classText != null) _classText.text = string.IsNullOrWhiteSpace(dto.ClassId) ? "-" : dto.ClassId;
            if (_levelText != null) _levelText.text = $"Lv. {Mathf.Max(1, dto.Level)}";
            if (_powerScoreText != null) _powerScoreText.text = $"PS {Mathf.Max(0, dto.PowerScore)}";
            if (_mapText != null) _mapText.text = string.IsNullOrWhiteSpace(dto.MapName) ? "-" : dto.MapName;
            if (_selectionHighlight != null) _selectionHighlight.enabled = selected;
        }

        private void HandleClicked()
        {
            if (_characterId <= 0)
                return;

            Selected?.Invoke(_characterId);
        }
    }
}
