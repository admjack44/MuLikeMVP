using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Shared.Protocol;
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
        [SerializeField] private int _requestTimeoutMs = 8_000;

        [Header("Heartbeat")]
        [SerializeField] private bool _heartbeatEnabled = true;
        [SerializeField] private float _heartbeatIntervalSeconds = 5f;
        [SerializeField] private float _heartbeatTimeoutSeconds = 12f;

        [Header("Command Queue")]
        [SerializeField] private bool _useCommandQueue = true;
        [SerializeField, Min(1)] private int _maxCommandsPerSecond = 30;
        [SerializeField, Min(8)] private int _maxCommandQueueSize = 128;

        [Header("App Lifecycle")]
        [SerializeField] private bool _disconnectOnBackground = true;
        [SerializeField] private bool _autoReauthenticate = true;
        [SerializeField] private bool _autoConnectOnStart = true;

        [Header("Auth")]
        [SerializeField] private string _username = "admin";
        [SerializeField] private string _password = "admin123";

        [Header("Debug Utilities")]
        [SerializeField] private bool _enableDebugUtilities = false;
        [SerializeField] private bool _enableKeyboardDebugShortcuts = false;
        [SerializeField] private KeyCode _loginKey = KeyCode.F1;
        [SerializeField] private KeyCode _moveKey = KeyCode.F2;
        [SerializeField] private KeyCode _skillKey = KeyCode.F3;

        [Header("Snapshot")]
        [SerializeField] private int _localPlayerEntityId;

        private IGameConnection _connection;
        private CommandQueueGameConnection _queuedConnection;
        private PacketRouter _packetRouter;
        private NetworkEventStream _eventStream;
        private AuthClientService _authService;
        private MovementClientService _movementService;
        private MovementHandler _movementHandler;
        private CombatClientHandler _combatHandler;
        private SkillClientService _skillService;
        private SnapshotSyncDriver _snapshotSyncDriver;
        private readonly List<SnapshotApplier.EntitySnapshot> _snapshotBuffer = new(64);
        private uint _lastSnapshotSequence;
        private NetworkHeartbeatService _heartbeatService;

        private bool _connected;
        private bool _isConnecting;
        private bool _isAuthenticating;
        private bool _isShuttingDown;
        private bool _isInBackground;
        private bool _networkInitialized;
        private int _reconnectAttempt;
        private float _nextReconnectAt;
        private float _smoothedRttMs;
        private float _rttJitterMs;
        private DateTime _lastPacketReceivedUtc = DateTime.MinValue;
        private float _incomingPacketsPerSecond;
        private int _incomingPacketsWindowCount;
        private float _incomingPacketsWindowStart;
        private ConnectionStatus _status = ConnectionStatus.Disconnected;

        public bool IsConnected => _connected;
        public bool IsAuthenticated => _authService != null && _authService.IsAuthenticated;
        public bool CanRefreshToken => _authService != null && _authService.CanRefresh;
        public DateTime AccessTokenExpiresAtUtc => _authService != null ? _authService.AccessTokenExpiresAtUtc : DateTime.MinValue;
        public ConnectionStatus Status => _status;
        public int LocalPlayerEntityId => _localPlayerEntityId;
        public float SmoothedRttMs => _smoothedRttMs;
        public float RttJitterMs => _rttJitterMs;
        public DateTime LastPacketReceivedUtc => _lastPacketReceivedUtc;
        public float IncomingPacketsPerSecond => _incomingPacketsPerSecond;
        public int QueuedCommands => _queuedConnection != null ? _queuedConnection.QueuedCount : 0;
        public long DroppedQueuedCommands => _queuedConnection != null ? _queuedConnection.DroppedCommands : 0;
        public bool IsInBackground => _isInBackground;
        public bool IsConnecting => _isConnecting;
        public PacketRouter PacketRouter => _packetRouter;
        public event Action<string> OnClientLog;
        public event Action<bool, string> OnLoginResult;
        public event Action<bool, string> OnRefreshResult;
        public event Action<bool, Vector3, string> OnMoveResult;
        public event Action<bool, int, int, string> OnSkillResult;
        public event Action<int, bool, int, bool> OnAttackResult;
        public event Action<int> OnEntityDied;
        public event Action<ConnectionStatus, string> OnConnectionStatusChanged;
        public event Action<float, float> OnLatencyUpdated;
        public event Action<string> OnSessionExpired;

        private async void Start()
        {
            EnsureNetworkInitialized();
            if (!_autoConnectOnStart)
            {
                SetStatus(ConnectionStatus.Disconnected, "Waiting for SessionConnectionController.");
                return;
            }

            SetStatus(ConnectionStatus.Connecting, $"Connecting using {(_useInMemoryGateway ? "InMemory" : "TCP")} transport...");
            await ConnectInternalAsync();
        }

        private void EnsureNetworkInitialized()
        {
            if (_networkInitialized)
                return;

            BuildNetworkStack();
            WireNetworkStack();
            _networkInitialized = true;
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

            _queuedConnection = new CommandQueueGameConnection(_connection, new CommandQueueGameConnection.Settings
            {
                Enabled = _useCommandQueue,
                MaxCommandsPerSecond = _maxCommandsPerSecond,
                MaxQueueSize = _maxCommandQueueSize
            });

            _packetRouter = new PacketRouter();
            _eventStream = new NetworkEventStream(_packetRouter);
            _authService = new AuthClientService(_queuedConnection, _eventStream)
            {
                RequestTimeoutMs = Mathf.Max(1_000, _requestTimeoutMs)
            };
            _movementService = new MovementClientService(_queuedConnection, _eventStream)
            {
                RequestTimeoutMs = Mathf.Max(500, _requestTimeoutMs)
            };
            _movementHandler = new MovementHandler(_eventStream);
            _combatHandler = new CombatClientHandler(_queuedConnection, _eventStream);
            _skillService = new SkillClientService(_queuedConnection, _eventStream)
            {
                RequestTimeoutMs = Mathf.Max(500, _requestTimeoutMs)
            };
            _authService.ConfigureCredentials(_username, _password);
            _heartbeatService = new NetworkHeartbeatService(new NetworkHeartbeatService.Settings
            {
                Enabled = _heartbeatEnabled,
                IntervalSeconds = _heartbeatIntervalSeconds,
                TimeoutSeconds = _heartbeatTimeoutSeconds
            });
            _snapshotSyncDriver = FindObjectOfType<SnapshotSyncDriver>();
        }

        private void Update()
        {
            HandleReconnectTick();
            HandleHeartbeatTick();
            HandleProactiveRefreshTick();
            _queuedConnection?.Tick();

            if (_connection is InMemoryGameConnection inMemory)
                inMemory.PumpServerEvents();

            if (!_connected)
                return;

            if (!_enableDebugUtilities || !_enableKeyboardDebugShortcuts)
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
            _eventStream.FullSnapshotReceived += HandleFullSnapshot;
            _eventStream.DeltaSnapshotReceived += HandleDeltaSnapshot;
            _eventStream.SelectCharacterResponseReceived += HandleSelectCharacterResponse;
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
            EnsureNetworkInitialized();

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
            EnsureNetworkInitialized();
            await _movementService.MoveAsync(x, y, z);
        }

        public async Task SendSkillCastAsync(int skillId, int targetId)
        {
            EnsureNetworkInitialized();
            await _skillService.CastAsync(skillId, targetId);
        }

        public async Task SendRawPacketAsync(byte[] packet)
        {
            EnsureNetworkInitialized();
            if (packet == null || packet.Length == 0 || _queuedConnection == null)
                return;

            await _queuedConnection.SendAsync(packet);
        }

        public async Task SendRefreshAsync()
        {
            EnsureNetworkInitialized();

            if (_isAuthenticating)
                return;

            _isAuthenticating = true;
            SetStatus(ConnectionStatus.Authenticating, "Refreshing token...");
            await _authService.RefreshAsync();
        }

        public async Task<bool> EnsureConnectedAsync(int timeoutMs, CancellationToken ct = default)
        {
            EnsureNetworkInitialized();

            if (_connected)
                return true;

            if (!_isConnecting)
                _ = ConnectInternalAsync();

            if (timeoutMs <= 0)
                timeoutMs = 1;

            float deadline = Time.realtimeSinceStartup + (timeoutMs / 1000f);

            while (!_connected && Time.realtimeSinceStartup < deadline)
            {
                if (ct.IsCancellationRequested)
                    return false;

                await Task.Delay(50, ct);
            }

            return _connected;
        }

        public async Task<bool> WaitForAuthenticatedAsync(int timeoutMs, CancellationToken ct = default)
        {
            EnsureNetworkInitialized();

            if (IsAuthenticated)
                return true;

            if (timeoutMs <= 0)
                timeoutMs = 1;

            float deadline = Time.realtimeSinceStartup + (timeoutMs / 1000f);
            while (!IsAuthenticated && Time.realtimeSinceStartup < deadline)
            {
                if (ct.IsCancellationRequested)
                    return false;

                await Task.Delay(50, ct);
            }

            return IsAuthenticated;
        }

        public async Task<bool> ConnectAndAuthenticateAsync(int connectTimeoutMs, int authenticateTimeoutMs, CancellationToken ct = default)
        {
            EnsureNetworkInitialized();

            bool connected = await EnsureConnectedAsync(connectTimeoutMs, ct);
            if (!connected)
                return false;

            if (IsAuthenticated)
                return true;

            await SendLoginAsync();
            return await WaitForAuthenticatedAsync(authenticateTimeoutMs, ct);
        }

        public AuthSessionSnapshot CaptureAuthSession()
        {
            EnsureNetworkInitialized();
            return _authService.CaptureSession();
        }

        public void RestoreAuthSession(AuthSessionSnapshot snapshot)
        {
            EnsureNetworkInitialized();
            _authService.RestoreSession(snapshot);
            Log("Auth session restored from local persistence.");
        }

        /// <summary>
        /// Allows scene flow to set the local player entity id before server select response arrives.
        /// </summary>
        public void SetLocalPlayerEntityId(int entityId)
        {
            if (entityId <= 0)
                return;

            _localPlayerEntityId = entityId;
            _snapshotSyncDriver?.SetLocalPlayerEntityId(_localPlayerEntityId);
        }

        public void Logout(bool disconnect = false)
        {
            EnsureNetworkInitialized();

            _isAuthenticating = false;
            _authService.ClearSession();

            if (disconnect && _connected)
                _connection.Disconnect();

            SetStatus(ConnectionStatus.Disconnected, "Session cleared by logout.");
        }

        public void RequestReconnect(string reason = "Manual reconnect requested.")
        {
            EnsureNetworkInitialized();

            if (_isInBackground)
                return;

            if (_connected)
                _connection?.Disconnect();

            if (_autoReconnect)
                ScheduleReconnect(reason);
            else
                SetStatus(ConnectionStatus.Disconnected, reason);
        }

        public void DisconnectTransport(bool scheduleReconnect, string reason = "Transport disconnected.")
        {
            EnsureNetworkInitialized();

            if (_connected)
                _connection?.Disconnect();

            if (scheduleReconnect && _autoReconnect && !_isInBackground)
                ScheduleReconnect(reason);
            else
                SetStatus(ConnectionStatus.Disconnected, reason);
        }

        public bool TrySendHeartbeatPing()
        {
            if (!_heartbeatEnabled || !_connected || !IsAuthenticated || _isInBackground || _connection == null)
                return false;

            long nowTicks = DateTime.UtcNow.Ticks;
            _ = _queuedConnection.SendAsync(ClientMessageFactory.CreateHeartbeatRequest(nowTicks));
            return true;
        }

        public void SetBackgroundState(bool inBackground)
        {
            ApplyApplicationBackgroundState(inBackground, fromUnityCallback: false);
        }

        private void HandleIncomingPacket(byte[] packet)
        {
            UpdateIncomingPacketMetrics();
            _eventStream.ProcessPacket(packet);
        }

        private void HandleConnected()
        {
            _connected = true;
            _reconnectAttempt = 0;
            _heartbeatService?.Reset();
            SetStatus(ConnectionStatus.Connected, $"Connected via {(_useInMemoryGateway ? "InMemoryGatewayBridge" : "TCP")}.");

            if (_snapshotSyncDriver != null)
                _snapshotSyncDriver.SetLocalPlayerEntityId(_localPlayerEntityId);

            if (_autoReauthenticate)
                _ = EnsureAuthenticatedAsync();
        }

        private void HandleDisconnected()
        {
            _connected = false;
            _authService?.Reset();
            _heartbeatService?.Reset();

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
            OnAttackResult?.Invoke(targetId, hitSuccess, damage, isCritical);
        }

        private void HandleEntityDied(int entityId)
        {
            Log($"Entity {entityId} died");
            OnEntityDied?.Invoke(entityId);
        }

        private void HandleEntityRespawned(int entityId, Vector3 position)
        {
            Log($"Entity {entityId} respawned at ({position.x:F2},{position.y:F2},{position.z:F2})");
        }

        private void HandleFullSnapshot(SnapshotData snapshot)
        {
            ApplyWorldSnapshot(snapshot, true);
        }

        private void HandleDeltaSnapshot(SnapshotData snapshot)
        {
            ApplyWorldSnapshot(snapshot, false);
        }

        private void ApplyWorldSnapshot(SnapshotData snapshot, bool isFull)
        {
            if (snapshot == null)
                return;

            if (!isFull && snapshot.SequenceNumber != 0 && snapshot.SequenceNumber <= _lastSnapshotSequence)
                return;

            if (_snapshotSyncDriver == null)
                _snapshotSyncDriver = FindObjectOfType<SnapshotSyncDriver>();

            if (_snapshotSyncDriver == null)
                return;

            _snapshotSyncDriver.SetLocalPlayerEntityId(_localPlayerEntityId);

            _snapshotBuffer.Clear();
            if (snapshot.Entities != null)
            {
                for (int i = 0; i < snapshot.Entities.Count; i++)
                {
                    SnapshotEntityData entity = snapshot.Entities[i];
                    if (entity == null || entity.EntityId <= 0)
                        continue;

                    var type = (SnapshotApplier.EntityType)entity.EntityType;
                    _snapshotBuffer.Add(new SnapshotApplier.EntitySnapshot
                    {
                        EntityId = entity.EntityId,
                        Type = type,
                        Position = new Vector3(entity.PosX, entity.PosY, entity.PosZ),
                        RotationY = entity.RotationY,
                        HpCurrent = entity.HpCurrent,
                        HpMax = entity.HpMax,
                        IsAlive = entity.IsAlive,
                        DisplayName = entity.DisplayName,
                        OwnerEntityId = entity.OwnerEntityId
                    });
                }
            }

            _snapshotSyncDriver.ApplyWorldSnapshot(
                _snapshotBuffer,
                isFull,
                sequenceNumber: snapshot.SequenceNumber,
                serverTimestampMs: snapshot.TimestampMs);
            _lastSnapshotSequence = snapshot.SequenceNumber;
        }

        private void HandleSelectCharacterResponse(bool success, int characterId, string _)
        {
            if (!success || characterId <= 0)
                return;

            _localPlayerEntityId = characterId;
            _snapshotSyncDriver?.SetLocalPlayerEntityId(_localPlayerEntityId);
            Log($"Local player entity id set: {_localPlayerEntityId}");
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
                bool looksExpired = !string.IsNullOrWhiteSpace(message)
                    && (message.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0
                        || message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0
                        || message.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0);

                if (looksExpired)
                    OnSessionExpired?.Invoke(string.IsNullOrWhiteSpace(message) ? "Session expired." : message);
            }

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
            OnRefreshResult?.Invoke(success, message);
            if (success)
            {
                SetStatus(ConnectionStatus.Authenticated, "Session refreshed.");
                return;
            }

            LogWarning($"Refresh failed: {message}");
            OnSessionExpired?.Invoke(string.IsNullOrWhiteSpace(message) ? "Session refresh failed." : message);
            if (_connected)
                _ = SendLoginAsync();
        }

        private void HandleHeartbeatResponse(long _)
        {
            _heartbeatService?.OnHeartbeatAck(_);

            _smoothedRttMs = _heartbeatService != null ? _heartbeatService.EstimatedRttMs : 0f;
            _rttJitterMs = _heartbeatService != null ? _heartbeatService.EstimatedJitterMs : 0f;

            OnLatencyUpdated?.Invoke(_smoothedRttMs, _rttJitterMs);
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

            _heartbeatService?.TryTick(
                canRun: true,
                isInBackground: _isInBackground,
                sendHeartbeat: clientTicks =>
                {
                    _ = _queuedConnection.SendAsync(ClientMessageFactory.CreateHeartbeatRequest(clientTicks));
                    return true;
                },
                onTimeout: () =>
                {
                    LogWarning("Heartbeat timeout detected. Forcing reconnect.");
                    _connection.Disconnect();
                });
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
            ApplyApplicationBackgroundState(paused, fromUnityCallback: true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;

            if (!_isInBackground && !_connected && _autoReconnect)
                ScheduleReconnect("App focus restored.");
        }

        private void ApplyApplicationBackgroundState(bool inBackground, bool fromUnityCallback)
        {
            if (_isInBackground == inBackground)
                return;

            _isInBackground = inBackground;

            if (inBackground)
            {
                SetStatus(ConnectionStatus.Suspended, "App moved to background.");
                if (_disconnectOnBackground && _connected)
                    _connection?.Disconnect();

                return;
            }

            SetStatus(ConnectionStatus.Disconnected, "App resumed.");
            if (_autoReconnect)
            {
                string reason = fromUnityCallback
                    ? "Resumed from background."
                    : "Session controller requested foreground reconnect.";
                ScheduleReconnect(reason);
            }
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
            _networkInitialized = false;
        }

        private void UpdateIncomingPacketMetrics()
        {
            _lastPacketReceivedUtc = DateTime.UtcNow;
            _incomingPacketsWindowCount++;

            float now = Time.unscaledTime;
            if (_incomingPacketsWindowStart <= 0f)
                _incomingPacketsWindowStart = now;

            float elapsed = now - _incomingPacketsWindowStart;
            if (elapsed < 1f)
                return;

            _incomingPacketsPerSecond = _incomingPacketsWindowCount / Mathf.Max(0.001f, elapsed);
            _incomingPacketsWindowCount = 0;
            _incomingPacketsWindowStart = now;
        }
    }
}
