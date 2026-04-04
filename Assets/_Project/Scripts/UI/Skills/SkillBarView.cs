using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Skills
{
    /// <summary>
    /// Decoupled skill bar view with cooldown overlays.
    /// </summary>
    public class SkillBarView : MonoBehaviour
    {
        [Serializable]
        public struct SkillSlotWidget
        {
            public int SkillId;
            public Button TriggerButton;
            public Image CooldownFill;
            public TMP_Text CooldownText;
            public TMP_Text NameText;
        }

        [SerializeField] private SkillSlotWidget[] _slots = Array.Empty<SkillSlotWidget>();
        [SerializeField] private TMP_Text _statusText;

        public event Action<int> SkillTriggered;

        private readonly Dictionary<int, int> _slotIndexBySkillId = new();

        private void Awake()
        {
            _slotIndexBySkillId.Clear();

            for (int i = 0; i < _slots.Length; i++)
            {
                int skillId = _slots[i].SkillId;
                _slotIndexBySkillId[skillId] = i;

                int capturedSkillId = skillId;
                if (_slots[i].TriggerButton != null)
                    _slots[i].TriggerButton.onClick.AddListener(() => SkillTriggered?.Invoke(capturedSkillId));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].TriggerButton != null)
                    _slots[i].TriggerButton.onClick.RemoveAllListeners();
            }
        }

        public void SetSkillName(int skillId, string displayName)
        {
            if (!_slotIndexBySkillId.TryGetValue(skillId, out int idx))
                return;

            if (_slots[idx].NameText != null)
                _slots[idx].NameText.text = displayName ?? string.Empty;
        }

        public void SetCooldown(int skillId, float remaining, float total)
        {
            if (!_slotIndexBySkillId.TryGetValue(skillId, out int idx))
                return;

            float normalized = total > 0.0001f ? Mathf.Clamp01(remaining / total) : 0f;

            if (_slots[idx].CooldownFill != null)
                _slots[idx].CooldownFill.fillAmount = normalized;

            if (_slots[idx].CooldownText != null)
                _slots[idx].CooldownText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;

            if (_slots[idx].TriggerButton != null)
                _slots[idx].TriggerButton.interactable = remaining <= 0.01f;
        }

        public void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.text = text ?? string.Empty;
        }
    }
}
