using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Mobile-optimized network manager for MU Online.
    ///
    /// Layered on top of <see cref="IGameConnection"/>: adds transparent packet compression,
    /// per-category throttled send queues, and exponential-backoff reconnection.
    ///
    /// Transport selection:
    ///   InMemory   → local dev / combat demo scenes
    ///   TCP        → production game server (primary)
    ///   WebSocket  → corporate/restricted networks (fallback)
    ///
    /// Throttle budgets (configurable in Inspector):
    ///   Position  → 20 Hz (0.050 s)   real-time movement / attacks
    ///   State     → 10 Hz (0.100 s)   HP, animations, status effects
    ///   Inventory →  5 Hz (0.200 s)   items, equipment, stats
    ///
    /// Upgrade path to Unity Netcode for GameObjects (NGO):
    ///   1. Add com.unity.netcode.gameobjects to Packages/manifest.json
    ///   2. Replace <see cref="CompressPayload"/> body with K4os.Compression.LZ4 (bundled by NGO)
    ///   3. The UNITY_NETCODE_GAMEOBJECTS define unlocks NGO-specific hooks below
    /// </summary>
    public sealed class MuNetworkManager : MonoBehaviour
    {
        // ── Enums ──────────────────────────────────────────────────────────────────

        public enum TransportMode { InMemory, Tcp, WebSocket }

        /// <summary>Data class assigned to each throttled send slot.</summary>
        public enum ThrottleCategory
        {
            /// <summary>20 Hz — position, velocity, facing.</summary>
            Position  = 0,
            /// <summary>10 Hz — animation states, HP deltas.</summary>
            State     = 1,
            /// <summary>5 Hz — inventory changes, equipment, stats.</summary>
            Inventory = 2
        }

        // ── Inspector fields ───────────────────────────────────────────────────────

        [Header("Transport")]
        [SerializeField] private TransportMode _transport = TransportMode.Tcp;
        [SerializeField] private string _tcpHost = "127.0.0.1";
        [SerializeField] private int    _tcpPort = 7777;
        [SerializeField] private string _webSocketUrl = "ws://127.0.0.1:7778";

        [Header("Compression")]
        [Tooltip("Prefix every packet with a 1-byte flag; deflate packets above the byte threshold.")]
        [SerializeField] private bool _enableCompression = true;
        [SerializeField, Min(64)] private int _compressionMinBytes = 128;

        [Header("Reconnection — exponential backoff")]
        [SerializeField] private bool _autoReconnect = true;
        [SerializeField, Range(1, 10)] private int   _maxReconnectAttempts   = 3;
        [SerializeField, Min(0.5f)]    private float _reconnectBaseDelay      = 1f;
        [SerializeField, Min(1f)]      private float _reconnectBackoffFactor  = 2f;

        [Header("Send Throttle (Hz)")]
        [SerializeField, Range(1, 60)] private int _positionHz  = 20;
        [SerializeField, Range(1, 30)] private int _stateHz     = 10;
        [SerializeField, Range(1, 10)] private int _inventoryHz =  5;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private IGameConnection _connection;
        private bool _connected;
        private bool _isConnecting;
        private int  _reconnectAttempt;
        private float _nextReconnectAt;

        private readonly Dictionary<ThrottleCategory, ThrottleBucket> _buckets = new(3);

        // Protocol header: 1 byte prepended to every packet (0 overhead after compression savings)
        private const byte FlagRaw        = 0x00;
        private const byte FlagCompressed = 0x01;

        // ── Public surface ─────────────────────────────────────────────────────────

        public bool IsConnected  => _connected;
        public bool IsConnecting => _isConnecting;
        public int  ReconnectAttempt => _reconnectAttempt;

        /// <summary>Exposes the raw <see cref="IGameConnection"/> for use by packet routers.</summary>
        public IGameConnection RawConnection => _connection;

        /// <summary>Optional anti-cheat/speed validator. Assign before <see cref="ConnectAsync"/>.</summary>
        public MuServerValidator Validator { get; set; }

        public event Action              Connected;
        public event Action              Disconnected;
        /// <summary>Fires with the decompressed application payload (no protocol flag byte).</summary>
        public event Action<byte[]>      PacketReceived;
        /// <summary>Fires when all reconnect attempts are exhausted.</summary>
        public event Action<int, string> ReconnectFailed;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _buckets[ThrottleCategory.Position]  = new ThrottleBucket(1f / Mathf.Max(1, _positionHz));
            _buckets[ThrottleCategory.State]      = new ThrottleBucket(1f / Mathf.Max(1, _stateHz));
            _buckets[ThrottleCategory.Inventory]  = new ThrottleBucket(1f / Mathf.Max(1, _inventoryHz));
        }

        private void Update()
        {
            HandleReconnectTick();
            FlushThrottleBuckets();
        }

        private void OnDestroy() => _connection?.Disconnect();

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Builds the selected transport and opens the connection.</summary>
        public async Task ConnectAsync()
        {
            if (_isConnecting || _connected) return;
            BuildConnection();
            try   { await _connection.ConnectAsync(); }
            catch (Exception ex) { Debug.LogWarning($"[MuNetworkManager] Connect error: {ex.Message}"); }
        }

        /// <summary>Close and disable auto-reconnect.</summary>
        public void Disconnect()
        {
            _autoReconnect = false;
            _connected     = false;
            _isConnecting  = false;
            _connection?.Disconnect();
        }

        /// <summary>
        /// Send immediately, bypassing throttle queues.
        /// Use for critical game data: auth, inventory confirmations, trade, chat.
        /// Maps to TCP-reliable semantics.
        /// </summary>
        public Task SendImmediateAsync(byte[] payload)
        {
            if (!_connected || payload == null || _connection == null)
                return Task.CompletedTask;

            return _connection.SendAsync(WrapPacket(payload));
        }

        /// <summary>
        /// Enqueue for throttled send. Only the latest payload per category is sent
        /// when the interval elapses — intermediate frames are silently dropped.
        /// Use for real-time data: position, animation states, inventory polls.
        /// Maps to UDP best-effort semantics.
        /// </summary>
        public void SendThrottled(ThrottleCategory category, byte[] payload)
        {
            if (!_connected || payload == null) return;
            _buckets[category].SetPending(payload);
        }

        /// <summary>
        /// Client-side position spam guard. Checked before <see cref="SendThrottled"/> for Position.
        /// Returns false and logs if movement speed exceeds <see cref="MuServerValidator.MaxMoveSpeedUps"/>.
        /// The server always has final authority.
        /// </summary>
        public bool TrySendPositionThrottled(byte[] positionPacket, Vector3 from, Vector3 to, float deltaTime)
        {
            if (Validator != null && !Validator.IsMovementSpeedValid(from, to, deltaTime))
            {
                Debug.LogWarning($"[MuNetworkManager] Dropped position packet — speed violation " +
                                 $"({Vector3.Distance(from, to) / Mathf.Max(0.001f, deltaTime):F1} u/s).");
                return false;
            }

            SendThrottled(ThrottleCategory.Position, positionPacket);
            return true;
        }

        // ── Connection factory ─────────────────────────────────────────────────────

        private void BuildConnection()
        {
            if (_connection != null)
            {
                _connection.Connected      -= HandleConnected;
                _connection.Disconnected   -= HandleDisconnected;
                _connection.PacketReceived -= HandleRawPacket;
            }

            IGameConnection raw = _transport switch
            {
                TransportMode.InMemory  => new InMemoryGameConnection(),
                TransportMode.WebSocket => new WebSocketGameConnection(_webSocketUrl),
                _                       => new TcpGameConnection(_tcpHost, _tcpPort)
            };

            _connection = raw;
            _connection.Connected      += HandleConnected;
            _connection.Disconnected   += HandleDisconnected;
            _connection.PacketReceived += HandleRawPacket;
            _isConnecting = true;
        }

        // ── Connection event handlers ──────────────────────────────────────────────

        private void HandleConnected()
        {
            _connected        = true;
            _isConnecting     = false;
            _reconnectAttempt = 0;
            Connected?.Invoke();
        }

        private void HandleDisconnected()
        {
            _connected    = false;
            _isConnecting = false;
            Disconnected?.Invoke();

            if (_autoReconnect && _reconnectAttempt < _maxReconnectAttempts)
                ScheduleNextReconnect();
            else if (_reconnectAttempt >= _maxReconnectAttempts)
                ReconnectFailed?.Invoke(_reconnectAttempt, "Max reconnect attempts reached.");
        }

        private void HandleRawPacket(byte[] raw)
        {
            if (raw == null || raw.Length < 1) return;

            byte flag     = raw[0];
            byte[] payload = flag == FlagCompressed
                ? DecompressSlice(raw, 1)
                : SliceFrom(raw, 1);

            if (payload != null && payload.Length > 0)
                PacketReceived?.Invoke(payload);
        }

        // ── Exponential backoff reconnection ───────────────────────────────────────

        private void ScheduleNextReconnect()
        {
            float delay = _reconnectBaseDelay * Mathf.Pow(_reconnectBackoffFactor, _reconnectAttempt);
            _nextReconnectAt = Time.unscaledTime + delay;
            _reconnectAttempt++;
            Debug.Log($"[MuNetworkManager] Reconnect {_reconnectAttempt}/{_maxReconnectAttempts} scheduled in {delay:F1}s.");
        }

        private void HandleReconnectTick()
        {
            if (_connected || _isConnecting || !_autoReconnect) return;
            if (_reconnectAttempt >= _maxReconnectAttempts) return;
            if (_nextReconnectAt <= 0f || Time.unscaledTime < _nextReconnectAt) return;

            _nextReconnectAt = 0f;
            _ = DoReconnectAsync();
        }

        private async Task DoReconnectAsync()
        {
            try
            {
                BuildConnection();
                await _connection.ConnectAsync();
            }
            catch (Exception ex)
            {
                _isConnecting = false;
                Debug.LogWarning($"[MuNetworkManager] Reconnect attempt {_reconnectAttempt} failed: {ex.Message}");

                if (_reconnectAttempt < _maxReconnectAttempts)
                    ScheduleNextReconnect();
                else
                    ReconnectFailed?.Invoke(_reconnectAttempt, ex.Message);
            }
        }

        // ── Throttle flush ─────────────────────────────────────────────────────────

        private void FlushThrottleBuckets()
        {
            if (!_connected) return;
            float now = Time.unscaledTime;
            foreach (ThrottleBucket bucket in _buckets.Values)
                bucket.TryFlush(now, FlushBucketPayload);
        }

        private void FlushBucketPayload(byte[] payload)
        {
            _ = _connection.SendAsync(WrapPacket(payload));
        }

        // ── Packet compression ─────────────────────────────────────────────────────

        private byte[] WrapPacket(byte[] payload)
        {
            return _enableCompression && payload.Length >= _compressionMinBytes
                ? CompressPayload(payload)
                : PrependFlag(FlagRaw, payload);
        }

        /// <summary>
        /// Baseline: Deflate via <see cref="System.IO.Compression.DeflateStream"/> —
        /// available in all Unity targets without extra packages.
        ///
        /// Upgrade to LZ4 (3× faster encode, similar ratio at 64-4096 byte game packets):
        ///   1. Add com.unity.netcode.gameobjects → K4os.Compression.LZ4 is bundled automatically.
        ///   2. Replace the DeflateStream block below with:
        ///        int maxLen = LZ4Codec.MaximumOutputSize(payload.Length);
        ///        byte[] dst  = new byte[1 + 4 + maxLen];
        ///        dst[0]      = FlagCompressed;
        ///        BinaryPrimitives.WriteInt32LittleEndian(dst.AsSpan(1), payload.Length);
        ///        int encoded = LZ4Codec.Encode(payload, 0, payload.Length, dst, 5, maxLen);
        ///        return dst[..(1 + 4 + encoded)];
        /// </summary>
        private byte[] CompressPayload(byte[] payload)
        {
            using MemoryStream ms = new();
            ms.WriteByte(FlagCompressed);
            using (DeflateStream ds = new(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                ds.Write(payload, 0, payload.Length);
            return ms.ToArray();
        }

        private static byte[] DecompressSlice(byte[] raw, int offset)
        {
            try
            {
                using MemoryStream input  = new(raw, offset, raw.Length - offset);
                using MemoryStream output = new();
                using DeflateStream ds    = new(input, CompressionMode.Decompress);
                ds.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MuNetworkManager] Decompress error: {ex.Message}");
                return null;
            }
        }

        private static byte[] PrependFlag(byte flag, byte[] payload)
        {
            byte[] buf = new byte[1 + payload.Length];
            buf[0] = flag;
            Buffer.BlockCopy(payload, 0, buf, 1, payload.Length);
            return buf;
        }

        private static byte[] SliceFrom(byte[] raw, int offset)
        {
            int len = raw.Length - offset;
            if (len <= 0) return Array.Empty<byte>();
            byte[] result = new byte[len];
            Buffer.BlockCopy(raw, offset, result, 0, len);
            return result;
        }

        // ── Nested: ThrottleBucket ─────────────────────────────────────────────────

        private sealed class ThrottleBucket
        {
            private readonly float _intervalSeconds;
            private float _lastFlushAt;
            private byte[] _pending;

            public ThrottleBucket(float intervalSeconds) => _intervalSeconds = intervalSeconds;

            /// <summary>Overwrites previous pending payload (latest-value semantics).</summary>
            public void SetPending(byte[] data) => _pending = data;

            public void TryFlush(float now, Action<byte[]> sender)
            {
                if (_pending == null) return;
                if (now - _lastFlushAt < _intervalSeconds) return;

                byte[] toSend = _pending;
                _pending      = null;
                _lastFlushAt  = now;
                sender(toSend);
            }
        }

        // ── Nested: WebSocketGameConnection ────────────────────────────────────────

        /// <summary>
        /// WebSocket fallback for restricted networks (corporate firewalls, captive portals).
        /// Implements <see cref="IGameConnection"/> via System.Net.WebSockets.ClientWebSocket —
        /// no external package required. Binary framing matches the custom MU protocol.
        /// </summary>
        private sealed class WebSocketGameConnection : IGameConnection
        {
            private readonly string _url;
            private ClientWebSocket _ws;
            private CancellationTokenSource _cts;
            private bool _connected;

            public WebSocketGameConnection(string url) => _url = url;

            public bool IsConnected => _connected && _ws?.State == WebSocketState.Open;

            public event Action Connected;
            public event Action Disconnected;
            public event Action<byte[]> PacketReceived;

            public async Task ConnectAsync()
            {
                _cts = new CancellationTokenSource();
                _ws  = new ClientWebSocket();
                try
                {
                    await _ws.ConnectAsync(new Uri(_url), _cts.Token);
                    _connected = true;
                    Connected?.Invoke();
                    _ = ReceiveLoopAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WebSocketGameConnection] Connect failed: {ex.Message}");
                    Disconnected?.Invoke();
                }
            }

            public async Task SendAsync(byte[] packet)
            {
                if (!IsConnected || _cts == null) return;
                try
                {
                    await _ws.SendAsync(
                        new ArraySegment<byte>(packet),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        _cts.Token);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WebSocketGameConnection] Send error: {ex.Message}");
                }
            }

            public void Disconnect()
            {
                _connected = false;
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _ = _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                _ws?.Dispose();
                _ws = null;
            }

            private async Task ReceiveLoopAsync()
            {
                byte[] buf = new byte[8192];
                using MemoryStream ms = new();

                while (IsConnected && _cts != null)
                {
                    ms.SetLength(0);
                    try
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                TriggerDisconnect();
                                return;
                            }
                            ms.Write(buf, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        PacketReceived?.Invoke(ms.ToArray());
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[WebSocketGameConnection] Receive error: {ex.Message}");
                        break;
                    }
                }

                TriggerDisconnect();
            }

            private void TriggerDisconnect()
            {
                if (!_connected) return;
                _connected = false;
                Disconnected?.Invoke();
            }
        }
    }

    // ── MuServerValidator ──────────────────────────────────────────────────────────

    /// <summary>
    /// Client-side pre-filtering rules for basic anti-cheat.
    /// Packets failing a check are dropped before reaching the network layer.
    /// The server always performs its own authoritative validation — this is
    /// defence-in-depth to reduce bandwidth waste from stale/invalid inputs.
    /// </summary>
    public sealed class MuServerValidator
    {
        /// <summary>Maximum character move speed (units/second). Exceeding this drops the packet.</summary>
        public float MaxMoveSpeedUps { get; set; } = 12f;

        /// <summary>
        /// Per-skill cooldown registry. Maps skillId → earliest next cast time (unscaled).
        /// </summary>
        private readonly Dictionary<int, float> _cooldownMap = new();

        /// <summary>15% tolerance added to account for jitter from high-latency inputs.</summary>
        public bool IsMovementSpeedValid(Vector3 from, Vector3 to, float deltaTime)
        {
            if (deltaTime < 0.001f) return true;
            float speed = Vector3.Distance(from, to) / deltaTime;
            return speed <= MaxMoveSpeedUps * 1.15f;
        }

        /// <summary>
        /// Returns true and registers the cast if skill is off cooldown; false if still cooling down.
        /// </summary>
        public bool TryRegisterSkillCast(int skillId, float cooldownSeconds)
        {
            float now = Time.unscaledTime;
            if (_cooldownMap.TryGetValue(skillId, out float nextAvailAt) && now < nextAvailAt)
                return false;

            _cooldownMap[skillId] = now + cooldownSeconds;
            return true;
        }

        public void ResetCooldowns() => _cooldownMap.Clear();
    }
}
