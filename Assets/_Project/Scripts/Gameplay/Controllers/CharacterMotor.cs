using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MuLike.Networking;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// ARPG/MMO character motor with joystick input, camera-relative movement, and network sync.
    /// Click-to-move available only in editor for testing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 720f;
        [SerializeField] private float _stoppingDistance = 0.15f;
        [SerializeField] private float _inputDeadZone = 0.1f;
        [SerializeField] private float _inputSmoothingSpeed = 12f;

        [Header("Click To Move (Editor Only)")]
        [SerializeField] private bool _enableClickToMove = true;
        [SerializeField] private LayerMask _groundMask = ~0;

        [Header("Cast Lock")]
        [SerializeField] private bool _lockMovementDuringCast = true;

        [Header("Networking")]
        [SerializeField] private NetworkGameClient _networkClient;
        [SerializeField] private bool _sendMoveRequests = true;
        [SerializeField] private float _networkSendInterval = 0.1f;
        [SerializeField] private float _networkSendMinDistance = 0.25f;
        [SerializeField] private float _networkResendInterval = 0.5f;

        [Header("Reconciliation")]
        [SerializeField] private float _softCorrectionDistance = 0.5f;
        [SerializeField] private float _hardSnapDistance = 1.5f;
        [SerializeField] private float _softCorrectionLerp = 0.35f;

        private CharacterController _controller;
        private Camera _mainCamera;
        private ICharacterMovementDriver _movementDriver;
        private CameraFollowController _cameraFollow;
        private Vector3 _moveDirection;
        private Vector2 _rawInput = Vector2.zero;
        private Vector2 _smoothedInput = Vector2.zero;
        private Vector3 _lastSentMoveTarget;
        private float _nextSendTime;
        private bool _isCastingLocked;
        private bool _hasSentMoveTarget;
        private float _lastSentAt;

        public bool HasDestination => _movementDriver != null && _movementDriver.HasDestination;
        public Vector3 Destination => _movementDriver != null ? _movementDriver.Destination : transform.position;
        public bool IsMovementLocked => _lockMovementDuringCast && _isCastingLocked;
        public float MoveSpeed
        {
            get => _moveSpeed;
            set => _moveSpeed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
            _movementDriver = new StraightLineMovementDriver();
            _cameraFollow = FindAnyObjectByType<CameraFollowController>();

            if (_networkClient == null)
                _networkClient = FindObjectOfType<NetworkGameClient>();
        }

        private void OnEnable()
        {
            if (_networkClient != null)
                _networkClient.OnMoveResult += HandleNetworkMoveResult;
        }

        private void OnDisable()
        {
            if (_networkClient != null)
                _networkClient.OnMoveResult -= HandleNetworkMoveResult;
        }

        private void Update()
        {
            // Process editor-only click-to-move
            if (_enableClickToMove && IsEditorOrDesktop())
                ProcessClickToMoveInput();

            if (IsMovementLocked)
            {
                Stop();
                return;
            }

            // Update steering based on current input mode
            UpdateSteering();

            // Apply input smoothing
            SmoothInput();

            // Calculate move direction from smoothed input
            CalculateMoveDirectionFromInput();

            // Rotate character toward move direction
            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            }

            // Apply movement
            _controller.SimpleMove(_moveDirection * _moveSpeed);

            // Send movement to network
            if (_sendMoveRequests)
                TrySendMoveRequest();
        }

        /// <summary>
        /// Set raw input from joystick (called by MobileHudController).
        /// Input should already be relative to camera.
        /// </summary>
        public void SetJoystickInput(Vector2 input)
        {
            _rawInput = Vector2.ClampMagnitude(input, 1f);

            // Apply dead zone
            if (_rawInput.magnitude < _inputDeadZone)
                _rawInput = Vector2.zero;
            else
                _rawInput = (_rawInput - _rawInput.normalized * _inputDeadZone) / (1f - _inputDeadZone);
        }

        public void MoveToPoint(Vector3 worldPoint)
        {
            worldPoint.y = transform.position.y;
            _movementDriver.SetDestination(worldPoint);
        }

        /// <summary>
        /// Set continuous move direction for joystick input (Vector2 input already converted to world direction).
        /// </summary>
        public void SetMoveDirection(Vector3 direction)
        {
            _movementDriver.ClearDestination();
            _moveDirection = direction.normalized;
        }

        public void NotifyCastingState(bool isCasting)
        {
            _isCastingLocked = isCasting;
        }

        public void Stop()
        {
            _movementDriver.ClearDestination();
            _moveDirection = Vector3.zero;
            _rawInput = Vector2.zero;
            _smoothedInput = Vector2.zero;
        }

        /// <summary>Snaps character to server-authoritative position.</summary>
        public void ApplyServerCorrection(Vector3 serverPosition)
        {
            float drift = Vector3.Distance(transform.position, serverPosition);
            if (drift <= 0.001f) return;

            if (drift >= _hardSnapDistance)
            {
                Teleport(serverPosition);
                return;
            }

            if (drift >= _softCorrectionDistance)
            {
                Vector3 corrected = Vector3.Lerp(transform.position, serverPosition, Mathf.Clamp01(_softCorrectionLerp));
                Teleport(corrected);
            }
        }

        private void CalculateMoveDirectionFromInput()
        {
            // If we have a click-to-move destination, use steering
            if (_movementDriver.HasDestination)
                return;

            // Otherwise, use joystick input direction
            if (_smoothedInput.sqrMagnitude < 0.001f)
            {
                _moveDirection = Vector3.zero;
                return;
            }

            // Convert camera-relative input to world direction
            if (_cameraFollow != null)
            {
                Vector3 cameraForward = _cameraFollow.GetCameraRelativeForward();
                Vector3 cameraRight = _cameraFollow.GetCameraRelativeRight();
                _moveDirection = (cameraForward * _smoothedInput.y + cameraRight * _smoothedInput.x).normalized;
            }
            else
            {
                // Fallback: use camera or character forward
                Vector3 forward = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
                Vector3 right = _mainCamera != null ? _mainCamera.transform.right : transform.right;
                forward.y = 0f;
                right.y = 0f;
                _moveDirection = (forward.normalized * _smoothedInput.y + right.normalized * _smoothedInput.x).normalized;
            }
        }

        private void SmoothInput()
        {
            float delta = Mathf.Max(1f, _inputSmoothingSpeed) * Time.deltaTime;
            _smoothedInput = Vector2.Lerp(_smoothedInput, _rawInput, delta);

            // Snap to zero if very close
            if (_smoothedInput.sqrMagnitude < 0.00001f)
                _smoothedInput = Vector2.zero;
        }

        private void UpdateSteering()
        {
            // Only use steering if we have a click-to-move destination
            if (!_movementDriver.HasDestination)
                return;

            if (_movementDriver.TryGetSteering(
                transform.position,
                _stoppingDistance,
                out Vector3 direction,
                out _))
            {
                _moveDirection = direction;
                return;
            }

            // Destination reached
            _moveDirection = Vector3.zero;
        }

        private void ProcessClickToMoveInput()
        {
            if (!WasRightMousePressed())
                return;

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            if (_mainCamera == null)
                return;

            Ray ray = _mainCamera.ScreenPointToRay(GetMouseScreenPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundMask))
                MoveToPoint(hit.point);
        }

        private void TrySendMoveRequest()
        {
            if (_networkClient == null || !_networkClient.IsConnected || !_networkClient.IsAuthenticated)
                return;

            if (Time.time < _nextSendTime)
                return;

            // Determine move target for network
            Vector3 moveTarget;

            if (_movementDriver.HasDestination)
            {
                // Click-to-move mode: send the destination
                moveTarget = _movementDriver.Destination;
            }
            else if (_moveDirection.sqrMagnitude > 0.01f)
            {
                // Joystick continuous mode: create pseudo-destination ahead
                moveTarget = transform.position + _moveDirection * 10f; // 10 units ahead
            }
            else
            {
                // No movement
                return;
            }

            bool destinationChanged = !_hasSentMoveTarget || Vector3.Distance(_lastSentMoveTarget, moveTarget) >= _networkSendMinDistance;
            bool resendDue = _hasSentMoveTarget && (Time.time - _lastSentAt) >= _networkResendInterval;

            if (!destinationChanged && !resendDue)
                return;

            _nextSendTime = Time.time + _networkSendInterval;
            _lastSentMoveTarget = moveTarget;
            _lastSentAt = Time.time;
            _hasSentMoveTarget = true;
            _ = _networkClient.SendMoveAsync(moveTarget.x, moveTarget.y, moveTarget.z);
        }

        private void HandleNetworkMoveResult(bool success, Vector3 position, string message)
        {
            if (!success) return;
            ApplyServerCorrection(position);
        }

        private void Teleport(Vector3 position)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
        }

        private static bool IsEditorOrDesktop()
        {
#if UNITY_EDITOR
            return true;
#elif UNITY_ANDROID || UNITY_IOS
            return false;
#else
            return true;
#endif
        }

        private static bool WasRightMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
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

        private void OnDrawGizmosSelected()
        {
            if (!_movementDriver?.HasDestination ?? false)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_movementDriver.Destination, 0.15f);
            Gizmos.DrawLine(transform.position, _movementDriver.Destination);

            // Draw move direction
            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, transform.position + _moveDirection);
            }
        }
    }
}
