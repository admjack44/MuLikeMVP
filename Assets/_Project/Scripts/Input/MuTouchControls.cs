using System;
using System.Collections.Generic;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using MuLike.UI;
using UiJoystick = MuLike.UI.Joystick;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace MuLike.Input
{
    /// <summary>
    /// Central touch input orchestrator for MU Online Mobile.
    ///
    /// Handles everything that is NOT already delegated to EventSystem-based UI widgets:
    ///   • Pinch-to-zoom      → <see cref="CameraFollowController.FollowDistance"/>
    ///   • Two-finger rotate  → <see cref="CameraFollowController.AddYawDelta"/> with optional 45° snap on lift
    ///   • Smart Camera mode  → <see cref="CameraFollowController.SmartCameraEnabled"/>
    ///   • One-Handed mode    → moves the right-side button cluster to one side of the screen
    ///   • Combat auto-hide   → fades non-critical HUD panels during a user-configurable idle window
    ///   • World tap          → raycast → <see cref="TargetingController.SetManualTarget"/>
    ///   • Swipe on right-half → cycle through nearby target candidates
    ///   • Phone vs. tablet   → scale the HUD canvas based on estimated screen diagonal
    ///
    /// Dependencies (auto-discovered, or assign in Inspector):
    ///   <see cref="Joystick"/>                 — floating left-side joystick
    ///   <see cref="CameraFollowController"/>    — wraps the camera rig
    ///   <see cref="TargetingController"/>       — target lock management
    ///   <see cref="CharacterMotor"/>            — receives joystick input
    /// </summary>
    public sealed class MuTouchControls : MonoBehaviour
    {
        // ── Enums ──────────────────────────────────────────────────────────────────

        /// <summary>Determines how the HUD root scale is chosen on startup.</summary>
        public enum ScreenSizeMode { Auto, ForcePhone, ForceTablet }

        // ── Inspector ──────────────────────────────────────────────────────────────

        [Header("Dependencies")]
        [SerializeField] private UiJoystick              _joystick;
        [SerializeField] private CameraFollowController  _camera;
        [SerializeField] private TargetingController     _targeting;
        [SerializeField] private CharacterMotor          _characterMotor;

        [Header("Pinch-to-Zoom")]
        [SerializeField] private bool  _enablePinchZoom = true;
        [SerializeField] private float _pinchSensitivity = 0.02f;

        [Header("Two-Finger Rotate")]
        [SerializeField] private bool  _enableTwoFingerRotate = true;
        [Tooltip("Snap camera yaw to the nearest 45° when the gesture ends.")]
        [SerializeField] private bool  _snapYawOn45 = true;
        [SerializeField] private float _rotateSensitivity = 0.4f;

        [Header("Target — Tap")]
        [Tooltip("World-space ray against this layer mask to detect tappable entities.")]
        [SerializeField] private LayerMask    _tappableLayer = ~0;
        [SerializeField] private float        _tapMaxDistance = 50f;
        [Tooltip("Max touch travel in pixels to count as a tap (not a drag).")]
        [SerializeField] private float        _tapMaxMovementPx = 18f;
        [Tooltip("Max duration in seconds to count as a tap.")]
        [SerializeField] private float        _tapMaxDurationSec = 0.25f;

        [Header("Target — Swipe Cycle")]
        [Tooltip("Swipe this many pixels horizontally on the right half to cycle targets.")]
        [SerializeField] private float        _swipeMinDistancePx = 60f;
        [SerializeField] private float        _swipeMaxDurationSec = 0.4f;
        [Tooltip("Search radius (metres) when cycling targets.")]
        [SerializeField] private float        _cycleTargetSearchRadius = 24f;

        [Header("Mount / Muun Button")]
        [Tooltip("Double-tap interval in seconds to activate mount/muun.")]
        [SerializeField] private float        _doubleTapIntervalSec = 0.3f;

        [Header("Combat Auto-Hide")]
        [SerializeField] private bool         _enableCombatAutoHide = true;
        [Tooltip("HUD panels to fade out when the player stops interacting.")]
        [SerializeField] private CanvasGroup[] _autoHidePanels = Array.Empty<CanvasGroup>();
        [SerializeField] private float         _hudAutoHideDelaySec  = 4f;
        [SerializeField] private float         _hudFadeOutDuration    = 0.5f;
        [SerializeField] private float         _hudFadeInDuration     = 0.2f;

        [Header("One-Handed Mode")]
        [Tooltip("RectTransform whose AnchorMin/Max is shifted for one-handed compact layout.")]
        [SerializeField] private RectTransform _rightButtonCluster;
        [SerializeField] private bool          _oneHandedMode = false;
        [Tooltip("0 = full-right, 1 = full-left (shifts anchor and position).")]
        [SerializeField, Range(-1f, 0f)] private float _oneHandedOffsetX = -0.3f;

        [Header("Screen Scale (Phone vs Tablet)")]
        [SerializeField] private ScreenSizeMode _screenSizeMode = ScreenSizeMode.Auto;
        [Tooltip("Screen diagonal in inches at which the layout switches to tablet scale.")]
        [SerializeField] private float          _tabletDiagonalInches = 7f;
        [SerializeField] private RectTransform  _hudScaleRoot;
        [SerializeField] private float          _phoneScale  = 1.0f;
        [SerializeField] private float          _tabletScale = 1.3f;

        // ── Events ─────────────────────────────────────────────────────────────────

        /// <summary>Fires when the local player taps a world entity whose EntityView has been identified.</summary>
        public event Action<EntityView> OnEntityTapped;

        /// <summary>Fires when the screen size mode is resolved (phone or tablet).</summary>
        public event Action<bool> OnTabletModeResolved;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private Camera _mainCamera;

        // Pinch / rotate state
        private bool  _wasTwoFingersLastFrame;
        private float _prevPinchDistance;
        private float _prevTwoFingerAngleDeg;

        // Tap tracking — per-finger
        private readonly Dictionary<int, TapTracker> _tapTrackers = new();

        // Swipe tracking
        private SwipeTracker _swipeTracker;

        // Auto-hide
        private float _hideTimerRemaining;
        private bool  _hudHidden;

        // Double-tap
        private float _lastMountTapAt = -99f;

        // Target cycling cache
        private readonly Collider[] _cycleBuffer = new Collider[32];
        private readonly List<EntityView> _cycleList = new(32);

        // ── Structs ────────────────────────────────────────────────────────────────

        private struct TapTracker
        {
            public int     FingerId;
            public Vector2 StartPosition;
            public float   StartTime;
            public bool    Consumed; // marked true if finger exceeded tap constraints
        }

        private struct SwipeTracker
        {
            public bool    Active;
            public Vector2 StartPosition;
            public float   StartTime;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _mainCamera = Camera.main;
            AutoDiscoverDependencies();
            ApplyScreenScale();
            ApplyOneHandedMode(_oneHandedMode);
            ResetAutoHideTimer();
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
            if (_joystick != null)
                _joystick.InputChanged += HandleJoystickInput;
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Disable();
#endif
            if (_joystick != null)
                _joystick.InputChanged -= HandleJoystickInput;
        }

        private void Update()
        {
            ProcessTouches();
            TickAutoHide();
        }

        // ── Public controls ────────────────────────────────────────────────────────

        public void SetOneHandedMode(bool enabled)
        {
            _oneHandedMode = enabled;
            ApplyOneHandedMode(enabled);
        }

        public void SetSmartCamera(bool enabled)
        {
            if (_camera != null)
                _camera.SmartCameraEnabled = enabled;
        }

        /// <summary>Reveal hidden HUD panels immediately (e.g. on any HUD tap).</summary>
        public void NotifyHudActivity()
        {
            ShowHudPanels();
            ResetAutoHideTimer();
        }

        // ── Joystick passthrough ───────────────────────────────────────────────────

        private void HandleJoystickInput(Vector2 input)
        {
            _characterMotor?.SetJoystickInput(input);
            NotifyHudActivity();
        }

        // ── Multi-touch main loop ──────────────────────────────────────────────────

        private void ProcessTouches()
        {
#if ENABLE_INPUT_SYSTEM
            var activeTouches = EnhancedTouch.Touch.activeTouches;
            int count = activeTouches.Count;

            if (count >= 2)
            {
                EnhancedTouch.Touch t0 = activeTouches[0];
                EnhancedTouch.Touch t1 = activeTouches[1];
                ProcessTwoFingerGesture(t0.screenPosition, t1.screenPosition);
                _wasTwoFingersLastFrame = true;
                return;
            }

            if (_wasTwoFingersLastFrame)
                OnTwoFingerGestureLifted();

            _wasTwoFingersLastFrame = false;

            if (count == 1)
            {
                EnhancedTouch.Touch t = activeTouches[0];
                int id = t.touchId;
                if (t.began)       BeginTap(id, t.screenPosition);
                else if (t.ended)  EndTap(id, t.screenPosition);
                else               UpdateSwipe(t.screenPosition, t.delta);
            }
            else
            {
                CancelAllTaps();
                _swipeTracker.Active = false;
            }
#else
            int count = UnityEngine.Input.touchCount;

            if (count >= 2)
            {
                Vector2 p0 = UnityEngine.Input.GetTouch(0).position;
                Vector2 p1 = UnityEngine.Input.GetTouch(1).position;
                ProcessTwoFingerGesture(p0, p1);
                _wasTwoFingersLastFrame = true;
                return;
            }

            if (_wasTwoFingersLastFrame)
                OnTwoFingerGestureLifted();

            _wasTwoFingersLastFrame = false;

            if (count == 1)
            {
                Touch t = UnityEngine.Input.GetTouch(0);
                int   id = t.fingerId;
                if (t.phase == TouchPhase.Began)       BeginTap(id, t.position);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) EndTap(id, t.position);
                else                                   UpdateSwipe(t.position, t.deltaPosition);
            }
            else
            {
                CancelAllTaps();
                _swipeTracker.Active = false;
            }
#endif
        }

        // ── Two-finger gesture: pinch + rotate ─────────────────────────────────────

        private void ProcessTwoFingerGesture(Vector2 p0, Vector2 p1)
        {
            if (_camera == null) return;

            _camera.IsOrbitOverridden = true;

            float  currentDist  = Vector2.Distance(p0, p1);
            float  currentAngle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;

            if (_wasTwoFingersLastFrame)
            {
                // Pinch-to-zoom
                if (_enablePinchZoom)
                {
                    float distDelta = currentDist - _prevPinchDistance;
                    _camera.FollowDistance -= distDelta * _pinchSensitivity;
                }

                // Two-finger rotate
                if (_enableTwoFingerRotate)
                {
                    float angleDelta = Mathf.DeltaAngle(_prevTwoFingerAngleDeg, currentAngle);
                    _camera.AddYawDelta(-angleDelta * _rotateSensitivity);
                }
            }

            _prevPinchDistance      = currentDist;
            _prevTwoFingerAngleDeg  = currentAngle;
        }

        private void OnTwoFingerGestureLifted()
        {
            if (_camera == null) return;
            _camera.IsOrbitOverridden = false;

            if (_snapYawOn45)
                _camera.SnapYawToNearest45();
        }

        // ── Tap detection ──────────────────────────────────────────────────────────

        private void BeginTap(int fingerId, Vector2 screenPos)
        {
            _tapTrackers[fingerId] = new TapTracker
            {
                FingerId      = fingerId,
                StartPosition = screenPos,
                StartTime     = Time.unscaledTime,
                Consumed      = IsPointerOnUI(screenPos)  // UI touches don't start world-tap tracking
            };

            // Begin swipe tracking on right side only
            if (screenPos.x > Screen.width * 0.5f && !_tapTrackers[fingerId].Consumed)
            {
                _swipeTracker = new SwipeTracker
                {
                    Active        = true,
                    StartPosition = screenPos,
                    StartTime     = Time.unscaledTime
                };
            }
        }

        private void EndTap(int fingerId, Vector2 screenPos)
        {
            if (!_tapTrackers.TryGetValue(fingerId, out TapTracker tracker))
                return;

            _tapTrackers.Remove(fingerId);

            float elapsed  = Time.unscaledTime - tracker.StartTime;
            float traveled = Vector2.Distance(tracker.StartPosition, screenPos);

            bool isCleanTap = !tracker.Consumed
                              && elapsed  <= _tapMaxDurationSec
                              && traveled <= _tapMaxMovementPx;

            if (isCleanTap)
            {
                // Check whether swipe criteria are met first
                bool isSwipe = _swipeTracker.Active
                               && elapsed <= _swipeMaxDurationSec
                               && Mathf.Abs(screenPos.x - _swipeTracker.StartPosition.x) >= _swipeMinDistancePx;

                if (isSwipe && screenPos.x > Screen.width * 0.5f)
                {
                    float dx = screenPos.x - _swipeTracker.StartPosition.x;
                    CycleTarget(dx > 0f ? 1 : -1);
                }
                else
                {
                    TryWorldTap(screenPos);
                }
            }

            _swipeTracker.Active = false;
            NotifyHudActivity();
        }

        private void UpdateSwipe(Vector2 screenPos, Vector2 delta)
        {
            // Mark taps as consumed if they've moved too far
            foreach (int key in new List<int>(_tapTrackers.Keys))
            {
                TapTracker t = _tapTrackers[key];
                if (!t.Consumed && Vector2.Distance(t.StartPosition, screenPos) > _tapMaxMovementPx)
                {
                    t.Consumed = true;
                    _tapTrackers[key] = t;
                }
            }
        }

        private void CancelAllTaps()
        {
            _tapTrackers.Clear();
            _swipeTracker.Active = false;
        }

        // ── World tap → set target ─────────────────────────────────────────────────

        private void TryWorldTap(Vector2 screenPos)
        {
            if (_targeting == null || _mainCamera == null) return;

            Ray ray = _mainCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, _tapMaxDistance, _tappableLayer, QueryTriggerInteraction.Collide))
            {
                EntityView view = hit.collider.GetComponentInParent<EntityView>();
                if (view != null)
                {
                    _targeting.SetManualTarget(view);
                    OnEntityTapped?.Invoke(view);
                }
            }
        }

        // ── Target cycling ─────────────────────────────────────────────────────────

        /// <summary>
        /// Cycles to the nearest target clockwise (direction=+1) or counter-clockwise (direction=-1)
        /// relative to the current target's position, on the horizontal plane.
        /// </summary>
        private void CycleTarget(int direction)
        {
            if (_targeting == null) return;

            Transform origin = _characterMotor != null
                ? _characterMotor.transform
                : (_camera != null ? _camera.transform : null);

            if (origin == null) return;

            // Collect all candidate entities in range
            _cycleList.Clear();
            int found = Physics.OverlapSphereNonAlloc(
                origin.position,
                _cycleTargetSearchRadius,
                _cycleBuffer,
                _tappableLayer,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < found; i++)
            {
                EntityView view = _cycleBuffer[i]?.GetComponentInParent<EntityView>();
                if (view != null && _targeting.IsTargetValid(view))
                    _cycleList.Add(view);
            }

            if (_cycleList.Count == 0) return;

            // Sort by signed angle around player vertical axis (horizontal plane)
            Vector3 forward = Vector3.ProjectOnPlane(origin.forward, Vector3.up).normalized;
            _cycleList.Sort((a, b) =>
            {
                float angleA = SignedAngleFlat(origin.position, a.transform.position, forward);
                float angleB = SignedAngleFlat(origin.position, b.transform.position, forward);
                return angleA.CompareTo(angleB);
            });

            // Find the next entity after the current target in the sorted ring
            int currentIdx = -1;
            EntityView currentTarget = _targeting.CurrentTarget;
            for (int i = 0; i < _cycleList.Count; i++)
            {
                if (_cycleList[i] == currentTarget)
                {
                    currentIdx = i;
                    break;
                }
            }

            int nextIdx = (currentIdx + direction + _cycleList.Count) % _cycleList.Count;
            _targeting.SetManualTarget(_cycleList[nextIdx]);
        }

        private static float SignedAngleFlat(Vector3 from, Vector3 to, Vector3 forward)
        {
            Vector3 dir = Vector3.ProjectOnPlane(to - from, Vector3.up);
            return Vector3.SignedAngle(forward, dir, Vector3.up);
        }

        // ── Mount / Muun double-tap ────────────────────────────────────────────────

        /// <summary>
        /// Call this from the Mount/Muun button's click handler.
        /// Returns true on the second tap within the double-tap window.
        /// </summary>
        public bool RegisterMountTap()
        {
            float now = Time.unscaledTime;
            bool isDouble = now - _lastMountTapAt <= _doubleTapIntervalSec;
            _lastMountTapAt = now;
            return isDouble;
        }

        // ── HUD auto-hide ──────────────────────────────────────────────────────────

        private void TickAutoHide()
        {
            if (!_enableCombatAutoHide || _autoHidePanels.Length == 0) return;

            if (_hudHidden) return;

            _hideTimerRemaining -= Time.unscaledDeltaTime;
            if (_hideTimerRemaining <= 0f)
                FadeOutHudPanels();
        }

        private void ShowHudPanels()
        {
            if (!_hudHidden) return;
            _hudHidden = false;
            SetHudAlpha(1f);
        }

        private void FadeOutHudPanels()
        {
            _hudHidden = true;
            SetHudAlpha(0f);
        }

        private void SetHudAlpha(float alpha)
        {
            foreach (CanvasGroup cg in _autoHidePanels)
            {
                if (cg != null)
                    cg.alpha = alpha;
            }
        }

        private void ResetAutoHideTimer() => _hideTimerRemaining = _hudAutoHideDelaySec;

        // ── One-Handed mode ────────────────────────────────────────────────────────

        private void ApplyOneHandedMode(bool oneHanded)
        {
            if (_rightButtonCluster == null) return;

            if (oneHanded)
            {
                // Shift the button cluster left by the configured offset amount
                Vector2 anchorMin = _rightButtonCluster.anchorMin;
                Vector2 anchorMax = _rightButtonCluster.anchorMax;
                _rightButtonCluster.anchorMin = new Vector2(anchorMin.x + _oneHandedOffsetX, anchorMin.y);
                _rightButtonCluster.anchorMax = new Vector2(anchorMax.x + _oneHandedOffsetX, anchorMax.y);
            }
            else
            {
                // Reset to default anchors (right edge)
                _rightButtonCluster.anchorMin = new Vector2(1f, 0f);
                _rightButtonCluster.anchorMax = new Vector2(1f, 0f);
            }
        }

        // ── Phone vs. tablet scaling ───────────────────────────────────────────────

        private void ApplyScreenScale()
        {
            if (_hudScaleRoot == null) return;

            bool isTablet = _screenSizeMode switch
            {
                ScreenSizeMode.ForcePhone  => false,
                ScreenSizeMode.ForceTablet => true,
                _                          => IsTablet()
            };

            float scale = isTablet ? _tabletScale : _phoneScale;
            _hudScaleRoot.localScale = new Vector3(scale, scale, 1f);
            OnTabletModeResolved?.Invoke(isTablet);
        }

        private bool IsTablet()
        {
            float dpi = Screen.dpi > 10f ? Screen.dpi : 160f; // 160 fallback if unknown
            float widthInches  = Screen.width  / dpi;
            float heightInches = Screen.height / dpi;
            float diagonal     = Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);
            return diagonal >= _tabletDiagonalInches;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private void AutoDiscoverDependencies()
        {
            if (_camera       == null) _camera       = FindAnyObjectByType<CameraFollowController>();
            if (_targeting    == null) _targeting    = FindAnyObjectByType<TargetingController>();
            if (_characterMotor == null) _characterMotor = FindAnyObjectByType<CharacterMotor>();
            if (_joystick     == null) _joystick     = FindAnyObjectByType<UiJoystick>();

            if (_mainCamera   == null) _mainCamera   = Camera.main;
        }

        private static bool IsPointerOnUI(Vector2 screenPos)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current?.RaycastAll(pointer, results);
            return results.Count > 0;
        }
    }
}
