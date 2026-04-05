using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Gameplay.Controllers;
using MuLike.Systems;
using MuLike.UI;
using UnityEngine;

namespace MuLike.Social
{
    /// <summary>
    /// Party orchestration for MU Mobile.
    /// Supports 5-member parties, invites by proximity/friend list, configurable EXP sharing,
    /// composition buffs, minimap indicators, and chat/push integration.
    /// </summary>
    public sealed class PartyManager : MonoBehaviour
    {
        public enum ExperienceDistributionMode
        {
            Equal,
            DamageBased,
            LevelBased
        }

        public enum InviteSource
        {
            Proximity,
            FriendList
        }

        [Serializable]
        public sealed class PartyMember
        {
            public string name;
            public Transform worldTransform;
            public StatsClientSystem.CharacterClass classId;
            public int level;
            public bool isLeader;
            public bool isOnline = true;
            public Color markerColor;
            public float damageContribution;
        }

        [Serializable]
        public struct PartyInvite
        {
            public string inviter;
            public string invitee;
            public InviteSource source;
            public float expiresAt;
        }

        [Serializable]
        public struct PartyBuff
        {
            public string id;
            public string label;
            public float attackBonusPercent;
            public float defenseBonusPercent;
            public float experienceBonusPercent;
            public float moveSpeedBonusPercent;
        }

        [Serializable]
        public struct ExperienceShareResult
        {
            public string playerName;
            public long experienceGranted;
        }

        [Header("Party")]
        [SerializeField, Min(2)] private int _maxMembers = 5;
        [SerializeField, Min(2f)] private float _inviteDurationSeconds = 20f;
        [SerializeField, Min(2f)] private float _nearbyInviteRadius = 20f;
        [SerializeField] private ExperienceDistributionMode _distributionMode = ExperienceDistributionMode.Equal;

        [Header("Dependencies")]
        [SerializeField] private MinimapSystem _minimapSystem;
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private FriendSystem _friendSystem;

        private readonly List<PartyMember> _members = new();
        private readonly List<PartyInvite> _pendingInvites = new();
        private readonly List<PartyBuff> _activeBuffs = new();
        private readonly Color[] _partyColors =
        {
            new Color(0.23f, 0.84f, 1f),
            new Color(0.32f, 1f, 0.56f),
            new Color(1f, 0.74f, 0.24f),
            new Color(1f, 0.36f, 0.48f),
            new Color(0.82f, 0.50f, 1f)
        };

        private float _nextMarkerSyncAt;

        public IReadOnlyList<PartyMember> Members => _members;
        public IReadOnlyList<PartyInvite> PendingInvites => _pendingInvites;
        public IReadOnlyList<PartyBuff> ActiveBuffs => _activeBuffs;
        public bool HasParty => _members.Count > 0;
        public ExperienceDistributionMode DistributionMode => _distributionMode;

        public event Action OnPartyChanged;
        public event Action<IReadOnlyList<PartyBuff>> OnBuffsChanged;
        public event Action<IReadOnlyList<ExperienceShareResult>> OnExperienceDistributed;

        private void Awake()
        {
            if (_minimapSystem == null)
                _minimapSystem = FindAnyObjectByType<MinimapSystem>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_friendSystem == null)
                _friendSystem = FindAnyObjectByType<FriendSystem>();

            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            CleanupExpiredInvites();

            if (_members.Count > 0 && Time.unscaledTime >= _nextMarkerSyncAt)
            {
                _nextMarkerSyncAt = Time.unscaledTime + 0.25f;
                SyncPartyMarkers();
            }
        }

        public bool CreateParty(string leaderName, Transform leaderTransform, StatsClientSystem.CharacterClass classId, int level)
        {
            if (_members.Count > 0)
                return false;

            _members.Add(new PartyMember
            {
                name = leaderName,
                worldTransform = leaderTransform,
                classId = classId,
                level = Mathf.Max(1, level),
                isLeader = true,
                markerColor = _partyColors[0]
            });

            RecalculateBuffs();
            NotifyPartyState($"Party created by {leaderName}.");
            return true;
        }

        public int InviteNearby(string inviterName)
        {
            int invites = 0;
            CharacterMotor[] motors = FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None);
            Transform inviterTransform = FindMemberTransform(inviterName);
            if (inviterTransform == null)
                return 0;

