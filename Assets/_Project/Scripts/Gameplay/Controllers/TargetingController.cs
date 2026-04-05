using MuLike.Gameplay.Entities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Manages current target lock with score-based selection for mobile ARPG/MMO combat.
    /// </summary>
    public class TargetingController : MonoBehaviour
    {
        public EntityView CurrentTarget { get; private set; }
        public int CurrentTargetEntityId => CurrentTarget != null ? CurrentTarget.EntityId : 0;
        public event System.Action<EntityView> OnTargetChanged;

        [Header("Dependencies")]
        [SerializeField] private Transform _playerRoot;
        [SerializeField] private Camera _mainCamera;

        [Header("Selection")]
        [SerializeField] private LayerMask _targetableLayer;
        [SerializeField] private float _maxRange = 30f;
        [SerializeField] private float _acquireRadius = 24f;
        [SerializeField, Range(5f, 180f)] private float _acquireAngle = 90f;
        [SerializeField] private LayerMask _lineOfSightBlockingMask;
        [SerializeField] private float _lineOfSightHeightOffset = 1.1f;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Priority")]
        [SerializeField] private bool _preferManualTarget = true;
        [SerializeField] private bool _preferNearestVisible = true;
        [SerializeField] private bool _preferAggressorFallback = true;

        [Header("Scoring Weights")]
        [SerializeField, Min(0f)] private float _distanceWeight = 1.2f;
        [SerializeField, Min(0f)] private float _angleWeight = 0.75f;
        [SerializeField, Min(0f)] private float _threatWeight = 0.9f;
        [SerializeField, Min(0f)] private float _stickyCurrentBonus = 0.45f;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = false;
        [SerializeField] private Color _gizmoAcquireColor = new(1f, 0.8f, 0.2f, 0.6f);
        [SerializeField] private Color _gizmoTargetColor = new(1f, 0.25f, 0.2f, 0.9f);

        private readonly Collider[] _candidateBuffer = new Collider[64];
        private readonly Dictionary<int, float> _threatByEntity = new();
        private int _manualTargetEntityId;

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_playerRoot == null)
                _playerRoot = transform;
        }

        private void Update()
        {
            if (WasLeftMousePressed())
                TrySelectByClick();

            ValidateLockedTarget();
        }

        private void TrySelectByClick()
        {
            if (IsPointerOverUi())
                return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                return;

            Vector2 mouseScreenPos = GetMouseScreenPosition();
            Ray ray = _mainCamera.ScreenPointToRay(mouseScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, _maxRange, GetEffectiveTargetLayer(), _triggerInteraction))
            {
                var view = hit.collider.GetComponentInParent<EntityView>();
                if (view != null)
                    SetManualTarget(view);
            }
        }

        public EntityView AcquirePriorityTarget()
        {
            EntityView manual = GetManualTargetIfValid();
            if (_preferManualTarget && manual != null)
                return manual;

            if (_preferNearestVisible)
            {
                EntityView nearest = AcquireNearestVisibleTarget();
                if (nearest != null)
                    return nearest;
            }

            if (_preferAggressorFallback)
            {
                EntityView aggressor = AcquireHighestAggressorTarget();
                if (aggressor != null)
                    return aggressor;
            }

            return AcquireBestTarget();
        }

        public EntityView AcquireNearestVisibleTarget()
        {
            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                Mathf.Max(1f, _acquireRadius),
                _candidateBuffer,
                GetEffectiveTargetLayer(),
                _triggerInteraction);

            EntityView nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _candidateBuffer[i];
                if (col == null)
                    continue;

                EntityView candidate = col.GetComponentInParent<EntityView>();
                if (!IsTargetValid(candidate))
                    continue;

                float distance = Vector3.Distance(origin, candidate.transform.position);
                if (distance > _maxRange || distance >= nearestDistance)
                    continue;

                if (!IsTargetVisible(origin, candidate))
                    continue;

                nearestDistance = distance;
                nearest = candidate;
            }

            return nearest;
        }

        public EntityView AcquireHighestAggressorTarget()
        {
            if (_threatByEntity.Count == 0)
                return null;

            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;
            EntityView best = null;
            float bestThreat = float.MinValue;

            foreach (var pair in _threatByEntity)
            {
                if (pair.Value <= 0f)
                    continue;

                EntityView candidate = ResolveEntityById(pair.Key);
                if (!IsTargetValid(candidate))
                    continue;

                float distance = Vector3.Distance(origin, candidate.transform.position);
                if (distance > _maxRange)
                    continue;

                if (!IsTargetVisible(origin, candidate))
                    continue;

                if (pair.Value <= bestThreat)
                    continue;

                bestThreat = pair.Value;
                best = candidate;
            }

            return best;
        }

        public EntityView AcquireBestTarget()
        {
            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;
            Vector3 forward = _playerRoot != null ? _playerRoot.forward : transform.forward;
            return AcquireBestTarget(origin, forward);
        }

        public EntityView AcquireBestTarget(Vector3 origin, Vector3 forward)
        {
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                Mathf.Max(1f, _acquireRadius),
                _candidateBuffer,
                GetEffectiveTargetLayer(),
                _triggerInteraction);

            EntityView best = null;
            float bestScore = float.MinValue;
            Vector3 flatForward = Flatten(forward);

            for (int i = 0; i < count; i++)
            {
                Collider col = _candidateBuffer[i];
                if (col == null)
                    continue;

                EntityView candidate = col.GetComponentInParent<EntityView>();
                if (candidate == null)
                    continue;

                Vector3 toTarget = candidate.transform.position - origin;
                float distance = toTarget.magnitude;
                if (distance > _maxRange || distance <= 0.01f)
                    continue;

                Vector3 flatToTarget = Flatten(toTarget);
                float angle = Vector3.Angle(flatForward.sqrMagnitude > 0.001f ? flatForward : Vector3.forward, flatToTarget);
                if (angle > _acquireAngle)
                    continue;

                float score = ScoreCandidate(candidate, distance, angle);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        public void SetLockedTarget(EntityView target)
        {
            ApplyTarget(target, isManual: false);
        }

        public void SetManualTarget(EntityView target)
        {
            ApplyTarget(target, isManual: true);
        }

        public void SetTarget(EntityView target)
        {
            SetManualTarget(target);
        }

        public void ClearTarget()
        {
            ReleaseTarget();
        }

        public void ReleaseTarget()
        {
            _manualTargetEntityId = 0;
            if (CurrentTarget == null)
                return;

            CurrentTarget = null;
            Debug.Log("[Targeting] Target released.");
            OnTargetChanged?.Invoke(null);
        }

        public void RegisterAggressor(int entityId, float pressure = 1f)
        {
            RegisterThreat(entityId, pressure);
        }

        public bool IsTargetInRange(Vector3 fromPosition, float range)
        {
            return IsTargetInRange(CurrentTarget, fromPosition, range);
        }

        public bool IsTargetInRange(EntityView target, Vector3 fromPosition, float range)
        {
            if (!IsTargetValid(target))
                return false;

            return Vector3.Distance(fromPosition, target.transform.position) <= Mathf.Max(0f, range);
        }

        public bool IsTargetValid(EntityView target)
        {
            return target != null && target.isActiveAndEnabled;
        }

        private void ApplyTarget(EntityView target, bool isManual)
        {
            if (isManual)
                _manualTargetEntityId = target != null ? target.EntityId : 0;

            if (CurrentTarget == target)
                return;

            CurrentTarget = target;
            Debug.Log($"[Targeting] Locked target: {target?.EntityId}");
            OnTargetChanged?.Invoke(CurrentTarget);
        }

        public void RegisterThreat(int entityId, float threatDelta)
        {
            if (entityId <= 0 || Mathf.Approximately(threatDelta, 0f))
                return;

            _threatByEntity.TryGetValue(entityId, out float current);
            _threatByEntity[entityId] = Mathf.Max(0f, current + threatDelta);
        }

        public void DecayThreat(float amountPerSecond)
        {
            if (_threatByEntity.Count == 0)
                return;

            float decay = Mathf.Max(0f, amountPerSecond) * Time.deltaTime;
            if (decay <= 0f)
                return;

            List<int> keys = new(_threatByEntity.Keys);
            List<int> remove = null;
            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                if (!_threatByEntity.TryGetValue(key, out float value))
                    continue;

                float next = value - decay;
                if (next <= 0f)
                {
                    remove ??= new List<int>();
                    remove.Add(key);
                }
                else
                {
                    _threatByEntity[key] = next;
                }
            }

            if (remove == null)
                return;

            for (int i = 0; i < remove.Count; i++)
                _threatByEntity.Remove(remove[i]);
        }

        private static bool WasLeftMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                    return true;
            }

            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();

            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private void ValidateLockedTarget()
        {
            if (CurrentTarget == null)
                return;

            if (!IsTargetValid(CurrentTarget))
            {
                ReleaseTarget();
                return;
            }

            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;
            float distance = Vector3.Distance(origin, CurrentTarget.transform.position);
            if (distance > _maxRange)
                ReleaseTarget();

            if (_manualTargetEntityId > 0 && (CurrentTarget == null || CurrentTarget.EntityId != _manualTargetEntityId))
                _manualTargetEntityId = 0;
        }

        private EntityView GetManualTargetIfValid()
        {
            if (_manualTargetEntityId <= 0)
                return null;

            EntityView manual = ResolveEntityById(_manualTargetEntityId);
            if (!IsTargetValid(manual))
            {
                _manualTargetEntityId = 0;
                return null;
            }

            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;
            if (!IsTargetInRange(manual, origin, _maxRange) || !IsTargetVisible(origin, manual))
                return null;

            return manual;
        }

        private bool IsTargetVisible(Vector3 origin, EntityView target)
        {
            if (!IsTargetValid(target))
                return false;

            if (_lineOfSightBlockingMask.value == 0)
                return true;

            Vector3 from = origin + Vector3.up * Mathf.Max(0f, _lineOfSightHeightOffset);
            Vector3 to = target.transform.position + Vector3.up * Mathf.Max(0f, _lineOfSightHeightOffset);
            Vector3 dir = to - from;
            float distance = dir.magnitude;
            if (distance <= 0.01f)
                return true;

            dir /= distance;
            if (!Physics.Raycast(from, dir, out RaycastHit hit, distance, _lineOfSightBlockingMask, _triggerInteraction))
                return true;

            EntityView hitEntity = hit.collider != null ? hit.collider.GetComponentInParent<EntityView>() : null;
            return hitEntity == target;
        }

        private EntityView ResolveEntityById(int entityId)
        {
            if (entityId <= 0)
                return null;

            if (CurrentTarget != null && CurrentTarget.EntityId == entityId)
                return CurrentTarget;

            EntityView[] views = FindObjectsByType<EntityView>();
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].EntityId == entityId)
                    return views[i];
            }

            return null;
        }

        private float ScoreCandidate(EntityView candidate, float distance, float angle)
        {
            float distanceScore = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, _acquireRadius));
            float angleScore = 1f - Mathf.Clamp01(angle / Mathf.Max(1f, _acquireAngle));
            _threatByEntity.TryGetValue(candidate.EntityId, out float threat);
            float threatScore = Mathf.Clamp01(threat / 10f);
            float sticky = candidate == CurrentTarget ? _stickyCurrentBonus : 0f;

            return distanceScore * _distanceWeight
                + angleScore * _angleWeight
                + threatScore * _threatWeight
                + sticky;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }

        private int GetEffectiveTargetLayer()
        {
            return _targetableLayer.value != 0 ? _targetableLayer.value : Physics.DefaultRaycastLayers;
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos)
                return;

            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;

            Gizmos.color = _gizmoAcquireColor;
            Gizmos.DrawWireSphere(origin, Mathf.Max(1f, _acquireRadius));

            if (CurrentTarget == null)
                return;

            Gizmos.color = _gizmoTargetColor;
            Gizmos.DrawLine(origin, CurrentTarget.transform.position);
            Gizmos.DrawWireSphere(CurrentTarget.transform.position, 0.35f);
        }
    }
}
