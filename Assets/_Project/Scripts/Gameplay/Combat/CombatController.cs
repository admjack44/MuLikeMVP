using System;
using System.Collections.Generic;
using MuLike.Core;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
using MuLike.Systems;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Gameplay.Combat
{
    /// <summary>
    /// Client combat orchestrator for target selection, basic attack and skill cast requests.
    /// Keeps local validation and cooldown visuals without server gameplay logic.
    /// </summary>
    public class CombatController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TargetingController _targeting;
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private NetworkGameClient _networkClient;
        [SerializeField] private StatsClientSystem _statsSystem;

        [Header("Combat")]
        [SerializeField] private float _basicAttackRange = 2.8f;
        [SerializeField] private float _basicAttackCooldown = 0.65f;
        [SerializeField] private KeyCode _basicAttackKey = KeyCode.F4;
        [SerializeField] private int _basicAttackSkillId = 0;
        [SerializeField] private bool _autoAcquireTargetOnAttack = true;
        [SerializeField] private int _basicAttackManaCost = 0;
        [SerializeField] private int _basicAttackStaminaCost = 0;

        [Header("Skills")]
        [SerializeField] private CombatSkillModel[] _skills = Array.Empty<CombatSkillModel>();
        [SerializeField] private bool _seedDefaultSkillsIfEmpty = true;
        [SerializeField] private bool _enableKeyboardShortcuts = false;

        [Header("Cast Lock")]
        [SerializeField] private float _defaultCastLockDuration = 0.3f;

        [Header("Auto Combat")]
        [SerializeField] private AutoCombatSystem _autoCombatSystem = new();
        [SerializeField] private bool _autoCombatEnabledByDefault;

        [Header("Target Loop")]
        [SerializeField] private bool _usePriorityTargeting = true;
        [SerializeField] private bool _autoRetargetOnTargetDeath = true;
        [SerializeField] private bool _autoRetargetOnTargetOutOfRange = true;
        [SerializeField] private bool _clearTargetIfNoRetarget = true;
        [SerializeField] private float _autoRetargetInterval = 0.25f;

        [Header("Auto Attack Tick")]
        [SerializeField] private bool _enableAutoAttackTick;
        [SerializeField] private float _autoAttackTickInterval = 0.2f;
        [SerializeField] private bool _autoAttackRequiresInRange = true;

        [Header("Actor Status")]
        [SerializeField] private bool _isDead;
        [SerializeField] private bool _isStunned;

        private readonly Dictionary<int, float> _cooldownEndBySkillId = new();
        private float _castLockRemaining;
        private float _nextAutoAttackTickAt;
        private float _nextRetargetAt;

        public event Action<int, float, float> OnSkillCooldownUpdated;
        public event Action<int> OnSkillCastStarted;
        public event Action<int, string> OnSkillCastRejected;
        public event Action<int, int, int, string> OnSkillCastConfirmed;
        public event Action<CombatFeedbackEvent> OnCombatFeedbackRequested;
        public event Action<bool> OnAutoCombatEnabledChanged;
        public event Action<AutoCombatState> OnAutoCombatStateChanged;
        public event Action<string> OnAutoCombatLog;
        public event Action<int, int> OnTargetRetargeted;
        public event Action<int> OnTargetCleared;

        public int SkillCount => _skills?.Length ?? 0;
        public int BasicAttackSkillId => _basicAttackSkillId;
        public float BasicAttackRange => _basicAttackRange;
        public bool IsAutoCombatEnabled => _autoCombatSystem != null && _autoCombatSystem.IsEnabled;

        private void Awake()
        {
            EnsureDefaultSkills();

            if (_targeting == null)
                _targeting = FindAnyObjectByType<TargetingController>();

            if (_motor == null)
                _motor = FindAnyObjectByType<CharacterMotor>();

            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();

            if (_statsSystem == null && GameContext.TryGetSystem(out StatsClientSystem stats))
                _statsSystem = stats;

            _autoCombatSystem ??= new AutoCombatSystem();
            _autoCombatSystem.Initialize(this, _motor, _targeting, transform, _statsSystem);
            _autoCombatSystem.StateChanged += HandleAutoCombatStateChanged;
            _autoCombatSystem.LogGenerated += HandleAutoCombatLog;
        }

        private void EnsureDefaultSkills()
        {
            if (!_seedDefaultSkillsIfEmpty)
                return;

            if (_skills != null && _skills.Length >= 3)
                return;

            _skills = new[]
            {
                new CombatSkillModel
                {
                    SkillId = 1,
                    DisplayName = "Slash",
                    Range = 3f,
                    Cooldown = 2.5f,
                    ManaCost = 8,
                    StaminaCost = 0,
                    AutoPriority = 30,
                    AllowAutoCast = true,
                    LocksMovement = true,
                    Hotkey = KeyCode.Alpha1
                },
                new CombatSkillModel
                {
                    SkillId = 2,
                    DisplayName = "Whirlwind",
                    Range = 3.4f,
                    Cooldown = 4.5f,
                    ManaCost = 14,
                    StaminaCost = 5,
                    AutoPriority = 20,
                    AllowAutoCast = true,
                    LocksMovement = true,
                    Hotkey = KeyCode.Alpha2
                },
                new CombatSkillModel
                {
                    SkillId = 3,
                    DisplayName = "Pierce",
                    Range = 4.2f,
                    Cooldown = 6f,
                    ManaCost = 18,
                    StaminaCost = 0,
                    AutoPriority = 10,
                    AllowAutoCast = true,
                    LocksMovement = true,
                    Hotkey = KeyCode.Alpha3
                }
            };
        }

        private void OnEnable()
        {
            if (_networkClient != null)
            {
                _networkClient.OnSkillResult += HandleSkillResult;
                _networkClient.OnAttackResult += HandleAttackResultForThreat;
                _networkClient.OnEntityDied += HandleEntityDiedFromServer;
            }

            if (_statsSystem != null)
                _statsSystem.OnStatsSnapshotApplied += HandleStatsSnapshotForControlState;
        }

        private void OnDisable()
        {
            if (_networkClient != null)
            {
                _networkClient.OnSkillResult -= HandleSkillResult;
                _networkClient.OnAttackResult -= HandleAttackResultForThreat;
                _networkClient.OnEntityDied -= HandleEntityDiedFromServer;
            }

            if (_statsSystem != null)
                _statsSystem.OnStatsSnapshotApplied -= HandleStatsSnapshotForControlState;

            if (_autoCombatSystem != null)
            {
                _autoCombatSystem.StateChanged -= HandleAutoCombatStateChanged;
                _autoCombatSystem.LogGenerated -= HandleAutoCombatLog;
            }

            if (_motor != null)
                _motor.NotifyCastingState(false);

            _castLockRemaining = 0f;
        }

        private void Start()
        {
            SetAutoCombatEnabled(_autoCombatEnabledByDefault);
        }

        private void Update()
        {
            UpdateCastLock();
            UpdateCooldownVisuals();
            _autoCombatSystem?.Tick();
            HandleTargetLoopTick();
            HandleAutoAttackTickControl();
            ProcessInput();
        }

        public EntityView AcquireNearestTarget(bool lockAsCurrent = true)
        {
            if (_targeting == null)
                return null;

            EntityView next = _usePriorityTargeting
                ? _targeting.AcquirePriorityTarget()
                : _targeting.AcquireNearestVisibleTarget();

            if (lockAsCurrent && next != null && next != _targeting.CurrentTarget)
                _targeting.SetLockedTarget(next);

            return next;
        }

        public bool IsTargetInRange(float range)
        {
            return IsTargetInRange(_targeting != null ? _targeting.CurrentTarget : null, range);
        }

        public bool IsTargetInRange(EntityView target, float range)
        {
            if (_targeting == null)
                return false;

            return _targeting.IsTargetInRange(target, transform.position, range);
        }

        public bool AutoRetarget()
        {
            if (_targeting == null)
                return false;

            int previousId = _targeting.CurrentTarget != null ? _targeting.CurrentTarget.EntityId : 0;
            EntityView next = AcquireNearestTarget(lockAsCurrent: false);
            if (next == null)
            {
                if (_clearTargetIfNoRetarget && _targeting.CurrentTarget != null)
                {
                    int cleared = _targeting.CurrentTarget.EntityId;
                    _targeting.ReleaseTarget();
                    OnTargetCleared?.Invoke(cleared);
                }

                return false;
            }

            if (_targeting.CurrentTarget == next)
                return true;

            _targeting.SetLockedTarget(next);
            OnTargetRetargeted?.Invoke(previousId, next.EntityId);
            return true;
        }

        public bool TryGetSkillModel(int index, out CombatSkillModel skill)
        {
            if (_skills == null || index < 0 || index >= _skills.Length)
            {
                skill = default;
                return false;
            }

            skill = _skills[index];
            return true;
        }

        public float GetRemainingCooldown(int skillId)
        {
            if (!_cooldownEndBySkillId.TryGetValue(skillId, out float endAt))
                return 0f;

            return Mathf.Max(0f, endAt - Time.time);
        }

        public float GetTotalCooldown(int skillId)
        {
            if (skillId == _basicAttackSkillId)
                return _basicAttackCooldown;

            if (_skills == null) return 0f;
            for (int i = 0; i < _skills.Length; i++)
            {
                if (_skills[i].SkillId == skillId)
                    return Mathf.Max(0f, _skills[i].Cooldown);
            }

            return 0f;
        }

        public bool TryBasicAttack()
        {
            return TryCastInternal(
                skillId: _basicAttackSkillId,
                range: _basicAttackRange,
                cooldown: _basicAttackCooldown,
                manaCost: _basicAttackManaCost,
                staminaCost: _basicAttackStaminaCost,
                lockMovement: true,
                feedbackType: CombatFeedbackType.BasicAttackStarted,
                debugName: "BasicAttack");
        }

        public bool TryCastSkillByIndex(int index)
        {
            if (!TryGetSkillModel(index, out CombatSkillModel skill))
            {
                Reject(index, "Invalid skill index.");
                return false;
            }

            if (!skill.IsValid)
            {
                Reject(skill.SkillId, "Invalid skill configuration.");
                return false;
            }

            return TryCastInternal(
                skillId: skill.SkillId,
                range: skill.Range,
                cooldown: skill.Cooldown,
                manaCost: skill.ManaCost,
                staminaCost: skill.StaminaCost,
                lockMovement: skill.LocksMovement,
                feedbackType: CombatFeedbackType.SkillCastStarted,
                debugName: skill.DisplayName);
        }

        public bool TryCastSkillById(int skillId)
        {
            if (skillId == _basicAttackSkillId)
                return TryBasicAttack();

            if (_skills == null)
                return false;

            for (int i = 0; i < _skills.Length; i++)
            {
                if (_skills[i].SkillId != skillId)
                    continue;

                return TryCastSkillByIndex(i);
            }

            Reject(skillId, "Skill id not found.");
            return false;
        }

        public bool CanCastSkillById(int skillId, out string reason)
        {
            reason = string.Empty;
            if (_isDead)
            {
                reason = "Actor is dead.";
                return false;
            }

            if (_isStunned)
            {
                reason = "Actor is stunned.";
                return false;
            }

            float requiredRange;
            int manaCost;
            int staminaCost;

            if (skillId == _basicAttackSkillId)
            {
                requiredRange = _basicAttackRange;
                manaCost = _basicAttackManaCost;
                staminaCost = _basicAttackStaminaCost;

                if (IsOnCooldown(skillId))
                {
                    reason = "Skill is on cooldown.";
                    return false;
                }

                if (!HasValidCastTarget(requiredRange, out reason))
                    return false;

                return CanConsumeResources(manaCost, staminaCost, consume: false, out reason);
            }

            if (_skills == null)
            {
                reason = "Skill set is empty.";
                return false;
            }

            for (int i = 0; i < _skills.Length; i++)
            {
                CombatSkillModel model = _skills[i];
                if (model.SkillId != skillId)
                    continue;

                if (IsOnCooldown(model.SkillId))
                {
                    reason = "Skill is on cooldown.";
                    return false;
                }

                requiredRange = model.Range;
                manaCost = model.ManaCost;
                staminaCost = model.StaminaCost;

                if (!HasValidCastTarget(requiredRange, out reason))
                    return false;

                return CanConsumeResources(manaCost, staminaCost, consume: false, out reason);
            }

            reason = "Skill id not found.";
            return false;
        }

        public void SetAutoCombatEnabled(bool enabled)
        {
            _autoCombatSystem?.SetEnabled(enabled);
            Debug.Log($"[AutoCombat] Enabled={enabled}");
            OnAutoCombatEnabledChanged?.Invoke(enabled);
        }

        public void SetControlBlockers(bool isDead, bool isStunned)
        {
            _isDead = isDead;
            _isStunned = isStunned;
            _autoCombatSystem?.SetControlBlockers(isDead, isStunned);

            if (isDead || isStunned)
                _motor?.Stop();
        }

        private bool TryCastInternal(
            int skillId,
            float range,
            float cooldown,
            int manaCost,
            int staminaCost,
            bool lockMovement,
            CombatFeedbackType feedbackType,
            string debugName)
        {
            if (_isDead)
            {
                Reject(skillId, "Cannot cast while dead.");
                return false;
            }

            if (_isStunned)
            {
                Reject(skillId, "Cannot cast while stunned.");
                return false;
            }

            if (_networkClient == null || !_networkClient.IsConnected || !_networkClient.IsAuthenticated)
            {
                Reject(skillId, "Not connected/authenticated.");
                return false;
            }

            if (_targeting == null || _targeting.CurrentTarget == null)
            {
                if (_autoAcquireTargetOnAttack && _targeting != null)
                {
                    EntityView auto = _targeting.AcquireBestTarget();
                    if (auto != null)
                        _targeting.SetLockedTarget(auto);
                }

                if (_targeting == null || _targeting.CurrentTarget == null)
                {
                    Reject(skillId, "No target selected.");
                    return false;
                }
            }

            EntityView target = _targeting.CurrentTarget;
            if (!IsTargetInRange(target, Mathf.Max(0f, range)))
            {
                Reject(skillId, "Target out of range.");
                return false;
            }

            if (IsOnCooldown(skillId))
            {
                Reject(skillId, "Skill is on cooldown.");
                return false;
            }

            if (!CanConsumeResources(manaCost, staminaCost, consume: true, out string resourceError))
            {
                Reject(skillId, resourceError);
                return false;
            }

            SetCooldown(skillId, cooldown);
            ApplyCastLock(lockMovement);

            OnSkillCastStarted?.Invoke(skillId);
            OnCombatFeedbackRequested?.Invoke(new CombatFeedbackEvent
            {
                Type = feedbackType,
                SkillId = skillId,
                TargetEntityId = target.EntityId,
                Damage = 0,
                Message = debugName,
                WorldPosition = target.transform.position
            });

            _ = _networkClient.SendSkillCastAsync(skillId, target.EntityId);
            return true;
        }

        private bool HasValidCastTarget(float requiredRange, out string reason)
        {
            reason = string.Empty;
            if (_targeting == null)
            {
                reason = "Targeting unavailable.";
                return false;
            }

            EntityView target = _targeting.CurrentTarget;
            if (target == null)
            {
                reason = "No target selected.";
                return false;
            }

            if (!IsTargetInRange(target, requiredRange))
            {
                reason = "Target out of range.";
                return false;
            }

            return true;
        }

        private bool CanConsumeResources(int manaCost, int staminaCost, bool consume, out string reason)
        {
            reason = string.Empty;
            if (_statsSystem == null)
                return true;

            StatsClientSystem.PlayerStatsSnapshot snapshot = _statsSystem.Snapshot;
            int currentMana = snapshot.Resources.Mana.Current;
            int currentStamina = snapshot.Resources.Stamina.Current;

            if (currentMana < Mathf.Max(0, manaCost))
            {
                reason = "Not enough mana.";
                return false;
            }

            if (currentStamina < Mathf.Max(0, staminaCost))
            {
                reason = "Not enough stamina.";
                return false;
            }

            if (!consume)
                return true;

            _statsSystem.ApplyDelta(new StatsClientSystem.PlayerStatsDelta
            {
                HasMana = true,
                ManaCurrent = Mathf.Max(0, currentMana - Mathf.Max(0, manaCost)),
                ManaMax = snapshot.Resources.Mana.Max,
                HasStamina = true,
                StaminaCurrent = Mathf.Max(0, currentStamina - Mathf.Max(0, staminaCost)),
                StaminaMax = snapshot.Resources.Stamina.Max
            });

            return true;
        }

        private void HandleSkillResult(bool success, int targetId, int damage, string message)
        {
            if (_targeting == null || _targeting.CurrentTarget == null)
                return;

            int skillId = InferSkillIdFromCooldownContext();

            if (!success)
            {
                OnSkillCastRejected?.Invoke(skillId, message);
                OnCombatFeedbackRequested?.Invoke(new CombatFeedbackEvent
                {
                    Type = CombatFeedbackType.SkillCastRejected,
                    SkillId = skillId,
                    TargetEntityId = targetId,
                    Damage = 0,
                    Message = message,
                    WorldPosition = _targeting.CurrentTarget.transform.position
                });
                return;
            }

            OnSkillCastConfirmed?.Invoke(skillId, targetId, damage, message);
            OnCombatFeedbackRequested?.Invoke(new CombatFeedbackEvent
            {
                Type = CombatFeedbackType.SkillCastConfirmed,
                SkillId = skillId,
                TargetEntityId = targetId,
                Damage = damage,
                Message = message,
                WorldPosition = _targeting.CurrentTarget.transform.position
            });
        }

        private int InferSkillIdFromCooldownContext()
        {
            int bestSkillId = _basicAttackSkillId;
            float bestRemaining = -1f;

            foreach (var pair in _cooldownEndBySkillId)
            {
                float remaining = pair.Value - Time.time;
                if (remaining > bestRemaining)
                {
                    bestRemaining = remaining;
                    bestSkillId = pair.Key;
                }
            }

            return bestSkillId;
        }

        private void ProcessInput()
        {
            if (!_enableKeyboardShortcuts)
                return;

            if (WasKeyPressed(_basicAttackKey))
                TryBasicAttack();

            if (_skills == null) return;
            for (int i = 0; i < _skills.Length; i++)
            {
                if (WasKeyPressed(_skills[i].Hotkey))
                    TryCastSkillByIndex(i);
            }
        }

        private void UpdateCastLock()
        {
            if (_castLockRemaining <= 0f)
                return;

            _castLockRemaining -= Time.deltaTime;
            if (_castLockRemaining <= 0f)
            {
                _castLockRemaining = 0f;
                if (_motor != null)
                    _motor.NotifyCastingState(false);
            }
        }

        private void UpdateCooldownVisuals()
        {
            if (_cooldownEndBySkillId.Count == 0)
                return;

            if (_skills != null)
            {
                for (int i = 0; i < _skills.Length; i++)
                {
                    int skillId = _skills[i].SkillId;
                    float remaining = GetRemainingCooldown(skillId);
                    float total = Mathf.Max(0.01f, _skills[i].Cooldown);
                    OnSkillCooldownUpdated?.Invoke(skillId, remaining, total);
                }
            }

            float basicRemaining = GetRemainingCooldown(_basicAttackSkillId);
            OnSkillCooldownUpdated?.Invoke(_basicAttackSkillId, basicRemaining, Mathf.Max(0.01f, _basicAttackCooldown));
        }

        private bool IsOnCooldown(int skillId)
        {
            return GetRemainingCooldown(skillId) > 0f;
        }

        private void SetCooldown(int skillId, float cooldown)
        {
            _cooldownEndBySkillId[skillId] = Time.time + Mathf.Max(0f, cooldown);
        }

        private void ApplyCastLock(bool lockMovement)
        {
            if (!lockMovement || _motor == null)
                return;

            _castLockRemaining = Mathf.Max(_castLockRemaining, _defaultCastLockDuration);
            _motor.NotifyCastingState(true);
        }

        private void Reject(int skillId, string reason)
        {
            OnSkillCastRejected?.Invoke(skillId, reason);
            OnCombatFeedbackRequested?.Invoke(new CombatFeedbackEvent
            {
                Type = CombatFeedbackType.SkillCastRejected,
                SkillId = skillId,
                TargetEntityId = _targeting != null && _targeting.CurrentTarget != null ? _targeting.CurrentTarget.EntityId : 0,
                Damage = 0,
                Message = reason,
                WorldPosition = _targeting != null && _targeting.CurrentTarget != null ? _targeting.CurrentTarget.transform.position : transform.position
            });
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return keyCode switch
            {
                KeyCode.Alpha1 => keyboard.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => keyboard.digit2Key.wasPressedThisFrame,
                KeyCode.Alpha3 => keyboard.digit3Key.wasPressedThisFrame,
                KeyCode.Alpha4 => keyboard.digit4Key.wasPressedThisFrame,
                KeyCode.Alpha5 => keyboard.digit5Key.wasPressedThisFrame,
                KeyCode.Alpha6 => keyboard.digit6Key.wasPressedThisFrame,
                KeyCode.Alpha7 => keyboard.digit7Key.wasPressedThisFrame,
                KeyCode.Alpha8 => keyboard.digit8Key.wasPressedThisFrame,
                KeyCode.Alpha9 => keyboard.digit9Key.wasPressedThisFrame,
                KeyCode.F1 => keyboard.f1Key.wasPressedThisFrame,
                KeyCode.F2 => keyboard.f2Key.wasPressedThisFrame,
                KeyCode.F3 => keyboard.f3Key.wasPressedThisFrame,
                KeyCode.F4 => keyboard.f4Key.wasPressedThisFrame,
                KeyCode.F5 => keyboard.f5Key.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

        private void HandleAutoCombatStateChanged(AutoCombatState state)
        {
            OnAutoCombatStateChanged?.Invoke(state);
        }

        private void HandleAutoCombatLog(string line)
        {
            OnAutoCombatLog?.Invoke(line);
        }

        private void HandleStatsSnapshotForControlState(StatsClientSystem.PlayerStatsSnapshot snapshot)
        {
            bool isDeadNow = snapshot.Resources.Hp.Max > 0 && snapshot.Resources.Hp.Current <= 0;
            if (isDeadNow == _isDead)
                return;

            SetControlBlockers(isDeadNow, _isStunned);
            if (isDeadNow)
                Reject(_basicAttackSkillId, "Combat cancelled: actor is dead.");
        }

        private void HandleTargetLoopTick()
        {
            if (_targeting == null)
                return;

            if (Time.time < _nextRetargetAt)
                return;

            _nextRetargetAt = Time.time + Mathf.Max(0.05f, _autoRetargetInterval);

            EntityView current = _targeting.CurrentTarget;
            if (current == null)
            {
                if (_autoCombatSystem != null && _autoCombatSystem.IsEnabled)
                    AutoRetarget();

                return;
            }

            if (!_targeting.IsTargetValid(current))
            {
                if (!_autoRetargetOnTargetDeath || !AutoRetarget())
                {
                    int cleared = current.EntityId;
                    _targeting.ReleaseTarget();
                    OnTargetCleared?.Invoke(cleared);
                }

                return;
            }

            if (_autoRetargetOnTargetOutOfRange && !IsTargetInRange(current, _basicAttackRange))
                AutoRetarget();
        }

        private void HandleAutoAttackTickControl()
        {
            if (!_enableAutoAttackTick)
                return;

            if (_autoCombatSystem != null && _autoCombatSystem.IsEnabled)
                return;

            if (_isDead || _isStunned)
                return;

            if (Time.time < _nextAutoAttackTickAt)
                return;

            _nextAutoAttackTickAt = Time.time + Mathf.Max(0.05f, _autoAttackTickInterval);

            if (_targeting == null || _targeting.CurrentTarget == null)
            {
                if (_autoAcquireTargetOnAttack)
                    AutoRetarget();

                return;
            }

            if (_autoAttackRequiresInRange && !IsTargetInRange(_basicAttackRange))
            {
                if (_autoRetargetOnTargetOutOfRange)
                    AutoRetarget();

                return;
            }

            TryBasicAttack();
        }

        private void HandleAttackResultForThreat(int targetId, bool hitSuccess, int damage, bool _)
        {
            if (_targeting == null || targetId <= 0)
                return;

            float pressure = hitSuccess ? Mathf.Max(1f, damage * 0.01f) : 0.25f;
            _targeting.RegisterAggressor(targetId, pressure);
        }

        private void HandleEntityDiedFromServer(int entityId)
        {
            if (_targeting == null || entityId <= 0)
                return;

            if (_targeting.CurrentTarget == null || _targeting.CurrentTarget.EntityId != entityId)
                return;

            if (_autoRetargetOnTargetDeath && AutoRetarget())
                return;

            _targeting.ReleaseTarget();
            OnTargetCleared?.Invoke(entityId);
        }
    }

    public enum AutoCombatState
    {
        Disabled,
        SearchingTarget,
        ChasingTarget,
        CastingSkill,
        BasicAttacking,
        Suspended,
        Invalid
    }

    [Serializable]
    public sealed class SkillExecutionPolicy
    {
        [Header("Auto Skill Priority")]
        [SerializeField] private bool _useOnlyAutoEnabledSkills = true;
        [SerializeField] private bool _preferHighestPriority = true;

        public bool TrySelectBestSkill(
            CombatController combat,
            StatsClientSystem stats,
            EntityView target,
            out SkillSelection selection)
        {
            selection = default;
            if (combat == null || target == null)
                return false;

            int chosenIndex = -1;
            int chosenPriority = int.MinValue;

            for (int i = 0; i < combat.SkillCount; i++)
            {
                if (!combat.TryGetSkillModel(i, out CombatSkillModel skill))
                    continue;

                if (!skill.IsValid)
                    continue;

                if (_useOnlyAutoEnabledSkills && !skill.AllowAutoCast)
                    continue;

                if (!combat.CanCastSkillById(skill.SkillId, out _))
                    continue;

                if (_preferHighestPriority)
                {
                    if (skill.AutoPriority <= chosenPriority)
                        continue;

                    chosenPriority = skill.AutoPriority;
                    chosenIndex = i;
                    continue;
                }

                chosenIndex = i;
                break;
            }

            if (chosenIndex >= 0 && combat.TryGetSkillModel(chosenIndex, out CombatSkillModel chosen))
            {
                selection = new SkillSelection
                {
                    SkillId = chosen.SkillId,
                    ArrayIndex = chosenIndex,
                    Range = chosen.Range,
                    IsBasicAttack = false
                };

                return true;
            }

            selection = SkillSelection.Basic(combat.BasicAttackSkillId, combat.BasicAttackRange);
            return combat.CanCastSkillById(combat.BasicAttackSkillId, out _);
        }

        public struct SkillSelection
        {
            public int SkillId;
            public int ArrayIndex;
            public float Range;
            public bool IsBasicAttack;

            public static SkillSelection Basic(int skillId, float range)
            {
                return new SkillSelection
                {
                    SkillId = skillId,
                    ArrayIndex = -1,
                    Range = range,
                    IsBasicAttack = true
                };
            }
        }
    }

    [Serializable]
    public sealed class TargetSelectionPolicy
    {
        [Header("Selection")]
        [SerializeField] private float _maxAcquireDistance = 28f;
        [SerializeField] private float _retargetInterval = 0.25f;
        [SerializeField] private bool _allowRetargetWhenCurrentValid = false;

        private float _nextRetargetAt;

        public EntityView AcquireBestTarget(TargetingController targeting, Transform actor, float range)
        {
            if (targeting == null || actor == null)
                return null;

            float safeRange = Mathf.Min(_maxAcquireDistance, Mathf.Max(1f, range));
            if (Time.time < _nextRetargetAt && targeting.CurrentTarget != null)
                return targeting.CurrentTarget;

            _nextRetargetAt = Time.time + Mathf.Max(0.05f, _retargetInterval);

            if (!_allowRetargetWhenCurrentValid && targeting.CurrentTarget != null && targeting.IsTargetInRange(actor.position, safeRange))
                return targeting.CurrentTarget;

            return targeting.AcquireBestTarget(actor.position, actor.forward);
        }
    }

    [Serializable]
    public sealed class AutoCombatSystem
    {
        [Header("Tick")]
        [SerializeField] private float _decisionInterval = 0.12f;

        [Header("Pursuit")]
        [SerializeField] private float _chaseStopBuffer = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogs = false;

        [SerializeField] private SkillExecutionPolicy _skillPolicy = new();
        [SerializeField] private TargetSelectionPolicy _targetPolicy = new();

        private CombatController _combat;
        private CharacterMotor _motor;
        private TargetingController _targeting;
        private Transform _actor;
        private StatsClientSystem _stats;

        private AutoCombatState _state = AutoCombatState.Disabled;
        private bool _enabled;
        private bool _isDead;
        private bool _isStunned;
        private float _nextDecisionAt;

        public AutoCombatState State => _state;
        public bool IsEnabled => _enabled;

        public event Action<AutoCombatState> StateChanged;
        public event Action<string> LogGenerated;

        public void Initialize(
            CombatController combat,
            CharacterMotor motor,
            TargetingController targeting,
            Transform actor,
            StatsClientSystem stats)
        {
            _combat = combat;
            _motor = motor;
            _targeting = targeting;
            _actor = actor;
            _stats = stats;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _motor?.Stop();
                SetState(AutoCombatState.Disabled);
                return;
            }

            SetState(AutoCombatState.SearchingTarget);
        }

        public void SetControlBlockers(bool isDead, bool isStunned)
        {
            _isDead = isDead;
            _isStunned = isStunned;
        }

        public void Tick()
        {
            if (!_enabled)
                return;

            if (_combat == null || _motor == null || _targeting == null || _actor == null)
            {
                SetState(AutoCombatState.Invalid);
                return;
            }

            if (Time.time < _nextDecisionAt)
                return;

            _nextDecisionAt = Time.time + Mathf.Max(0.03f, _decisionInterval);

            if (_isDead || _isStunned)
            {
                _motor.Stop();
                SetState(AutoCombatState.Suspended);
                return;
            }

            ProcessCombatDecision();
        }

        private void ProcessCombatDecision()
        {
            SkillExecutionPolicy.SkillSelection selected;
            if (!_skillPolicy.TrySelectBestSkill(_combat, _stats, _targeting.CurrentTarget, out selected))
            {
                SetState(AutoCombatState.SearchingTarget);
                EmitLog("No castable skills/resources available.");
                return;
            }

            float requiredRange = Mathf.Max(0.1f, selected.Range);
            EntityView target = _targetPolicy.AcquireBestTarget(_targeting, _actor, Mathf.Max(requiredRange, _combat.BasicAttackRange));
            if (target == null)
            {
                _targeting.ReleaseTarget();
                SetState(AutoCombatState.SearchingTarget);
                EmitLog("No valid hostile target found.");
                return;
            }

            if (_targeting.CurrentTarget != target)
                _targeting.SetLockedTarget(target);

            float distance = Vector3.Distance(_actor.position, target.transform.position);
            if (distance > requiredRange + _chaseStopBuffer)
            {
                _motor.MoveToPoint(target.transform.position);
                SetState(AutoCombatState.ChasingTarget);
                return;
            }

            _motor.Stop();

            bool success = selected.IsBasicAttack
                ? _combat.TryBasicAttack()
                : _combat.TryCastSkillById(selected.SkillId);

            if (!success)
            {
                SetState(AutoCombatState.SearchingTarget);
                return;
            }

            SetState(selected.IsBasicAttack ? AutoCombatState.BasicAttacking : AutoCombatState.CastingSkill);
        }

        private void SetState(AutoCombatState next)
        {
            if (_state == next)
                return;

            _state = next;
            StateChanged?.Invoke(_state);
            EmitLog($"State -> {_state}");
        }

        private void EmitLog(string msg)
        {
            if (!_verboseLogs)
                return;

            string line = $"[AutoCombat] {msg}";
            Debug.Log(line);
            LogGenerated?.Invoke(line);
        }
    }
}
