using System;
using System.Collections.Generic;
using System.Net;
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
        public sealed class Options
        {
            public int ConnectTimeoutMs { get; set; } = 8_000;
            public int ReceiveTimeoutMs { get; set; } = 30_000;
            public int SendTimeoutMs { get; set; } = 8_000;
            public bool EnableTcpKeepAlive { get; set; } = true;
            public int KeepAliveTimeMs { get; set; } = 15_000;
            public int KeepAliveIntervalMs { get; set; } = 5_000;
        }

        public bool IsConnected => _tcpClient?.Connected ?? false;

        private readonly Options _options;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private int _disconnectNotified;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnPacketReceived;

        public NetworkClient(Options options = null)
        {
            _options = options ?? new Options();
        }

        public async Task ConnectAsync(string host, int port)
        {
            CloseInternal(notifyDisconnected: false);

            _tcpClient = new TcpClient();

            using (var connectCts = new CancellationTokenSource())
            {
                connectCts.CancelAfter(Mathf.Max(500, _options.ConnectTimeoutMs));
                Task connectTask = _tcpClient.ConnectAsync(host, port);
                Task completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, connectCts.Token));
                if (completed != connectTask)
                    throw new TimeoutException($"Connect timeout after {_options.ConnectTimeoutMs}ms.");

                await connectTask;
            }

            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();
            Interlocked.Exchange(ref _disconnectNotified, 0);

            if (_options.EnableTcpKeepAlive)
                ConfigureKeepAlive(_tcpClient.Client);

            OnConnected?.Invoke();
            _ = ReceiveLoopAsync(_cts.Token);
        }

        public void Disconnect()
        {
            CloseInternal(notifyDisconnected: true);
        }

        public async Task SendAsync(byte[] data)
        {
            if (!IsConnected || data == null || data.Length == 0)
                return;

            using var sendCts = new CancellationTokenSource();
            sendCts.CancelAfter(Mathf.Max(500, _options.SendTimeoutMs));
            await _stream.WriteAsync(data, 0, data.Length, sendCts.Token);
            await _stream.FlushAsync(sendCts.Token);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var readBuffer = new byte[8192];
            var pending = new List<byte>(16384);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int bytesRead;
                    using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        readCts.CancelAfter(Mathf.Max(500, _options.ReceiveTimeoutMs));
                        bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, readCts.Token);
                    }

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
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"[NetworkClient] Receive timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] Receive error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void ConfigureKeepAlive(Socket socket)
        {
            if (socket == null)
                return;

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            byte[] keepAlive = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(keepAlive, 0);
            BitConverter.GetBytes((uint)Mathf.Max(1_000, _options.KeepAliveTimeMs)).CopyTo(keepAlive, 4);
            BitConverter.GetBytes((uint)Mathf.Max(1_000, _options.KeepAliveIntervalMs)).CopyTo(keepAlive, 8);

            socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
        }

        private void CloseInternal(bool notifyDisconnected)
        {
            if (notifyDisconnected && Interlocked.Exchange(ref _disconnectNotified, 1) == 1)
                return;

            _cts?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            _cts = null;
            _stream = null;
            _tcpClient = null;

            if (notifyDisconnected)
                OnDisconnected?.Invoke();
        }
    }
}
