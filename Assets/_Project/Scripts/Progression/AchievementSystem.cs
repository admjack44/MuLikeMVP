using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Inventory;
using MuLike.Social;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Progression
{
    /// <summary>
    /// Achievement, ranking, weekly rewards, and hall-of-fame runtime.
    /// </summary>
    public sealed class AchievementSystem : MonoBehaviour
    {
        public enum AchievementCategory
        {
            Progression,
            Combat,
            Social,
            Endgame
        }

        public enum RankingType
        {
            TopLevels,
            TopPk,
            TopGuilds
        }

        [Serializable]
        public struct AchievementReward
        {
            public string visibleTitle;
            public int itemId;
            public int statPoints;
        }

        [Serializable]
        public sealed class AchievementDefinition
        {
            public string id;
            public string title;
            public AchievementCategory category;
            public int targetValue;
            public AchievementReward reward;
        }

        [Serializable]
        public sealed class AchievementProgress
        {
            public string achievementId;
            public int progress;
            public bool unlocked;
            public bool claimed;
        }

        [Serializable]
        public sealed class RankingEntry
        {
            public string subjectId;
            public string displayName;
            public int score;
            public string visibleTitle;
        }

        [Serializable]
        public sealed class HallOfFamePedestal
        {
            public string pedestalId;
            public string displayName;
            public Vector3 cityPosition;
            public RankingType rankingType;
        }

        [Header("Rewards")]
        [SerializeField] private AchievementDefinition[] _definitions = Array.Empty<AchievementDefinition>();
        [SerializeField] private HallOfFamePedestal[] _pedestals = Array.Empty<HallOfFamePedestal>();
        [SerializeField, Min(1)] private int _defaultRewardItemId = 8100;

        [Header("Dependencies")]
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private StatsClientSystem _statsSystem;
        [SerializeField] private FriendSystem _friendSystem;
        [SerializeField] private PartyManager _partyManager;
        [SerializeField] private GuildManager _guildManager;

        private readonly Dictionary<string, AchievementProgress> _progress = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<RankingType, List<RankingEntry>> _rankings = new();
        private readonly Dictionary<string, string> _visibleTitles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _monsterKills = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _pkCounts = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, AchievementProgress> Progress => _progress;
        public IReadOnlyDictionary<RankingType, List<RankingEntry>> Rankings => _rankings;

        public event Action<AchievementDefinition, AchievementProgress> OnAchievementUnlocked;
        public event Action<RankingType, IReadOnlyList<RankingEntry>> OnRankingUpdated;
        public event Action<HallOfFamePedestal, RankingEntry> OnHallOfFameUpdated;

        private void Awake()
        {
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_statsSystem == null)
                GameContext.TryGetSystem(out _statsSystem);
            if (_friendSystem == null)
                _friendSystem = FindAnyObjectByType<FriendSystem>();
            if (_partyManager == null)
                _partyManager = FindAnyObjectByType<PartyManager>();
            if (_guildManager == null)
                _guildManager = FindAnyObjectByType<GuildManager>();

            SeedDefaultDefinitions();
            SubscribeSocialSignals();
            GameContext.RegisterSystem(this);
        }

        private void OnDestroy()
        {
            if (_statsSystem != null)
                _statsSystem.OnLevelChanged -= HandleLevelChanged;
            if (_partyManager != null)
                _partyManager.OnPartyChanged -= HandlePartyChanged;
            if (_friendSystem != null)
                _friendSystem.OnFriendsChanged -= HandleFriendsChanged;
            if (_guildManager != null)
                _guildManager.OnGuildChanged -= HandleGuildChanged;
        }

        public void RegisterMonsterKill(string playerName, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return;

            _monsterKills.TryGetValue(playerName, out int total);
            total += Mathf.Max(1, amount);
            _monsterKills[playerName] = total;
            IncrementProgress("kill_100_monsters", amount);
        }

        public void RegisterPkKill(string playerName, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return;

            _pkCounts.TryGetValue(playerName, out int total);
            total += Mathf.Max(1, amount);
            _pkCounts[playerName] = total;
            RebuildRankings();
        }

        public void RegisterEndgameClear(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            IncrementProgress("clear_endgame_10", 1);
        }

        public bool ClaimReward(string achievementId)
        {
            if (!_progress.TryGetValue(achievementId, out AchievementProgress progress) || !progress.unlocked || progress.claimed)
                return false;

            AchievementDefinition def = FindDefinition(achievementId);
            if (def == null)
                return false;

            progress.claimed = true;
            if (_inventoryManager != null)
            {
                _inventoryManager.TryAddItem(new InventoryManager.InventoryItem
                {
                    instanceId = Guid.NewGuid().ToString("N"),
                    itemId = def.reward.itemId <= 0 ? _defaultRewardItemId : def.reward.itemId,
                    displayName = def.reward.visibleTitle,
                    size = InventoryManager.ItemSize.OneByOne,
                    quantity = 1,
                    rarity = InventoryManager.InventoryRarity.Rare
                });
            }

            if (!string.IsNullOrWhiteSpace(def.reward.visibleTitle))
                _visibleTitles[GetLocalPlayerName()] = def.reward.visibleTitle;

            _chatSystem?.ReceiveSystemMessage($"Achievement reward claimed: {def.title}.");
            RebuildRankings();
            return true;
        }

        public string GetVisibleTitle(string playerName)
        {
            return _visibleTitles.TryGetValue(playerName, out string title) ? title : string.Empty;
        }

        public IReadOnlyList<RankingEntry> GetRanking(RankingType type)
        {
            if (_rankings.TryGetValue(type, out List<RankingEntry> board))
                return board;
            return Array.Empty<RankingEntry>();
        }

        public void GrantWeeklyRankingRewards()
        {
            foreach (RankingType type in Enum.GetValues(typeof(RankingType)))
            {
                IReadOnlyList<RankingEntry> board = GetRanking(type);
                for (int i = 0; i < Mathf.Min(3, board.Count); i++)
                {
                    if (_inventoryManager == null)
                        continue;

                    _inventoryManager.TryAddItem(new InventoryManager.InventoryItem
                    {
                        instanceId = Guid.NewGuid().ToString("N"),
                        itemId = _defaultRewardItemId + i,
                        displayName = $"{type} Weekly Reward #{i + 1}",
                        quantity = 1,
                        size = InventoryManager.ItemSize.OneByOne,
                        rarity = InventoryManager.InventoryRarity.Epic
                    });
                }
            }

            _chatSystem?.ReceiveSystemMessage("Weekly ranking rewards granted.");
        }

        private void SeedDefaultDefinitions()
        {
            if (_definitions != null && _definitions.Length > 0)
            {
                for (int i = 0; i < _definitions.Length; i++)
                    EnsureProgressRecord(_definitions[i]);
                return;
            }

            _definitions = new[]
            {
                new AchievementDefinition
                {
                    id = "reach_level_100",
                    title = "Century Hero",
                    category = AchievementCategory.Progression,
                    targetValue = 100,
                    reward = new AchievementReward { visibleTitle = "Century Hero", itemId = 8201, statPoints = 10 }
                },
                new AchievementDefinition
                {
                    id = "kill_100_monsters",
                    title = "Monster Hunter",
                    category = AchievementCategory.Combat,
                    targetValue = 100,
                    reward = new AchievementReward { visibleTitle = "Monster Hunter", itemId = 8202, statPoints = 5 }
                },
                new AchievementDefinition
                {
                    id = "make_10_friends",
                    title = "Social Magnet",
                    category = AchievementCategory.Social,
                    targetValue = 10,
                    reward = new AchievementReward { visibleTitle = "Social Magnet", itemId = 8203, statPoints = 3 }
                },
                new AchievementDefinition
                {
                    id = "join_party_5",
                    title = "Expeditioner",
                    category = AchievementCategory.Social,
                    targetValue = 5,
                    reward = new AchievementReward { visibleTitle = "Expeditioner", itemId = 8204, statPoints = 3 }
                },
                new AchievementDefinition
                {
                    id = "join_guild_1",
                    title = "Guildsworn",
                    category = AchievementCategory.Social,
                    targetValue = 1,
                    reward = new AchievementReward { visibleTitle = "Guildsworn", itemId = 8205, statPoints = 2 }
                },
                new AchievementDefinition
                {
                    id = "clear_endgame_10",
                    title = "Endgame Vanguard",
                    category = AchievementCategory.Endgame,
                    targetValue = 10,
                    reward = new AchievementReward { visibleTitle = "Endgame Vanguard", itemId = 8206, statPoints = 8 }
                }
            };

            for (int i = 0; i < _definitions.Length; i++)
                EnsureProgressRecord(_definitions[i]);
        }

        private void SubscribeSocialSignals()
        {
            if (_statsSystem != null)
                _statsSystem.OnLevelChanged += HandleLevelChanged;
            if (_partyManager != null)
                _partyManager.OnPartyChanged += HandlePartyChanged;
            if (_friendSystem != null)
                _friendSystem.OnFriendsChanged += HandleFriendsChanged;
            if (_guildManager != null)
                _guildManager.OnGuildChanged += HandleGuildChanged;
        }

        private void HandleLevelChanged(int _, int newLevel)
        {
            SetProgress("reach_level_100", newLevel);
            RebuildRankings();
        }

        private void HandlePartyChanged()
        {
            IncrementProgress("join_party_5", _partyManager != null && _partyManager.HasParty ? 1 : 0);
        }

        private void HandleFriendsChanged()
        {
            int count = _friendSystem != null ? _friendSystem.Friends.Count : 0;
            SetProgress("make_10_friends", count);
        }

        private void HandleGuildChanged()
        {
            if (_guildManager != null && _guildManager.HasGuild)
                SetProgress("join_guild_1", 1);
            RebuildRankings();
        }

        private void IncrementProgress(string achievementId, int amount)
        {
            AchievementDefinition def = FindDefinition(achievementId);
            if (def == null)
                return;

            AchievementProgress progress = _progress[achievementId];
            progress.progress += Mathf.Max(0, amount);
            TryUnlock(def, progress);
            _progress[achievementId] = progress;
        }

        private void SetProgress(string achievementId, int value)
        {
            AchievementDefinition def = FindDefinition(achievementId);
            if (def == null)
                return;

            AchievementProgress progress = _progress[achievementId];
            progress.progress = Mathf.Max(progress.progress, value);
            TryUnlock(def, progress);
            _progress[achievementId] = progress;
        }

        private void TryUnlock(AchievementDefinition def, AchievementProgress progress)
        {
            if (!progress.unlocked && progress.progress >= def.targetValue)
            {
                progress.unlocked = true;
                _progress[def.id] = progress;
                _chatSystem?.ReceiveSystemMessage($"Achievement unlocked: {def.title}.");
                OnAchievementUnlocked?.Invoke(def, progress);
            }
        }

        private void EnsureProgressRecord(AchievementDefinition def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.id) || _progress.ContainsKey(def.id))
                return;

            _progress[def.id] = new AchievementProgress
            {
                achievementId = def.id,
                progress = 0,
                unlocked = false,
                claimed = false
            };
        }

        private AchievementDefinition FindDefinition(string achievementId)
        {
            for (int i = 0; i < _definitions.Length; i++)
            {
                if (string.Equals(_definitions[i].id, achievementId, StringComparison.OrdinalIgnoreCase))
                    return _definitions[i];
            }

            return null;
        }

        private void RebuildRankings()
        {
            string localPlayer = GetLocalPlayerName();
            int level = _statsSystem != null ? _statsSystem.Snapshot.Primary.Level : 1;

            _rankings[RankingType.TopLevels] = new List<RankingEntry>
            {
                new RankingEntry { subjectId = localPlayer, displayName = localPlayer, score = level, visibleTitle = GetVisibleTitle(localPlayer) }
            };

            int pkScore = _pkCounts.TryGetValue(localPlayer, out int totalPk) ? totalPk : 0;
            _rankings[RankingType.TopPk] = new List<RankingEntry>
            {
                new RankingEntry { subjectId = localPlayer, displayName = localPlayer, score = pkScore, visibleTitle = GetVisibleTitle(localPlayer) }
            };

            int guildScore = _guildManager != null && _guildManager.HasGuild ? _guildManager.Members.Count * 100 : 0;
            _rankings[RankingType.TopGuilds] = new List<RankingEntry>
            {
                new RankingEntry
                {
                    subjectId = _guildManager != null ? _guildManager.GuildName : "NoGuild",
                    displayName = _guildManager != null ? _guildManager.GuildName : "NoGuild",
                    score = guildScore,
                    visibleTitle = "Hall Owner"
                }
            };

            foreach (KeyValuePair<RankingType, List<RankingEntry>> kv in _rankings)
            {
                kv.Value.Sort((a, b) => b.score.CompareTo(a.score));
                OnRankingUpdated?.Invoke(kv.Key, kv.Value);
                UpdateHallOfFame(kv.Key, kv.Value);
            }
        }

        private void UpdateHallOfFame(RankingType type, IReadOnlyList<RankingEntry> board)
        {
            if (board == null || board.Count == 0)
                return;

            RankingEntry top = board[0];
            for (int i = 0; i < _pedestals.Length; i++)
            {
                if (_pedestals[i] == null || _pedestals[i].rankingType != type)
                    continue;

                OnHallOfFameUpdated?.Invoke(_pedestals[i], top);
            }
        }

        private string GetLocalPlayerName()
        {
            return _chatSystem != null ? _chatSystem.LocalPlayerName : "Player";
        }
    }
}