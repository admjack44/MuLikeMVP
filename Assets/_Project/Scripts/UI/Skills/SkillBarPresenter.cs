using MuLike.Gameplay.Combat;
using UnityEngine;

namespace MuLike.UI.Skills
{
    /// <summary>
    /// Presenter for skill bar cooldown/status wiring.
    /// </summary>
    public class SkillBarPresenter : MonoBehaviour
    {
        [SerializeField] private CombatController _combat;
        [SerializeField] private SkillBarView _view;

        private void Awake()
        {
            if (_combat == null)
                _combat = FindObjectOfType<CombatController>();

            if (_view == null)
                _view = FindObjectOfType<SkillBarView>();

            if (_combat == null || _view == null)
            {
                Debug.LogWarning("[SkillBarPresenter] Missing CombatController or SkillBarView.");
                enabled = false;
                return;
            }

            for (int i = 0; i < _combat.SkillCount; i++)
            {
                if (_combat.TryGetSkillModel(i, out CombatSkillModel skill))
                    _view.SetSkillName(skill.SkillId, skill.DisplayName);
            }
        }

        private void OnEnable()
        {
            if (_combat == null || _view == null)
                return;

            _view.SkillTriggered += HandleSkillTriggered;
            _combat.OnSkillCooldownUpdated += HandleCooldownUpdated;
            _combat.OnSkillCastRejected += HandleSkillRejected;
            _combat.OnSkillCastConfirmed += HandleSkillConfirmed;
        }

        private void OnDisable()
        {
            if (_combat == null || _view == null)
                return;

            _view.SkillTriggered -= HandleSkillTriggered;
            _combat.OnSkillCooldownUpdated -= HandleCooldownUpdated;
            _combat.OnSkillCastRejected -= HandleSkillRejected;
            _combat.OnSkillCastConfirmed -= HandleSkillConfirmed;
        }

        private void HandleSkillTriggered(int skillId)
        {
            for (int i = 0; i < _combat.SkillCount; i++)
            {
                if (!_combat.TryGetSkillModel(i, out CombatSkillModel skill))
                    continue;

                if (skill.SkillId == skillId)
                {
                    _combat.TryCastSkillByIndex(i);
                    return;
                }
            }

            _view.SetStatus($"Unknown skill id {skillId}.");
        }

        private void HandleCooldownUpdated(int skillId, float remaining, float total)
        {
            _view.SetCooldown(skillId, remaining, total);
        }

        private void HandleSkillRejected(int skillId, string reason)
        {
            _view.SetStatus($"Skill {skillId} rejected: {reason}");
        }

        private void HandleSkillConfirmed(int skillId, int targetId, int damage, string message)
        {
            _view.SetStatus($"Skill {skillId} -> target {targetId}: {message} ({damage})");
        }
    }
}
