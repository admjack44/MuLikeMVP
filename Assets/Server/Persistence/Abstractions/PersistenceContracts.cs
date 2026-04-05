using System;
using System.Collections.Generic;

namespace MuLike.Server.Persistence.Abstractions
{
    public sealed class AccountPersistenceModel
    {
        public int AccountId { get; set; }
        public string Username { get; set; }
        public string AccountName { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class CharacterPersistenceModel
    {
        public int CharacterId { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; }
        public string Class { get; set; }
        public int Level { get; set; }
        public int MapId { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public DateTime? LastLogoutUtc { get; set; }
    }

    public sealed class InventoryItemPersistenceModel
    {
        public int CharacterId { get; set; }
        public int SlotIndex { get; set; }
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public int EnhancementLevel { get; set; }
        public int ExcellentFlags { get; set; }
        public string SocketData { get; set; }
        public int SellValue { get; set; }
    }

    public sealed class EquipmentSlotPersistenceModel
    {
        public int CharacterId { get; set; }
        public string SlotName { get; set; }
        public long ItemInstanceId { get; set; }
        public int ItemId { get; set; }
        public int EnhancementLevel { get; set; }
        public int ExcellentFlags { get; set; }
        public string SocketData { get; set; }
        public int SellValue { get; set; }
    }

    public sealed class SkillLoadoutPersistenceModel
    {
        public int CharacterId { get; set; }
        public int SlotIndex { get; set; }
        public int SkillId { get; set; }
    }

    public sealed class PetPersistenceModel
    {
        public int CharacterId { get; set; }
        public int PetId { get; set; }
        public int Level { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class MailCurrencyPersistenceModel
    {
        public int CharacterId { get; set; }
        public long Zen { get; set; }
        public int Gems { get; set; }
        public int Bless { get; set; }
        public int Soul { get; set; }
    }

    public sealed class CharacterAggregatePersistenceModel
    {
        public CharacterPersistenceModel Character { get; set; }
        public IReadOnlyList<InventoryItemPersistenceModel> InventoryItems { get; set; }
        public IReadOnlyList<EquipmentSlotPersistenceModel> EquipmentSlots { get; set; }
        public IReadOnlyList<SkillLoadoutPersistenceModel> SkillLoadout { get; set; }
        public PetPersistenceModel Pet { get; set; }
        public MailCurrencyPersistenceModel MailCurrency { get; set; }
    }

    public interface IAccountPersistenceRepository
    {
        bool TryGetByUsername(string username, out AccountPersistenceModel account);
        bool TryGetByAccountId(int accountId, out AccountPersistenceModel account);
        void Upsert(AccountPersistenceModel account);
    }

    public interface ICharacterPersistenceRepository
    {
        bool TryGetByAccountId(int accountId, out CharacterPersistenceModel character);
        IReadOnlyList<CharacterPersistenceModel> LoadByAccountId(int accountId);
        bool TryGetByCharacterId(int characterId, out CharacterPersistenceModel character);
        void Upsert(CharacterPersistenceModel character);
        void Delete(int characterId);
        void MarkLogin(int characterId, DateTime loginUtc);
        void MarkLogout(int characterId, DateTime logoutUtc);
    }

    public interface IInventoryItemPersistenceRepository
    {
        IReadOnlyList<InventoryItemPersistenceModel> LoadByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<InventoryItemPersistenceModel> items);
    }

    public interface IEquipmentSlotPersistenceRepository
    {
        IReadOnlyList<EquipmentSlotPersistenceModel> LoadByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<EquipmentSlotPersistenceModel> slots);
    }

    public interface ISkillLoadoutPersistenceRepository
    {
        IReadOnlyList<SkillLoadoutPersistenceModel> LoadByCharacterId(int characterId);
        void ReplaceForCharacter(int characterId, IReadOnlyList<SkillLoadoutPersistenceModel> skills);
    }

    public interface IPetPersistenceRepository
    {
        bool TryGetByCharacterId(int characterId, out PetPersistenceModel pet);
        void Upsert(PetPersistenceModel pet);
    }

    public interface IMailCurrencyPersistenceRepository
    {
        bool TryGetByCharacterId(int characterId, out MailCurrencyPersistenceModel wallet);
        void Upsert(MailCurrencyPersistenceModel wallet);
    }

    public interface IServerUnitOfWork : IDisposable
    {
        IAccountPersistenceRepository Accounts { get; }
        ICharacterPersistenceRepository Characters { get; }
        IInventoryItemPersistenceRepository InventoryItems { get; }
        IEquipmentSlotPersistenceRepository EquipmentSlots { get; }
        ISkillLoadoutPersistenceRepository SkillLoadouts { get; }
        IPetPersistenceRepository Pets { get; }
        IMailCurrencyPersistenceRepository MailCurrencies { get; }

        void Commit();
        void Rollback();
    }

    public interface IServerUnitOfWorkFactory
    {
        IServerUnitOfWork Create();
    }
}
