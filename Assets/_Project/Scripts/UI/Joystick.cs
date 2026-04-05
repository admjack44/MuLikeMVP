using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuLike.UI
{
    /// <summary>
    /// Floating virtual joystick that repositions to the touch-start location on the left half of
    /// the screen. Complements <see cref="MuLike.UI.MobileHUD.VirtualJoystickView"/> by adding:
    ///   - Three sensitivity presets (Low / Normal / High)
    ///   - Configurable dead zone (10–30 %)
    ///   - Optional 8-direction snap (snap output to nearest 45° cardinal / diagonal)
    ///   - Floating: the background rect teleports to the finger-down position
    ///
    /// Designed to be referenced by <see cref="MuLike.Input.MuTouchControls"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ── Sensitivity preset ────────────────────────────────────────────────────

        public enum SensitivityPreset
        {
            /// <summary>Smaller radius, larger dead zone — better for big thumbs.</summary>
            Low    = 0,
            /// <summary>Balanced defaults.</summary>
            Normal = 1,
            /// <summary>Wider radius, smaller dead zone — precise directional control.</summary>
            High   = 2
        }

        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Visual References")]
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private CanvasGroup   _canvasGroup;

        [Header("Sensitivity")]
        [SerializeField] private SensitivityPreset _sensitivity = SensitivityPreset.Normal;
        [SerializeField, Range(0.10f, 0.30f)] private float _deadZone = 0.15f;

        [Header("Floating")]
        [SerializeField] private bool _floating = true;
        [Tooltip("Padding from screen edge where the joystick background can spawn (in UI pixels).")]
        [SerializeField] private float _floatEdgePadding = 80f;

        [Header("Direction")]
        [Tooltip("Snap output vector to nearest 45° direction (8 directions: N NE E SE S SW W NW).")]
        [SerializeField] private bool _snap8Directions = false;

        [Header("Smooth")]
        [SerializeField] private bool  _smoothOutput = true;
        [SerializeField] private float _smoothingSpeed = 16f;
        [SerializeField] private bool  _snapToZeroOnRelease = true;

        // ── Runtime state ─────────────────────────────────────────────────────────

        private Canvas  _parentCanvas;
        private Vector2 _rawInput;
        private Vector2 _smoothedInput;
        private float   _activeRadius;
        private Vector2 _anchorBase; // original anchored position of _background

        private bool    _isActive;

        // ── Output ────────────────────────────────────────────────────────────────

        /// <summary>Normalized joystick input in [-1,1] on both axes, post dead-zone and smoothing.</summary>
        public Vector2 Input { get; private set; }

        /// <summary>Called every frame the joystick value changes.</summary>
        public event Action<Vector2> InputChanged;

        // ── Preset lookup ─────────────────────────────────────────────────────────

        // (radius, deadZone, responseExponent)
        private static readonly (float radius, float deadZone, float curve) s_presets =
            default; // filled per-preset in GetPresetValues()

        private static (float radius, float deadZone, float curve) GetPresetValues(SensitivityPreset preset) =>
            preset switch
            {
                SensitivityPreset.Low    => (64f,  0.22f, 1.60f),
                SensitivityPreset.High   => (100f, 0.10f, 1.10f),
                _                        => (80f,  0.15f, 1.35f)  // Normal
            };

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            ApplySensitivityPreset(_sensitivity);

            if (_background != null)
                _anchorBase = _background.anchoredPosition;

            SetVisible(false);
        }

        private void Update()
        {
            if (!_smoothOutput) return;

            float step = Mathf.Max(1f, _smoothingSpeed) * Time.unscaledDeltaTime;
            Vector2 target = _snap8Directions ? Snap8(_rawInput) : _rawInput;
            _smoothedInput = Vector2.Lerp(_smoothedInput, target, step);

            if ((_smoothedInput - target).sqrMagnitude < 0.0001f)
                _smoothedInput = target;

            PublishInput(_smoothedInput);
        }

        // ── IPointerDownHandler ───────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData data)
        {
            if (_floating)
                RepositionBackground(data);

            SetVisible(true);
            _isActive = true;
            UpdateFromPointer(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!_isActive) return;
            UpdateFromPointer(data);
        }

        public void OnPointerUp(PointerEventData data)
        {
            _isActive = false;

            if (_snapToZeroOnRelease)
            {
                _rawInput = Vector2.zero;
                if (!_smoothOutput)
                    PublishInput(Vector2.zero);
            }

            if (_floating)
            {
                SetVisible(false);
                if (_background != null)
                    _background.anchoredPosition = _anchorBase;
            }
            else
            {
                if (_knob != null)
                    _knob.anchoredPosition = Vector2.zero;
            }
        }

        // ── Public controls ───────────────────────────────────────────────────────

        /// <summary>Swap sensitivity preset at runtime (e.g. from Settings screen).</summary>
        public void SetSensitivity(SensitivityPreset preset)
        {
            _sensitivity = preset;
            ApplySensitivityPreset(preset);
        }

        /// <summary>Set dead zone percentage [0.10 – 0.30]. Clamped automatically.</summary>
        public void SetDeadZone(float deadZonePercent)
        {
            _deadZone = Mathf.Clamp(deadZonePercent, 0.10f, 0.30f);
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private void ApplySensitivityPreset(SensitivityPreset preset)
        {
            (float radius, _, _) = GetPresetValues(preset);
            _activeRadius = radius;
        }

        private void UpdateFromPointer(PointerEventData data)
        {
            if (_background == null) return;

            Vector2 localPoint;
            Camera cam = data.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_background, data.position, cam, out localPoint))
                return;

            (_, float deadZone, float curve) = GetPresetValues(_sensitivity);
            float effectiveDead = Mathf.Max(_deadZone, deadZone);

            Vector2 normalized = localPoint / Mathf.Max(1f, _activeRadius);
            normalized = Vector2.ClampMagnitude(normalized, 1f);
            _rawInput  = ApplyDeadZoneAndCurve(normalized, effectiveDead, curve);

            if (_knob != null)
                _knob.anchoredPosition = Vector2.ClampMagnitude(localPoint, _activeRadius);

            if (!_smoothOutput)
                PublishInput(_snap8Directions ? Snap8(_rawInput) : _rawInput);
        }

        private void RepositionBackground(PointerEventData data)
        {
            if (_background == null || _parentCanvas == null) return;

            Camera uiCam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _parentCanvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvas.transform as RectTransform,
                data.position,
                uiCam,
                out Vector2 localPoint);

            // Clamp so the joystick background doesn't fall off screen
            RectTransform canvasRect = _parentCanvas.transform as RectTransform;
            if (canvasRect != null)
            {
                float hw = canvasRect.rect.width  * 0.5f - _floatEdgePadding;
                float hh = canvasRect.rect.height * 0.5f - _floatEdgePadding;
                localPoint.x = Mathf.Clamp(localPoint.x, -hw, 0f); // left half only
                localPoint.y = Mathf.Clamp(localPoint.y, -hh, hh);
            }

            _background.anchoredPosition = localPoint;
        }

        private void PublishInput(Vector2 value)
        {
            value = Vector2.ClampMagnitude(value, 1f);
            if (value == Input) return;
            Input = value;
            InputChanged?.Invoke(Input);
        }

        private static Vector2 ApplyDeadZoneAndCurve(Vector2 input, float deadZone, float curve)
        {
            float magnitude = input.magnitude;
            if (magnitude <= deadZone) return Vector2.zero;
            float normalized = Mathf.InverseLerp(deadZone, 1f, magnitude);
            float curved     = Mathf.Pow(normalized, Mathf.Max(0.01f, curve));
            return input.normalized * curved;
        }

        /// <summary>Rounds a continuous vector to the nearest 45° axis (8 directions).</summary>
        private static Vector2 Snap8(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector2.zero;
            float angle   = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(angle / 45f) * 45f;
            float rad     = snapped * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * input.magnitude;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
            }
            else if (_background != null)
            {
                _background.gameObject.SetActive(visible || !_floating);
            }
        }

        private void OnValidate()
        {
            _deadZone       = Mathf.Clamp(_deadZone, 0.10f, 0.30f);
            _smoothingSpeed = Mathf.Max(1f, _smoothingSpeed);
        }
    }
}
