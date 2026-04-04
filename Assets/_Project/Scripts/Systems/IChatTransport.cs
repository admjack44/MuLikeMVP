using System;
using System.Threading.Tasks;

namespace MuLike.Systems
{
    public interface IChatTransport
    {
        event Action<ChatMessage> MessageReceived;
        Task SendAsync(ChatSendRequest request, string localSender);
    }
}
