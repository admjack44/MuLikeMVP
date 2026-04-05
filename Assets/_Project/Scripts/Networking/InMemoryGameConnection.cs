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
        private const double SnapshotPollIntervalSeconds = 0.1d;

        private InMemoryGatewayBridge _bridge;
        private ServerApplication _serverApp;
        private Guid _sessionId;
        private bool _isConnected;
        private DateTime _nextSnapshotPollUtc;

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
            _nextSnapshotPollUtc = DateTime.UtcNow;

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

            PumpServerEvents(forceImmediate: true);

            return Task.CompletedTask;
        }

        public void PumpServerEvents()
        {
            PumpServerEvents(forceImmediate: false);
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

        private void PumpServerEvents(bool forceImmediate)
        {
            if (!_isConnected || _bridge == null)
                return;

            DateTime now = DateTime.UtcNow;
            if (!forceImmediate && now < _nextSnapshotPollUtc)
                return;

            _nextSnapshotPollUtc = now.AddSeconds(SnapshotPollIntervalSeconds);

            if (_bridge.TryPullSnapshotPacket(_sessionId, out byte[] packet) && packet != null)
            {
                PacketReceived?.Invoke(packet);
            }
        }
    }
}
