using System;
using System.Text;
using System.Threading.Tasks;
using MuLike.Shared.Protocol;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// PacketRouter-based chat transport. Uses PacketCodec framing + JSON payload.
    /// </summary>
    public sealed class PacketRouterChatTransport : IChatTransport
    {
        private readonly PacketRouter _router;
        private readonly ushort _chatReceiveOpcode;
        private readonly ushort _chatSendOpcode;
        private readonly Func<byte[], Task> _sendPacketAsync;

        public PacketRouterChatTransport(
            PacketRouter router,
            ushort chatReceiveOpcode,
            ushort chatSendOpcode,
            Func<byte[], Task> sendPacketAsync)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _chatReceiveOpcode = chatReceiveOpcode;
            _chatSendOpcode = chatSendOpcode;
            _sendPacketAsync = sendPacketAsync ?? throw new ArgumentNullException(nameof(sendPacketAsync));

            _router.Subscribe(_chatReceiveOpcode, HandleChatPayload);
        }

        public event Action<ChatMessage> MessageReceived;

        public async Task SendAsync(ChatSendRequest request, string localSender)
        {
            var wire = new ChatWireMessage
            {
                version = 1,
                messageId = Guid.NewGuid().ToString("N"),
                channel = request.Channel.ToString(),
                from = localSender,
                to = request.Target,
                text = request.Text,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            string json = JsonUtility.ToJson(wire);
            byte[] packet = PacketCodec.Encode(_chatSendOpcode, writer =>
            {
                PacketCodec.WriteString(writer, json);
            });

            await _sendPacketAsync(packet);
        }

        private void HandleChatPayload(byte[] payload)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(payload);
                using var reader = new System.IO.BinaryReader(ms, Encoding.UTF8, true);
                string json = PacketCodec.ReadString(reader);
                ChatWireMessage wire = JsonUtility.FromJson<ChatWireMessage>(json);

                if (wire.version <= 0)
                    wire.version = 1;

                ChatChannel channel = ChatChannel.General;
                if (!Enum.TryParse(wire.channel, true, out channel))
                    channel = ChatChannel.General;

                MessageReceived?.Invoke(new ChatMessage
                {
                    MessageId = wire.messageId,
                    Channel = channel,
                    Sender = wire.from,
                    Target = wire.to,
                    Text = wire.text,
                    TimestampUnixMs = wire.ts,
                    IsLocalEcho = false
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PacketRouterChatTransport] Invalid chat payload: {ex.Message}");
            }
        }
    }
}
