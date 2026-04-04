using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MuLike.Server.Auth;
using MuLike.Server.Game.Entities;
using MuLike.Server.Game.Loop;
using MuLike.Server.Game.Repositories;
using MuLike.Server.Game.Systems;
using MuLike.Server.Game.World;
using MuLike.Server.Gateway;

namespace MuLike.Server.Infrastructure
{
    /// <summary>
    /// Composition root for the server runtime. Wires gateway, auth, world and gameplay systems.
    /// </summary>
    public sealed class ServerApplication
    {
        private const int DefaultMapId = 1;
        private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(5);

        private readonly GameLoop _gameLoop;

        private ServerApplication(
            SessionManager sessionManager,
            AuthService authService,
            WorldManager worldManager,
            SpawnManager spawnManager,
            MovementSystem movementSystem,
            CombatSystem combatSystem,
            InventorySystem inventorySystem,
            EquipmentSystem equipmentSystem,
            SkillSystem skillSystem,
            PetSystem petSystem,
            LootSystem lootSystem,
            StatRebuildService statRebuildService,
            DeathSystem deathSystem,
            CharacterRepository characterRepository,
            InventoryRepository inventoryRepository,
            EquipmentRepository equipmentRepository,
            PetRepository petRepository,
            GameLoop gameLoop)
        {
            SessionManager = sessionManager;
            AuthService = authService;
            WorldManager = worldManager;
            SpawnManager = spawnManager;
            MovementSystem = movementSystem;
            CombatSystem = combatSystem;
            InventorySystem = inventorySystem;
            EquipmentSystem = equipmentSystem;
            SkillSystem = skillSystem;
            PetSystem = petSystem;
            LootSystem = lootSystem;
            StatRebuildService = statRebuildService;
            DeathSystem = deathSystem;
            CharacterRepository = characterRepository;
            InventoryRepository = inventoryRepository;
            EquipmentRepository = equipmentRepository;
            PetRepository = petRepository;
            _gameLoop = gameLoop;

            _gameLoop.OnTick += HandleTick;
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
            var app = new ServerApplication(
                new SessionManager(),
                new AuthService(new PasswordHasher(), new TokenService("MuLike.Server")),
                new WorldManager(),
                new SpawnManager(),
                new MovementSystem(),
                new CombatSystem(),
                new InventorySystem(),
                new EquipmentSystem(),
                new SkillSystem(),
                new PetSystem(),
                new LootSystem(),
                new StatRebuildService(),
                new DeathSystem(),
                new CharacterRepository(),
                new InventoryRepository(),
                new EquipmentRepository(),
                new PetRepository(),
                new GameLoop(ticksPerSecond));

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

        public bool AuthenticateClient(
            Guid sessionId,
            int accountId,
            string accountName,
            string providedPassword,
            string storedHash,
            out string accessToken)
        {
            accessToken = null;

            if (!SessionManager.TryGet(sessionId, out var connection)) return false;
            if (!AuthService.ValidateCredentials(providedPassword, storedHash)) return false;

            accessToken = AuthService.IssueAccessToken(accountId, accountName);

            int characterId = accountId;
            connection.MarkAuthenticated(characterId);

            if (!CharacterRepository.TryGet(characterId, out var player))
            {
                player = new PlayerEntity(characterId, accountId, accountName, 0f, 0f, 0f);
                CharacterRepository.Save(player);

                if (WorldManager.TryGetMap(DefaultMapId, out var map))
                {
                    map.AddEntity(player);
                }
            }

            return true;
        }

        public bool DisconnectClient(Guid sessionId)
        {
            if (!SessionManager.TryRemove(sessionId, out var connection)) return false;

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
            if (!SessionManager.TryGet(sessionId, out var connection)) return false;
            connection.MarkHeartbeat();
            return true;
        }

        public bool TryMoveCharacter(Guid sessionId, float x, float y, float z)
        {
            if (!TryResolvePlayer(sessionId, out var player)) return false;

            MovementSystem.Move(player, x, y, z);
            return true;
        }

        public bool TryCastSkill(Guid sessionId, int skillId, int targetEntityId, out int damage)
        {
            damage = 0;
            if (!TryResolvePlayer(sessionId, out var player)) return false;
            if (!WorldManager.TryGetMap(DefaultMapId, out var map)) return false;
            if (!map.TryGetEntity(targetEntityId, out var target)) return false;
            if (target.IsDead()) return false;

            // Placeholder combat formula until definitions and buffs are wired.
            damage = 10 + (skillId % 5);
            damage = CombatSystem.ApplyDamage(player, target, damage);
            return damage > 0;
        }

        private void InitializeDefaultWorld()
        {
            WorldManager.RegisterMap(new MapInstance(DefaultMapId, "World_Dev"));
        }

        private bool TryResolvePlayer(Guid sessionId, out PlayerEntity player)
        {
            player = null;

            if (!SessionManager.TryGet(sessionId, out var connection)) return false;
            if (!connection.CharacterId.HasValue) return false;
            return CharacterRepository.TryGet(connection.CharacterId.Value, out player);
        }

        private void HandleTick(float deltaTime)
        {
            if (!IsRunning) return;

            // Keep sessions healthy and remove stale clients.
            var stale = SessionManager.GetAll()
                .Where(c => DateTime.UtcNow - c.LastHeartbeatUtc > HeartbeatTimeout)
                .Select(c => c.SessionId)
                .ToArray();

            foreach (var sessionId in stale)
            {
                DisconnectClient(sessionId);
            }
        }
    }
}
