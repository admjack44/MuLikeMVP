using System;
using System.Collections.Generic;
using System.Linq;
using MuLike.Server.Auth;
using MuLike.Server.Game.Entities;
using MuLike.Server.Infrastructure;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Gateway
{
    /// <summary>
    /// Routes decoded client packets to server application use-cases and returns response packets.
    /// </summary>
    public sealed class ServerPacketRouter
    {
        private const string MessagePlayerNotFound = "Player not found";
        private const string MessageNotAuthenticated = "Not authenticated";

        private readonly ServerApplication _app;

        public ServerPacketRouter(ServerApplication app)
        {
            _app = app;
        }

        public byte[] HandlePacket(Guid sessionId, byte[] packet)
        {
            if (!_app.TouchSession(sessionId))
                return CreateError(ProtocolErrorCode.SessionExpired, "Session expired. Reconnect required.");

            if (!PacketCodec.TryDecode(packet, out ushort opcode, out byte[] payload))
                return CreateError(ProtocolErrorCode.MalformedPacket, "Malformed packet");

            return opcode switch
            {
                NetOpcodes.LoginRequest => HandleLogin(sessionId, payload),
                NetOpcodes.RefreshTokenRequest => HandleRefreshToken(sessionId, payload),
                NetOpcodes.HeartbeatRequest => HandleHeartbeat(payload),
                NetOpcodes.ListCharactersRequest => HandleListCharacters(sessionId, payload),
                NetOpcodes.CreateCharacterRequest => HandleCreateCharacter(sessionId, payload),
                NetOpcodes.DeleteCharacterRequest => HandleDeleteCharacter(sessionId, payload),
                NetOpcodes.SelectCharacterRequest => HandleSelectCharacter(sessionId, payload),
                NetOpcodes.MoveRequest => HandleMove(sessionId, payload),
                NetOpcodes.AttackRequest => HandleAttack(sessionId, payload),
                NetOpcodes.SkillListRequest => HandleSkillList(sessionId, payload),
                NetOpcodes.SkillCastRequest => HandleSkillCast(sessionId, payload),
                _ => CreateError(ProtocolErrorCode.UnsupportedOpcode, $"Unsupported opcode: {opcode}")
            };
        }

        private byte[] HandleLogin(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadLoginRequest(payload, out string username, out string password))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid login payload");

            bool ok = _app.AuthenticateClient(sessionId, username, password, out AuthenticationTokens tokens);

            if (!ok)
                return PacketContracts.CreateLoginResponse(false, string.Empty, "Invalid credentials");

            return PacketContracts.CreateLoginResponse(true, ToTokenBundle(tokens), "Login successful");
        }

        private byte[] HandleRefreshToken(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadRefreshTokenRequest(payload, out string refreshToken))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid refresh payload");

            bool ok = _app.AuthenticateClientWithRefresh(sessionId, refreshToken, out AuthenticationTokens tokens);
            if (!ok)
            {
                return PacketContracts.CreateRefreshTokenResponse(
                    false,
                    new PacketContracts.TokenBundle(),
                    "Refresh failed");
            }

            return PacketContracts.CreateRefreshTokenResponse(true, ToTokenBundle(tokens), "Refresh successful");
        }

        private byte[] HandleHeartbeat(byte[] payload)
        {
            if (!PacketContracts.TryReadHeartbeatRequest(payload, out _))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid heartbeat payload");

            return PacketContracts.CreateHeartbeatResponse(DateTime.UtcNow.Ticks);
        }

        private static PacketContracts.TokenBundle ToTokenBundle(AuthenticationTokens tokens)
        {
            return new PacketContracts.TokenBundle
            {
                AccessToken = tokens?.AccessToken ?? string.Empty,
                AccessTokenExpiresAtUtcTicks = tokens?.AccessTokenExpiresAtUtc.Ticks ?? 0,
                RefreshToken = tokens?.RefreshToken ?? string.Empty,
                RefreshTokenExpiresAtUtcTicks = tokens?.RefreshTokenExpiresAtUtc.Ticks ?? 0
            };
        }

        private byte[] HandleMove(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadMoveRequest(payload, out float x, out float y, out float z))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid move payload");

            bool moved = _app.TryMoveCharacter(sessionId, x, y, z);
            
            if (moved && _app.TryResolvePlayer(sessionId, out var player))
            {
                // Broadcast position update to other players on same map
                BroadcastPlayerMove(player.Id, x, y, z);
            }

            return PacketContracts.CreateMoveResponse(moved, x, y, z, moved ? "Move applied" : "Move rejected");
        }

        private void BroadcastPlayerMove(int playerId, float x, float y, float z)
        {
            if (!_app.WorldManager.TryGetMap(1, out var map)) return;
            
            byte[] broadcastPacket = PacketContracts.CreateMoveSnapshot(playerId, x, y, z);
            var otherPlayers = map.GetEntities()
                .OfType<PlayerEntity>()
                .Where(p => p.Id != playerId)
                .ToArray();

            foreach (var otherPlayer in otherPlayers)
            {
                // Find session ID for this player and send packet
                // This would require SessionManager to have a reverse lookup
                // For MVP, this is logged for implementation by gateway layer
            }
        }

        private byte[] HandleAttack(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadAttackRequest(payload, out int targetId))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid attack payload");

            if (!_app.TryResolvePlayer(sessionId, out var player))
                return CreateError(ProtocolErrorCode.NotFound, MessagePlayerNotFound);

            bool attackStarted = _app.TryStartAutoAttack(player.Id, targetId);
            if (!attackStarted)
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid target or out of range");

            return PacketContracts.CreateErrorResponse("Attack started");
        }

        private byte[] HandleSkillList(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadSkillListRequest(payload, out string accessToken))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid skill list payload");

            if (!_app.TryResolvePlayer(sessionId, out var player))
                return CreateError(ProtocolErrorCode.NotFound, MessagePlayerNotFound);

            // Get available skills for player's level
            var allSkills = _app.SkillSystem.GetAvailableSkills(player.Level);
            
            var skillInfoList = new List<PacketContracts.SkillInfo>();
            foreach (var skill in allSkills)
            {
                skillInfoList.Add(new PacketContracts.SkillInfo
                {
                    Id = skill.Id,
                    Name = skill.Name,
                    Description = skill.Description,
                    ManaCost = skill.ManaCost,
                    CooldownSeconds = skill.CooldownSeconds,
                    CastRange = skill.CastRange,
                    CastTypeIndex = (int)skill.CastType
                });
            }

            return PacketContracts.CreateSkillListResponse(skillInfoList);
        }

        private byte[] HandleSkillCast(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadSkillCastRequest(payload, out int skillId, out int targetId))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid skill payload");

            bool cast = _app.TryCastSkill(sessionId, skillId, targetId, out int damage);
            return PacketContracts.CreateSkillCastResponse(cast, targetId, damage, cast ? "Skill cast applied" : "Skill cast rejected");
        }

        private byte[] HandleListCharacters(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadListCharactersRequest(payload, out string accessToken))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid list characters payload");

            if (!_app.AuthService.TryValidateAccessToken(accessToken, out AccessTokenPrincipal principal))
                return CreateError(ProtocolErrorCode.Unauthorized, "Invalid or expired access token");

            IReadOnlyList<CharacterSummary> characters = _app.ListCharactersByAccountId(principal.AccountId);
            return PacketContracts.CreateListCharactersResponse(characters);
        }

        private byte[] HandleCreateCharacter(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadCreateCharacterRequest(payload, out string characterName, out string characterClass))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid create character payload");

            if (!_app.SessionManager.TryGet(sessionId, out var connection))
                return CreateError(ProtocolErrorCode.NotFound, "Session not found");

            if (!connection.IsAuthenticated || !connection.CharacterId.HasValue)
                return CreateError(ProtocolErrorCode.Unauthorized, MessageNotAuthenticated);

            int accountId = (int)(connection.CharacterId.Value / 1000);

            if (!_app.TryCreateCharacter(accountId, characterName, characterClass, out int newCharacterId))
                return PacketContracts.CreateCreateCharacterResponse(false, 0, "Failed to create character. Check name/class or max character limit.");

            return PacketContracts.CreateCreateCharacterResponse(true, newCharacterId, "Character created successfully");
        }

        private byte[] HandleDeleteCharacter(Guid sessionId, byte[] payload)
        {
            if (!PacketContracts.TryReadDeleteCharacterRequest(payload, out int characterId))
                return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid delete character payload");

            if (!_app.SessionManager.TryGet(sessionId, out var connection))
                return CreateError(ProtocolErrorCode.NotFound, "Session not found");

            if (!connection.IsAuthenticated || !connection.CharacterId.HasValue)
                return CreateError(ProtocolErrorCode.Unauthorized, MessageNotAuthenticated);

            int accountId = (int)(connection.CharacterId.Value / 1000);

            if (!_app.TryDeleteCharacter(accountId, characterId))
                return PacketContracts.CreateDeleteCharacterResponse(false, "Failed to delete character");

            return PacketContracts.CreateDeleteCharacterResponse(true, "Character deleted successfully");
        }

        private byte[] HandleSelectCharacter(Guid sessionId, byte[] payload)
        {
           if (!PacketContracts.TryReadSelectCharacterRequest(payload, out int characterId))
            return CreateError(ProtocolErrorCode.ValidationFailed, "Invalid select character payload");

            if (!_app.TrySelectCharacter(sessionId, characterId, out int selectedCharacterId))
                return PacketContracts.CreateSelectCharacterResponse(false, 0, "Failed to select character");

            return PacketContracts.CreateSelectCharacterResponse(true, selectedCharacterId, "Character selected successfully");
        }

        private static byte[] CreateError(ProtocolErrorCode code, string message)
        {
            return PacketContracts.CreateErrorResponse(ProtocolError.Create(code, message));
        }
    }
}
