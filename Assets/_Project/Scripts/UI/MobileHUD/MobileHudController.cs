using MuLike.Core;
using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Bridges MobileHudView with gameplay systems.
    /// </summary>
    public sealed class MobileHudController : MonoBehaviour
    {
        [SerializeField] private MobileHudView _view;
        [SerializeField] private CharacterMotor _characterMotor;
        [SerializeField] private CombatController _combatController;
        [SerializeField] private TargetingController _targetingController;
        [SerializeField] private StatsClientSystem _statsSystem;

        [Header("Auto Attack")]
        [SerializeField] private float _autoAttackTickInterval = 0.2f;

        private bool _autoAttackEnabled;
        private float _nextAutoAttackAt;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<MobileHudView>();

            if (_characterMotor == null)
                _characterMotor = FindObjectOfType<CharacterMotor>();

            if (_combatController == null)
                _combatController = FindObjectOfType<CombatController>();

            if (_targetingController == null)
                _targetingController = FindObjectOfType<TargetingController>();

            if (_statsSystem == null && GameContext.TryGetSystem(out StatsClientSystem stats))
                _statsSystem = stats;

            if (_view == null)
            {
                Debug.LogError("[MobileHudController] MobileHudView missing.");
                enabled = false;
                return;
            }

            if (_combatController != null)
            {
                for (int i = 0; i < _combatController.SkillCount; i++)
                {
                    if (_combatController.TryGetSkillModel(i, out CombatSkillModel skill))
                        _view.SetSkillName(skill.SkillId, skill.DisplayName);
                }
            }
        }

        private void OnEnable()
        {
            _view.MoveInputChanged += HandleMoveInput;
            _view.SkillPressed += HandleSkillPressed;
            _view.AutoAttackChanged += HandleAutoAttackChanged;
            _view.ChatPressed += HandleChatPressed;
            _view.InventoryPressed += HandleInventoryPressed;
            _view.MinimapPressed += HandleMinimapPressed;

            if (_statsSystem != null)
            {
                _statsSystem.OnStatsSnapshotApplied += HandleStatsSnapshot;
                _statsSystem.OnStatsDeltaApplied += HandleStatsDelta;
                HandleStatsSnapshot(_statsSystem.Snapshot);
            }

            if (_targetingController != null)
                _targetingController.OnTargetChanged += HandleTargetChanged;

            if (_combatController != null)
                _combatController.OnSkillCooldownUpdated += HandleSkillCooldownUpdated;
        }

        private void OnDisable()
        {
            _view.MoveInputChanged -= HandleMoveInput;
            _view.SkillPressed -= HandleSkillPressed;
            _view.AutoAttackChanged -= HandleAutoAttackChanged;
            _view.ChatPressed -= HandleChatPressed;
            _view.InventoryPressed -= HandleInventoryPressed;
            _view.MinimapPressed -= HandleMinimapPressed;

            if (_statsSystem != null)
            {
                _statsSystem.OnStatsSnapshotApplied -= HandleStatsSnapshot;
                _statsSystem.OnStatsDeltaApplied -= HandleStatsDelta;
            }

            if (_targetingController != null)
                _targetingController.OnTargetChanged -= HandleTargetChanged;

            if (_combatController != null)
                _combatController.OnSkillCooldownUpdated -= HandleSkillCooldownUpdated;
        }

        private void Update()
        {
            if (!_autoAttackEnabled || _combatController == null)
                return;

            if (Time.time < _nextAutoAttackAt)
                return;

            _nextAutoAttackAt = Time.time + Mathf.Max(0.05f, _autoAttackTickInterval);
            _combatController.TryBasicAttack();
        }

        private void HandleMoveInput(Vector2 input)
        {
            if (_characterMotor == null)
                return;

            Vector3 direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 0.001f)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    Vector3 forward = cam.forward;
                    Vector3 right = cam.right;
                    forward.y = 0f;
                    right.y = 0f;
                    forward.Normalize();
                    right.Normalize();
                    direction = (right * input.x + forward * input.y).normalized;
                }

                _characterMotor.SetMoveDirection(direction);
            }
            else
            {
                _characterMotor.Stop();
            }
        }

        private void HandleSkillPressed(int skillId)
        {
            if (_combatController == null)
                return;

            for (int i = 0; i < _combatController.SkillCount; i++)
            {
                if (!_combatController.TryGetSkillModel(i, out CombatSkillModel skill))
                    continue;

                if (skill.SkillId == skillId)
                {
                    _combatController.TryCastSkillByIndex(i);
                    return;
                }
            }
        }

        private void HandleAutoAttackChanged(bool enabled)
        {
            _autoAttackEnabled = enabled;
            _nextAutoAttackAt = Time.time;
        }

        private void HandleStatsSnapshot(StatsClientSystem.PlayerStatsSnapshot snapshot)
        {
            _view.SetHp(snapshot.Resources.Hp.Current, snapshot.Resources.Hp.Max);
            _view.SetMp(snapshot.Resources.Mana.Current, snapshot.Resources.Mana.Max);
            _view.SetSd(snapshot.Resources.Shield.Current, snapshot.Resources.Shield.Max);
            _view.SetCombo(snapshot.Resources.Stamina.Current, snapshot.Resources.Stamina.Max);
        }

        private void HandleStatsDelta(StatsClientSystem.PlayerStatsDelta _)
        {
            if (_statsSystem != null)
                HandleStatsSnapshot(_statsSystem.Snapshot);
        }

        private void HandleTargetChanged(EntityView target)
        {
            _view.SetTarget(target != null ? target.name : "No target");
        }

        private void HandleSkillCooldownUpdated(int skillId, float remaining, float total)
        {
            _view.SetSkillCooldown(skillId, remaining, total);
        }

        private static void HandleChatPressed()
        {
            Debug.Log("[MobileHUD] Chat button pressed.");
        }

        private static void HandleInventoryPressed()
        {
            Debug.Log("[MobileHUD] Inventory button pressed.");
        }

        private static void HandleMinimapPressed()
        {
            Debug.Log("[MobileHUD] Minimap placeholder pressed.");
        }
    }
}
