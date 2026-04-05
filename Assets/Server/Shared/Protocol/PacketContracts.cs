using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MuLike.Shared.Protocol
{
    public sealed class CharacterSummary
    {
        public int CharacterId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Class { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    public sealed class SnapshotEntityData
    {
        public int EntityId { get; set; }
        public byte EntityType { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotationY { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public bool IsAlive { get; set; }
        public string DisplayName { get; set; }
        public int OwnerEntityId { get; set; }
    }

    public sealed class SnapshotData
    {
        public uint SequenceNumber { get; set; }
        public long TimestampMs { get; set; }
        public List<SnapshotEntityData> Entities { get; set; }
    }

    public static class PacketContracts
    {
        public sealed class TokenBundle
        {
            public string AccessToken { get; set; }
            public long AccessTokenExpiresAtUtcTicks { get; set; }
            public string RefreshToken { get; set; }
            public long RefreshTokenExpiresAtUtcTicks { get; set; }
        }

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
            return CreateLoginResponse(
                success,
                new TokenBundle
                {
                    AccessToken = token ?? string.Empty,
                    AccessTokenExpiresAtUtcTicks = 0,
                    RefreshToken = string.Empty,
                    RefreshTokenExpiresAtUtcTicks = 0
                },
                message);
        }

        public static byte[] CreateLoginResponse(bool success, TokenBundle tokens, string message)
        {
            return PacketCodec.Encode(NetOpcodes.LoginResponse, writer =>
            {
                writer.Write(success);
                PacketCodec.WriteString(writer, tokens?.AccessToken ?? string.Empty);
                writer.Write(tokens?.AccessTokenExpiresAtUtcTicks ?? 0);
                PacketCodec.WriteString(writer, tokens?.RefreshToken ?? string.Empty);
                writer.Write(tokens?.RefreshTokenExpiresAtUtcTicks ?? 0);
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
                _ = reader.ReadInt64();
                _ = PacketCodec.ReadString(reader);
                _ = reader.ReadInt64();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadLoginResponse(
            byte[] payload,
            out bool success,
            out TokenBundle tokens,
            out string message)
        {
            success = false;
            tokens = null;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                tokens = new TokenBundle
                {
                    AccessToken = PacketCodec.ReadString(reader),
                    AccessTokenExpiresAtUtcTicks = reader.ReadInt64(),
                    RefreshToken = PacketCodec.ReadString(reader),
                    RefreshTokenExpiresAtUtcTicks = reader.ReadInt64()
                };
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateRefreshTokenRequest(string refreshToken)
        {
            return PacketCodec.Encode(NetOpcodes.RefreshTokenRequest, writer =>
            {
                PacketCodec.WriteString(writer, refreshToken ?? string.Empty);
            });
        }

        public static bool TryReadRefreshTokenRequest(byte[] payload, out string refreshToken)
        {
            refreshToken = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                refreshToken = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateRefreshTokenResponse(bool success, TokenBundle tokens, string message)
        {
            return PacketCodec.Encode(NetOpcodes.RefreshTokenResponse, writer =>
            {
                writer.Write(success);
                PacketCodec.WriteString(writer, tokens?.AccessToken ?? string.Empty);
                writer.Write(tokens?.AccessTokenExpiresAtUtcTicks ?? 0);
                PacketCodec.WriteString(writer, tokens?.RefreshToken ?? string.Empty);
                writer.Write(tokens?.RefreshTokenExpiresAtUtcTicks ?? 0);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadRefreshTokenResponse(byte[] payload, out bool success, out TokenBundle tokens, out string message)
        {
            success = false;
            tokens = null;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                tokens = new TokenBundle
                {
                    AccessToken = PacketCodec.ReadString(reader),
                    AccessTokenExpiresAtUtcTicks = reader.ReadInt64(),
                    RefreshToken = PacketCodec.ReadString(reader),
                    RefreshTokenExpiresAtUtcTicks = reader.ReadInt64()
                };
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateHeartbeatRequest(long clientUtcTicks)
        {
            return PacketCodec.Encode(NetOpcodes.HeartbeatRequest, writer =>
            {
                writer.Write(clientUtcTicks);
            });
        }

        public static bool TryReadHeartbeatRequest(byte[] payload, out long clientUtcTicks)
        {
            clientUtcTicks = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                clientUtcTicks = reader.ReadInt64();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateHeartbeatResponse(long serverUtcTicks)
        {
            return PacketCodec.Encode(NetOpcodes.HeartbeatResponse, writer =>
            {
                writer.Write(serverUtcTicks);
            });
        }

        public static bool TryReadHeartbeatResponse(byte[] payload, out long serverUtcTicks)
        {
            serverUtcTicks = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                serverUtcTicks = reader.ReadInt64();
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

        public static byte[] CreateMoveSnapshot(int entityId, float x, float y, float z)
        {
            return PacketCodec.Encode(NetOpcodes.MoveSnapshot, writer =>
            {
                writer.Write(entityId);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
            });
        }

        public static bool TryReadMoveSnapshot(byte[] payload, out int entityId, out float x, out float y, out float z)
        {
            entityId = 0;
            x = 0f;
            y = 0f;
            z = 0f;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                entityId = reader.ReadInt32();
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

        public static byte[] CreateSkillListRequest(string accessToken)
        {
            return PacketCodec.Encode(NetOpcodes.SkillListRequest, writer =>
            {
                PacketCodec.WriteString(writer, accessToken ?? string.Empty);
            });
        }

        public static bool TryReadSkillListRequest(byte[] payload, out string accessToken)
        {
            accessToken = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                accessToken = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public sealed class SkillInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int ManaCost { get; set; }
            public float CooldownSeconds { get; set; }
            public float CastRange { get; set; }
            public int CastTypeIndex { get; set; } // 0=SingleTarget, 1=Self, 2=Area
        }

        public static byte[] CreateSkillListResponse(List<SkillInfo> skills)
        {
            return PacketCodec.Encode(NetOpcodes.SkillListResponse, writer =>
            {
                int count = skills?.Count ?? 0;
                writer.Write(count);
                if (count > 0)
                {
                    foreach (var skill in skills)
                    {
                        writer.Write(skill.Id);
                        PacketCodec.WriteString(writer, skill.Name);
                        PacketCodec.WriteString(writer, skill.Description);
                        writer.Write(skill.ManaCost);
                        writer.Write(skill.CooldownSeconds);
                        writer.Write(skill.CastRange);
                        writer.Write(skill.CastTypeIndex);
                    }
                }
            });
        }

        public static bool TryReadSkillListResponse(byte[] payload, out List<SkillInfo> skills)
        {
            skills = new List<SkillInfo>();
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var skill = new SkillInfo
                    {
                        Id = reader.ReadInt32(),
                        Name = PacketCodec.ReadString(reader),
                        Description = PacketCodec.ReadString(reader),
                        ManaCost = reader.ReadInt32(),
                        CooldownSeconds = reader.ReadSingle(),
                        CastRange = reader.ReadSingle(),
                        CastTypeIndex = reader.ReadInt32()
                    };
                    skills.Add(skill);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateAttackRequest(int targetId)
        {
            return PacketCodec.Encode(NetOpcodes.AttackRequest, writer =>
            {
                writer.Write(targetId);
            });
        }

        public static bool TryReadAttackRequest(byte[] payload, out int targetId)
        {
            targetId = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                targetId = reader.ReadInt32();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateAttackResponse(int targetId, bool hitSuccess, int damage, bool isCritical)
        {
            return PacketCodec.Encode(NetOpcodes.AttackResponse, writer =>
            {
                writer.Write(targetId);
                writer.Write(hitSuccess);
                writer.Write(damage);
                writer.Write(isCritical);
            });
        }

        public static bool TryReadAttackResponse(byte[] payload, out int targetId, out bool hitSuccess, out int damage, out bool isCritical)
        {
            targetId = 0;
            hitSuccess = false;
            damage = 0;
            isCritical = false;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                targetId = reader.ReadInt32();
                hitSuccess = reader.ReadBoolean();
                damage = reader.ReadInt32();
                isCritical = reader.ReadBoolean();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateDeathNotification(int entityId)
        {
            return PacketCodec.Encode(NetOpcodes.DeathNotification, writer =>
            {
                writer.Write(entityId);
            });
        }

        public static bool TryReadDeathNotification(byte[] payload, out int entityId)
        {
            entityId = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                entityId = reader.ReadInt32();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateRespawnNotification(int entityId, float x, float y, float z)
        {
            return PacketCodec.Encode(NetOpcodes.RespawnNotification, writer =>
            {
                writer.Write(entityId);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
            });
        }

        public static bool TryReadRespawnNotification(byte[] payload, out int entityId, out float x, out float y, out float z)
        {
            entityId = 0;
            x = 0f;
            y = 0f;
            z = 0f;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                entityId = reader.ReadInt32();
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

        public static byte[] CreateErrorResponse(string message)
        {
            return CreateErrorResponse(ProtocolError.Create(ProtocolErrorCode.Unknown, message ?? "Unknown error"));
        }

        public static byte[] CreateErrorResponse(ProtocolError error, uint requestId = 0)
        {
            return PacketEnvelopeCodec.Encode(NetOpcodes.ErrorResponse, requestId, error, null);
        }

        public static bool TryReadErrorResponse(byte[] payload, out ProtocolError error, out uint requestId)
        {
            error = null;
            requestId = 0;

            if (PacketEnvelopeCodec.TryDecodeEnvelopePayload(NetOpcodes.ErrorResponse, payload, out PacketEnvelope envelope))
            {
                requestId = envelope.RequestId;
                error = envelope.Error ?? ProtocolError.Create(ProtocolErrorCode.Unknown, "Unknown error");
                return true;
            }

            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                string legacyMessage = PacketCodec.ReadString(reader);
                error = ProtocolError.Create(ProtocolErrorCode.Unknown, legacyMessage);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Character Selection Flow

        public static byte[] CreateListCharactersRequest(string accessToken)
        {
            return PacketCodec.Encode(NetOpcodes.ListCharactersRequest, writer =>
            {
                PacketCodec.WriteString(writer, accessToken ?? string.Empty);
            });
        }

        public static bool TryReadListCharactersRequest(byte[] payload, out string accessToken)
        {
            accessToken = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                accessToken = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateListCharactersResponse(IReadOnlyList<CharacterSummary> characters)
        {
            return PacketCodec.Encode(NetOpcodes.ListCharactersResponse, writer =>
            {
                int count = characters?.Count ?? 0;
                writer.Write(count);
                if (count > 0)
                {
                    foreach (CharacterSummary character in characters)
                    {
                        writer.Write(character.CharacterId);
                        PacketCodec.WriteString(writer, character.Name ?? string.Empty);
                        writer.Write(character.Level);
                        PacketCodec.WriteString(writer, character.Class ?? string.Empty);
                        long ticks = character.LastLoginUtc?.Ticks ?? 0;
                        writer.Write(ticks);
                    }
                }
            });
        }

        public static bool TryReadListCharactersResponse(byte[] payload, out List<CharacterSummary> characters)
        {
            characters = new List<CharacterSummary>();
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int characterId = reader.ReadInt32();
                    string name = PacketCodec.ReadString(reader);
                    int level = reader.ReadInt32();
                    string characterClass = PacketCodec.ReadString(reader);
                    long ticks = reader.ReadInt64();
                    DateTime? lastLogin = ticks > 0 ? new DateTime(ticks) : null;
                    characters.Add(new CharacterSummary
                    {
                        CharacterId = characterId,
                        Name = name,
                        Level = level,
                        Class = characterClass,
                        LastLoginUtc = lastLogin
                    });
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateCreateCharacterRequest(string characterName, string characterClass)
        {
            return PacketCodec.Encode(NetOpcodes.CreateCharacterRequest, writer =>
            {
                PacketCodec.WriteString(writer, characterName ?? string.Empty);
                PacketCodec.WriteString(writer, characterClass ?? string.Empty);
            });
        }

        public static bool TryReadCreateCharacterRequest(byte[] payload, out string characterName, out string characterClass)
        {
            characterName = string.Empty;
            characterClass = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                characterName = PacketCodec.ReadString(reader);
                characterClass = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateCreateCharacterResponse(bool success, int characterId, string message)
        {
            return PacketCodec.Encode(NetOpcodes.CreateCharacterResponse, writer =>
            {
                writer.Write(success);
                writer.Write(characterId);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadCreateCharacterResponse(byte[] payload, out bool success, out int characterId, out string message)
        {
            success = false;
            characterId = 0;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                characterId = reader.ReadInt32();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateDeleteCharacterRequest(int characterId)
        {
            return PacketCodec.Encode(NetOpcodes.DeleteCharacterRequest, writer =>
            {
                writer.Write(characterId);
            });
        }

        public static bool TryReadDeleteCharacterRequest(byte[] payload, out int characterId)
        {
            characterId = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                characterId = reader.ReadInt32();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateDeleteCharacterResponse(bool success, string message)
        {
            return PacketCodec.Encode(NetOpcodes.DeleteCharacterResponse, writer =>
            {
                writer.Write(success);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadDeleteCharacterResponse(byte[] payload, out bool success, out string message)
        {
            success = false;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateSelectCharacterRequest(int characterId)
        {
            return PacketCodec.Encode(NetOpcodes.SelectCharacterRequest, writer =>
            {
                writer.Write(characterId);
            });
        }

        public static bool TryReadSelectCharacterRequest(byte[] payload, out int characterId)
        {
            characterId = 0;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                characterId = reader.ReadInt32();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] CreateSelectCharacterResponse(bool success, int characterId, string message)
        {
            return PacketCodec.Encode(NetOpcodes.SelectCharacterResponse, writer =>
            {
                writer.Write(success);
                writer.Write(characterId);
                PacketCodec.WriteString(writer, message ?? string.Empty);
            });
        }

        public static bool TryReadSelectCharacterResponse(byte[] payload, out bool success, out int characterId, out string message)
        {
            success = false;
            characterId = 0;
            message = string.Empty;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);
                success = reader.ReadBoolean();
                characterId = reader.ReadInt32();
                message = PacketCodec.ReadString(reader);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Snapshot Replication

        public static byte[] CreateSnapshotData(SnapshotData snapshot, bool isFullSnapshot)
        {
            ushort opcode = isFullSnapshot ? NetOpcodes.FullSnapshot : NetOpcodes.DeltaSnapshot;
            return PacketCodec.Encode(opcode, writer =>
            {
                writer.Write(snapshot.SequenceNumber);
                writer.Write(snapshot.TimestampMs);

                int count = snapshot.Entities?.Count ?? 0;
                writer.Write(count);

                if (count > 0)
                {
                    foreach (SnapshotEntityData entity in snapshot.Entities)
                    {
                        writer.Write(entity.EntityId);
                        writer.Write(entity.EntityType);
                        writer.Write(entity.PosX);
                        writer.Write(entity.PosY);
                        writer.Write(entity.PosZ);
                        writer.Write(entity.RotationY);
                        writer.Write(entity.HpCurrent);
                        writer.Write(entity.HpMax);
                        writer.Write(entity.IsAlive);
                        PacketCodec.WriteString(writer, entity.DisplayName ?? string.Empty);
                        writer.Write(entity.OwnerEntityId);
                    }
                }
            });
        }

        public static bool TryReadSnapshotData(byte[] payload, out SnapshotData snapshot)
        {
            snapshot = null;
            try
            {
                using var ms = new MemoryStream(payload);
                using var reader = new BinaryReader(ms);

                uint sequenceNumber = reader.ReadUInt32();
                long timestampMs = reader.ReadInt64();
                int count = reader.ReadInt32();

                var entities = new List<SnapshotEntityData>(count);
                for (int i = 0; i < count; i++)
                {
                    int entityId = reader.ReadInt32();
                    byte entityType = reader.ReadByte();
                    float posX = reader.ReadSingle();
                    float posY = reader.ReadSingle();
                    float posZ = reader.ReadSingle();
                    float rotationY = reader.ReadSingle();
                    int hpCurrent = reader.ReadInt32();
                    int hpMax = reader.ReadInt32();
                    bool isAlive = reader.ReadBoolean();
                    string displayName = PacketCodec.ReadString(reader);
                    int ownerEntityId = reader.ReadInt32();

                    entities.Add(new SnapshotEntityData
                    {
                        EntityId = entityId,
                        EntityType = entityType,
                        PosX = posX,
                        PosY = posY,
                        PosZ = posZ,
                        RotationY = rotationY,
                        HpCurrent = hpCurrent,
                        HpMax = hpMax,
                        IsAlive = isAlive,
                        DisplayName = displayName,
                        OwnerEntityId = ownerEntityId
                    });
                }

                snapshot = new SnapshotData
                {
                    SequenceNumber = sequenceNumber,
                    TimestampMs = timestampMs,
                    Entities = entities
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
