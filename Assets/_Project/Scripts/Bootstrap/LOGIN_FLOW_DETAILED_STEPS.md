# Login Flow - Detailed Integration Steps

## Phase 1: Prepare Boot Scene (Already Complete)
- FrontendFlowDirector: Singleton, DontDestroyOnLoad ✓
- SceneController: Singleton, DontDestroyOnLoad ✓
- Scene routing configured ✓

## Phase 2: Setup Login Scene in Unity Editor

### Step 1: Create Canvas & UI
1. Create Canvas in Login scene
   - Render Mode: Screen Space - Overlay
   - Safe Area: enabled
   
2. Create InputField for Username
   - Name: `UsernameInput`
   - Text: "admin" (placeholder)
   - Content Type: Standard

3. Create InputField for Password
   - Name: `PasswordInput`
   - Text: "password" (placeholder)
   - Content Type: Password

4. Create Button "Enter"
   - Name: `EnterButton`
   - Text: "LOGIN"

5. Create Button "Logout"
   - Name: `LogoutButton`
   - Text: "LOGOUT"
   - Initially hidden or disabled

6. Create Text "Status"
   - Name: `StatusText`
   - Text: "[LoginFlow] Ready. Enter credentials."
   - Alignment: Center

### Step 2: Create NetworkGameClient in Scene
1. Create empty GameObject `NetworkGameClient`
2. Add component: `NetworkGameClient.cs`
3. Inspector settings:
   - Use In-Memory Gateway: true (for testing)
   - Or: TCP Host="127.0.0.1", Port=7777
   - Auto-Reconnect: enabled
   - Auto-Authenticate: enabled
   - Heartbeat Enabled: true

### Step 3: Create LoginView Component
1. Create empty GameObject `LoginView`
2. Add component: `LoginView.cs`
3. Connect in inspector:
   - Username Input → UsernameInput field
   - Password Input → PasswordInput field
   - Enter Button → EnterButton button
   - Logout Button → LogoutButton button
   - Status Text → StatusText text

### Step 4: Create LoginFlowController
1. Create empty GameObject `LoginFlowController`
2. Add component: `LoginFlowController.cs`
3. Connect in inspector:
   - View → LoginView (from Step 3)
   - NetworkClient → NetworkGameClient (from Step 2)
   - FrontendFlowDirector → (auto-finds or drag Boot scene instance)
   - Load Scene On Success: true
   - Character Select Scene Name: "CharacterSelect"
   - Connect Timeout: 10000
   - Request Timeout: 12000

### Step 5: Add Scene to Build Settings
1. File → Build Settings
2. Add Login scene:
   - LoginScene (or give it a index after Boot)
3. Verify in FrontendFlowDirector inspector:
   - Login Scene Name matches build settings name

## Phase 3: Setup Character Select Scene in Unity Editor

### Step 1: Create Canvas & UI
1. Create Canvas
   - Render Mode: Screen Space - Overlay
   - Safe Area: enabled

2. Create Character List Container
   - Name: `CharacterListContainer`
   - HorizontalLayoutGroup or vertical scroll

