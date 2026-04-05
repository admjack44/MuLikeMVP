using System;
using System.Collections.Generic;
using System.Linq;
using MuLike.Server.Persistence.Core;

namespace MuLike.Server.Persistence.InMemory
{
    public sealed class InMemoryMmorpgPersistenceStore
    {
        private readonly object _lock = new();

        private readonly Dictionary<int, AccountData> _accounts = new();
        private readonly Dictionary<int, CharacterData> _characters = new();
        private readonly Dictionary<int, CharacterStatsData> _stats = new();
        private readonly Dictionary<int, Dictionary<int, InventoryItemData>> _inventoryByCharacter = new();
        private readonly Dictionary<int, Dictionary<string, EquippedItemData>> _equippedByCharacter = new();
        private readonly Dictionary<int, Dictionary<int, SkillLoadoutSlotData>> _skillsByCharacter = new();
        private readonly Dictionary<Guid, SessionData> _sessions = new();
        private readonly Dictionary<long, MailRewardData> _mailRewards = new();

        // ItemInstanceId -> CharacterId owner map to block dupes across characters.
        private readonly Dictionary<long, int> _itemOwnership = new();

        public InMemoryMmorpgPersistenceStore()
        {
        }

        internal object SyncRoot => _lock;
        internal Dictionary<int, AccountData> Accounts => _accounts;
        internal Dictionary<int, CharacterData> Characters => _characters;
        internal Dictionary<int, CharacterStatsData> Stats => _stats;
        internal Dictionary<int, Dictionary<int, InventoryItemData>> InventoryByCharacter => _inventoryByCharacter;
        internal Dictionary<int, Dictionary<string, EquippedItemData>> EquippedByCharacter => _equippedByCharacter;
        internal Dictionary<int, Dictionary<int, SkillLoadoutSlotData>> SkillsByCharacter => _skillsByCharacter;
        internal Dictionary<Guid, SessionData> Sessions => _sessions;
        internal Dictionary<long, MailRewardData> MailRewards => _mailRewards;
        internal Dictionary<long, int> ItemOwnership => _itemOwnership;
    }

    public sealed class InMemoryMmorpgPersistenceUnitOfWorkFactory : IMmorpgPersistenceUnitOfWorkFactory
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryMmorpgPersistenceUnitOfWorkFactory(InMemoryMmorpgPersistenceStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IMmorpgPersistenceUnitOfWork Create()
        {
            return new InMemoryMmorpgPersistenceUnitOfWork(_store);
        }
    }

    public sealed class InMemoryMmorpgPersistenceUnitOfWork : IMmorpgPersistenceUnitOfWork
    {
        private readonly InMemoryMmorpgPersistenceStore _store;
        private readonly object _sync;
        private bool _completed;

        public InMemoryMmorpgPersistenceUnitOfWork(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
            _sync = store.SyncRoot;

            Accounts = new InMemoryAccountDataRepository(_store);
            Characters = new InMemoryCharacterDataRepository(_store);
            CharacterStats = new InMemoryCharacterStatsRepository(_store);
            InventoryItems = new InMemoryInventoryItemDataRepository(_store);
            EquippedItems = new InMemoryEquippedItemDataRepository(_store);
            SkillLoadouts = new InMemorySkillLoadoutRepository(_store);
            Sessions = new InMemorySessionDataRepository(_store);
            MailRewards = new InMemoryMailRewardRepository(_store);
            ItemOwnershipGuard = new InMemoryItemOwnershipGuard(_store);

            System.Threading.Monitor.Enter(_sync);
        }

        public IAccountDataRepository Accounts { get; }
        public ICharacterDataRepository Characters { get; }
        public ICharacterStatsRepository CharacterStats { get; }
        public IInventoryItemDataRepository InventoryItems { get; }
        public IEquippedItemDataRepository EquippedItems { get; }
        public ISkillLoadoutRepository SkillLoadouts { get; }
        public ISessionDataRepository Sessions { get; }
        public IMailRewardRepository MailRewards { get; }
        public IItemOwnershipGuard ItemOwnershipGuard { get; }

