using System;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Mobile HUD view with reusable subcomponents and intent-only events.
    /// </summary>
    public sealed class MobileHudView : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private VirtualJoystickView _leftJoystick;
        [SerializeField] private SkillButtonStripView _skillStrip;
        [SerializeField] private Toggle _autoAttackToggle;

        [Header("Target")]
        [SerializeField] private TargetPortraitView _targetPortrait;

        [Header("Bars")]
        [SerializeField] private HudResourceBarView _hpBar;
        [SerializeField] private HudResourceBarView _mpBar;
        [SerializeField] private HudResourceBarView _sdBar;
        [SerializeField] private HudResourceBarView _comboBar;

        [Header("Top Right")]
        [SerializeField] private Button _chatButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _minimapButton;

        public event Action<Vector2> MoveInputChanged;
        public event Action<int> SkillPressed;
        public event Action<bool> AutoAttackChanged;
        public event Action ChatPressed;
        public event Action InventoryPressed;
        public event Action MinimapPressed;

        private void Awake()
        {
            if (_leftJoystick != null)
                _leftJoystick.InputChanged += HandleMoveInput;

            if (_skillStrip != null)
                _skillStrip.SkillPressed += HandleSkillPressed;

            if (_autoAttackToggle != null)
                _autoAttackToggle.onValueChanged.AddListener(HandleAutoAttackChanged);

            if (_chatButton != null)
                _chatButton.onClick.AddListener(() => ChatPressed?.Invoke());

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(() => InventoryPressed?.Invoke());

            if (_minimapButton != null)
                _minimapButton.onClick.AddListener(() => MinimapPressed?.Invoke());

            _hpBar?.SetLabel("HP");
            _mpBar?.SetLabel("MP");
            _sdBar?.SetLabel("SD");
            _comboBar?.SetLabel("COMBO");
            _targetPortrait?.SetTarget("No target");
        }

        private void OnDestroy()
        {
            if (_leftJoystick != null)
                _leftJoystick.InputChanged -= HandleMoveInput;

            if (_skillStrip != null)
                _skillStrip.SkillPressed -= HandleSkillPressed;

            if (_autoAttackToggle != null)
                _autoAttackToggle.onValueChanged.RemoveListener(HandleAutoAttackChanged);

            if (_chatButton != null)
                _chatButton.onClick.RemoveAllListeners();

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveAllListeners();

            if (_minimapButton != null)
                _minimapButton.onClick.RemoveAllListeners();
        }

        public void SetTarget(string targetName)
        {
            _targetPortrait?.SetTarget(targetName);
        }

        public void SetHp(int current, int max) => _hpBar?.SetValue(current, max);
        public void SetMp(int current, int max) => _mpBar?.SetValue(current, max);
        public void SetSd(int current, int max) => _sdBar?.SetValue(current, max);
        public void SetCombo(int current, int max) => _comboBar?.SetValue(current, max);

        public void SetSkillName(int skillId, string name)
        {
            _skillStrip?.SetSkillName(skillId, name);
        }

        public void SetSkillCooldown(int skillId, float remaining, float total)
        {
            _skillStrip?.SetCooldown(skillId, remaining, total);
        }

        private void HandleMoveInput(Vector2 input)
        {
            MoveInputChanged?.Invoke(input);
        }

        private void HandleSkillPressed(int skillId)
        {
            SkillPressed?.Invoke(skillId);
        }

        private void HandleAutoAttackChanged(bool enabled)
        {
            AutoAttackChanged?.Invoke(enabled);
        }
    }
}
