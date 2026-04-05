using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Social;
using MuLike.UI;
using MuLike.World;
using UnityEngine;

namespace MuLike.Events
{
    /// <summary>
    /// Endgame event and world-boss runtime.
    /// Covers invasions, 15-minute push notifications, world boss spawn timers,
    /// and loot ownership rules based on first hit and most damage.
    /// </summary>
    public sealed class InvasionManager : MonoBehaviour
    {
        public enum InvasionType
        {
            GoldenInvasion,
            RedDragonInvasion,
            SkeletonKingNight
        }

        public enum WorldBossType
        {
            Kundun,
            Selupan,
            Medusa
        }

        public enum LootTag
        {
            Participation,
            FirstHit,
            MostDamage,
            PartyClear,
            GuildClear
        }

        [Serializable]
        public struct InvasionSpawnRule
        {
            public InvasionType type;
            public MapLoader.MapId[] maps;
            public Vector3[] spawnPoints;
            public DayOfWeek dayOfWeek;
            public int hourLocal;
            public int minuteLocal;
            public float durationMinutes;
        }

        [Serializable]
        public struct InvasionRuntimeState
        {
            public InvasionType type;
            public MapLoader.MapId mapId;
            public Vector3 spawnPoint;
            public float startsAt;
            public float endsAt;
            public bool preNotified;
            public bool active;
            public DayOfWeek recurrenceDay;
            public int recurrenceHour;
            public int recurrenceMinute;
            public float durationSeconds;
        }

        [Serializable]
        public sealed class BossContribution
        {
            public string playerName;
            public float totalDamage;
            public long firstHitUnixMs;
        }

        [Serializable]
        public sealed class WorldBossState
        {
            public string bossId;
            public WorldBossType type;
            public MapLoader.MapId mapId;
            public Vector3 spawnPoint;
            public float respawnIntervalSeconds;
            public float nextSpawnAt;
            public bool active;
            public long spawnUnixMs;
            public string firstHitPlayer;
            public string mostDamagePlayer;
            public readonly List<BossContribution> contributions = new();
        }

        [Serializable]
        public struct BossLootResult
        {
            public string bossId;
            public string playerName;
            public LootTag[] lootTags;
        }

        [Header("Invasions")]
        [SerializeField] private InvasionSpawnRule[] _invasionSchedule = Array.Empty<InvasionSpawnRule>();
        [SerializeField, Min(60f)] private float _preNotificationLeadSeconds = 900f;

        [Header("World Bosses")]
        [SerializeField] private WorldBossState[] _bossTemplates = Array.Empty<WorldBossState>();

        [Header("Dependencies")]
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private MinimapSystem _minimapSystem;
        [SerializeField] private PartyManager _partyManager;
        [SerializeField] private GuildManager _guildManager;

        private readonly List<InvasionRuntimeState> _scheduledInvasions = new();
        private readonly Dictionary<string, WorldBossState> _bossStates = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<InvasionRuntimeState> ScheduledInvasions => _scheduledInvasions;
        public IReadOnlyDictionary<string, WorldBossState> WorldBosses => _bossStates;

        public event Action<InvasionRuntimeState> OnInvasionPreNotified;
        public event Action<InvasionRuntimeState> OnInvasionStarted;
        public event Action<InvasionRuntimeState> OnInvasionEnded;
        public event Action<WorldBossState> OnWorldBossSpawned;
        public event Action<WorldBossState> OnWorldBossDespawned;
        public event Action<WorldBossState> OnWorldBossTimerChanged;
        public event Action<BossLootResult[]> OnBossLootResolved;
        public event Action<ChatSystem.PushNotificationRequest> OnPushNotificationRequested;

        private void Awake()
        {
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_minimapSystem == null)
                _minimapSystem = FindAnyObjectByType<MinimapSystem>();
            if (_partyManager == null)
                _partyManager = FindAnyObjectByType<PartyManager>();
            if (_guildManager == null)
                _guildManager = FindAnyObjectByType<GuildManager>();

            BuildUpcomingInvasions();
            BuildBossStateCache();
            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            TickInvasions();
            TickWorldBosses();
        }

        public bool CanEngageRedDragon(string playerName)
        {
            bool hasParty = _partyManager != null && _partyManager.HasParty && _partyManager.IsMember(playerName);
            bool hasGuild = _guildManager != null && _guildManager.HasGuild && _guildManager.IsMember(playerName);
            return hasParty || hasGuild;
        }

        public void RegisterBossHit(string bossId, string playerName, float damage)
        {
            if (!_bossStates.TryGetValue(bossId, out WorldBossState boss) || !boss.active)
                return;

            playerName = string.IsNullOrWhiteSpace(playerName) ? "Unknown" : playerName;
            BossContribution contribution = FindOrCreateContribution(boss, playerName);
            contribution.totalDamage += Mathf.Max(0f, damage);
            if (contribution.firstHitUnixMs <= 0)
                contribution.firstHitUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (string.IsNullOrWhiteSpace(boss.firstHitPlayer))
                boss.firstHitPlayer = playerName;

            BossContribution top = contribution;
            for (int i = 0; i < boss.contributions.Count; i++)
            {
                if (boss.contributions[i].totalDamage > top.totalDamage)
                    top = boss.contributions[i];
            }

            boss.mostDamagePlayer = top.playerName;
        }

