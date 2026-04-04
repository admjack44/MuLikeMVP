using System;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// Transport abstraction for game client communication.
    /// </summary>
    public interface IGameConnection
    {
        bool IsConnected { get; }

        event Action Connected;
        event Action Disconnected;
        event Action<byte[]> PacketReceived;

        Task ConnectAsync();
        Task SendAsync(byte[] packet);
        void Disconnect();
    }
}
