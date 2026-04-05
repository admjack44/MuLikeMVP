using System;
using System.Collections.Generic;
using MuLike.Chat;
using MuLike.Core;
using MuLike.Inventory;
using UnityEngine;

namespace MuLike.Economy
{
    /// <summary>
    /// 1v1 secure trade runtime with double-confirmation, offer verification, and high-value tax.
    /// </summary>
    public sealed class TradeSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class TradeOffer
        {
            public readonly List<InventoryManager.InventoryItem> items = new();
            public long zen;
            public int bless;
            public int soul;
            public int chaos;
            public int life;
        }

        [Serializable]
        public sealed class TradeSession
        {
            public string sessionId;
            public string playerA;
            public string playerB;
            public readonly TradeOffer offerA = new();
            public readonly TradeOffer offerB = new();
            public bool confirmA;
            public bool confirmB;
            public string verificationHashA;
            public string verificationHashB;
            public bool completed;
        }

        [Header("Tax")]
        [SerializeField, Range(0f, 0.25f)] private float _highValueTradeTax = 0.05f;
        [SerializeField, Min(1)] private long _highValueThreshold = 25000;

        [Header("Dependencies")]
        [SerializeField] private InventoryManager _inventoryManager;
        [SerializeField] private CurrencyManager _currencyManager;
        [SerializeField] private ChatSystem _chatSystem;

        private readonly Dictionary<string, TradeSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, TradeSession> Sessions => _sessions;

        public event Action<TradeSession> OnTradeUpdated;
        public event Action<TradeSession, bool, string> OnTradeCompleted;

        private void Awake()
        {
            if (_inventoryManager == null)
                _inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (_currencyManager == null)
                _currencyManager = FindAnyObjectByType<CurrencyManager>();
            if (_chatSystem == null)
                _chatSystem = FindAnyObjectByType<ChatSystem>();

            GameContext.RegisterSystem(this);
        }

        public TradeSession CreateTrade(string playerA, string playerB)
        {
            var session = new TradeSession
            {
                sessionId = Guid.NewGuid().ToString("N"),
                playerA = playerA,
                playerB = playerB
            };
            _sessions[session.sessionId] = session;
            OnTradeUpdated?.Invoke(session);
            return session;
        }

        public bool AddItemOffer(string sessionId, string playerName, string itemInstanceId)
        {
            if (!_sessions.TryGetValue(sessionId, out TradeSession session) || session.completed || _inventoryManager == null)
                return false;

            if (!_inventoryManager.TryRemoveItem(itemInstanceId, out InventoryManager.InventoryItem removed) || removed == null)
                return false;

            TradeOffer offer = ResolveOffer(session, playerName);
            if (offer == null)
            {
                _inventoryManager.TryAddItem(removed);
                return false;
            }

            offer.items.Add(removed);
            ResetConfirmation(session);
            OnTradeUpdated?.Invoke(session);
            return true;
        }

        public bool SetCurrencyOffer(string sessionId, string playerName, long zen, int bless, int soul, int chaos, int life)
        {
            if (!_sessions.TryGetValue(sessionId, out TradeSession session) || session.completed)
                return false;

            TradeOffer offer = ResolveOffer(session, playerName);
            if (offer == null)
                return false;

            long currentZen = offer.zen;
            int currentBless = offer.bless;
            int currentSoul = offer.soul;
            int currentChaos = offer.chaos;
            int currentLife = offer.life;

            if (!TryReserveCurrencies(currentZen, currentBless, currentSoul, currentChaos, currentLife, zen, bless, soul, chaos, life))
                return false;

            offer.zen = Math.Max(0L, zen);
            offer.bless = Mathf.Max(0, bless);
            offer.soul = Mathf.Max(0, soul);
            offer.chaos = Mathf.Max(0, chaos);
            offer.life = Mathf.Max(0, life);
            ResetConfirmation(session);
            OnTradeUpdated?.Invoke(session);
            return true;
        }

        public bool ConfirmTrade(string sessionId, string playerName)
        {
            if (!_sessions.TryGetValue(sessionId, out TradeSession session) || session.completed)
                return false;

            string hash = BuildVerificationHash(ResolveOffer(session, playerName));
            if (string.Equals(session.playerA, playerName, StringComparison.OrdinalIgnoreCase))
            {
                session.confirmA = true;
                session.verificationHashA = hash;
            }
            else if (string.Equals(session.playerB, playerName, StringComparison.OrdinalIgnoreCase))
            {
                session.confirmB = true;
                session.verificationHashB = hash;
            }
            else
            {
                return false;
            }

            OnTradeUpdated?.Invoke(session);

            if (!session.confirmA || !session.confirmB)
                return true;

            bool hashesValid = session.verificationHashA == BuildVerificationHash(session.offerA)
                && session.verificationHashB == BuildVerificationHash(session.offerB);
            if (!hashesValid)
            {
                CancelTrade(sessionId, "Trade verification failed.");
                return false;
            }

            return FinalizeTrade(session);
        }

        public bool CancelTrade(string sessionId, string reason = null)
        {
            if (!_sessions.TryGetValue(sessionId, out TradeSession session) || session.completed)
                return false;

            RollbackOffer(session.offerA);
            RollbackOffer(session.offerB);
            _sessions.Remove(sessionId);
            OnTradeCompleted?.Invoke(session, false, reason ?? "Trade cancelled.");
            return true;
        }

