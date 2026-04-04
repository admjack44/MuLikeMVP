using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Validates and initiates skill casts. Checks range, cooldowns and sends the request to the server.
    /// </summary>
    public class SkillCastController : MonoBehaviour
    {
        [SerializeField] private TargetingController _targeting;
        [SerializeField] private MuLike.Gameplay.Combat.CombatController _combatController;

        private float[] _cooldowns = new float[0];

        public void Initialize(int skillCount)
        {
            _cooldowns = new float[skillCount];
        }

        public bool TryCastSkill(int skillIndex, float range, float cooldown)
        {
            if (_combatController != null)
            {
                return _combatController.TryCastSkillByIndex(skillIndex);
            }

            if (skillIndex < 0 || skillIndex >= _cooldowns.Length) return false;
            if (_cooldowns[skillIndex] > 0f) return false;
            if (_targeting.CurrentTarget == null) return false;

            float dist = Vector3.Distance(transform.position, _targeting.CurrentTarget.transform.position);
            if (dist > range) return false;

            StartCoroutine(RunCooldown(skillIndex, cooldown));
            SendCastRequest(skillIndex, _targeting.CurrentTarget.EntityId);
            return true;
        }

        private void SendCastRequest(int skillIndex, int targetEntityId)
        {
            // TODO: use ClientMessageFactory.CreateSkillCastRequest and send via NetworkClient
            Debug.Log($"[SkillCast] Cast skill {skillIndex} on entity {targetEntityId}");
        }

        private IEnumerator RunCooldown(int skillIndex, float duration)
        {
            _cooldowns[skillIndex] = duration;
            while (_cooldowns[skillIndex] > 0f)
            {
                yield return null;
                _cooldowns[skillIndex] -= Time.deltaTime;
            }
            _cooldowns[skillIndex] = 0f;
        }

        private void Update()
        {
            // Skill hotkeys: 1-9
            for (int i = 0; i < Mathf.Min(_cooldowns.Length, 9); i++)
            {
                if (WasSkillHotkeyPressed(i))
                    TryCastSkill(i, 10f, 1.5f);
            }
        }

        private static bool WasSkillHotkeyPressed(int index)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return index switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame,
                2 => keyboard.digit3Key.wasPressedThisFrame,
                3 => keyboard.digit4Key.wasPressedThisFrame,
                4 => keyboard.digit5Key.wasPressedThisFrame,
                5 => keyboard.digit6Key.wasPressedThisFrame,
                6 => keyboard.digit7Key.wasPressedThisFrame,
                7 => keyboard.digit8Key.wasPressedThisFrame,
                8 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetKeyDown(KeyCode.Alpha1 + index);
#endif
        }
    }
}
