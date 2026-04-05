using System;
using System.Collections.Generic;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.Shop
{
    /// <summary>
    /// In-game shop + premium cash shop runtime.
    ///
    /// Categories: Featured, Consumables, Equipment, Cosmetic
    /// Supports premium currency, daily offers and bundles.
    /// </summary>
    public sealed class CashShopManager : MonoBehaviour
    {
        public enum ShopCategory
        {
            Featured,
            Consumables,
            Equipment,
            Cosmetic
        }

        [Serializable]
        public struct ShopProduct
        {
            public string productId;
            public string displayName;
            public ShopCategory category;
            public int premiumPrice;
            public InventoryManager.InventoryItem itemReward;
            public bool isBundle;
            public InventoryManager.InventoryItem[] bundleItems;
            public bool isDailyOffer;
            public int dailyStock;
        }

        [Header("Dependencies")]
        [SerializeField] private InventoryManager _inventory;

        [Header("Economy")]
        [SerializeField] private int _premiumCurrency = 1000;

        [Header("Catalog")]
        [SerializeField] private ShopProduct[] _products = Array.Empty<ShopProduct>();

        private readonly Dictionary<string, int> _dailyBoughtByProduct = new();
        private DateTime _dailyResetDateUtc = DateTime.UtcNow.Date;

        public int PremiumCurrency => _premiumCurrency;
        public IReadOnlyList<ShopProduct> Products => _products;

        public event Action<int> OnPremiumCurrencyChanged;
        public event Action<string, bool, string> OnPurchaseProcessed;

        private void Update()
        {
            DateTime today = DateTime.UtcNow.Date;
            if (today <= _dailyResetDateUtc)
                return;

            _dailyResetDateUtc = today;
            _dailyBoughtByProduct.Clear();
        }

        public void AddPremiumCurrency(int amount)
        {
            _premiumCurrency = Mathf.Max(0, _premiumCurrency + Mathf.Max(0, amount));
            OnPremiumCurrencyChanged?.Invoke(_premiumCurrency);
        }

        public IReadOnlyList<ShopProduct> GetByCategory(ShopCategory category)
        {
            var list = new List<ShopProduct>();
            for (int i = 0; i < _products.Length; i++)
            {
                if (_products[i].category == category)
                    list.Add(_products[i]);
            }

            return list;
        }

        public bool TryPurchase(string productId)
        {
            if (!TryFind(productId, out ShopProduct p))
            {
                OnPurchaseProcessed?.Invoke(productId, false, "Product not found.");
                return false;
            }

            if (p.premiumPrice > _premiumCurrency)
            {
                OnPurchaseProcessed?.Invoke(productId, false, "Insufficient premium currency.");
                return false;
            }

            if (p.isDailyOffer && !HasDailyStock(p))
            {
                OnPurchaseProcessed?.Invoke(productId, false, "Daily stock exhausted.");
                return false;
            }

            if (_inventory == null)
            {
                OnPurchaseProcessed?.Invoke(productId, false, "Inventory not configured.");
                return false;
            }

            bool granted = p.isBundle ? GrantBundle(p) : GrantSingle(p);
            if (!granted)
            {
                OnPurchaseProcessed?.Invoke(productId, false, "Inventory full for reward.");
                return false;
            }

            _premiumCurrency -= Mathf.Max(0, p.premiumPrice);
            if (p.isDailyOffer)
            {
                _dailyBoughtByProduct.TryGetValue(p.productId, out int bought);
                _dailyBoughtByProduct[p.productId] = bought + 1;
            }

            OnPremiumCurrencyChanged?.Invoke(_premiumCurrency);
            OnPurchaseProcessed?.Invoke(productId, true, "Purchase successful.");
            return true;
        }

        private bool GrantSingle(ShopProduct product)
        {
            if (product.itemReward == null)
                return false;

            InventoryManager.InventoryItem clone = CloneItem(product.itemReward);
            return _inventory.TryAddItem(clone);
        }

        private bool GrantBundle(ShopProduct product)
        {
            if (product.bundleItems == null || product.bundleItems.Length == 0)
                return false;

            var granted = new List<InventoryManager.InventoryItem>();
            for (int i = 0; i < product.bundleItems.Length; i++)
            {
                InventoryManager.InventoryItem src = product.bundleItems[i];
                if (src == null)
                    continue;

                InventoryManager.InventoryItem clone = CloneItem(src);
                if (_inventory.TryAddItem(clone))
                {
                    granted.Add(clone);
                    continue;
                }

                // rollback simple best-effort
                for (int r = 0; r < granted.Count; r++)
                    _inventory.TryRemoveItem(granted[r].instanceId, out _);

                return false;
            }

            return true;
        }

        private bool HasDailyStock(ShopProduct p)
        {
            int max = Mathf.Max(0, p.dailyStock);
            if (max <= 0)
                return false;

            _dailyBoughtByProduct.TryGetValue(p.productId, out int bought);
            return bought < max;
        }

        private bool TryFind(string productId, out ShopProduct product)
        {
            for (int i = 0; i < _products.Length; i++)
            {
                if (_products[i].productId != productId)
                    continue;

                product = _products[i];
                return true;
            }

            product = default;
            return false;
        }

        private static InventoryManager.InventoryItem CloneItem(InventoryManager.InventoryItem src)
        {
            return new InventoryManager.InventoryItem
            {
                instanceId = string.IsNullOrEmpty(src.instanceId) ? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                itemId = src.itemId,
                displayName = src.displayName,
                category = src.category,
                rarity = src.rarity,
                size = src.size,
                quantity = Mathf.Max(1, src.quantity),
                enhancementLevel = src.enhancementLevel,
                visualId = src.visualId,
                stats = src.stats
            };
        }
    }
}
