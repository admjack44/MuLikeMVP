using UnityEngine;

namespace MuLike.Performance.Rendering
{
    /// <summary>
    /// Distance culling toggle for remote entities and fx.
    /// </summary>
    public sealed class DistanceCullingController : MonoBehaviour
    {
        [SerializeField] private float _visibleDistance = 45f;
        [SerializeField] private float _checkInterval = 0.2f;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private Behaviour[] _heavyBehaviours;
        [SerializeField] private Collider[] _colliders;

        private bool _isVisible = true;
        private float _nextCheckAt;

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextCheckAt)
                return;

            _nextCheckAt = Time.unscaledTime + Mathf.Max(0.02f, _checkInterval);

            Camera cam = Camera.main;
            if (cam == null)
                return;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            bool shouldBeVisible = distance <= _visibleDistance;
            if (shouldBeVisible == _isVisible)
                return;

            _isVisible = shouldBeVisible;
            SetRenderers(_isVisible);
            SetBehaviours(_isVisible);
            SetColliders(_isVisible);
        }

        private void SetRenderers(bool enabled)
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = enabled;
            }
        }

        private void SetBehaviours(bool enabled)
        {
            if (_heavyBehaviours == null)
                return;

            for (int i = 0; i < _heavyBehaviours.Length; i++)
            {
                if (_heavyBehaviours[i] != null)
                    _heavyBehaviours[i].enabled = enabled;
            }
        }

        private void SetColliders(bool enabled)
        {
            if (_colliders == null)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].enabled = enabled;
            }
        }
    }
}
