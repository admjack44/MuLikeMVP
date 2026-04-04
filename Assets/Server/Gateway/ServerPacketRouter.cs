using System;
using System.Collections.Generic;
using MuLike.Server.Infrastructure;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Gateway
{
    /// <summary>
    /// Routes decoded client packets to server application use-cases and returns response packets.
    /// </summary>
    public sealed class ServerPacketRouter
    {
        private readonly ServerApplication _app;
        private readonly Dictionary<string, AccountRecord> _accounts;

        public ServerPacketRouter(ServerApplication app)
        {
            _app = app;
            _accounts = BuildDefaultAccounts();
        }

        public byte[] HandlePacket(Guid sessionId, byte[] packet)
        {
            if (!_app.TouchSession(sessionId))
                return PacketContracts.CreateErrorResponse("Session expired. Reconnect required.");

            if (!PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload))
                return PacketContracts.CreateErrorResponse("Malformed packet");

            return opcode switch
            {
                NetOpcodes.LoginRequest => HandleLogin(sessionId, payload),
                NetOpcodes.MoveRequest => HandleMove(sessionId, payload),
                NetOpcodes.SkillCastRequest => HandleSkillCast(sessionId, payload),
                _ => PacketContracts.CreateErrorResponse($"Unsupported opcode: {opcode}")
            };
        }

        private byte[] HandleLogin(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadLoginRequest(payload, out string username, out string password))
                return PacketContracts.CreateErrorResponse("Invalid login payload");

            if (!_accounts.TryGetValue(username, out var account))
                return PacketContracts.CreateLoginResponse(false, string.Empty, "Account not found");

            bool ok = _app.AuthenticateClient(
                sessionId,
                account.AccountId,
                account.AccountName,
                password,
                account.PasswordHash,
                out string accessToken);

            if (!ok)
                return PacketContracts.CreateLoginResponse(false, string.Empty, "Invalid credentials");

            return PacketContracts.CreateLoginResponse(true, accessToken, "Login successful");
        }

        private byte[] HandleMove(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadMoveRequest(payload, out float x, out float y, out float z))
                return PacketContracts.CreateErrorResponse("Invalid move payload");

            bool moved = _app.TryMoveCharacter(sessionId, x, y, z);
            return PacketContracts.CreateMoveResponse(moved, x, y, z, moved ? "Move applied" : "Move rejected");
        }

        private byte[] HandleSkillCast(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadSkillCastRequest(payload, out int skillId, out int targetId))
                return PacketContracts.CreateErrorResponse("Invalid skill payload");

            bool cast = _app.TryCastSkill(sessionId, skillId, targetId, out int damage);
            return PacketContracts.CreateSkillCastResponse(cast, targetId, damage, cast ? "Skill cast applied" : "Skill cast rejected");
        }

        private Dictionary<string, AccountRecord> BuildDefaultAccounts()
        {
            var passwordHasher = new Auth.PasswordHasher();
            return new Dictionary<string, AccountRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new AccountRecord(1, "admin", passwordHasher.Hash("admin123")),
                ["tester"] = new AccountRecord(2, "tester", passwordHasher.Hash("tester123"))
            };
        }

        private sealed class AccountRecord
        {
            public int AccountId { get; }
            public string AccountName { get; }
            public string PasswordHash { get; }

            public AccountRecord(int accountId, string accountName, string passwordHash)
            {
                AccountId = accountId;
                AccountName = accountName;
                PasswordHash = passwordHash;
            }
        }
    }
}
