using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Inventory;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Social
{
    /// <summary>
    /// Guild runtime with creation requirements, ranks, shared storage, guild wars, notice, and emblem upload.
    /// </summary>
    public sealed class GuildManager : MonoBehaviour
    {
        public enum GuildRank
        {
            GuildMaster,
            Assistant,
            BattleMaster,
            Member
        }

        public enum GuildWarStatus
        {
            Pending,
            Active,
            Ended
        }

        [Serializable]
        public sealed class GuildMember
        {
            public string name;
            public GuildRank rank;
            public StatsClientSystem.CharacterClass classId;
            public int level;
            public bool online;
        }

        [Serializable]
        public sealed class GuildWar
        {
            public string enemyGuildName;
            public string declaredBy;
            public GuildWarStatus status;
            public long startedAtUnixMs;
            public bool freePvpEnabled;
        }

        [Serializable]
        public sealed class GuildStorageEntry
        {
            public InventoryManager.InventoryItem item;
            public string depositedBy;
            public long depositedAtUnixMs;
        }

        [Header("Creation")]
        [SerializeField, Min(1)] private int _guildCreationItemId = 7001;
        [SerializeField, Min(50)] private int _requiredLevel = 50;

        [Header("Storage")]
        [SerializeField, Min(40)] private int _storageCapacity = 80;

        [Header("Dependencies")]
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private StatsClientSystem _statsSystem;

        private readonly List<GuildMember> _members = new();
        private readonly List<GuildWar> _guildWars = new();
        private readonly List<GuildStorageEntry> _storage = new();

        public string GuildName { get; private set; }
        public string GuildNotice { get; private set; }
        public Texture2D GuildEmblem { get; private set; }
        public bool HasGuild => !string.IsNullOrWhiteSpace(GuildName);
        public IReadOnlyList<GuildMember> Members => _members;
        public IReadOnlyList<GuildWar> GuildWars => _guildWars;
        public IReadOnlyList<GuildStorageEntry> Storage => _storage;

        public event Action OnGuildChanged;
        public event Action<GuildWar> OnGuildWarChanged;
        public event Action<Texture2D> OnEmblemChanged;

        private void Awake()
        {
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_statsSystem == null)
                GameContext.TryGetSystem(out _statsSystem);

            GameContext.RegisterSystem(this);
        }

        public bool TryCreateGuild(string guildName, string guildMasterName, StatsClientSystem.CharacterClass classId)
        {
            guildName = ChatSanitizer.SanitizeName(guildName, 16);
            guildMasterName = ChatSanitizer.SanitizeName(guildMasterName);
            if (HasGuild || string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(guildMasterName))
                return false;

            int level = _statsSystem != null ? _statsSystem.Snapshot.Primary.Level : 1;
            if (level < _requiredLevel)
                return false;

            if (!ConsumeRequiredGuildItem())
                return false;

            GuildName = guildName;
            GuildNotice = "Welcome to the guild.";
            _members.Clear();
            _members.Add(new GuildMember
            {
                name = guildMasterName,
                rank = GuildRank.GuildMaster,
                classId = classId,
                level = level,
                online = true
            });

            _chatSystem?.ReceiveSystemMessage($"Guild {GuildName} created.");
            OnGuildChanged?.Invoke();
            return true;
        }

        public bool AddMember(string playerName, StatsClientSystem.CharacterClass classId, int level)
        {
            if (!HasGuild || FindMemberIndex(playerName) >= 0)
                return false;

            _members.Add(new GuildMember
            {
                name = ChatSanitizer.SanitizeName(playerName),
                rank = GuildRank.Member,
                classId = classId,
                level = Mathf.Max(1, level),
                online = true
            });

            OnGuildChanged?.Invoke();
            return true;
        }

        public bool SetRank(string operatorName, string targetName, GuildRank rank)
        {
            int operatorIndex = FindMemberIndex(operatorName);
            int targetIndex = FindMemberIndex(targetName);
            if (operatorIndex < 0 || targetIndex < 0)
                return false;

            if (_members[operatorIndex].rank != GuildRank.GuildMaster)
                return false;

            _members[targetIndex].rank = rank;
            OnGuildChanged?.Invoke();
            return true;
        }

        public bool TrySetNotice(string operatorName, string notice)
        {
            int operatorIndex = FindMemberIndex(operatorName);
            if (operatorIndex < 0 || _members[operatorIndex].rank != GuildRank.GuildMaster)
                return false;

            GuildNotice = ChatSanitizer.SanitizeText(notice);
            _chatSystem?.ReceiveSystemMessage($"Guild notice updated: {GuildNotice}");
            OnGuildChanged?.Invoke();
            return true;
        }

        public bool TrySetEmblem(Texture2D emblemTexture)
        {
            if (!HasGuild || emblemTexture == null)
                return false;

            if (emblemTexture.width != 64 || emblemTexture.height != 64)
                return false;

            Texture2D runtimeCopy = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            runtimeCopy.SetPixels(emblemTexture.GetPixels());
            runtimeCopy.Apply();
            GuildEmblem = runtimeCopy;

            OnEmblemChanged?.Invoke(GuildEmblem);
            return true;
        }

        public bool DepositToStorage(string depositedBy, string itemInstanceId)
        {
            if (!HasGuild || _inventoryManager == null || _storage.Count >= _storageCapacity)
                return false;

            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem removed) || removed == null)
                return false;

            _storage.Add(new GuildStorageEntry
            {
                item = removed,
                depositedBy = ChatSanitizer.SanitizeName(depositedBy),
                depositedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            OnGuildChanged?.Invoke();
            return true;
        }

        public bool WithdrawFromStorage(string withdrawnBy, string itemInstanceId)
        {
            if (!HasGuild || _inventoryManager == null)
                return false;

            int memberIndex = FindMemberIndex(withdrawnBy);
            if (memberIndex < 0)
                return false;

            GuildRank rank = _members[memberIndex].rank;
            if (rank == GuildRank.Member)
                return false;

            int storageIndex = FindStorageIndex(itemInstanceId);
            if (storageIndex < 0)
                return false;

            GuildStorageEntry entry = _storage[storageIndex];
            if (!_inventoryManager.TryAddItem(entry.item))
                return false;

            _storage.RemoveAt(storageIndex);
            OnGuildChanged?.Invoke();
            return true;
        }

        public bool DeclareGuildWar(string operatorName, string enemyGuildName)
        {
            int memberIndex = FindMemberIndex(operatorName);
            if (memberIndex < 0)
                return false;

            GuildRank rank = _members[memberIndex].rank;
            if (rank != GuildRank.GuildMaster && rank != GuildRank.BattleMaster)
                return false;

            var war = new GuildWar
            {
                enemyGuildName = ChatSanitizer.SanitizeName(enemyGuildName, 16),
                declaredBy = ChatSanitizer.SanitizeName(operatorName),
                status = GuildWarStatus.Active,
                startedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                freePvpEnabled = true
            };

            _guildWars.Add(war);
            _chatSystem?.ReceiveSystemMessage($"Guild war started against {war.enemyGuildName}.");
            OnGuildWarChanged?.Invoke(war);
            return true;
        }

        public bool CanFreePvp(string ownGuildName, string otherGuildName)
        {
            for (int i = 0; i < _guildWars.Count; i++)
            {
                GuildWar war = _guildWars[i];
                if (war.status != GuildWarStatus.Active || !war.freePvpEnabled)
                    continue;

                if (string.Equals(GuildName, ownGuildName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(war.enemyGuildName, otherGuildName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsMember(string playerName)
        {
            return FindMemberIndex(playerName) >= 0;
        }

        private bool ConsumeRequiredGuildItem()
        {
            if (_inventoryManager == null)
                return false;

            var entries = _inventoryManager.InventoryEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryManager.InventoryItem item = entries[i]?.item;
                if (item == null || item.itemId != _guildCreationItemId)
                    continue;

                return _inventoryManager.TryRemoveItem(item.instanceId, out _);
            }

            return false;
        }

        private int FindMemberIndex(string name)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private int FindStorageIndex(string itemInstanceId)
        {
            for (int i = 0; i < _storage.Count; i++)
            {
                if (_storage[i].item != null && string.Equals(_storage[i].item.instanceId, itemInstanceId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}