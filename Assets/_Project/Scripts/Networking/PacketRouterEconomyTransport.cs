using System;
using System.Text;
using System.Threading.Tasks;
using MuLike.Shared.Protocol;
using UnityEngine;

namespace MuLike.Networking
{
    /// <summary>
    /// Generic packet-router transport for economy commands/events.
    /// Uses JSON payloads for rapid vertical-slice iteration.
    /// </summary>
    public sealed class PacketRouterEconomyTransport
    {
        [Serializable]
        public struct EconomyEnvelope
        {
            public int version;
            public string channel;
            public string action;
            public string sender;
            public string payloadJson;
            public long ts;
        }

        private readonly PacketRouter _router;
        private readonly ushort _tradeInboundOpcode;
        private readonly ushort _tradeOutboundOpcode;
        private readonly ushort _auctionInboundOpcode;
        private readonly ushort _auctionOutboundOpcode;
        private readonly Func<byte[], Task> _sendPacketAsync;

        public event Action<EconomyEnvelope> TradeEnvelopeReceived;
        public event Action<EconomyEnvelope> AuctionEnvelopeReceived;

        public PacketRouterEconomyTransport(
            PacketRouter router,
            ushort tradeInboundOpcode,
            ushort tradeOutboundOpcode,
            ushort auctionInboundOpcode,
            ushort auctionOutboundOpcode,
            Func<byte[], Task> sendPacketAsync)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _tradeInboundOpcode = tradeInboundOpcode;
            _tradeOutboundOpcode = tradeOutboundOpcode;
            _auctionInboundOpcode = auctionInboundOpcode;
            _auctionOutboundOpcode = auctionOutboundOpcode;
            _sendPacketAsync = sendPacketAsync ?? throw new ArgumentNullException(nameof(sendPacketAsync));

            _router.Subscribe(_tradeInboundOpcode, payload => HandleInbound(payload, true));
            _router.Subscribe(_auctionInboundOpcode, payload => HandleInbound(payload, false));
        }

        public Task SendTradeAsync(EconomyEnvelope envelope)
        {
            return SendEnvelopeAsync(_tradeOutboundOpcode, envelope);
        }

        public Task SendAuctionAsync(EconomyEnvelope envelope)
        {
            return SendEnvelopeAsync(_auctionOutboundOpcode, envelope);
        }

        private async Task SendEnvelopeAsync(ushort opcode, EconomyEnvelope envelope)
        {
            envelope.version = envelope.version <= 0 ? 1 : envelope.version;
            envelope.ts = envelope.ts <= 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : envelope.ts;
            string json = JsonUtility.ToJson(envelope);

            byte[] packet = PacketCodec.Encode(opcode, writer =>
            {
                PacketCodec.WriteString(writer, json);
            });

            await _sendPacketAsync(packet);
        }

        private void HandleInbound(byte[] payload, bool trade)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(payload);
                using var reader = new System.IO.BinaryReader(ms, Encoding.UTF8, true);
                string json = PacketCodec.ReadString(reader);
                EconomyEnvelope envelope = JsonUtility.FromJson<EconomyEnvelope>(json);
                if (string.IsNullOrWhiteSpace(envelope.channel))
                    envelope.channel = trade ? "trade" : "auction";

                if (trade)
                    TradeEnvelopeReceived?.Invoke(envelope);
                else
                    AuctionEnvelopeReceived?.Invoke(envelope);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PacketRouterEconomyTransport] Invalid economy payload: {ex.Message}");
            }
        }
    }
}