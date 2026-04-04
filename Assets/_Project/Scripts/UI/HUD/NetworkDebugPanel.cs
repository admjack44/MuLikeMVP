using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.HUD
{
    /// <summary>
    /// Simple runtime panel that drives NetworkGameClient actions for quick integration tests.
    /// </summary>
    public class NetworkDebugPanel : MonoBehaviour
    {
        [SerializeField] private MuLike.Networking.NetworkGameClient _client;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _moveButton;
        [SerializeField] private Button _skillButton;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _logText;

        private void Awake()
        {
            if (_client == null)
            {
                _client = FindObjectOfType<MuLike.Networking.NetworkGameClient>();
            }

            if (_loginButton != null) _loginButton.onClick.AddListener(OnLoginClicked);
            if (_moveButton != null) _moveButton.onClick.AddListener(OnMoveClicked);
            if (_skillButton != null) _skillButton.onClick.AddListener(OnSkillClicked);
        }

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.OnClientLog += HandleClientLog;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.OnClientLog -= HandleClientLog;
            }
        }

        private void Update()
        {
            if (_statusText == null || _client == null) return;

            _statusText.text = $"Connected: {_client.IsConnected} | Auth: {_client.IsAuthenticated}";

            if (_moveButton != null) _moveButton.interactable = _client.IsAuthenticated;
            if (_skillButton != null) _skillButton.interactable = _client.IsAuthenticated;
        }

        public async void OnLoginClicked()
        {
            if (_client == null) return;
            await _client.SendLoginAsync();
        }

        public async void OnMoveClicked()
        {
            if (_client == null) return;

            Vector3 current = _client.transform.position;
            Vector3 target = current + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            await _client.SendMoveAsync(target.x, target.y, target.z);
        }

        public async void OnSkillClicked()
        {
            if (_client == null) return;
            await _client.SendSkillCastAsync(1, 999);
        }

        private void HandleClientLog(string logLine)
        {
            if (_logText == null || string.IsNullOrWhiteSpace(logLine)) return;

            string existing = _logText.text;
            string combined = string.IsNullOrEmpty(existing) ? logLine : $"{existing}\n{logLine}";

            const int maxChars = 900;
            _logText.text = combined.Length > maxChars
                ? combined.Substring(combined.Length - maxChars)
                : combined;
        }
    }
}
