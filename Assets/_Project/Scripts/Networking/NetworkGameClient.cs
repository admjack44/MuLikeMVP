using System;
using System.Threading.Tasks;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Networking
{
    /// <summary>
    /// Play Mode integration client that sends login/move/skill packets and parses server responses.
    /// Supports in-memory bridge for local iteration and TCP for remote server testing.
    /// </summary>
    public class NetworkGameClient : MonoBehaviour
    {
        public enum ConnectionStatus
        {
            Disconnected,
            Connecting,
            Connected,
            Authenticating,
            Authenticated,
            Reconnecting,
            Suspended
        }

        [Header("Mode")]
        [SerializeField] private bool _useInMemoryGateway = true;

        [Header("TCP")]
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7777;

        [Header("Connection Resilience")]
        [SerializeField] private bool _autoReconnect = true;
        [SerializeField] private float _reconnectInitialDelaySeconds = 1f;
        [SerializeField] private float _reconnectMaxDelaySeconds = 15f;
        [SerializeField] private int _connectTimeoutMs = 8_000;
        [SerializeField] private int _receiveTimeoutMs = 30_000;
        [SerializeField] private int _sendTimeoutMs = 8_000;

        [Header("Heartbeat")]
        [SerializeField] private bool _heartbeatEnabled = true;
        [SerializeField] private float _heartbeatIntervalSeconds = 5f;
        [SerializeField] private float _heartbeatTimeoutSeconds = 12f;

        [Header("App Lifecycle")]
        [SerializeField] private bool _disconnectOnBackground = true;
        [SerializeField] private bool _autoReauthenticate = true;

        [Header("Auth")]
        [SerializeField] private string _username = "admin";
        [SerializeField] private string _password = "admin123";

        [Header("Debug Inputs")]
        [SerializeField] private KeyCode _loginKey = KeyCode.F1;
        [SerializeField] private KeyCode _moveKey = KeyCode.F2;
        [SerializeField] private KeyCode _skillKey = KeyCode.F3;

        private IGameConnection _connection;
        private PacketRouter _packetRouter;
        private NetworkEventStream _eventStream;
        private AuthClientService _authService;
        private MovementClientService _movementService;
        private MovementHandler _movementHandler;
        private CombatClientHandler _combatHandler;
        private SkillClientService _skillService;

        private bool _connected;
        private bool _isConnecting;
        private bool _isAuthenticating;
        private bool _isShuttingDown;
        private bool _isInBackground;
        private int _reconnectAttempt;
        private float _nextReconnectAt;
        private float _lastHeartbeatSentAt;
        private float _lastHeartbeatAckAt;
        private ConnectionStatus _status = ConnectionStatus.Disconnected;

        public bool IsConnected => _connected;
        public bool IsAuthenticated => _authService != null && _authService.IsAuthenticated;
        public event Action<string> OnClientLog;
        public event Action<bool, string> OnLoginResult;
        public event Action<bool, Vector3, string> OnMoveResult;
        public event Action<bool, int, int, string> OnSkillResult;
        public event Action<ConnectionStatus, string> OnConnectionStatusChanged;

        private async void Start()
        {
            BuildNetworkStack();
            WireNetworkStack();
            SetStatus(ConnectionStatus.Connecting, $"Connecting using {(_useInMemoryGateway ? "InMemory" : "TCP")} transport...");
            await ConnectInternalAsync();
        }

        private void BuildNetworkStack()
        {
            _connection = _useInMemoryGateway
                ? new InMemoryGameConnection()
                : new TcpGameConnection(
                    _host,
                    _port,
                    new NetworkClient.Options
                    {
                        ConnectTimeoutMs = _connectTimeoutMs,
                        ReceiveTimeoutMs = _receiveTimeoutMs,
                        SendTimeoutMs = _sendTimeoutMs
                    });

            _packetRouter = new PacketRouter();
            _eventStream = new NetworkEventStream(_packetRouter);
            _authService = new AuthClientService(_connection, _eventStream);
            _movementService = new MovementClientService(_connection, _eventStream);
            _movementHandler = new MovementHandler(_eventStream);
            _combatHandler = new CombatClientHandler(_connection, _eventStream);
            _skillService = new SkillClientService(_connection, _eventStream);
            _authService.ConfigureCredentials(_username, _password);
        }

        private void Update()
        {
            HandleReconnectTick();
            HandleHeartbeatTick();
            HandleProactiveRefreshTick();

            if (!_connected)
                return;

            if (WasKeyPressed(_loginKey))
            {
                _ = SendLoginAsync();
            }

            if (IsAuthenticated && WasKeyPressed(_moveKey))
            {
                Vector3 target = transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                _ = SendMoveAsync(target.x, target.y, target.z);
            }

            if (IsAuthenticated && WasKeyPressed(_skillKey))
            {
                // Demo target entity id. In a real flow this comes from TargetingController.
                _ = SendSkillCastAsync(skillId: 1, targetId: 999);
            }
        }

        private void WireNetworkStack()
        {
            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.PacketReceived += HandleIncomingPacket;

            _eventStream.ErrorReceived += HandleServerError;
            _eventStream.LoginResponseReceived += HandleLoginResponse;
            _eventStream.HeartbeatResponseReceived += HandleHeartbeatResponse;
            _movementService.MoveResultReceived += HandleMoveResult;
            _movementHandler.EntityMoved += HandleEntityMoved;
            _combatHandler.AttackPerformed += HandleAttackPerformed;
            _combatHandler.EntityDied += HandleEntityDied;
            _combatHandler.EntityRespawned += HandleEntityRespawned;
            _skillService.SkillResultReceived += HandleSkillResult;
            _authService.LoginResultReceived += HandleLoginResult;
            _authService.RefreshResultReceived += HandleRefreshResult;
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return keyCode switch
            {
                KeyCode.F1 => keyboard.f1Key.wasPressedThisFrame,
                KeyCode.F2 => keyboard.f2Key.wasPressedThisFrame,
                KeyCode.F3 => keyboard.f3Key.wasPressedThisFrame,
                KeyCode.F4 => keyboard.f4Key.wasPressedThisFrame,
                KeyCode.F5 => keyboard.f5Key.wasPressedThisFrame,
                KeyCode.Alpha1 => keyboard.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => keyboard.digit2Key.wasPressedThisFrame,
                KeyCode.Alpha3 => keyboard.digit3Key.wasPressedThisFrame,
                KeyCode.Alpha4 => keyboard.digit4Key.wasPressedThisFrame,
                KeyCode.Alpha5 => keyboard.digit5Key.wasPressedThisFrame,
                KeyCode.Alpha6 => keyboard.digit6Key.wasPressedThisFrame,
                KeyCode.Alpha7 => keyboard.digit7Key.wasPressedThisFrame,
                KeyCode.Alpha8 => keyboard.digit8Key.wasPressedThisFrame,
                KeyCode.Alpha9 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

        public async Task SendLoginAsync()
        {
            if (_isAuthenticating)
                return;

            _isAuthenticating = true;
            SetStatus(ConnectionStatus.Authenticating, "Authenticating...");
            await _authService.LoginAsync();
        }

        public void ConfigureCredentials(string username, string password)
        {
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
            _authService?.ConfigureCredentials(_username, _password);
            Log($"Credentials updated for user '{_username}'.");
        }

        public async Task SendMoveAsync(float x, float y, float z)
        {
            await _movementService.MoveAsync(x, y, z);
        }

        public async Task SendSkillCastAsync(int skillId, int targetId)
        {
            await _skillService.CastAsync(skillId, targetId);
        }

        private void HandleIncomingPacket(byte[] packet)
        {
            _eventStream.ProcessPacket(packet);
        }

        private void HandleConnected()
        {
            _connected = true;
            _reconnectAttempt = 0;
            _lastHeartbeatAckAt = Time.unscaledTime;
            SetStatus(ConnectionStatus.Connected, $"Connected via {(_useInMemoryGateway ? "InMemoryGatewayBridge" : "TCP")}.");

            if (_autoReauthenticate)
                _ = EnsureAuthenticatedAsync();
        }

        private void HandleDisconnected()
        {
            _connected = false;
            _authService?.Reset();

            if (_isShuttingDown)
            {
                SetStatus(ConnectionStatus.Disconnected, "Disconnected.");
                return;
            }

            if (_isInBackground)
            {
                SetStatus(ConnectionStatus.Suspended, "Connection suspended in background.");
                return;
            }

            if (_autoReconnect)
            {
                ScheduleReconnect("Connection lost. Scheduling reconnect.");
            }
            else
            {
                SetStatus(ConnectionStatus.Disconnected, "Disconnected.");
            }
        }

        private void HandleServerError(string message)
        {
            LogWarning($"Server error: {message}");
        }

        private void HandleLoginResponse(bool success, string token, string message)
        {
            Log($"Login response: success={success}, msg={message}");
        }

        private void HandleMoveResult(bool success, Vector3 position, string message)
        {
            if (success)
            {
                transform.position = position;
            }

            Log($"Move response: success={success}, pos=({position.x:F2},{position.y:F2},{position.z:F2}), msg={message}");
            OnMoveResult?.Invoke(success, position, message);
        }

        private void HandleEntityMoved(int entityId, Vector3 position)
        {
            // Update remote entity position from server snapshot
            // In full implementation, find EntityView by entityId and update transform
            Log($"Entity {entityId} moved to ({position.x:F2},{position.y:F2},{position.z:F2})");
        }

        private void HandleAttackPerformed(int targetId, bool hitSuccess, int damage, bool isCritical)
        {
            string result = isCritical ? "CRITICAL" : (hitSuccess ? "HIT" : "MISS");
            Log($"Attack on {targetId}: {result} ({damage} dmg)");
        }

        private void HandleEntityDied(int entityId)
        {
            Log($"Entity {entityId} died");
        }

        private void HandleEntityRespawned(int entityId, Vector3 position)
        {
            Log($"Entity {entityId} respawned at ({position.x:F2},{position.y:F2},{position.z:F2})");
        }

        private void HandleSkillResult(bool success, int targetId, int damage, string message)
        {
            Log($"Skill response: success={success}, target={targetId}, damage={damage}, msg={message}");
            OnSkillResult?.Invoke(success, targetId, damage, message);
        }

        private void HandleLoginResult(bool success, string message)
        {
            _isAuthenticating = false;
            if (!success)
            {
                LogWarning($"Login failed: {message}");
                SetStatus(ConnectionStatus.Connected, "Connected, authentication failed.");
            }
            else
            {
                SetStatus(ConnectionStatus.Authenticated, "Authenticated.");
            }

            OnLoginResult?.Invoke(success, message);
        }

        private void HandleRefreshResult(bool success, string message)
        {
            _isAuthenticating = false;
            if (success)
            {
                SetStatus(ConnectionStatus.Authenticated, "Session refreshed.");
                return;
            }

            LogWarning($"Refresh failed: {message}");
            if (_connected)
                _ = SendLoginAsync();
        }

        private void HandleHeartbeatResponse(long _)
        {
            _lastHeartbeatAckAt = Time.unscaledTime;
        }

        private async Task ConnectInternalAsync()
        {
            if (_isConnecting || _connection == null || _isInBackground)
                return;

            _isConnecting = true;
            try
            {
                await _connection.ConnectAsync();
            }
            catch (Exception ex)
            {
                LogWarning($"Connect failed: {ex.Message}");
                _connected = false;
                if (_autoReconnect && !_isShuttingDown && !_isInBackground)
                    ScheduleReconnect("Connect failed. Retrying...");
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private async Task EnsureAuthenticatedAsync()
        {
            if (_authService == null || !_connected || _isAuthenticating)
                return;

            _isAuthenticating = true;
            SetStatus(ConnectionStatus.Authenticating, "Reauthenticating...");

            try
            {
                bool ok = await _authService.TryReauthenticateAsync();
                if (!ok)
                    SetStatus(ConnectionStatus.Connected, "Connected, waiting auth.");
            }
            catch (Exception ex)
            {
                _isAuthenticating = false;
                LogWarning($"Authentication error: {ex.Message}");
                SetStatus(ConnectionStatus.Connected, "Auth error.");
            }
        }

        private void HandleReconnectTick()
        {
            if (_isShuttingDown || _isInBackground || _connected || !_autoReconnect)
                return;

            if (Time.unscaledTime < _nextReconnectAt)
                return;

            SetStatus(ConnectionStatus.Reconnecting, "Reconnecting...");
            _ = ConnectInternalAsync();
        }

        private void HandleHeartbeatTick()
        {
            if (!_heartbeatEnabled || !_connected || !IsAuthenticated || _isInBackground)
                return;

            float now = Time.unscaledTime;
            if (now - _lastHeartbeatSentAt >= Mathf.Max(1f, _heartbeatIntervalSeconds))
            {
                _lastHeartbeatSentAt = now;
                _ = _connection.SendAsync(ClientMessageFactory.CreateHeartbeatRequest(DateTime.UtcNow.Ticks));
            }

            if (now - _lastHeartbeatAckAt > Mathf.Max(2f, _heartbeatTimeoutSeconds))
            {
                LogWarning("Heartbeat timeout detected. Forcing reconnect.");
                _connection.Disconnect();
            }
        }

        private void HandleProactiveRefreshTick()
        {
            if (!_connected || _isInBackground || _isAuthenticating || _authService == null)
                return;

            if (!_authService.IsAuthenticated || !_authService.CanRefresh)
                return;

            TimeSpan remaining = _authService.AccessTokenExpiresAtUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.FromSeconds(90))
            {
                _isAuthenticating = true;
                SetStatus(ConnectionStatus.Authenticating, "Refreshing access token...");
                _ = _authService.RefreshAsync();
            }
        }

        private void ScheduleReconnect(string reason)
        {
            _reconnectAttempt++;
            float backoff = Mathf.Min(
                Mathf.Max(0.5f, _reconnectMaxDelaySeconds),
                Mathf.Max(0.25f, _reconnectInitialDelaySeconds) * Mathf.Pow(2f, _reconnectAttempt - 1));

            _nextReconnectAt = Time.unscaledTime + backoff;
            SetStatus(ConnectionStatus.Reconnecting, $"{reason} Next attempt in {backoff:F1}s.");
        }

        private void SetStatus(ConnectionStatus status, string message)
        {
            bool changed = _status != status;
            _status = status;

            if (changed || !string.IsNullOrWhiteSpace(message))
            {
                string text = $"[{status}] {message}";
                Log(text);
                OnConnectionStatusChanged?.Invoke(status, message ?? string.Empty);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _isInBackground = true;
                SetStatus(ConnectionStatus.Suspended, "App moved to background.");
                if (_disconnectOnBackground && _connected)
                    _connection.Disconnect();

                return;
            }

            _isInBackground = false;
            SetStatus(ConnectionStatus.Disconnected, "App resumed.");
            if (_autoReconnect)
                ScheduleReconnect("Resumed from background.");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                return;

            if (!_isInBackground && !_connected && _autoReconnect)
                ScheduleReconnect("App focus restored.");
        }

        private void Log(string message)
        {
            string line = $"[NetworkGameClient] {message}";
            Debug.Log(line);
            OnClientLog?.Invoke(line);
        }

        private void LogWarning(string message)
        {
            string line = $"[NetworkGameClient] {message}";
            Debug.LogWarning(line);
            OnClientLog?.Invoke(line);
        }

        private void OnDestroy()
        {
            _isShuttingDown = true;
            _connection?.Disconnect();
            _connected = false;
        }
    }
}
