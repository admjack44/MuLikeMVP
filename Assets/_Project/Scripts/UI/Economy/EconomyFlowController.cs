using System;
using System.Collections.Generic;
using MuLike.Crafting;
using MuLike.Data.Catalogs;
using MuLike.Economy;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.UI.Economy
{
    /// <summary>
    /// Scene composition/controller for the economy hub.
    /// Wires view actions to trade, auction, crafting, currency, and network bridge systems.
    /// </summary>
    public sealed class EconomyFlowController : MonoBehaviour
    {
        [SerializeField] private EconomyHubView _view;
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private CurrencyManager _currency;
        [SerializeField] private TradeSystem _trade;
        [SerializeField] private AuctionHouse _auction;
        [SerializeField] private ChaosMachine _chaosMachine;
        [SerializeField] private EconomyNetworkBridge _networkBridge;

        private string _currentPlayerName = "Player";

        private void Awake()
        {
            if (_view == null)
                _view = FindAnyObjectByType<EconomyHubView>(FindObjectsInactive.Include);
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
            if (_networkBridge == null)
                _networkBridge = FindAnyObjectByType<EconomyNetworkBridge>();

            if (_view == null)
                return;

            Bind();
            RefreshAll();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Bind()
        {
            _view.TradeCreateRequested += HandleTradeCreate;
            _view.TradeAddItemRequested += HandleTradeAddItem;
            _view.TradeCurrencyRequested += HandleTradeCurrency;
            _view.TradeConfirmRequested += HandleTradeConfirm;
            _view.TradeCancelRequested += HandleTradeCancel;
            _view.AuctionCreateRequested += HandleAuctionCreate;
            _view.AuctionRefreshRequested += HandleAuctionRefresh;
            _view.AuctionBidRequested += HandleAuctionBid;
            _view.AuctionBuyNowRequested += HandleAuctionBuyNow;
            _view.CraftCombineRequested += HandleCombine;
            _view.CraftWingRequested += HandleCraftWing;
            _view.CraftSocketRequested += HandleCraftSocket;
            _view.CraftElementRequested += HandleCraftElement;
            _view.VipActivateRequested += HandleVipActivate;
            _view.WalletRefreshRequested += RefreshAll;

            if (_inventory != null)
                _inventory.OnInventoryChanged += RefreshInventory;
            if (_currency != null)
            {
                _currency.OnBalanceChanged += HandleBalanceChanged;
                _currency.OnVipChanged += HandleVipChanged;
            }
            if (_trade != null)
            {
                _trade.OnTradeUpdated += HandleTradeUpdated;
                _trade.OnTradeCompleted += HandleTradeCompleted;
            }
            if (_auction != null)
            {
                _auction.OnListingUpdated += HandleListingUpdated;
                _auction.OnListingSold += HandleListingSold;
            }
            if (_chaosMachine != null)
                _chaosMachine.OnCraftCompleted += HandleCraftCompleted;
        }

        private void Unbind()
        {
            if (_view != null)
            {
                _view.TradeCreateRequested -= HandleTradeCreate;
                _view.TradeAddItemRequested -= HandleTradeAddItem;
                _view.TradeCurrencyRequested -= HandleTradeCurrency;
                _view.TradeConfirmRequested -= HandleTradeConfirm;
                _view.TradeCancelRequested -= HandleTradeCancel;
                _view.AuctionCreateRequested -= HandleAuctionCreate;
                _view.AuctionRefreshRequested -= HandleAuctionRefresh;
                _view.AuctionBidRequested -= HandleAuctionBid;
                _view.AuctionBuyNowRequested -= HandleAuctionBuyNow;
                _view.CraftCombineRequested -= HandleCombine;
                _view.CraftWingRequested -= HandleCraftWing;
                _view.CraftSocketRequested -= HandleCraftSocket;
                _view.CraftElementRequested -= HandleCraftElement;
                _view.VipActivateRequested -= HandleVipActivate;
                _view.WalletRefreshRequested -= RefreshAll;
            }

            if (_inventory != null)
                _inventory.OnInventoryChanged -= RefreshInventory;
            if (_currency != null)
            {
                _currency.OnBalanceChanged -= HandleBalanceChanged;
                _currency.OnVipChanged -= HandleVipChanged;
            }
            if (_trade != null)
            {
                _trade.OnTradeUpdated -= HandleTradeUpdated;
                _trade.OnTradeCompleted -= HandleTradeCompleted;
            }
            if (_auction != null)
            {
                _auction.OnListingUpdated -= HandleListingUpdated;
                _auction.OnListingSold -= HandleListingSold;
            }
            if (_chaosMachine != null)
                _chaosMachine.OnCraftCompleted -= HandleCraftCompleted;
        }

        public void Toggle()
        {
            _view?.ToggleVisible();
        }

        private async void HandleTradeCreate(string playerA, string playerB)
        {
            if (string.IsNullOrWhiteSpace(playerB))
            {
                _view.RenderTradeStatus("Target player is required.");
                return;
            }

            if (_networkBridge != null)
                await _networkBridge.CreateTradeAsync(_currentPlayerName, playerB);
            else
                _trade?.CreateTrade(_currentPlayerName, playerB);

            if (_trade != null)
            {
                foreach (var kv in _trade.Sessions)
                {
                    if (string.Equals(kv.Value.playerA, _currentPlayerName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(kv.Value.playerB, playerB, StringComparison.OrdinalIgnoreCase))
                    {
                        _view.SetTradeSessionId(kv.Key);
                        break;
                    }
                }
            }
        }

        private async void HandleTradeAddItem(string sessionId, string _, string itemInstanceId)
        {
            if (_networkBridge != null)
                await _networkBridge.AddTradeItemAsync(sessionId, _currentPlayerName, itemInstanceId);
            else
                _trade?.AddItemOffer(sessionId, _currentPlayerName, itemInstanceId);
        }

        private async void HandleTradeCurrency(string sessionId, string _, long zen, int bless, int soul, int chaos, int life)
        {
            if (_networkBridge != null)
                await _networkBridge.SetTradeCurrencyAsync(sessionId, _currentPlayerName, zen, bless, soul, chaos, life);
            else
                _trade?.SetCurrencyOffer(sessionId, _currentPlayerName, zen, bless, soul, chaos, life);
        }

        private async void HandleTradeConfirm(string sessionId, string _)
        {
            if (_networkBridge != null)
                await _networkBridge.ConfirmTradeAsync(sessionId, _currentPlayerName);
            else
                _trade?.ConfirmTrade(sessionId, _currentPlayerName);
        }

        private async void HandleTradeCancel(string sessionId, string _)
        {
            if (_networkBridge != null)
                await _networkBridge.CancelTradeAsync(sessionId, _currentPlayerName);
            else
                _trade?.CancelTrade(sessionId, "Cancelled from UI.");
        }

        private async void HandleAuctionCreate(string seller, string itemInstanceId, long buyNowZen, long openingBidZen)
        {
            if (_networkBridge != null)
                await _networkBridge.CreateListingAsync(_currentPlayerName, itemInstanceId, buyNowZen, openingBidZen);
            else
                _auction?.CreateListing(_currentPlayerName, itemInstanceId, buyNowZen, openingBidZen);
            HandleAuctionRefresh(default);
        }

        private void HandleAuctionRefresh(EconomyHubView.AuctionFilterInput input)
        {
            if (_auction == null || _view == null)
                return;

            var filter = new AuctionHouse.AuctionFilter
            {
                classRestriction = (CharacterClassRestriction)Mathf.Clamp(input.classRestrictionIndex, 0, 5),
                category = input.categoryIndex <= 0 ? null : (ItemCategory?)(input.categoryIndex - 1),
                rarity = input.rarityIndex <= 0 ? null : (InventoryManager.InventoryRarity?)(input.rarityIndex - 1),
                minRequiredLevel = Mathf.Max(0, input.minLevel),
                maxRequiredLevel = Mathf.Max(0, input.maxLevel)
            };

            IReadOnlyList<AuctionHouse.AuctionListing> listings = _auction.QueryListings(filter);
            var lines = new List<string>(listings.Count);
            for (int i = 0; i < listings.Count; i++)
            {
                AuctionHouse.AuctionListing listing = listings[i];
                lines.Add($"{listing.listingId} | {listing.item.displayName} | BuyNow {listing.buyNowZen} | Bid {listing.currentBidZen} by {listing.currentBidder}");
            }
            _view.RenderAuctionListings(lines);

            if (listings.Count > 0)
            {
                IReadOnlyList<AuctionHouse.PricePoint> history = _auction.GetPriceHistory(listings[0].item.itemId);
                var historyLines = new List<string>(history.Count);
                for (int i = 0; i < history.Count; i++)
                    historyLines.Add($"{history[i].soldPriceZen} Zen @ {DateTimeOffset.FromUnixTimeMilliseconds(history[i].soldAtUnixMs).ToLocalTime():MM-dd HH:mm}");
                _view.RenderPriceHistory(historyLines);
            }
            else
            {
                _view.RenderPriceHistory(Array.Empty<string>());
            }
        }

        private async void HandleAuctionBid(string bidder, string listingId, long amountZen)
        {
            if (_networkBridge != null)
                await _networkBridge.BidAsync(_currentPlayerName, listingId, amountZen);
            else
                _auction?.TryBid(_currentPlayerName, listingId, amountZen);
        }

        private async void HandleAuctionBuyNow(string buyer, string listingId)
        {
            if (_networkBridge != null)
                await _networkBridge.BuyNowAsync(_currentPlayerName, listingId);
            else
                _auction?.TryBuyNow(_currentPlayerName, listingId);
        }

        private void HandleCombine(string rawItemIds)
        {
            string[] ids = rawItemIds.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _chaosMachine?.CombineItems(ids);
        }

        private void HandleCraftWing(int wingLevel, string baseItemId)
        {
            _chaosMachine?.CreateWing(wingLevel, baseItemId);
        }

        private void HandleCraftSocket(string itemId)
        {
            _chaosMachine?.TryAddSocket(itemId);
        }

        private void HandleCraftElement(string itemId, int elementIndex)
        {
            _chaosMachine?.TryApplyElementalAttribute(itemId, (ChaosMachine.ElementalType)Mathf.Clamp(elementIndex, 0, 3));
        }

        private void HandleVipActivate()
        {
            if (_currency != null && _currency.TryActivateMonthlyVip())
                RefreshWallet();
        }

        private void HandleBalanceChanged(CurrencyManager.CurrencyType _, long __)
        {
            RefreshWallet();
        }

        private void HandleVipChanged(CurrencyManager.VipProfile _)
        {
            RefreshWallet();
        }

        private void HandleTradeUpdated(TradeSystem.TradeSession session)
        {
            _view?.RenderTradeStatus($"Trade {session.sessionId}: {session.playerA} <-> {session.playerB} | A:{session.offerA.items.Count} items | B:{session.offerB.items.Count} items");
            _view?.SetTradeSessionId(session.sessionId);
        }

        private void HandleTradeCompleted(TradeSystem.TradeSession session, bool success, string message)
        {
            _view?.RenderTradeStatus($"Trade {(success ? "completed" : "failed")}: {message}");
            RefreshAll();
        }

        private void HandleListingUpdated(AuctionHouse.AuctionListing _)
        {
            HandleAuctionRefresh(default);
        }

        private void HandleListingSold(AuctionHouse.AuctionListing listing, string buyer)
        {
            _view?.RenderAuctionStatus($"Sold {listing.item.displayName} to {buyer}.");
            RefreshAll();
        }

        private void HandleCraftCompleted(ChaosMachine.CraftRecipeType _, ChaosMachine.CraftResult result)
        {
            _view?.RenderCraftStatus(result.message);
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshInventory();
            RefreshWallet();
            HandleAuctionRefresh(default);
        }

        private void RefreshInventory()
        {
            if (_view == null || _inventory == null)
                return;

            var lines = new List<string>(_inventory.InventoryEntries.Count);
            for (int i = 0; i < _inventory.InventoryEntries.Count; i++)
            {
                InventoryManager.GridEntry entry = _inventory.InventoryEntries[i];
                if (entry?.item == null)
                    continue;

                lines.Add($"{entry.item.instanceId} | {entry.item.displayName} | +{entry.item.enhancementLevel} | {entry.item.rarity}");
            }

            _view.RenderInventorySummary(lines);
        }

        private void RefreshWallet()
        {
            if (_view == null || _currency == null)
                return;

            string wallet = $"Zen: {_currency.GetBalance(CurrencyManager.CurrencyType.Zen)}\nBless: {_currency.GetBalance(CurrencyManager.CurrencyType.JewelOfBless)}\nSoul: {_currency.GetBalance(CurrencyManager.CurrencyType.JewelOfSoul)}\nChaos: {_currency.GetBalance(CurrencyManager.CurrencyType.JewelOfChaos)}\nLife: {_currency.GetBalance(CurrencyManager.CurrencyType.JewelOfLife)}";
            string vip = _currency.IsVipActive
                ? $"VIP Active\nEXP x{_currency.ExperienceMultiplier:F1}\nDrop x{_currency.DropMultiplier:F1}\nExtra Rows {_currency.ExtraInventoryRows}\nUnlimited Teleport {_currency.UnlimitedTeleport}"
                : "VIP Inactive";
            _view.RenderWallet(wallet, vip);
        }
    }
}