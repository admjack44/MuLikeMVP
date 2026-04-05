using System;
using System.Collections.Generic;

namespace MuLike.Server.Persistence.Core
{
    public interface IAccountDataRepository
    {
        bool TryGetById(int accountId, out AccountData account);
        bool TryGetByUsername(string username, out AccountData account);
        void Upsert(AccountData account);
    }

    public interface ICharacterDataRepository
    {
        bool TryGetById(int characterId, out CharacterData character, bool includeSoftDeleted = false);
        IReadOnlyList<CharacterData> ListByAccountId(int accountId, bool includeSoftDeleted = false);
        void Upsert(CharacterData character);
        void SoftDelete(int accountId, int characterId, DateTime nowUtc);
        void MarkLogin(int characterId, DateTime nowUtc);
        void MarkLogout(int characterId, DateTime nowUtc);
    }

    public interface ICharacterStatsRepository
    {
        bool TryGetByCharacterId(int characterId, out CharacterStatsData stats);
        void Upsert(CharacterStatsData stats);
    }

    public interface IInventoryItemDataRepository
    {
        IReadOnlyList<InventoryItemData> ListByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<InventoryItemData> items);
    }

    public interface IEquippedItemDataRepository
    {
        IReadOnlyList<EquippedItemData> ListByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<EquippedItemData> items);
    }

    public interface ISkillLoadoutRepository
    {
        IReadOnlyList<SkillLoadoutSlotData> ListByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<SkillLoadoutSlotData> skills);
    }

    public interface ISessionDataRepository
    {
        bool TryGetBySessionId(Guid sessionId, out SessionData session);
        IReadOnlyList<SessionData> ListByAccountId(int accountId);
        void Upsert(SessionData session);
        void Delete(Guid sessionId);
    }

    public interface IMailRewardRepository
    {
        IReadOnlyList<MailRewardData> ListPendingForAccount(int accountId);
        IReadOnlyList<MailRewardData> ListPendingForCharacter(int characterId);
        void Upsert(MailRewardData reward);
    }

    public interface IItemOwnershipGuard
    {
        PersistenceValidationResult ValidateAndAssignOwnership(
            int characterId,
            IReadOnlyList<InventoryItemData> inventory,
            IReadOnlyList<EquippedItemData> equipped);
    }

    public interface IMmorpgPersistenceUnitOfWork : IDisposable
    {
        IAccountDataRepository Accounts { get; }
        ICharacterDataRepository Characters { get; }
        ICharacterStatsRepository CharacterStats { get; }
        IInventoryItemDataRepository InventoryItems { get; }
        IEquippedItemDataRepository EquippedItems { get; }
        ISkillLoadoutRepository SkillLoadouts { get; }
        ISessionDataRepository Sessions { get; }
        IMailRewardRepository MailRewards { get; }
        IItemOwnershipGuard ItemOwnershipGuard { get; }

        void Commit();
        void Rollback();
    }

    public interface IMmorpgPersistenceUnitOfWorkFactory
    {
        IMmorpgPersistenceUnitOfWork Create();
    }

    public interface IMmorpgPersistenceService
    {
        bool TryLoadCharacterForWorldEntry(int characterId, out CharacterPersistenceAggregate aggregate);
        PersistenceValidationResult SaveCharacterFromRuntime(CharacterPersistenceAggregate aggregate, DateTime nowUtc, bool markLogout);
        void SaveSession(SessionData session);
        void DeleteSession(Guid sessionId);
        bool TrySoftDeleteCharacter(int accountId, int characterId, DateTime nowUtc);
    }

    // These methods are transport/storage-agnostic and can be mapped to SQLite/PostgreSQL command batches.
    public interface ITransactionalCharacterWriter
    {
        PersistenceValidationResult SaveCharacterAggregateAtomic(CharacterPersistenceAggregate aggregate, DateTime nowUtc, bool markLogout);
        bool SoftDeleteCharacterAtomic(int accountId, int characterId, DateTime nowUtc);
    }
}
