using System;
using System.Threading.Tasks;
using MuLike.Server.Infrastructure;

namespace MuLike.Networking
{
    /// <summary>
    /// In-memory loopback connection for local iteration.
    /// </summary>
    public sealed class InMemoryGameConnection : IGameConnection
    {
        private InMemoryGatewayBridge _bridge;
        private ServerApplication _serverApp;
        private Guid _sessionId;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> PacketReceived;

        public async Task ConnectAsync()
        {
            if (_isConnected) return;

            var startup = await ServerBootstrap.StartDefaultAsync();
            _serverApp = startup.app;
            _bridge = startup.bridge;
            _sessionId = startup.sessionId;
            _isConnected = true;

            Connected?.Invoke();
        }

        public Task SendAsync(byte[] packet)
        {
            if (!_isConnected || packet == null) return Task.CompletedTask;

            byte[] response = _bridge.Send(_sessionId, packet);
            if (response != null)
            {
                PacketReceived?.Invoke(response);
            }

            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;

            _serverApp?.Stop();
            _serverApp = null;
            _bridge = null;

            Disconnected?.Invoke();
        }
    }
}
