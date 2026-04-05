using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Data.Catalogs;
using MuLike.Inventory;
using MuLike.PvP;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Economy
{
    /// <summary>
    /// Global market with buy-now, bidding, filters, price history, and seller commission.
    /// Accessible in any city by design; no map restriction enforced here.
    /// </summary>
    public sealed class AuctionHouse : MonoBehaviour
    {
        [Serializable]
        public struct AuctionFilter
        {
            public CharacterClassRestriction classRestriction;
            public int minRequiredLevel;
            public int maxRequiredLevel;
            public InventoryManager.InventoryRarity? rarity;
            public ItemCategory? category;
        }

        [Serializable]
        public sealed class BidEntry
        {
            public string bidder;
            public long amountZen;
            public long timestampUnixMs;
        }

        [Serializable]
        public sealed class AuctionListing
        {
            public string listingId;
            public string seller;
            public InventoryManager.InventoryItem item;
            public long buyNowZen;
            public long currentBidZen;
            public string currentBidder;
            public bool closed;
            public float expiresAt;
            public readonly List<BidEntry> bidHistory = new();
        }

        [Serializable]
        public struct PricePoint
        {
            public long soldPriceZen;
            public long soldAtUnixMs;
        }

        [Header("Fees")]
        [SerializeField, Range(0f, 0.30f)] private float _sellerCommission = 0.10f;
        [SerializeField, Min(60f)] private float _defaultDurationSeconds = 86400f;

        [Header("Dependencies")]
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private CurrencyManager _currencyManager;
        [SerializeField] private ChatSystem _chatSystem;
        [SerializeField] private CastleSiege _castleSiege;

        private readonly Dictionary<string, AuctionListing> _listings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<PricePoint>> _priceHistory = new();

        public IReadOnlyDictionary<string, AuctionListing> Listings => _listings;
        public IReadOnlyDictionary<int, List<PricePoint>> PriceHistory => _priceHistory;

        public event Action<AuctionListing> OnListingUpdated;
        public event Action<AuctionListing, string> OnListingSold;

        private void Awake()
        {
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_currencyManager == null)
                _currencyManager = FindAnyObjectByType<CurrencyManager>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();
            if (_castleSiege == null)
                _castleSiege = FindAnyObjectByType<CastleSiege>();

            GameContext.RegisterSystem(this);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            foreach (KeyValuePair<string, AuctionListing> pair in _listings)
            {
                AuctionListing listing = pair.Value;
                if (listing.closed || listing.expiresAt > now)
                    continue;

                CloseExpiredListing(listing);
            }
        }

        public AuctionListing CreateListing(string seller, string itemInstanceId, long buyNowZen, long openingBidZen = 0)
        {
            if (_inventoryManager == null)
                return null;

            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem removed) || removed == null)
                return null;

            var listing = new AuctionListing
            {
                listingId = Guid.NewGuid().ToString("N"),
                seller = seller,
                item = removed,
                buyNowZen = Math.Max(0L, buyNowZen),
                currentBidZen = Math.Max(0L, openingBidZen),
                expiresAt = Time.unscaledTime + _defaultDurationSeconds
            };

            _listings[listing.listingId] = listing;
            OnListingUpdated?.Invoke(listing);
            return listing;
        }

        public IReadOnlyList<AuctionListing> QueryListings(AuctionFilter filter)
        {
            var results = new List<AuctionListing>();
            foreach (KeyValuePair<string, AuctionListing> pair in _listings)
            {
                AuctionListing listing = pair.Value;
                if (listing.closed || listing.item == null)
                    continue;

                if (!MatchesFilter(listing.item, filter))
                    continue;

                results.Add(listing);
            }

            return results;
        }

        public bool TryBuyNow(string buyer, string listingId)
        {
            if (!_listings.TryGetValue(listingId, out AuctionListing listing) || listing.closed || listing.buyNowZen <= 0)
                return false;

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.Zen, listing.buyNowZen))
                return false;

            return CompleteSale(listing, buyer, listing.buyNowZen);
        }

        public bool TryBid(string bidder, string listingId, long amountZen)
        {
            if (!_listings.TryGetValue(listingId, out AuctionListing listing) || listing.closed)
                return false;

            long minimum = Math.Max(listing.currentBidZen + 1L, 1L);
            if (amountZen < minimum)
                return false;

            if (_currencyManager == null || !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.Zen, amountZen))
                return false;

            if (!string.IsNullOrWhiteSpace(listing.currentBidder) && listing.currentBidZen > 0)
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.Zen, listing.currentBidZen);

            listing.currentBidZen = amountZen;
            listing.currentBidder = bidder;
            listing.bidHistory.Add(new BidEntry
            {
                bidder = bidder,
                amountZen = amountZen,
                timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            OnListingUpdated?.Invoke(listing);
            return true;
        }

        public IReadOnlyList<PricePoint> GetPriceHistory(int itemId)
        {
            if (_priceHistory.TryGetValue(itemId, out List<PricePoint> history))
                return history;
            return Array.Empty<PricePoint>();
        }

        public void SeedHistoricalSale(int itemId, long soldPriceZen, long soldAtUnixMs = 0)
        {
            PushPricePoint(itemId, soldPriceZen, soldAtUnixMs <= 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : soldAtUnixMs);
        }

        private bool CompleteSale(AuctionListing listing, string buyer, long soldPrice)
        {
            if (_inventoryManager == null || !_inventoryManager.TryAddItem(listing.item))
            {
                _currencyManager?.AddCurrency(CurrencyManager.CurrencyType.Zen, soldPrice);
                return false;
            }

            listing.closed = true;
            long sellerNet = Math.Max(0L, (long)Math.Round(soldPrice * (1f - ResolveTotalFeeRate())));
            _currencyManager?.AddCurrency(CurrencyManager.CurrencyType.Zen, sellerNet);
            PushPricePoint(listing.item.itemId, soldPrice, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _chatSystem?.ReceiveSystemMessage($"Auction sale complete: {listing.item.displayName} sold to {buyer}.");
            OnListingSold?.Invoke(listing, buyer);
            return true;
        }

        private void CloseExpiredListing(AuctionListing listing)
        {
            if (listing.currentBidZen > 0 && !string.IsNullOrWhiteSpace(listing.currentBidder))
            {
                CompleteSale(listing, listing.currentBidder, listing.currentBidZen);
                return;
            }

            listing.closed = true;
            _inventoryManager?.TryAddItem(listing.item);
            OnListingUpdated?.Invoke(listing);
        }

        private void PushPricePoint(int itemId, long soldPrice, long soldAtUnixMs)
        {
            if (!_priceHistory.TryGetValue(itemId, out List<PricePoint> history))
            {
                history = new List<PricePoint>();
                _priceHistory[itemId] = history;
            }

            history.Add(new PricePoint
            {
                soldPriceZen = soldPrice,
                soldAtUnixMs = soldAtUnixMs
            });

            if (history.Count > 32)
                history.RemoveAt(0);
        }

        private float ResolveTotalFeeRate()
        {
            float castleTax = _castleSiege != null ? Mathf.Max(0f, _castleSiege.Snapshot.marketTaxRate) : 0f;
            return Mathf.Clamp01(_sellerCommission + castleTax);
        }

        private static bool MatchesFilter(InventoryManager.InventoryItem item, AuctionFilter filter)
        {
            if (item == null)
                return false;

            if (filter.rarity.HasValue && item.rarity != filter.rarity.Value)
                return false;

            if (GameContext.CatalogResolver == null || !GameContext.CatalogResolver.TryGetItemDefinition(item.itemId, out ItemDefinition definition) || definition == null)
            {
                return !filter.category.HasValue && filter.classRestriction == CharacterClassRestriction.Any;
            }

            if (filter.category.HasValue && definition.Category != filter.category.Value)
                return false;

            int minLevel = Mathf.Max(0, filter.minRequiredLevel);
            int maxLevel = filter.maxRequiredLevel <= 0 ? int.MaxValue : filter.maxRequiredLevel;
            if (definition.RequiredLevel < minLevel || definition.RequiredLevel > maxLevel)
                return false;

            if (filter.classRestriction != CharacterClassRestriction.Any)
            {
                List<CharacterClassRestriction> classes = definition.AllowedClasses ?? new List<CharacterClassRestriction>();
                if (!classes.Contains(CharacterClassRestriction.Any) && !classes.Contains(filter.classRestriction))
                    return false;
            }

            return true;
        }
    }
}