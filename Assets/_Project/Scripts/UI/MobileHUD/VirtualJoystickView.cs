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
        [SerializeField, Range(0f, 0.8f)] private float _deadZone = 0.15f;
        [SerializeField] private float _responseExponent = 1.35f;
        [SerializeField] private bool _smoothOutput = true;
        [SerializeField] private float _smoothingSpeed = 14f;
        [SerializeField] private bool _snapToZeroOnRelease = true;
        [SerializeField] private Image _background;

        private Vector2 _input;
        private Vector2 _rawInput;

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
            {
                _rawInput = Vector2.zero;
                SetInput(Vector2.zero);
            }
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
            _rawInput = ApplyDeadZoneAndCurve(normalized);

            if (_smoothOutput)
                return;

            SetInput(_rawInput);
        }

        private void Update()
        {
            if (!_smoothOutput)
                return;

            float step = Mathf.Max(1f, _smoothingSpeed) * Time.unscaledDeltaTime;
            Vector2 next = Vector2.Lerp(_input, _rawInput, step);
            if ((next - _rawInput).sqrMagnitude <= 0.00001f)
                next = _rawInput;

            SetInput(next);
        }

        private void SetInput(Vector2 value)
        {
            _input = Vector2.ClampMagnitude(value, 1f);

            if (_knob != null)
                _knob.anchoredPosition = _input * _radius;

            InputChanged?.Invoke(_input);
        }

        private Vector2 ApplyDeadZoneAndCurve(Vector2 input)
        {
            float magnitude = input.magnitude;
            if (magnitude <= _deadZone)
                return Vector2.zero;

            float normalized = Mathf.InverseLerp(_deadZone, 1f, magnitude);
            float curved = Mathf.Pow(normalized, Mathf.Max(0.01f, _responseExponent));
            return input.normalized * curved;
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(20f, _radius);
            _deadZone = Mathf.Clamp(_deadZone, 0f, 0.8f);
            _responseExponent = Mathf.Max(0.01f, _responseExponent);
            _smoothingSpeed = Mathf.Max(1f, _smoothingSpeed);
            if (_background != null)
                _background.raycastTarget = true;
        }
    }
}
