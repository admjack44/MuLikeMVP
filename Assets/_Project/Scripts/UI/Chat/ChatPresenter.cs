using MuLike.Systems;
using UnityEngine;

namespace MuLike.UI.Chat
{
    /// <summary>
    /// Orchestrates chat system and chat view.
    /// </summary>
    public sealed class ChatPresenter
    {
        private readonly ChatView _view;
        private readonly ChatClientSystem _chatSystem;
        private ChatFilterMode _activeFilter = ChatFilterMode.World;

        public ChatPresenter(ChatView view, ChatClientSystem chatSystem)
        {
            _view = view;
            _chatSystem = chatSystem;
        }

        public void Bind()
        {
            _view.SendRequested += HandleSendRequested;
            _view.ClearRequested += HandleClearRequested;
            _view.FilterChanged += HandleFilterChanged;
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
            _chatSystem.OnMessageReceived -= HandleMessageReceived;
        }

        private async void HandleSendRequested(ChatSendRequest request)
        {
            _view.SetInteractable(false);

            bool sent = await _chatSystem.SendAsync(request, error =>
            {
                _view.SetStatus(error);
                Debug.LogWarning($"[ChatPresenter] Send blocked: {error}");
            });

            if (sent)
            {
                _view.ClearInput();
                _view.SetStatus("Message sent.");
            }

            _view.SetInteractable(true);
        }

        private void HandleClearRequested()
        {
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
            var filtered = new System.Collections.Generic.List<ChatMessage>(_chatSystem.Messages.Count);
            for (int i = 0; i < _chatSystem.Messages.Count; i++)
            {
                ChatMessage message = _chatSystem.Messages[i];
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
                ChatFilterMode.Party => channel == ChatChannel.Party,
                ChatFilterMode.Guild => channel == ChatChannel.Guild,
                ChatFilterMode.System => channel == ChatChannel.System,
                _ => true
            };
        }
    }
}
