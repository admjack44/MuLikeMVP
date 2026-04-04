using System;
using System.Net;
using System.Threading.Tasks;
using MuLike.Server.Infrastructure;
using MuLike.Shared.Protocol;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MuLike.Networking
{
    /// <summary>
    /// Play Mode integration client that sends login/move/skill packets and parses server responses.
    /// Supports in-memory bridge for local iteration and TCP for remote server testing.
    /// </summary>
    public class NetworkGameClient : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool _useInMemoryGateway = true;

        [Header("TCP")]
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7777;

        [Header("Auth")]
        [SerializeField] private string _username = "admin";
        [SerializeField] private string _password = "admin123";

        [Header("Debug Inputs")]
        [SerializeField] private KeyCode _loginKey = KeyCode.F1;
        [SerializeField] private KeyCode _moveKey = KeyCode.F2;
        [SerializeField] private KeyCode _skillKey = KeyCode.F3;

        private PacketRouter _packetRouter;
        private NetworkClient _networkClient;

        private ServerApplication _serverApp;
        private InMemoryGatewayBridge _bridge;
        private Guid _sessionId;

        private bool _connected;
        private bool _authenticated;
        private string _accessToken;

        public bool IsConnected => _connected;
        public bool IsAuthenticated => _authenticated;
        public event Action<string> OnClientLog;

        private async void Start()
        {
            _packetRouter = new PacketRouter();
            RegisterPacketHandlers();

            if (_useInMemoryGateway)
            {
                await StartInMemoryAsync();
            }
            else
            {
                await ConnectTcpAsync();
            }
        }

        private void Update()
        {
            if (!_connected) return;

            if (WasKeyPressed(_loginKey))
            {
                _ = SendLoginAsync();
            }

            if (_authenticated && WasKeyPressed(_moveKey))
            {
                Vector3 target = transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                _ = SendMoveAsync(target.x, target.y, target.z);
            }

            if (_authenticated && WasKeyPressed(_skillKey))
            {
                // Demo target entity id. In a real flow this comes from TargetingController.
                _ = SendSkillCastAsync(skillId: 1, targetId: 999);
            }
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return keyCode switch
            {
                KeyCode.F1 => keyboard.f1Key.wasPressedThisFrame,
                KeyCode.F2 => keyboard.f2Key.wasPressedThisFrame,
                KeyCode.F3 => keyboard.f3Key.wasPressedThisFrame,
                KeyCode.F4 => keyboard.f4Key.wasPressedThisFrame,
                KeyCode.F5 => keyboard.f5Key.wasPressedThisFrame,
                KeyCode.Alpha1 => keyboard.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => keyboard.digit2Key.wasPressedThisFrame,
                KeyCode.Alpha3 => keyboard.digit3Key.wasPressedThisFrame,
                KeyCode.Alpha4 => keyboard.digit4Key.wasPressedThisFrame,
                KeyCode.Alpha5 => keyboard.digit5Key.wasPressedThisFrame,
                KeyCode.Alpha6 => keyboard.digit6Key.wasPressedThisFrame,
                KeyCode.Alpha7 => keyboard.digit7Key.wasPressedThisFrame,
                KeyCode.Alpha8 => keyboard.digit8Key.wasPressedThisFrame,
                KeyCode.Alpha9 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

        public async Task SendLoginAsync()
        {
            byte[] packet = ClientMessageFactory.CreateLoginRequest(_username, _password);
            await SendPacketAsync(packet);
        }

        public async Task SendMoveAsync(float x, float y, float z)
        {
            byte[] packet = ClientMessageFactory.CreateMoveRequest(x, y, z);
            await SendPacketAsync(packet);
        }

        public async Task SendSkillCastAsync(int skillId, int targetId)
        {
            byte[] packet = ClientMessageFactory.CreateSkillCastRequest(skillId, targetId);
            await SendPacketAsync(packet);
        }

        private async Task StartInMemoryAsync()
        {
            var startup = await ServerBootstrap.StartDefaultAsync();
            _serverApp = startup.app;
            _bridge = startup.bridge;
            _sessionId = startup.sessionId;
            _connected = true;

            Log("Connected via InMemoryGatewayBridge.");
        }

        private async Task ConnectTcpAsync()
        {
            _networkClient = new NetworkClient();
            _networkClient.OnPacketReceived += HandleIncomingPacket;
            _networkClient.OnConnected += () =>
            {
                _connected = true;
                Log("Connected via TCP.");
            };
            _networkClient.OnDisconnected += () =>
            {
                _connected = false;
                _authenticated = false;
                Log("Disconnected.");
            };

            await _networkClient.ConnectAsync(_host, _port);

            // Session identifier for remote servers can be set during login response in a future iteration.
            _sessionId = Guid.NewGuid();
        }

        private async Task SendPacketAsync(byte[] packet)
        {
            if (!_connected || packet == null) return;

            if (_useInMemoryGateway)
            {
                byte[] response = _bridge.Send(_sessionId, packet);
                if (response != null)
                {
                    _packetRouter.Route(response);
                }
            }
            else
            {
                await _networkClient.SendAsync(packet);
            }
        }

        private void HandleIncomingPacket(byte[] packet)
        {
            _packetRouter.Route(packet);
        }

        private void RegisterPacketHandlers()
        {
            _packetRouter.Register(NetOpcodes.LoginResponse, payload =>
            {
                if (!ServerMessageParser.TryParseLoginResponse(payload, out bool success, out string token, out string message))
                {
                    LogWarning("Invalid LoginResponse payload.");
                    return;
                }

                _authenticated = success;
                _accessToken = token;
                Log($"Login response: success={success}, msg={message}");
            });

            _packetRouter.Register(NetOpcodes.MoveResponse, payload =>
            {
                if (!ServerMessageParser.TryParseMoveResponse(payload, out bool success, out float x, out float y, out float z, out string message))
                {
                    LogWarning("Invalid MoveResponse payload.");
                    return;
                }

                if (success)
                {
                    transform.position = new Vector3(x, y, z);
                }

                Log($"Move response: success={success}, pos=({x:F2},{y:F2},{z:F2}), msg={message}");
            });

            _packetRouter.Register(NetOpcodes.SkillCastResponse, payload =>
            {
                if (!ServerMessageParser.TryParseSkillCastResponse(payload, out bool success, out int targetId, out int damage, out string message))
                {
                    LogWarning("Invalid SkillCastResponse payload.");
                    return;
                }

                Log($"Skill response: success={success}, target={targetId}, damage={damage}, msg={message}");
            });

            _packetRouter.Register(NetOpcodes.ErrorResponse, payload =>
            {
                try
                {
                    using var ms = new System.IO.MemoryStream(payload);
                    using var reader = new System.IO.BinaryReader(ms);
                    string message = PacketCodec.ReadString(reader);
                    LogWarning($"Server error: {message}");
                }
                catch
                {
                    LogWarning("Malformed error payload.");
                }
            });
        }

        private void Log(string message)
        {
            string line = $"[NetworkGameClient] {message}";
            Debug.Log(line);
            OnClientLog?.Invoke(line);
        }

        private void LogWarning(string message)
        {
            string line = $"[NetworkGameClient] {message}";
            Debug.LogWarning(line);
            OnClientLog?.Invoke(line);
        }

        private void OnDestroy()
        {
            if (_networkClient != null && _networkClient.IsConnected)
            {
                _networkClient.Disconnect();
            }

            if (_serverApp != null)
            {
                _serverApp.Stop();
            }

            _connected = false;
            _authenticated = false;
            _accessToken = null;
        }
    }
}
