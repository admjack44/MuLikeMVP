using System;
using System.Threading;
using System.Threading.Tasks;
using MuLike.Bootstrap;
using MuLike.Networking;
using UnityEngine;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Handles login flow state and orchestrates view + network interactions.
    /// Coordinates with SessionStateClient for session state transitions.
    /// </summary>
    public sealed class LoginPresenter
    {
        private readonly LoginView _view;
        private readonly ILoginFlowService _flowService;
        private readonly Action _onLoginSuccess;
        private readonly Action _onLogout;
        private readonly SessionStateClient _sessionState;
        private readonly ClientFlowFeedbackService _feedback;

        private readonly CancellationTokenSource _lifetimeCts = new();
        private bool _isProcessing;

        public LoginPresenter(
            LoginView view,
            ILoginFlowService flowService,
            Action onLoginSuccess,
            Action onLogout,
            SessionStateClient sessionState,
            ClientFlowFeedbackService feedback)
        {
            _view = view;
            _flowService = flowService;
            _onLoginSuccess = onLoginSuccess;
            _onLogout = onLogout;
            _sessionState = sessionState;
            _feedback = feedback;
        }

        public LoginPresenter(LoginView view, ILoginFlowService flowService, Action onLoginSuccess, Action onLogout)
            : this(view, flowService, onLoginSuccess, onLogout, new SessionStateClient(), new ClientFlowFeedbackService())
        {
        }

        public LoginPresenter(LoginView view, NetworkGameClient client, Action onLoginSuccess)
            : this(
                view,
                new LoginFlowService(
                    new NetworkGameClientLoginGateway(client),
                    new PlayerPrefsLoginSessionStore(),
                    requestTimeoutMs: 12000,
                    connectTimeoutMs: 10000,
                    refreshLeadSeconds: 90),
                onLoginSuccess,
                onLogout: null)
        {
        }

        public void Bind()
        {
            _view.EnterRequested += HandleEnterRequested;
            _view.LogoutRequested += HandleLogoutRequested;

            _flowService.StateChanged += HandleStateChanged;
            _flowService.StatusMessageChanged += HandleStatusMessageChanged;
            _flowService.Authenticated += HandleAuthenticated;
            _flowService.LoggedOut += HandleLoggedOut;

            _view.SetInteractable(true);
            _view.SetStatus("[LoginFlow] Ready. Enter credentials.");
            _view.SetUsername(_flowService.LastKnownUsername);

            _sessionState?.TryTransitionTo(ClientSessionState.Disconnected);

            _ = InitializeAndTrySilentLoginAsync();
        }

        public void Unbind()
        {
            _view.EnterRequested -= HandleEnterRequested;
            _view.LogoutRequested -= HandleLogoutRequested;

            _flowService.StateChanged -= HandleStateChanged;
            _flowService.StatusMessageChanged -= HandleStatusMessageChanged;
            _flowService.Authenticated -= HandleAuthenticated;
            _flowService.LoggedOut -= HandleLoggedOut;

            _lifetimeCts.Cancel();
        }

        private async void HandleEnterRequested()
        {
            await TryLoginAsync();
        }

        private void HandleLogoutRequested()
        {
            _flowService.Logout();
        }

        private async Task InitializeAndTrySilentLoginAsync()
        {
            try
            {
                // Attempt silent auto-login from cached session
                await _flowService.InitializeAsync(_lifetimeCts.Token);
                _view.SetUsername(_flowService.LastKnownUsername);

                if (_sessionState != null && _flowService.LastKnownUsername != null)
                    _sessionState.TryTransitionTo(ClientSessionState.Connecting);

                LoginAttemptResult autoLogin = await _flowService.TrySilentAutoLoginAsync(_lifetimeCts.Token);
                if (autoLogin.Success)
                {
                    _view.SetStatus("[LoginFlow] Session restored.");
                    _feedback?.Clear();
                    _onLoginSuccess?.Invoke();
                }
                else
                {
                    _view.SetStatus("[LoginFlow] Ready. Enter credentials.");
                    _sessionState?.TryTransitionTo(ClientSessionState.Disconnected);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LoginFlow] Initialization warning: {ex.Message}");
                _view.SetStatus("[LoginFlow] Ready. Enter credentials.");
                _sessionState?.TryTransitionTo(ClientSessionState.Disconnected);
            }
        }

        private async Task TryLoginAsync()
        {
            if (_isProcessing) return;

            string username = _view.Username?.Trim() ?? string.Empty;
            string password = _view.Password ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                string errorMsg = "[LoginFlow] Ingresa usuario y contrasena.";
                _view.SetStatus(errorMsg);
                _feedback?.ShowError("Username and password required.");
                return;
            }

            _isProcessing = true;
            _view.SetInteractable(false);
            _sessionState?.TryTransitionTo(ClientSessionState.Connecting);
            _feedback?.ShowLoading("Connecting to server...");
            _view.SetStatus("[LoginFlow] Connecting...");

            Debug.Log($"[LoginFlow] Login requested for user '{username}'.");

            try
            {
                LoginAttemptResult result = await _flowService.SubmitLoginAsync(username, password, _lifetimeCts.Token);
                if (result.Success)
                {
                    _sessionState?.TryTransitionTo(ClientSessionState.Authenticated);
                    _view.SetStatus("[LoginFlow] Login successful. Entering character select...");
                    _feedback?.Clear();
                    Debug.Log("[LoginFlow] Login succeeded.");
                    _onLoginSuccess?.Invoke();
                    return;
                }

                string errorMsg = $"Login failed: {result.Message}";
                _view.SetStatus($"[LoginFlow] {errorMsg}");
                _feedback?.ShowError(errorMsg);
                _sessionState?.TryTransitionTo(ClientSessionState.Failed);
                Debug.LogWarning($"[LoginFlow] {errorMsg}");
            }
            catch (OperationCanceledException)
            {
                _view.SetStatus("[LoginFlow] Login cancelled.");
                _sessionState?.TryTransitionTo(ClientSessionState.Disconnected);
                _feedback?.ShowError("Login cancelled.");
            }
            finally
            {
                _isProcessing = false;
                _view.SetInteractable(true);
            }
        }

        private void HandleStateChanged(LoginFlowState state)
        {
            if (state == LoginFlowState.Authenticating)
                _sessionState?.TryTransitionTo(ClientSessionState.Authenticating);

            if (state == LoginFlowState.Failed)
            {
                _view.SetInteractable(true);
                _sessionState?.TryTransitionTo(ClientSessionState.Failed);
            }
        }

        private void HandleStatusMessageChanged(string message)
        {
            _view.SetStatus(message);
        }

        private void HandleAuthenticated()
        {
            Debug.Log("[LoginFlow] Authenticated event emitted.");
            _sessionState?.TryTransitionTo(ClientSessionState.Authenticated);
        }

        private void HandleLoggedOut()
        {
            _view.SetStatus("[LoginFlow] Session closed.");
            _view.SetInteractable(true);
            _sessionState?.ClearForLogout();
            _feedback?.Clear();
            _onLogout?.Invoke();
        }
    }
}
