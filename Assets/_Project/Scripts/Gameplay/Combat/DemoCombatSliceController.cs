using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Gameplay.Combat
{
    /// <summary>
    /// Local combat bridge for demo scenes.
    /// Applies damage to DemoMobRuntime and triggers combat feedback controllers.
    /// </summary>
    public sealed class DemoCombatSliceController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CombatController _combatController;
        [SerializeField] private TargetingController _targetingController;
        [SerializeField] private CombatFeedbackController _combatFeedback;
        [SerializeField] private FloatingDamageController _floatingDamage;
        [SerializeField] private DemoLootSpawner _lootSpawner;

        [Header("Damage")]
        [SerializeField] private Vector2Int _basicDamageRange = new(14, 26);
        [SerializeField] private Vector2Int _skillDamageRange = new(28, 48);
        [SerializeField, Range(0f, 1f)] private float _critChance = 0.2f;
        [SerializeField] private float _critMultiplier = 1.6f;

        [Header("Loot")]
        [SerializeField] private bool _spawnMockLootOnKill = true;
        [SerializeField] private int _mockLootItemId = 1001;
        [SerializeField] private string _mockLootName = "Zen Bundle";

        private void Awake()
        {
            if (_combatController == null)
                _combatController = FindAnyObjectByType<CombatController>();

            if (_targetingController == null)
                _targetingController = FindAnyObjectByType<TargetingController>();

            if (_combatFeedback == null)
                _combatFeedback = FindAnyObjectByType<CombatFeedbackController>();

            if (_floatingDamage == null)
                _floatingDamage = FindAnyObjectByType<FloatingDamageController>();

            if (_lootSpawner == null)
                _lootSpawner = FindAnyObjectByType<DemoLootSpawner>();
        }

        private void OnEnable()
        {
            if (_combatController != null)
                _combatController.OnCombatFeedbackRequested += HandleCombatFeedbackRequested;
        }

        private void OnDisable()
        {
            if (_combatController != null)
                _combatController.OnCombatFeedbackRequested -= HandleCombatFeedbackRequested;
        }

        private void HandleCombatFeedbackRequested(CombatFeedbackEvent feedback)
        {
            if (feedback.Type != CombatFeedbackType.BasicAttackStarted
                && feedback.Type != CombatFeedbackType.SkillCastStarted)
            {
                return;
            }

            DemoMobRuntime mob = ResolveCurrentMobTarget();
            if (mob == null || mob.IsDead)
                return;

            bool isCrit = Random.value <= Mathf.Clamp01(_critChance);
            int damage = RollDamage(feedback.Type == CombatFeedbackType.BasicAttackStarted, isCrit);

            bool applied = mob.TryApplyDamage(damage, out bool killed);
            if (!applied)
                return;

            Vector3 hitPosition = mob.transform.position + Vector3.up * 1.35f;
            if (isCrit)
                _combatFeedback?.PlayCrit(hitPosition);
            else
                _combatFeedback?.PlayHit(hitPosition);

            _floatingDamage?.ShowDamage(hitPosition, damage, isCrit);

            if (!killed)
                return;

            if (_targetingController != null && _targetingController.CurrentTarget == mob.GetComponent<EntityView>())
                _targetingController.ReleaseTarget();

            if (_spawnMockLootOnKill)
                _lootSpawner?.SpawnMockLoot(mob.transform.position + Vector3.up * 0.2f, _mockLootItemId, _mockLootName);
        }

        private DemoMobRuntime ResolveCurrentMobTarget()
        {
            if (_targetingController == null || _targetingController.CurrentTarget == null)
                return null;

            return _targetingController.CurrentTarget.GetComponent<DemoMobRuntime>();
        }

        private int RollDamage(bool isBasic, bool isCrit)
        {
            Vector2Int range = isBasic ? _basicDamageRange : _skillDamageRange;
            int min = Mathf.Max(1, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            int damage = Random.Range(min, max + 1);
            if (isCrit)
                damage = Mathf.RoundToInt(damage * Mathf.Max(1f, _critMultiplier));

            return damage;
        }
    }
}
