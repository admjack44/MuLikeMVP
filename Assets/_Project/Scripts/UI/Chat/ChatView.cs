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

        public event Action<ChatSendRequest> SendRequested;
        public event Action ClearRequested;

        private void Awake()
        {
            if (_sendButton != null)
                _sendButton.onClick.AddListener(HandleSendClicked);

            if (_clearButton != null)
                _clearButton.onClick.AddListener(() => ClearRequested?.Invoke());

            SetStatus("Chat ready.");
        }

        private void OnDestroy()
        {
            if (_sendButton != null)
                _sendButton.onClick.RemoveListener(HandleSendClicked);

            if (_clearButton != null)
                _clearButton.onClick.RemoveAllListeners();
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

        private void HandleSendClicked()
        {
            ChatChannel channel = ChatChannel.General;
            if (_channelDropdown != null)
            {
                channel = (ChatChannel)Mathf.Clamp(_channelDropdown.value, 0, 2);
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
    }
}
