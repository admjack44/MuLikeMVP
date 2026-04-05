using System;
using MuLike.Classes;
using MuLike.Skills;
using UnityEngine;

namespace MuLike.Combat
{
    /// <summary>
    /// Class-aware combo runtime independent from SkillManager.
    ///
    /// Interop pattern:
    /// - SkillManager confirms local execution -> call RegisterSkillExecution(skillId)
    /// - HitFrameDispatcher event can optionally validate step advancement for strict combos
    /// - Consumers read CurrentBonusMultiplier for damage/effect amplification
    /// </summary>
    public sealed class ComboSystem : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ComboDefinition[] _definitions = Array.Empty<ComboDefinition>();
        [SerializeField] private CombatTuningProfile _tuning;

        [Header("State")]
        [SerializeField] private MuClassId _activeClass = MuClassId.Unknown;

        private ComboDefinition _activeDefinition;
        private int _nextStepIndex;
        private float _lastInputAt;
        private bool _comboActive;
        private float _currentBonus = 1f;

        public bool IsComboActive => _comboActive;
        public float CurrentBonusMultiplier => _currentBonus;
        public int NextRequiredSkillId => ResolveNextSkillId();

        public event Action<int, int> OnComboProgressed;
        public event Action OnComboReset;
        public event Action<float> OnComboFinalBonusApplied;

        private void Awake()
        {
            ResolveActiveDefinition();
        }

        private void Update()
        {
            if (_activeDefinition == null || _activeDefinition.steps == null || _activeDefinition.steps.Length == 0)
                return;

            if (!_comboActive)
                return;

            float timeout = _activeDefinition.resetTimeout * ResolveWindowMultiplier();
            if (Time.unscaledTime - _lastInputAt > timeout)
                ResetCombo();
        }

        public void SetActiveClass(MuClassId classId)
        {
            _activeClass = classId;
            ResolveActiveDefinition();
            ResetCombo();
        }

        public bool RegisterSkillExecution(int skillId)
        {
            if (_activeDefinition == null || _activeDefinition.steps == null || _activeDefinition.steps.Length == 0)
                return false;

            if (_nextStepIndex >= _activeDefinition.steps.Length)
                _nextStepIndex = 0;

            ComboStep step = _activeDefinition.steps[_nextStepIndex];

            // First step can start anytime.
            if (_nextStepIndex == 0)
            {
                if (skillId != step.requiredSkillId)
                    return false;

                StartCombo();
                AdvanceStep();
                return true;
            }

            // Validate skill + input window in unscaled time (frame-rate independent).
            float dt = Time.unscaledTime - _lastInputAt;
            float minW = step.minInputWindow * ResolveWindowMultiplier();
            float maxW = step.maxInputWindow * ResolveWindowMultiplier();
            if (skillId != step.requiredSkillId || dt < minW || dt > maxW)
            {
                ResetCombo();
                return false;
            }

            AdvanceStep();
            return true;
        }

        private void StartCombo()
        {
            _comboActive = true;
            _currentBonus = 1f;
            _lastInputAt = Time.unscaledTime;
        }

        private void AdvanceStep()
        {
            _lastInputAt = Time.unscaledTime;
            int step = _nextStepIndex;
            _nextStepIndex++;

            OnComboProgressed?.Invoke(step, _activeDefinition.steps.Length);

            if (_nextStepIndex < _activeDefinition.steps.Length)
                return;

            // Final step bonus/effect.
            _currentBonus = Mathf.Max(1f, _activeDefinition.finalStepBonusMultiplier);
            OnComboFinalBonusApplied?.Invoke(_currentBonus);

            // Keep combo active briefly until timeout or next incorrect input.
            _nextStepIndex = 0;
        }

        private void ResetCombo()
        {
            _comboActive = false;
            _nextStepIndex = 0;
            _currentBonus = 1f;
            OnComboReset?.Invoke();
        }

        private int ResolveNextSkillId()
        {
            if (_activeDefinition == null || _activeDefinition.steps == null || _activeDefinition.steps.Length == 0)
                return 0;

            int index = Mathf.Clamp(_nextStepIndex, 0, _activeDefinition.steps.Length - 1);
            return _activeDefinition.steps[index].requiredSkillId;
        }

        private void ResolveActiveDefinition()
        {
            _activeDefinition = null;
            if (_definitions == null)
                return;

            for (int i = 0; i < _definitions.Length; i++)
            {
                ComboDefinition def = _definitions[i];
                if (def == null || def.classId != _activeClass)
                    continue;

                _activeDefinition = def;
                break;
            }
        }

        private float ResolveWindowMultiplier()
        {
            return _tuning != null ? Mathf.Max(0.5f, _tuning.comboWindowMultiplier) : 1f;
        }
    }
}
