using System;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Networking;

namespace MuLike.UI.Login
{
    public interface ILoginFlowService
    {
        LoginFlowState State { get; }
        string LastKnownUsername { get; }

        event Action<LoginFlowState> StateChanged;
        event Action<string> StatusMessageChanged;
        event Action Authenticated;
        event Action LoggedOut;

        Task InitializeAsync(CancellationToken ct);
        Task<LoginAttemptResult> SubmitLoginAsync(string username, string password, CancellationToken ct);
        Task<LoginAttemptResult> TrySilentAutoLoginAsync(CancellationToken ct);
        Task<LoginAttemptResult> TryAutoRefreshAsync(CancellationToken ct);
        void Logout();
    }

    /// <summary>
    /// Orchestrates login/refresh/logout with timeout, race protection and local session persistence.
    /// </summary>
    public sealed class LoginFlowService : ILoginFlowService
    {
        private readonly ILoginGateway _gateway;
        private readonly ILoginSessionStore _sessionStore;
        private readonly LoginFlowStateMachine _stateMachine;
        private readonly SemaphoreSlim _authGate = new(1, 1);

        private readonly int _requestTimeoutMs;
        private readonly int _connectTimeoutMs;
        private readonly int _refreshLeadSeconds;

        private DateTime _nextAutoRefreshCheckUtc = DateTime.MinValue;
        private string _lastKnownUsername = string.Empty;

        public LoginFlowState State => _stateMachine.Current;
        public string LastKnownUsername => _lastKnownUsername;

        public event Action<LoginFlowState> StateChanged;
        public event Action<string> StatusMessageChanged;
        public event Action Authenticated;
        public event Action LoggedOut;

        public LoginFlowService(
            ILoginGateway gateway,
            ILoginSessionStore sessionStore,
            int requestTimeoutMs,
            int connectTimeoutMs,
            int refreshLeadSeconds)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _requestTimeoutMs = Math.Max(1000, requestTimeoutMs);
            _connectTimeoutMs = Math.Max(1000, connectTimeoutMs);
            _refreshLeadSeconds = Math.Max(15, refreshLeadSeconds);
            _stateMachine = new LoginFlowStateMachine();
            _stateMachine.StateChanged += state => StateChanged?.Invoke(state);
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            EmitStatus("[LoginFlow] Initializing login flow...");
            _stateMachine.TryMoveTo(LoginFlowState.Idle);

            if (_sessionStore.TryLoad(out LoginSessionData cached) && cached != null)
            {
                _lastKnownUsername = cached.LastUsername ?? string.Empty;
                _gateway.RestoreSession(new AuthSessionSnapshot
                {
                    AccessToken = cached.AccessToken,
                    AccessTokenExpiresAtUtcTicks = cached.AccessTokenExpiresAtUtcTicks,
                    RefreshToken = cached.RefreshToken,
                    RefreshTokenExpiresAtUtcTicks = cached.RefreshTokenExpiresAtUtcTicks
                });

                EmitStatus("[LoginFlow] Cached session loaded.");
            }

            await Task.CompletedTask;
            ct.ThrowIfCancellationRequested();
        }

        public async Task<LoginAttemptResult> SubmitLoginAsync(string username, string password, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Fail("Ingresa usuario y contrasena para continuar.");

            await _authGate.WaitAsync(ct);
            try
            {
                _lastKnownUsername = username.Trim();
                _gateway.ConfigureCredentials(_lastKnownUsername, password);

                bool connected = await EnsureConnectionAsync(ct);
                if (!connected)
                    return Fail("No se pudo conectar. Revisa tu red e intenta de nuevo.");

                _stateMachine.TryMoveTo(LoginFlowState.Authenticating);
                EmitStatus("[LoginFlow] Authenticating...");

                LoginAttemptResult result = await WaitLoginResultAsync(ct);
                if (!result.Success)
                    return result;

                HandleAuthenticatedPersist();
                return result;
            }
            finally
            {
                if (_stateMachine.Current == LoginFlowState.Authenticating)
                    _stateMachine.TryMoveTo(LoginFlowState.Idle);

                _authGate.Release();
            }
        }

