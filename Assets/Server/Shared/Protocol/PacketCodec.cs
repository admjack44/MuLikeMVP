using System;
using System.IO;
using System.Text;

namespace MuLike.Shared.Protocol
{
    /// <summary>
    /// Packet framing format: [2-byte length][2-byte opcode][payload].
    /// Length includes opcode + payload, but not the length field itself.
    /// </summary>
    public static class PacketCodec
    {
        private const int HeaderSize = 4;

        public static byte[] Encode(ushort opcode, Action<BinaryWriter> payloadWriter)
        {
            using var payloadStream = new MemoryStream();
            using (var writer = new BinaryWriter(payloadStream, Encoding.UTF8, true))
            {
                payloadWriter?.Invoke(writer);
            }

            byte[] payload = payloadStream.ToArray();
            int bodyLength = 2 + payload.Length;
            byte[] packet = new byte[HeaderSize + payload.Length];

            WriteUInt16BigEndian(packet, 0, (ushort)bodyLength);
            WriteUInt16BigEndian(packet, 2, opcode);
            Buffer.BlockCopy(payload, 0, packet, HeaderSize, payload.Length);

            return packet;
        }

        public static bool TryDecode(byte[] packet, out ushort opcode, out byte[] payload)
        {
            opcode = 0;
            payload = null;

            if (packet == null || packet.Length < HeaderSize)
                return false;

            ushort bodyLength = ReadUInt16BigEndian(packet, 0);
            if (packet.Length != bodyLength + 2)
                return false;

            opcode = ReadUInt16BigEndian(packet, 2);
            int payloadLength = bodyLength - 2;
            payload = new byte[payloadLength];
            if (payloadLength > 0)
                Buffer.BlockCopy(packet, HeaderSize, payload, 0, payloadLength);

            return true;
        }

        public static bool TryReadFrame(byte[] buffer, int offset, int availableBytes, out int frameLength)
        {
            frameLength = 0;
            if (availableBytes < 2) return false;

            ushort bodyLength = ReadUInt16BigEndian(buffer, offset);
            frameLength = bodyLength + 2;
            return availableBytes >= frameLength;
        }

        public static void WriteString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        public static string ReadString(BinaryReader reader)
        {
            ushort len = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        public static ushort ReadUInt16BigEndian(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
        }
    }
}
