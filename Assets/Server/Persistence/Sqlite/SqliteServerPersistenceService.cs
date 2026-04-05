using System;
using System.Collections.Generic;
using MuLike.Server.Auth;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.Repositories;
using MuLike.Server.Persistence.Abstractions;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Persistence.Sqlite
{
    public sealed class SqliteServerPersistenceService
    {
        private readonly IServerUnitOfWorkFactory _uowFactory;

        public SqliteServerPersistenceService(IServerUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        }

        public bool TryLoadCharacterAggregateByAccountId(int accountId, out CharacterAggregatePersistenceModel aggregate)
        {
            aggregate = null;

            using IServerUnitOfWork uow = _uowFactory.Create();
            if (!uow.Characters.TryGetByAccountId(accountId, out CharacterPersistenceModel character))
            {
                uow.Commit();
                return false;
            }

            aggregate = new CharacterAggregatePersistenceModel
            {
                Character = character,
                InventoryItems = uow.InventoryItems.LoadByCharacterId(character.CharacterId),
                EquipmentSlots = uow.EquipmentSlots.LoadByCharacterId(character.CharacterId),
                SkillLoadout = uow.SkillLoadouts.LoadByCharacterId(character.CharacterId),
                Pet = uow.Pets.TryGetByCharacterId(character.CharacterId, out PetPersistenceModel pet) ? pet : null,
                MailCurrency = uow.MailCurrencies.TryGetByCharacterId(character.CharacterId, out MailCurrencyPersistenceModel wallet) ? wallet : null
            };

            uow.Commit();
            return true;
        }

        public bool TryLoadCharacterAggregateByCharacterId(int characterId, out CharacterAggregatePersistenceModel aggregate)
        {
            aggregate = null;

            using IServerUnitOfWork uow = _uowFactory.Create();
            if (!uow.Characters.TryGetByCharacterId(characterId, out CharacterPersistenceModel character))
            {
                uow.Commit();
                return false;
            }

            aggregate = new CharacterAggregatePersistenceModel
            {
                Character = character,
                InventoryItems = uow.InventoryItems.LoadByCharacterId(character.CharacterId),
                EquipmentSlots = uow.EquipmentSlots.LoadByCharacterId(character.CharacterId),
                SkillLoadout = uow.SkillLoadouts.LoadByCharacterId(character.CharacterId),
                Pet = uow.Pets.TryGetByCharacterId(character.CharacterId, out PetPersistenceModel pet) ? pet : null,
                MailCurrency = uow.MailCurrencies.TryGetByCharacterId(character.CharacterId, out MailCurrencyPersistenceModel wallet) ? wallet : null
            };

            uow.Commit();
            return true;
        }

        public CharacterAggregatePersistenceModel CreateDefaultCharacterAggregate(int accountId, string name, string characterClass = "Warrior")
        {
            var spawnPos = GetSpawnPositionByClass(characterClass);
            return new CharacterAggregatePersistenceModel
            {
                Character = new CharacterPersistenceModel
                {
                    CharacterId = accountId,
                    AccountId = accountId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Player{accountId}" : name,
                    Class = characterClass ?? "Warrior",
                    Level = 1,
                    MapId = 1,
                    PosX = spawnPos.X,
                    PosY = spawnPos.Y,
                    PosZ = spawnPos.Z,
                    HpCurrent = 100,
                    HpMax = 100,
                    LastLoginUtc = DateTime.UtcNow,
                    LastLogoutUtc = null
                },
                InventoryItems = Array.Empty<InventoryItemPersistenceModel>(),
                EquipmentSlots = Array.Empty<EquipmentSlotPersistenceModel>(),
                SkillLoadout = Array.Empty<SkillLoadoutPersistenceModel>(),
                Pet = null,
                MailCurrency = new MailCurrencyPersistenceModel
                {
                    CharacterId = accountId,
                    Zen = 0,
                    Gems = 0,
                    Bless = 0,
                    Soul = 0
                }
            };
        }

        public IReadOnlyList<CharacterSummary> ListCharactersByAccountId(int accountId)
        {
            using IServerUnitOfWork uow = _uowFactory.Create();
            IReadOnlyList<CharacterPersistenceModel> characters = uow.Characters.LoadByAccountId(accountId);
            var summaries = new List<CharacterSummary>();
            foreach (CharacterPersistenceModel character in characters)
            {
                summaries.Add(new CharacterSummary
                {
                    CharacterId = character.CharacterId,
                    Name = character.Name,
                    Level = character.Level,
                    Class = character.Class,
                    LastLoginUtc = character.LastLoginUtc
                });
            }
            uow.Commit();
            return summaries;
        }

        public bool TryCreateCharacter(int accountId, string characterName, string characterClass, out int newCharacterId)
        {
            newCharacterId = 0;

            if (string.IsNullOrWhiteSpace(characterName) || characterName.Length > 20)
                return false;

            if (string.IsNullOrWhiteSpace(characterClass))
                characterClass = "Warrior";

            characterClass = NormalizeCharacterClass(characterClass);
            if (string.IsNullOrEmpty(characterClass))
                return false;

            using IServerUnitOfWork uow = _uowFactory.Create();
            IReadOnlyList<CharacterPersistenceModel> existing = uow.Characters.LoadByAccountId(accountId);
            if (existing.Count >= 5)
            {
                uow.Commit();
                return false;
            }

            newCharacterId = (accountId * 1000) + existing.Count + 1;
            var spawnPos = GetSpawnPositionByClass(characterClass);
            var newCharacter = new CharacterPersistenceModel
            {
                CharacterId = newCharacterId,
                AccountId = accountId,
                Name = characterName,
                Class = characterClass,
                Level = 1,
                MapId = 1,
                PosX = spawnPos.X,
                PosY = spawnPos.Y,
                PosZ = spawnPos.Z,
                HpCurrent = 100,
                HpMax = 100,
                LastLoginUtc = null,
                LastLogoutUtc = null
            };

            uow.Characters.Upsert(newCharacter);
            uow.MailCurrencies.Upsert(new MailCurrencyPersistenceModel
            {
                CharacterId = newCharacterId,
                Zen = 0,
                Gems = 0,
                Bless = 0,
                Soul = 0
            });
            uow.Commit();
            return true;
        }

        public bool TryDeleteCharacter(int accountId, int characterId)
        {
            using IServerUnitOfWork uow = _uowFactory.Create();
            if (!uow.Characters.TryGetByCharacterId(characterId, out CharacterPersistenceModel character))
            {
                uow.Commit();
                return false;
            }

            if (character.AccountId != accountId)
            {
                uow.Commit();
                return false;
            }

            uow.Characters.Delete(characterId);
            uow.InventoryItems.ReplaceForCharacter(characterId, null);
            uow.EquipmentSlots.ReplaceForCharacter(characterId, null);
            uow.SkillLoadouts.ReplaceForCharacter(characterId, null);
            uow.Commit();
            return true;
        }

        public void UpsertCharacterAggregate(CharacterAggregatePersistenceModel aggregate, DateTime nowUtc, bool markLogin, bool markLogout)
        {
            if (aggregate == null || aggregate.Character == null)
                throw new ArgumentNullException(nameof(aggregate));

            using IServerUnitOfWork uow = _uowFactory.Create();

            uow.Characters.Upsert(aggregate.Character);
            if (markLogin)
                uow.Characters.MarkLogin(aggregate.Character.CharacterId, nowUtc);
            if (markLogout)
                uow.Characters.MarkLogout(aggregate.Character.CharacterId, nowUtc);

            uow.InventoryItems.ReplaceForCharacter(aggregate.Character.CharacterId, aggregate.InventoryItems);
            uow.EquipmentSlots.ReplaceForCharacter(aggregate.Character.CharacterId, aggregate.EquipmentSlots);
            uow.SkillLoadouts.ReplaceForCharacter(aggregate.Character.CharacterId, aggregate.SkillLoadout);

            if (aggregate.Pet != null)
                uow.Pets.Upsert(aggregate.Pet);

            if (aggregate.MailCurrency != null)
                uow.MailCurrencies.Upsert(aggregate.MailCurrency);

            uow.Commit();
        }

        public CharacterAggregatePersistenceModel BuildAggregateFromRuntime(
            PlayerEntity player,
            IReadOnlyDictionary<int, InventoryItemRecord> inventory,
            IReadOnlyDictionary<string, EquippedItemRecord> equipment,
            int? activePetId)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            var inventoryItems = new List<InventoryItemPersistenceModel>();
            if (inventory != null)
            {
                foreach (KeyValuePair<int, InventoryItemRecord> entry in inventory)
                {
                    inventoryItems.Add(new InventoryItemPersistenceModel
                    {
                        CharacterId = player.Id,
                        SlotIndex = entry.Key,
                        ItemInstanceId = entry.Value.ItemInstanceId,
                        ItemId = entry.Value.ItemId,
                        Quantity = entry.Value.Quantity,
                        EnhancementLevel = entry.Value.Options?.EnhancementLevel ?? 0,
                        ExcellentFlags = entry.Value.Options?.ExcellentFlags ?? 0,
                        SocketData = SerializeSockets(entry.Value.Options?.Sockets),
                        SellValue = entry.Value.Options?.SellValue ?? 0
                    });
                }
            }

            var equipmentSlots = new List<EquipmentSlotPersistenceModel>();
            if (equipment != null)
            {
                foreach (KeyValuePair<string, EquippedItemRecord> entry in equipment)
                {
                    equipmentSlots.Add(new EquipmentSlotPersistenceModel
                    {
                        CharacterId = player.Id,
                        SlotName = entry.Key,
                        ItemInstanceId = entry.Value.ItemInstanceId,
                        ItemId = entry.Value.ItemId,
                        EnhancementLevel = entry.Value.Options?.EnhancementLevel ?? 0,
                        ExcellentFlags = entry.Value.Options?.ExcellentFlags ?? 0,
                        SocketData = SerializeSockets(entry.Value.Options?.Sockets),
                        SellValue = entry.Value.Options?.SellValue ?? 0
                    });
                }
            }

            return new CharacterAggregatePersistenceModel
            {
                Character = new CharacterPersistenceModel
                {
                    CharacterId = player.Id,
                    AccountId = player.AccountId,
                    Name = player.Name,
                    Class = "Unknown",
                    Level = player.Level,
                    MapId = 1,
                    PosX = player.X,
                    PosY = player.Y,
                    PosZ = player.Z,
                    HpCurrent = player.HpCurrent,
                    HpMax = player.HpMax,
                    LastLoginUtc = null,
                    LastLogoutUtc = DateTime.UtcNow
                },
                InventoryItems = inventoryItems,
                EquipmentSlots = equipmentSlots,
                SkillLoadout = Array.Empty<SkillLoadoutPersistenceModel>(),
                Pet = activePetId.HasValue
                    ? new PetPersistenceModel
                    {
                        CharacterId = player.Id,
                        PetId = activePetId.Value,
                        Level = 1,
                        IsActive = true
                    }
                    : null,
                MailCurrency = new MailCurrencyPersistenceModel
                {
                    CharacterId = player.Id,
                    Zen = 0,
                    Gems = 0,
                    Bless = 0,
                    Soul = 0
                }
            };
        }

        private static string NormalizeCharacterClass(string characterClass)
        {
            if (string.IsNullOrWhiteSpace(characterClass))
                return null;

            string normalized = characterClass.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "warrior":
                case "knight":
                    return "Warrior";
                case "mage":
                case "wizard":
                    return "Mage";
                case "ranger":
                case "archer":
                    return "Ranger";
                case "paladin":
                case "cleric":
                    return "Paladin";
                case "darklord":
                case "dark lord":
                    return "DarkLord";
                default:
                    return "Warrior";
            }
        }

        private sealed class SpawnPosition
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        private static string SerializeSockets(int[] sockets)
        {
            if (sockets == null || sockets.Length == 0)
                return string.Empty;

            return string.Join(",", sockets);
        }

        private static SpawnPosition GetSpawnPositionByClass(string characterClass)
        {
            switch (characterClass)
            {
                case "Warrior":
                    return new SpawnPosition { X = 0f, Y = 0f, Z = 0f };
                case "Mage":
                    return new SpawnPosition { X = 5f, Y = 0f, Z = 0f };
                case "Ranger":
                    return new SpawnPosition { X = 10f, Y = 0f, Z = 0f };
                case "Paladin":
                    return new SpawnPosition { X = 15f, Y = 0f, Z = 0f };
                case "DarkLord":
                    return new SpawnPosition { X = 20f, Y = 0f, Z = 0f };
                default:
                    return new SpawnPosition { X = 0f, Y = 0f, Z = 0f };
            }
        }
    }

    public sealed class SqliteAuthAccountStore : IAccountStore
    {
        private readonly IServerUnitOfWorkFactory _uowFactory;

        public SqliteAuthAccountStore(IServerUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        }

        public bool TryGetByUsername(string username, out AccountRecord account)
        {
            account = null;
            using IServerUnitOfWork uow = _uowFactory.Create();
            if (!uow.Accounts.TryGetByUsername(username, out AccountPersistenceModel row))
            {
                uow.Commit();
                return false;
            }

            account = ToAuthModel(row);
            uow.Commit();
            return true;
        }

        public bool TryGetByAccountId(int accountId, out AccountRecord account)
        {
            account = null;
            using IServerUnitOfWork uow = _uowFactory.Create();
            if (!uow.Accounts.TryGetByAccountId(accountId, out AccountPersistenceModel row))
            {
                uow.Commit();
                return false;
            }

            account = ToAuthModel(row);
            uow.Commit();
            return true;
        }

        public void Upsert(AccountRecord account)
        {
            using IServerUnitOfWork uow = _uowFactory.Create();

            DateTime now = DateTime.UtcNow;
            DateTime createdAt = now;
            if (uow.Accounts.TryGetByAccountId(account.AccountId, out AccountPersistenceModel existing))
                createdAt = existing.CreatedAtUtc;

            uow.Accounts.Upsert(new AccountPersistenceModel
            {
                AccountId = account.AccountId,
                Username = account.Username,
                AccountName = account.AccountName,
                PasswordHash = account.PasswordHash,
                IsActive = account.IsActive,
                CreatedAtUtc = createdAt,
                UpdatedAtUtc = now
            });

            uow.Commit();
        }

        private static AccountRecord ToAuthModel(AccountPersistenceModel row)
        {
            return new AccountRecord
            {
                AccountId = row.AccountId,
                Username = row.Username,
                AccountName = row.AccountName,
                PasswordHash = row.PasswordHash,
                IsActive = row.IsActive
            };
        }
    }
}
