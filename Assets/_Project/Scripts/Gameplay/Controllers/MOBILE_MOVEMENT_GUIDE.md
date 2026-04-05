# Mobile ARPG Movement System - Implementation Guide

## Overview
Complete mobile movement experience for ARPG/MMORPG (MU Immortal / Dragon Havoc style) with:
- Virtual joystick (left) for continuous directional movement
- Smooth camera follow with orbit control (right side touch)
- Camera-relative movement (joystick moves relative to camera orientation)
- Precise stop and dead zone control
- Click-to-move support (editor only for testing)
- Mobile-optimized input processing

## Architecture

### 1. CharacterMotor.cs (Refactored)

**Key Changes:**
- Separated **click-to-move** (editor/desktop only) from **joystick continuous input**
- Both input modes feed into network movement sync
- Supports cast lock during spellcasting
- Input smoothing with configurable dead zone
- Network sends "pseudo-destinations" when joystick-driven (maintains protocol compatibility)

**New Methods:**
```csharp
// Set joystick input (called by MobileHudController after camera resolution)
public void SetMoveDirection(Vector3 direction)

// Server correction (unchanged)
public void ApplyServerCorrection(Vector3 serverPosition)

// Cast state notification
public void NotifyCastingState(bool isCasting)
```

**Removed Methods:**
- `SetMoveDirection(Vector3)` → **Re-added for backward compatibility**

**Internal Flow:**
1. **Editor Click-to-Move**: Right-click raycast → `MoveToPoint()` → `StraightLineMovementDriver`
2. **Joystick Input**: `SetMoveDirection(Vector3)` → Direct movement (no destination)
3. **Network Sync**: Periodic sends position/pseudo-destination based on mode
4. **Server Correction**: Soft-lerp or hard-snap based on drift distance

**Inspector Settings (CharacterMotor):**
```
Movement:
  - Move Speed: 5.0
  - Rotation Speed: 720 (degrees/sec)
  - Stopping Distance: 0.15
  - Input Dead Zone: 0.1
  - Input Smoothing Speed: 12

Click To Move (Editor Only):
  - Enable Click To Move: true
  - Ground Mask: Default

Cast Lock:
  - Lock Movement During Cast: true

Networking:
  - Network Client: [auto-found]
  - Send Move Requests: true
  - Network Send Interval: 0.1
  - Network Send Min Distance: 0.25
  - Network Resend Interval: 0.5

Reconciliation:
  - Soft Correction Distance: 0.5
  - Hard Snap Distance: 1.5
  - Soft Correction Lerp: 0.35
```

---

### 2. CameraFollowController.cs (New)

**Purpose:**
Mobile camera controller with smooth follow, yaw/pitch orbit control, and head-tracking orientation alignment.
**No Cinemachine dependency.**

**Features:**
- **Smooth Follow**: Follows character position with configurable lag (SmoothDamp)
- **Yaw Orbit**: Right-side touch drag rotates horizontal camera (yaw)
- **Pitch Control**: Touch drag up/down adjusts vertical angle (pitch)
- **Pitch Clamp**: Min/max pitch limits (e.g., -45° to +45°)
- **Auto Alignment**: Optional automatic yaw alignment to character forward when not orbiting
- **Damping**: Smooth deceleration when touch released (not active orbit)
- **Debug Gizmos**: Visual sphere and distance indicators

**Touch Input Zone:**
- Configurable rect (default: right 50% of screen, full height)
- Prevents joystick interference
- Single touch only (no multi-touch support yet)

**Methods:**
```csharp
// Get forward direction relative to camera (for joystick movement)
public Vector3 GetCameraRelativeForward()

// Get right direction relative to camera (for joystick movement)
public Vector3 GetCameraRelativeRight()
```

**Inspector Settings (CameraFollowController):**
```
Follow:
  - Follow Distance: 6.0
  - Follow Height: 2.0
  - Follow Smooth Time: 0.25

Orbit:
  - Orbit Sensitivity: 0.5
  - Orbit Damping: 0.92

Pitch:
  - Min Pitch: -45
  - Max Pitch: 45
  - Pitch Sensitivity: 0.5

Auto Alignment:
  - Auto Align To Target: true
  - Auto Align Strength: 0.08

Orbit Input:
  - Orbit Input Rect Min X: 0.5 (right half)
  - Orbit Input Rect Min Y: 0.0
  - Orbit Input Rect Width: 0.5
  - Orbit Input Rect Height: 1.0

Debug:
  - Draw Debug Gizmos: false
```

---

### 3. MobileHudController.cs (Modified)

**Key Changes:**
- Added `_cameraFollow` field (auto-found)
- Modified `ResolveMoveDirectionFromCamera()` to use CameraFollowController
- Fallback to main camera if CameraFollowController unavailable
- Maintains full backward compatibility

