using System;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Networking;

namespace MuLike.UI.Login
{
    public sealed class NetworkGameClientLoginGateway : ILoginGateway
    {
        private readonly NetworkGameClient _client;

        public NetworkGameClientLoginGateway(NetworkGameClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _client.OnLoginResult += HandleLoginResult;
            _client.OnRefreshResult += HandleRefreshResult;
            _client.OnConnectionStatusChanged += HandleConnectionStatusChanged;
        }

        public bool IsConnected => _client.IsConnected;
        public bool IsAuthenticated => _client.IsAuthenticated;
        public bool CanRefresh => _client.CanRefreshToken;
        public DateTime AccessTokenExpiresAtUtc => _client.AccessTokenExpiresAtUtc;

        public event Action<bool, string> LoginResultReceived;
        public event Action<bool, string> RefreshResultReceived;
        public event Action<NetworkGameClient.ConnectionStatus, string> ConnectionStatusChanged;

        public async Task<bool> EnsureConnectedAsync(int timeoutMs, CancellationToken ct)
        {
            return await _client.EnsureConnectedAsync(timeoutMs, ct);
        }

        public void ConfigureCredentials(string username, string password)
        {
            _client.ConfigureCredentials(username, password);
        }

        public async Task SendLoginAsync()
        {
            await _client.SendLoginAsync();
        }

        public async Task SendRefreshAsync()
        {
            await _client.SendRefreshAsync();
        }

        public void Logout(bool disconnect)
        {
            _client.Logout(disconnect);
        }

        public AuthSessionSnapshot CaptureSession()
        {
            return _client.CaptureAuthSession();
        }

        public void RestoreSession(AuthSessionSnapshot snapshot)
        {
            _client.RestoreAuthSession(snapshot);
        }

        private void HandleLoginResult(bool success, string message)
        {
            LoginResultReceived?.Invoke(success, message);
        }

        private void HandleRefreshResult(bool success, string message)
        {
            RefreshResultReceived?.Invoke(success, message);
        }

        private void HandleConnectionStatusChanged(NetworkGameClient.ConnectionStatus status, string message)
        {
            ConnectionStatusChanged?.Invoke(status, message);
        }
    }
}
