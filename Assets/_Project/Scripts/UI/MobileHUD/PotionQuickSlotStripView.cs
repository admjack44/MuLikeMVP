using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Quick slot strip for restorative consumables (typically HP/MP potions).
    /// </summary>
    public sealed class PotionQuickSlotStripView : MonoBehaviour
    {
        [Serializable]
        public struct PotionSlotBinding
        {
            public int slotId;
            public Button button;
            public TMP_Text labelText;
            public TMP_Text countText;
            public Image cooldownFill;
            public TMP_Text cooldownText;
        }

        [SerializeField] private PotionSlotBinding[] _slots = Array.Empty<PotionSlotBinding>();
        [SerializeField, Range(5f, 60f)] private float _maxCooldownUiUpdatesPerSecond = 20f;

        private readonly Dictionary<int, float> _nextUiUpdateBySlot = new();

        public event Action<int> SlotPressed;

        private void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                int capturedId = _slots[i].slotId;
                if (_slots[i].button != null)
                    _slots[i].button.onClick.AddListener(() => SlotPressed?.Invoke(capturedId));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].button != null)
                    _slots[i].button.onClick.RemoveAllListeners();
            }
        }

        public void SetSlot(
            int slotId,
            string label,
            int count,
            float cooldownRemaining,
            float cooldownTotal,
            bool interactable)
        {
            if (!TryGetSlot(slotId, out PotionSlotBinding binding))
                return;

            if (binding.labelText != null)
                binding.labelText.text = label ?? string.Empty;

            if (binding.countText != null)
                binding.countText.text = Mathf.Max(0, count).ToString();

            float now = Time.unscaledTime;
            float minInterval = 1f / Mathf.Clamp(_maxCooldownUiUpdatesPerSecond, 5f, 60f);
            if (_nextUiUpdateBySlot.TryGetValue(slotId, out float nextAllowedAt) && now < nextAllowedAt)
                return;

            _nextUiUpdateBySlot[slotId] = now + minInterval;

            float normalized = cooldownTotal > 0.001f
                ? Mathf.Clamp01(cooldownRemaining / cooldownTotal)
                : 0f;

            if (binding.cooldownFill != null)
                binding.cooldownFill.fillAmount = normalized;

            if (binding.cooldownText != null)
                binding.cooldownText.text = cooldownRemaining > 0.05f ? Mathf.CeilToInt(cooldownRemaining).ToString() : string.Empty;

            if (binding.button != null)
                binding.button.interactable = interactable && count > 0 && cooldownRemaining <= 0.01f;
        }

        private bool TryGetSlot(int slotId, out PotionSlotBinding binding)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].slotId == slotId)
                {
                    binding = _slots[i];
                    return true;
                }
            }

            binding = default;
            return false;
        }
    }
}
