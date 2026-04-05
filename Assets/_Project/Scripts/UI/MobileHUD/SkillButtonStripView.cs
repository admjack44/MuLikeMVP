using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Reusable right-side skill strip with cooldown support.
    /// </summary>
    public sealed class SkillButtonStripView : MonoBehaviour
    {
        [Serializable]
        public struct SkillButtonBinding
        {
            public int skillId;
            public Button button;
            public TMP_Text nameText;
            public Image cooldownFill;
            public TMP_Text cooldownText;
        }

        [SerializeField] private SkillButtonBinding[] _buttons = Array.Empty<SkillButtonBinding>();
        [SerializeField, Range(5f, 60f)] private float _maxCooldownUiUpdatesPerSecond = 15f;

        private readonly Dictionary<int, float> _nextUiUpdateBySkill = new();

        public event Action<int> SkillPressed;

        private void Awake()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                int capturedId = _buttons[i].skillId;
                if (_buttons[i].button != null)
                    _buttons[i].button.onClick.AddListener(() => SkillPressed?.Invoke(capturedId));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i].button != null)
                    _buttons[i].button.onClick.RemoveAllListeners();
            }
        }

        public void SetSkillName(int skillId, string name)
        {
            if (!TryGet(skillId, out SkillButtonBinding binding))
                return;

            if (binding.nameText != null)
                binding.nameText.text = name ?? string.Empty;
        }

        public void SetCooldown(int skillId, float remaining, float total)
        {
            if (!TryGet(skillId, out SkillButtonBinding binding))
                return;

            float now = Time.unscaledTime;
            float minInterval = 1f / Mathf.Clamp(_maxCooldownUiUpdatesPerSecond, 5f, 60f);
            if (_nextUiUpdateBySkill.TryGetValue(skillId, out float nextAllowedAt) && now < nextAllowedAt)
                return;

            _nextUiUpdateBySkill[skillId] = now + minInterval;

            float normalized = total > 0.001f ? Mathf.Clamp01(remaining / total) : 0f;

            if (binding.cooldownFill != null)
                binding.cooldownFill.fillAmount = normalized;

            if (binding.cooldownText != null)
                binding.cooldownText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;

            if (binding.button != null)
                binding.button.interactable = remaining <= 0.01f;
        }

        public void SetInteractable(int skillId, bool interactable)
        {
            if (!TryGet(skillId, out SkillButtonBinding binding))
                return;

            if (binding.button != null)
                binding.button.interactable = interactable;
        }

        private bool TryGet(int skillId, out SkillButtonBinding binding)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i].skillId == skillId)
                {
                    binding = _buttons[i];
                    return true;
                }
            }

            binding = default;
            return false;
        }
    }
}
