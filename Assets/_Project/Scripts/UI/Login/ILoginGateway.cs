using System;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Networking;

namespace MuLike.UI.Login
{
    public interface ILoginGateway
    {
        bool IsConnected { get; }
        bool IsAuthenticated { get; }
        bool CanRefresh { get; }

        DateTime AccessTokenExpiresAtUtc { get; }

        event Action<bool, string> LoginResultReceived;
        event Action<bool, string> RefreshResultReceived;
        event Action<NetworkGameClient.ConnectionStatus, string> ConnectionStatusChanged;

        Task<bool> EnsureConnectedAsync(int timeoutMs, CancellationToken ct);
        void ConfigureCredentials(string username, string password);
        Task SendLoginAsync();
        Task SendRefreshAsync();
        void Logout(bool disconnect);

        AuthSessionSnapshot CaptureSession();
        void RestoreSession(AuthSessionSnapshot snapshot);
    }
}
