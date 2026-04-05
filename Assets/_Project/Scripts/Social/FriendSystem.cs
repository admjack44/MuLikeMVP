using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Gameplay.Controllers;
using MuLike.Inventory;
using MuLike.Navigation;
using MuLike.Systems;
using MuLike.World;
using UnityEngine;

namespace MuLike.Social
{
    /// <summary>
    /// Friends, soul-bounds, whisper routing, online state, and marriage system.
    /// </summary>
    public sealed class FriendSystem : MonoBehaviour
    {
        public enum FriendPresence
        {
            Offline,
            Online,
            Afk
        }

        [Serializable]
        public sealed class FriendEntry
        {
            public string name;
            public FriendPresence presence;
            public bool soulBound;
            public MapLoader.MapId lastKnownMap;
            public Vector3 lastKnownPosition;
            public float lastActivityAt;
        }

        [Serializable]
        public struct MarriageBond
        {
            public string spouseA;
            public string spouseB;
            public bool active;
            public float attackBonusPercent;
            public float defenseBonusPercent;
        }

        [Header("Friends")]
        [SerializeField, Min(1)] private int _maxFriends = 50;
        [SerializeField, Min(10f)] private float _afkThresholdSeconds = 180f;

        [Header("Marriage")]
        [SerializeField, Min(1)] private int _marriageRingItemId = 9100;
        [SerializeField, Min(0)] private int _divorceZenCost = 150000;
        [SerializeField] private int _localZenBalance = 500000;

        [Header("Dependencies")]
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private MuNavigator _navigator;
        [SerializeField] private MapLoader _mapLoader;
        [SerializeField] private CharacterMotor _characterMotor;

        private readonly List<FriendEntry> _friends = new();
        private MarriageBond _marriageBond;
        private string _localPlayerName = "Player";
        private float _lastLocalActivityAt;

        public IReadOnlyList<FriendEntry> Friends => _friends;
        public MarriageBond CurrentMarriage => _marriageBond;
        public bool IsMarried => _marriageBond.active;
        public int ZenBalance => _localZenBalance;

        public event Action OnFriendsChanged;
        public event Action<MarriageBond> OnMarriageChanged;

        private void Awake()
        {
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_navigator == null)
                _navigator = FindAnyObjectByType<MuNavigator>();
            if (_mapLoader == null)
                _mapLoader = FindAnyObjectByType<MapLoader>();
            if (_characterMotor == null)
                _characterMotor = FindAnyObjectByType<CharacterMotor>();

            _localPlayerName = _chatSystem != null ? _chatSystem.LocalPlayerName : gameObject.name;
            _lastLocalActivityAt = Time.unscaledTime;
            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            UpdateLocalAfkState();
        }

        public bool AddFriend(string playerName, FriendPresence presence = FriendPresence.Online)
        {
            playerName = ChatSanitizer.SanitizeName(playerName);
            if (string.IsNullOrWhiteSpace(playerName) || _friends.Count >= _maxFriends || IsFriend(playerName))
                return false;

            _friends.Add(new FriendEntry
            {
                name = playerName,
                presence = presence,
                lastKnownMap = _mapLoader != null ? _mapLoader.ActiveMapId : MapLoader.MapId.Lorencia,
                lastKnownPosition = Vector3.zero,
                lastActivityAt = Time.unscaledTime
            });

            OnFriendsChanged?.Invoke();
            return true;
        }

        public bool RemoveFriend(string playerName)
        {
            int index = FindFriendIndex(playerName);
            if (index < 0)
                return false;

            _friends.RemoveAt(index);
            OnFriendsChanged?.Invoke();
            return true;
        }

        public bool IsFriend(string playerName)
        {
            return FindFriendIndex(playerName) >= 0;
        }

        public bool UpdatePresence(string playerName, FriendPresence presence, Vector3 lastKnownPosition, MapLoader.MapId lastKnownMap)
        {
            int index = FindFriendIndex(playerName);
            if (index < 0)
                return false;

            _friends[index].presence = presence;
            _friends[index].lastKnownPosition = lastKnownPosition;
            _friends[index].lastKnownMap = lastKnownMap;
            _friends[index].lastActivityAt = Time.unscaledTime;
            OnFriendsChanged?.Invoke();
            return true;
        }

