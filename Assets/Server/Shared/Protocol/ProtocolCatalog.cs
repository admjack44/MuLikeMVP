using System.Collections.Generic;

namespace MuLike.Shared.Protocol
{
    public readonly struct ProtocolOpcodeInfo
    {
        public ProtocolOpcodeInfo(ProtocolDomain domain, ProtocolMessageKind kind)
        {
            Domain = domain;
            Kind = kind;
        }

        public ProtocolDomain Domain { get; }
        public ProtocolMessageKind Kind { get; }
    }

    public static class ProtocolCatalog
    {
        private static readonly Dictionary<ushort, ProtocolOpcodeInfo> _map = new()
        {
            [NetOpcodes.Auth.LoginRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Request),
            [NetOpcodes.Auth.LoginResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Response),
            [NetOpcodes.Auth.RefreshTokenRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Request),
            [NetOpcodes.Auth.RefreshTokenResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Response),
            [NetOpcodes.Auth.HeartbeatRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Request),
            [NetOpcodes.Auth.HeartbeatResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Auth, ProtocolMessageKind.Response),

            [NetOpcodes.Character.ListRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Request),
            [NetOpcodes.Character.ListResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Response),
            [NetOpcodes.Character.CreateRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Request),
            [NetOpcodes.Character.CreateResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Response),
            [NetOpcodes.Character.DeleteRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Request),
            [NetOpcodes.Character.DeleteResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Response),
            [NetOpcodes.Character.SelectRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Request),
            [NetOpcodes.Character.SelectResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Character, ProtocolMessageKind.Response),

            [NetOpcodes.World.FullSnapshot] = new ProtocolOpcodeInfo(ProtocolDomain.World, ProtocolMessageKind.Event),
            [NetOpcodes.World.DeltaSnapshot] = new ProtocolOpcodeInfo(ProtocolDomain.World, ProtocolMessageKind.Event),
            [NetOpcodes.World.MoveRequest] = new ProtocolOpcodeInfo(ProtocolDomain.World, ProtocolMessageKind.Request),
            [NetOpcodes.World.MoveResponse] = new ProtocolOpcodeInfo(ProtocolDomain.World, ProtocolMessageKind.Response),
            [NetOpcodes.World.MoveSnapshot] = new ProtocolOpcodeInfo(ProtocolDomain.World, ProtocolMessageKind.Event),

            [NetOpcodes.Combat.AttackRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Combat, ProtocolMessageKind.Request),
            [NetOpcodes.Combat.AttackResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Combat, ProtocolMessageKind.Response),
            [NetOpcodes.Combat.DeathNotification] = new ProtocolOpcodeInfo(ProtocolDomain.Combat, ProtocolMessageKind.Event),
            [NetOpcodes.Combat.RespawnNotification] = new ProtocolOpcodeInfo(ProtocolDomain.Combat, ProtocolMessageKind.Event),

            [NetOpcodes.Skill.CastRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Skill, ProtocolMessageKind.Request),
            [NetOpcodes.Skill.CastResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Skill, ProtocolMessageKind.Response),
            [NetOpcodes.Skill.ListRequest] = new ProtocolOpcodeInfo(ProtocolDomain.Skill, ProtocolMessageKind.Request),
            [NetOpcodes.Skill.ListResponse] = new ProtocolOpcodeInfo(ProtocolDomain.Skill, ProtocolMessageKind.Response),

            [NetOpcodes.Economy.TradeCommand] = new ProtocolOpcodeInfo(ProtocolDomain.Economy, ProtocolMessageKind.Request),
            [NetOpcodes.Economy.TradeEvent] = new ProtocolOpcodeInfo(ProtocolDomain.Economy, ProtocolMessageKind.Event),
            [NetOpcodes.Economy.AuctionCommand] = new ProtocolOpcodeInfo(ProtocolDomain.Economy, ProtocolMessageKind.Request),
            [NetOpcodes.Economy.AuctionEvent] = new ProtocolOpcodeInfo(ProtocolDomain.Economy, ProtocolMessageKind.Event),

            [NetOpcodes.System.ErrorResponse] = new ProtocolOpcodeInfo(ProtocolDomain.System, ProtocolMessageKind.Response)
        };

        public static ProtocolOpcodeInfo GetInfo(ushort opcode)
        {
            return _map.TryGetValue(opcode, out ProtocolOpcodeInfo info)
                ? info
                : new ProtocolOpcodeInfo(ProtocolDomain.Unknown, ProtocolMessageKind.Unknown);
        }

        public static bool TryGetInfo(ushort opcode, out ProtocolOpcodeInfo info)
        {
            return _map.TryGetValue(opcode, out info);
        }
    }
}
