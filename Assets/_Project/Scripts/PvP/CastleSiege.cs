using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Social;
using UnityEngine;

namespace MuLike.PvP
{
    /// <summary>
    /// Weekly Castle Siege runtime.
    /// Saturday siege scheduling, attack/defense state, siege weapons, and market tax ownership.
    /// </summary>
    public sealed class CastleSiege : MonoBehaviour
    {
        public enum SiegeState
        {
            Scheduled,
            Preparation,
            Running,
            Resolved
        }

        public enum SiegeWeaponType
        {
            Cannon,
            Ballista,
            Ram,
            FlameTower
        }

        [Serializable]
        public sealed class SiegeWeapon
        {
            public string weaponId;
            public SiegeWeaponType type;
            public string controllingGuild;
            public float cooldownEndsAt;
            public int ammo = 10;
        }

        [Serializable]
        public sealed class SiegeSnapshot
        {
            public string defenderGuild;
            public readonly List<string> attackerGuilds = new();
            public SiegeState state;
            public float startsAt;
            public float endsAt;
            public string crownHolderGuild;
            public float marketTaxRate;
        }

        [Header("Schedule")]
        [SerializeField] private int _siegeStartHourSaturday = 20;
        [SerializeField, Min(1800f)] private float _siegeDurationSeconds = 7200f;
        [SerializeField, Range(0f, 0.30f)] private float _defaultMarketTax = 0.05f;

        [Header("Dependencies")]
        [SerializeField] private GuildManager _guildManager;
        [SerializeField] private ChatSystem _chatSystem;

        private readonly List<SiegeWeapon> _weapons = new();
        private readonly SiegeSnapshot _snapshot = new();

        public SiegeSnapshot Snapshot => _snapshot;
        public IReadOnlyList<SiegeWeapon> Weapons => _weapons;

        public event Action<SiegeSnapshot> OnSiegeUpdated;
        public event Action<SiegeWeapon> OnSiegeWeaponFired;

        private void Awake()
        {
            if (_guildManager == null)
                _guildManager = FindAnyObjectByType<GuildManager>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();

            ScheduleNextSiege();
            SeedWeapons();
            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (_snapshot.state == SiegeState.Scheduled && now >= _snapshot.startsAt - 1800f)
            {
                _snapshot.state = SiegeState.Preparation;
                _chatSystem?.ReceiveSystemMessage("Castle Siege enters preparation phase.");
                OnSiegeUpdated?.Invoke(_snapshot);
            }

            if ((_snapshot.state == SiegeState.Scheduled || _snapshot.state == SiegeState.Preparation) && now >= _snapshot.startsAt)
            {
                _snapshot.state = SiegeState.Running;
                _chatSystem?.ReceiveSystemMessage("Castle Siege has started.");
                OnSiegeUpdated?.Invoke(_snapshot);
            }

            if (_snapshot.state == SiegeState.Running && now >= _snapshot.endsAt)
            {
                _snapshot.state = SiegeState.Resolved;
                _chatSystem?.ReceiveSystemMessage($"Castle Siege resolved. Owner: {_snapshot.crownHolderGuild}.");
                OnSiegeUpdated?.Invoke(_snapshot);
                ScheduleNextSiege();
            }
        }

        public bool SetDefenderGuild(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
                return false;

            _snapshot.defenderGuild = guildName;
            _snapshot.crownHolderGuild = guildName;
            OnSiegeUpdated?.Invoke(_snapshot);
            return true;
        }

        public bool RegisterAttackerGuild(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName) || _snapshot.attackerGuilds.Contains(guildName))
                return false;

            _snapshot.attackerGuilds.Add(guildName);
            OnSiegeUpdated?.Invoke(_snapshot);
            return true;
        }

        public bool CaptureCrown(string guildName)
        {
            if (_snapshot.state != SiegeState.Running || string.IsNullOrWhiteSpace(guildName))
                return false;

            if (!_snapshot.attackerGuilds.Contains(guildName) && !string.Equals(_snapshot.defenderGuild, guildName, StringComparison.OrdinalIgnoreCase))
                return false;

            _snapshot.crownHolderGuild = guildName;
            _chatSystem?.ReceiveSystemMessage($"{guildName} captured the castle crown.");
            OnSiegeUpdated?.Invoke(_snapshot);
            return true;
        }

        public bool UseSiegeWeapon(string weaponId, string guildName)
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                SiegeWeapon weapon = _weapons[i];
                if (!string.Equals(weapon.weaponId, weaponId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (weapon.ammo <= 0 || weapon.cooldownEndsAt > Time.unscaledTime)
                    return false;

                weapon.controllingGuild = guildName;
                weapon.ammo--;
                weapon.cooldownEndsAt = Time.unscaledTime + 20f;
                _chatSystem?.ReceiveSystemMessage($"{guildName} fired {weapon.type}.");
                OnSiegeWeaponFired?.Invoke(weapon);
                return true;
            }

            return false;
        }

        public bool SetMarketTaxRate(string guildName, float taxRate)
        {
            if (!string.Equals(_snapshot.crownHolderGuild, guildName, StringComparison.OrdinalIgnoreCase))
                return false;

            _snapshot.marketTaxRate = Mathf.Clamp(taxRate, 0f, 0.30f);
            OnSiegeUpdated?.Invoke(_snapshot);
            return true;
        }

        private void ScheduleNextSiege()
        {
            DateTime now = DateTime.Now;
            int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
            DateTime next = new DateTime(now.Year, now.Month, now.Day, Mathf.Clamp(_siegeStartHourSaturday, 0, 23), 0, 0).AddDays(daysUntilSaturday);
            if (next <= now)
                next = next.AddDays(7);

            _snapshot.state = SiegeState.Scheduled;
            _snapshot.startsAt = Time.unscaledTime + Mathf.Max(0f, (float)(next - now).TotalSeconds);
            _snapshot.endsAt = _snapshot.startsAt + _siegeDurationSeconds;
            _snapshot.marketTaxRate = _defaultMarketTax;
        }

        private void SeedWeapons()
        {
            if (_weapons.Count > 0)
                return;

            _weapons.Add(new SiegeWeapon { weaponId = "cannon-a", type = SiegeWeaponType.Cannon });
            _weapons.Add(new SiegeWeapon { weaponId = "ballista-a", type = SiegeWeaponType.Ballista });
            _weapons.Add(new SiegeWeapon { weaponId = "ram-a", type = SiegeWeaponType.Ram });
            _weapons.Add(new SiegeWeapon { weaponId = "flame-a", type = SiegeWeaponType.FlameTower });
        }
    }
}