using System;
using System.Threading;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles authentication commands and state.
    /// </summary>
    public sealed class AuthClientService
    {
        private readonly IGameConnection _connection;
        private readonly NetworkEventStream _eventStream;

        private bool _isAwaitingLoginResponse;
        private bool _isAwaitingRefreshResponse;
        private CancellationTokenSource _loginTimeoutCts;
        private CancellationTokenSource _refreshTimeoutCts;
        private string _username;
        private string _password;

        public int RequestTimeoutMs { get; set; } = 12_000;

        public AuthClientService(IGameConnection connection, NetworkEventStream eventStream)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventStream = eventStream ?? throw new ArgumentNullException(nameof(eventStream));

            _eventStream.LoginTokenBundleReceived += HandleLoginResponse;
            _eventStream.RefreshTokenResponseReceived += HandleRefreshResponse;
            _eventStream.ErrorReceived += HandleError;
        }

        public bool IsAuthenticated { get; private set; }
        public string AccessToken { get; private set; }
        public DateTime AccessTokenExpiresAtUtc { get; private set; }
        public string RefreshToken { get; private set; }
        public DateTime RefreshTokenExpiresAtUtc { get; private set; }

        public bool HasValidAccessToken => !string.IsNullOrWhiteSpace(AccessToken) && DateTime.UtcNow < AccessTokenExpiresAtUtc;
        public bool CanRefresh => !string.IsNullOrWhiteSpace(RefreshToken) && DateTime.UtcNow < RefreshTokenExpiresAtUtc;

        public event Action<bool, string> LoginResultReceived;
        public event Action<bool, string> RefreshResultReceived;

        public void ConfigureCredentials(string username, string password)
        {
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
        }

        public async Task LoginAsync()
        {
            if (!_connection.IsConnected)
            {
                LoginResultReceived?.Invoke(false, "Login rejected: client is not connected.");
                return;
            }

            if (_isAwaitingLoginResponse)
                return;

            byte[] packet = ClientMessageFactory.CreateLoginRequest(_username, _password);
            _isAwaitingLoginResponse = true;
            ArmLoginTimeout();
            await _connection.SendAsync(packet);
        }

        public async Task RefreshAsync()
        {
            if (!_connection.IsConnected)
            {
                RefreshResultReceived?.Invoke(false, "Refresh rejected: client is not connected.");
                return;
            }

            if (_isAwaitingRefreshResponse)
                return;

            if (!CanRefresh)
            {
                RefreshResultReceived?.Invoke(false, "Refresh token is missing or expired.");
                return;
            }

            byte[] packet = ClientMessageFactory.CreateRefreshTokenRequest(RefreshToken);
            _isAwaitingRefreshResponse = true;
            ArmRefreshTimeout();
            await _connection.SendAsync(packet);
        }

        public async Task<bool> TryReauthenticateAsync()
        {
            if (CanRefresh)
            {
                await RefreshAsync();
                return IsAuthenticated;
            }

            await LoginAsync();
            return IsAuthenticated;
        }

        public void Reset()
        {
            IsAuthenticated = false;
            AccessToken = null;
            AccessTokenExpiresAtUtc = DateTime.MinValue;
            _isAwaitingLoginResponse = false;
            _isAwaitingRefreshResponse = false;
            CancelLoginTimeout();
            CancelRefreshTimeout();
        }

        public AuthSessionSnapshot CaptureSession()
        {
            return new AuthSessionSnapshot
            {
                AccessToken = AccessToken ?? string.Empty,
                AccessTokenExpiresAtUtcTicks = AccessTokenExpiresAtUtc.Ticks,
                RefreshToken = RefreshToken ?? string.Empty,
                RefreshTokenExpiresAtUtcTicks = RefreshTokenExpiresAtUtc.Ticks
            };
        }

        public void RestoreSession(AuthSessionSnapshot snapshot)
        {
            AccessToken = snapshot.AccessToken ?? string.Empty;
            AccessTokenExpiresAtUtc = snapshot.AccessTokenExpiresAtUtcTicks > 0
                ? new DateTime(snapshot.AccessTokenExpiresAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;

            RefreshToken = snapshot.RefreshToken ?? string.Empty;
            RefreshTokenExpiresAtUtc = snapshot.RefreshTokenExpiresAtUtcTicks > 0
                ? new DateTime(snapshot.RefreshTokenExpiresAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;

            IsAuthenticated = HasValidAccessToken;
        }

        public void ClearSession()
        {
            IsAuthenticated = false;
            AccessToken = null;
            AccessTokenExpiresAtUtc = DateTime.MinValue;
            RefreshToken = null;
            RefreshTokenExpiresAtUtc = DateTime.MinValue;
            _isAwaitingLoginResponse = false;
            _isAwaitingRefreshResponse = false;
            CancelLoginTimeout();
            CancelRefreshTimeout();
        }

        private void HandleLoginResponse(bool success, MuLike.Shared.Protocol.PacketContracts.TokenBundle tokens, string message)
        {
            ApplyTokenBundle(success, tokens);
            _isAwaitingLoginResponse = false;
            CancelLoginTimeout();
            LoginResultReceived?.Invoke(success, message);
        }

        private void HandleRefreshResponse(bool success, MuLike.Shared.Protocol.PacketContracts.TokenBundle tokens, string message)
        {
            ApplyTokenBundle(success, tokens);
            _isAwaitingRefreshResponse = false;
            CancelRefreshTimeout();
            RefreshResultReceived?.Invoke(success, message);
        }

        private void ApplyTokenBundle(bool success, MuLike.Shared.Protocol.PacketContracts.TokenBundle tokens)
        {
            IsAuthenticated = success;
            if (!success)
            {
                AccessToken = null;
                AccessTokenExpiresAtUtc = DateTime.MinValue;
                return;
            }

            AccessToken = tokens?.AccessToken;
            AccessTokenExpiresAtUtc = tokens != null && tokens.AccessTokenExpiresAtUtcTicks > 0
                ? new DateTime(tokens.AccessTokenExpiresAtUtcTicks, DateTimeKind.Utc)
                : DateTime.UtcNow.AddMinutes(15);

            if (tokens != null && !string.IsNullOrWhiteSpace(tokens.RefreshToken))
                RefreshToken = tokens.RefreshToken;

            if (tokens != null && tokens.RefreshTokenExpiresAtUtcTicks > 0)
                RefreshTokenExpiresAtUtc = new DateTime(tokens.RefreshTokenExpiresAtUtcTicks, DateTimeKind.Utc);
        }

        private void HandleError(string message)
        {
            if (_isAwaitingLoginResponse)
            {
                _isAwaitingLoginResponse = false;
                CancelLoginTimeout();
                LoginResultReceived?.Invoke(false, message);
            }

            if (_isAwaitingRefreshResponse)
            {
                _isAwaitingRefreshResponse = false;
                CancelRefreshTimeout();
                RefreshResultReceived?.Invoke(false, message);
            }
        }

        private void ArmLoginTimeout()
        {
            CancelLoginTimeout();
            _loginTimeoutCts = new CancellationTokenSource();
            _ = MonitorLoginTimeoutAsync(_loginTimeoutCts.Token);
        }

        private void ArmRefreshTimeout()
        {
            CancelRefreshTimeout();
            _refreshTimeoutCts = new CancellationTokenSource();
            _ = MonitorRefreshTimeoutAsync(_refreshTimeoutCts.Token);
        }

        private async Task MonitorLoginTimeoutAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Math.Max(1000, RequestTimeoutMs), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isAwaitingLoginResponse)
                return;

            _isAwaitingLoginResponse = false;
            LoginResultReceived?.Invoke(false, "Login request timeout.");
        }

        private async Task MonitorRefreshTimeoutAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(Math.Max(1000, RequestTimeoutMs), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_isAwaitingRefreshResponse)
                return;

            _isAwaitingRefreshResponse = false;
            RefreshResultReceived?.Invoke(false, "Refresh request timeout.");
        }

        private void CancelLoginTimeout()
        {
            if (_loginTimeoutCts == null)
                return;

            _loginTimeoutCts.Cancel();
            _loginTimeoutCts.Dispose();
            _loginTimeoutCts = null;
        }

        private void CancelRefreshTimeout()
        {
            if (_refreshTimeoutCts == null)
                return;

            _refreshTimeoutCts.Cancel();
            _refreshTimeoutCts.Dispose();
            _refreshTimeoutCts = null;
        }
    }
}
