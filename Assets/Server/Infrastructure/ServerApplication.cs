using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MuLike.Server.Auth;
using MuLike.Server.Game.Definitions;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.Loop;
using MuLike.Server.Game.Repositories;
using MuLike.Server.Game.Skills;
using MuLike.Server.Game.Snapshots;
using MuLike.Server.Game.Systems;
using MuLike.Server.Game.World;
using MuLike.Server.Gateway;
using MuLike.Server.Infrastructure.ContentPipeline;
using MuLike.Server.Persistence.Abstractions;
using MuLike.Server.Persistence.Sqlite;
using MuLike.Shared.Protocol;

namespace MuLike.Server.Infrastructure
{
    /// <summary>
    /// Composition root for the server runtime. Wires gateway, auth, world and gameplay systems.
    /// </summary>
    public sealed class ServerApplication
    {
        private const int DefaultMapId = 1;
        private const float MonsterApproachTolerance = 0.05f;
        private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(5);

        private readonly GameLoop _gameLoop;
        private readonly SqliteServerPersistenceService _persistenceService;
        private readonly TargetingSystem _targetingSystem;
        private readonly AutoAttackSystem _autoAttackSystem;
        private readonly SkillDatabase _skillDatabase;
        private float _lastDeltaTime;

        public event Action<int, float, float, float> OnPlayerMoved;
        public event Action<int, int, int, bool> OnAttackPerformed; // attackerId, targetId, damage, isCritical

        private ServerApplication(
            SessionManager sessionManager,
            AuthService authService,
            WorldManager worldManager,
            SpawnManager spawnManager,
            MovementSystem movementSystem,
            CombatSystem combatSystem,
            TargetingSystem targetingSystem,
            AutoAttackSystem autoAttackSystem,
            InventorySystem inventorySystem,
            EquipmentSystem equipmentSystem,
            SkillDatabase skillDatabase,
            ItemDatabase itemDatabase,
            SkillSystem skillSystem,
            PetSystem petSystem,
            LootSystem lootSystem,
            StatRebuildService statRebuildService,
            DeathSystem deathSystem,
            CharacterRepository characterRepository,
            InventoryRepository inventoryRepository,
            EquipmentRepository equipmentRepository,
            PetRepository petRepository,
            SqliteServerPersistenceService persistenceService,
            GameLoop gameLoop)
        {
            SessionManager = sessionManager;
            AuthService = authService;
            WorldManager = worldManager;
            SpawnManager = spawnManager;
            MovementSystem = movementSystem;
            CombatSystem = combatSystem;
            _targetingSystem = targetingSystem;
            _autoAttackSystem = autoAttackSystem;
            InventorySystem = inventorySystem;
            EquipmentSystem = equipmentSystem;
            _skillDatabase = skillDatabase ?? throw new ArgumentNullException(nameof(skillDatabase));
            _ = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
            SkillSystem = skillSystem;
            PetSystem = petSystem;
            LootSystem = lootSystem;
            StatRebuildService = statRebuildService;
            DeathSystem = deathSystem;
            CharacterRepository = characterRepository;
            InventoryRepository = inventoryRepository;
            EquipmentRepository = equipmentRepository;
            PetRepository = petRepository;
            _persistenceService = persistenceService;
            _gameLoop = gameLoop;
            _lastDeltaTime = 0.05f; // Default 20 ticks/sec

            _autoAttackSystem.AttackPerformed += HandleAttackPerformed;
            _gameLoop.OnTick += HandleTick;
        }

        private void HandleAttackPerformed(int attackerId, int targetId, int damage, bool isCritical)
        {
            OnAttackPerformed?.Invoke(attackerId, targetId, damage, isCritical);
        }

        public SessionManager SessionManager { get; }
        public AuthService AuthService { get; }

        public WorldManager WorldManager { get; }
        public SpawnManager SpawnManager { get; }

