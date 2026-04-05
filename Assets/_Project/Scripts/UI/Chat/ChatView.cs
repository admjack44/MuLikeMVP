using System;
using System.Collections.Generic;
using MuLike.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Chat
{
    /// <summary>
    /// View-only chat UI. No networking logic lives here.
    /// </summary>
    public class ChatView : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private GameObject _modalRoot;

        [Header("Inputs")]
        [SerializeField] private TMP_Dropdown _channelDropdown;
        [SerializeField] private TMP_InputField _targetInput;
        [SerializeField] private TMP_InputField _messageInput;

        [Header("Actions")]
        [SerializeField] private Button _sendButton;
        [SerializeField] private Button _clearButton;

        [Header("Output")]
        [SerializeField] private TMP_Text _messagesText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private ScrollRect _messagesScrollRect;
        [SerializeField, Range(0.02f, 0.5f)] private float _autoScrollRefreshSeconds = 0.1f;

        [Header("Filters")]
        [SerializeField] private Button _worldFilterButton;
        [SerializeField] private Button _tradeFilterButton;
        [SerializeField] private Button _partyFilterButton;
        [SerializeField] private Button _guildFilterButton;
        [SerializeField] private Button _systemFilterButton;

        public event Action<ChatSendRequest> SendRequested;
        public event Action ClearRequested;
        public event Action<ChatFilterMode> FilterChanged;

        public bool IsVisible { get; private set; }
        public ChatFilterMode CurrentFilter { get; private set; } = ChatFilterMode.World;

        private static readonly ChatChannel[] SendChannels =
        {
            ChatChannel.World,
            ChatChannel.Trade,
            ChatChannel.Party,
            ChatChannel.Guild,
            ChatChannel.Private
        };

        private float _nextAutoScrollAt;

        private void Awake()
        {
            if (_sendButton != null)
                _sendButton.onClick.AddListener(HandleSendClicked);

            if (_clearButton != null)
                _clearButton.onClick.AddListener(() => ClearRequested?.Invoke());

            if (_messageInput != null)
                _messageInput.onSubmit.AddListener(HandleSubmit);

            if (_worldFilterButton != null)
                _worldFilterButton.onClick.AddListener(() => SetFilter(ChatFilterMode.World));

            if (_tradeFilterButton != null)
                _tradeFilterButton.onClick.AddListener(() => SetFilter(ChatFilterMode.Trade));

            if (_partyFilterButton != null)
                _partyFilterButton.onClick.AddListener(() => SetFilter(ChatFilterMode.Party));

            if (_guildFilterButton != null)
                _guildFilterButton.onClick.AddListener(() => SetFilter(ChatFilterMode.Guild));

            if (_systemFilterButton != null)
                _systemFilterButton.onClick.AddListener(() => SetFilter(ChatFilterMode.System));

            ConfigureChannelDropdown();

            SetStatus("Chat ready.");
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_sendButton != null)
                _sendButton.onClick.RemoveListener(HandleSendClicked);

            if (_clearButton != null)
                _clearButton.onClick.RemoveAllListeners();

            if (_messageInput != null)
                _messageInput.onSubmit.RemoveListener(HandleSubmit);

            if (_worldFilterButton != null)
                _worldFilterButton.onClick.RemoveAllListeners();

            if (_tradeFilterButton != null)
                _tradeFilterButton.onClick.RemoveAllListeners();

            if (_partyFilterButton != null)
                _partyFilterButton.onClick.RemoveAllListeners();

            if (_guildFilterButton != null)
                _guildFilterButton.onClick.RemoveAllListeners();

            if (_systemFilterButton != null)
                _systemFilterButton.onClick.RemoveAllListeners();
        }

        public void Render(IReadOnlyList<ChatMessage> messages)
        {
            if (_messagesText == null) return;

            if (messages == null || messages.Count == 0)
            {
                _messagesText.text = string.Empty;
                return;
            }

            var lines = new System.Text.StringBuilder(1024);
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) lines.Append('\n');
                lines.Append(messages[i].ToDisplayString());
            }

            _messagesText.text = lines.ToString();

            if (_messagesScrollRect != null)
            {
                if (Time.unscaledTime < _nextAutoScrollAt)
                    return;

                _nextAutoScrollAt = Time.unscaledTime + Mathf.Max(0.02f, _autoScrollRefreshSeconds);
                Canvas.ForceUpdateCanvases();
                _messagesScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        public void SetStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message ?? string.Empty;
        }

        public void SetInteractable(bool interactable)
        {
            if (_channelDropdown != null) _channelDropdown.interactable = interactable;
            if (_targetInput != null) _targetInput.interactable = interactable;
            if (_messageInput != null) _messageInput.interactable = interactable;
            if (_sendButton != null) _sendButton.interactable = interactable;
            if (_clearButton != null) _clearButton.interactable = interactable;
        }

        public void ClearInput()
        {
            if (_messageInput != null)
                _messageInput.text = string.Empty;
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (_modalRoot != null)
                _modalRoot.SetActive(visible);
        }

        public void SetFilter(ChatFilterMode filter)
        {
            CurrentFilter = filter;
            FilterChanged?.Invoke(filter);
        }

        private void HandleSendClicked()
        {
            ChatChannel channel = ChatChannel.World;
            if (_channelDropdown != null)
            {
                int index = Mathf.Clamp(_channelDropdown.value, 0, SendChannels.Length - 1);
                channel = SendChannels[index];
            }

            string target = _targetInput != null ? _targetInput.text : string.Empty;
            string text = _messageInput != null ? _messageInput.text : string.Empty;

            SendRequested?.Invoke(new ChatSendRequest
            {
                Channel = channel,
                Target = target,
                Text = text
            });
        }

        private void HandleSubmit(string _)
        {
            HandleSendClicked();
        }

        private void ConfigureChannelDropdown()
        {
            if (_channelDropdown == null)
                return;

            _channelDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>(SendChannels.Length);
            for (int i = 0; i < SendChannels.Length; i++)
                options.Add(new TMP_Dropdown.OptionData(SendChannels[i].ToString()));

            _channelDropdown.AddOptions(options);
            _channelDropdown.SetValueWithoutNotify(0);
        }
    }
}
