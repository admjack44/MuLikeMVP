using System;
using System.Collections.Generic;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Networking;
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

        [Header("Combat")]
        [SerializeField] private float _basicAttackRange = 2.8f;
        [SerializeField] private float _basicAttackCooldown = 0.65f;
        [SerializeField] private KeyCode _basicAttackKey = KeyCode.F4;
        [SerializeField] private int _basicAttackSkillId = 0;

        [Header("Skills")]
        [SerializeField] private CombatSkillModel[] _skills = Array.Empty<CombatSkillModel>();

        [Header("Cast Lock")]
        [SerializeField] private float _defaultCastLockDuration = 0.3f;

        private readonly Dictionary<int, float> _cooldownEndBySkillId = new();
        private float _castLockRemaining;

        public event Action<int, float, float> OnSkillCooldownUpdated;
        public event Action<int> OnSkillCastStarted;
        public event Action<int, string> OnSkillCastRejected;
        public event Action<int, int, int, string> OnSkillCastConfirmed;
        public event Action<CombatFeedbackEvent> OnCombatFeedbackRequested;

        public int SkillCount => _skills?.Length ?? 0;

        private void Awake()
        {
            if (_targeting == null)
                _targeting = FindObjectOfType<TargetingController>();

            if (_motor == null)
                _motor = FindObjectOfType<CharacterMotor>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();
        }

        private void OnEnable()
        {
            if (_networkClient != null)
                _networkClient.OnSkillResult += HandleSkillResult;
        }

        private void OnDisable()
        {
            if (_networkClient != null)
                _networkClient.OnSkillResult -= HandleSkillResult;

            if (_motor != null)
                _motor.NotifyCastingState(false);

            _castLockRemaining = 0f;
        }

        private void Update()
        {
            UpdateCastLock();
            UpdateCooldownVisuals();
            ProcessInput();
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
                lockMovement: skill.LocksMovement,
                feedbackType: CombatFeedbackType.SkillCastStarted,
                debugName: skill.DisplayName);
        }

        private bool TryCastInternal(
            int skillId,
            float range,
            float cooldown,
            bool lockMovement,
            CombatFeedbackType feedbackType,
            string debugName)
        {
            if (_networkClient == null || !_networkClient.IsConnected || !_networkClient.IsAuthenticated)
            {
                Reject(skillId, "Not connected/authenticated.");
                return false;
            }

            if (_targeting == null || _targeting.CurrentTarget == null)
            {
                Reject(skillId, "No target selected.");
                return false;
            }

            EntityView target = _targeting.CurrentTarget;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > Mathf.Max(0f, range))
            {
                Reject(skillId, "Target out of range.");
                return false;
            }

            if (IsOnCooldown(skillId))
            {
                Reject(skillId, "Skill is on cooldown.");
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
    }
}
