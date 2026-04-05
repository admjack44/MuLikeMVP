using UnityEngine;

namespace MuLike.Performance.Rendering
{
    /// <summary>
    /// Mobile animator optimization: lower update cost for far entities.
    /// </summary>
    public sealed class AnimatorOptimizationProxy : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private float _fullUpdateDistance = 16f;
        [SerializeField] private float _lowUpdateDistance = 32f;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_animator == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            if (distance <= _fullUpdateDistance)
            {
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                _animator.speed = 1f;
                return;
            }

            if (distance <= _lowUpdateDistance)
            {
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                _animator.speed = 0.9f;
                return;
            }

            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }
    }
}
