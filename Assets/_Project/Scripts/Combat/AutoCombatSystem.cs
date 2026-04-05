using System;
using System.Collections.Generic;
using MuLike.Core;
using MuLike.Data.Catalogs;
using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.Systems;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.Combat
{
    /// <summary>
    /// MU Immortal / MU Dragon Havoc-style auto-combat controller.
    ///
    /// Responsibilities:
    /// 1) Auto-combat core: NavMesh + mobile A* route solver, target scan/prioritization, farming zone clamp.
    /// 2) Combat intelligence: skill combo, mana/cooldown checks, auto-potions, low-HP retreat behavior.
    /// 3) Revive lifecycle: in-spot revive via configured resurrection items, city fallback.
    /// 4) Mobile safeguards: battery-aware pause, dynamic quality fallback when FPS drops.
    /// 5) Offline farming hook: emits claim request on resume (server-authoritative integration point).
    ///
    /// Notes:
    /// - This component orchestrates existing systems (`CombatController`, `TargetingController`, `CharacterMotor`).
    /// - Server authority is preserved. Combat cast still flows through `CombatController` validations.
    /// - Offline farming progression MUST be validated by backend. This script only emits the elapsed time signal.
    /// </summary>
    public sealed class AutoCombatSystem : MonoBehaviour
    {
        public enum DetectionShape
        {
            Circular,
            FrontalCone
        }

        public enum ReviveMode
        {
            Spot,
            City
        }

        [Serializable]
        public struct FarmingZone
        {
            public bool enabled;
            public Vector3 center;
            public Vector2 size;

            public bool Contains(Vector3 worldPos)
            {
                if (!enabled)
                    return true;

                Vector3 local = worldPos - center;
                float halfX = Mathf.Max(0.5f, size.x * 0.5f);
                float halfZ = Mathf.Max(0.5f, size.y * 0.5f);
                return Mathf.Abs(local.x) <= halfX && Mathf.Abs(local.z) <= halfZ;
            }

            public Vector3 Clamp(Vector3 worldPos)
            {
                if (!enabled)
                    return worldPos;

                float halfX = Mathf.Max(0.5f, size.x * 0.5f);
                float halfZ = Mathf.Max(0.5f, size.y * 0.5f);
                Vector3 local = worldPos - center;
                local.x = Mathf.Clamp(local.x, -halfX, halfX);
                local.z = Mathf.Clamp(local.z, -halfZ, halfZ);
                return center + local;
            }
        }

        [Serializable]
        public struct ClassCombo
        {
            public StatsClientSystem.CharacterClass characterClass;
            public int[] skillSequence;
        }

        [Header("Dependencies")]
        [SerializeField] private CombatController _combat;
        [SerializeField] private TargetingController _targeting;
        [SerializeField] private CharacterMotor _motor;
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField] private StatsClientSystem _stats;
        [SerializeField] private InventoryClientSystem _inventory;

        [Header("Activation")]
        [SerializeField] private bool _enabledByDefault = false;
        [SerializeField] private bool _pauseWhenBatteryLow = true;
        [SerializeField, Range(0.05f, 1f)] private float _batteryPauseThreshold = 0.20f;

        [Header("Detection")]
        [SerializeField] private DetectionShape _detectionShape = DetectionShape.FrontalCone;
        [SerializeField] private LayerMask _monsterLayer = ~0;
        [SerializeField, Min(1f)] private float _detectRange = 16f;
        [SerializeField, Range(10f, 180f)] private float _frontalAngle = 120f;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Target Priority")]
        [SerializeField] private bool _preferElite = true;
        [SerializeField] private bool _preferDistance = true;

        [Header("Farming Zone")]
        [SerializeField] private FarmingZone _farmingZone = new()
        {
            enabled = false,
            center = Vector3.zero,
            size = new Vector2(40f, 40f)
        };

        [Header("Pathfinding")]
        [SerializeField] private bool _useAStarOverNavMesh = true;
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.25f;
        [SerializeField, Min(1)] private int _astarMaxNodes = 192;
        [SerializeField, Min(4)] private int _astarNeighborsPerNode = 8;

        [Header("Combat Intelligence")]
        [SerializeField, Min(0.03f)] private float _decisionInterval = 0.12f;
        [SerializeField, Min(0f)] private float _attackStopDistanceBuffer = 0.30f;
        [SerializeField] private ClassCombo[] _classCombos = Array.Empty<ClassCombo>();
        [SerializeField, Range(0.05f, 1f)] private float _hpPotionThreshold = 0.30f;
        [SerializeField, Range(0.05f, 1f)] private float _mpPotionThreshold = 0.30f;
        [SerializeField, Range(0.01f, 0.5f)] private float _retreatHpThreshold = 0.15f;
        [SerializeField, Min(0.2f)] private float _retreatDistance = 6f;

        [Header("Revive")]
        [SerializeField] private bool _autoReviveEnabled = true;
        [SerializeField] private bool _reviveInCityIfNoItem = true;
        [SerializeField] private int[] _resurrectionItemIds = { 9001 };
        [SerializeField] private Vector3 _cityRevivePoint = new(125f, 0f, 125f);

        [Header("Performance Safeguards")]
        [SerializeField] private bool _autoReduceQualityOnLowFps = true;
        [SerializeField, Range(15f, 40f)] private float _fpsThreshold = 25f;
        [SerializeField, Min(0.5f)] private float _fpsWindowSeconds = 2f;
        [SerializeField, Min(1f)] private float _qualityStepCooldown = 10f;

        [Header("Offline Farming Hook")]
        [SerializeField] private bool _emitOfflineFarmingClaimOnResume = true;
        [SerializeField, Min(30f)] private float _minOfflineSecondsToClaim = 60f;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogs = false;

        private const string OfflineTsPlayerPrefsKey = "mulike.offline.lastUtcTicks";

        private readonly Collider[] _scanBuffer = new Collider[96];
        private readonly List<EntityView> _candidateMonsters = new(96);
        private readonly Dictionary<int, float> _localSkillCooldownEnd = new();
        private readonly List<Vector3> _activeRoute = new(64);

        private EntityView _currentTarget;
        private bool _isEnabled;
        private float _nextDecisionAt;
        private float _nextRepathAt;
        private int _comboCursor;
        private float _fpsAccumulator;
        private int _fpsFrames;
        private float _nextQualityChangeAt;
        private bool _isInBackground;

        public bool IsEnabled => _isEnabled;

        public event Action<bool> OnEnabledChanged;
        public event Action<ReviveMode> OnReviveRequested;
        public event Action<string> OnBackgroundDeathNotificationRequested;
        public event Action<double> OnOfflineFarmingClaimRequested;
        public event Action<string> OnAutoCombatLog;

        private void Awake()
        {
            if (_combat == null)
                _combat = FindAnyObjectByType<CombatController>();
            if (_targeting == null)
                _targeting = FindAnyObjectByType<TargetingController>();
            if (_motor == null)
                _motor = FindAnyObjectByType<CharacterMotor>();
            if (_navMeshAgent == null)
                _navMeshAgent = GetComponent<NavMeshAgent>();

            if (_stats == null)
                _stats = GameContext.StatsClientSystem;
            if (_inventory == null)
                _inventory = GameContext.InventoryClientSystem;

            if (_navMeshAgent != null)
                _navMeshAgent.updateRotation = false;
        }

        private void Start()
        {
            SetEnabled(_enabledByDefault);
        }

        private void Update()
        {
            if (!_isEnabled)
                return;

            if (ShouldPauseForBattery())
            {
                Emit("Paused: battery low.");
                return;
            }

            if (_stats != null)
            {
                bool isDead = _stats.Snapshot.Resources.Hp.Current <= 0;
                if (isDead)
                {
                    HandleDeathLifecycle();
                    return;
                }
            }

            UpdateFpsSafeguard();

            if (Time.unscaledTime >= _nextDecisionAt)
            {
                _nextDecisionAt = Time.unscaledTime + Mathf.Max(0.03f, _decisionInterval);
                TickDecision();
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (_isEnabled == enabled)
                return;

            _isEnabled = enabled;
            if (!enabled)
            {
                _currentTarget = null;
                _activeRoute.Clear();
                _motor?.Stop();
            }

            OnEnabledChanged?.Invoke(_isEnabled);
            Emit($"Enabled={_isEnabled}");
        }

        private void TickDecision()
        {
            if (_combat == null || _targeting == null || _motor == null)
            {
                Emit("Missing dependencies. Auto-combat suspended.");
                return;
            }

            TryAutoUsePotions();

            if (ShouldRetreat())
            {
                ExecuteRetreat();
                return;
            }

            _currentTarget = AcquireBestTarget();
            if (_currentTarget == null)
            {
                _targeting.ReleaseTarget();
                return;
            }

            if (_targeting.CurrentTarget != _currentTarget)
                _targeting.SetManualTarget(_currentTarget);

            float attackRange = ResolveDesiredAttackRange();
            float distance = Vector3.Distance(transform.position, _currentTarget.transform.position);

            if (distance > attackRange + _attackStopDistanceBuffer)
            {
                NavigateTo(_currentTarget.transform.position);
                return;
            }

            _motor.Stop();
            TryExecuteComboOrFallbackBasic();
        }

        private EntityView AcquireBestTarget()
        {
            _candidateMonsters.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _detectRange,
                _scanBuffer,
                _monsterLayer,
                _triggerInteraction);

            Vector3 forward = transform.forward;
            Vector3 origin = transform.position;

            for (int i = 0; i < count; i++)
            {
                Collider c = _scanBuffer[i];
                if (c == null)
                    continue;

                EntityView entity = c.GetComponentInParent<EntityView>();
                if (entity == null || !entity.isActiveAndEnabled)
                    continue;

                if (_farmingZone.enabled && !_farmingZone.Contains(entity.transform.position))
                    continue;

                if (_detectionShape == DetectionShape.FrontalCone)
                {
                    Vector3 to = entity.transform.position - origin;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f)
                    {
                        float angle = Vector3.Angle(forward, to.normalized);
                        if (angle > _frontalAngle * 0.5f)
                            continue;
                    }
                }

                _candidateMonsters.Add(entity);
            }

            if (_candidateMonsters.Count == 0)
                return null;

            _candidateMonsters.Sort(CompareByPriority);
            return _candidateMonsters[0];
        }

        private int CompareByPriority(EntityView a, EntityView b)
        {
            bool eliteA = IsElite(a);
            bool eliteB = IsElite(b);

            if (_preferElite && eliteA != eliteB)
                return eliteA ? -1 : 1;

            if (_preferDistance)
            {
                float da = (a.transform.position - transform.position).sqrMagnitude;
                float db = (b.transform.position - transform.position).sqrMagnitude;
                return da.CompareTo(db);
            }

            return 0;
        }

        private static bool IsElite(EntityView view)
        {
            MuLike.AI.MuMonsterAI ai = view != null ? view.GetComponent<MuLike.AI.MuMonsterAI>() : null;
            return ai != null && ai.Rank == MuLike.AI.MuMonsterAI.MonsterRank.Elite;
        }

        private void NavigateTo(Vector3 destination)
        {
            Vector3 clamped = _farmingZone.Clamp(destination);

            if (Time.unscaledTime < _nextRepathAt)
                return;

            _nextRepathAt = Time.unscaledTime + _repathInterval;

            _activeRoute.Clear();
            if (_useAStarOverNavMesh)
            {
                MobileAStarNavMeshPathfinder.FindPath(
                    transform.position,
                    clamped,
                    _astarMaxNodes,
                    _astarNeighborsPerNode,
                    _activeRoute);
            }

            if (_activeRoute.Count == 0)
            {
                _motor.MoveToPoint(clamped);
                return;
            }

            Vector3 next = _activeRoute[0];
            if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.SetDestination(next);
                return;
            }

            _motor.MoveToPoint(next);
        }

        private float ResolveDesiredAttackRange()
        {
            if (_combat == null)
                return 2.5f;

            CombatSkillModel next = ResolveNextComboSkill();
            if (next.IsValid)
                return Mathf.Max(1f, next.Range);

            return Mathf.Max(1f, _combat.BasicAttackRange);
        }

        private void TryExecuteComboOrFallbackBasic()
        {
            if (_combat == null)
                return;

            CombatSkillModel comboSkill = ResolveNextComboSkill();
            if (comboSkill.IsValid
                && comboSkill.AllowAutoCast
                && IsSkillReady(comboSkill.SkillId, comboSkill.Cooldown)
                && _combat.CanCastSkillById(comboSkill.SkillId, out _)
                && HasManaFor(comboSkill.ManaCost))
            {
                if (_combat.TryCastSkillById(comboSkill.SkillId))
                {
                    _comboCursor++;
                    RegisterLocalSkillCooldown(comboSkill.SkillId, comboSkill.Cooldown);
                    return;
                }
            }

            if (_combat.CanCastSkillById(_combat.BasicAttackSkillId, out _))
                _combat.TryBasicAttack();
        }

        private CombatSkillModel ResolveNextComboSkill()
        {
            if (_combat == null || _combat.SkillCount <= 0)
                return default;

            int[] sequence = ResolveComboSequence();
            if (sequence == null || sequence.Length == 0)
                return default;

            int index = _comboCursor % sequence.Length;
            int wantedSkillId = sequence[index];

            for (int i = 0; i < _combat.SkillCount; i++)
            {
                if (!_combat.TryGetSkillModel(i, out CombatSkillModel skill))
                    continue;

                if (skill.SkillId == wantedSkillId)
                    return skill;
            }

            return default;
        }

        private int[] ResolveComboSequence()
        {
            if (_stats == null || _classCombos == null)
                return null;

            StatsClientSystem.CharacterClass currentClass = _stats.Snapshot.Primary.Class;
            for (int i = 0; i < _classCombos.Length; i++)
            {
                if (_classCombos[i].characterClass != currentClass)
                    continue;

                return _classCombos[i].skillSequence;
            }

            return null;
        }

        private bool IsSkillReady(int skillId, float cooldown)
        {
            if (_combat != null && _combat.GetRemainingCooldown(skillId) > 0.01f)
                return false;

            if (_localSkillCooldownEnd.TryGetValue(skillId, out float endAt) && Time.time < endAt)
                return false;

            if (cooldown > 0f && !_localSkillCooldownEnd.ContainsKey(skillId))
                return true;

            return true;
        }

        private void RegisterLocalSkillCooldown(int skillId, float cooldown)
        {
            _localSkillCooldownEnd[skillId] = Time.time + Mathf.Max(0f, cooldown);
        }

        private bool HasManaFor(int manaCost)
        {
            if (_stats == null)
                return true;

            int currentMana = _stats.Snapshot.Resources.Mana.Current;
            return currentMana >= Mathf.Max(0, manaCost);
        }

        private void TryAutoUsePotions()
        {
            if (_stats == null || _inventory == null || GameContext.CatalogResolver == null)
                return;

            StatsClientSystem.ResourceStats resources = _stats.Snapshot.Resources;
            float hpRatio = Ratio(resources.Hp.Current, resources.Hp.Max);
            float mpRatio = Ratio(resources.Mana.Current, resources.Mana.Max);

            if (hpRatio < _hpPotionThreshold)
                TryConsumePotion(preferHp: true);

            if (mpRatio < _mpPotionThreshold)
                TryConsumePotion(preferHp: false);
        }

        private bool TryConsumePotion(bool preferHp)
        {
            CatalogResolver resolver = GameContext.CatalogResolver;
            if (resolver == null || _inventory == null || _stats == null)
                return false;

            int bestSlot = -1;
            ItemDefinition bestItem = null;

            IReadOnlyList<InventoryClientSystem.InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!resolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition item) || item == null)
                    continue;

                if (item.Category != ItemCategory.Consumable || item.Restore.IsEmpty)
                    continue;

                int score = preferHp ? item.Restore.Hp : item.Restore.Mana;
                if (score <= 0)
                    continue;

                if (bestItem == null || score > (preferHp ? bestItem.Restore.Hp : bestItem.Restore.Mana))
                {
                    bestItem = item;
                    bestSlot = slot.SlotIndex;
                }
            }

            if (bestItem == null || bestSlot < 0)
                return false;

            if (!_inventory.TryConsumeFromSlot(bestSlot, 1, out _, out _))
                return false;

            StatsClientSystem.PlayerStatsSnapshot snapshot = _stats.Snapshot;
            _stats.ApplyDelta(new StatsClientSystem.PlayerStatsDelta
            {
                HasHp = true,
                HpCurrent = snapshot.Resources.Hp.Current + Mathf.Max(0, bestItem.Restore.Hp),
                HpMax = snapshot.Resources.Hp.Max,
                HasMana = true,
                ManaCurrent = snapshot.Resources.Mana.Current + Mathf.Max(0, bestItem.Restore.Mana),
                ManaMax = snapshot.Resources.Mana.Max
            });

            return true;
        }

        private bool ShouldRetreat()
        {
            if (_stats == null)
                return false;

            float hpRatio = Ratio(_stats.Snapshot.Resources.Hp.Current, _stats.Snapshot.Resources.Hp.Max);
            if (hpRatio >= _retreatHpThreshold)
                return false;

            // Retreat only when we are out of restorative options.
            bool hasAnyPotion = HasRestorativeConsumable();
            return !hasAnyPotion;
        }

        private void ExecuteRetreat()
        {
            Vector3 retreatTarget;
            if (_currentTarget != null)
            {
                Vector3 away = (transform.position - _currentTarget.transform.position).normalized;
                away.y = 0f;
                if (away.sqrMagnitude <= 0.0001f)
                    away = -transform.forward;

                retreatTarget = transform.position + away.normalized * _retreatDistance;
            }
            else
            {
                retreatTarget = transform.position - transform.forward * _retreatDistance;
            }

            retreatTarget = _farmingZone.enabled ? _farmingZone.Clamp(retreatTarget) : retreatTarget;
            NavigateTo(retreatTarget);
            Emit("Retreating: low HP and no potions available.");
        }

        private bool HasRestorativeConsumable()
        {
            if (_inventory == null || GameContext.CatalogResolver == null)
                return false;

            IReadOnlyList<InventoryClientSystem.InventorySlot> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventoryClientSystem.InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                    continue;

                if (!GameContext.CatalogResolver.TryGetItemDefinition(slot.ItemId, out ItemDefinition item) || item == null)
                    continue;

                if (item.Category == ItemCategory.Consumable && !item.Restore.IsEmpty)
                    return true;
            }

            return false;
        }

        private void HandleDeathLifecycle()
        {
            _motor?.Stop();

            if (!_autoReviveEnabled)
                return;

            bool usedItem = TryConsumeResurrectionItem();
            if (usedItem)
            {
                OnReviveRequested?.Invoke(ReviveMode.Spot);
                Emit("Auto-revive requested at spot.");
                return;
            }

            if (_reviveInCityIfNoItem)
            {
                OnReviveRequested?.Invoke(ReviveMode.City);
                transform.position = _cityRevivePoint;
                Emit("No resurrection item. Revive in city requested.");
            }

            if (_isInBackground)
                OnBackgroundDeathNotificationRequested?.Invoke("Tu personaje murió durante auto-combat en segundo plano.");
        }

        private bool TryConsumeResurrectionItem()
        {
            if (_inventory == null || _resurrectionItemIds == null || _resurrectionItemIds.Length == 0)
                return false;

            for (int i = 0; i < _resurrectionItemIds.Length; i++)
            {
                int itemId = _resurrectionItemIds[i];
                if (!_inventory.TryFindFirstByItemId(itemId, out InventoryClientSystem.InventorySlot slot) || slot.IsEmpty)
                    continue;

                if (_inventory.TryConsumeFromSlot(slot.SlotIndex, 1, out _, out _))
                    return true;
            }

            return false;
        }

        private bool ShouldPauseForBattery()
        {
            if (!_pauseWhenBatteryLow)
                return false;

            float battery = SystemInfo.batteryLevel;
            if (battery < 0f)
                return false; // unknown battery on this platform

            return battery <= _batteryPauseThreshold;
        }

        private void UpdateFpsSafeguard()
        {
            if (!_autoReduceQualityOnLowFps)
                return;

            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccumulator < _fpsWindowSeconds)
                return;

            float fps = _fpsFrames / Mathf.Max(0.0001f, _fpsAccumulator);
            _fpsAccumulator = 0f;
            _fpsFrames = 0;

            if (fps >= _fpsThreshold)
                return;

            if (Time.unscaledTime < _nextQualityChangeAt)
                return;

            _nextQualityChangeAt = Time.unscaledTime + _qualityStepCooldown;
            if (QualitySettings.GetQualityLevel() > 0)
            {
                QualitySettings.DecreaseLevel(applyExpensiveChanges: true);
                Emit($"FPS {fps:F1} < {_fpsThreshold:F1}. Quality reduced to level {QualitySettings.GetQualityLevel()}.");
            }
        }

        private void OnApplicationPause(bool paused)
        {
            _isInBackground = paused;

            if (paused)
            {
                SaveOfflineTimestamp();
                return;
            }

            if (_emitOfflineFarmingClaimOnResume)
                EmitOfflineClaimIfNeeded();
        }

        private void OnApplicationQuit()
        {
            SaveOfflineTimestamp();
        }

        private void SaveOfflineTimestamp()
        {
            PlayerPrefs.SetString(OfflineTsPlayerPrefsKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }

        private void EmitOfflineClaimIfNeeded()
        {
            if (!PlayerPrefs.HasKey(OfflineTsPlayerPrefsKey))
                return;

            string raw = PlayerPrefs.GetString(OfflineTsPlayerPrefsKey, string.Empty);
            if (!long.TryParse(raw, out long ticks))
                return;

            DateTime then = new(ticks, DateTimeKind.Utc);
            double elapsed = Math.Max(0d, (DateTime.UtcNow - then).TotalSeconds);
            if (elapsed < _minOfflineSecondsToClaim)
                return;

            OnOfflineFarmingClaimRequested?.Invoke(elapsed);
            Emit($"Offline farming claim requested: {elapsed:F0}s.");
        }

        private static float Ratio(int current, int max)
        {
            if (max <= 0)
                return 0f;

            return Mathf.Clamp01(current / (float)max);
        }

        private void Emit(string msg)
        {
            if (!_verboseLogs)
                return;

            string line = $"[AutoCombatSystem] {msg}";
            Debug.Log(line);
            OnAutoCombatLog?.Invoke(line);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_farmingZone.enabled)
                return;

            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.55f);
            Vector3 size = new(_farmingZone.size.x, 0.1f, _farmingZone.size.y);
            Gizmos.DrawWireCube(_farmingZone.center, size);

            Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _detectRange);
        }