        public MovementSystem MovementSystem { get; }
        public CombatSystem CombatSystem { get; }
        public InventorySystem InventorySystem { get; }
        public EquipmentSystem EquipmentSystem { get; }
        public SkillSystem SkillSystem { get; }
        public PetSystem PetSystem { get; }
        public LootSystem LootSystem { get; }
        public StatRebuildService StatRebuildService { get; }
        public DeathSystem DeathSystem { get; }

        public CharacterRepository CharacterRepository { get; }
        public InventoryRepository InventoryRepository { get; }
        public EquipmentRepository EquipmentRepository { get; }
        public PetRepository PetRepository { get; }

        public bool IsRunning { get; private set; }

        public static ServerApplication CreateDefault(int ticksPerSecond = 20)
        {
            var passwordHasher = new PasswordHasher();

            var sqliteOptions = new SqliteServerDatabaseOptions
            {
                DatabasePath = ResolveServerDatabasePath()
            };
            var sqliteConnectionFactory = new SqliteConnectionFactory(sqliteOptions);
            new SqliteMigrationRunner(sqliteConnectionFactory).EnsureMigrated();
            new SqliteSeedRunner(sqliteConnectionFactory).SeedIfEmpty(
                passwordHasher.Hash("admin123"),
                passwordHasher.Hash("tester123"));

            var sqliteUowFactory = new SqliteServerUnitOfWorkFactory(sqliteConnectionFactory);
            var accountStore = new SqliteAuthAccountStore(sqliteUowFactory);
            var persistenceService = new SqliteServerPersistenceService(sqliteUowFactory);

            var authOptions = new AuthOptions
            {
                Issuer = "MuLike.Server",
                Audience = "MuLike.Client",
                AccessTokenSigningKey = ResolveAccessTokenSigningKey(),
                AccessTokenLifetime = TimeSpan.FromMinutes(15),
                RefreshTokenLifetime = TimeSpan.FromDays(14),
                RotateRefreshTokens = true,
                MaxActiveRefreshSessionsPerAccount = 5
            };

            var sessionStore = new InMemorySessionStore();
            var skillDatabase = new SkillDatabase();
            var itemDatabase = new ItemDatabase();
            ServerItemCatalogBridge.PopulateFromClientCatalog(itemDatabase);
            var combatSystem = new CombatSystem();
            var worldManager = new WorldManager();
            var spawnManager = new SpawnManager();

            bool importedWorldContent = ServerContentPipelineImporter.TryApplyFromResources(
                itemDatabase,
                skillDatabase,
                worldManager,
                spawnManager);

            var app = new ServerApplication(
                new SessionManager(),
                new AuthService(accountStore, sessionStore, passwordHasher, new TokenService(authOptions), authOptions),
                worldManager,
                spawnManager,
                new MovementSystem(),
                combatSystem,
                new TargetingSystem(),
                new AutoAttackSystem(),
                new InventorySystem(itemDatabase),
                new EquipmentSystem(itemDatabase),
                skillDatabase,
                itemDatabase,
                new SkillSystem(skillDatabase, combatSystem),
                new PetSystem(),
                new LootSystem(),
                new StatRebuildService(),
                new DeathSystem(),
                new CharacterRepository(),
                new InventoryRepository(),
                new EquipmentRepository(),
                new PetRepository(),
                persistenceService,
                new GameLoop(ticksPerSecond));

            if (!importedWorldContent)
                app.InitializeDefaultWorld();

            return app;
        }

        public Task StartAsync()
        {
            if (IsRunning) return Task.CompletedTask;
            IsRunning = true;
            _ = _gameLoop.StartAsync();
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _gameLoop.Stop();
        }

        public Guid ConnectClient(EndPoint remoteEndPoint)
        {
            var sessionId = Guid.NewGuid();
            var endpoint = remoteEndPoint ?? new IPEndPoint(IPAddress.None, 0);
            SessionManager.TryAdd(new ClientConnection(sessionId, endpoint));
            return sessionId;
        }

