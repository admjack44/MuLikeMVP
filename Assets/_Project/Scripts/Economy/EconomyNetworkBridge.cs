using System;
using System.Threading.Tasks;
using MuLike.Chat;
using MuLike.Networking;
using MuLike.Shared.Protocol;
using UnityEngine;

namespace MuLike.Economy
{
    /// <summary>
    /// Network-ready bridge for trade and auction commands.
    /// Falls back to direct local execution when no network transport is available.
    /// </summary>
    public sealed class EconomyNetworkBridge : MonoBehaviour
    {
        [Serializable]
        private struct TradeCreatePayload
        {
            public string playerA;
            public string playerB;
        }

        [Serializable]
        private struct TradeItemPayload
        {
            public string sessionId;
            public string playerName;
            public string itemInstanceId;
        }

        [Serializable]
        private struct TradeCurrencyPayload
        {
            public string sessionId;
            public string playerName;
            public long zen;
            public int bless;
            public int soul;
            public int chaos;
            public int life;
        }

        [Serializable]
        private struct TradeConfirmPayload
        {
            public string sessionId;
            public string playerName;
        }

        [Serializable]
        private struct AuctionCreatePayload
        {
            public string seller;
            public string itemInstanceId;
            public long buyNowZen;
            public long openingBidZen;
        }

        [Serializable]
        private struct AuctionBidPayload
        {
            public string bidder;
            public string listingId;
            public long amountZen;
        }

        [Serializable]
        private struct AuctionBuyPayload
        {
            public string buyer;
            public string listingId;
        }

        [Header("Dependencies")]
        [SerializeField] private NetworkGameClient _networkClient;
        [SerializeField] private TradeSystem _tradeSystem;
        [SerializeField] private AuctionHouse _auctionHouse;
        [SerializeField] private ChatSystem _chatSystem;

        private PacketRouterEconomyTransport _transport;

        public bool HasNetworkTransport => _transport != null;

        private void Awake()
        {
            if (_networkClient == null)
                _networkClient = FindAnyObjectByType<NetworkGameClient>();
            if (_tradeSystem == null)
                _tradeSystem = FindAnyObjectByType<TradeSystem>();
            if (_auctionHouse == null)
                _auctionHouse = FindAnyObjectByType<AuctionHouse>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();

            TryAttachTransport();
        }

