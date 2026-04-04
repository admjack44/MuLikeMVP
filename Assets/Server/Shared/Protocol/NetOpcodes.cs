namespace MuLike.Shared.Protocol
{
    public static class NetOpcodes
    {
        public const ushort LoginRequest = 0x0001;
        public const ushort LoginResponse = 0x0002;

        public const ushort MoveRequest = 0x0100;
        public const ushort MoveResponse = 0x0101;

        public const ushort SkillCastRequest = 0x0200;
        public const ushort SkillCastResponse = 0x0201;

        public const ushort ErrorResponse = 0x7FFF;
    }
}