        public bool AuthenticateClient(Guid sessionId, string username, string password, out string accessToken)
        {
            accessToken = null;
            bool ok = AuthenticateClient(sessionId, username, password, out AuthenticationTokens tokens);
            if (!ok || tokens == null)
                return false;

            accessToken = tokens.AccessToken;
            return true;
        }

        public bool AuthenticateClient(Guid sessionId, string username, string password, out AuthenticationTokens tokens)
        {
            tokens = null;

            if (!SessionManager.TryGet(sessionId, out ClientConnection connection))
                return false;

            string remoteIp = connection.RemoteEndPoint?.ToString();
            AuthenticationResult auth = AuthService.Authenticate(username, password, remoteIp, userAgent: "gateway");
            if (!auth.Success || auth.Account == null || auth.Tokens == null)
                return false;

            if (!AttachAuthenticatedAccountToSession(connection, auth.Account.AccountId, auth.Account.AccountName))
                return false;

            tokens = auth.Tokens;
            return true;
        }

        public bool AuthenticateClientWithRefresh(Guid sessionId, string refreshToken, out AuthenticationTokens tokens)
        {
            tokens = null;

            if (!SessionManager.TryGet(sessionId, out ClientConnection connection))
                return false;

            string remoteIp = connection.RemoteEndPoint?.ToString();
            AuthenticationResult auth = AuthService.Refresh(refreshToken, remoteIp, userAgent: "gateway-refresh");
            if (!auth.Success || auth.Account == null || auth.Tokens == null)
                return false;

            if (!AttachAuthenticatedAccountToSession(connection, auth.Account.AccountId, auth.Account.AccountName))
                return false;

            tokens = auth.Tokens;
            return true;
        }

        private bool AttachAuthenticatedAccountToSession(ClientConnection connection, int accountId, string accountName)
        {
            CharacterAggregatePersistenceModel aggregate;
            if (!_persistenceService.TryLoadCharacterAggregateByAccountId(accountId, out aggregate))
            {
                aggregate = _persistenceService.CreateDefaultCharacterAggregate(accountId, accountName);
            }

            int characterId = aggregate.Character.CharacterId;
            connection.MarkAuthenticated(characterId);

            var player = new PlayerEntity(
                characterId,
                accountId,
                aggregate.Character.Name,
                aggregate.Character.PosX,
                aggregate.Character.PosY,
                aggregate.Character.PosZ,
                aggregate.Character.Class);
            player.SetLevel(aggregate.Character.Level);
            CharacterRepository.Save(player);

            var inventory = new Dictionary<int, InventoryItemRecord>();
            if (aggregate.InventoryItems != null)
            {
                for (int i = 0; i < aggregate.InventoryItems.Count; i++)
                {
                    InventoryItemPersistenceModel item = aggregate.InventoryItems[i];
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
                            Sockets = ParseSockets(item.SocketData)
                        }
                    };
                }
            }
            InventoryRepository.Replace(characterId, inventory);

            var equipment = new Dictionary<string, EquippedItemRecord>(StringComparer.OrdinalIgnoreCase);
            if (aggregate.EquipmentSlots != null)
            {
                for (int i = 0; i < aggregate.EquipmentSlots.Count; i++)
                {
                    EquipmentSlotPersistenceModel slot = aggregate.EquipmentSlots[i];
                    equipment[slot.SlotName] = new EquippedItemRecord
                    {
                        ItemInstanceId = slot.ItemInstanceId,
                        ItemId = slot.ItemId,
                        Options = new ItemInstanceOptionsRecord
                        {
                            EnhancementLevel = slot.EnhancementLevel,
                            ExcellentFlags = slot.ExcellentFlags,
                            SellValue = slot.SellValue,
                            Sockets = ParseSockets(slot.SocketData)
                        }
                    };
                }
            }
            EquipmentRepository.Replace(characterId, equipment);

            RebuildPlayerStatsFromEquipment(player);

