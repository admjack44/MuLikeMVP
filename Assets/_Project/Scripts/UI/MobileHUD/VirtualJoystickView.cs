using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    /// <summary>
    /// Touch joystick that emits normalized 2D movement input.
    /// Works with Input System through InputSystemUIInputModule.
    /// </summary>
    public sealed class VirtualJoystickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private float _radius = 75f;
        [SerializeField] private bool _snapToZeroOnRelease = true;
        [SerializeField] private Image _background;

        private Vector2 _input;

        public event Action<Vector2> InputChanged;

        public Vector2 Input => _input;

        private void Awake()
        {
            if (_root == null)
                _root = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateFromPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_snapToZeroOnRelease)
                SetInput(Vector2.zero);
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (_root == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 normalized = localPoint / Mathf.Max(1f, _radius);
            normalized = Vector2.ClampMagnitude(normalized, 1f);
            SetInput(normalized);
        }

        private void SetInput(Vector2 value)
        {
            _input = Vector2.ClampMagnitude(value, 1f);

            if (_knob != null)
                _knob.anchoredPosition = _input * _radius;

            InputChanged?.Invoke(_input);
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(20f, _radius);
            if (_background != null)
                _background.raycastTarget = true;
        }
    }
}
