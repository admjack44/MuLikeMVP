using System;
using UnityEngine;

namespace MuLike.Combat
{
    /// <summary>
    /// Animator-event bridge that emits hit frame notifications.
    ///
    /// Usage:
    /// - Add animation event in attack/cast clip calling `AnimEvent_HitFrame`.
    /// - Runtime subscribers (combat/skill/vfx/sfx) synchronize damage with server-authoritative flow.
    ///
    /// This keeps combo/hit logic frame-rate independent: events are driven by clip timeline,
    /// not by Update() delta.
    /// </summary>
    public sealed class HitFrameDispatcher : MonoBehaviour
    {
        public struct HitFrameEvent
        {
            public int skillId;
            public int comboStep;
            public float normalizedTime;
            public string marker;
        }

        public event Action<HitFrameEvent> OnHitFrameReached;

        public void AnimEvent_HitFrame()
        {
            Emit(0, 0, 0f, "hit");
        }

        public void AnimEvent_HitFrameSkill(int skillId)
        {
            Emit(skillId, 0, 0f, "skill-hit");
        }

        public void AnimEvent_HitFrameCombo(int comboStep)
        {
            Emit(0, comboStep, 0f, "combo-hit");
        }

        public void AnimEvent_HitFrameExt(string payload)
        {
            // Payload format: skillId|comboStep|normalized|marker
            if (string.IsNullOrWhiteSpace(payload))
            {
                Emit(0, 0, 0f, "ext");
                return;
            }

            string[] parts = payload.Split('|');
            int skillId = parts.Length > 0 && int.TryParse(parts[0], out int s) ? s : 0;
            int combo = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 0;
            float norm = parts.Length > 2 && float.TryParse(parts[2], out float n) ? Mathf.Clamp01(n) : 0f;
            string marker = parts.Length > 3 ? parts[3] : "ext";
            Emit(skillId, combo, norm, marker);
        }

        private void Emit(int skillId, int comboStep, float normalized, string marker)
        {
            OnHitFrameReached?.Invoke(new HitFrameEvent
            {
                skillId = skillId,
                comboStep = comboStep,
                normalizedTime = normalized,
                marker = marker
            });
        }
    }
}
