using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Mobile-ready session orchestrator for connection/auth/retries and app lifecycle.
    /// </summary>
    public sealed class SessionConnectionController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NetworkGameClient _networkClient;

        [Header("Startup")]
        [SerializeField] private bool _autoConnectOnEnable = true;
        [SerializeField] private bool _autoAuthenticate = true;

        [Header("Timeouts")]
        [SerializeField, Min(1000)] private int _connectTimeoutMs = 10000;
        [SerializeField, Min(1000)] private int _authenticateTimeoutMs = 12000;

        [Header("Retry")]
        [SerializeField] private bool _retryEnabled = true;
        [SerializeField, Min(0.25f)] private float _retryInitialDelaySeconds = 1f;
        [SerializeField, Min(0.5f)] private float _retryMaxDelaySeconds = 15f;

        [Header("Lifecycle")]
        [SerializeField] private bool _pauseConnectionOnBackground = true;
        [SerializeField] private bool _attemptReconnectOnForeground = true;
        [SerializeField] private bool _sendHeartbeatOnForeground = true;

        private CancellationTokenSource _connectFlowCts;
        private bool _backgroundState;
        private bool _isConnectingFlow;
        private int _retryAttempt;
        private float _nextRetryAt;
        private string _lastErrorMessage = string.Empty;

        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;
        public bool IsConnected => _networkClient != null && _networkClient.IsConnected;
        public bool IsAuthenticated => _networkClient != null && _networkClient.IsAuthenticated;
        public float SmoothedRttMs => _networkClient != null ? _networkClient.SmoothedRttMs : 0f;
        public float RttJitterMs => _networkClient != null ? _networkClient.RttJitterMs : 0f;
        public DateTime LastPacketReceivedUtc => _networkClient != null ? _networkClient.LastPacketReceivedUtc : DateTime.MinValue;
        public float IncomingPacketsPerSecond => _networkClient != null ? _networkClient.IncomingPacketsPerSecond : 0f;

        public event Action<NetworkConnectionState, string> OnConnectionStateChanged;
        public event Action<float, float> OnLatencyUpdated;
        public event Action<string> OnSessionExpired;

        private void Awake()
        {
            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();

            if (_networkClient == null)
            {
                SetState(NetworkConnectionState.Error, "SessionConnectionController requires NetworkGameClient.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_networkClient == null)
                return;

            _networkClient.OnConnectionStatusChanged += HandleClientConnectionStatusChanged;
            _networkClient.OnLatencyUpdated += HandleLatencyUpdated;
            _networkClient.OnSessionExpired += HandleSessionExpired;
            _networkClient.OnLoginResult += HandleLoginResult;

            SetState(MapState(_networkClient.Status), "Session controller enabled.");

            if (_autoConnectOnEnable)
                _ = ConnectSessionAsync();
        }

        private void OnDisable()
        {
            CancelConnectFlow();

            if (_networkClient == null)
                return;

            _networkClient.OnConnectionStatusChanged -= HandleClientConnectionStatusChanged;
            _networkClient.OnLatencyUpdated -= HandleLatencyUpdated;
            _networkClient.OnSessionExpired -= HandleSessionExpired;
            _networkClient.OnLoginResult -= HandleLoginResult;
        }

        private void Update()
        {
            if (!_retryEnabled || _backgroundState || _isConnectingFlow)
                return;

            if (State != NetworkConnectionState.Error && State != NetworkConnectionState.Reconnecting)
                return;

            if (Time.unscaledTime < _nextRetryAt)
                return;

            _ = ConnectSessionAsync();
        }

        public async Task<bool> ConnectSessionAsync(CancellationToken ct = default)
        {
            if (_networkClient == null)
            {
                SetState(NetworkConnectionState.Error, "NetworkGameClient is not available.");
                return false;
            }

            if (_isConnectingFlow)
                return false;

            CancelConnectFlow();
            _connectFlowCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            CancellationToken linkedCt = _connectFlowCts.Token;
            _isConnectingFlow = true;

            try
            {
                SetState(NetworkConnectionState.Connecting, "Connecting...");
                bool connected = await _networkClient.EnsureConnectedAsync(_connectTimeoutMs, linkedCt);
                if (!connected)
                {
                    HandleConnectionFailure("Connection timeout.");
                    return false;
                }

                SetState(NetworkConnectionState.Connected, "Connected.");

                if (!_autoAuthenticate)
                {
                    _retryAttempt = 0;
                    return true;
                }

                if (_networkClient.IsAuthenticated)
                {
                    SetState(NetworkConnectionState.Authenticated, "Authenticated.");
                    _retryAttempt = 0;
                    return true;
                }

                SetState(NetworkConnectionState.Authenticating, "Authenticating...");
                await _networkClient.SendLoginAsync();

                bool authenticated = await _networkClient.WaitForAuthenticatedAsync(_authenticateTimeoutMs, linkedCt);
                if (!authenticated)
                {
                    HandleConnectionFailure("Authentication timeout.");
                    return false;
                }

                SetState(NetworkConnectionState.Authenticated, "Authenticated.");
                _retryAttempt = 0;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                HandleConnectionFailure($"Connection flow error: {ex.Message}");
                return false;
            }
            finally
            {
                _isConnectingFlow = false;
            }
        }

        public void DisconnectSession(bool clearAuth = false)
        {
            CancelConnectFlow();

            if (_networkClient == null)
                return;

            if (clearAuth)
                _networkClient.Logout(disconnect: true);
            else
                _networkClient.DisconnectTransport(scheduleReconnect: false, reason: "Disconnected by controller.");

            SetState(NetworkConnectionState.Disconnected, "Disconnected by controller.");
        }

        public void ForceReconnect(string reason = "Manual reconnect requested.")
        {
            if (_networkClient == null)
                return;

            _networkClient.RequestReconnect(reason);
            SetState(NetworkConnectionState.Reconnecting, reason);
        }

        private void HandleClientConnectionStatusChanged(NetworkGameClient.ConnectionStatus status, string message)
        {
            NetworkConnectionState mapped = MapState(status);
            SetState(mapped, string.IsNullOrWhiteSpace(message) ? mapped.ToString() : message);

            if (mapped == NetworkConnectionState.Disconnected && !_backgroundState && _retryEnabled)
                ScheduleRetry("Disconnected.");
        }

        private void HandleLatencyUpdated(float rttMs, float jitterMs)
        {
            OnLatencyUpdated?.Invoke(rttMs, jitterMs);
        }

        private void HandleSessionExpired(string message)
        {
            string text = string.IsNullOrWhiteSpace(message) ? "Session expired." : message;
            SetState(NetworkConnectionState.Error, text);
            OnSessionExpired?.Invoke(text);
            ScheduleRetry(text);
        }

        private void HandleLoginResult(bool success, string message)
        {
            if (success)
            {
                SetState(NetworkConnectionState.Authenticated, "Authenticated.");
                _retryAttempt = 0;
                return;
            }

            string text = string.IsNullOrWhiteSpace(message) ? "Authentication failed." : message;
            SetState(NetworkConnectionState.Error, text);
            ScheduleRetry(text);
        }

        private void HandleConnectionFailure(string message)
        {
            _lastErrorMessage = message ?? string.Empty;
            SetState(NetworkConnectionState.Error, _lastErrorMessage);
            ScheduleRetry(_lastErrorMessage);
        }

        private void ScheduleRetry(string reason)
        {
            if (!_retryEnabled || _backgroundState)
                return;

            _retryAttempt++;
            float delay = Mathf.Min(
                Mathf.Max(0.5f, _retryMaxDelaySeconds),
                Mathf.Max(0.25f, _retryInitialDelaySeconds) * Mathf.Pow(2f, _retryAttempt - 1));

            _nextRetryAt = Time.unscaledTime + delay;
            SetState(NetworkConnectionState.Reconnecting, $"{reason} Retrying in {delay:F1}s.");
        }

        private void OnApplicationPause(bool paused)
        {
            _backgroundState = paused;

            if (_networkClient == null)
                return;

            if (_pauseConnectionOnBackground)
                _networkClient.SetBackgroundState(paused);

            if (!paused && _attemptReconnectOnForeground)
            {
                if (_sendHeartbeatOnForeground)
                    _networkClient.TrySendHeartbeatPing();

                _ = ConnectSessionAsync();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || _backgroundState || !_attemptReconnectOnForeground)
                return;

            _ = ConnectSessionAsync();
        }

        private void CancelConnectFlow()
        {
            if (_connectFlowCts == null)
                return;

            _connectFlowCts.Cancel();
            _connectFlowCts.Dispose();
            _connectFlowCts = null;
        }

        private void SetState(NetworkConnectionState state, string message)
        {
            bool changed = State != state;
            State = state;

            if (changed || !string.IsNullOrWhiteSpace(message))
                OnConnectionStateChanged?.Invoke(state, message ?? string.Empty);
        }

        private static NetworkConnectionState MapState(NetworkGameClient.ConnectionStatus status)
        {
            return status switch
            {
                NetworkGameClient.ConnectionStatus.Disconnected => NetworkConnectionState.Disconnected,
                NetworkGameClient.ConnectionStatus.Connecting => NetworkConnectionState.Connecting,
                NetworkGameClient.ConnectionStatus.Connected => NetworkConnectionState.Connected,
                NetworkGameClient.ConnectionStatus.Authenticating => NetworkConnectionState.Authenticating,
                NetworkGameClient.ConnectionStatus.Authenticated => NetworkConnectionState.Authenticated,
                NetworkGameClient.ConnectionStatus.Reconnecting => NetworkConnectionState.Reconnecting,
                NetworkGameClient.ConnectionStatus.Suspended => NetworkConnectionState.Disconnected,
                _ => NetworkConnectionState.Error
            };
        }
    }
}
