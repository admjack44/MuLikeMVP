using System;
using System.Threading.Tasks;

namespace MuLike.Systems
{
    /// <summary>
    /// Local transport for MVP iteration. It echoes own messages and emits simple system feedback.
    /// </summary>
    public sealed class MockChatTransport : IChatTransport
    {
        public event Action<ChatMessage> MessageReceived;

        public Task SendAsync(ChatSendRequest request, string localSender)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var userMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Channel = request.Channel,
                Sender = string.IsNullOrWhiteSpace(localSender) ? "Player" : localSender,
                Target = request.Target,
                Text = request.Text,
                TimestampUnixMs = now,
                IsLocalEcho = true
            };

            MessageReceived?.Invoke(userMessage);

            if (request.Channel == ChatChannel.Private)
            {
                var systemAck = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    Channel = ChatChannel.System,
                    Sender = "System",
                    Target = string.Empty,
                    Text = $"Private message sent to {request.Target}.",
                    TimestampUnixMs = now,
                    IsLocalEcho = true
                };
                MessageReceived?.Invoke(systemAck);
            }

            return Task.CompletedTask;
        }
    }
}
