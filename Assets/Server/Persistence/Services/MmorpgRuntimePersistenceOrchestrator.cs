using System;
using System.Collections.Generic;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.Repositories;
using MuLike.Server.Gateway;
using MuLike.Server.Persistence.Core;

namespace MuLike.Server.Persistence.Services
{
    /// <summary>
    /// Maps runtime entities/repositories to persistence aggregates and handles load/save policy.
    /// </summary>
    public sealed class MmorpgRuntimePersistenceOrchestrator
    {
        private readonly IMmorpgPersistenceService _persistence;
        private readonly SessionManager _sessionManager;
        private readonly CharacterRepository _characters;
        private readonly InventoryRepository _inventory;
        private readonly EquipmentRepository _equipment;
        private readonly SkillLoadoutRepository _skills;
        private readonly PetRepository _pets;

        public MmorpgRuntimePersistenceOrchestrator(
            IMmorpgPersistenceService persistence,
            SessionManager sessionManager,
            CharacterRepository characters,
            InventoryRepository inventory,
            EquipmentRepository equipment,
            SkillLoadoutRepository skills,
            PetRepository pets)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
        }

        public bool TryLoadCharacterIntoRuntime(int characterId, out CharacterPersistenceAggregate aggregate)
        {
            aggregate = null;

            if (!_persistence.TryLoadCharacterForWorldEntry(characterId, out CharacterPersistenceAggregate loaded) || loaded == null)
                return false;

            aggregate = loaded;
            ApplyAggregateToRuntime(loaded);
            return true;
        }

        public PersistenceValidationResult SaveCharacterFromRuntime(int characterId, DateTime nowUtc, bool markLogout)
        {
            if (!_characters.TryGet(characterId, out PlayerEntity player))
                return PersistenceValidationResult.Fail($"Character {characterId} is not loaded in runtime.");

            CharacterPersistenceAggregate aggregate = BuildAggregateFromRuntime(player, nowUtc);
            return _persistence.SaveCharacterFromRuntime(aggregate, nowUtc, markLogout);
        }

        public void SaveAllOnlineCheckpoint(DateTime nowUtc)
        {
            IReadOnlyCollection<ClientConnection> connections = _sessionManager.GetAll();
            foreach (ClientConnection connection in connections)
            {
                if (!connection.IsAuthenticated || !connection.CharacterId.HasValue)
                    continue;

                int characterId = connection.CharacterId.Value;
                PersistenceValidationResult save = SaveCharacterFromRuntime(characterId, nowUtc, markLogout: false);
                if (!save.Success)
                {
                    // Keep checkpoint best-effort and non-blocking.
                    UnityEngine.Debug.LogWarning($"[PersistenceCheckpoint] Character {characterId}: {save.Message}");
                }

                _persistence.SaveSession(new SessionData
                {
                    SessionId = connection.SessionId,
                    AccountId = _characters.TryGet(characterId, out PlayerEntity p) ? p.AccountId : 0,
                    CharacterId = characterId,
                    IsAuthenticated = connection.IsAuthenticated,
                    ConnectedAtUtc = connection.ConnectedAtUtc,
                    LastSeenUtc = connection.LastHeartbeatUtc,
                    RemoteAddress = connection.RemoteEndPoint?.ToString(),
                    RefreshTokenHash = string.Empty
                });
            }
        }

        public void SaveSessionOnLogin(ClientConnection connection, int accountId)
        {
            if (connection == null)
                return;

            _persistence.SaveSession(new SessionData
            {
                SessionId = connection.SessionId,
                AccountId = accountId,
                CharacterId = connection.CharacterId,
                IsAuthenticated = connection.IsAuthenticated,
                ConnectedAtUtc = connection.ConnectedAtUtc,
                LastSeenUtc = connection.LastHeartbeatUtc,
                RemoteAddress = connection.RemoteEndPoint?.ToString(),
                RefreshTokenHash = string.Empty
            });
        }

        public void SaveAndCloseSession(ClientConnection connection, DateTime nowUtc)
        {
            if (connection == null)
                return;

            if (connection.CharacterId.HasValue)
                _ = SaveCharacterFromRuntime(connection.CharacterId.Value, nowUtc, markLogout: true);

            _persistence.DeleteSession(connection.SessionId);
        }

        public bool SoftDeleteCharacter(int accountId, int characterId, DateTime nowUtc)
        {
            return _persistence.TrySoftDeleteCharacter(accountId, characterId, nowUtc);
        }

        private CharacterPersistenceAggregate BuildAggregateFromRuntime(PlayerEntity player, DateTime nowUtc)
        {
            Dictionary<int, InventoryItemRecord> inventoryItems = _inventory.Load(player.Id);
            Dictionary<string, EquippedItemRecord> equippedItems = _equipment.Load(player.Id);
            Dictionary<int, int> skillLoadout = _skills.Load(player.Id);

            var inventory = new List<InventoryItemData>(inventoryItems.Count);
            foreach (KeyValuePair<int, InventoryItemRecord> entry in inventoryItems)
            {
                InventoryItemRecord item = entry.Value;
                if (item == null)
                    continue;

                inventory.Add(new InventoryItemData
                {
                    CharacterId = player.Id,
                    SlotIndex = entry.Key,
                    ItemInstanceId = item.ItemInstanceId,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                    EnhancementLevel = item.Options?.EnhancementLevel ?? 0,
                    ExcellentFlags = item.Options?.ExcellentFlags ?? 0,
                    SellValue = item.Options?.SellValue ?? 0,
                    Sockets = item.Options?.Sockets != null ? (int[])item.Options.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 },
                    UpdatedAtUtc = nowUtc
                });
            }

