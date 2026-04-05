using MuLike.Gameplay.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Draws target lock indicator above current target in screen space.
    /// </summary>
    public sealed class TargetIndicatorView : MonoBehaviour
    {
        [SerializeField] private RectTransform _indicatorRoot;
        [SerializeField] private Image _ringImage;
        [SerializeField] private Vector3 _worldOffset = new(0f, 2.2f, 0f);
        [SerializeField] private float _pulseSpeed = 2.5f;
        [SerializeField] private float _pulseAmplitude = 0.08f;

        private Camera _camera;
        private EntityView _target;
        private float _baseScale = 1f;

        private void Awake()
        {
            _camera = Camera.main;
            if (_indicatorRoot != null)
                _baseScale = _indicatorRoot.localScale.x;

            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_target == null || _indicatorRoot == null)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                return;

            Vector3 world = _target.transform.position + _worldOffset;
            Vector3 screen = _camera.WorldToScreenPoint(world);
            bool visible = screen.z > 0f;
            SetVisible(visible);
            if (!visible)
                return;

            _indicatorRoot.position = screen;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * _pulseSpeed) * _pulseAmplitude;
            _indicatorRoot.localScale = new Vector3(_baseScale * pulse, _baseScale * pulse, 1f);
        }

        public void SetTarget(EntityView target)
        {
            _target = target;
            SetVisible(_target != null);
        }

        private void SetVisible(bool visible)
        {
            if (_indicatorRoot != null)
                _indicatorRoot.gameObject.SetActive(visible);

            if (_ringImage != null)
                _ringImage.enabled = visible;
        }
    }
}