        public async Task CreateTradeAsync(string playerA, string playerB)
        {
            if (_transport == null)
            {
                _tradeSystem?.CreateTrade(playerA, playerB);
                return;
            }

            await _transport.SendTradeAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "trade",
                action = "create",
                sender = playerA,
                payloadJson = JsonUtility.ToJson(new TradeCreatePayload { playerA = playerA, playerB = playerB })
            });
        }

        public async Task AddTradeItemAsync(string sessionId, string playerName, string itemInstanceId)
        {
            if (_transport == null)
            {
                _tradeSystem?.AddItemOffer(sessionId, playerName, itemInstanceId);
                return;
            }

            await _transport.SendTradeAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "trade",
                action = "add-item",
                sender = playerName,
                payloadJson = JsonUtility.ToJson(new TradeItemPayload { sessionId = sessionId, playerName = playerName, itemInstanceId = itemInstanceId })
            });
        }

        public async Task SetTradeCurrencyAsync(string sessionId, string playerName, long zen, int bless, int soul, int chaos, int life)
        {
            if (_transport == null)
            {
                _tradeSystem?.SetCurrencyOffer(sessionId, playerName, zen, bless, soul, chaos, life);
                return;
            }

            await _transport.SendTradeAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "trade",
                action = "set-currency",
                sender = playerName,
                payloadJson = JsonUtility.ToJson(new TradeCurrencyPayload
                {
                    sessionId = sessionId,
                    playerName = playerName,
                    zen = zen,
                    bless = bless,
                    soul = soul,
                    chaos = chaos,
                    life = life
                })
            });
        }

        public async Task ConfirmTradeAsync(string sessionId, string playerName)
        {
            if (_transport == null)
            {
                _tradeSystem?.ConfirmTrade(sessionId, playerName);
                return;
            }

            await _transport.SendTradeAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "trade",
                action = "confirm",
                sender = playerName,
                payloadJson = JsonUtility.ToJson(new TradeConfirmPayload { sessionId = sessionId, playerName = playerName })
            });
        }

        public async Task CancelTradeAsync(string sessionId, string playerName)
        {
            if (_transport == null)
            {
                _tradeSystem?.CancelTrade(sessionId, $"Cancelled by {playerName}.");
                return;
            }

            await _transport.SendTradeAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "trade",
                action = "cancel",
                sender = playerName,
                payloadJson = JsonUtility.ToJson(new TradeConfirmPayload { sessionId = sessionId, playerName = playerName })
            });
        }

        public async Task CreateListingAsync(string seller, string itemInstanceId, long buyNowZen, long openingBidZen)
        {
            if (_transport == null)
            {
                _auctionHouse?.CreateListing(seller, itemInstanceId, buyNowZen, openingBidZen);
                return;
            }

            await _transport.SendAuctionAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "auction",
                action = "create-listing",
                sender = seller,
                payloadJson = JsonUtility.ToJson(new AuctionCreatePayload
                {
                    seller = seller,
                    itemInstanceId = itemInstanceId,
                    buyNowZen = buyNowZen,
                    openingBidZen = openingBidZen
                })
            });
        }

        public async Task BidAsync(string bidder, string listingId, long amountZen)
        {
            if (_transport == null)
            {
                _auctionHouse?.TryBid(bidder, listingId, amountZen);
                return;
            }

            await _transport.SendAuctionAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "auction",
                action = "bid",
                sender = bidder,
                payloadJson = JsonUtility.ToJson(new AuctionBidPayload { bidder = bidder, listingId = listingId, amountZen = amountZen })
            });
        }

        public async Task BuyNowAsync(string buyer, string listingId)
        {
            if (_transport == null)
            {
                _auctionHouse?.TryBuyNow(buyer, listingId);
                return;
            }

            await _transport.SendAuctionAsync(new PacketRouterEconomyTransport.EconomyEnvelope
            {
                channel = "auction",
                action = "buy-now",
                sender = buyer,
                payloadJson = JsonUtility.ToJson(new AuctionBuyPayload { buyer = buyer, listingId = listingId })
            });
        }

        private void TryAttachTransport()
        {
            if (_networkClient == null)
                return;

            if (_networkClient.PacketRouter == null)
                return;

            _transport = new PacketRouterEconomyTransport(
                _networkClient.PacketRouter,
                NetOpcodes.Economy.TradeEvent,
                NetOpcodes.Economy.TradeCommand,
                NetOpcodes.Economy.AuctionEvent,
                NetOpcodes.Economy.AuctionCommand,
                _networkClient.SendRawPacketAsync);

            _transport.TradeEnvelopeReceived += HandleTradeEnvelope;
            _transport.AuctionEnvelopeReceived += HandleAuctionEnvelope;
            _chatSystem?.ReceiveSystemMessage("Economy network bridge attached.");
        }

        private void HandleTradeEnvelope(PacketRouterEconomyTransport.EconomyEnvelope envelope)
        {
            switch (envelope.action)
            {
                case "create":
                {
                    TradeCreatePayload payload = JsonUtility.FromJson<TradeCreatePayload>(envelope.payloadJson);
                    _tradeSystem?.CreateTrade(payload.playerA, payload.playerB);
                    break;
                }
                case "add-item":
                {
                    TradeItemPayload payload = JsonUtility.FromJson<TradeItemPayload>(envelope.payloadJson);
                    _tradeSystem?.AddItemOffer(payload.sessionId, payload.playerName, payload.itemInstanceId);
                    break;
                }
                case "set-currency":
                {
                    TradeCurrencyPayload payload = JsonUtility.FromJson<TradeCurrencyPayload>(envelope.payloadJson);
                    _tradeSystem?.SetCurrencyOffer(payload.sessionId, payload.playerName, payload.zen, payload.bless, payload.soul, payload.chaos, payload.life);
                    break;
                }
                case "confirm":
                {
                    TradeConfirmPayload payload = JsonUtility.FromJson<TradeConfirmPayload>(envelope.payloadJson);
                    _tradeSystem?.ConfirmTrade(payload.sessionId, payload.playerName);
                    break;
                }
                case "cancel":
                {
                    TradeConfirmPayload payload = JsonUtility.FromJson<TradeConfirmPayload>(envelope.payloadJson);
                    _tradeSystem?.CancelTrade(payload.sessionId, $"Cancelled by {payload.playerName}.");
                    break;
                }
            }
        }

        private void HandleAuctionEnvelope(PacketRouterEconomyTransport.EconomyEnvelope envelope)
        {
            switch (envelope.action)
            {
                case "create-listing":
                {
                    AuctionCreatePayload payload = JsonUtility.FromJson<AuctionCreatePayload>(envelope.payloadJson);
                    _auctionHouse?.CreateListing(payload.seller, payload.itemInstanceId, payload.buyNowZen, payload.openingBidZen);
                    break;
                }
                case "bid":
                {
                    AuctionBidPayload payload = JsonUtility.FromJson<AuctionBidPayload>(envelope.payloadJson);
                    _auctionHouse?.TryBid(payload.bidder, payload.listingId, payload.amountZen);
                    break;
                }
                case "buy-now":
                {
                    AuctionBuyPayload payload = JsonUtility.FromJson<AuctionBuyPayload>(envelope.payloadJson);
                    _auctionHouse?.TryBuyNow(payload.buyer, payload.listingId);
                    break;
                }
            }
        }
    }
}