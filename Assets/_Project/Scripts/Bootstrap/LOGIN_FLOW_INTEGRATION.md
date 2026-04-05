# MU Like MMO - Login Flow Integration Guide

## Overview
Complete login, character selection, and world entry flow for MU Like mobile MMORPG. The flow is:
**Boot Scene** → **Login Scene** → **Character Select Scene** → **World Scene** → **In-Game HUD**

## Architecture Components

### 1. Session State Management
- **ClientSessionState** (enum): Tracks client progression from disconnected to in-world
  - Values: `Disconnected`, `Connecting`, `Authenticating`, `Authenticated`, `CharacterSelection`, `EnteringWorld`, `InWorld`, `Failed`
  
- **SessionStateClient**: Lightweight service tracking account/character/world scene. Shared across scenes.
  - Properties: `State`, `IsAuthenticated`, `AccountId`, `CharacterId`, `SessionToken`, `CurrentWorldScene`
  - Events: `StateChanged`, `StateTransitioned(previous, current)`

- **ClientFlowFeedbackService**: Manages loading/error UI feedback without coupling to views.
  - States: `Idle`, `Loading`, `Error`
  - Methods: `ShowLoading()`, `ShowError()`, `Clear()`

### 2. Login Flow (Login Scene)

**Controllers & Presenters:**
- `LoginFlowController` (MonoBehaviour, scene composition root)
  - Instantiates `SessionStateClient` and `ClientFlowFeedbackService`
  - Creates `LoginPresenter` with these services
  - Exposes properties: `SessionStateClient`, `FeedbackService`
  - On login success → calls `FrontendFlowDirector.EnterCharacterSelect()` and exports session services

- `LoginPresenter`
  - Binds view events (EnterRequested, LogoutRequested)
  - Listens to `LoginFlowService` state changes
  - Updates `SessionStateClient` state: `Connecting` → `Authenticating` → `Authenticated`
  - Uses `ClientFlowFeedbackService` to show loading/errors
  - On auth success → emits `_onLoginSuccess` callback

- `LoginView` (pure UI)
  - Exposes: `UsernameInput`, `PasswordInput`, `EnterButton`, `LogoutButton`, `StatusText`
  - Events: `EnterRequested`, `LogoutRequested`
  - No network logic

### 3. Character Select Flow (Character Select Scene)

**Controllers & Presenters:**
- `CharacterSelectFlowController` (MonoBehaviour, scene composition root)
  - Receives injected `SessionStateClient` and `ClientFlowFeedbackService` from Login scene
  - Method: `InjectSessionServices(sessionState, feedback)` — call this from LoginFlowController
  - Creates fallback services if none provided
  - Creates `CharacterSelectPresenter` with injected services
  - On character selection → updates `SessionStateClient` with character ID and world scene

- `CharacterSelectPresenter`
  - Listens to view events: `RefreshRequested`, `CreateRequested`, `CharacterSelected`, `EnterWorldRequested`
  - On bind → sets session state to `CharacterSelection`
  - On character list load → shows loading feedback
  - On "Enter World" → sets state to `EnteringWorld` and clears feedback
  - Uses `CharacterSelectService`/`NetworkCharacterSelectService` for character ops
  - Updates `SessionStateClient` with selected character and target scene

- `CharacterSelectView` (pure UI)
  - Exposes: `ListContainer`, `PreviewAnchor`, `RefreshButton`, `DeleteButton`, `EnterWorldButton`
  - Events: `RefreshRequested`, `CreateRequested`, `CharacterSelected`, `EnterWorldRequested`
  - No network logic

### 4. Scene Management

- **SceneController** (persistent, singleton)
  - Loads scenes via `SceneManager.LoadSceneAsync()`
  - Recovery fallback to "Login" if scene load fails

- **FrontendFlowDirector** (persistent, singleton)
  - High-level flow: `EnterBoot()`, `EnterLogin()`, `EnterCharacterSelect()`, `EnterWorld(sceneName)`
  - Maintains `FrontendFlowState` (authenticated, selectedCharacter, etc.)
  - Routes to `SceneController` for actual loads

- **SceneFlowService** (runtime instance)
  - Facade combining `SceneController`, `FrontendFlowDirector`, `SessionStateClient`
  - Gated by `SessionStateClient.IsAuthenticated` (prevents unauthorized scene loads)
  - Emits scene transitions

