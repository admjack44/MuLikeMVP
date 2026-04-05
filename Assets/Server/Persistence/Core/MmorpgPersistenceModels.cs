using System;
using System.Collections.Generic;

namespace MuLike.Server.Persistence.Core
{
    public sealed class AccountData
    {
        public int AccountId { get; set; }
        public string Username { get; set; }
        public string AccountName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class WorldPositionData
    {
        public int MapId { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class CharacterData
    {
        public int CharacterId { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; }
        public string Class { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public bool IsSoftDeleted { get; set; }
        public DateTime? SoftDeletedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginUtc { get; set; }
        public DateTime? LastLogoutUtc { get; set; }
        public WorldPositionData WorldPosition { get; set; } = new WorldPositionData();
    }

    public sealed class CharacterStatsData
    {
        public int CharacterId { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public int MpCurrent { get; set; }
        public int MpMax { get; set; }
        public int Strength { get; set; }
        public int Agility { get; set; }
        public int Vitality { get; set; }
        public int Energy { get; set; }
        public int Leadership { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class InventoryItemData
    {
        public int CharacterId { get; set; }
        public int SlotIndex { get; set; }
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int EnhancementLevel { get; set; }
        public int ExcellentFlags { get; set; }
        public int SellValue { get; set; }
        public int[] Sockets { get; set; } = new[] { -1, -1, -1, -1, -1 };
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class EquippedItemData
    {
        public int CharacterId { get; set; }
        public string SlotName { get; set; }
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public int EnhancementLevel { get; set; }
        public int ExcellentFlags { get; set; }
        public int SellValue { get; set; }
        public int[] Sockets { get; set; } = new[] { -1, -1, -1, -1, -1 };
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class SkillLoadoutSlotData
    {
        public int CharacterId { get; set; }
        public int SlotIndex { get; set; }
        public int SkillId { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class SessionData
    {
        public Guid SessionId { get; set; }
        public int AccountId { get; set; }
        public int? CharacterId { get; set; }
        public bool IsAuthenticated { get; set; }
        public DateTime ConnectedAtUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public string RemoteAddress { get; set; }
        public string RefreshTokenHash { get; set; }
    }

    public sealed class MailRewardData
    {
        public long RewardId { get; set; }
        public int AccountId { get; set; }
        public int? CharacterId { get; set; }
        public string RewardType { get; set; }
        public string PayloadJson { get; set; }
        public bool IsClaimed { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ClaimedAtUtc { get; set; }
    }

    public sealed class CharacterPersistenceAggregate
    {
        public CharacterData Character { get; set; }
        public CharacterStatsData Stats { get; set; }
        public IReadOnlyList<InventoryItemData> Inventory { get; set; } = Array.Empty<InventoryItemData>();
        public IReadOnlyList<EquippedItemData> Equipped { get; set; } = Array.Empty<EquippedItemData>();
        public IReadOnlyList<SkillLoadoutSlotData> SkillLoadout { get; set; } = Array.Empty<SkillLoadoutSlotData>();
    }

    public sealed class PersistenceValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static PersistenceValidationResult Ok()
        {
            return new PersistenceValidationResult { Success = true, Message = string.Empty };
        }

        public static PersistenceValidationResult Fail(string message)
        {
            return new PersistenceValidationResult { Success = false, Message = message ?? "Validation failed." };
        }
    }
}
