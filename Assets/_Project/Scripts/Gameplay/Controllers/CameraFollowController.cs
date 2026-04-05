using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Mobile camera controller with smooth follow, yaw orbit, and pitch control.
    /// No Cinemachine dependency. Designed for ARPG/MMORPG gameplay.
    /// </summary>
    public sealed class CameraFollowController : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Header("Follow")]
        [SerializeField] private float _followDistance = 6f;
        [SerializeField] private float _followHeight = 2f;
        [SerializeField] private float _followSmoothTime = 0.25f;

        [Header("Orbit")]
        [SerializeField] private float _orbitSensitivity = 0.5f;
        [SerializeField] private float _orbitDamping = 0.92f;

        [Header("Pitch")]
        [SerializeField, Range(-89f, 0f)] private float _minPitch = -45f;
        [SerializeField, Range(0f, 89f)] private float _maxPitch = 45f;
        [SerializeField] private float _pitchSensitivity = 0.5f;

        [Header("Auto Alignment")]
        [SerializeField] private bool _autoAlignToTarget = true;
        [SerializeField, Range(0f, 1f)] private float _autoAlignStrength = 0.08f;

        [Header("Orbit Input")]
        [SerializeField] private float _orbitInputRectMinX = 0.5f;
        [SerializeField] private float _orbitInputRectMinY = 0f;
        [SerializeField] private float _orbitInputRectWidth = 0.5f;
        [SerializeField] private float _orbitInputRectHeight = 1f;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = false;

        private float _targetYaw;
        private float _currentYaw;
        private float _targetPitch;
        private float _currentPitch;
        private float _yawVelocity;
        private Vector3 _velocityFollow;
        private bool _isOrbiting;

        private Rect _orbitInputRect;

        private void OnEnable()
        {
            if (_target == null)
                _target = transform.parent;

            UpdateOrbitInputRect();
        }

        private void Update()
        {
            if (_target == null)
                return;

            ProcessOrbitInput();
            UpdateCameraPosition();
        }

        private void UpdateOrbitInputRect()
        {
            Rect screenRect = new(
                Screen.width * _orbitInputRectMinX,
                Screen.height * _orbitInputRectMinY,
                Screen.width * _orbitInputRectWidth,
                Screen.height * _orbitInputRectHeight);
            _orbitInputRect = screenRect;
        }

        private void ProcessOrbitInput()
        {
            bool touchActive = false;
            Vector2 touchDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.isPressed)
            {
                Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
                if (_orbitInputRect.Contains(touchPos))
                {
                    touchActive = true;
                    Vector2 touchPosPrev = touchPos - Touchscreen.current.primaryTouch.delta.ReadValue();
                    touchDelta = touchPos - touchPosPrev;
                }
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (_orbitInputRect.Contains(touch.position))
                {
                    touchActive = true;
                    touchDelta = touch.deltaPosition;
                }
            }
#endif

            if (touchActive)
            {
                _targetYaw += touchDelta.x * _orbitSensitivity;
                _targetPitch -= touchDelta.y * _pitchSensitivity;
                _targetPitch = Mathf.Clamp(_targetPitch, _minPitch, _maxPitch);
                _isOrbiting = true;
            }
            else
            {
                _isOrbiting = false;
            }

            // Apply damping when not orbiting
            if (!_isOrbiting)
            {
                _targetYaw = Mathf.Lerp(_targetYaw, _currentYaw, _orbitDamping);
            }

            // Smooth yaw rotation
            _currentYaw = Mathf.SmoothDamp(_currentYaw, _targetYaw, ref _yawVelocity, 0.1f, Mathf.Infinity, Time.deltaTime);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, Time.deltaTime * 8f);
        }

        private void UpdateCameraPosition()
        {
            // Target position: character + height offset
            Vector3 targetPos = _target.position + Vector3.up * _followHeight;

            // Calculate camera offset based on yaw/pitch
            Quaternion rotationYaw = Quaternion.Euler(0f, _currentYaw, 0f);
            Quaternion rotationPitch = Quaternion.Euler(_currentPitch, 0f, 0f);
            Quaternion totalRotation = rotationYaw * rotationPitch;

            Vector3 cameraOffset = totalRotation * (Vector3.back * _followDistance);
            Vector3 desiredCameraPos = targetPos + cameraOffset;

            // Smooth follow
            Vector3 smoothedPos = Vector3.SmoothDamp(
                transform.position,
                desiredCameraPos,
                ref _velocityFollow,
                _followSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);

            transform.position = smoothedPos;

            // Look at target
            Vector3 lookTarget = targetPos;
            if (_autoAlignToTarget && !_isOrbiting)
            {
                // Gradually align camera yaw to character forward
                Vector3 targetForward = _target.forward;
                Vector3 cameraForward = (lookTarget - transform.position).normalized;
                Vector3 alignedForward = Vector3.Lerp(cameraForward, targetForward, _autoAlignStrength);
                transform.rotation = Quaternion.LookRotation(alignedForward, Vector3.up);
            }
            else
            {
                transform.LookAt(lookTarget, Vector3.up);
            }
        }

        /// <summary>
        /// Get the forward direction relative to camera (for joystick movement).
        /// </summary>
        public Vector3 GetCameraRelativeForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.normalized;
        }

        /// <summary>
        /// Get the right direction relative to camera (for joystick movement).
        /// </summary>
        public Vector3 GetCameraRelativeRight()
        {
            Vector3 right = transform.right;
            right.y = 0f;
            return right.normalized;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos || _target == null)
                return;

            // Draw orbit input rect
            Vector3 rectMin = new(_orbitInputRect.xMin, 0, 0);
            Vector3 rectMax = new(_orbitInputRect.xMax, 0, 0);
            Gizmos.color = new Color(0, 1, 0, 0.3f);

            // Follow distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_target.position + Vector3.up * _followHeight, 0.2f);
        }
    }
}
