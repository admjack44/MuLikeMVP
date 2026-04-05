namespace MuLike.Shared.Protocol
{
    public static class NetOpcodes
    {
        public static class Auth
        {
            public const ushort LoginRequest = 0x0001;
            public const ushort LoginResponse = 0x0002;
            public const ushort RefreshTokenRequest = 0x0003;
            public const ushort RefreshTokenResponse = 0x0004;
            public const ushort HeartbeatRequest = 0x0005;
            public const ushort HeartbeatResponse = 0x0006;
        }

        public static class Character
        {
            public const ushort ListRequest = 0x0010;
            public const ushort ListResponse = 0x0011;
            public const ushort CreateRequest = 0x0012;
            public const ushort CreateResponse = 0x0013;
            public const ushort DeleteRequest = 0x0014;
            public const ushort DeleteResponse = 0x0015;
            public const ushort SelectRequest = 0x0016;
            public const ushort SelectResponse = 0x0017;
        }

        public static class World
        {
            public const ushort FullSnapshot = 0x0020;
            public const ushort DeltaSnapshot = 0x0021;
            public const ushort MoveRequest = 0x0100;
            public const ushort MoveResponse = 0x0101;
            public const ushort MoveSnapshot = 0x0102;
        }

        public static class Combat
        {
            public const ushort AttackRequest = 0x0110;
            public const ushort AttackResponse = 0x0111;
            public const ushort DeathNotification = 0x0112;
            public const ushort RespawnNotification = 0x0113;
        }

        public static class Skill
        {
            public const ushort CastRequest = 0x0200;
            public const ushort CastResponse = 0x0201;
            public const ushort ListRequest = 0x0202;
            public const ushort ListResponse = 0x0203;
        }

        public static class Economy
        {
            public const ushort TradeCommand = 0x0300;
            public const ushort TradeEvent = 0x0301;
            public const ushort AuctionCommand = 0x0310;
            public const ushort AuctionEvent = 0x0311;
        }

        public static class System
        {
            public const ushort ErrorResponse = 0x7FFF;
        }

        // Legacy aliases kept for backward compatibility with existing call sites.
        public const ushort LoginRequest = Auth.LoginRequest;
        public const ushort LoginResponse = Auth.LoginResponse;
        public const ushort RefreshTokenRequest = Auth.RefreshTokenRequest;
        public const ushort RefreshTokenResponse = Auth.RefreshTokenResponse;
        public const ushort HeartbeatRequest = Auth.HeartbeatRequest;
        public const ushort HeartbeatResponse = Auth.HeartbeatResponse;

        public const ushort ListCharactersRequest = Character.ListRequest;
        public const ushort ListCharactersResponse = Character.ListResponse;
        public const ushort CreateCharacterRequest = Character.CreateRequest;
        public const ushort CreateCharacterResponse = Character.CreateResponse;
        public const ushort DeleteCharacterRequest = Character.DeleteRequest;
        public const ushort DeleteCharacterResponse = Character.DeleteResponse;
        public const ushort SelectCharacterRequest = Character.SelectRequest;
        public const ushort SelectCharacterResponse = Character.SelectResponse;

        public const ushort FullSnapshot = World.FullSnapshot;
        public const ushort DeltaSnapshot = World.DeltaSnapshot;

        public const ushort MoveRequest = World.MoveRequest;
        public const ushort MoveResponse = World.MoveResponse;
        public const ushort MoveSnapshot = World.MoveSnapshot;

        public const ushort AttackRequest = Combat.AttackRequest;
        public const ushort AttackResponse = Combat.AttackResponse;
        public const ushort DeathNotification = Combat.DeathNotification;
        public const ushort RespawnNotification = Combat.RespawnNotification;

        public const ushort SkillListRequest = Skill.ListRequest;
        public const ushort SkillListResponse = Skill.ListResponse;
        public const ushort SkillCastRequest = Skill.CastRequest;
        public const ushort SkillCastResponse = Skill.CastResponse;

        public const ushort TradeCommand = Economy.TradeCommand;
        public const ushort TradeEvent = Economy.TradeEvent;
        public const ushort AuctionCommand = Economy.AuctionCommand;
        public const ushort AuctionEvent = Economy.AuctionEvent;

        public const ushort ErrorResponse = System.ErrorResponse;
    }
}