        public bool SetSoulBound(string playerName, bool enabled)
        {
            int index = FindFriendIndex(playerName);
            if (index < 0)
                return false;

            _friends[index].soulBound = enabled;
            OnFriendsChanged?.Invoke();
            return true;
        }

        public Task<bool> SendWhisperAsync(string playerName, string text, Action<string> onError = null)
        {
            if (_chatSystem == null)
                return Task.FromResult(false);

            return _chatSystem.SendWhisperAsync(playerName, text, onError);
        }

        public bool TryTeleportToSoulBoundFriend(string playerName)
        {
            int index = FindFriendIndex(playerName);
            if (index < 0)
                return false;

            FriendEntry friend = _friends[index];
            if (!friend.soulBound || friend.presence == FriendPresence.Offline)
                return false;

            if (_mapLoader != null && _mapLoader.ActiveMapId != friend.lastKnownMap)
                _mapLoader.TransitionToMap(friend.lastKnownMap);

            if (_characterMotor != null)
            {
                _characterMotor.transform.position = friend.lastKnownPosition;
                _characterMotor.Stop();
                return true;
            }

            return _navigator != null && _navigator.NavigateTo(friend.lastKnownPosition);
        }

        public bool TryMarry(string partnerName)
        {
            partnerName = ChatSanitizer.SanitizeName(partnerName);
            if (IsMarried || !IsFriend(partnerName))
                return false;

            if (!ConsumeMarriageRings(2))
                return false;

            _marriageBond = new MarriageBond
            {
                spouseA = _localPlayerName,
                spouseB = partnerName,
                active = true,
                attackBonusPercent = 0.05f,
                defenseBonusPercent = 0.05f
            };

            _chatSystem?.ReceiveSystemMessage($"Marriage bond formed with {partnerName}.");
            OnMarriageChanged?.Invoke(_marriageBond);
            return true;
        }

        public bool TryTeleportToSpouse()
        {
            if (!IsMarried)
                return false;

            string spouse = GetSpouseName();
            return TryTeleportToSoulBoundFriend(spouse);
        }

        public bool TryDivorce()
        {
            if (!IsMarried || _localZenBalance < _divorceZenCost)
                return false;

            _localZenBalance -= _divorceZenCost;
            _marriageBond.active = false;
            _chatSystem?.ReceiveSystemMessage("Marriage dissolved.");
            OnMarriageChanged?.Invoke(_marriageBond);
            return true;
        }

        public void NotifyLocalActivity()
        {
            _lastLocalActivityAt = Time.unscaledTime;
            UpdatePresence(_localPlayerName, FriendPresence.Online, _characterMotor != null ? _characterMotor.transform.position : Vector3.zero, _mapLoader != null ? _mapLoader.ActiveMapId : MapLoader.MapId.Lorencia);
        }

        public string GetSpouseName()
        {
            if (!IsMarried)
                return string.Empty;

            return string.Equals(_marriageBond.spouseA, _localPlayerName, StringComparison.OrdinalIgnoreCase)
                ? _marriageBond.spouseB
                : _marriageBond.spouseA;
        }

        private void UpdateLocalAfkState()
        {
            float elapsed = Time.unscaledTime - _lastLocalActivityAt;
            FriendPresence state = elapsed >= _afkThresholdSeconds ? FriendPresence.Afk : FriendPresence.Online;
            int index = FindFriendIndex(_localPlayerName);
            if (index >= 0 && _friends[index].presence != state)
            {
                _friends[index].presence = state;
                OnFriendsChanged?.Invoke();
            }
        }

        private bool ConsumeMarriageRings(int count)
        {
            if (_inventoryManager == null)
                return false;

            int consumed = 0;
            var entries = _inventoryManager.InventoryEntries;
            for (int i = 0; i < entries.Count && consumed < count; i++)
            {
                InventoryManager.InventoryItem item = entries[i]?.item;
                if (item == null || item.itemId != _marriageRingItemId)
                    continue;

                if (_inventoryManager.TryRemoveItem(item.instanceId, out _))
                    consumed++;
            }

            return consumed >= count;
        }

        private int FindFriendIndex(string playerName)
        {
            for (int i = 0; i < _friends.Count; i++)
            {
                if (string.Equals(_friends[i].name, playerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}