using System;
using System.Threading.Tasks;
using MuLike.Networking;
using UnityEngine;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Handles login flow state and orchestrates view + network interactions.
    /// </summary>
    public sealed class LoginPresenter
    {
        private readonly LoginView _view;
        private readonly NetworkGameClient _client;
        private readonly Action _onLoginSuccess;

        private bool _isProcessing;

        public LoginPresenter(LoginView view, NetworkGameClient client, Action onLoginSuccess)
        {
            _view = view;
            _client = client;
            _onLoginSuccess = onLoginSuccess;
        }

        public void Bind()
        {
            _view.EnterRequested += HandleEnterRequested;
            _client.OnLoginResult += HandleLoginResult;

            _view.SetInteractable(true);
            _view.SetStatus("Ready. Enter credentials.");
        }

        public void Unbind()
        {
            _view.EnterRequested -= HandleEnterRequested;
            _client.OnLoginResult -= HandleLoginResult;
        }

        private async void HandleEnterRequested()
        {
            await TryLoginAsync();
        }

        private async Task TryLoginAsync()
        {
            if (_isProcessing) return;

            string username = _view.Username?.Trim() ?? string.Empty;
            string password = _view.Password ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _view.SetStatus("Username and password are required.");
                return;
            }

            _isProcessing = true;
            _view.SetInteractable(false);
            _view.SetStatus("Connecting/login in progress...");

            Debug.Log($"[LoginPresenter] Login requested for user '{username}'.");

            _client.ConfigureCredentials(username, password);
            await _client.SendLoginAsync();
        }

        private void HandleLoginResult(bool success, string message)
        {
            _isProcessing = false;
            _view.SetInteractable(true);

            if (success)
            {
                _view.SetStatus("Login successful. Entering world...");
                Debug.Log("[LoginPresenter] Login succeeded.");
                _onLoginSuccess?.Invoke();
                return;
            }

            string statusMessage = string.IsNullOrWhiteSpace(message)
                ? "Login failed. Try again."
                : $"Login failed: {message}";

            _view.SetStatus(statusMessage);
            Debug.LogWarning($"[LoginPresenter] {statusMessage}");
        }
    }
}
