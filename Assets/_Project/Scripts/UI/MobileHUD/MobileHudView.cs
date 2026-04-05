using System;
using System.Collections.Generic;
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
        [SerializeField] private PotionQuickSlotStripView _potionQuickSlots;

        [Header("Target")]
        [SerializeField] private TargetPortraitView _targetPortrait;

        [Header("Bars")]
        [SerializeField] private HudResourceBarView _hpBar;
        [SerializeField] private HudResourceBarView _mpBar;
        [SerializeField] private HudResourceBarView _sdBar;
        [SerializeField] private HudResourceBarView _staminaBar;

        [Header("Top Right")]
        [SerializeField] private Button _chatButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _minimapButton;
        [SerializeField] private Button _characterButton;
        [SerializeField] private Button _mapButton;
        [SerializeField] private Button _settingsButton;

        [Header("UX Modules")]
        [SerializeField] private QuestTrackerView _questTracker;
        [SerializeField] private MinimapView _minimap;
        [SerializeField] private TargetIndicatorView _targetIndicator;
        [SerializeField] private HudCombatFeedbackView _combatFeedback;
        [SerializeField] private CanvasGroup _nonCriticalCanvasGroup;
        [SerializeField] private GameObject[] _nonCriticalPanels = Array.Empty<GameObject>();

        public event Action<Vector2> MoveInputChanged;
        public event Action<int> SkillPressed;
        public event Action<bool> AutoAttackChanged;
        public event Action<bool> AutoBattleChanged;
        public event Action<int> PotionQuickSlotPressed;
        public event Action ChatPressed;
        public event Action InventoryPressed;
        public event Action CharacterPressed;
        public event Action MinimapPressed;
        public event Action MapPressed;
        public event Action SettingsPressed;
        public event Action<int> QuestTapped;
        public event Action MinimapExpandPressed;
        public event Action AnyHudInput;

        private void Awake()
        {
            if (_leftJoystick != null)
                _leftJoystick.InputChanged += HandleMoveInput;

            if (_skillStrip != null)
                _skillStrip.SkillPressed += HandleSkillPressed;

            if (_autoAttackToggle != null)
                _autoAttackToggle.onValueChanged.AddListener(HandleAutoAttackChanged);

            if (_potionQuickSlots != null)
                _potionQuickSlots.SlotPressed += HandlePotionQuickSlotPressed;

            if (_chatButton != null)
                _chatButton.onClick.AddListener(HandleChatPressedInternal);

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(HandleInventoryPressedInternal);

            if (_minimapButton != null)
                _minimapButton.onClick.AddListener(HandleMinimapPressedInternal);

            if (_characterButton != null)
                _characterButton.onClick.AddListener(HandleCharacterPressedInternal);

            if (_mapButton != null)
                _mapButton.onClick.AddListener(HandleMapPressedInternal);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(HandleSettingsPressedInternal);

            if (_questTracker != null)
                _questTracker.QuestTapped += HandleQuestTapped;

            if (_minimap != null)
                _minimap.ExpandedMapRequested += HandleMinimapExpandedRequested;

            _hpBar?.SetLabel("HP");
            _mpBar?.SetLabel("MP");
            _sdBar?.SetLabel("SD");
            _staminaBar?.SetLabel("STA");
            _targetPortrait?.ClearTarget();
        }

        private void OnDestroy()
        {
            if (_leftJoystick != null)
                _leftJoystick.InputChanged -= HandleMoveInput;

            if (_skillStrip != null)
                _skillStrip.SkillPressed -= HandleSkillPressed;

            if (_autoAttackToggle != null)
                _autoAttackToggle.onValueChanged.RemoveListener(HandleAutoAttackChanged);

            if (_potionQuickSlots != null)
                _potionQuickSlots.SlotPressed -= HandlePotionQuickSlotPressed;

            if (_chatButton != null)
                _chatButton.onClick.RemoveAllListeners();

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveAllListeners();

            if (_minimapButton != null)
                _minimapButton.onClick.RemoveAllListeners();

            if (_characterButton != null)
                _characterButton.onClick.RemoveAllListeners();

            if (_mapButton != null)
                _mapButton.onClick.RemoveAllListeners();

            if (_settingsButton != null)
                _settingsButton.onClick.RemoveAllListeners();

            if (_questTracker != null)
                _questTracker.QuestTapped -= HandleQuestTapped;

            if (_minimap != null)
                _minimap.ExpandedMapRequested -= HandleMinimapExpandedRequested;
        }

        public void SetTarget(string targetName)
        {
            _targetPortrait?.SetTarget(targetName);
        }

        public void SetTargetPortrait(string targetName, int targetLevel, int targetHpCurrent, int targetHpMax, bool hasTarget)
        {
            if (_targetPortrait == null)
                return;

            if (!hasTarget)
            {
                _targetPortrait.ClearTarget();
                return;
            }

            _targetPortrait.SetTargetInfo(targetName, targetLevel, targetHpCurrent, targetHpMax, true);
        }

        public void SetHp(int current, int max) => _hpBar?.SetValue(current, max);
        public void SetMp(int current, int max) => _mpBar?.SetValue(current, max);
        public void SetSd(int current, int max) => _sdBar?.SetValue(current, max);
        public void SetStamina(int current, int max) => _staminaBar?.SetValue(current, max);
        public void SetCombo(int current, int max) => SetStamina(current, max);

        public void SetPotionQuickSlot(
            int slotId,
            string label,
            int count,
            float cooldownRemaining,
            float cooldownTotal,
            bool interactable)
        {
            _potionQuickSlots?.SetSlot(slotId, label, count, cooldownRemaining, cooldownTotal, interactable);
        }

        public void SetAutoBattleState(bool enabled)
        {
            if (_autoAttackToggle == null)
                return;

            if (_autoAttackToggle.isOn == enabled)
                return;

            _autoAttackToggle.SetIsOnWithoutNotify(enabled);
        }

        public void SetSkillName(int skillId, string name)
        {
            _skillStrip?.SetSkillName(skillId, name);
        }

        public void SetSkillCooldown(int skillId, float remaining, float total)
        {
            _skillStrip?.SetCooldown(skillId, remaining, total);
        }

        public void SetSkillInteractable(int skillId, bool interactable)
        {
            _skillStrip?.SetInteractable(skillId, interactable);
        }

        public void SetQuestEntries(IReadOnlyList<QuestTrackerEntry> entries)
        {
            _questTracker?.SetEntries(entries);
        }

        public void SetTargetIndicatorTarget(MuLike.Gameplay.Entities.EntityView target)
        {
            _targetIndicator?.SetTarget(target);
        }

        public void SetMinimapMapName(string mapName)
        {
            _minimap?.SetMapName(mapName);
        }

        public void SetMinimapMarker(string markerId, Vector3 worldOffsetFromPlayer, bool visible, string label)
        {
            _minimap?.SetMarker(markerId, worldOffsetFromPlayer, visible, label);
        }

        public void ShowDamageTaken(int amount)
        {
            _combatFeedback?.ShowDamageTaken(amount);
        }

        public void SetLowHpState(bool lowHp)
        {
            _combatFeedback?.SetLowHpState(lowHp);
        }

        public void SetNonCriticalVisible(bool visible)
        {
            for (int i = 0; i < _nonCriticalPanels.Length; i++)
            {
                if (_nonCriticalPanels[i] != null)
                    _nonCriticalPanels[i].SetActive(visible);
            }

            if (_nonCriticalCanvasGroup != null)
            {
                _nonCriticalCanvasGroup.alpha = visible ? 1f : 0f;
                _nonCriticalCanvasGroup.blocksRaycasts = visible;
                _nonCriticalCanvasGroup.interactable = visible;
            }
        }

        public void SetStatusToast(string message)
        {
            Debug.Log($"[MobileHUD] {message}");
        }

        private void HandleMoveInput(Vector2 input)
        {
            AnyHudInput?.Invoke();
            MoveInputChanged?.Invoke(input);
        }

        private void HandleSkillPressed(int skillId)
        {
            AnyHudInput?.Invoke();
            SkillPressed?.Invoke(skillId);
        }

        private void HandleAutoAttackChanged(bool enabled)
        {
            AnyHudInput?.Invoke();
            AutoAttackChanged?.Invoke(enabled);
            AutoBattleChanged?.Invoke(enabled);
        }

        private void HandlePotionQuickSlotPressed(int slotId)
        {
            AnyHudInput?.Invoke();
            PotionQuickSlotPressed?.Invoke(slotId);
        }

        private void HandleChatPressedInternal()
        {
            AnyHudInput?.Invoke();
            ChatPressed?.Invoke();
        }

        private void HandleInventoryPressedInternal()
        {
            AnyHudInput?.Invoke();
            InventoryPressed?.Invoke();
        }

        private void HandleCharacterPressedInternal()
        {
            AnyHudInput?.Invoke();
            CharacterPressed?.Invoke();
        }

        private void HandleMinimapPressedInternal()
        {
            AnyHudInput?.Invoke();
            MinimapPressed?.Invoke();
            MapPressed?.Invoke();
        }

        private void HandleMapPressedInternal()
        {
            AnyHudInput?.Invoke();
            MapPressed?.Invoke();
        }

        private void HandleSettingsPressedInternal()
        {
            AnyHudInput?.Invoke();
            SettingsPressed?.Invoke();
        }

        private void HandleQuestTapped(int questId)
        {
            AnyHudInput?.Invoke();
            QuestTapped?.Invoke(questId);
        }

        private void HandleMinimapExpandedRequested()
        {
            AnyHudInput?.Invoke();
            MinimapExpandPressed?.Invoke();
            MapPressed?.Invoke();
        }
    }
}
