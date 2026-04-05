using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
            public int InitialPendingBufferBytes { get; set; } = 16_384;
        }

        public bool IsConnected => _tcpClient?.Connected ?? false;
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public DateTime LastPacketReceivedUtc { get; private set; } = DateTime.MinValue;
        public float ReceivedPacketsPerSecond { get; private set; }
        public long TotalPacketsReceived { get; private set; }

        private readonly Options _options;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private int _disconnectNotified;
        private int _closeStarted;
        private float _packetMetricsWindowStart;
        private int _packetsInCurrentWindow;

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
            State = ConnectionState.Connecting;

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
            Interlocked.Exchange(ref _closeStarted, 0);
            _packetMetricsWindowStart = Time.unscaledTime;
            _packetsInCurrentWindow = 0;
            ReceivedPacketsPerSecond = 0f;
            TotalPacketsReceived = 0;
            LastPacketReceivedUtc = DateTime.MinValue;

            if (_options.EnableTcpKeepAlive)
                ConfigureKeepAlive(_tcpClient.Client);

            State = ConnectionState.Connected;
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

            await _sendLock.WaitAsync();
            try
            {
                if (!IsConnected || _stream == null)
                    return;

                using var sendCts = new CancellationTokenSource();
                sendCts.CancelAfter(Mathf.Max(500, _options.SendTimeoutMs));
                await _stream.WriteAsync(data, 0, data.Length, sendCts.Token);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var readBuffer = new byte[8192];
            int pendingCapacity = Mathf.Max(2048, _options.InitialPendingBufferBytes);
            byte[] pendingBuffer = new byte[pendingCapacity];
            int pendingCount = 0;
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

                    EnsurePendingCapacity(ref pendingBuffer, pendingCount + bytesRead);
                    Buffer.BlockCopy(readBuffer, 0, pendingBuffer, pendingCount, bytesRead);
                    pendingCount += bytesRead;

                    int offset = 0;
                    while (pendingCount - offset >= 2)
                    {
                        if (!PacketCodec.TryReadFrame(pendingBuffer, offset, pendingCount - offset, out int frameLength))
                            break;

                        var packet = new byte[frameLength];
                        Buffer.BlockCopy(pendingBuffer, offset, packet, 0, frameLength);
                        offset += frameLength;

                        LastPacketReceivedUtc = DateTime.UtcNow;
                        UpdatePacketMetrics();
                        OnPacketReceived?.Invoke(packet);
                    }

                    if (offset > 0)
                        ShiftPendingLeft(pendingBuffer, ref pendingCount, offset);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsurePendingCapacity(ref byte[] buffer, int needed)
        {
            if (buffer.Length >= needed)
                return;

            int next = buffer.Length;
            while (next < needed)
                next *= 2;

            Array.Resize(ref buffer, next);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ShiftPendingLeft(byte[] pendingBuffer, ref int pendingCount, int consumed)
        {
            int remaining = pendingCount - consumed;
            if (remaining > 0)
                Buffer.BlockCopy(pendingBuffer, consumed, pendingBuffer, 0, remaining);

            pendingCount = Mathf.Max(0, remaining);
        }

        private void UpdatePacketMetrics()
        {
            TotalPacketsReceived++;
            _packetsInCurrentWindow++;

            float now = Time.unscaledTime;
            if (_packetMetricsWindowStart <= 0f)
                _packetMetricsWindowStart = now;

            float elapsed = now - _packetMetricsWindowStart;
            if (elapsed < 1f)
                return;

            ReceivedPacketsPerSecond = _packetsInCurrentWindow / Mathf.Max(0.001f, elapsed);
            _packetMetricsWindowStart = now;
            _packetsInCurrentWindow = 0;
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
            bool firstClose = Interlocked.Exchange(ref _closeStarted, 1) == 0;
            if (firstClose)
            {
                State = ConnectionState.Closing;
                _cts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
                _cts = null;
                _stream = null;
                _tcpClient = null;
                State = ConnectionState.Disconnected;
            }

            if (!notifyDisconnected)
                return;

            if (Interlocked.Exchange(ref _disconnectNotified, 1) == 0)
                OnDisconnected?.Invoke();
        }
    }
}
