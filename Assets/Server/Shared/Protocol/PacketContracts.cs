using System;
using System.IO;

namespace MuLike.Shared.Protocol
{
    public static class PacketContracts
    {
        public static byte[] CreateLoginRequest(string username, string password)
        {
            return PacketCodec.Encode(NetOpcodes.LoginRequest, writer =>
            {
                PacketCodec.WriteString(writer, username);
                PacketCodec.WriteString(writer, password);
            });
        }

        public static bool TryReadLoginRequest(byte[] payload, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                username = PacketCodec.ReadString(reader);
                password = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateLoginResponse(bool success, string token, string message)
        {
            return PacketCodec.Encode(NetOpcodes.LoginResponse, writer =>
            {
                writer.Write(success);
                PacketCodec.WriteString(writer, token ?? string.Empty);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadLoginResponse(byte[] payload, out bool success, out string token, out string message)
        {
            success = false;
            token = string.Empty;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                token = PacketCodec.ReadString(reader);
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateMoveRequest(float x, float y, float z)
        {
            return PacketCodec.Encode(NetOpcodes.MoveRequest, writer =>
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
            });
        }

        public static bool TryReadMoveRequest(byte[] payload, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                x = reader.ReadSingle();
                y = reader.ReadSingle();
                z = reader.ReadSingle();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateMoveResponse(bool success, float x, float y, float z, string message)
        {
            return PacketCodec.Encode(NetOpcodes.MoveResponse, writer =>
            {
                writer.Write(success);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadMoveResponse(byte[] payload, out bool success, out float x, out float y, out float z, out string message)
        {
            success = false;
            x = 0f;
            y = 0f;
            z = 0f;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                x = reader.ReadSingle();
                y = reader.ReadSingle();
                z = reader.ReadSingle();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateSkillCastRequest(int skillId, int targetId)
        {
            return PacketCodec.Encode(NetOpcodes.SkillCastRequest, writer =>
            {
                writer.Write(skillId);
                writer.Write(targetId);
            });
        }

        public static bool TryReadSkillCastRequest(byte[] payload, out int skillId, out int targetId)
        {
            skillId = 0;
            targetId = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                skillId = reader.ReadInt32();
                targetId = reader.ReadInt32();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateSkillCastResponse(bool success, int targetId, int damage, string message)
        {
            return PacketCodec.Encode(NetOpcodes.SkillCastResponse, writer =>
            {
                writer.Write(success);
                writer.Write(targetId);
                writer.Write(damage);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadSkillCastResponse(byte[] payload, out bool success, out int targetId, out int damage, out string message)
        {
            success = false;
            targetId = 0;
            damage = 0;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                targetId = reader.ReadInt32();
                damage = reader.ReadInt32();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateErrorResponse(string message)
        {
            return PacketCodec.Encode(NetOpcodes.ErrorResponse, writer =>
            {
                PacketCodec.WriteString(writer, message ?? "Unknown error");
            });
        }
    }
}