            if (aggregate.Pet != null && aggregate.Pet.IsActive)
                PetRepository.SetActivePet(characterId, aggregate.Pet.PetId);
            else
                PetRepository.ClearActivePet(characterId);

            _persistenceService.UpsertCharacterAggregate(aggregate, DateTime.UtcNow, markLogin: true, markLogout: false);

            if (WorldManager.TryGetMap(DefaultMapId, out MapInstance map) && !map.TryGetEntity(player.Id, out _))
                map.AddEntity(player);

            return true;
        }

        public bool DisconnectClient(Guid sessionId)
        {
            if (!SessionManager.TryRemove(sessionId, out var connection)) return false;

            if (connection.CharacterId.HasValue && CharacterRepository.TryGet(connection.CharacterId.Value, out PlayerEntity player))
            {
                int characterId = connection.CharacterId.Value;
                Dictionary<int, InventoryItemRecord> inventory = InventoryRepository.Load(characterId);
                Dictionary<string, EquippedItemRecord> equipment = EquipmentRepository.Load(characterId);

                int? activePet = null;
                if (PetRepository.TryGetActivePet(characterId, out int petId))
                    activePet = petId;

                CharacterAggregatePersistenceModel aggregate = _persistenceService.BuildAggregateFromRuntime(player, inventory, equipment, activePet);
                _persistenceService.UpsertCharacterAggregate(aggregate, DateTime.UtcNow, markLogin: false, markLogout: true);

                CharacterRepository.Remove(characterId);
                PetRepository.ClearActivePet(characterId);
            }

            if (connection.CharacterId.HasValue &&
                WorldManager.TryGetMap(DefaultMapId, out var map))
            {
                map.RemoveEntity(connection.CharacterId.Value);
            }

            connection.MarkDisconnected();
            return true;
        }

        public bool TouchSession(Guid sessionId)
        {
            return SessionManager.TryMarkHeartbeat(sessionId);
        }

        public bool TryMoveCharacter(Guid sessionId, float x, float y, float z)
        {
            if (!TryResolvePlayer(sessionId, out var player)) return false;

            // Server-authoritative validation: check speed, distance, cheating patterns
            var validation = MovementSystem.ValidateMovement(player, x, y, z, _lastDeltaTime);
            if (!validation.IsValid) return false;

            MovementSystem.ApplyMovement(player, validation.CorrectedX, validation.CorrectedY, validation.CorrectedZ);
            OnPlayerMoved?.Invoke(player.Id, validation.CorrectedX, validation.CorrectedY, validation.CorrectedZ);
            return true;
        }

        public bool TryStartAutoAttack(int playerId, int targetId)
        {
            if (!TryResolvePlayerById(playerId, out var player))
                return false;

            if (!WorldManager.TryGetMap(DefaultMapId, out var map))
                return false;

            if (!map.TryGetEntity(targetId, out var target) || target.IsDead())
                return false;

            if (!_targetingSystem.SetTarget(player, target))
                return false;

            _autoAttackSystem.StartAutoAttack(player, target);
            return true;
        }

        public bool TryCastSkill(Guid sessionId, int skillId, int targetEntityId, out int damage)
        {
            damage = 0;
            if (!TryResolvePlayer(sessionId, out var player)) return false;
            if (!WorldManager.TryGetMap(DefaultMapId, out var map)) return false;
            if (!map.TryGetEntity(targetEntityId, out var target)) return false;

            var allEntities = map.GetEntities();
            var result = SkillSystem.ValidateAndExecuteSkill(player, skillId, target, allEntities);

            if (result.Success)
            {
                damage = result.DamageDealt;
                return true;
            }

            return false;
        }

        public bool TryMoveInventoryItem(Guid sessionId, int fromSlot, int toSlot)
        {
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            return InventorySystem.TryMoveItem(InventoryRepository, player.Id, fromSlot, toSlot);
        }

        public bool TrySplitInventoryStack(Guid sessionId, int fromSlot, int toSlot, int amount)
        {
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            return InventorySystem.TrySplitStack(InventoryRepository, player.Id, fromSlot, toSlot, amount);
        }

        public bool TryDropInventoryItem(Guid sessionId, int fromSlot, int amount, out int dropEntityId)
        {
            dropEntityId = 0;
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            if (!WorldManager.TryGetMap(DefaultMapId, out var map))
                return false;

            if (!InventorySystem.TryDropItem(InventoryRepository, player.Id, fromSlot, amount, out int itemId, out int droppedAmount))
                return false;

            var drop = SpawnManager.SpawnDrop(itemId, droppedAmount, player.X, player.Y, player.Z);
            map.AddEntity(drop);
            dropEntityId = drop.Id;
            return true;
        }

        public bool TryPickUpDropItem(Guid sessionId, int dropEntityId)
        {
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            if (!WorldManager.TryGetMap(DefaultMapId, out var map))
                return false;

            if (!map.TryGetEntity(dropEntityId, out var entity) || entity is not DropEntity drop)
                return false;

            if (!InventorySystem.TryAddItem(InventoryRepository, player.Id, drop.ItemId, drop.Quantity, out int remaining))
            {
                if (remaining == drop.Quantity)
                    return false;
            }

            int taken = drop.Quantity - remaining;
            if (taken <= 0)
                return false;

            drop.Take(taken);
            if (drop.Quantity <= 0)
                map.RemoveEntity(drop.Id);

            return true;
        }

        public bool TryEquipItem(Guid sessionId, int fromSlot, string equipSlot)
        {
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            if (!InventorySystem.TryTakeFromSlot(InventoryRepository, player.Id, fromSlot, out InventoryItemRecord item))
                return false;

            if (!EquipmentSystem.TryEquip(EquipmentRepository, player.Id, player.Level, player.CharacterClass, equipSlot, item, out EquippedItemRecord replaced))
            {
                InventorySystem.TryPlaceInSlot(InventoryRepository, player.Id, fromSlot, item);
                return false;
            }

            if (replaced != null)
            {
                InventorySystem.TryPlaceInSlot(InventoryRepository, player.Id, fromSlot, new InventoryItemRecord
                {
                    ItemInstanceId = replaced.ItemInstanceId,
                    ItemId = replaced.ItemId,
                    Quantity = 1,
                    Options = replaced.Options
                });
            }

            RebuildPlayerStatsFromEquipment(player);
            return true;
        }

        public bool TryUnequipItem(Guid sessionId, string equipSlot, int targetInventorySlot = -1)
        {
            if (!TryResolvePlayer(sessionId, out var player))
                return false;

            if (!EquipmentSystem.TryUnequip(EquipmentRepository, player.Id, equipSlot, out EquippedItemRecord equipped))
                return false;

            int slot = targetInventorySlot;
            if (slot < 0)
            {
                slot = InventorySystem.FindFirstEmptySlot(InventoryRepository, player.Id);
            }

            if (slot < 0 || !InventorySystem.TryPlaceInSlot(InventoryRepository, player.Id, slot, new InventoryItemRecord
            {
                ItemInstanceId = equipped.ItemInstanceId,
                ItemId = equipped.ItemId,
                Quantity = 1,
                Options = equipped.Options
            }))
            {
                // Rollback if inventory has no room.
                EquipmentSystem.TryEquip(EquipmentRepository, player.Id, player.Level, player.CharacterClass, equipSlot, new InventoryItemRecord
                {
                    ItemInstanceId = equipped.ItemInstanceId,
                    ItemId = equipped.ItemId,
                    Quantity = 1,
                    Options = equipped.Options
                }, out _);
                return false;
            }

            RebuildPlayerStatsFromEquipment(player);
            return true;
        }

        public IReadOnlyList<CharacterSummary> ListCharactersByAccountId(int accountId)
        {
            return _persistenceService.ListCharactersByAccountId(accountId);
        }

        public bool TryCreateCharacter(int accountId, string characterName, string characterClass, out int characterId)
        {
            return _persistenceService.TryCreateCharacter(accountId, characterName, characterClass, out characterId);
        }

        public bool TryDeleteCharacter(int accountId, int characterId)
        {
            return _persistenceService.TryDeleteCharacter(accountId, characterId);
        }

        public bool TrySelectCharacter(Guid sessionId, int characterId, out int selectedCharacterId)
        {
            selectedCharacterId = 0;

            if (!SessionManager.TryGet(sessionId, out var connection)) return false;
            if (!_persistenceService.TryLoadCharacterAggregateByCharacterId(characterId, out CharacterAggregatePersistenceModel aggregate))
                return false;

            int accountId = aggregate.Character.AccountId;

            connection.MarkAuthenticated(characterId);
            selectedCharacterId = characterId;

            var player = new PlayerEntity(
                characterId,
                accountId,
                aggregate.Character.Name,
                aggregate.Character.PosX,
                aggregate.Character.PosY,
                aggregate.Character.PosZ,
                aggregate.Character.Class);
            player.SetLevel(aggregate.Character.Level);
            CharacterRepository.Save(player);

            var inventory = new Dictionary<int, InventoryItemRecord>();
            if (aggregate.InventoryItems != null)
            {
                for (int i = 0; i < aggregate.InventoryItems.Count; i++)
                {
                    InventoryItemPersistenceModel item = aggregate.InventoryItems[i];
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
                            Sockets = ParseSockets(item.SocketData)
                        }
                    };
                }
            }
            InventoryRepository.Replace(characterId, inventory);

            var equipment = new Dictionary<string, EquippedItemRecord>(StringComparer.OrdinalIgnoreCase);
            if (aggregate.EquipmentSlots != null)
            {
                for (int i = 0; i < aggregate.EquipmentSlots.Count; i++)
                {
                    EquipmentSlotPersistenceModel slot = aggregate.EquipmentSlots[i];
                    equipment[slot.SlotName] = new EquippedItemRecord
                    {
                        ItemInstanceId = slot.ItemInstanceId,
                        ItemId = slot.ItemId,
                        Options = new ItemInstanceOptionsRecord
                        {
                            EnhancementLevel = slot.EnhancementLevel,
                            ExcellentFlags = slot.ExcellentFlags,
                            SellValue = slot.SellValue,
                            Sockets = ParseSockets(slot.SocketData)
                        }
                    };
                }
            }
            EquipmentRepository.Replace(characterId, equipment);

            RebuildPlayerStatsFromEquipment(player);

            if (aggregate.Pet != null && aggregate.Pet.IsActive)
                PetRepository.SetActivePet(characterId, aggregate.Pet.PetId);
            else
                PetRepository.ClearActivePet(characterId);

            _persistenceService.UpsertCharacterAggregate(aggregate, DateTime.UtcNow, markLogin: true, markLogout: false);

            if (WorldManager.TryGetMap(DefaultMapId, out var map))
            {
                map.AddEntity(player);
            }

            return true;
        }

        private void InitializeDefaultWorld()
        {
            WorldManager.RegisterMap(new MapInstance(DefaultMapId, "World_Dev"));

            var spider = new MonsterDefinition
            {
                MonsterId = 101,
                Name = "Spider",
                Level = 1,
                HpMax = 65,
                Attack = 11,
                Defense = 4,
                AggroRadius = 9f,
                ChaseRadius = 18f,
                LeashRadius = 22f,
                AttackRange = 2f,
                MoveSpeed = 2.8f,
                RespawnSeconds = 8f,
                ExpReward = 35,
                Drops = new[]
                {
                    new MonsterDropDefinition { ItemId = 1001, ChancePercent = 35, MinQuantity = 1, MaxQuantity = 2 }
                }
            };

            var goblin = new MonsterDefinition
            {
                MonsterId = 102,
                Name = "Goblin",
                Level = 2,
                HpMax = 90,
                Attack = 14,
                Defense = 6,
                AggroRadius = 10f,
                ChaseRadius = 20f,
                LeashRadius = 24f,
                AttackRange = 2.2f,
                MoveSpeed = 3.2f,
                RespawnSeconds = 10f,
                ExpReward = 55,
                Drops = new[]
                {
                    new MonsterDropDefinition { ItemId = 1002, ChancePercent = 25, MinQuantity = 1, MaxQuantity = 1 },
                    new MonsterDropDefinition { ItemId = 1003, ChancePercent = 12, MinQuantity = 1, MaxQuantity = 1 }
                }
            };

            SpawnManager.RegisterMonsterSpawnPoint(DefaultMapId, spider, -4f, 0f, 6f);
            SpawnManager.RegisterMonsterSpawnPoint(DefaultMapId, spider, 3f, 0f, 8f);
            SpawnManager.RegisterMonsterSpawnPoint(DefaultMapId, goblin, 10f, 0f, -2f);
            SpawnManager.SpawnInitialMonsters(WorldManager);
        }

        public bool TryResolvePlayer(Guid sessionId, out PlayerEntity player)
        {
            player = null;

            if (!SessionManager.TryGet(sessionId, out var connection)) return false;
            if (!connection.CharacterId.HasValue) return false;
            return CharacterRepository.TryGet(connection.CharacterId.Value, out player);
        }

        private bool TryResolvePlayerById(int playerId, out PlayerEntity player)
        {
            return CharacterRepository.TryGet(playerId, out player);
        }

        private void RebuildPlayerStatsFromEquipment(PlayerEntity player)
        {
            var bonus = EquipmentSystem.BuildEquipmentBonus(EquipmentRepository, player.Id);
            StatRebuildService.RebuildPlayerWithEquipment(player, bonus.attack, bonus.defense, bonus.hp);
        }

        private void HandleTick(float deltaTime)
        {
            if (!IsRunning) return;

            _lastDeltaTime = deltaTime;

            // Update auto-attack system
            _autoAttackSystem.Update(deltaTime, CombatSystem, _targetingSystem);

            // Update skill cooldowns
            SkillSystem.Update(deltaTime);

            // Update dead monster respawns.
            SpawnManager.UpdateRespawns(deltaTime, WorldManager);

            // Keep sessions healthy and remove stale clients.
            IReadOnlyList<Guid> stale = SessionManager.GetExpiredSessionIds(HeartbeatTimeout, DateTime.UtcNow);

            foreach (var sessionId in stale)
            {
                DisconnectClient(sessionId);
            }

            // Handle monster aggro and deaths
            HandleMonsterAggro(deltaTime);
            HandleDeaths();
        }

        private void HandleMonsterAggro(float deltaTime)
        {
            if (!WorldManager.TryGetMap(DefaultMapId, out var map)) return;

            var monsters = map.GetEntities().OfType<MonsterEntity>().ToList();
            var players = map.GetEntities().OfType<PlayerEntity>().ToList();

            foreach (var monster in monsters)
            {
                if (monster.IsDead())
                    continue;

                if (monster.TargetId.HasValue)
                {
                    if (!map.TryGetEntity(monster.TargetId.Value, out var targetEntity) ||
                        targetEntity is not PlayerEntity targetPlayer ||
                        targetPlayer.IsDead())
                    {
                        _autoAttackSystem.StopAutoAttack(monster);
                        _targetingSystem.ClearTarget(monster);
                        continue;
                    }

                    float distanceToHome = Distance(monster.X, monster.Y, monster.Z, monster.SpawnX, monster.SpawnY, monster.SpawnZ);
                    float distanceToTarget = Distance(monster.X, monster.Y, monster.Z, targetPlayer.X, targetPlayer.Y, targetPlayer.Z);

                    if (distanceToHome > monster.LeashRadius || distanceToTarget > monster.ChaseRadius)
                    {
                        _autoAttackSystem.StopAutoAttack(monster);
                        monster.ReturnToSpawn();
                        continue;
                    }

                    if (distanceToTarget > monster.AttackRange)
                    {
                        MoveTowards(monster, targetPlayer.X, targetPlayer.Y, targetPlayer.Z, monster.MoveSpeed * deltaTime);
                        continue;
                    }

                    _autoAttackSystem.StartAutoAttack(monster, targetPlayer);
                    continue;
                }

                PlayerEntity selectedTarget = null;
                float nearestDistance = float.MaxValue;

                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.IsDead())
                        continue;

                    float distance = Distance(monster.X, monster.Y, monster.Z, player.X, player.Y, player.Z);
                    if (distance <= monster.AggroRadius && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        selectedTarget = player;
                    }
                }

                if (selectedTarget != null)
                {
                    _targetingSystem.SetTarget(monster, selectedTarget);
                }
            }
        }

        private void HandleDeaths()
        {
            if (!WorldManager.TryGetMap(DefaultMapId, out var map)) return;

            var deadEntities = map.GetEntities()
                .Where(e => e.IsDead())
                .ToArray();

            foreach (var entity in deadEntities)
            {
                _autoAttackSystem.StopAutoAttack(entity);

                if (entity is MonsterEntity monster)
                {
                    RewardMonsterKill(monster, map);
                    map.RemoveEntity(monster.Id);
                    SpawnManager.NotifyMonsterDeath(monster);
                }

                DeathSystem.HandleDeath(entity);
            }
        }

        private void RewardMonsterKill(MonsterEntity monster, MapInstance map)
        {
            if (monster.LastDamagedByPlayerId.HasValue &&
                CharacterRepository.TryGet(monster.LastDamagedByPlayerId.Value, out var killer))
            {
                killer.AddExperience(monster.ExpReward);
            }

            var drops = LootSystem.RollDrops(monster.Drops);
            for (int i = 0; i < drops.Count; i++)
            {
                (int itemId, int quantity) = drops[i];
                var dropEntity = SpawnManager.SpawnDrop(itemId, quantity, monster.X, monster.Y, monster.Z);
                map.AddEntity(dropEntity);
            }
        }

        private static float Distance(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float dz = z2 - z1;
            return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        private static void MoveTowards(Entity entity, float targetX, float targetY, float targetZ, float maxStep)
        {
            float dx = targetX - entity.X;
            float dy = targetY - entity.Y;
            float dz = targetZ - entity.Z;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

            if (distance <= MonsterApproachTolerance)
                return;

            float step = MathF.Min(maxStep, distance);
            float ratio = step / distance;
            entity.SetPosition(
                entity.X + (dx * ratio),
                entity.Y + (dy * ratio),
                entity.Z + (dz * ratio));
        }

        private static int[] ParseSockets(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new[] { -1, -1, -1, -1, -1 };

            string[] parts = raw.Split(',');
            var sockets = new[] { -1, -1, -1, -1, -1 };
            int count = parts.Length < sockets.Length ? parts.Length : sockets.Length;
            for (int i = 0; i < count; i++)
            {
                if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    sockets[i] = value;
            }

            return sockets;
        }

        private static string ResolveAccessTokenSigningKey()
        {
            string envValue = Environment.GetEnvironmentVariable("MULIKE_ACCESS_TOKEN_KEY");
            if (!string.IsNullOrWhiteSpace(envValue) && envValue.Length >= 32)
                return envValue;

            return "MuLikeMVP_ChangeThis_32Byte_Minimum_SigningKey";
        }

        private static string ResolveServerDatabasePath()
        {
            string envValue = Environment.GetEnvironmentVariable("MULIKE_SERVER_DB_PATH");
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue;

            string baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "mulike_server.sqlite3");
        }
    }
}
