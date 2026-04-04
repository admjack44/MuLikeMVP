using System;
using System.Threading.Tasks;
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

        private IGameConnection _connection;
        private PacketRouter _packetRouter;
        private NetworkEventStream _eventStream;
        private AuthClientService _authService;
        private MovementClientService _movementService;
        private SkillClientService _skillService;

        private bool _connected;

        public bool IsConnected => _connected;
        public bool IsAuthenticated => _authService != null && _authService.IsAuthenticated;
        public event Action<string> OnClientLog;
        public event Action<bool, string> OnLoginResult;
        public event Action<bool, Vector3, string> OnMoveResult;
        public event Action<bool, int, int, string> OnSkillResult;

        private async void Start()
        {
            BuildNetworkStack();
            WireNetworkStack();

            Log($"Connecting using {(_useInMemoryGateway ? "InMemory" : "TCP")} transport...");
            await _connection.ConnectAsync();
        }

        private void BuildNetworkStack()
        {
            _connection = _useInMemoryGateway
                ? new InMemoryGameConnection()
                : new TcpGameConnection(_host, _port);

            _packetRouter = new PacketRouter();
            _eventStream = new NetworkEventStream(_packetRouter);
            _authService = new AuthClientService(_connection, _eventStream);
            _movementService = new MovementClientService(_connection, _eventStream);
            _skillService = new SkillClientService(_connection, _eventStream);
            _authService.ConfigureCredentials(_username, _password);
        }

        private void Update()
        {
            if (!_connected) return;

            if (WasKeyPressed(_loginKey))
            {
                _ = SendLoginAsync();
            }

            if (IsAuthenticated && WasKeyPressed(_moveKey))
            {
                Vector3 target = transform.position + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                _ = SendMoveAsync(target.x, target.y, target.z);
            }

            if (IsAuthenticated && WasKeyPressed(_skillKey))
            {
                // Demo target entity id. In a real flow this comes from TargetingController.
                _ = SendSkillCastAsync(skillId: 1, targetId: 999);
            }
        }

        private void WireNetworkStack()
        {
            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.PacketReceived += HandleIncomingPacket;

            _eventStream.ErrorReceived += HandleServerError;
            _eventStream.LoginResponseReceived += HandleLoginResponse;
            _movementService.MoveResultReceived += HandleMoveResult;
            _skillService.SkillResultReceived += HandleSkillResult;
            _authService.LoginResultReceived += HandleLoginResult;
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
            await _authService.LoginAsync();
        }

        public void ConfigureCredentials(string username, string password)
        {
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
            _authService?.ConfigureCredentials(_username, _password);
            Log($"Credentials updated for user '{_username}'.");
        }

        public async Task SendMoveAsync(float x, float y, float z)
        {
            await _movementService.MoveAsync(x, y, z);
        }

        public async Task SendSkillCastAsync(int skillId, int targetId)
        {
            await _skillService.CastAsync(skillId, targetId);
        }

        private void HandleIncomingPacket(byte[] packet)
        {
            _eventStream.ProcessPacket(packet);
        }

        private void HandleConnected()
        {
            _connected = true;
            Log($"Connected via {(_useInMemoryGateway ? "InMemoryGatewayBridge" : "TCP") }.");
        }

        private void HandleDisconnected()
        {
            _connected = false;
            _authService?.Reset();
            Log("Disconnected.");
        }

        private void HandleServerError(string message)
        {
            LogWarning($"Server error: {message}");
        }

        private void HandleLoginResponse(bool success, string token, string message)
        {
            Log($"Login response: success={success}, msg={message}");
        }

        private void HandleMoveResult(bool success, Vector3 position, string message)
        {
            if (success)
            {
                transform.position = position;
            }

            Log($"Move response: success={success}, pos=({position.x:F2},{position.y:F2},{position.z:F2}), msg={message}");
            OnMoveResult?.Invoke(success, position, message);
        }

        private void HandleSkillResult(bool success, int targetId, int damage, string message)
        {
            Log($"Skill response: success={success}, target={targetId}, damage={damage}, msg={message}");
            OnSkillResult?.Invoke(success, targetId, damage, message);
        }

        private void HandleLoginResult(bool success, string message)
        {
            if (!success)
            {
                LogWarning($"Login failed: {message}");
            }

            OnLoginResult?.Invoke(success, message);
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
            _connection?.Disconnect();
            _connected = false;
        }
    }
}
