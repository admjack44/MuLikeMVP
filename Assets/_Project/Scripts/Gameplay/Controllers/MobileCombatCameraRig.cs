using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Fixed isometric/third-person mobile camera with slight contextual yaw adaptation.
    /// </summary>
    public sealed class MobileCombatCameraRig : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Vector3 _pivotOffset = new(0f, 1.5f, 0f);
        [SerializeField] private float _distance = 7.5f;
        [SerializeField, Range(20f, 75f)] private float _pitch = 42f;
        [SerializeField] private float _baseYaw = 35f;
        [SerializeField] private float _positionLerp = 12f;

        [Header("Contextual Rotation")]
        [SerializeField] private bool _enableContextYaw = true;
        [SerializeField, Range(0f, 1f)] private float _targetYawInfluence = 0.25f;
        [SerializeField] private float _yawLerpSpeed = 3.5f;
        [SerializeField] private TargetingController _targetingController;

        [Header("Debug")]
        [SerializeField] private bool _drawDebugGizmos = false;

        private float _currentYaw;

        private void Awake()
        {
            if (_targetingController == null)
                _targetingController = FindAnyObjectByType<TargetingController>();

            if (_followTarget == null)
            {
                CharacterMotor motor = FindAnyObjectByType<CharacterMotor>();
                if (motor != null)
                    _followTarget = motor.transform;
            }

            _currentYaw = _baseYaw;
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
                return;

            float desiredYaw = _baseYaw;
            if (_enableContextYaw && _targetingController != null && _targetingController.CurrentTarget != null)
            {
                Vector3 toTarget = _targetingController.CurrentTarget.transform.position - _followTarget.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    float targetYaw = Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles.y;
                    desiredYaw = Mathf.LerpAngle(_baseYaw, targetYaw, Mathf.Clamp01(_targetYawInfluence));
                }
            }

            _currentYaw = Mathf.LerpAngle(_currentYaw, desiredYaw, 1f - Mathf.Exp(-Mathf.Max(0.1f, _yawLerpSpeed) * Time.deltaTime));
            Quaternion rotation = Quaternion.Euler(_pitch, _currentYaw, 0f);

            Vector3 pivot = _followTarget.position + _pivotOffset;
            Vector3 desiredPos = pivot + rotation * new Vector3(0f, 0f, -Mathf.Max(1f, _distance));

            float posT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _positionLerp) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, posT);
            transform.rotation = rotation;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos || _followTarget == null)
                return;

            Vector3 pivot = _followTarget.position + _pivotOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pivot, 0.15f);
            Gizmos.DrawLine(transform.position, pivot);
        }
    }
}