        public BossLootResult[] ResolveBossDefeat(string bossId)
        {
            if (!_bossStates.TryGetValue(bossId, out WorldBossState boss))
                return Array.Empty<BossLootResult>();

            var results = new List<BossLootResult>(boss.contributions.Count);
            for (int i = 0; i < boss.contributions.Count; i++)
            {
                BossContribution entry = boss.contributions[i];
                var tags = new List<LootTag> { LootTag.Participation };
                if (string.Equals(entry.playerName, boss.firstHitPlayer, StringComparison.OrdinalIgnoreCase))
                    tags.Add(LootTag.FirstHit);
                if (string.Equals(entry.playerName, boss.mostDamagePlayer, StringComparison.OrdinalIgnoreCase))
                    tags.Add(LootTag.MostDamage);
                if (_partyManager != null && _partyManager.IsMember(entry.playerName))
                    tags.Add(LootTag.PartyClear);
                if (_guildManager != null && _guildManager.IsMember(entry.playerName))
                    tags.Add(LootTag.GuildClear);

                results.Add(new BossLootResult
                {
                    bossId = bossId,
                    playerName = entry.playerName,
                    lootTags = tags.ToArray()
                });
            }

            boss.active = false;
            boss.nextSpawnAt = Time.unscaledTime + Mathf.Max(60f, boss.respawnIntervalSeconds);
            boss.firstHitPlayer = string.Empty;
            boss.mostDamagePlayer = string.Empty;
            boss.contributions.Clear();
            _minimapSystem?.RemoveMarker($"boss:{bossId}");
            OnWorldBossDespawned?.Invoke(boss);
            OnBossLootResolved?.Invoke(results.ToArray());
            return results.ToArray();
        }

        public bool TryForceSpawnBoss(WorldBossType type, MapLoader.MapId mapId, Vector3 position, float respawnSeconds)
        {
            string bossId = type.ToString();
            if (!_bossStates.TryGetValue(bossId, out WorldBossState boss))
            {
                boss = new WorldBossState { bossId = bossId, type = type };
                _bossStates[bossId] = boss;
            }

            boss.mapId = mapId;
            boss.spawnPoint = position;
            boss.respawnIntervalSeconds = Mathf.Max(60f, respawnSeconds);
            SpawnBoss(boss);
            return true;
        }

        public float GetBossRespawnRemaining(string bossId)
        {
            if (!_bossStates.TryGetValue(bossId, out WorldBossState boss))
                return 0f;

            if (boss.active)
                return 0f;

            return Mathf.Max(0f, boss.nextSpawnAt - Time.unscaledTime);
        }

        private void BuildUpcomingInvasions()
        {
            _scheduledInvasions.Clear();
            DateTime now = DateTime.Now;
            for (int i = 0; i < _invasionSchedule.Length; i++)
            {
                InvasionSpawnRule rule = _invasionSchedule[i];
                DateTime next = ResolveNextOccurrence(now, rule.dayOfWeek, rule.hourLocal, rule.minuteLocal);
                if (rule.maps == null || rule.maps.Length == 0 || rule.spawnPoints == null || rule.spawnPoints.Length == 0)
                    continue;

                for (int mapIndex = 0; mapIndex < rule.maps.Length; mapIndex++)
                {
                    Vector3 spawn = rule.spawnPoints[Mathf.Min(mapIndex, rule.spawnPoints.Length - 1)];
                    _scheduledInvasions.Add(new InvasionRuntimeState
                    {
                        type = rule.type,
                        mapId = rule.maps[mapIndex],
                        spawnPoint = spawn,
                        startsAt = ToUnscaledSchedule(next),
                        endsAt = ToUnscaledSchedule(next) + Mathf.Max(60f, rule.durationMinutes * 60f),
                        preNotified = false,
                        active = false,
                        recurrenceDay = rule.dayOfWeek,
                        recurrenceHour = rule.hourLocal,
                        recurrenceMinute = rule.minuteLocal,
                        durationSeconds = Mathf.Max(60f, rule.durationMinutes * 60f)
                    });
                }
            }
        }

        private void BuildBossStateCache()
        {
            _bossStates.Clear();
            for (int i = 0; i < _bossTemplates.Length; i++)
            {
                WorldBossState template = _bossTemplates[i];
                if (template == null)
                    continue;

                string key = string.IsNullOrWhiteSpace(template.bossId) ? template.type.ToString() : template.bossId;
                template.bossId = key;
                template.contributions.Clear();
                if (template.nextSpawnAt <= 0f)
                    template.nextSpawnAt = Time.unscaledTime + Mathf.Max(30f, template.respawnIntervalSeconds);
                _bossStates[key] = template;
            }
        }

