using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Shared.Protocol;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Manages the TCP connection to the game server. Handles connect, disconnect and raw receive loop.
    /// </summary>
    public class NetworkClient
    {
        public bool IsConnected => _tcpClient?.Connected ?? false;

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnPacketReceived;

        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();

            OnConnected?.Invoke();
            _ = ReceiveLoopAsync(_cts.Token);
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            OnDisconnected?.Invoke();
        }

        public async Task SendAsync(byte[] data)
        {
            if (!IsConnected) return;
            await _stream.WriteAsync(data, 0, data.Length);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var readBuffer = new byte[8192];
            var pending = new List<byte>(16384);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, token);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        pending.Add(readBuffer[i]);
                    }

                    int offset = 0;
                    while (PacketCodec.TryReadFrame(pending.ToArray(), offset, pending.Count - offset, out int frameLength))
                    {
                        var packet = new byte[frameLength];
                        pending.CopyTo(offset, packet, 0, frameLength);
                        offset += frameLength;
                        OnPacketReceived?.Invoke(packet);
                    }

                    if (offset > 0)
                    {
                        pending.RemoveRange(0, offset);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] Receive error: {ex.Message}");
            }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }
    }
}
