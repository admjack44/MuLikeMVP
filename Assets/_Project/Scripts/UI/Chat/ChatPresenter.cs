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

        public ChatPresenter(ChatView view, ChatClientSystem chatSystem)
        {
            _view = view;
            _chatSystem = chatSystem;
        }

        public void Bind()
        {
            _view.SendRequested += HandleSendRequested;
            _view.ClearRequested += HandleClearRequested;
            _chatSystem.OnMessageReceived += HandleMessageReceived;

            _view.SetInteractable(true);
            _view.Render(_chatSystem.Messages);
            _view.SetStatus("Chat connected.");
        }

        public void Unbind()
        {
            _view.SendRequested -= HandleSendRequested;
            _view.ClearRequested -= HandleClearRequested;
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
            _view.Render(_chatSystem.Messages);
            _view.SetStatus("Chat buffer cleared.");
        }

        private void HandleMessageReceived(ChatMessage _)
        {
            _view.Render(_chatSystem.Messages);
        }
    }
}
