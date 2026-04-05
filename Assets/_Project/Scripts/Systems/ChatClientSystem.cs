using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MuLike.Systems
{
    /// <summary>
    /// Client chat buffer + message pipeline with transport abstraction.
    /// </summary>
    public sealed class ChatClientSystem
    {
        private readonly List<ChatMessage> _messages = new();
        private readonly int _bufferLimit;
        private IChatTransport _transport;

        public ChatClientSystem(int bufferLimit = 200)
        {
            _bufferLimit = Mathf.Max(20, bufferLimit);
        }

        public string LocalPlayerName { get; set; } = "Player";
        public int BufferLimit => _bufferLimit;
        public IReadOnlyList<ChatMessage> Messages => _messages;
        public bool HasTransport => _transport != null;

        public event Action<ChatMessage> OnMessageReceived;

        public void AttachTransport(IChatTransport transport)
        {
            if (_transport == transport)
                return;

            if (_transport != null)
                _transport.MessageReceived -= HandleTransportMessageReceived;

            _transport = transport;

            if (_transport != null)
                _transport.MessageReceived += HandleTransportMessageReceived;
        }

        public async Task<bool> SendAsync(ChatSendRequest request, Action<string> onError = null)
        {
            if (_transport == null)
            {
                onError?.Invoke("Chat transport is not configured.");
                return false;
            }

            ChatSendRequest sanitized = new ChatSendRequest
            {
                Channel = request.Channel,
                Text = ChatSanitizer.SanitizeText(request.Text),
                Target = ChatSanitizer.SanitizeName(request.Target)
            };

            if (!ValidateRequest(sanitized, out string validationError))
            {
                onError?.Invoke(validationError);
                return false;
            }

            await _transport.SendAsync(sanitized, ChatSanitizer.SanitizeName(LocalPlayerName));
            return true;
        }

        public void ReceiveSystemMessage(string text)
        {
            AppendMessage(new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Channel = ChatChannel.System,
                Sender = "System",
                Target = string.Empty,
                Text = ChatSanitizer.SanitizeText(text),
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsLocalEcho = true
            });
        }

        public void Clear()
        {
            _messages.Clear();
        }

        private void HandleTransportMessageReceived(ChatMessage incoming)
        {
            ChatMessage sanitized = incoming;
            sanitized.Sender = ChatSanitizer.SanitizeName(incoming.Sender);
            sanitized.Target = ChatSanitizer.SanitizeName(incoming.Target);
            sanitized.Text = ChatSanitizer.SanitizeText(incoming.Text);
            if (string.IsNullOrWhiteSpace(sanitized.MessageId))
                sanitized.MessageId = Guid.NewGuid().ToString("N");
            if (sanitized.TimestampUnixMs <= 0)
                sanitized.TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            AppendMessage(sanitized);
        }

        private void AppendMessage(ChatMessage message)
        {
            _messages.Add(message);
            if (_messages.Count > _bufferLimit)
            {
                int overflow = _messages.Count - _bufferLimit;
                _messages.RemoveRange(0, overflow);
            }

            OnMessageReceived?.Invoke(message);
        }

        private static bool ValidateRequest(ChatSendRequest request, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                error = "Message cannot be empty.";
                return false;
            }

            if (request.Channel == ChatChannel.Private && string.IsNullOrWhiteSpace(request.Target))
            {
                error = "Private messages require a target name.";
                return false;
            }

            return true;
        }
    }
}