## Inspector Setup

### Boot Scene
- **FrontendFlowDirector** (DontDestroyOnLoad)
  - Boot Scene Name: "Boot"
  - Login Scene Name: "Login"
  - Character Select Scene Name: "CharacterSelect"
  - Default World Scene: "World_Dev"

### Login Scene
- **SceneController** (DontDestroyOnLoad, if not already in Boot scene)
- **NetworkGameClient**
  - Mode: InMemory (for testing) or TCP
  - TCP Host: "127.0.0.1", Port: 7777
  - Auto-reconnect: enabled
  - Auto-authenticate: enabled

- **LoginView** (UI Canvas)
  - Username Input: TMP_InputField
  - Password Input: TMP_InputField
  - Enter Button: Button
  - Logout Button: Button
  - Status Text: TextMeshProUGUI

- **LoginFlowController** (MonoBehaviour on any GameObject)
  - View: [drag LoginView from scene]
  - NetworkClient: [drag NetworkGameClient from scene]
  - FrontendFlowDirector: [auto-found or drag]
  - Load Scene On Success: true
  - Character Select Scene Name: "CharacterSelect"
  - Connect Timeout: 10000ms
  - Request Timeout: 12000ms
  - Refresh Lead: 90 sec
  - Disconnect Socket On Logout: false

### Character Select Scene
- **CharacterSelectView** (UI Canvas)
  - List Container: Transform (for character slots)
  - Item Prefab: CharacterSlotView
  - Preview Anchor: Transform (for 3D model)
  - Selected Name/Class/Level/Power Text: TextMeshProUGUI
  - Refresh/Delete/EnterWorld Buttons: Button
  - Status Text: TextMeshProUGUI
  - Create Form: InputField, Dropdown, Button
  - Mobile Layout: Portrait/Landscape roots

- **CharacterSelectFlowController** (MonoBehaviour)
  - View: [drag CharacterSelectView]
  - NetworkClient: [drag NetworkGameClient from Boot/scene]
  - Use Mock Service: true (for testing) or false (for network)
  - Load Scene On Enter World: true
  - Fallback World Scene: "World_Dev"
  - Town Scene: "Town_01"
  - FrontendFlowDirector: [auto-found or drag]

### Passing Session Services from Login to Character Select

**Option A: Via FrontendFlowDirector (Recommended)**
```csharp
// In LoginFlowController.HandleLoginSuccess():
var loginController = FindObjectOfType<LoginFlowController>();
if (loginController != null)
{
    var charSelectController = FindObjectOfType<CharacterSelectFlowController>(includeInactive: true);
    if (charSelectController != null)
    {
        charSelectController.InjectSessionServices(
            loginController.SessionStateClient,
            loginController.FeedbackService);
    }
}
```

**Option B: Via Static GameContext (if using composition root from prior phase)**
```csharp
// Store in GameContext at login time
GameContext.SessionStateClient = loginController.SessionStateClient;
GameContext.FeedbackService = loginController.FeedbackService;

// Retrieve in CharacterSelectFlowController.Awake()
if (_sessionStateClient == null && GameContext.TryGetService(out SessionStateClient sessionState))
{
    _sessionStateClient = sessionState;
}
```

## State Flow Diagram

```
Boot Scene
    ↓
    ClientSessionState.Disconnected
    ↓
[Enter Login Scene]
    ↓
    ClientSessionState.Connecting
    ↓
[User enters credentials]
    ↓
    ClientSessionState.Authenticating
    ↓
[Auth success]
    ↓
    ClientSessionState.Authenticated
    ↓
[Sessions services transferred]
    ↓
[Enter Character Select Scene]
    ↓
    ClientSessionState.CharacterSelection
    ↓
[User selects character + clicks Enter World]
    ↓
    ClientSessionState.EnteringWorld
    ↓
[Load World Scene + instantiate HUD]
    ↓
    ClientSessionState.InWorld
    ↓
[Active Gameplay]
    ↓
[User logs out]
    ↓
    ClientSessionState.Disconnected
    ↓
[Back to Login Scene]
```

## Event Integration with Existing Systems

### Network Integration
- **NetworkGameClient** manages TCP connection and packet routing
- **AuthClientService** handles token lifecycle (access + refresh)
- **LoginFlowService** orchestrates login attempt with network timeout
- **NetworkCharacterSelectService** routes character ops to server (or mock fallback)

