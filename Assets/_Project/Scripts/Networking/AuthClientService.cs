using System;
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
        private string _username;
        private string _password;

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
        }

        private void HandleLoginResponse(bool success, MuLike.Shared.Protocol.PacketContracts.TokenBundle tokens, string message)
        {
            ApplyTokenBundle(success, tokens);
            _isAwaitingLoginResponse = false;
            LoginResultReceived?.Invoke(success, message);
        }

        private void HandleRefreshResponse(bool success, MuLike.Shared.Protocol.PacketContracts.TokenBundle tokens, string message)
        {
            ApplyTokenBundle(success, tokens);
            _isAwaitingRefreshResponse = false;
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
                LoginResultReceived?.Invoke(false, message);
            }

            if (_isAwaitingRefreshResponse)
            {
                _isAwaitingRefreshResponse = false;
                RefreshResultReceived?.Invoke(false, message);
            }
        }
    }
}