**Integration Points:**
1. **Joystick Input Flow**:
   - VirtualJoystickView emits Vector2 (normalized screen input)
   - MobileHudView passes to MobileHudController.HandleMoveInput()
   - Controller smooths input, applies dead zone
   - Controller resolves to world direction using `ResolveMoveDirectionFromCamera()`
   - Controller applies aim assist
   - Controller calls `CharacterMotor.SetMoveDirection(Vector3 worldDirection)`

2. **Camera Integration**:
   - If CameraFollowController exists, use its `GetCameraRelativeForward/Right()`
   - These methods account for camera yaw/pitch orbit
   - Fallback to main camera if not available

**Code Changes:**
```csharp
[SerializeField] private CameraFollowController _cameraFollow;

private void Awake()
{
    // ... existing code ...
    if (_cameraFollow == null)
        _cameraFollow = FindAnyObjectByType<CameraFollowController>();
}

public Vector3 ResolveMoveDirectionFromCamera(Vector2 input)
{
    // Prefer CameraFollowController
    if (_cameraFollow != null)
    {
        Vector3 cameraForward = _cameraFollow.GetCameraRelativeForward();
        Vector3 cameraRight = _cameraFollow.GetCameraRelativeRight();
        // ... use these for movement ...
    }
    // Fallback to main camera
}
```

---

## Scene Setup Guide

### Hierarchy Structure
```
World Scene
├── Player (Capsule / CharacterController)
│   ├── CharacterController (component)
│   ├── CharacterMotor (component)
│   └── [Animator]
│
├── Camera Rig (empty GameObject, parent of camera)
│   ├── MainCamera
│   │   └── CameraFollowController (component, attached to camera)
│   └── [optional: camera shake effects follow]
│
└── HUD Canvas
    ├── MobileHudController (component)
    ├── MobileHudView (component)
    │   ├── LeftJoystick (VirtualJoystickView)
    │   ├── SkillButtonStrip
    │   ├── ResourceBars (HP/MP/Shield/Stamina)
    │   └── [other HUD elements]
    └── [other UI panels]
```

### Step-by-Step Setup

#### Step 1: Add CameraFollowController to Camera

1. **Select MainCamera** in hierarchy
2. **Add Component**: `CameraFollowController`
3. **Inspector Configure**:
   - **Target**: Drag Player GameObject (or its parent)
   - **Follow Distance**: 6.0
   - **Follow Height**: 2.0
   - **Follow Smooth Time**: 0.25
   - **Auto Align To Target**: true
   - **Orbit Input** → ensure **Rect Min X: 0.5** (right side)

#### Step 2: Configure CharacterMotor

1. **Select Player** GameObject
2. **Inspector (CharacterMotor)**:
   - **Rotation Speed**: 720
   - **Enable Click To Move**: true (for editor testing)
   - **Lock Movement During Cast**: true
   - **Input Dead Zone**: 0.1
   - **Input Smoothing Speed**: 12

#### Step 3: Verify MobileHudController Integration

1. **Select HUD Canvas** or dedicated controller GameObject
2. **Find MobileHudController** component
3. **Inspector check**:
   - **Character Motor**: [should be auto-found or drag Player]
   - **Camera Follow**: [should be auto-found, but can manually assign]
   - **Combat Controller**: [auto-found]
   - **Targeting Controller**: [auto-found]
   - **Move Dead Zone**: 0.12
   - **Move Smoothing**: 14

#### Step 4: Verify VirtualJoystickView in MobileHudView

1. **Open MobileHudView prefab/scene**
2. **Inspector (MobileHudView)**:
   - **Left Joystick**: [should reference VirtualJoystickView]
3. **Expand LeftJoystick** (VirtualJoystickView):
   - **Radius**: 75
   - **Dead Zone**: 0.15
   - **Smooth Output**: true
   - **Smoothing Speed**: 14

#### Step 5: Test Camera + Movement

1. **Play Scene**
2. **Movement Test**:
   - Touch left joystick, move in any direction
   - Character should move **relative to camera forward/right**
   - Character should rotate toward move direction
3. **Camera Orbit Test**:
   - Touch right side of screen, drag left/right
   - Camera yaw should orbit around character
   - Touch drag up/down → camera pitch rotates
   - Release → camera smoothly stops (damping)
4. **Editor Click-to-Move** (if enabled):
   - Right-click on ground → character pathfinds (straight line)
   - Should work alongside joystick

---

## Input Flow Diagram

