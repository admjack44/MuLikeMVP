using MuLike.Systems;
using UnityEngine;
using MuLike.Chat;

namespace MuLike.UI.Chat
{
    /// <summary>
    /// Orchestrates chat system and chat view.
    /// </summary>
    public sealed class ChatPresenter
    {
        private readonly ChatView _view;
        private readonly ChatClientSystem _chatSystem;
        private readonly ChatSystem _socialChatSystem;
        private ChatFilterMode _activeFilter = ChatFilterMode.World;

        public ChatPresenter(ChatView view, ChatClientSystem chatSystem)
        {
            _view = view;
            _chatSystem = chatSystem;
            _socialChatSystem = Object.FindAnyObjectByType<ChatSystem>();
        }

        public void Bind()
        {
            _view.SendRequested += HandleSendRequested;
            _view.ClearRequested += HandleClearRequested;
            _view.FilterChanged += HandleFilterChanged;
            if (_socialChatSystem != null)
                _socialChatSystem.OnMessageReceived += HandleMessageReceived;
            else
                _chatSystem.OnMessageReceived += HandleMessageReceived;

            _view.SetInteractable(true);
            RenderFiltered();
            _view.SetStatus("Chat connected.");
        }

        public void Unbind()
        {
            _view.SendRequested -= HandleSendRequested;
            _view.ClearRequested -= HandleClearRequested;
            _view.FilterChanged -= HandleFilterChanged;
            if (_socialChatSystem != null)
                _socialChatSystem.OnMessageReceived -= HandleMessageReceived;
            else
                _chatSystem.OnMessageReceived -= HandleMessageReceived;
        }

        private async void HandleSendRequested(ChatSendRequest request)
        {
            _view.SetInteractable(false);

            bool sent;
            if (_socialChatSystem != null)
            {
                sent = await _socialChatSystem.SendAsync(request, error =>
                {
                    _view.SetStatus(error);
                    Debug.LogWarning($"[ChatPresenter] Send blocked: {error}");
                });
            }
            else
            {
                sent = await _chatSystem.SendAsync(request, error =>
                {
                    _view.SetStatus(error);
                    Debug.LogWarning($"[ChatPresenter] Send blocked: {error}");
                });
            }

            if (sent)
            {
                _view.ClearInput();
                _view.SetStatus("Message sent.");
            }

            _view.SetInteractable(true);
        }

        private void HandleClearRequested()
        {
            if (_socialChatSystem != null)
                _socialChatSystem.Clear();
            else
                _chatSystem.Clear();

            RenderFiltered();
            _view.SetStatus("Chat buffer cleared.");
        }

        private void HandleMessageReceived(ChatMessage _)
        {
            RenderFiltered();
        }

        private void HandleFilterChanged(ChatFilterMode filter)
        {
            _activeFilter = filter;
            RenderFiltered();
        }

        private void RenderFiltered()
        {
            var messages = _socialChatSystem != null ? _socialChatSystem.Messages : _chatSystem.Messages;
            var filtered = new System.Collections.Generic.List<ChatMessage>(messages.Count);
            for (int i = 0; i < messages.Count; i++)
            {
                ChatMessage message = messages[i];
                if (IsMessageVisible(message.Channel))
                    filtered.Add(message);
            }

            _view.Render(filtered);
        }

        private bool IsMessageVisible(ChatChannel channel)
        {
            if (_activeFilter == ChatFilterMode.All)
                return true;

            return _activeFilter switch
            {
                ChatFilterMode.World => channel == ChatChannel.World || channel == ChatChannel.General,
                ChatFilterMode.Trade => channel == ChatChannel.Trade,
                ChatFilterMode.Party => channel == ChatChannel.Party,
                ChatFilterMode.Guild => channel == ChatChannel.Guild,
                ChatFilterMode.System => channel == ChatChannel.System,
                _ => true
            };
        }
    }
}
