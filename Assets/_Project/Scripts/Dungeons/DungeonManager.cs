using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Social;
using MuLike.UI;
using UnityEngine;

namespace MuLike.Dungeons
{
    /// <summary>
    /// Instanced dungeon runtime for MU endgame modes.
    /// Blood Castle, Devil Square, Chaos Castle, and Illusion Temple.
    /// </summary>
    public sealed class DungeonManager : MonoBehaviour
    {
        public enum DungeonType
        {
            BloodCastle,
            DevilSquare,
            ChaosCastle,
            IllusionTemple
        }

        public enum InstanceState
        {
            Lobby,
            Running,
            Completed,
            Failed
        }

        [Serializable]
        public sealed class DungeonParticipant
        {
            public string playerName;
            public int teamId;
            public int kills;
            public int deaths;
            public int score;
            public bool alive = true;
        }

        [Serializable]
        public sealed class DungeonInstance
        {
            public string instanceId;
            public DungeonType type;
            public int difficultyTier;
            public InstanceState state;
            public float startedAt;
            public float endsAt;
            public int currentWave;
            public int capturedFlags;
            public string crownCarrier;
            public readonly List<DungeonParticipant> participants = new();
        }

        [Header("Durations")]
        [SerializeField, Min(120f)] private float _bloodCastleDuration = 900f;
        [SerializeField, Min(120f)] private float _devilSquareDuration = 600f;
        [SerializeField, Min(120f)] private float _chaosCastleDuration = 720f;
        [SerializeField, Min(120f)] private float _illusionTempleDuration = 840f;

        [Header("Dependencies")]
        [SerializeField] private PartyManager _partyManager;
        [SerializeField] private GuildManager _guildManager;
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private MinimapSystem _minimapSystem;

        private readonly Dictionary<string, DungeonInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, DungeonInstance> Instances => _instances;

        public event Action<DungeonInstance> OnDungeonStarted;
        public event Action<DungeonInstance> OnDungeonUpdated;
        public event Action<DungeonInstance> OnDungeonCompleted;

        private void Awake()
        {
            if (_partyManager == null)
                _partyManager = FindAnyObjectByType<PartyManager>();
            if (_guildManager == null)
                _guildManager = FindAnyObjectByType<GuildManager>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_minimapSystem == null)
                _minimapSystem = FindAnyObjectByType<MinimapSystem>();

            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            foreach (KeyValuePair<string, DungeonInstance> kv in _instances)
            {
                DungeonInstance instance = kv.Value;
                if (instance.state == InstanceState.Running && Time.unscaledTime >= instance.endsAt)
                {
                    instance.state = instance.type == DungeonType.ChaosCastle && CountAlive(instance) > 1 ? InstanceState.Failed : InstanceState.Completed;
                    FinalizeInstance(instance);
                }
            }
        }

        public DungeonInstance CreateInstance(DungeonType type, int difficultyTier, IReadOnlyList<string> players)
        {
            var instance = new DungeonInstance
            {
                instanceId = Guid.NewGuid().ToString("N"),
                type = type,
                difficultyTier = Mathf.Clamp(difficultyTier, 1, 7),
                state = InstanceState.Lobby,
                currentWave = type == DungeonType.DevilSquare ? 1 : 0
            };

            for (int i = 0; players != null && i < players.Count; i++)
            {
                instance.participants.Add(new DungeonParticipant
                {
                    playerName = players[i],
                    teamId = type == DungeonType.IllusionTemple ? i % 2 : 0
                });
            }

            _instances[instance.instanceId] = instance;
            return instance;
        }