### Character System Integration
- On character selection, **CharacterSessionSystem** is updated with:
  - Selected Character ID
  - Character Name
  - Last World Scene
  - Session Token for re-auth

### HUD Integration
- **MobileHudController** (World scene) expects:
  - `GameContext.SessionStateClient.CharacterId` to be set
  - `GameContext.SessionStateClient.State == ClientSessionState.InWorld`
  - Network systems initialized (InventoryClientSystem, StatsClientSystem, etc.)

## Debugging & Testing

### Test Checklist
1. **Silent Login**: Close app, relaunch → should restore session from cached token
2. **Manual Login**: Enter creds, verify `SessionStateClient.State` progression
3. **Session Transfer**: Confirm `CharacterSelectFlowController.InjectSessionServices()` is called
4. **Character Selection**: Select character, verify `SessionStateClient.CharacterId` updated
5. **World Entry**: Click "Enter World", confirm `ClientSessionState.EnteringWorld` state
6. **HUD Appearance**: Verify HUD renders with correct character data
7. **Logout**: Click logout, verify state → `Disconnected`, scene → Login
8. **Reconnection**: Simulate network drop, verify auto-reconnect within **Connect Timeout**

### Debug Logs to Watch
```
[LoginFlow] Ready. Enter credentials.
[LoginFlow] Connecting...
[LoginFlow] Authenticating...
[LoginFlow] Login successful. Entering character select...
[LoginFlow] Session restored. (auto-login)
[CharacterSelectPresenter] Loaded X character(s).
[CharacterSelectPresenter] Enter world accepted for character=ID, scene=SCENE.
[ClientFlowFeedback] Error: ...
[SceneController] Loading scene 'CharacterSelect'...
[SceneController] Scene 'CharacterSelect' loaded successfully.
```

### Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| CharacterSelectFlowController can't find SessionStateClient | Not injected from Login scene | Ensure `InjectSessionServices()` called or use GameContext fallback |
| Network client not connecting | TCP settings or server offline | Verify TCP Host/Port or use InMemory mode for testing |
| Silent login fails silently | No cached token | First login requires manual credentials; refresh token expires check |
| HUD doesn't appear | `ClientSessionState.InWorld` not set | Confirm state transition in CharacterSelectFlowController.HandleEnterWorldAccepted |
| Scene load fails to fallback | No fallback scene in build settings | Ensure "Login" and "World_Dev" are added to Build Settings |

## Mobile-Specific Notes

### Portrait vs Landscape
- **LoginView**: Single layout (portrait by default)
- **CharacterSelectView**: Dual layouts (Portrait/LandscapeRoot GameObjects)
  - Portrait: Character list + details vertical
  - Landscape: Character list + preview side-by-side

### Safe Area
- **HudSafeAreaFitter** is used in World scene HUD
- Login/CharacterSelect scenes should also respect safe area for full-screen UI

### Performance Tuning
- Session services (SessionStateClient, ClientFlowFeedbackService) are lightweight
- NetworkGameClient auto-resets connection if app backgrounded
- Character list caching: CharacterSelectPresenter holds list in memory during scene

## Future Enhancements

1. **Two-Factor Auth**: Add OTP step after LoginFlowService.Authenticated
2. **Guest Login**: Bypass full auth, jump to pre-made character
3. **Character Creation UI**: Character Select scene with class preview and stat allocation
4. **Server Maintenance Message**: Intercept auth failure for scheduled downtime
5. **Cross-Platform Sync**: Use LoginSessionData for cloud backup of credentials

## File Changes Summary

| File | Change | Purpose |
|------|--------|---------|
| `ClientSessionState.cs` | NEW | Enum for session progression |
| `ClientFlowFeedbackService.cs` | NEW | Loading/error UI abstraction |
| `SessionStateClient.cs` | EXTENDED | Add `State` + `TryTransitionTo()` method |
| `LoginPresenter.cs` | REFACTORED | Accept SessionStateClient, ClientFlowFeedbackService |
| `LoginFlowController.cs` | REFACTORED | Instantiate session services, expose for injection |
| `CharacterSelectPresenter.cs` | REFACTORED | Accept session services, update state on bind |
| `CharacterSelectFlowController.cs` | REFACTORED | Add `InjectSessionServices()`, pass to presenter |

---

**Last Updated**: 2026-04-04  
**Status**: Ready for integration and device testing
