using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Optional lightweight send queue to avoid packet flood bursts on mobile.
    /// </summary>
    public sealed class CommandQueueGameConnection : IGameConnection
    {
        public sealed class Settings
        {
            public bool Enabled = false;
            public int MaxCommandsPerSecond = 30;
            public int MaxQueueSize = 128;
        }

        private readonly IGameConnection _inner;
        private readonly Settings _settings;
        private readonly Queue<byte[]> _pending = new();

        private bool _isDraining;
        private float _windowStartSec;
        private int _sentThisWindow;

        public CommandQueueGameConnection(IGameConnection inner, Settings settings = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _settings = settings ?? new Settings();

            _inner.Connected += () => Connected?.Invoke();
            _inner.Disconnected += () => Disconnected?.Invoke();
            _inner.PacketReceived += packet => PacketReceived?.Invoke(packet);
        }

        public bool IsConnected => _inner.IsConnected;
        public int QueuedCount => _pending.Count;
        public long DroppedCommands { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> PacketReceived;

        public Task ConnectAsync()
        {
            return _inner.ConnectAsync();
        }

        public Task SendAsync(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
                return Task.CompletedTask;

            if (!_settings.Enabled)
                return _inner.SendAsync(packet);

            if (_pending.Count >= Mathf.Max(8, _settings.MaxQueueSize))
            {
                DroppedCommands++;
                return Task.CompletedTask;
            }

            _pending.Enqueue(packet);
            return Task.CompletedTask;
        }

        public void Tick()
        {
            if (!_settings.Enabled || _isDraining || !_inner.IsConnected || _pending.Count == 0)
                return;

            _ = DrainAsync();
        }

        public void Disconnect()
        {
            _pending.Clear();
            _inner.Disconnect();
        }

        private async Task DrainAsync()
        {
            _isDraining = true;
            try
            {
                while (_inner.IsConnected && _pending.Count > 0)
                {
                    int allowance = ComputeAllowance();
                    if (allowance <= 0)
                        break;

                    while (allowance > 0 && _pending.Count > 0)
                    {
                        byte[] packet = _pending.Dequeue();
                        await _inner.SendAsync(packet);
                        _sentThisWindow++;
                        allowance--;
                    }
                }
            }
            finally
            {
                _isDraining = false;
            }
        }

        private int ComputeAllowance()
        {
            float now = Time.unscaledTime;
            if (_windowStartSec <= 0f || now - _windowStartSec >= 1f)
            {
                _windowStartSec = now;
                _sentThisWindow = 0;
            }

            int max = Mathf.Max(1, _settings.MaxCommandsPerSecond);
            return max - _sentThisWindow;
        }
    }
}