        public bool StartInstance(string instanceId)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.state != InstanceState.Lobby)
                return false;

            instance.state = InstanceState.Running;
            instance.startedAt = Time.unscaledTime;
            instance.endsAt = Time.unscaledTime + ResolveDuration(instance.type);

            _chatSystem?.ReceiveSystemMessage($"{instance.type} started. Tier {instance.difficultyTier}.");
            _minimapSystem?.UpsertWorldEvent($"dungeon:{instance.instanceId}", instance.type.ToString(), MinimapSystem.MarkerType.Event, Vector3.zero, instance.endsAt - Time.unscaledTime);
            OnDungeonStarted?.Invoke(instance);
            return true;
        }

        public bool RegisterKill(string instanceId, string killerName, string victimName = null)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.state != InstanceState.Running)
                return false;

            int killerIndex = FindParticipantIndex(instance, killerName);
            if (killerIndex >= 0)
            {
                instance.participants[killerIndex].kills++;
                instance.participants[killerIndex].score += ResolveKillScore(instance.type);
            }

            int victimIndex = FindParticipantIndex(instance, victimName);
            if (victimIndex >= 0)
            {
                instance.participants[victimIndex].deaths++;
                if (instance.type == DungeonType.ChaosCastle)
                    instance.participants[victimIndex].alive = false;
            }

            if (instance.type == DungeonType.ChaosCastle && CountAlive(instance) <= 1)
            {
                instance.state = InstanceState.Completed;
                FinalizeInstance(instance);
            }

            OnDungeonUpdated?.Invoke(instance);
            return true;
        }

        public bool AdvanceDevilSquareWave(string instanceId)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.type != DungeonType.DevilSquare)
                return false;

            instance.currentWave = Mathf.Clamp(instance.currentWave + 1, 1, 7);
            if (instance.currentWave >= 7)
            {
                instance.state = InstanceState.Completed;
                FinalizeInstance(instance);
            }

            OnDungeonUpdated?.Invoke(instance);
            return true;
        }

        public bool RegisterBloodCastleArkProgress(string instanceId, int scoreDelta)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.type != DungeonType.BloodCastle)
                return false;

            instance.capturedFlags += Mathf.Max(0, scoreDelta);
            if (instance.capturedFlags >= 1)
            {
                instance.state = InstanceState.Completed;
                FinalizeInstance(instance);
            }

            OnDungeonUpdated?.Invoke(instance);
            return true;
        }

        public bool CaptureIllusionTempleRelic(string instanceId, string playerName)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.type != DungeonType.IllusionTemple)
                return false;

            int index = FindParticipantIndex(instance, playerName);
            if (index < 0)
                return false;

            instance.participants[index].score += 100;
            instance.capturedFlags++;
            if (instance.capturedFlags >= 3)
            {
                instance.state = InstanceState.Completed;
                FinalizeInstance(instance);
            }

            OnDungeonUpdated?.Invoke(instance);
            return true;
        }

        public bool PickupChaosCastleCrown(string instanceId, string playerName)
        {
            if (!_instances.TryGetValue(instanceId, out DungeonInstance instance) || instance.type != DungeonType.ChaosCastle)
                return false;

            instance.crownCarrier = playerName;
            OnDungeonUpdated?.Invoke(instance);
            return true;
        }

        private void FinalizeInstance(DungeonInstance instance)
        {
            _chatSystem?.ReceiveSystemMessage($"{instance.type} {(instance.state == InstanceState.Completed ? "completed" : "failed")}.");
            _minimapSystem?.RemoveMarker($"event:dungeon:{instance.instanceId}");
            GrantDungeonRewards(instance);
            OnDungeonCompleted?.Invoke(instance);
        }

        private void GrantDungeonRewards(DungeonInstance instance)
        {
            for (int i = 0; i < instance.participants.Count; i++)
            {
                DungeonParticipant participant = instance.participants[i];
                int baseScore = instance.type switch
                {
                    DungeonType.BloodCastle => 120,
                    DungeonType.DevilSquare => 150 * Mathf.Max(1, instance.currentWave),
                    DungeonType.ChaosCastle => participant.alive ? 300 : 60,
                    DungeonType.IllusionTemple => 180 + participant.score,
                    _ => 50
                };
                participant.score += baseScore;
            }
        }

        private static int FindParticipantIndex(DungeonInstance instance, string playerName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(playerName))
                return -1;

            for (int i = 0; i < instance.participants.Count; i++)
            {
                if (string.Equals(instance.participants[i].playerName, playerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int CountAlive(DungeonInstance instance)
        {
            int alive = 0;
            for (int i = 0; i < instance.participants.Count; i++)
            {
                if (instance.participants[i].alive)
                    alive++;
            }

            return alive;
        }

        private static int ResolveKillScore(DungeonType type)
        {
            return type switch
            {
                DungeonType.ChaosCastle => 80,
                DungeonType.IllusionTemple => 40,
                DungeonType.BloodCastle => 25,
                _ => 15
            };
        }

        private float ResolveDuration(DungeonType type)
        {
            return type switch
            {
                DungeonType.BloodCastle => _bloodCastleDuration,
                DungeonType.DevilSquare => _devilSquareDuration,
                DungeonType.ChaosCastle => _chaosCastleDuration,
                _ => _illusionTempleDuration
            };
        }
    }
}