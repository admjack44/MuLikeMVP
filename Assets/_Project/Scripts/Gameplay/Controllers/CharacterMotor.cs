using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MuLike.Networking;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// ARPG/MMO-ready character motor with click-to-move, network move requests and simple reconciliation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 720f;
        [SerializeField] private float _stoppingDistance = 0.15f;

        [Header("Click To Move")]
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
        private Vector3 _moveDirection;
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
            if (_enableClickToMove)
                ProcessClickToMoveInput();

            if (IsMovementLocked)
            {
                Stop();
                return;
            }

            UpdateSteering();

            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            }

            _controller.SimpleMove(_moveDirection * _moveSpeed);

            if (_sendMoveRequests)
                TrySendMoveRequest();
        }

        public void MoveToPoint(Vector3 worldPoint)
        {
            worldPoint.y = transform.position.y;
            _movementDriver.SetDestination(worldPoint);
        }

        public void NotifyCastingState(bool isCasting)
        {
            _isCastingLocked = isCasting;
        }

        public void SetMoveDirection(Vector3 direction)
        {
            _movementDriver.ClearDestination();
            _moveDirection = direction.normalized;
        }

        public void Stop()
        {
            _movementDriver.ClearDestination();
            _moveDirection = Vector3.zero;
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

        private void UpdateSteering()
        {
            if (_movementDriver.TryGetSteering(
                transform.position,
                _stoppingDistance,
                out Vector3 direction,
                out _))
            {
                _moveDirection = direction;
                return;
            }

            if (_moveDirection.sqrMagnitude <= 0.0001f)
                return;

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

            if (!_movementDriver.HasDestination)
                return;

            if (Time.time < _nextSendTime)
                return;

            Vector3 target = _movementDriver.Destination;
            bool destinationChanged = !_hasSentMoveTarget || Vector3.Distance(_lastSentMoveTarget, target) >= _networkSendMinDistance;
            bool resendDue = _hasSentMoveTarget && (Time.time - _lastSentAt) >= _networkResendInterval;
            if (!destinationChanged && !resendDue)
                return;

            _nextSendTime = Time.time + _networkSendInterval;
            _lastSentMoveTarget = target;
            _lastSentAt = Time.time;
            _hasSentMoveTarget = true;
            _ = _networkClient.SendMoveAsync(target.x, target.y, target.z);
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
            if (_movementDriver == null || !_movementDriver.HasDestination)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_movementDriver.Destination, 0.15f);
            Gizmos.DrawLine(transform.position, _movementDriver.Destination);
            }
    }
}
