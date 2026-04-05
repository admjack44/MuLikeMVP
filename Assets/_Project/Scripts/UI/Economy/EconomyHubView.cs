using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.Economy
{
    /// <summary>
    /// Economy hub view for trade, auction house, chaos machine, and wallet/VIP.
    /// View-only component that emits user intents and renders current state.
    /// </summary>
    public sealed class EconomyHubView : MonoBehaviour
    {
        public enum Tab
        {
            Trade,
            Auction,
            Crafting,
            Wallet
        }

        [Serializable]
        public struct AuctionFilterInput
        {
            public int classRestrictionIndex;
            public int categoryIndex;
            public int rarityIndex;
            public int minLevel;
            public int maxLevel;
        }

        public event Action<string, string> TradeCreateRequested;
        public event Action<string, string, string> TradeAddItemRequested;
        public event Action<string, string, long, int, int, int, int> TradeCurrencyRequested;
        public event Action<string, string> TradeConfirmRequested;
        public event Action<string, string> TradeCancelRequested;
        public event Action<string, string, long, long> AuctionCreateRequested;
        public event Action<AuctionFilterInput> AuctionRefreshRequested;
        public event Action<string, string, long> AuctionBidRequested;
        public event Action<string, string> AuctionBuyNowRequested;
        public event Action<string> CraftCombineRequested;
        public event Action<int, string> CraftWingRequested;
        public event Action<string> CraftSocketRequested;
        public event Action<string, int> CraftElementRequested;
        public event Action VipActivateRequested;
        public event Action WalletRefreshRequested;

        private GameObject _modalRoot;
        private TMP_Text _inventoryText;
        private TMP_Text _walletText;
        private TMP_Text _vipText;
        private TMP_Text _tradeStatusText;
        private TMP_Text _auctionStatusText;
        private TMP_Text _craftStatusText;
        private TMP_Text _generalStatusText;
        private TMP_Text _auctionListingsText;
        private TMP_Text _priceHistoryText;

        private TMP_InputField _tradeTargetInput;
        private TMP_InputField _tradeSessionInput;
        private TMP_InputField _tradeItemInput;
        private TMP_InputField _tradeZenInput;
        private TMP_InputField _tradeBlessInput;
        private TMP_InputField _tradeSoulInput;
        private TMP_InputField _tradeChaosInput;
        private TMP_InputField _tradeLifeInput;

        private TMP_InputField _auctionItemInput;
        private TMP_InputField _auctionBuyNowInput;
        private TMP_InputField _auctionOpeningBidInput;
        private TMP_InputField _auctionBidListingInput;
        private TMP_InputField _auctionBidAmountInput;
        private TMP_InputField _auctionMinLevelInput;
        private TMP_InputField _auctionMaxLevelInput;
        private TMP_Dropdown _auctionClassDropdown;
        private TMP_Dropdown _auctionCategoryDropdown;
        private TMP_Dropdown _auctionRarityDropdown;

        private TMP_InputField _combineInput;
        private TMP_InputField _wingBaseInput;
        private TMP_Dropdown _wingLevelDropdown;
        private TMP_InputField _socketItemInput;
        private TMP_InputField _elementalItemInput;
        private TMP_Dropdown _elementDropdown;

        private CanvasGroup _tradePanel;
        private CanvasGroup _auctionPanel;
        private CanvasGroup _craftPanel;
        private CanvasGroup _walletPanel;

        private bool _visible;

        public bool IsVisible => _visible;

        private void Awake()
        {
            ResolveReferences();
            WireButtons();
            ConfigureDropdowns();
            SetVisible(false);
            ShowTab(Tab.Trade);
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_modalRoot != null)
                _modalRoot.SetActive(visible);
        }

        public void ToggleVisible()
        {
            SetVisible(!_visible);
        }

        public void ShowTab(Tab tab)
        {
            SetPanel(_tradePanel, tab == Tab.Trade);
            SetPanel(_auctionPanel, tab == Tab.Auction);
            SetPanel(_craftPanel, tab == Tab.Crafting);
            SetPanel(_walletPanel, tab == Tab.Wallet);
        }

        public void RenderInventorySummary(IReadOnlyList<string> lines)
        {
            if (_inventoryText == null)
                return;

            _inventoryText.text = lines == null || lines.Count == 0 ? "Inventory empty." : string.Join("\n", lines);
        }

        public void RenderWallet(string wallet, string vip)
        {
            if (_walletText != null)
                _walletText.text = wallet ?? string.Empty;
            if (_vipText != null)
                _vipText.text = vip ?? string.Empty;
        }

        public void RenderTradeStatus(string text)
        {
            if (_tradeStatusText != null)
                _tradeStatusText.text = text ?? string.Empty;
        }

        public void RenderAuctionStatus(string text)
        {
            if (_auctionStatusText != null)
                _auctionStatusText.text = text ?? string.Empty;
        }

        public void RenderCraftStatus(string text)
        {
            if (_craftStatusText != null)
                _craftStatusText.text = text ?? string.Empty;
        }

        public void RenderGlobalStatus(string text)
        {
            if (_generalStatusText != null)
                _generalStatusText.text = text ?? string.Empty;
        }

        public void RenderAuctionListings(IReadOnlyList<string> lines)
        {
            if (_auctionListingsText != null)
                _auctionListingsText.text = lines == null || lines.Count == 0 ? "No listings." : string.Join("\n", lines);
        }

        public void RenderPriceHistory(IReadOnlyList<string> lines)
        {
            if (_priceHistoryText != null)
                _priceHistoryText.text = lines == null || lines.Count == 0 ? "No price history." : string.Join("\n", lines);
        }

        public void SetTradeSessionId(string sessionId)
        {
            if (_tradeSessionInput != null)
                _tradeSessionInput.text = sessionId ?? string.Empty;
        }

        private void ResolveReferences()
        {
            _modalRoot = FindChild("EconomyModal")?.gameObject;
            _inventoryText = FindText("InventoryText");
            _walletText = FindText("WalletText");
            _vipText = FindText("VipText");
            _tradeStatusText = FindText("TradeStatusText");
            _auctionStatusText = FindText("AuctionStatusText");
            _craftStatusText = FindText("CraftStatusText");
            _generalStatusText = FindText("GeneralStatusText");
            _auctionListingsText = FindText("AuctionListingsText");
            _priceHistoryText = FindText("PriceHistoryText");

            _tradeTargetInput = FindInput("TradeTargetInput");
            _tradeSessionInput = FindInput("TradeSessionInput");
            _tradeItemInput = FindInput("TradeItemInput");
            _tradeZenInput = FindInput("TradeZenInput");
            _tradeBlessInput = FindInput("TradeBlessInput");
            _tradeSoulInput = FindInput("TradeSoulInput");
            _tradeChaosInput = FindInput("TradeChaosInput");
            _tradeLifeInput = FindInput("TradeLifeInput");

            _auctionItemInput = FindInput("AuctionItemInput");
            _auctionBuyNowInput = FindInput("AuctionBuyNowInput");
            _auctionOpeningBidInput = FindInput("AuctionOpeningBidInput");
            _auctionBidListingInput = FindInput("AuctionBidListingInput");
            _auctionBidAmountInput = FindInput("AuctionBidAmountInput");
            _auctionMinLevelInput = FindInput("AuctionMinLevelInput");
            _auctionMaxLevelInput = FindInput("AuctionMaxLevelInput");
            _auctionClassDropdown = FindDropdown("AuctionClassDropdown");
            _auctionCategoryDropdown = FindDropdown("AuctionCategoryDropdown");
            _auctionRarityDropdown = FindDropdown("AuctionRarityDropdown");

            _combineInput = FindInput("CombineInput");
            _wingBaseInput = FindInput("WingBaseInput");
            _wingLevelDropdown = FindDropdown("WingLevelDropdown");
            _socketItemInput = FindInput("SocketItemInput");
            _elementalItemInput = FindInput("ElementalItemInput");
            _elementDropdown = FindDropdown("ElementDropdown");

            _tradePanel = FindCanvasGroup("TradePanel");
            _auctionPanel = FindCanvasGroup("AuctionPanel");
            _craftPanel = FindCanvasGroup("CraftPanel");
            _walletPanel = FindCanvasGroup("WalletPanel");
        }

        private void ConfigureDropdowns()
        {
            ConfigureDropdown(_auctionClassDropdown, new[] { "Any", "Warrior", "Mage", "Ranger", "Paladin", "DarkLord" });
            ConfigureDropdown(_auctionCategoryDropdown, new[] { "Any", "Weapon", "Shield", "Armor", "Accessory", "Consumable", "Material", "Quest", "Wings", "Pet", "Costume" });
            ConfigureDropdown(_auctionRarityDropdown, new[] { "Any", "Common", "Magic", "Rare", "Epic", "Legendary" });
            ConfigureDropdown(_wingLevelDropdown, new[] { "Lv1", "Lv2", "Lv3" });
            ConfigureDropdown(_elementDropdown, new[] { "Fire", "Ice", "Lightning", "Poison" });
        }

        private void WireButtons()
        {
            BindButton("TabTradeButton", () => ShowTab(Tab.Trade));
            BindButton("TabAuctionButton", () => ShowTab(Tab.Auction));
            BindButton("TabCraftButton", () => ShowTab(Tab.Crafting));
            BindButton("TabWalletButton", () => ShowTab(Tab.Wallet));
            BindButton("CloseButton", ToggleVisible);

            BindButton("TradeCreateButton", () => TradeCreateRequested?.Invoke(GetPlayerName(), Read(_tradeTargetInput)));
            BindButton("TradeAddItemButton", () => TradeAddItemRequested?.Invoke(Read(_tradeSessionInput), GetPlayerName(), Read(_tradeItemInput)));
            BindButton("TradeSetCurrencyButton", () => TradeCurrencyRequested?.Invoke(
                Read(_tradeSessionInput),
                GetPlayerName(),
                ReadLong(_tradeZenInput),
                ReadInt(_tradeBlessInput),
                ReadInt(_tradeSoulInput),
                ReadInt(_tradeChaosInput),
                ReadInt(_tradeLifeInput)));
            BindButton("TradeConfirmButton", () => TradeConfirmRequested?.Invoke(Read(_tradeSessionInput), GetPlayerName()));
            BindButton("TradeCancelButton", () => TradeCancelRequested?.Invoke(Read(_tradeSessionInput), GetPlayerName()));

            BindButton("AuctionCreateButton", () => AuctionCreateRequested?.Invoke(GetPlayerName(), Read(_auctionItemInput), ReadLong(_auctionBuyNowInput), ReadLong(_auctionOpeningBidInput)));
            BindButton("AuctionRefreshButton", () => AuctionRefreshRequested?.Invoke(new AuctionFilterInput
            {
                classRestrictionIndex = _auctionClassDropdown != null ? _auctionClassDropdown.value : 0,
                categoryIndex = _auctionCategoryDropdown != null ? _auctionCategoryDropdown.value : 0,
                rarityIndex = _auctionRarityDropdown != null ? _auctionRarityDropdown.value : 0,
                minLevel = ReadInt(_auctionMinLevelInput),
                maxLevel = ReadInt(_auctionMaxLevelInput)
            }));
            BindButton("AuctionBidButton", () => AuctionBidRequested?.Invoke(GetPlayerName(), Read(_auctionBidListingInput), ReadLong(_auctionBidAmountInput)));
            BindButton("AuctionBuyNowButton", () => AuctionBuyNowRequested?.Invoke(GetPlayerName(), Read(_auctionBidListingInput)));

            BindButton("CraftCombineButton", () => CraftCombineRequested?.Invoke(Read(_combineInput)));
            BindButton("CraftWingButton", () => CraftWingRequested?.Invoke((_wingLevelDropdown != null ? _wingLevelDropdown.value : 0) + 1, Read(_wingBaseInput)));
            BindButton("CraftSocketButton", () => CraftSocketRequested?.Invoke(Read(_socketItemInput)));
            BindButton("CraftElementButton", () => CraftElementRequested?.Invoke(Read(_elementalItemInput), _elementDropdown != null ? _elementDropdown.value : 0));

            BindButton("VipActivateButton", () => VipActivateRequested?.Invoke());
            BindButton("WalletRefreshButton", () => WalletRefreshRequested?.Invoke());
        }

        private void UnwireButtons()
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
        }

        private void BindButton(string childName, Action action)
        {
            Button button = FindButton(childName);
            if (button != null)
                button.onClick.AddListener(() => action?.Invoke());
        }

        private void SetPanel(CanvasGroup panel, bool visible)
        {
            if (panel == null)
                return;
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
        }

        private static void ConfigureDropdown(TMP_Dropdown dropdown, string[] options)
        {
            if (dropdown == null)
                return;
            dropdown.ClearOptions();
            var data = new List<TMP_Dropdown.OptionData>(options.Length);
            for (int i = 0; i < options.Length; i++)
                data.Add(new TMP_Dropdown.OptionData(options[i]));
            dropdown.AddOptions(data);
            dropdown.SetValueWithoutNotify(0);
        }

        private string GetPlayerName()
        {
            return "Player";
        }

        private RectTransform FindChild(string name)
        {
            Transform child = transform.Find(name);
            if (child != null)
                return child as RectTransform;

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                    return all[i] as RectTransform;
            }
            return null;
        }

        private TMP_Text FindText(string name)
        {
            RectTransform rt = FindChild(name);
            return rt != null ? rt.GetComponent<TMP_Text>() : null;
        }

        private TMP_InputField FindInput(string name)
        {
            RectTransform rt = FindChild(name);
            return rt != null ? rt.GetComponent<TMP_InputField>() : null;
        }

        private TMP_Dropdown FindDropdown(string name)
        {
            RectTransform rt = FindChild(name);
            return rt != null ? rt.GetComponent<TMP_Dropdown>() : null;
        }

        private Button FindButton(string name)
        {
            RectTransform rt = FindChild(name);
            return rt != null ? rt.GetComponent<Button>() : null;
        }

        private CanvasGroup FindCanvasGroup(string name)
        {
            RectTransform rt = FindChild(name);
            return rt != null ? rt.GetComponent<CanvasGroup>() : null;
        }

        private static string Read(TMP_InputField field)
        {
            return field != null ? field.text : string.Empty;
        }

        private static int ReadInt(TMP_InputField field)
        {
            return int.TryParse(Read(field), out int value) ? value : 0;
        }

        private static long ReadLong(TMP_InputField field)
        {
            return long.TryParse(Read(field), out long value) ? value : 0L;
        }
    }
}