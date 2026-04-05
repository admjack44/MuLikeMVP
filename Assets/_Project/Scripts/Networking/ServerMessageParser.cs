using System.Collections.Generic;
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

        public static bool TryParseLoginResponse(byte[] payload, out bool success, out PacketContracts.TokenBundle tokens, out string message)
        {
            return PacketContracts.TryReadLoginResponse(payload, out success, out tokens, out message);
        }

        public static bool TryParseRefreshTokenResponse(byte[] payload, out bool success, out PacketContracts.TokenBundle tokens, out string message)
        {
            return PacketContracts.TryReadRefreshTokenResponse(payload, out success, out tokens, out message);
        }

        public static bool TryParseHeartbeatResponse(byte[] payload, out long serverUtcTicks)
        {
            return PacketContracts.TryReadHeartbeatResponse(payload, out serverUtcTicks);
        }

        public static bool TryParseListCharactersResponse(byte[] payload, out List<CharacterSummary> characters)
        {
            return PacketContracts.TryReadListCharactersResponse(payload, out characters);
        }

        public static bool TryParseCreateCharacterResponse(byte[] payload, out bool success, out int characterId, out string message)
        {
            return PacketContracts.TryReadCreateCharacterResponse(payload, out success, out characterId, out message);
        }

        public static bool TryParseDeleteCharacterResponse(byte[] payload, out bool success, out string message)
        {
            return PacketContracts.TryReadDeleteCharacterResponse(payload, out success, out message);
        }

        public static bool TryParseSelectCharacterResponse(byte[] payload, out bool success, out int characterId, out string message)
        {
            return PacketContracts.TryReadSelectCharacterResponse(payload, out success, out characterId, out message);
        }

        public static bool TryParseMoveResponse(byte[] payload, out bool success, out float x, out float y, out float z, out string message)
        {
            return PacketContracts.TryReadMoveResponse(payload, out success, out x, out y, out z, out message);
        }

        public static bool TryParseSkillCastResponse(byte[] payload, out bool success, out int targetId, out int damage, out string message)
        {
            return PacketContracts.TryReadSkillCastResponse(payload, out success, out targetId, out damage, out message);
        }

        public static bool TryParseMoveSnapshot(byte[] payload, out int entityId, out float x, out float y, out float z)
        {
            return PacketContracts.TryReadMoveSnapshot(payload, out entityId, out x, out y, out z);
        }

        public static bool TryParseAttackResponse(byte[] payload, out int targetId, out bool hitSuccess, out int damage, out bool isCritical)
        {
            return PacketContracts.TryReadAttackResponse(payload, out targetId, out hitSuccess, out damage, out isCritical);
        }

        public static bool TryParseDeathNotification(byte[] payload, out int entityId)
        {
            return PacketContracts.TryReadDeathNotification(payload, out entityId);
        }

        public static bool TryParseRespawnNotification(byte[] payload, out int entityId, out float x, out float y, out float z)
        {
            return PacketContracts.TryReadRespawnNotification(payload, out entityId, out x, out y, out z);
        }

        public static bool TryParseSnapshotData(byte[] payload, out SnapshotData snapshot)
        {
            return PacketContracts.TryReadSnapshotData(payload, out snapshot);
        }

        public static bool TryParseErrorResponse(byte[] payload, out ProtocolError error, out uint requestId)
        {
            return PacketContracts.TryReadErrorResponse(payload, out error, out requestId);
        }
    }
}