#endif

        /// <summary>
        /// Lightweight A* pathfinder that runs on top of sampled NavMesh vertices.
        /// Falls back gracefully to `NavMesh.CalculatePath` if graph search cannot build.
        ///
        /// Mobile design:
        /// - Max node cap, nearest-neighbor cap, and small temporary allocations.
        /// - Suitable for periodic repath ticks; not intended for hundreds of agents per frame.
        /// </summary>
        private static class MobileAStarNavMeshPathfinder
        {
            private struct Node
            {
                public Vector3 position;
                public int parent;
                public float g;
                public float f;
                public bool closed;
            }

            private static readonly List<Vector3> s_vertexPool = new(1024);
            private static readonly List<Node> s_nodes = new(256);

            public static void FindPath(
                Vector3 from,
                Vector3 to,
                int maxNodes,
                int neighbors,
                List<Vector3> outPath)
            {
                outPath.Clear();

                if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, 3f, NavMesh.AllAreas))
                    return;

                if (!NavMesh.SamplePosition(to, out NavMeshHit toHit, 4f, NavMesh.AllAreas))
                    return;

                BuildNodePool(fromHit.position, toHit.position, maxNodes);
                if (s_vertexPool.Count < 2)
                {
                    AppendNavMeshPathCorners(fromHit.position, toHit.position, outPath);
                    return;
                }

                s_nodes.Clear();
                for (int i = 0; i < s_vertexPool.Count; i++)
                {
                    s_nodes.Add(new Node
                    {
                        position = s_vertexPool[i],
                        parent = -1,
                        g = float.MaxValue,
                        f = float.MaxValue,
                        closed = false
                    });
                }

                int start = 0;
                int goal = 1;
                Node n0 = s_nodes[start];
                n0.g = 0f;
                n0.f = Heuristic(n0.position, s_nodes[goal].position);
                s_nodes[start] = n0;

                List<int> open = new() { start };

                while (open.Count > 0)
                {
                    int current = PopLowest(open);
                    Node c = s_nodes[current];
                    c.closed = true;
                    s_nodes[current] = c;

                    if (current == goal)
                    {
                        Reconstruct(goal, outPath);
                        return;
                    }

                    AppendNeighbors(current, neighbors, open, goal);
                }

                AppendNavMeshPathCorners(fromHit.position, toHit.position, outPath);
            }

            private static void BuildNodePool(Vector3 start, Vector3 goal, int maxNodes)
            {
                s_vertexPool.Clear();
                s_vertexPool.Add(start);
                s_vertexPool.Add(goal);

                NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
                if (tri.vertices == null || tri.vertices.Length == 0)
                    return;

                Vector3 mid = (start + goal) * 0.5f;
                float radius = Mathf.Clamp(Vector3.Distance(start, goal) * 0.8f + 12f, 12f, 80f);
                float sqr = radius * radius;

                int step = Mathf.Max(1, tri.vertices.Length / Mathf.Max(16, maxNodes));
                for (int i = 0; i < tri.vertices.Length && s_vertexPool.Count < Mathf.Max(8, maxNodes); i += step)
                {
                    Vector3 v = tri.vertices[i];
                    if ((v - mid).sqrMagnitude > sqr)
                        continue;

                    if (!NavMesh.SamplePosition(v, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                        continue;

                    s_vertexPool.Add(hit.position);
                }
            }

            private static int PopLowest(List<int> open)
            {
                int bestIdx = 0;
                float bestF = s_nodes[open[0]].f;

                for (int i = 1; i < open.Count; i++)
                {
                    float f = s_nodes[open[i]].f;
                    if (f < bestF)
                    {
                        bestF = f;
                        bestIdx = i;
                    }
                }

                int id = open[bestIdx];
                open.RemoveAt(bestIdx);
                return id;
            }

            private static void AppendNeighbors(int current, int maxNeighbors, List<int> open, int goal)
            {
                Vector3 cp = s_nodes[current].position;

                List<(int idx, float dist)> nearest = new(maxNeighbors + 2);
                for (int i = 0; i < s_nodes.Count; i++)
                {
                    if (i == current)
                        continue;

                    float d = Vector3.SqrMagnitude(s_nodes[i].position - cp);
                    nearest.Add((i, d));
                }

                nearest.Sort((a, b) => a.dist.CompareTo(b.dist));
                int count = Mathf.Min(maxNeighbors, nearest.Count);

                for (int i = 0; i < count; i++)
                {
                    int ni = nearest[i].idx;
                    Node n = s_nodes[ni];
                    if (n.closed)
                        continue;

                    if (NavMesh.Raycast(cp, n.position, out _, NavMesh.AllAreas))
                        continue;

                    float candidateG = s_nodes[current].g + Mathf.Sqrt(nearest[i].dist);
                    if (candidateG >= n.g)
                        continue;

                    n.parent = current;
                    n.g = candidateG;
                    n.f = candidateG + Heuristic(n.position, s_nodes[goal].position);
                    s_nodes[ni] = n;

                    if (!open.Contains(ni))
                        open.Add(ni);
                }
            }

            private static void Reconstruct(int goal, List<Vector3> outPath)
            {
                List<Vector3> reversed = new(32);
                int cursor = goal;
                int guard = 0;
                while (cursor >= 0 && guard++ < 1024)
                {
                    reversed.Add(s_nodes[cursor].position);
                    cursor = s_nodes[cursor].parent;
                }

                for (int i = reversed.Count - 1; i >= 0; i--)
                    outPath.Add(reversed[i]);
            }

            private static float Heuristic(Vector3 a, Vector3 b)
            {
                return Vector3.Distance(a, b);
            }

            private static void AppendNavMeshPathCorners(Vector3 from, Vector3 to, List<Vector3> outPath)
            {
                NavMeshPath path = new();
                if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.corners == null)
                    return;

                for (int i = 0; i < path.corners.Length; i++)
                    outPath.Add(path.corners[i]);
            }
        }
    }
}
