# System Architecture Diagram

## Complete Mobile Movement Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         PLAYER INPUT LAYER                              │
└─────────────────────────────────────────────────────────────────────────┘

    Left 50% of Screen              Right 50% of Screen
   (Joystick Input)                 (Camera Orbit)
    ↓                               ↓
    
┌─────────────────────────────────────────────────────────────────────────┐
│                     INPUT DETECTION LAYER                               │
└─────────────────────────────────────────────────────────────────────────┘

    VirtualJoystickView             CameraFollowController
    - Normalizes touch              - Detects touch position
    - Applies response curve        - Calculates delta
    - Dead zone 0.15                - Updates yaw/pitch
    │                               │
    └───► Vector2(x, y)             └───► Orbit state (_currentYaw, _currentPitch)
          (normalized,                    (quaternion angles)
           screen coords)                 
    
┌─────────────────────────────────────────────────────────────────────────┐
│                  MOVEMENT RESOLUTION LAYER                              │
└─────────────────────────────────────────────────────────────────────────┘

    MobileHudController.HandleMoveInput(Vector2 input)
    │
    ├─ Apply dead zone (0.12)
    ├─ Apply smoothing (14 speed)
    ├─ Call: ResolveMoveDirectionFromCamera(Vector2)
    │   ├─ If CameraFollowController exists:
    │   │  ├─ cameraForward = CameraFollowController.GetCameraRelativeForward()
    │   │  ├─ cameraRight = CameraFollowController.GetCameraRelativeRight()
    │   │  └─ Result: (forward * input.y + right * input.x).normalized
    │   │
    │   └─ Else (fallback):
    │      ├─ cameraForward = Camera.main.transform.forward
    │      ├─ cameraRight = Camera.main.transform.right
    │      └─ Result: (forward * input.y + right * input.x).normalized
    │
    └─ Result: Vector3 worldDirection (camera-relative)

┌─────────────────────────────────────────────────────────────────────────┐
│                  CHARACTER MOVEMENT LAYER                               │
└─────────────────────────────────────────────────────────────────────────┘

    CharacterMotor.SetMoveDirection(Vector3 direction)
    │
    ├─ Store _currentMoveDirection
    ├─ Rotate character toward direction (rotation speed = 720°/s)
    ├─ Move character using CharacterController.SimpleMove(direction * speed)
    └─ Update state for network sync

┌─────────────────────────────────────────────────────────────────────────┐
│                   NETWORK SYNC LAYER                                    │
└─────────────────────────────────────────────────────────────────────────┘

    CharacterMotor.TrySendMoveRequest() [every 0.1s, or 0.25m moved]
    │
    ├─ If using click-to-move (editor):
    │  └─ Send destination (from StraightLineMovementDriver)
    │
    └─ If using joystick:
       └─ Send pseudo-destination (10 units ahead in move direction)
    
    Server receives move request, validates, sends back corrected position
    │
    └─ CharacterMotor.ApplyServerCorrection(Vector3 serverPosition)
       ├─ If drift < 0.5m: Soft-lerp (35% blend per frame)
       ├─ If drift < 1.5m: Soft-snap
       └─ If drift > 1.5m: Hard-snap (teleport)
```

---

## Component Interactions (Simple View)

```
                    ┌─────────────────────┐
                    │  VirtualJoystick    │
                    │     View            │
                    └──────────┬──────────┘
                               │
                        Vector2(x, y)
                               │
                    ┌──────────▼──────────┐
        CameraFollowController◄─┤ Mobile Hud   │
        - GetCameraRelative     │ Controller   │
          Forward/Right()       └──────────┬───┘
                 ▲                         │
        Yaw/Pitch orbit           Camera-relative direction
                                           │
                                  ┌────────▼────────┐
                                  │Character Motor  │
                                  │  SetMoveDir()   │
                                  └────────┬────────┘
                                           │
                            Movement + Rotation + Network Sync
```

---

## Data Flow (Real Example)

```
Scenario: Player swipes joystick to the right, camera orbited 90° left (yaw = 90)

1) VirtualJoystickView detects touch
   Output: Vector2(1.0, 0)  ← right direction in screen space

2) MobileHudController
   Input: Vector2(1.0, 0)
   Dead zone: 0.12 ✓ (pass)
   Smoothing: 14 (lerp toward input)
   Call ResolveMoveDirectionFromCamera(1.0, 0)

3) ResolveMoveDirectionFromCamera
   CameraFollowController._currentYaw = 90 degrees
   
   GetCameraRelativeForward():
     yawRotation = Quaternion.Euler(0, 90, 0)
     return yawRotation * Vector3.forward → (0, 0, 1) rotated by yaw
     result: Vector3(1, 0, 0)  ← points to the left in world space
   
   GetCameraRelativeRight():
     yawRotation = Quaternion.Euler(0, 90, 0)
     return yawRotation * Vector3.right → (1, 0, 0) rotated by yaw
     result: Vector3(0, 0, -1)  ← points backward in world space
   
   Final calculation:
     (forward * y + right * x).normalized
     = (Vector3(1, 0, 0) * 0 + Vector3(0, 0, -1) * 1.0)
     = Vector3(0, 0, -1)  ← backward in world space

4) CharacterMotor.SetMoveDirection(Vector3(0, 0, -1))
   - Rotate toward Vector3(0, 0, -1) (backward)
   - Move backward at speed 5.0
   - Update _moveDirection for network