        public async Task<LoginAttemptResult> TrySilentAutoLoginAsync(CancellationToken ct)
        {
            await _authGate.WaitAsync(ct);
            try
            {
                if (!_gateway.CanRefresh)
                {
                    _stateMachine.TryMoveTo(LoginFlowState.Idle);
                    return Fail("No hay sesion para auto-login.");
                }

                bool connected = await EnsureConnectionAsync(ct);
                if (!connected)
                    return Fail("No se pudo reconectar para recuperar sesion.");

                _stateMachine.TryMoveTo(LoginFlowState.Refreshing);
                EmitStatus("[LoginFlow] Restoring session...");

                LoginAttemptResult refreshResult = await WaitRefreshResultAsync(ct);
                if (!refreshResult.Success)
                {
                    _stateMachine.TryMoveTo(LoginFlowState.Failed);
                    EmitStatus("[LoginFlow] Auto-login failed.");
                    _gateway.Logout(disconnect: false);
                    _sessionStore.Clear();
                    return refreshResult;
                }

                HandleAuthenticatedPersist();
                return refreshResult;
            }
            finally
            {
                _authGate.Release();
            }
        }

        public async Task<LoginAttemptResult> TryAutoRefreshAsync(CancellationToken ct)
        {
            if (!_gateway.IsAuthenticated || !_gateway.CanRefresh)
                return Fail("Refresh skipped: no active session.");

            if (_nextAutoRefreshCheckUtc > DateTime.UtcNow)
                return new LoginAttemptResult(true, "Skipped");

            TimeSpan remaining = _gateway.AccessTokenExpiresAtUtc - DateTime.UtcNow;
            if (remaining > TimeSpan.FromSeconds(_refreshLeadSeconds))
            {
                _nextAutoRefreshCheckUtc = DateTime.UtcNow.AddSeconds(2);
                return new LoginAttemptResult(true, "Not due");
            }

            await _authGate.WaitAsync(ct);
            try
            {
                _stateMachine.TryMoveTo(LoginFlowState.Refreshing);
                EmitStatus("[LoginFlow] Refreshing token...");

                LoginAttemptResult result = await WaitRefreshResultAsync(ct);
                if (!result.Success)
                {
                    _stateMachine.TryMoveTo(LoginFlowState.Failed);
                    EmitStatus("[LoginFlow] Session refresh failed. Please login again.");
                    _gateway.Logout(disconnect: false);
                    _sessionStore.Clear();
                    return result;
                }

                HandleAuthenticatedPersist();
                return result;
            }
            finally
            {
                _authGate.Release();
            }
        }

        public void Logout()
        {
            _gateway.Logout(disconnect: false);
            _sessionStore.Clear();
            _stateMachine.TryMoveTo(LoginFlowState.LoggedOut);
            EmitStatus("[LoginFlow] Logged out.");
            LoggedOut?.Invoke();
            _stateMachine.TryMoveTo(LoginFlowState.Idle);
        }

        private async Task<bool> EnsureConnectionAsync(CancellationToken ct)
        {
            _stateMachine.TryMoveTo(LoginFlowState.Connecting);
            EmitStatus("[LoginFlow] Connecting...");

            bool connected = await _gateway.EnsureConnectedAsync(_connectTimeoutMs, ct);
            if (!connected)
            {
                _stateMachine.TryMoveTo(LoginFlowState.Failed);
                EmitStatus("[LoginFlow] Connection timeout.");
            }

            return connected;
        }

