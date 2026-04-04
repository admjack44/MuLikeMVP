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

        public static byte[] CreateMoveRequest(float x, float y, float z)
        {
            return PacketContracts.CreateMoveRequest(x, y, z);
        }

        public static byte[] CreateSkillCastRequest(int skillId, int targetId)
        {
            return PacketContracts.CreateSkillCastRequest(skillId, targetId);
        }
    }
}