3. Create Character Item Prefab (if doesn't exist)
   - Name: `CharacterSlotView`
   - Components: Image, Button, Text
   - Used by CharacterSelectPresenter to spawn

4. Create Character Info Panel
   - Selected Name: TextMeshProUGUI
   - Selected Class: TextMeshProUGUI
   - Selected Level: TextMeshProUGUI
   - Selected Power: TextMeshProUGUI
   - Selected Map: TextMeshProUGUI

5. Create Action Buttons
   - Refresh: Button
   - Delete: Button
   - EnterWorld: Button
   - Create: Button (with child InputField for name, Dropdown for class)

6. Create Status Text
   - Name: `StatusText`

7. Create Loading Spinner (optional)
   - Animated circle or dots
   - Initially hidden

### Step 2: Create CharacterSelectView Component
1. Create empty GameObject `CharacterSelectView`
2. Add component: `CharacterSelectView.cs`
3. Connect in inspector:
   - List Container → CharacterListContainer
   - Item Prefab → CharacterSlotView prefab
   - Selected Name/Class/Level/Power/Map Text → respective text fields
   - Refresh/Delete/EnterWorld Buttons → respective buttons
   - Status Text → StatusText
   - Loading Root → loading spinner GameObject
   - Portrait/Landscape Roots → if mobile layout

### Step 3: Create CharacterSelectFlowController
1. Create empty GameObject `CharacterSelectFlowController`
2. Add component: `CharacterSelectFlowController.cs`
3. Connect in inspector:
   - View → CharacterSelectView (from Step 2)
   - NetworkClient → (auto-finds or drag from Boot scene)
   - Use Mock Service: true (for testing without server)
   - Load Scene On Enter World: true
   - Fallback World Scene Name: "World_Dev"
   - Town Scene Name: "Town_01"
   - FrontendFlowDirector → (auto-finds or drag)

### Step 4: Add Scene to Build Settings
1. File → Build Settings
2. Add Character Select scene
3. Verify name matches FrontendFlowDirector's Character Select Scene Name

## Phase 4: Bridge Login & Character Select Scenes

### Option A: Explicit Injection (Cleanest)

**In LoginFlowController (after successful login):**
```csharp
private void HandleLoginSuccess()
{
    // ... existing code ...
    
    // Prepare to inject services into next scene
    // (This happens BEFORE scene unload)
    if (_frontendFlowDirector != null)
    {
        _frontendFlowDirector.EnterCharacterSelect();
        return;
    }
}
```

**Create a SceneBootstrapper or use a static service locator:**
```csharp
// In a new file: Assets/_Project/Scripts/Bootstrap/LoginSessionCarrier.cs
public static class LoginSessionCarrier
{
    private static SessionStateClient _sessionStateClient;
    private static ClientFlowFeedbackService _feedbackService;

    public static void CarrySession(SessionStateClient session, ClientFlowFeedbackService feedback)
    {
        _sessionStateClient = session;
        _feedbackService = feedback;
    }

    public static bool TryGetSession(out SessionStateClient session, out ClientFlowFeedbackService feedback)
    {
        session = _sessionStateClient;
        feedback = _feedbackService;
        return _sessionStateClient != null;
    }

    public static void Clear()
    {
        _sessionStateClient = null;
        _feedbackService = null;
    }
}
```

**In LoginFlowController.HandleLoginSuccess():**
```csharp
private void HandleLoginSuccess()
{
    // ... auth update code ...
    
    // Carry session services to next scene
    LoginSessionCarrier.CarrySession(_sessionStateClient, _feedbackService);
    
    if (_frontendFlowDirector != null)
    {
        _frontendFlowDirector.EnterCharacterSelect();
    }
}
```

**In CharacterSelectFlowController.Awake():**
```csharp
private void Awake()
{
    // ... existing setup ...
    
    // Try to receive session services from login scene
    if (LoginSessionCarrier.TryGetSession(out var session, out var feedback))
    {
        InjectSessionServices(session, feedback);
        LoginSessionCarrier.Clear();
    }
    
    // ... create presenter ...
}
```

### Option B: Via GameContext (if using prior composition root)

**In GameContext (from prior phase), add properties:**
```csharp
private static SessionStateClient _sessionStateClient;
private static ClientFlowFeedbackService _feedbackService;

public static SessionStateClient SessionStateClient
{
    get => _sessionStateClient;
    set => _sessionStateClient = value;
}

public static ClientFlowFeedbackService FeedbackService
{
    get => _feedbackService;
    set => _feedbackService = value;
}
```

**In LoginFlowController.HandleLoginSuccess():**
```csharp
GameContext.SessionStateClient = _sessionStateClient;
GameContext.FeedbackService = _feedbackService;
```

**In CharacterSelectFlowController.Awake():**
```csharp
if (_sessionStateClient == null && GameContext.SessionStateClient != null)
{
    _sessionStateClient = GameContext.SessionStateClient;
}
if (_feedbackService == null && GameContext.FeedbackService != null)
{
    _feedbackService = GameContext.FeedbackService;
}
```

## Phase 5: Test Login → Character Select Flow

### Test Procedure
1. **Build Settings**:
   - Ensure scenes in order: Boot, Login, CharacterSelect, World_Dev
   - Or set Boot as startup

2. **Play Boot Scene**:
   - Should load Login scene
   - NetworkGameClient should instantiate
   - FrontendFlowDirector visible in hierarchy

3. **Login Scene**:
   - Enter "admin" / "admin123" (check mock auth in NetworkCharacterSelectService)
   - Click "LOGIN"
   - Status should show: "Authenticating..." → "Login successful..."
   - Should auto-load CharacterSelect scene

4. **Character Select Scene**:
   - Should see list of characters (from mock or server)
   - Status shows: "Loaded X character(s)"
   - Select a character
   - Click "ENTER WORLD"
   - Should transition to World_Dev scene

5. **Verify State Transitions**:
   - Add debug log in SessionStateClient.TryTransitionTo():
   ```csharp
   Debug.Log($"[SessionState] {previousState} → {requestedState}");
   ```
   - Should see: Disconnected → Connecting → Authenticating → Authenticated → CharacterSelection → EnteringWorld

## Phase 6: Integrate with World Scene HUD

### Setup World Scene HUD
1. Create or prepare World_Dev scene
2. Create Canvas for HUD
3. Add component: `MobileHudController.cs`
4. Inspector:
   - Expect `GameContext.SessionStateClient.CharacterId` to be set
   - Expect `GameContext.SessionStateClient.State == ClientSessionState.InWorld`

### In CharacterSelectFlowController.HandleEnterWorldAccepted():
```csharp
// (Already done in refactored code)
_sessionStateClient.TryTransitionTo(ClientSessionState.EnteringWorld);
// ... scene load ...
// HUD system will see state == InWorld and initialize
```

## Phase 7: Mobile Orientation Support

### In CharacterSelectView.Awake():
```csharp
private void UpdateOrientationLayout()
{
    bool isLandscape = Screen.width > Screen.height;
    if (_portraitRoot != null) _portraitRoot.SetActive(!isLandscape);
    if (_landscapeRoot != null) _landscapeRoot.SetActive(isLandscape);
}

private void Update()
{
    // Check for orientation change
    if (Screen.width > Screen.height && !_isLandscape)
        UpdateOrientationLayout();
    else if (Screen.width <= Screen.height && _isLandscape)
        UpdateOrientationLayout();
}
```

## Checklist Summary

- [ ] Boot scene has FrontendFlowDirector + SceneController
- [ ] Login scene created with UI (inputs, buttons, status)
- [ ] NetworkGameClient in Boot or Login scene
- [ ] LoginView component wired to UI elements
- [ ] LoginFlowController wired to LoginView + NetworkClient
- [ ] Character Select scene created with UI
- [ ] CharacterSelectView component wired to UI elements
- [ ] CharacterSelectFlowController wired to CharacterSelectView
- [ ] Session service injection implemented (LoginSessionCarrier or GameContext)
- [ ] Build Settings has scenes in order: Boot, Login, CharacterSelect, World_Dev
- [ ] Debug output verified during login/selection flow
- [ ] State transitions logged
- [ ] Logout brings back to Login scene
- [ ] Silent login (cached token) works
- [ ] Mobile layouts tested (portrait/landscape)
- [ ] HUD appears correctly in World scene

---

**Ready for QA Testing & Device Deployment**