        private bool FinalizeTrade(TradeSession session)
        {
            float taxRateA = ResolveTradeTaxRate(session.offerA);
            float taxRateB = ResolveTradeTaxRate(session.offerB);

            if (!TransferOffer(session.offerA, session.offerB, taxRateA))
            {
                CancelTrade(session.sessionId, "Trade transfer failed.");
                return false;
            }

            if (!TransferOffer(session.offerB, session.offerA, taxRateB))
            {
                CancelTrade(session.sessionId, "Trade transfer failed.");
                return false;
            }

            session.completed = true;
            _sessions.Remove(session.sessionId);
            _chatSystem?.ReceiveSystemMessage($"Secure trade completed between {session.playerA} and {session.playerB}.");
            OnTradeCompleted?.Invoke(session, true, "Trade completed.");
            return true;
        }

        private bool TransferOffer(TradeOffer source, TradeOffer destination, float taxRate)
        {
            if (_inventoryManager == null || _currencyManager == null)
                return false;

            for (int i = 0; i < source.items.Count; i++)
            {
                if (!_inventoryManager.TryAddItem(source.items[i]))
                    return false;
            }

            AddNetCurrency(CurrencyManager.CurrencyType.Zen, source.zen, taxRate);
            AddNetCurrency(CurrencyManager.CurrencyType.JewelOfBless, source.bless, taxRate);
            AddNetCurrency(CurrencyManager.CurrencyType.JewelOfSoul, source.soul, taxRate);
            AddNetCurrency(CurrencyManager.CurrencyType.JewelOfChaos, source.chaos, taxRate);
            AddNetCurrency(CurrencyManager.CurrencyType.JewelOfLife, source.life, taxRate);
            return true;
        }

        private void AddNetCurrency(CurrencyManager.CurrencyType type, long amount, float taxRate)
        {
            if (amount <= 0)
                return;

            long net = Math.Max(0L, (long)Math.Round(amount * (1f - Mathf.Clamp01(taxRate))));
            _currencyManager.AddCurrency(type, net);
        }

        private float ResolveTradeTaxRate(TradeOffer offer)
        {
            long value = offer.zen
                + offer.bless * 12000L
                + offer.soul * 15000L
                + offer.chaos * 22000L
                + offer.life * 18000L;

            for (int i = 0; i < offer.items.Count; i++)
                value += _currencyManager != null ? _currencyManager.EstimateHighValueScore(offer.items[i]) : 0L;

            return value >= _highValueThreshold ? _highValueTradeTax : 0f;
        }

        private bool TryReserveCurrencies(long oldZen, int oldBless, int oldSoul, int oldChaos, int oldLife, long newZen, int newBless, int newSoul, int newChaos, int newLife)
        {
            if (_currencyManager == null)
                return false;

            if (newZen > oldZen && !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.Zen, newZen - oldZen))
                return false;
            if (newBless > oldBless && !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfBless, newBless - oldBless))
                return false;
            if (newSoul > oldSoul && !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfSoul, newSoul - oldSoul))
                return false;
            if (newChaos > oldChaos && !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfChaos, newChaos - oldChaos))
                return false;
            if (newLife > oldLife && !_currencyManager.TrySpendCurrency(CurrencyManager.CurrencyType.JewelOfLife, newLife - oldLife))
                return false;

            if (oldZen > newZen) _currencyManager.AddCurrency(CurrencyManager.CurrencyType.Zen, oldZen - newZen);
            if (oldBless > newBless) _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfBless, oldBless - newBless);
            if (oldSoul > newSoul) _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfSoul, oldSoul - newSoul);
            if (oldChaos > newChaos) _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfChaos, oldChaos - newChaos);
            if (oldLife > newLife) _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfLife, oldLife - newLife);
            return true;
        }

        private void RollbackOffer(TradeOffer offer)
        {
            if (_inventoryManager != null)
            {
                for (int i = 0; i < offer.items.Count; i++)
                    _inventoryManager.TryAddItem(offer.items[i]);
            }

            if (_currencyManager != null)
            {
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.Zen, offer.zen);
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfBless, offer.bless);
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfSoul, offer.soul);
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfChaos, offer.chaos);
                _currencyManager.AddCurrency(CurrencyManager.CurrencyType.JewelOfLife, offer.life);
            }
        }

        private static void ResetConfirmation(TradeSession session)
        {
            session.confirmA = false;
            session.confirmB = false;
            session.verificationHashA = string.Empty;
            session.verificationHashB = string.Empty;
        }

        private static string BuildVerificationHash(TradeOffer offer)
        {
            if (offer == null)
                return string.Empty;

            var buffer = new System.Text.StringBuilder(256);
            buffer.Append(offer.zen).Append('|').Append(offer.bless).Append('|').Append(offer.soul).Append('|').Append(offer.chaos).Append('|').Append(offer.life);
            for (int i = 0; i < offer.items.Count; i++)
            {
                InventoryManager.InventoryItem item = offer.items[i];
                if (item == null)
                    continue;
                buffer.Append('|').Append(item.itemId).Append(':').Append(item.enhancementLevel).Append(':').Append(item.quantity).Append(':').Append(item.rarity);
            }

            return buffer.ToString();
        }

        private static TradeOffer ResolveOffer(TradeSession session, string playerName)
        {
            if (session == null)
                return null;

            if (string.Equals(session.playerA, playerName, StringComparison.OrdinalIgnoreCase))
                return session.offerA;
            if (string.Equals(session.playerB, playerName, StringComparison.OrdinalIgnoreCase))
                return session.offerB;
            return null;
        }
    }
}