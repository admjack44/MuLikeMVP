using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Pure UI view for login. It exposes user intent and receives state updates from the presenter.
    /// </summary>
    public class LoginView : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField _usernameInput;
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("Actions")]
        [SerializeField] private Button _enterButton;
        [SerializeField] private Button _logoutButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _statusText;

        public event Action EnterRequested;
        public event Action LogoutRequested;

        public string Username => _usernameInput != null ? _usernameInput.text : string.Empty;
        public string Password => _passwordInput != null ? _passwordInput.text : string.Empty;

        private void Awake()
        {
            if (_enterButton != null)
                _enterButton.onClick.AddListener(HandleEnterClicked);

            if (_logoutButton != null)
                _logoutButton.onClick.AddListener(HandleLogoutClicked);

            if (_passwordInput != null)
                _passwordInput.contentType = TMP_InputField.ContentType.Password;
        }

        private void OnDestroy()
        {
            if (_enterButton != null)
                _enterButton.onClick.RemoveListener(HandleEnterClicked);

            if (_logoutButton != null)
                _logoutButton.onClick.RemoveListener(HandleLogoutClicked);
        }

        public void SetInteractable(bool interactable)
        {
            if (_usernameInput != null) _usernameInput.interactable = interactable;
            if (_passwordInput != null) _passwordInput.interactable = interactable;
            if (_enterButton != null) _enterButton.interactable = interactable;
            if (_logoutButton != null) _logoutButton.interactable = true;
        }

        public void SetUsername(string username)
        {
            if (_usernameInput == null)
                return;

            _usernameInput.text = username ?? string.Empty;
        }

        public void SetStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message ?? string.Empty;
        }

        private void HandleEnterClicked()
        {
            EnterRequested?.Invoke();
        }

        private void HandleLogoutClicked()
        {
            LogoutRequested?.Invoke();
        }
    }
}
