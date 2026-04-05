using System;
using System.Collections.Generic;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Entity Interest Management system for MU Online Mobile.
    ///
    /// Determines which networked entities the local client should receive updates for,
    /// and at what frequency — reducing bandwidth and CPU on mid-range Android devices.
    ///
    /// Interest levels by distance to local player:
    ///   Near    ( 0 – 30 m)  → 20 Hz, full state sync
    ///   Mid     (30 – 80 m)  → 10 Hz, position + HP
    ///   Far     (80 – 150m)  → 5 Hz,  position only
    ///   OutOfRange (>150 m)  → excluded from sync entirely
    ///
    /// City congestion mode:
    ///   When tracked entities in "Near + Mid" zones exceed <see cref="_cityModeThreshold"/>,
    ///   Mid entities are downgraded to CityOnly — their 3D model is hidden and only their
    ///   name tag is shown. This is the classic MU Online approach to town performance.
    ///
    /// Integration:
    ///   1. Add to a persistent manager GameObject in each scene.
    ///   2. Call <see cref="RegisterEntity"/> when a new entity is spawned/synced.
    ///   3. Call <see cref="UnregisterEntity"/> on despawn.
    ///   4. Before applying a snapshot or calling SendAsync for that entity, check
    ///      <see cref="CanSyncNow"/> and <see cref="IsInInterest"/>.
    ///   5. Subscribe to <see cref="OnInterestLevelChanged"/> to toggle renderers/name tags.
    /// </summary>
    public sealed class InterestManager : MonoBehaviour
    {
        // ── Interest levels ────────────────────────────────────────────────────────

        public enum InterestLevel
        {
            /// <summary>Entity fully synced at 20 Hz.</summary>
            Near      = 0,
            /// <summary>Entity synced at 10 Hz (position + HP).</summary>
            Mid       = 1,
            /// <summary>Entity synced at 5 Hz (position only).</summary>
            Far       = 2,
            /// <summary>Entity in congested city: only name tag shown, body hidden.</summary>
            CityOnly  = 3,
            /// <summary>Beyond max range — no sync, no rendering.</summary>
            OutOfRange = 4
        }

        // ── Inspector ──────────────────────────────────────────────────────────────

        [Header("Distance Thresholds (metres)")]
        [SerializeField, Min(1f)]   private float _nearMaxDistance    =  30f;
        [SerializeField, Min(1f)]   private float _midMaxDistance     =  80f;
        [SerializeField, Min(1f)]   private float _farMaxDistance     = 150f;

        [Header("City Congestion Mode")]
        [Tooltip("When Near+Mid entity count exceeds this, Mid entities switch to CityOnly (name-tag only).")]
        [SerializeField, Min(1)] private int _cityModeThreshold = 40;

        [Header("Sync Intervals (seconds)")]
        [SerializeField] private float _nearSyncInterval     = 0.050f; // 20 Hz
        [SerializeField] private float _midSyncInterval      = 0.100f; // 10 Hz
        [SerializeField] private float _farSyncInterval      = 0.200f; //  5 Hz
        [SerializeField] private float _cityOnlySyncInterval = 0.500f; //  2 Hz (name only)

        [Header("Local Player")]
        [Tooltip("Auto-discovered if left empty. Must be the player's transform.")]
        [SerializeField] private Transform _localPlayerTransform;

        [Header("Performance")]
        [Tooltip("How often to recalculate interest levels (seconds). 0.1 = 10 Hz evaluation.")]
        [SerializeField, Min(0.016f)] private float _evaluationInterval = 0.1f;

        // ── Events ─────────────────────────────────────────────────────────────────

        /// <summary>Entity moved from OutOfRange into a valid interest level.</summary>
        public event Action<int, InterestLevel>             OnEntityEnteredInterest;

        /// <summary>Entity moved from a valid interest level to OutOfRange.</summary>
        public event Action<int>                            OnEntityExitedInterest;

        /// <summary>Entity changed interest level (Near↔Mid↔Far↔CityOnly).</summary>
        public event Action<int, InterestLevel, InterestLevel> OnInterestLevelChanged;

        /// <summary>Fires when entity enters city mode (body hide, show name tag only).</summary>
        public event Action<int, bool>                      OnEntityNameOnlyChanged;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private readonly Dictionary<int, InterestEntry> _entries = new();
        private readonly List<int> _tempRemoveList = new();

        private bool _cityModeActive;
        private float _nextEvaluationAt;
        private float _squaredNear;
        private float _squaredMid;
        private float _squaredFar;

        // ── Nested types ───────────────────────────────────────────────────────────

        private sealed class InterestEntry
        {
            public int            EntityId;
            public Transform      EntityTransform; // null-safe checked on evaluation
            public InterestLevel  Level      = InterestLevel.OutOfRange;
            public float          LastSyncAt;
            public bool           IsNameOnly;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            CacheDistanceSquared();
            AutoDiscoverLocalPlayer();
        }

        private void Update()
        {
            if (Time.time < _nextEvaluationAt) return;
            _nextEvaluationAt = Time.time + _evaluationInterval;
            EvaluateAllEntities();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Register a networked entity for interest tracking.
        /// Safe to call multiple times — first registration wins.
        /// </summary>
        public void RegisterEntity(int entityId, Transform entityTransform)
        {
            if (_entries.ContainsKey(entityId)) return;
            _entries[entityId] = new InterestEntry
            {
                EntityId        = entityId,
                EntityTransform = entityTransform,
                Level           = InterestLevel.OutOfRange
            };
        }

        /// <summary>Convenience overload accepting an <see cref="EntityView"/>.</summary>
        public void RegisterEntity(EntityView view)
        {
            if (view == null) return;
            RegisterEntity(view.EntityId, view.transform);
        }

        /// <summary>Unregister and raise <see cref="OnEntityExitedInterest"/> if needed.</summary>
        public void UnregisterEntity(int entityId)
        {
            if (!_entries.TryGetValue(entityId, out InterestEntry entry)) return;
            _entries.Remove(entityId);

            if (entry.Level != InterestLevel.OutOfRange)
                OnEntityExitedInterest?.Invoke(entityId);
        }

        /// <summary>Whether the entity is within the max-range threshold.</summary>
        public bool IsInInterest(int entityId)
        {
            return _entries.TryGetValue(entityId, out InterestEntry e)
                   && e.Level != InterestLevel.OutOfRange;
        }

        /// <summary>
        /// Whether enough time has elapsed since the last sync for this entity's interest level.
        /// Updates the last-sync timestamp when returning true.
        /// </summary>
        public bool CanSyncNow(int entityId)
        {
            if (!_entries.TryGetValue(entityId, out InterestEntry e)) return false;
            if (e.Level == InterestLevel.OutOfRange) return false;

            float interval = GetSyncInterval(e.Level);
            if (Time.unscaledTime - e.LastSyncAt < interval) return false;

            e.LastSyncAt = Time.unscaledTime;
            return true;
        }

        /// <summary>True if the entity is in city mode (body hidden, name-tag only).</summary>
        public bool ShouldShowNameOnly(int entityId)
        {
            return _entries.TryGetValue(entityId, out InterestEntry e) && e.IsNameOnly;
        }

        /// <summary>Returns the current interest level, or OutOfRange if not registered.</summary>
        public InterestLevel GetInterestLevel(int entityId)
        {
            return _entries.TryGetValue(entityId, out InterestEntry e)
                ? e.Level
                : InterestLevel.OutOfRange;
        }

        /// <summary>
        /// Returns the sync interval in seconds for a given interest level.
        /// Useful for external throttle calculations.
        /// </summary>
        public float GetSyncInterval(InterestLevel level) => level switch
        {
            InterestLevel.Near     => _nearSyncInterval,
            InterestLevel.Mid      => _midSyncInterval,
            InterestLevel.Far      => _farSyncInterval,
            InterestLevel.CityOnly => _cityOnlySyncInterval,
            _                      => float.MaxValue
        };

        /// <summary>Total number of currently tracked entities.</summary>
        public int TrackedEntityCount => _entries.Count;

        /// <summary>Whether city congestion mode is currently active.</summary>
        public bool IsCityModeActive => _cityModeActive;

        // ── Evaluation ─────────────────────────────────────────────────────────────

        private void EvaluateAllEntities()
        {
            if (_localPlayerTransform == null) return;

            Vector3 playerPos = _localPlayerTransform.position;
            int nearMidCount  = 0;

            _tempRemoveList.Clear();

            foreach (KeyValuePair<int, InterestEntry> kv in _entries)
            {
                InterestEntry entry = kv.Value;

                // Clean up destroyed GameObjects
                if (entry.EntityTransform == null)
                {
                    _tempRemoveList.Add(kv.Key);
                    continue;
                }

                float sqDist = (entry.EntityTransform.position - playerPos).sqrMagnitude;
                InterestLevel newLevel = ClassifyDistance(sqDist);

                if (newLevel == InterestLevel.Near || newLevel == InterestLevel.Mid)
                    nearMidCount++;

                ApplyNewLevel(entry, newLevel);
            }

            // Clean up destroyed entity entries
            foreach (int id in _tempRemoveList)
            {
                if (_entries[id].Level != InterestLevel.OutOfRange)
                    OnEntityExitedInterest?.Invoke(id);
                _entries.Remove(id);
            }

            // City mode: downgrade Mid → CityOnly when population is high
            bool shouldBeCityMode = nearMidCount > _cityModeThreshold;
            if (shouldBeCityMode != _cityModeActive)
            {
                _cityModeActive = shouldBeCityMode;
                ApplyCityModeToMidEntities();
            }
        }

        private InterestLevel ClassifyDistance(float sqDist)
        {
            if (sqDist <= _squaredNear) return InterestLevel.Near;
            if (sqDist <= _squaredMid)  return InterestLevel.Mid;
            if (sqDist <= _squaredFar)  return InterestLevel.Far;
            return InterestLevel.OutOfRange;
        }

        private void ApplyNewLevel(InterestEntry entry, InterestLevel newLevel)
        {
            InterestLevel prevLevel = entry.Level;

            if (prevLevel == newLevel) return;

            entry.Level = newLevel;

            if (prevLevel == InterestLevel.OutOfRange && newLevel != InterestLevel.OutOfRange)
            {
                OnEntityEnteredInterest?.Invoke(entry.EntityId, newLevel);
            }
            else if (newLevel == InterestLevel.OutOfRange)
            {
                OnEntityExitedInterest?.Invoke(entry.EntityId);
                SetEntityNameOnly(entry, false);
            }
            else
            {
                OnInterestLevelChanged?.Invoke(entry.EntityId, prevLevel, newLevel);
            }
        }

        /// <summary>
        /// When city mode flips, update all Mid entities' name-only flag and fire events.
        /// </summary>
        private void ApplyCityModeToMidEntities()
        {
            foreach (InterestEntry entry in _entries.Values)
            {
                if (entry.Level != InterestLevel.Mid) continue;
                SetEntityNameOnly(entry, _cityModeActive);
            }
        }

        private void SetEntityNameOnly(InterestEntry entry, bool nameOnly)
        {
            if (entry.IsNameOnly == nameOnly) return;
            entry.IsNameOnly = nameOnly;
            OnEntityNameOnlyChanged?.Invoke(entry.EntityId, nameOnly);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private void CacheDistanceSquared()
        {
            _squaredNear = _nearMaxDistance * _nearMaxDistance;
            _squaredMid  = _midMaxDistance  * _midMaxDistance;
            _squaredFar  = _farMaxDistance  * _farMaxDistance;
        }

        private void AutoDiscoverLocalPlayer()
        {
            if (_localPlayerTransform != null) return;

            CharacterController cc = FindAnyObjectByType<CharacterController>();
            if (cc != null)
            {
                _localPlayerTransform = cc.transform;
                return;
            }

            SnapshotSyncDriver driver = FindAnyObjectByType<SnapshotSyncDriver>();
            if (driver != null)
            {
                // Attempt to grab the local player transform from the sync driver via reflection-free API
                _localPlayerTransform = driver.transform;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep distance thresholds sane
            _midMaxDistance = Mathf.Max(_nearMaxDistance + 1f, _midMaxDistance);
            _farMaxDistance = Mathf.Max(_midMaxDistance  + 1f, _farMaxDistance);
            CacheDistanceSquared();
        }

        private void OnDrawGizmosSelected()
        {
            if (_localPlayerTransform == null) return;

            Vector3 center = _localPlayerTransform.position;

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            DrawGizmoCircle(center, _nearMaxDistance);

            Gizmos.color = new Color(1f, 1f, 0f, 0.10f);
            DrawGizmoCircle(center, _midMaxDistance);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
            DrawGizmoCircle(center, _farMaxDistance);
        }

        private static void DrawGizmoCircle(Vector3 center, float radius)
        {
            const int segments = 32;
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
