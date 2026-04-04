using MuLike.Shared.Protocol;

namespace MuLike.Networking
{
    /// <summary>
    /// Utilities to parse server payloads after routing by opcode.
    /// </summary>
    public static class ServerMessageParser
    {
        public static bool TryParseLoginResponse(byte[] payload, out bool success, out string token, out string message)
        {
            return PacketContracts.TryReadLoginResponse(payload, out success, out token, out message);
        }

        public static bool TryParseMoveResponse(byte[] payload, out bool success, out float x, out float y, out float z, out string message)
        {
            return PacketContracts.TryReadMoveResponse(payload, out success, out x, out y, out z, out message);
        }

        public static bool TryParseSkillCastResponse(byte[] payload, out bool success, out int targetId, out int damage, out string message)
        {
            return PacketContracts.TryReadSkillCastResponse(payload, out success, out targetId, out damage, out message);
        }
    }
}