        private async Task<LoginAttemptResult> WaitLoginResultAsync(CancellationToken ct)
        {
            Task<LoginAttemptResult> waitTask = WaitEventResultAsync(
                subscribe: handler => _gateway.LoginResultReceived += handler,
                unsubscribe: handler => _gateway.LoginResultReceived -= handler,
                trigger: () => _gateway.SendLoginAsync(),
                timeoutMs: _requestTimeoutMs,
                ct: ct,
                timeoutMessage: "Tiempo de espera agotado al autenticar.");

            LoginAttemptResult result = await waitTask;
            if (!result.Success)
            {
                _stateMachine.TryMoveTo(LoginFlowState.Failed);
                EmitStatus("[LoginFlow] Login failed.");
                return Fail(MapFriendlyMessage(result.Message));
            }

            return new LoginAttemptResult(true, "Autenticacion correcta.");
        }

        private async Task<LoginAttemptResult> WaitRefreshResultAsync(CancellationToken ct)
        {
            Task<LoginAttemptResult> waitTask = WaitEventResultAsync(
                subscribe: handler => _gateway.RefreshResultReceived += handler,
                unsubscribe: handler => _gateway.RefreshResultReceived -= handler,
                trigger: () => _gateway.SendRefreshAsync(),
                timeoutMs: _requestTimeoutMs,
                ct: ct,
                timeoutMessage: "Tiempo de espera agotado al refrescar sesion.");

            LoginAttemptResult result = await waitTask;
            if (!result.Success)
                return Fail(MapFriendlyMessage(result.Message));

            return new LoginAttemptResult(true, "Sesion restaurada.");
        }

        private async Task<LoginAttemptResult> WaitEventResultAsync(
            Action<Action<bool, string>> subscribe,
            Action<Action<bool, string>> unsubscribe,
            Func<Task> trigger,
            int timeoutMs,
            CancellationToken ct,
            string timeoutMessage)
        {
            var tcs = new TaskCompletionSource<LoginAttemptResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(bool success, string message)
            {
                tcs.TrySetResult(new LoginAttemptResult(success, message));
            }

            subscribe(Handler);
            try
            {
                await trigger();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeoutMs);

                Task finished = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (finished == tcs.Task)
                    return await tcs.Task;

                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);

                return Fail(timeoutMessage);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail($"Error de red: {ex.Message}");
            }
            finally
            {
                unsubscribe(Handler);
            }
        }

        private void HandleAuthenticatedPersist()
        {
            _stateMachine.TryMoveTo(LoginFlowState.Authenticated);
            _nextAutoRefreshCheckUtc = DateTime.UtcNow.AddSeconds(2);

            AuthSessionSnapshot snapshot = _gateway.CaptureSession();
            var data = new LoginSessionData
            {
                LastUsername = _lastKnownUsername,
                AccessToken = snapshot.AccessToken,
                AccessTokenExpiresAtUtcTicks = snapshot.AccessTokenExpiresAtUtcTicks,
                RefreshToken = snapshot.RefreshToken,
                RefreshTokenExpiresAtUtcTicks = snapshot.RefreshTokenExpiresAtUtcTicks
            };

            _sessionStore.Save(data);
            EmitStatus("[LoginFlow] Authenticated.");
            Authenticated?.Invoke();
        }

        private LoginAttemptResult Fail(string message)
        {
            return new LoginAttemptResult(false, MapFriendlyMessage(message));
        }

        private static string MapFriendlyMessage(string message)
        {
            string raw = message ?? string.Empty;
            if (raw.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "La conexion esta lenta. Intenta nuevamente.";

            if (raw.IndexOf("not connected", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Sin conexion al servidor. Verifica internet.";

            if (raw.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Tu sesion expiro. Inicia sesion de nuevo.";

            if (string.IsNullOrWhiteSpace(raw))
                return "No fue posible autenticar. Intenta de nuevo.";

            return raw;
        }

        private void EmitStatus(string message)
        {
            StatusMessageChanged?.Invoke(message ?? string.Empty);
        }
    }
}
