using System;
using MuLike.Crafting;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.Economy
{
    /// <summary>
    /// Seeds economy systems with test data for editor/runtime iteration.
    /// </summary>
    public sealed class EconomyDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _seedOnStart = true;
        [SerializeField] private bool _activateVipOnStart = true;

        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private CurrencyManager _currency;
        [SerializeField] private TradeSystem _trade;
        [SerializeField] private AuctionHouse _auction;
        [SerializeField] private ChaosMachine _chaosMachine;

        private void Start()
        {
            if (!_seedOnStart)
                return;

            if (_inventory == null)
                _inventory = FindAnyObjectByType<InventoryManager>();
            if (_currency == null)
                _currency = FindAnyObjectByType<CurrencyManager>();
            if (_trade == null)
                _trade = FindAnyObjectByType<TradeSystem>();
            if (_auction == null)
                _auction = FindAnyObjectByType<AuctionHouse>();
            if (_chaosMachine == null)
                _chaosMachine = FindAnyObjectByType<ChaosMachine>();

            Seed();
        }

        [ContextMenu("Seed Economy Demo Data")]
        public void Seed()
        {
            if (_inventory == null || _currency == null)
                return;

            EnsureDemoItem(2001, "Dragon Sword", MuLike.Data.Catalogs.ItemCategory.Weapon, InventoryManager.InventoryRarity.Epic, InventoryManager.ItemSize.OneByTwo, 9, 44, 0);
            EnsureDemoItem(2002, "Guardian Armor", MuLike.Data.Catalogs.ItemCategory.Armor, InventoryManager.InventoryRarity.Rare, InventoryManager.ItemSize.TwoByTwo, 7, 12, 38);
            EnsureDemoItem(2003, "Bless Stack", MuLike.Data.Catalogs.ItemCategory.Material, InventoryManager.InventoryRarity.Magic, InventoryManager.ItemSize.OneByOne, 0, 0, 0, 10);
            EnsureDemoItem(2004, "Wing Core", MuLike.Data.Catalogs.ItemCategory.Material, InventoryManager.InventoryRarity.Rare, InventoryManager.ItemSize.OneByOne, 0, 8, 8);
            EnsureDemoItem(2005, "Socket Spear", MuLike.Data.Catalogs.ItemCategory.Weapon, InventoryManager.InventoryRarity.Legendary, InventoryManager.ItemSize.OneByTwo, 11, 52, 0);

            _currency.AddCurrency(CurrencyManager.CurrencyType.Zen, 500000);
            _currency.AddCurrency(CurrencyManager.CurrencyType.JewelOfBless, 12);
            _currency.AddCurrency(CurrencyManager.CurrencyType.JewelOfSoul, 8);
            _currency.AddCurrency(CurrencyManager.CurrencyType.JewelOfChaos, 6);
            _currency.AddCurrency(CurrencyManager.CurrencyType.JewelOfLife, 4);
            if (_activateVipOnStart && !_currency.IsVipActive)
                _currency.TryActivateMonthlyVip();

            if (_auction != null)
            {
                _auction.SeedHistoricalSale(2001, 120000);
                _auction.SeedHistoricalSale(2001, 135000);
                _auction.SeedHistoricalSale(2002, 95000);
            }

            if (_trade != null)
                _trade.CreateTrade("Player", "MerchantBot");
        }

        private void EnsureDemoItem(int itemId, string name, MuLike.Data.Catalogs.ItemCategory category, InventoryManager.InventoryRarity rarity, InventoryManager.ItemSize size, int enhancement, int damage, int defense, int quantity = 1)
        {
            if (_inventory == null)
                return;

            var entries = _inventory.InventoryEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i]?.item != null && entries[i].item.itemId == itemId)
                    return;
            }

            _inventory.TryAddItem(new InventoryManager.InventoryItem
            {
                instanceId = Guid.NewGuid().ToString("N"),
                itemId = itemId,
                displayName = name,
                category = category,
                rarity = rarity,
                size = size,
                quantity = Mathf.Max(1, quantity),
                enhancementLevel = enhancement,
                stats = new InventoryManager.ItemStats
                {
                    damage = damage,
                    defense = defense,
                    hp = defense * 2
                }
            });
        }
    }
}