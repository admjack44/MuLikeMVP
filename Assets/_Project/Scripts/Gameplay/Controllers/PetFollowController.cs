using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Gameplay.Controllers
{
    /// <summary>
    /// Makes the pet entity follow its owner at a comfortable distance using smooth lerp movement.
    /// </summary>
    public class PetFollowController : MonoBehaviour
    {
        [SerializeField] private float _followDistance = 2f;
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _rotationSpeed = 360f;

        private PetView _petView;
        private Transform _ownerTransform;

        public void Initialize(PetView petView, EntityView owner)
        {
            _petView = petView;
            _ownerTransform = owner.transform;
        }

        private void Update()
        {
            if (_ownerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _ownerTransform.position);
            if (dist <= _followDistance) return;

            Vector3 direction = (_ownerTransform.position - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, _ownerTransform.position - direction * _followDistance, _moveSpeed * Time.deltaTime);

            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            }
        }
    }
}
