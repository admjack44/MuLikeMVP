using UnityEngine;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Handles character movement using CharacterController. Applies server-authoritative position corrections.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 720f;

        private CharacterController _controller;
        private Vector3 _moveDirection;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            }

            _controller.SimpleMove(_moveDirection * _moveSpeed);
        }

        public void SetMoveDirection(Vector3 direction)
        {
            _moveDirection = direction.normalized;
        }

        public void Stop()
        {
            _moveDirection = Vector3.zero;
        }

        /// <summary>Snaps character to server-authoritative position.</summary>
        public void ApplyServerCorrection(Vector3 serverPosition)
        {
            if (Vector3.Distance(transform.position, serverPosition) > 1.5f)
            {
                _controller.enabled = false;
                transform.position = serverPosition;
                _controller.enabled = true;
            }
        }
    }
}
