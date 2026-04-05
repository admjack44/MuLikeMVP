using MuLike.Shared.Protocol;

namespace MuLike.Networking
{
    /// <summary>
    /// Factory for building outgoing client packets with consistent framing (opcode + payload).
    /// </summary>
    public static class ClientMessageFactory
    {
        public static byte[] CreateLoginRequest(string username, string password)
        {
            return PacketContracts.CreateLoginRequest(username, password);
        }

        public static byte[] CreateRefreshTokenRequest(string refreshToken)
        {
            return PacketContracts.CreateRefreshTokenRequest(refreshToken);
        }

        public static byte[] CreateHeartbeatRequest(long clientUtcTicks)
        {
            return PacketContracts.CreateHeartbeatRequest(clientUtcTicks);
        }

        public static byte[] CreateListCharactersRequest(string accessToken)
        {
            return PacketContracts.CreateListCharactersRequest(accessToken);
        }

        public static byte[] CreateCreateCharacterRequest(string characterName, string characterClass)
        {
            return PacketContracts.CreateCreateCharacterRequest(characterName, characterClass);
        }

        public static byte[] CreateDeleteCharacterRequest(int characterId)
        {
            return PacketContracts.CreateDeleteCharacterRequest(characterId);
        }

        public static byte[] CreateSelectCharacterRequest(int characterId)
        {
            return PacketContracts.CreateSelectCharacterRequest(characterId);
        }

        public static byte[] CreateMoveRequest(float x, float y, float z)
        {
            return PacketContracts.CreateMoveRequest(x, y, z);
        }

        public static byte[] CreateAttackRequest(int targetId)
        {
            return PacketContracts.CreateAttackRequest(targetId);
        }

        public static byte[] CreateSkillCastRequest(int skillId, int targetId)
        {
            return PacketContracts.CreateSkillCastRequest(skillId, targetId);
        }
    }
}