        public void Commit()
        {
            _completed = true;
        }

        public void Rollback()
        {
            // In-memory implementation applies changes directly under lock.
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
                Rollback();

            System.Threading.Monitor.Exit(_sync);
        }
    }

    internal sealed class InMemoryAccountDataRepository : IAccountDataRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryAccountDataRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public bool TryGetById(int accountId, out AccountData account)
        {
            if (_store.Accounts.TryGetValue(accountId, out AccountData raw))
            {
                account = Clone(raw);
                return true;
            }

            account = null;
            return false;
        }

        public bool TryGetByUsername(string username, out AccountData account)
        {
            foreach (KeyValuePair<int, AccountData> row in _store.Accounts)
            {
                if (!string.Equals(row.Value.Username, username, StringComparison.OrdinalIgnoreCase))
                    continue;

                account = Clone(row.Value);
                return true;
            }

            account = null;
            return false;
        }

        public void Upsert(AccountData account)
        {
            account.UpdatedAtUtc = DateTime.UtcNow;
            if (!_store.Accounts.ContainsKey(account.AccountId))
                account.CreatedAtUtc = account.UpdatedAtUtc;

            _store.Accounts[account.AccountId] = Clone(account);
        }

        private static AccountData Clone(AccountData value)
        {
            return new AccountData
            {
                AccountId = value.AccountId,
                Username = value.Username,
                AccountName = value.AccountName,
                IsActive = value.IsActive,
                CreatedAtUtc = value.CreatedAtUtc,
                UpdatedAtUtc = value.UpdatedAtUtc
            };
        }
    }

    internal sealed class InMemoryCharacterDataRepository : ICharacterDataRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryCharacterDataRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public bool TryGetById(int characterId, out CharacterData character, bool includeSoftDeleted = false)
        {
            if (_store.Characters.TryGetValue(characterId, out CharacterData raw))
            {
                if (!includeSoftDeleted && raw.IsSoftDeleted)
                {
                    character = null;
                    return false;
                }

                character = Clone(raw);
                return true;
            }

            character = null;
            return false;
        }

        public IReadOnlyList<CharacterData> ListByAccountId(int accountId, bool includeSoftDeleted = false)
        {
            var rows = new List<CharacterData>();
            foreach (KeyValuePair<int, CharacterData> pair in _store.Characters)
            {
                if (pair.Value.AccountId != accountId)
                    continue;

                if (!includeSoftDeleted && pair.Value.IsSoftDeleted)
                    continue;

                rows.Add(Clone(pair.Value));
            }

            rows.Sort((a, b) => a.CharacterId.CompareTo(b.CharacterId));
            return rows;
        }

        public void Upsert(CharacterData character)
        {
            character.UpdatedAtUtc = DateTime.UtcNow;
            if (!_store.Characters.ContainsKey(character.CharacterId))
                character.CreatedAtUtc = character.UpdatedAtUtc;

            _store.Characters[character.CharacterId] = Clone(character);
        }

        public void SoftDelete(int accountId, int characterId, DateTime nowUtc)
        {
            if (!_store.Characters.TryGetValue(characterId, out CharacterData row))
                return;

            if (row.AccountId != accountId)
                return;

            row.IsSoftDeleted = true;
            row.SoftDeletedAtUtc = nowUtc;
            row.UpdatedAtUtc = nowUtc;
            _store.Characters[characterId] = Clone(row);
        }

        public void MarkLogin(int characterId, DateTime nowUtc)
        {
            if (!_store.Characters.TryGetValue(characterId, out CharacterData row))
                return;

            row.LastLoginUtc = nowUtc;
            row.UpdatedAtUtc = nowUtc;
            _store.Characters[characterId] = Clone(row);
        }

        public void MarkLogout(int characterId, DateTime nowUtc)
        {
            if (!_store.Characters.TryGetValue(characterId, out CharacterData row))
                return;

            row.LastLogoutUtc = nowUtc;
            row.UpdatedAtUtc = nowUtc;
            _store.Characters[characterId] = Clone(row);
        }

        private static CharacterData Clone(CharacterData value)
        {
            return new CharacterData
            {
                CharacterId = value.CharacterId,
                AccountId = value.AccountId,
                Name = value.Name,
                Class = value.Class,
                Level = value.Level,
                Experience = value.Experience,
                IsSoftDeleted = value.IsSoftDeleted,
                SoftDeletedAtUtc = value.SoftDeletedAtUtc,
                CreatedAtUtc = value.CreatedAtUtc,
                UpdatedAtUtc = value.UpdatedAtUtc,
                LastLoginUtc = value.LastLoginUtc,
                LastLogoutUtc = value.LastLogoutUtc,
                WorldPosition = value.WorldPosition == null
                    ? new WorldPositionData()
                    : new WorldPositionData
                    {
                        MapId = value.WorldPosition.MapId,
                        X = value.WorldPosition.X,
                        Y = value.WorldPosition.Y,
                        Z = value.WorldPosition.Z
                    }
            };
        }
    }

    internal sealed class InMemoryCharacterStatsRepository : ICharacterStatsRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryCharacterStatsRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public bool TryGetByCharacterId(int characterId, out CharacterStatsData stats)
        {
            if (_store.Stats.TryGetValue(characterId, out CharacterStatsData raw))
            {
                stats = Clone(raw);
                return true;
            }

            stats = null;
            return false;
        }

        public void Upsert(CharacterStatsData stats)
        {
            stats.UpdatedAtUtc = DateTime.UtcNow;
            _store.Stats[stats.CharacterId] = Clone(stats);
        }

        private static CharacterStatsData Clone(CharacterStatsData value)
        {
            return new CharacterStatsData
            {
                CharacterId = value.CharacterId,
                HpCurrent = value.HpCurrent,
                HpMax = value.HpMax,
                MpCurrent = value.MpCurrent,
                MpMax = value.MpMax,
                Strength = value.Strength,
                Agility = value.Agility,
                Vitality = value.Vitality,
                Energy = value.Energy,
                Leadership = value.Leadership,
                UpdatedAtUtc = value.UpdatedAtUtc
            };
        }
    }

    internal sealed class InMemoryInventoryItemDataRepository : IInventoryItemDataRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryInventoryItemDataRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public IReadOnlyList<InventoryItemData> ListByCharacterId(int characterId)
        {
            if (!_store.InventoryByCharacter.TryGetValue(characterId, out Dictionary<int, InventoryItemData> rows))
                return Array.Empty<InventoryItemData>();

            var values = new List<InventoryItemData>(rows.Count);
            foreach (KeyValuePair<int, InventoryItemData> row in rows)
                values.Add(Clone(row.Value));

            values.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return values;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<InventoryItemData> items)
        {
            var map = new Dictionary<int, InventoryItemData>();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    InventoryItemData row = Clone(items[i]);
                    row.CharacterId = characterId;
                    map[row.SlotIndex] = row;
                }
            }

            _store.InventoryByCharacter[characterId] = map;
        }

        private static InventoryItemData Clone(InventoryItemData value)
        {
            return new InventoryItemData
            {
                CharacterId = value.CharacterId,
                SlotIndex = value.SlotIndex,
                ItemInstanceId = value.ItemInstanceId,
                ItemId = value.ItemId,
                Quantity = value.Quantity,
                EnhancementLevel = value.EnhancementLevel,
                ExcellentFlags = value.ExcellentFlags,
                SellValue = value.SellValue,
                Sockets = value.Sockets != null ? (int[])value.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 },
                UpdatedAtUtc = value.UpdatedAtUtc
            };
        }
    }

    internal sealed class InMemoryEquippedItemDataRepository : IEquippedItemDataRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryEquippedItemDataRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public IReadOnlyList<EquippedItemData> ListByCharacterId(int characterId)
        {
            if (!_store.EquippedByCharacter.TryGetValue(characterId, out Dictionary<string, EquippedItemData> rows))
                return Array.Empty<EquippedItemData>();

            var values = new List<EquippedItemData>(rows.Count);
            foreach (KeyValuePair<string, EquippedItemData> row in rows)
                values.Add(Clone(row.Value));

            values.Sort((a, b) => string.CompareOrdinal(a.SlotName, b.SlotName));
            return values;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<EquippedItemData> items)
        {
            var map = new Dictionary<string, EquippedItemData>(StringComparer.OrdinalIgnoreCase);
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    EquippedItemData row = Clone(items[i]);
                    row.CharacterId = characterId;
                    map[row.SlotName ?? string.Empty] = row;
                }
            }

            _store.EquippedByCharacter[characterId] = map;
        }

        private static EquippedItemData Clone(EquippedItemData value)
        {
            return new EquippedItemData
            {
                CharacterId = value.CharacterId,
                SlotName = value.SlotName,
                ItemInstanceId = value.ItemInstanceId,
                ItemId = value.ItemId,
                EnhancementLevel = value.EnhancementLevel,
                ExcellentFlags = value.ExcellentFlags,
                SellValue = value.SellValue,
                Sockets = value.Sockets != null ? (int[])value.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 },
                UpdatedAtUtc = value.UpdatedAtUtc
            };
        }
    }

    internal sealed class InMemorySkillLoadoutRepository : ISkillLoadoutRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemorySkillLoadoutRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public IReadOnlyList<SkillLoadoutSlotData> ListByCharacterId(int characterId)
        {
            if (!_store.SkillsByCharacter.TryGetValue(characterId, out Dictionary<int, SkillLoadoutSlotData> rows))
                return Array.Empty<SkillLoadoutSlotData>();

            var values = new List<SkillLoadoutSlotData>(rows.Count);
            foreach (KeyValuePair<int, SkillLoadoutSlotData> row in rows)
                values.Add(Clone(row.Value));

            values.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return values;
        }

        public void ReplaceForCharacter(int characterId, IReadOnlyList<SkillLoadoutSlotData> skills)
        {
            var map = new Dictionary<int, SkillLoadoutSlotData>();
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    SkillLoadoutSlotData row = Clone(skills[i]);
                    row.CharacterId = characterId;
                    map[row.SlotIndex] = row;
                }
            }

            _store.SkillsByCharacter[characterId] = map;
        }

        private static SkillLoadoutSlotData Clone(SkillLoadoutSlotData value)
        {
            return new SkillLoadoutSlotData
            {
                CharacterId = value.CharacterId,
                SlotIndex = value.SlotIndex,
                SkillId = value.SkillId,
                UpdatedAtUtc = value.UpdatedAtUtc
            };
        }
    }

    internal sealed class InMemorySessionDataRepository : ISessionDataRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemorySessionDataRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public bool TryGetBySessionId(Guid sessionId, out SessionData session)
        {
            if (_store.Sessions.TryGetValue(sessionId, out SessionData row))
            {
                session = Clone(row);
                return true;
            }

            session = null;
            return false;
        }

        public IReadOnlyList<SessionData> ListByAccountId(int accountId)
        {
            var rows = new List<SessionData>();
            foreach (KeyValuePair<Guid, SessionData> pair in _store.Sessions)
            {
                if (pair.Value.AccountId != accountId)
                    continue;

                rows.Add(Clone(pair.Value));
            }

            rows.Sort((a, b) => a.ConnectedAtUtc.CompareTo(b.ConnectedAtUtc));
            return rows;
        }

        public void Upsert(SessionData session)
        {
            _store.Sessions[session.SessionId] = Clone(session);
        }

        public void Delete(Guid sessionId)
        {
            _store.Sessions.Remove(sessionId);
        }

        private static SessionData Clone(SessionData value)
        {
            return new SessionData
            {
                SessionId = value.SessionId,
                AccountId = value.AccountId,
                CharacterId = value.CharacterId,
                IsAuthenticated = value.IsAuthenticated,
                ConnectedAtUtc = value.ConnectedAtUtc,
                LastSeenUtc = value.LastSeenUtc,
                RemoteAddress = value.RemoteAddress,
                RefreshTokenHash = value.RefreshTokenHash
            };
        }
    }

    internal sealed class InMemoryMailRewardRepository : IMailRewardRepository
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryMailRewardRepository(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public IReadOnlyList<MailRewardData> ListPendingForAccount(int accountId)
        {
            return _store.MailRewards.Values
                .Where(r => r.AccountId == accountId && !r.IsClaimed)
                .Select(Clone)
                .OrderBy(r => r.CreatedAtUtc)
                .ToArray();
        }

        public IReadOnlyList<MailRewardData> ListPendingForCharacter(int characterId)
        {
            return _store.MailRewards.Values
                .Where(r => r.CharacterId == characterId && !r.IsClaimed)
                .Select(Clone)
                .OrderBy(r => r.CreatedAtUtc)
                .ToArray();
        }

        public void Upsert(MailRewardData reward)
        {
            _store.MailRewards[reward.RewardId] = Clone(reward);
        }

        private static MailRewardData Clone(MailRewardData value)
        {
            return new MailRewardData
            {
                RewardId = value.RewardId,
                AccountId = value.AccountId,
                CharacterId = value.CharacterId,
                RewardType = value.RewardType,
                PayloadJson = value.PayloadJson,
                IsClaimed = value.IsClaimed,
                CreatedAtUtc = value.CreatedAtUtc,
                ClaimedAtUtc = value.ClaimedAtUtc
            };
        }
    }

    internal sealed class InMemoryItemOwnershipGuard : IItemOwnershipGuard
    {
        private readonly InMemoryMmorpgPersistenceStore _store;

        public InMemoryItemOwnershipGuard(InMemoryMmorpgPersistenceStore store)
        {
            _store = store;
        }

        public PersistenceValidationResult ValidateAndAssignOwnership(
            int characterId,
            IReadOnlyList<InventoryItemData> inventory,
            IReadOnlyList<EquippedItemData> equipped)
        {
            var localItemIds = new HashSet<long>();

            if (inventory != null)
            {
                for (int i = 0; i < inventory.Count; i++)
                {
                    InventoryItemData item = inventory[i];
                    if (item == null || item.ItemInstanceId <= 0)
                        continue;

                    if (!localItemIds.Add(item.ItemInstanceId))
                        return PersistenceValidationResult.Fail($"Duplicate item instance id within inventory: {item.ItemInstanceId}");
                }
            }

            if (equipped != null)
            {
                for (int i = 0; i < equipped.Count; i++)
                {
                    EquippedItemData item = equipped[i];
                    if (item == null || item.ItemInstanceId <= 0)
                        continue;

                    if (!localItemIds.Add(item.ItemInstanceId))
                        return PersistenceValidationResult.Fail($"Duplicate item instance id between inventory/equipment: {item.ItemInstanceId}");
                }
            }

            // Remove previous ownership entries for this character.
            var keysToRemove = new List<long>();
            foreach (KeyValuePair<long, int> pair in _store.ItemOwnership)
            {
                if (pair.Value == characterId)
                    keysToRemove.Add(pair.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
                _store.ItemOwnership.Remove(keysToRemove[i]);

            foreach (long itemInstanceId in localItemIds)
            {
                if (_store.ItemOwnership.TryGetValue(itemInstanceId, out int ownerCharacterId) && ownerCharacterId != characterId)
                {
                    return PersistenceValidationResult.Fail($"Item instance id {itemInstanceId} already belongs to character {ownerCharacterId}");
                }

                _store.ItemOwnership[itemInstanceId] = characterId;
            }

            return PersistenceValidationResult.Ok();
        }
    }

    public sealed class InMemoryMmorpgPersistenceService : IMmorpgPersistenceService, ITransactionalCharacterWriter
    {
        private readonly IMmorpgPersistenceUnitOfWorkFactory _uowFactory;

        public InMemoryMmorpgPersistenceService(IMmorpgPersistenceUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        }

        public bool TryLoadCharacterForWorldEntry(int characterId, out CharacterPersistenceAggregate aggregate)
        {
            aggregate = null;
            using IMmorpgPersistenceUnitOfWork uow = _uowFactory.Create();
            if (!uow.Characters.TryGetById(characterId, out CharacterData character, includeSoftDeleted: false))
            {
                uow.Commit();
                return false;
            }

            uow.CharacterStats.TryGetByCharacterId(characterId, out CharacterStatsData stats);

            aggregate = new CharacterPersistenceAggregate
            {
                Character = character,
                Stats = stats,
                Inventory = uow.InventoryItems.ListByCharacterId(characterId),
                Equipped = uow.EquippedItems.ListByCharacterId(characterId),
                SkillLoadout = uow.SkillLoadouts.ListByCharacterId(characterId)
            };

            uow.Commit();
            return true;
        }

        public PersistenceValidationResult SaveCharacterFromRuntime(CharacterPersistenceAggregate aggregate, DateTime nowUtc, bool markLogout)
        {
            return SaveCharacterAggregateAtomic(aggregate, nowUtc, markLogout);
        }

        public PersistenceValidationResult SaveCharacterAggregateAtomic(CharacterPersistenceAggregate aggregate, DateTime nowUtc, bool markLogout)
        {
            if (aggregate == null || aggregate.Character == null)
                return PersistenceValidationResult.Fail("Character aggregate is required.");

            using IMmorpgPersistenceUnitOfWork uow = _uowFactory.Create();

            PersistenceValidationResult ownership = uow.ItemOwnershipGuard.ValidateAndAssignOwnership(
                aggregate.Character.CharacterId,
                aggregate.Inventory,
                aggregate.Equipped);
            if (!ownership.Success)
            {
                uow.Rollback();
                return ownership;
            }

            CharacterData character = aggregate.Character;
            character.UpdatedAtUtc = nowUtc;
            if (markLogout)
                character.LastLogoutUtc = nowUtc;

            uow.Characters.Upsert(character);

            if (aggregate.Stats != null)
            {
                aggregate.Stats.UpdatedAtUtc = nowUtc;
                uow.CharacterStats.Upsert(aggregate.Stats);
            }

            uow.InventoryItems.ReplaceForCharacter(character.CharacterId, aggregate.Inventory);
            uow.EquippedItems.ReplaceForCharacter(character.CharacterId, aggregate.Equipped);
            uow.SkillLoadouts.ReplaceForCharacter(character.CharacterId, aggregate.SkillLoadout);

            uow.Commit();
            return PersistenceValidationResult.Ok();
        }

        public void SaveSession(SessionData session)
        {
            if (session == null)
                return;

            using IMmorpgPersistenceUnitOfWork uow = _uowFactory.Create();
            uow.Sessions.Upsert(session);
            uow.Commit();
        }

        public void DeleteSession(Guid sessionId)
        {
            using IMmorpgPersistenceUnitOfWork uow = _uowFactory.Create();
            uow.Sessions.Delete(sessionId);
            uow.Commit();
        }

        public bool TrySoftDeleteCharacter(int accountId, int characterId, DateTime nowUtc)
        {
            return SoftDeleteCharacterAtomic(accountId, characterId, nowUtc);
        }

        public bool SoftDeleteCharacterAtomic(int accountId, int characterId, DateTime nowUtc)
        {
            using IMmorpgPersistenceUnitOfWork uow = _uowFactory.Create();
            if (!uow.Characters.TryGetById(characterId, out CharacterData character, includeSoftDeleted: true))
            {
                uow.Commit();
                return false;
            }

            if (character.AccountId != accountId)
            {
                uow.Commit();
                return false;
            }

            uow.Characters.SoftDelete(accountId, characterId, nowUtc);
            uow.Commit();
            return true;
        }
    }
}
