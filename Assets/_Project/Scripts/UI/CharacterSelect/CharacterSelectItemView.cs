using System;
using MuLike.Data.DTO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Single character row/card view used by CharacterSelectView.
    /// </summary>
    public class CharacterSelectItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _classText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Button _selectButton;
        [SerializeField] private Image _selectionHighlight;

        private int _characterId;

        public event Action<int> Selected;

        private void Awake()
        {
            if (_selectButton != null)
                _selectButton.onClick.AddListener(HandleSelectClicked);
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
                _selectButton.onClick.RemoveListener(HandleSelectClicked);
        }

        public void Bind(CharacterSummaryDto dto, bool selected)
        {
            _characterId = dto != null ? dto.characterId : 0;

            if (_nameText != null) _nameText.text = dto != null ? dto.name : "-";
            if (_classText != null) _classText.text = dto != null ? dto.classId : "-";
            if (_levelText != null) _levelText.text = dto != null ? $"Lv. {dto.level}" : "Lv. -";
            if (_selectionHighlight != null) _selectionHighlight.enabled = selected;
        }

        private void HandleSelectClicked()
        {
            if (_characterId <= 0) return;
            Selected?.Invoke(_characterId);
        }
    }
}