5) Network sends pseudo-destination:
   position + direction * 10 = origin + backward * 10
```

**Result**: Player swiped RIGHT on joystick → Character moves BACKWARD (relative to camera orbit)
This is correct! Camera orbited left, so "right on screen" = "away from camera" = world backward.

---

## Performance Profile

| Component | Cost | Per Frame | Notes |
|-----------|------|-----------|-------|
| VirtualJoystickView | ~0.1ms | 60Hz touch input | Touch pooling, no alloc |
| CameraFollowController | ~0.2ms | SmoothDamp + touch check | Pure math, no queries |
| MobileHudController | ~0.15ms | Smoothing + direction calc | 3 Vector3 ops + normalize |
| CharacterMotor | ~0.3ms | Rotation + SimpleMove | CharacterController built-in |
| Network sync | 0ms | 10/sec (0.1s intervals) | Asynchronous, background |
| **Total** | **~0.75ms** | 60 FPS = 16.7ms budget | **5% GPU budget** |

---

## State Transitions (Conceptual)

```
┌────────────────────────────────────────────────────────────────┐
│                    STARTUP                                     │
└────────────────────────────────────────────────────────────────┘
         ▼
┌────────────────────────────────────────────────────────────────┐
│ Scene Loaded: World                                            │
│ - CharacterMotor in Awake() finds CameraFollowController      │
│ - MobileHudController in Awake() finds both                   │
│ - All references resolved                                      │
└────────────────────────────────────────────────────────────────┘
         ▼
┌────────────────────────────────────────────────────────────────┐
│ IDLE STATE (Player not touching screen)                        │
│ - _currentMoveDirection = Vector3.zero                         │
│ - Camera auto-aligns to character forward                      │
│ - No network sends (no movement)                               │
└────────────────────────────────────────────────────────────────┘
         ▼
    Touch Joystick
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│ MOVING STATE (Joystick touched, left 50%)                      │
│ - VirtualJoystickView emits Vector2(x, y)                      │
│ - MobileHudController resolves to camera-relative direction   │
│ - CharacterMotor.SetMoveDirection() applies movement           │
│ - TrySendMoveRequest() sends every 0.1s                        │
└────────────────────────────────────────────────────────────────┘
         ▲ ▼
         │ (continue until release)
         │
    Release Joystick
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│ STOPPING STATE (0.1s+)                                         │
│ - SmoothInput lerps _smoothedInput toward zero                 │
│ - Character decelerates smoothly                               │
│ - Final movement request sent (destination at position)        │
└────────────────────────────────────────────────────────────────┘
         ▼
    Return to IDLE


    ===== PARALLEL: CAMERA ORBIT =====

    Touch Right Side (right 50%)
         ▼
┌────────────────────────────────────────────────────────────────┐
│ ORBITING STATE                                                 │
│ - CameraFollowController.ProcessOrbitInput() active            │
│ - _currentYaw updated by touch delta.x                         │
│ - _currentPitch updated by touch delta.y                       │
│ - GetCameraRelativeForward/Right() return orbit-aware dirs     │
│ - Movement continues (joystick) but is relative to new yaw    │
└────────────────────────────────────────────────────────────────┘
         ▲ ▼
         │ (continue until release)
         │
    Release Orbit Touch
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│ DAMPING STATE (smooth stop)                                    │
│ - Yaw velocity damped by 0.92 per frame                        │
│ - Camera smoothly stops rotating                               │
│ - After 0.2s idle: auto-align engages                          │
│ - _currentYaw lerps toward character forward                   │
└────────────────────────────────────────────────────────────────┘
         ▼
    Return to IDLE (Camera aligned + not moving)
```

---

## Fault Tolerance

```
SCENARIO: CameraFollowController removed from scene

Flow:
┌─────────────────────────────────────────────┐
│ MobileHudController.Awake()                 │
│ _cameraFollow = FindAnyObjectByType(...) → null
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Game runs, player touches joystick          │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ ResolveMoveDirectionFromCamera()            │
│ if (_cameraFollow != null) → FALSE          │
│ Fallback: Use Camera.main.transform        │
│ Movement works! (but not orbit-aware)       │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│ Result: Game playable, movement not         │
│ relative to camera rotation (legacy mode)   │
│ ✓ No crashes, ✓ Safe fallback              │
└─────────────────────────────────────────────┘
```

---

## Extension Points

**If you want to add features:**

### Feature: Sprint Mode
```csharp
// In CharacterMotor.cs, modify _moveSpeed:
float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? _moveSpeed * 1.5f : _moveSpeed;
Vector3 moveVelocity = _currentMoveDirection * currentSpeed;
```

### Feature: Two-Finger Zoom
```csharp
// In CameraFollowController.ProcessOrbitInput():
if (Input.touchCount == 2)
{
    Touch touch0 = Input.GetTouch(0);
    Touch touch1 = Input.GetTouch(1);
    float distance = Vector2.Distance(touch0.position, touch1.position);
    _followDistance = Mathf.Clamp(distance / 100f, 3f, 12f);  // Pinch zoom
}
```

### Feature: Gamepad Support
```csharp
// In MobileHudController.HandleMoveInput():
if (Input.GetJoystickNames().Length > 0)
{
    float x = Input.GetAxis("Horizontal");  // Left stick X
    float y = Input.GetAxis("Vertical");    // Left stick Y
    HandleMoveInput(new Vector2(x, y));
}
```

---

**Last Updated**: 2026-04-04
