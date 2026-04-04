using System;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// TCP implementation of IGameConnection using the existing NetworkClient.
    /// </summary>
    public sealed class TcpGameConnection : IGameConnection
    {
        private readonly string _host;
        private readonly int _port;
        private readonly NetworkClient _client;

        public TcpGameConnection(string host, int port)
        {
            _host = host;
            _port = port;
            _client = new NetworkClient();

            _client.OnConnected += () => Connected?.Invoke();
            _client.OnDisconnected += () => Disconnected?.Invoke();
            _client.OnPacketReceived += packet => PacketReceived?.Invoke(packet);
        }

        public bool IsConnected => _client.IsConnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> PacketReceived;

        public Task ConnectAsync()
        {
            return _client.ConnectAsync(_host, _port);
        }

        public Task SendAsync(byte[] packet)
        {
            return _client.SendAsync(packet);
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }
    }
}
