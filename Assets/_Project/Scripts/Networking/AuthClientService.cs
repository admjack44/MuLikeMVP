using System;
using System.Threading.Tasks;

namespace MuLike.Networking
{
    /// <summary>
    /// Handles authentication commands and state.
    /// </summary>
    public sealed class AuthClientService
    {
        private readonly IGameConnection _connection;

        private bool _isAwaitingLoginResponse;
        private string _username;
        private string _password;

        public AuthClientService(IGameConnection connection, NetworkEventStream eventStream)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));

            if (eventStream == null) throw new ArgumentNullException(nameof(eventStream));
            eventStream.LoginResponseReceived += HandleLoginResponse;
            eventStream.ErrorReceived += HandleError;
        }

        public bool IsAuthenticated { get; private set; }
        public string AccessToken { get; private set; }

        public event Action<bool, string> LoginResultReceived;

        public void ConfigureCredentials(string username, string password)
        {
            _username = username ?? string.Empty;
            _password = password ?? string.Empty;
        }

        public async Task LoginAsync()
        {
            if (!_connection.IsConnected)
            {
                LoginResultReceived?.Invoke(false, "Login rejected: client is not connected.");
                return;
            }

            byte[] packet = ClientMessageFactory.CreateLoginRequest(_username, _password);
            _isAwaitingLoginResponse = true;
            await _connection.SendAsync(packet);
        }

        public void Reset()
        {
            IsAuthenticated = false;
            AccessToken = null;
            _isAwaitingLoginResponse = false;
        }

        private void HandleLoginResponse(bool success, string token, string message)
        {
            IsAuthenticated = success;
            AccessToken = success ? token : null;
            _isAwaitingLoginResponse = false;
            LoginResultReceived?.Invoke(success, message);
        }

        private void HandleError(string message)
        {
            if (!_isAwaitingLoginResponse) return;

            _isAwaitingLoginResponse = false;
            LoginResultReceived?.Invoke(false, message);
        }
    }
}
