using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.Chat
{
    /// <summary>
    /// Scene composition root for chat MVP.
    /// </summary>
    public class ChatFlowController : MonoBehaviour
    {
        [SerializeField] private ChatView _view;
        [SerializeField] private int _bufferLimit = 200;
        [SerializeField] private string _localPlayerName = "Player";

        [Header("Transport")]
        [SerializeField] private bool _useMockTransport = true;

        private ChatClientSystem _chatSystem;
        private ChatPresenter _presenter;
        private IChatTransport _transport;

        private void Awake()
        {
            if (_view == null)
                _view = FindObjectOfType<ChatView>();

            if (_view == null)
            {
                Debug.LogError("[ChatFlowController] ChatView is missing in scene.");
                enabled = false;
                return;
            }

            _chatSystem = new ChatClientSystem(_bufferLimit)
            {
                LocalPlayerName = _localPlayerName
            };

            _transport = BuildTransport();
            _chatSystem.AttachTransport(_transport);
            _chatSystem.ReceiveSystemMessage("Welcome to MU MVP chat.");

            _presenter = new ChatPresenter(_view, _chatSystem);
        }

        private void OnEnable()
        {
            _presenter?.Bind();
        }

        private void OnDisable()
        {
            _presenter?.Unbind();
        }

        private IChatTransport BuildTransport()
        {
            if (_useMockTransport)
                return new MockChatTransport();

            Debug.LogWarning("[ChatFlowController] Real network chat transport not configured, fallback to mock.");
            return new MockChatTransport();
        }
    }
}
