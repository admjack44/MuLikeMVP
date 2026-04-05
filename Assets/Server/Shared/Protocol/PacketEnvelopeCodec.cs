using System;
using System.IO;

namespace MuLike.Shared.Protocol
{
    public sealed class PacketEnvelope
    {
        public byte Version { get; set; } = ProtocolVersion.Current;
        public ProtocolDomain Domain { get; set; } = ProtocolDomain.Unknown;
        public ProtocolMessageKind Kind { get; set; } = ProtocolMessageKind.Unknown;
        public ushort Opcode { get; set; }
        public uint RequestId { get; set; }
        public ProtocolError Error { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    public static class PacketEnvelopeCodec
    {
        public static byte[] Encode(ushort opcode, uint requestId, ProtocolError error, Action<BinaryWriter> payloadWriter)
        {
            ProtocolOpcodeInfo info = ProtocolCatalog.GetInfo(opcode);
            byte[] payload = BuildPayload(payloadWriter);

            byte[] wrappedPayload = BuildEnvelopePayload(
                ProtocolVersion.Current,
                info.Domain,
                info.Kind,
                requestId,
                error,
                payload);

            return PacketCodec.Encode(opcode, writer => writer.Write(wrappedPayload));
        }

        public static bool TryDecode(byte[] packet, out PacketEnvelope envelope)
        {
            envelope = null;

            if (!PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload))
                return false;

            if (TryDecodeEnvelopePayload(opcode, payload, out envelope))
                return true;

            ProtocolOpcodeInfo legacyInfo = ProtocolCatalog.GetInfo(opcode);
            envelope = new PacketEnvelope
            {
                Version = ProtocolVersion.Legacy,
                Domain = legacyInfo.Domain,
                Kind = legacyInfo.Kind,
                Opcode = opcode,
                RequestId = 0,
                Payload = payload ?? Array.Empty<byte>(),
                Error = null
            };

            return true;
        }

        public static bool TryDecodeEnvelopePayload(ushort opcode, byte[] payload, out PacketEnvelope envelope)
        {
            envelope = null;
            if (payload == null || payload.Length < 9)
                return false;

            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);

                byte version = reader.ReadByte();
                if (version != ProtocolVersion.Current)
                    return false;

                ProtocolMessageKind kind = (ProtocolMessageKind)reader.ReadByte();
                ProtocolDomain domain = (ProtocolDomain)reader.ReadByte();
                uint requestId = reader.ReadUInt32();
                bool hasError = reader.ReadBoolean();

                ProtocolError error = null;
                if (hasError)
                {
                    var code = (ProtocolErrorCode)reader.ReadInt32();
                    string message = PacketCodec.ReadString(reader);
                    string details = PacketCodec.ReadString(reader);
                    error = ProtocolError.Create(code, message, details);
                }

                int bodyLength = reader.ReadInt32();
                if (bodyLength < 0 || ms.Position + bodyLength > ms.Length)
                    return false;

                byte[] body = reader.ReadBytes(bodyLength);

                envelope = new PacketEnvelope
                {
                    Version = version,
                    Domain = domain,
                    Kind = kind,
                    Opcode = opcode,
                    RequestId = requestId,
                    Error = error,
                    Payload = body
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] BuildEnvelopePayload(
            byte version,
            ProtocolDomain domain,
            ProtocolMessageKind kind,
            uint requestId,
            ProtocolError error,
            byte[] payload)
        {
            payload ??= Array.Empty<byte>();

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(version);
            writer.Write((byte)kind);
            writer.Write((byte)domain);
            writer.Write(requestId);

            bool hasError = error != null;
            writer.Write(hasError);
            if (hasError)
            {
                writer.Write((int)error.Code);
                PacketCodec.WriteString(writer, error.Message);
                PacketCodec.WriteString(writer, error.Details);
            }

            writer.Write(payload.Length);
            writer.Write(payload);
            return ms.ToArray();
        }

        private static byte[] BuildPayload(Action<BinaryWriter> payloadWriter)
        {
            using var payloadStream = new MemoryStream();
            using (var payloadWriterBinary = new BinaryWriter(payloadStream, System.Text.Encoding.UTF8, true))
            {
                payloadWriter?.Invoke(payloadWriterBinary);
            }

            return payloadStream.ToArray();
        }
    }
}
