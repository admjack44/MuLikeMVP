using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.Economy
{
    /// <summary>
    /// Core economy balances: Zen, jewels, monster drops, and monthly VIP membership.
    /// VIP remains convenience-focused: inventory, teleport, cosmetics, and rate modifiers.
    /// </summary>
    public sealed class CurrencyManager : MonoBehaviour
    {
        public enum CurrencyType
        {
            Zen,
            JewelOfBless,
            JewelOfSoul,
            JewelOfChaos,
            JewelOfLife
        }

        [Serializable]
        public struct CurrencyBalance
        {
            public CurrencyType type;
            public long amount;
        }

        [Serializable]
        public struct VipProfile
        {
            public bool active;
            public string planId;
            public long activatedAtUnixMs;
            public long expiresAtUnixMs;
            public float experienceMultiplier;
            public float dropMultiplier;
            public bool unlimitedTeleport;
            public int extraInventoryRows;
            public string[] cosmeticUnlocks;
        }

        [Serializable]
        public struct MonsterDropReward
        {
            public int monsterLevel;
            public bool elite;
            public bool boss;
            public long zen;
            public int bless;
            public int soul;
            public int chaos;
            public int life;
        }

        [Header("Starting Balances")]
        [SerializeField, Min(0)] private long _startingZen = 50000;
        [SerializeField, Min(0)] private int _startingBless;
        [SerializeField, Min(0)] private int _startingSoul;
        [SerializeField, Min(0)] private int _startingChaos;
        [SerializeField, Min(0)] private int _startingLife;

        [Header("VIP")]
        [SerializeField] private string _defaultVipPlanId = "vip-monthly";
        [SerializeField, Min(1)] private int _vipDurationDays = 30;

        [Header("Dependencies")]
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private InventoryManager _inventoryManager;

        private readonly Dictionary<CurrencyType, long> _balances = new();

        public VipProfile CurrentVip { get; private set; }
        public IReadOnlyDictionary<CurrencyType, long> Balances => _balances;
        public float ExperienceMultiplier => IsVipActive ? Mathf.Max(1f, CurrentVip.experienceMultiplier) : 1f;
        public float DropMultiplier => IsVipActive ? Mathf.Max(1f, CurrentVip.dropMultiplier) : 1f;
        public bool UnlimitedTeleport => IsVipActive && CurrentVip.unlimitedTeleport;
        public int ExtraInventoryRows => IsVipActive ? Mathf.Max(0, CurrentVip.extraInventoryRows) : 0;
        public bool IsVipActive => CurrentVip.active && CurrentVip.expiresAtUnixMs > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public event Action<CurrencyType, long> OnBalanceChanged;
        public event Action<VipProfile> OnVipChanged;
        public event Action<MonsterDropReward> OnMonsterDropGranted;

        private void Awake()
        {
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();

            _balances[CurrencyType.Zen] = Math.Max(0L, _startingZen);
            _balances[CurrencyType.JewelOfBless] = Mathf.Max(0, _startingBless);
            _balances[CurrencyType.JewelOfSoul] = Mathf.Max(0, _startingSoul);
            _balances[CurrencyType.JewelOfChaos] = Mathf.Max(0, _startingChaos);
            _balances[CurrencyType.JewelOfLife] = Mathf.Max(0, _startingLife);

            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            if (CurrentVip.active && !IsVipActive)
            {
                VipProfile vip = CurrentVip;
                vip.active = false;
                CurrentVip = vip;
                OnVipChanged?.Invoke(CurrentVip);
                _chatSystem?.ReceiveSystemMessage("VIP membership expired.");
            }
        }

        public long GetBalance(CurrencyType type)
        {
            return _balances.TryGetValue(type, out long amount) ? amount : 0L;
        }

        public void AddCurrency(CurrencyType type, long amount)
        {
            if (amount <= 0)
                return;

            _balances.TryGetValue(type, out long current);
            _balances[type] = current + amount;
            OnBalanceChanged?.Invoke(type, _balances[type]);
        }

        public bool TrySpendCurrency(CurrencyType type, long amount)
        {
            if (amount < 0)
                return false;

            long current = GetBalance(type);
            if (current < amount)
                return false;

            _balances[type] = current - amount;
            OnBalanceChanged?.Invoke(type, _balances[type]);
            return true;
        }

        public MonsterDropReward GrantMonsterDrop(int monsterLevel, bool elite = false, bool boss = false)
        {
            float vipDrop = DropMultiplier;
            long zen = (long)Math.Round((monsterLevel * (boss ? 60f : elite ? 18f : 8f) + UnityEngine.Random.Range(3, 15)) * vipDrop);
            int bless = boss ? 1 : (elite && UnityEngine.Random.value < 0.08f * vipDrop ? 1 : 0);
            int soul = boss || UnityEngine.Random.value < 0.04f * vipDrop ? 1 : 0;
            int chaos = boss && UnityEngine.Random.value < 0.60f ? 1 : (elite && UnityEngine.Random.value < 0.03f * vipDrop ? 1 : 0);
            int life = boss && UnityEngine.Random.value < 0.30f ? 1 : 0;

            AddCurrency(CurrencyType.Zen, zen);
            AddCurrency(CurrencyType.JewelOfBless, bless);
            AddCurrency(CurrencyType.JewelOfSoul, soul);
            AddCurrency(CurrencyType.JewelOfChaos, chaos);
            AddCurrency(CurrencyType.JewelOfLife, life);

            var reward = new MonsterDropReward
            {
                monsterLevel = monsterLevel,
                elite = elite,
                boss = boss,
                zen = zen,
                bless = bless,
                soul = soul,
                chaos = chaos,
                life = life
            };

            OnMonsterDropGranted?.Invoke(reward);
            return reward;
        }

        public bool TryActivateMonthlyVip()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            CurrentVip = new VipProfile
            {
                active = true,
                planId = _defaultVipPlanId,
                activatedAtUnixMs = now,
                expiresAtUnixMs = now + (long)TimeSpan.FromDays(Mathf.Max(1, _vipDurationDays)).TotalMilliseconds,
                experienceMultiplier = 1.5f,
                dropMultiplier = 1.5f,
                unlimitedTeleport = true,
                extraInventoryRows = 4,
                cosmeticUnlocks = new[] { "vip-aura", "vip-frame", "vip-wings-skin" }
            };

            OnVipChanged?.Invoke(CurrentVip);
            _chatSystem?.ReceiveSystemMessage("VIP monthly membership activated.");
            return true;
        }

        public bool TryUpgradeWithBless(ref InventoryManager.InventoryItem item)
        {
            if (item == null || item.enhancementLevel >= 6)
                return false;

            if (!TrySpendCurrency(CurrencyType.JewelOfBless, 1))
                return false;

            item.enhancementLevel = Mathf.Clamp(item.enhancementLevel + 1, 0, 6);
            return true;
        }

        public bool TryUpgradeWithSoul(ref InventoryManager.InventoryItem item, float successChance = 0.70f)
        {
            if (item == null || item.enhancementLevel < 6 || item.enhancementLevel >= 9)
                return false;

            if (!TrySpendCurrency(CurrencyType.JewelOfSoul, 1))
                return false;

            bool success = UnityEngine.Random.value <= Mathf.Clamp01(successChance);
            if (success)
            {
                item.enhancementLevel = Mathf.Clamp(item.enhancementLevel + 1, 0, 9);
                return true;
            }

            item.enhancementLevel = Mathf.Max(0, item.enhancementLevel - 1);
            return false;
        }

        public bool TryApplyLifeOption(ref InventoryManager.InventoryItem item)
        {
            if (item == null)
                return false;

            if (!TrySpendCurrency(CurrencyType.JewelOfLife, 1))
                return false;

            item.stats.damage += 8;
            item.stats.defense += 8;
            return true;
        }

        public long EstimateHighValueScore(InventoryManager.InventoryItem item)
        {
            if (item == null)
                return 0L;

            long rarityFactor = item.rarity switch
            {
                InventoryManager.InventoryRarity.Legendary => 12000,
                InventoryManager.InventoryRarity.Epic => 7000,
                InventoryManager.InventoryRarity.Rare => 3000,
                InventoryManager.InventoryRarity.Magic => 1200,
                _ => 300
            };

            return rarityFactor + item.itemId * 2L + item.enhancementLevel * 750L + item.quantity * 25L;
        }
    }
}