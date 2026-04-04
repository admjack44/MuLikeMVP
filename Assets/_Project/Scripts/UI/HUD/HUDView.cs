using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.HUD
{
    /// <summary>
    /// Pure HUD view: exposes user intents and receives display state from presenter.
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private TMP_Text _characterNameText;
        [SerializeField] private TMP_Text _levelText;

        [Header("Resources")]
        [SerializeField] private Slider _hpBar;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Slider _manaBar;
        [SerializeField] private TMP_Text _manaText;
        [SerializeField] private Slider _shieldBar;
        [SerializeField] private TMP_Text _shieldText;
        [SerializeField] private Slider _staminaBar;
        [SerializeField] private TMP_Text _staminaText;

        [Header("Experience")]
        [SerializeField] private Slider _experienceBar;
        [SerializeField] private TMP_Text _experienceText;

        [Header("Target")]
        [SerializeField] private TMP_Text _targetText;

        [Header("Menu Buttons")]
        [SerializeField] private Button _chatToggleButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _equipmentButton;
        [SerializeField] private Button _quickConsumeButton;

        [Header("Status")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Debug Console")]
        [SerializeField] private GameObject _debugConsoleRoot;
        [SerializeField] private TMP_Text _debugConsoleText;
        [SerializeField] private int _debugMaxChars = 1200;

        public event Action ChatToggleRequested;
        public event Action InventoryRequested;
        public event Action EquipmentRequested;
        public event Action QuickConsumeRequested;

        private void Awake()
        {
            if (_chatToggleButton != null)
                _chatToggleButton.onClick.AddListener(() => ChatToggleRequested?.Invoke());

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(() => InventoryRequested?.Invoke());

            if (_equipmentButton != null)
                _equipmentButton.onClick.AddListener(() => EquipmentRequested?.Invoke());

            if (_quickConsumeButton != null)
                _quickConsumeButton.onClick.AddListener(() => QuickConsumeRequested?.Invoke());

            SetTargetName("No target");
            SetStatus("HUD ready.");
        }

        private void OnDestroy()
        {
            if (_chatToggleButton != null)
                _chatToggleButton.onClick.RemoveAllListeners();

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveAllListeners();

            if (_equipmentButton != null)
                _equipmentButton.onClick.RemoveAllListeners();

            if (_quickConsumeButton != null)
                _quickConsumeButton.onClick.RemoveAllListeners();
        }

        public void SetCharacterName(string characterName)
        {
            if (_characterNameText != null)
                _characterNameText.text = string.IsNullOrWhiteSpace(characterName) ? "Unknown" : characterName;
        }

        public void SetLevel(int level)
        {
            if (_levelText != null)
                _levelText.text = $"Lv. {Mathf.Max(1, level)}";
        }

        public void SetHp(int current, int max)
        {
            SetResource(_hpBar, _hpText, current, max);
        }

        public void SetMana(int current, int max)
        {
            SetResource(_manaBar, _manaText, current, max);
        }

        public void SetShield(int current, int max)
        {
            SetResource(_shieldBar, _shieldText, current, max);
        }

        public void SetStamina(int current, int max)
        {
            SetResource(_staminaBar, _staminaText, current, max);
        }

        public void SetExperience(long current, long nextLevel)
        {
            float normalized = nextLevel > 0 ? Mathf.Clamp01((float)current / nextLevel) : 0f;

            if (_experienceBar != null)
                _experienceBar.value = normalized;

            if (_experienceText != null)
                _experienceText.text = $"EXP {Math.Max(0, current)}/{Math.Max(0, nextLevel)}";
        }

        public void SetTargetName(string targetName)
        {
            if (_targetText != null)
                _targetText.text = string.IsNullOrWhiteSpace(targetName) ? "No target" : targetName;
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        public void SetDebugConsoleVisible(bool visible)
        {
            if (_debugConsoleRoot != null)
                _debugConsoleRoot.SetActive(visible);
        }

        public void AppendDebugLine(string line)
        {
            if (_debugConsoleText == null || string.IsNullOrWhiteSpace(line))
                return;

            string existing = _debugConsoleText.text;
            string combined = string.IsNullOrEmpty(existing) ? line : $"{existing}\n{line}";

            if (combined.Length > _debugMaxChars)
                combined = combined.Substring(combined.Length - _debugMaxChars);

            _debugConsoleText.text = combined;
        }

        private static void SetResource(Slider bar, TMP_Text text, int current, int max)
        {
            int safeMax = Mathf.Max(0, max);
            int safeCurrent = Mathf.Clamp(current, 0, safeMax > 0 ? safeMax : int.MaxValue);
            float normalized = safeMax > 0 ? Mathf.Clamp01((float)safeCurrent / safeMax) : 0f;

            if (bar != null)
                bar.value = normalized;

            if (text != null)
                text.text = $"{safeCurrent}/{safeMax}";
        }
    }
}