            for (int i = 0; i < motors.Length && _members.Count + invites < _maxMembers; i++)
            {
                CharacterMotor motor = motors[i];
                if (motor == null || motor.transform == inviterTransform)
                    continue;

                if (Vector3.Distance(inviterTransform.position, motor.transform.position) > _nearbyInviteRadius)
                    continue;

                string candidate = motor.gameObject.name;
                if (IsMember(candidate) || HasPendingInvite(candidate))
                    continue;

                CreateInvite(inviterName, candidate, InviteSource.Proximity);
                invites++;
            }

            if (invites > 0)
                NotifyPartyState($"Sent {invites} nearby party invite(s).");

            return invites;
        }

        public bool InviteFriend(string inviterName, string friendName)
        {
            if (_friendSystem != null && !_friendSystem.IsFriend(friendName))
                return false;

            if (_members.Count >= _maxMembers || IsMember(friendName) || HasPendingInvite(friendName))
                return false;

            CreateInvite(inviterName, friendName, InviteSource.FriendList);
            NotifyPartyState($"Party invite sent to {friendName}.");
            return true;
        }

        public bool AcceptInvite(string inviteeName, Transform worldTransform, StatsClientSystem.CharacterClass classId, int level)
        {
            int index = FindInviteIndex(inviteeName);
            if (index < 0 || _members.Count >= _maxMembers)
                return false;

            _pendingInvites.RemoveAt(index);
            _members.Add(new PartyMember
            {
                name = inviteeName,
                worldTransform = worldTransform,
                classId = classId,
                level = Mathf.Max(1, level),
                markerColor = _partyColors[Mathf.Clamp(_members.Count, 0, _partyColors.Length - 1)]
            });

            RecalculateBuffs();
            NotifyPartyState($"{inviteeName} joined the party.");
            return true;
        }

        public bool DeclineInvite(string inviteeName)
        {
            int index = FindInviteIndex(inviteeName);
            if (index < 0)
                return false;

            _pendingInvites.RemoveAt(index);
            NotifyPartyState($"{inviteeName} declined the party invite.");
            return true;
        }

        public bool RemoveMember(string name)
        {
            int index = FindMemberIndex(name);
            if (index < 0)
                return false;

            string markerId = BuildMarkerId(name);
            _minimapSystem?.RemoveMarker(markerId);

            bool wasLeader = _members[index].isLeader;
            _members.RemoveAt(index);
            if (wasLeader && _members.Count > 0)
                _members[0].isLeader = true;

            RecalculateBuffs();
            NotifyPartyState($"{name} left the party.");
            return true;
        }

        public void SetDistributionMode(ExperienceDistributionMode mode)
        {
            _distributionMode = mode;
            NotifyPartyState($"Party EXP distribution set to {mode}.");
        }

        public void RegisterDamage(string playerName, float damage)
        {
            int index = FindMemberIndex(playerName);
            if (index < 0)
                return;

            _members[index].damageContribution = Mathf.Max(0f, _members[index].damageContribution + damage);
        }

        public IReadOnlyList<ExperienceShareResult> DistributeExperience(long totalExperience)
        {
            var results = new List<ExperienceShareResult>(_members.Count);
            if (_members.Count == 0 || totalExperience <= 0)
            {
                OnExperienceDistributed?.Invoke(results);
                return results;
            }

            float totalWeight = ResolveTotalWeight();
            float expBuff = 1f;
            for (int i = 0; i < _activeBuffs.Count; i++)
                expBuff += Mathf.Max(0f, _activeBuffs[i].experienceBonusPercent);

            for (int i = 0; i < _members.Count; i++)
            {
                PartyMember member = _members[i];
                float weight = ResolveMemberWeight(member, totalWeight);
                long granted = (long)Mathf.Round(totalExperience * weight * expBuff);
                results.Add(new ExperienceShareResult
                {
                    playerName = member.name,
                    experienceGranted = Math.Max(1, granted)
                });
                _members[i].damageContribution = 0f;
            }

            OnExperienceDistributed?.Invoke(results);
            return results;
        }

        public bool IsMember(string playerName)
        {
            return FindMemberIndex(playerName) >= 0;
        }

        private void CreateInvite(string inviterName, string inviteeName, InviteSource source)
        {
            _pendingInvites.Add(new PartyInvite
            {
                inviter = inviterName,
                invitee = inviteeName,
                source = source,
                expiresAt = Time.unscaledTime + _inviteDurationSeconds
            });
        }

        private void RecalculateBuffs()
        {
            _activeBuffs.Clear();

            bool hasKnight = false;
            bool hasElf = false;
            bool hasWizard = false;
            var distinctClasses = new HashSet<StatsClientSystem.CharacterClass>();
            for (int i = 0; i < _members.Count; i++)
            {
                StatsClientSystem.CharacterClass cls = _members[i].classId;
                distinctClasses.Add(cls);
                hasKnight |= cls == StatsClientSystem.CharacterClass.DarkKnight;
                hasElf |= cls == StatsClientSystem.CharacterClass.FairyElf;
                hasWizard |= cls == StatsClientSystem.CharacterClass.DarkWizard;
            }

            if (hasKnight && hasElf)
            {
                _activeBuffs.Add(new PartyBuff
                {
                    id = "shield_wall",
                    label = "Shield Wall",
                    defenseBonusPercent = 0.08f
                });
            }

            if (hasKnight && hasWizard)
            {
                _activeBuffs.Add(new PartyBuff
                {
                    id = "arcane_assault",
                    label = "Arcane Assault",
                    attackBonusPercent = 0.08f
                });
            }

            if (distinctClasses.Count >= 3)
            {
                _activeBuffs.Add(new PartyBuff
                {
                    id = "expedition",
                    label = "Expedition Bonus",
                    experienceBonusPercent = 0.10f
                });
            }

            if (_members.Count >= _maxMembers)
            {
                _activeBuffs.Add(new PartyBuff
                {
                    id = "marching_order",
                    label = "Marching Order",
                    moveSpeedBonusPercent = 0.05f
                });
            }

            OnBuffsChanged?.Invoke(_activeBuffs);
            OnPartyChanged?.Invoke();
        }

        private void SyncPartyMarkers()
        {
            if (_minimapSystem == null)
                return;

            for (int i = 0; i < _members.Count; i++)
            {
                PartyMember member = _members[i];
                if (member.worldTransform == null)
                    continue;

                _minimapSystem.UpsertMarker(new MinimapSystem.MinimapMarker
                {
                    id = BuildMarkerId(member.name),
                    type = MinimapSystem.MarkerType.Party,
                    target = member.worldTransform,
                    worldPositionFallback = member.worldTransform.position,
                    label = member.name,
                    visible = true,
                    useCustomColor = true,
                    markerColor = member.markerColor
                });
            }
        }

        private void CleanupExpiredInvites()
        {
            float now = Time.unscaledTime;
            for (int i = _pendingInvites.Count - 1; i >= 0; i--)
            {
                if (_pendingInvites[i].expiresAt > now)
                    continue;

                _pendingInvites.RemoveAt(i);
            }
        }

        private float ResolveTotalWeight()
        {
            if (_distributionMode == ExperienceDistributionMode.Equal)
                return Mathf.Max(1, _members.Count);

            float total = 0f;
            for (int i = 0; i < _members.Count; i++)
            {
                total += _distributionMode switch
                {
                    ExperienceDistributionMode.DamageBased => Mathf.Max(1f, _members[i].damageContribution),
                    ExperienceDistributionMode.LevelBased => Mathf.Max(1f, _members[i].level),
                    _ => 1f
                };
            }

            return Mathf.Max(1f, total);
        }

        private float ResolveMemberWeight(PartyMember member, float totalWeight)
        {
            if (_distributionMode == ExperienceDistributionMode.Equal)
                return 1f / Mathf.Max(1, _members.Count);

            float own = _distributionMode switch
            {
                ExperienceDistributionMode.DamageBased => Mathf.Max(1f, member.damageContribution),
                ExperienceDistributionMode.LevelBased => Mathf.Max(1f, member.level),
                _ => 1f
            };

            return own / Mathf.Max(1f, totalWeight);
        }

        private void NotifyPartyState(string message)
        {
            _chatSystem?.ReceiveSystemMessage(message);
            OnPartyChanged?.Invoke();
        }

        private int FindInviteIndex(string inviteeName)
        {
            for (int i = 0; i < _pendingInvites.Count; i++)
            {
                if (string.Equals(_pendingInvites[i].invitee, inviteeName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private bool HasPendingInvite(string inviteeName)
        {
            return FindInviteIndex(inviteeName) >= 0;
        }

        private int FindMemberIndex(string playerName)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (string.Equals(_members[i].name, playerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private Transform FindMemberTransform(string playerName)
        {
            int index = FindMemberIndex(playerName);
            return index >= 0 ? _members[index].worldTransform : null;
        }

        private static string BuildMarkerId(string playerName)
        {
            return $"party:{playerName}";
        }
    }
}