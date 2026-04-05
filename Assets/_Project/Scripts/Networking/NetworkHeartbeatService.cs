using System;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Heartbeat ticker + timeout watchdog + RTT estimator.
    /// </summary>
    public sealed class NetworkHeartbeatService
    {
        public sealed class Settings
        {
            public bool Enabled = true;
            public float IntervalSeconds = 5f;
            public float TimeoutSeconds = 12f;
            public float RttSmoothing = 0.15f;
            public float JitterSmoothing = 0.20f;
        }

        private readonly Settings _settings;

        private float _lastSentAt;
        private float _lastAckAt;
        private long _pendingClientTicks;
        private double _pendingSentSec;
        private bool _initialized;

        public float EstimatedRttMs { get; private set; }
        public float EstimatedJitterMs { get; private set; }
        public DateTime LastHeartbeatAckUtc { get; private set; } = DateTime.MinValue;

        public NetworkHeartbeatService(Settings settings = null)
        {
            _settings = settings ?? new Settings();
        }

        public void Reset()
        {
            _initialized = false;
            _lastSentAt = 0f;
            _lastAckAt = 0f;
            _pendingClientTicks = 0;
            _pendingSentSec = 0d;
            EstimatedRttMs = 0f;
            EstimatedJitterMs = 0f;
            LastHeartbeatAckUtc = DateTime.MinValue;
        }

        public bool TryTick(bool canRun, bool isInBackground, Func<long, bool> sendHeartbeat, Action onTimeout)
        {
            if (!_settings.Enabled || !canRun || isInBackground)
                return false;

            float now = Time.unscaledTime;
            if (!_initialized)
            {
                _initialized = true;
                _lastAckAt = now;
                _lastSentAt = now;
            }

            if (now - _lastSentAt >= Mathf.Max(1f, _settings.IntervalSeconds))
            {
                long clientTicks = DateTime.UtcNow.Ticks;
                bool sent = sendHeartbeat != null && sendHeartbeat(clientTicks);
                if (sent)
                {
                    _lastSentAt = now;
                    _pendingClientTicks = clientTicks;
                    _pendingSentSec = Time.unscaledTimeAsDouble;
                }
            }

            if (now - _lastAckAt > Mathf.Max(2f, _settings.TimeoutSeconds))
            {
                onTimeout?.Invoke();
                _lastAckAt = now;
                return true;
            }

            return false;
        }

        public void OnHeartbeatAck(long _serverUtcTicks)
        {
            _lastAckAt = Time.unscaledTime;
            LastHeartbeatAckUtc = DateTime.UtcNow;

            if (_pendingClientTicks <= 0 || _pendingSentSec <= 0d)
                return;

            float rttMs = (float)((Time.unscaledTimeAsDouble - _pendingSentSec) * 1000d);
            if (EstimatedRttMs <= 0.01f)
                EstimatedRttMs = rttMs;
            else
                EstimatedRttMs = Mathf.Lerp(EstimatedRttMs, rttMs, Mathf.Clamp01(_settings.RttSmoothing));

            float delta = Mathf.Abs(rttMs - EstimatedRttMs);
            if (EstimatedJitterMs <= 0.01f)
                EstimatedJitterMs = delta;
            else
                EstimatedJitterMs = Mathf.Lerp(EstimatedJitterMs, delta, Mathf.Clamp01(_settings.JitterSmoothing));

            _pendingClientTicks = 0;
            _pendingSentSec = 0d;
        }
    }
}
