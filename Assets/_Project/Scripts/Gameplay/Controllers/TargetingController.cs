using MuLike.Gameplay.Entities;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Manages the player's current target selection. Handles click-to-target and tab-targeting.
    /// </summary>
    public class TargetingController : MonoBehaviour
    {
        public EntityView CurrentTarget { get; private set; }
        public event System.Action<EntityView> OnTargetChanged;

        [SerializeField] private LayerMask _targetableLayer;
        [SerializeField] private float _maxRange = 30f;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (WasLeftMousePressed())
                TrySelectByClick();
        }

        private void TrySelectByClick()
        {
            Vector2 mouseScreenPos = GetMouseScreenPosition();
            Ray ray = _mainCamera.ScreenPointToRay(mouseScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, _maxRange, _targetableLayer))
            {
                var view = hit.collider.GetComponentInParent<EntityView>();
                if (view != null) SetTarget(view);
            }
        }

        private static bool WasLeftMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        public void SetTarget(EntityView target)
        {
            if (CurrentTarget == target)
                return;

            CurrentTarget = target;
            Debug.Log($"[Targeting] Selected entity {target?.EntityId}");
            OnTargetChanged?.Invoke(CurrentTarget);
        }

        public void ClearTarget()
        {
            if (CurrentTarget == null)
                return;

            CurrentTarget = null;
            OnTargetChanged?.Invoke(null);
        }

        public bool IsTargetInRange(Vector3 fromPosition, float range)
        {
            if (CurrentTarget == null)
                return false;

            return Vector3.Distance(fromPosition, CurrentTarget.transform.position) <= range;
        }
    }
}