        private void TickInvasions()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _scheduledInvasions.Count; i++)
            {
                InvasionRuntimeState state = _scheduledInvasions[i];
                if (!state.preNotified && now >= state.startsAt - _preNotificationLeadSeconds)
                {
                    state.preNotified = true;
                    _scheduledInvasions[i] = state;
                    NotifyPush($"{state.type} incoming", $"{state.type} starts in 15 minutes at {state.mapId}.");
                    _chatSystem?.ReceiveSystemMessage($"{state.type} starts in 15 minutes at {state.mapId}.");
                    OnInvasionPreNotified?.Invoke(state);
                }

                if (!state.active && now >= state.startsAt && now < state.endsAt)
                {
                    state.active = true;
                    _scheduledInvasions[i] = state;
                    StartInvasion(state);
                    OnInvasionStarted?.Invoke(state);
                }

                if (state.active && now >= state.endsAt)
                {
                    state.active = false;
                    _scheduledInvasions[i] = state;
                    EndInvasion(state);
                    OnInvasionEnded?.Invoke(state);

                    DateTime next = ResolveNextOccurrence(DateTime.Now.AddMinutes(1), state.recurrenceDay, state.recurrenceHour, state.recurrenceMinute);
                    state.startsAt = ToUnscaledSchedule(next);
                    state.endsAt = state.startsAt + state.durationSeconds;
                    state.preNotified = false;
                    _scheduledInvasions[i] = state;
                }
            }
        }

        private void TickWorldBosses()
        {
            foreach (KeyValuePair<string, WorldBossState> kv in _bossStates)
            {
                WorldBossState boss = kv.Value;
                if (!boss.active && Time.unscaledTime >= boss.nextSpawnAt)
                    SpawnBoss(boss);

                OnWorldBossTimerChanged?.Invoke(boss);
            }
        }

        private void StartInvasion(InvasionRuntimeState state)
        {
            string label = state.type switch
            {
                InvasionType.GoldenInvasion => "Golden Goblins / Rabbits / Dragons",
                InvasionType.RedDragonInvasion => "Red Dragon",
                _ => "Skeleton King"
            };

            _chatSystem?.ReceiveSystemMessage($"{state.type} started in {state.mapId}.");
            _minimapSystem?.UpsertWorldEvent($"invasion:{state.type}:{state.mapId}", label, MinimapSystem.MarkerType.Event, state.spawnPoint, state.endsAt - Time.unscaledTime);
        }

        private void EndInvasion(InvasionRuntimeState state)
        {
            _chatSystem?.ReceiveSystemMessage($"{state.type} ended in {state.mapId}.");
            _minimapSystem?.RemoveMarker($"event:invasion:{state.type}:{state.mapId}");
        }

        private void SpawnBoss(WorldBossState boss)
        {
            boss.active = true;
            boss.spawnUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            boss.firstHitPlayer = string.Empty;
            boss.mostDamagePlayer = string.Empty;
            boss.contributions.Clear();

            _chatSystem?.ReceiveSystemMessage($"World boss {boss.type} has appeared in {boss.mapId}.");
            NotifyPush($"{boss.type} spawned", $"{boss.type} has appeared in {boss.mapId}.");
            _minimapSystem?.UpsertMarker(new MinimapSystem.MinimapMarker
            {
                id = $"boss:{boss.bossId}",
                type = MinimapSystem.MarkerType.EliteMonster,
                target = null,
                worldPositionFallback = boss.spawnPoint,
                label = boss.type.ToString(),
                visible = true,
                useCustomColor = true,
                markerColor = new Color(1f, 0.15f, 0.15f)
            });
            OnWorldBossSpawned?.Invoke(boss);
        }

        private void NotifyPush(string title, string body)
        {
            OnPushNotificationRequested?.Invoke(new ChatSystem.PushNotificationRequest
            {
                title = title,
                body = body,
                channel = MuLike.Systems.ChatChannel.System
            });
        }

        private static DateTime ResolveNextOccurrence(DateTime now, DayOfWeek dayOfWeek, int hour, int minute)
        {
            int delta = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
            DateTime candidate = new DateTime(now.Year, now.Month, now.Day, Mathf.Clamp(hour, 0, 23), Mathf.Clamp(minute, 0, 59), 0).AddDays(delta);
            if (candidate <= now)
                candidate = candidate.AddDays(7);
            return candidate;
        }

        private static float ToUnscaledSchedule(DateTime scheduled)
        {
            double seconds = (scheduled - DateTime.Now).TotalSeconds;
            return Time.unscaledTime + Mathf.Max(0f, (float)seconds);
        }

        private static BossContribution FindOrCreateContribution(WorldBossState boss, string playerName)
        {
            for (int i = 0; i < boss.contributions.Count; i++)
            {
                if (string.Equals(boss.contributions[i].playerName, playerName, StringComparison.OrdinalIgnoreCase))
                    return boss.contributions[i];
            }

            var created = new BossContribution
            {
                playerName = playerName,
                totalDamage = 0f,
                firstHitUnixMs = 0L
            };
            boss.contributions.Add(created);
            return created;
        }
    }
}