```
┌──────────────────┐
│ Touch Input      │
└────────┬─────────┘
         │
    ┌────▼─────────────────┐
    │ Left 50%: Joystick   │ Right 50%: Camera Orbit
    │ (VirtualJoystickView)│ (CameraFollowController)
    └────┬────────────────┬┘
         │                │
    ┌────▼─────┐   ┌──────▼──────┐
    │ Vector2   │   │ Touch Delta  │
    │ (Screen)  │   │ (yaw/pitch)  │
    └────┬─────┘   └──────┬───────┘
         │                │
    ┌────▼────────────────▼────┐
    │ MobileHudController      │
    │ • Smooth input (14fps)   │
    │ • Apply dead zone (0.12) │
    │ • Apply aim assist       │
    │ • Resolve to world dir   │ ← Uses CameraFollowController
    └────┬───────────────────┘
         │
    ┌────▼──────────────┐
    │ CharacterMotor    │
    │ SetMoveDirection()│
    └────┬──────────────┘
         │
    ┌────▼──────────────────────────┐
    │ Movement                       │
    │ • Rotate toward direction      │
    │ • Move at speed                │
    │ • Send to network (0.1s tick)  │
    │ • Reconcile with server        │
    └────────────────────────────────┘
```

---

## Network Integration

**Movement Send Policy:**
- **Click-to-Move**: Destination-based (point-and-go)
- **Joystick**: Pseudo-destination (10 units ahead in move direction)
- **Frequency**: Every 0.1 seconds, or if destination moved > 0.25 units
- **Resend**: Every 0.5 seconds (keep-alive)

**Reconciliation:**
- Server returns corrected position in move result
- **Drift < 0.5m**: Soft-lerp (35% blend per frame)
- **Drift 0.5m - 1.5m**: Soft-snap
- **Drift > 1.5m**: Hard-snap (teleport)

---

## Debugging

### Gizmos

**Enable in Inspector:**
- **CharacterMotor** → Draw Debug Gizmos (shows cyan destination sphere + line if click-to-move active)
- **CameraFollowController** → Draw Debug Gizmos (shows yellow follow distance sphere)

### Debug Logs

```
[CharacterMotor] Movement locked during cast
[CharacterMotor] Network move sent: destination
[CharacterMotor] Server correction: soft-lerp / hard-snap
[MobileHudController] Aim assist applied
[CameraFollowController] Orbit input detected
```

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Joystick moves camera instead of character | Orbit zone overlaps joystick zone | Adjust `Orbit Input Rect Min X` to 0.5+ |
| Movement feels laggy | Input smoothing too high | Reduce `Move Smoothing` (default 14) |
| Character points wrong direction | Camera relative broken | Ensure CameraFollowController exists + assigned |
| Network corrections jitter | Server send rate too low | Reduce `Network Send Interval` |
| Cast lock not working | System not calling NotifyCastingState | Check CombatController calls `CharacterMotor.NotifyCastingState()` |

---

## Mobile-Specific Considerations

### Performance
- **Dead Zone**: Prevents constant tiny inputs (frame-saving)
- **Input Smoothing**: Batches touch updates (cheaper than per-frame processing)
- **Network Throttle**: 0.1s interval prevents packet spam
- **Camera Follow**: SmoothDamp is cheaper than Cinemachine

### Touch Responsiveness
- **Joystick Dead Zone**: 0.15 (adjust if feels sluggish)
- **Smoothing Speed**: 14 (adjust for snappy vs smooth feel)
- **Response Exponent**: 1.35 (curves response for better control near center)

### Safe Area
- Ensure Canvas has **CanvasScaler** with **Safe Area Fitter**
- Joystick and orbit zones respect safe area automatically

### Landscape vs Portrait
- Current setup assumes **landscape orientation**
- For portrait: Adjust orbit zone rect and joystick position
- Use Screen.width/height comparison to auto-adjust at runtime

---

## Future Enhancements

1. **Sprint/Roll**: Add sprint mode (double tap, hold modifier)
2. **Right-Stick Camera**: Direct camera control instead of touch drag
3. **Gamepad Support**: Xbox controller mapping for emulator/controller play
4. **Path Prediction**: Show path arc for aimed skills
5. **Ability Indicators**: Show ability range/AoE during aiming
6. **Click-to-Move Prediction**: Predict movement path on right-click
7. **Dynamic Orbit Damping**: Speed-based damping (faster = more responsive)

---

## Testing Checklist

- [ ] Left joystick moves character forward/back/left/right
- [ ] Character rotates toward movement direction
- [ ] Movement is camera-relative (move forward = toward camera, not world forward)
- [ ] Right side touch drag orbits camera (yaw)
- [ ] Top/bottom drag adjusts pitch (clamps between -45/+45)
- [ ] Releasing touch stops orbit smoothly (damping works)
- [ ] Auto-align re-engages when not orbiting
- [ ] Network movement sends every 0.1 seconds
- [ ] Server corrections snap when far (>1.5m)
- [ ] Cast lock blocks movement when spell is casting
- [ ] Click-to-move works in editor (right-click)
- [ ] FPS stable 45-60 on target device

---

**Last Updated**: 2026-04-04  
**Status**: Ready for integration and testing