            var equipped = new List<EquippedItemData>(equippedItems.Count);
            foreach (KeyValuePair<string, EquippedItemRecord> entry in equippedItems)
            {
                EquippedItemRecord item = entry.Value;
                if (item == null)
                    continue;

                equipped.Add(new EquippedItemData
                {
                    CharacterId = player.Id,
                    SlotName = entry.Key,
                    ItemInstanceId = item.ItemInstanceId,
                    ItemId = item.ItemId,
                    EnhancementLevel = item.Options?.EnhancementLevel ?? 0,
                    ExcellentFlags = item.Options?.ExcellentFlags ?? 0,
                    SellValue = item.Options?.SellValue ?? 0,
                    Sockets = item.Options?.Sockets != null ? (int[])item.Options.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 },
                    UpdatedAtUtc = nowUtc
                });
            }

            var skills = new List<SkillLoadoutSlotData>(skillLoadout.Count);
            foreach (KeyValuePair<int, int> entry in skillLoadout)
            {
                skills.Add(new SkillLoadoutSlotData
                {
                    CharacterId = player.Id,
                    SlotIndex = entry.Key,
                    SkillId = entry.Value,
                    UpdatedAtUtc = nowUtc
                });
            }

            var character = new CharacterData
            {
                CharacterId = player.Id,
                AccountId = player.AccountId,
                Name = player.Name,
                Class = player.CharacterClass,
                Level = player.Level,
                Experience = player.Experience,
                UpdatedAtUtc = nowUtc,
                LastLogoutUtc = nowUtc,
                WorldPosition = new WorldPositionData
                {
                    MapId = 1,
                    X = player.X,
                    Y = player.Y,
                    Z = player.Z
                }
            };

            var stats = new CharacterStatsData
            {
                CharacterId = player.Id,
                HpCurrent = player.HpCurrent,
                HpMax = player.HpMax,
                MpCurrent = 0,
                MpMax = 0,
                Strength = player.Attack,
                Agility = 0,
                Vitality = player.Defense,
                Energy = 0,
                Leadership = 0,
                UpdatedAtUtc = nowUtc
            };

            return new CharacterPersistenceAggregate
            {
                Character = character,
                Stats = stats,
                Inventory = inventory,
                Equipped = equipped,
                SkillLoadout = skills
            };
        }

        private void ApplyAggregateToRuntime(CharacterPersistenceAggregate aggregate)
        {
            if (aggregate == null || aggregate.Character == null)
                return;

            CharacterData c = aggregate.Character;
            var player = new PlayerEntity(c.CharacterId, c.AccountId, c.Name, c.WorldPosition.X, c.WorldPosition.Y, c.WorldPosition.Z, c.Class);
            player.SetLevel(c.Level);
            _characters.Save(player);

            var inventory = new Dictionary<int, InventoryItemRecord>();
            if (aggregate.Inventory != null)
            {
                for (int i = 0; i < aggregate.Inventory.Count; i++)
                {
                    InventoryItemData item = aggregate.Inventory[i];
                    if (item == null)
                        continue;

                    inventory[item.SlotIndex] = new InventoryItemRecord
                    {
                        ItemInstanceId = item.ItemInstanceId,
                        ItemId = item.ItemId,
                        Quantity = item.Quantity,
                        Options = new ItemInstanceOptionsRecord
                        {
                            EnhancementLevel = item.EnhancementLevel,
                            ExcellentFlags = item.ExcellentFlags,
                            SellValue = item.SellValue,
                            Sockets = item.Sockets != null ? (int[])item.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 }
                        }
                    };
                }
            }

            _inventory.Replace(c.CharacterId, inventory);

            var equipped = new Dictionary<string, EquippedItemRecord>(StringComparer.OrdinalIgnoreCase);
            if (aggregate.Equipped != null)
            {
                for (int i = 0; i < aggregate.Equipped.Count; i++)
                {
                    EquippedItemData slot = aggregate.Equipped[i];
                    if (slot == null || string.IsNullOrWhiteSpace(slot.SlotName))
                        continue;

                    equipped[slot.SlotName] = new EquippedItemRecord
                    {
                        ItemInstanceId = slot.ItemInstanceId,
                        ItemId = slot.ItemId,
                        Options = new ItemInstanceOptionsRecord
                        {
                            EnhancementLevel = slot.EnhancementLevel,
                            ExcellentFlags = slot.ExcellentFlags,
                            SellValue = slot.SellValue,
                            Sockets = slot.Sockets != null ? (int[])slot.Sockets.Clone() : new[] { -1, -1, -1, -1, -1 }
                        }
                    };
                }
            }

            _equipment.Replace(c.CharacterId, equipped);

            var loadout = new Dictionary<int, int>();
            if (aggregate.SkillLoadout != null)
            {
                for (int i = 0; i < aggregate.SkillLoadout.Count; i++)
                {
                    SkillLoadoutSlotData slot = aggregate.SkillLoadout[i];
                    if (slot == null)
                        continue;

                    loadout[slot.SlotIndex] = slot.SkillId;
                }
            }

            _skills.Replace(c.CharacterId, loadout);

            if (_pets.TryGetActivePet(c.CharacterId, out int _))
            {
                // Preserve existing runtime pet state.
            }
        }
    }
}
