using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MuLike.Economy;
using MuLike.UI.Economy;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Builds an Economy UI prefab with trade, auction, crafting, and wallet/VIP tabs.
    /// Output: Assets/_Project/Prefabs/UI/EconomyUI.prefab
    /// </summary>
    public static class EconomyUiPrefabBuilder
    {
        private const string OutputPath = "Assets/_Project/Prefabs/UI/EconomyUI.prefab";

        [MenuItem("MuLike/Build/Create Economy UI Prefab")]
        public static void Build()
        {
            EnsureDirectory("Assets/_Project/Prefabs/UI");

            GameObject root = new("EconomyUI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            var view = root.AddComponent<EconomyHubView>();
            root.AddComponent<EconomyFlowController>();
            root.AddComponent<EconomyDemoBootstrap>();

            RectTransform modal = CreateRect("EconomyModal", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1480f, 880f));
            AddImage(modal.gameObject, new Color(0.05f, 0.07f, 0.10f, 0.96f));

            CreateText(modal, "HeaderText", "MU Economy Hub", 34f, new Vector2(0f, 400f), new Vector2(520f, 44f));
            CreateText(modal, "GeneralStatusText", "Economy ready.", 18f, new Vector2(0f, 360f), new Vector2(720f, 30f));
            CreateButton(modal, "CloseButton", "Close", new Vector2(650f, 400f), new Vector2(120f, 42f), new Color(0.55f, 0.18f, 0.18f, 1f));

            CreateTabButton(modal, "TabTradeButton", "Trade", new Vector2(-460f, 320f));
            CreateTabButton(modal, "TabAuctionButton", "Auction", new Vector2(-220f, 320f));
            CreateTabButton(modal, "TabCraftButton", "Craft", new Vector2(20f, 320f));
            CreateTabButton(modal, "TabWalletButton", "Wallet/VIP", new Vector2(260f, 320f));

            RectTransform inventoryPanel = CreatePanel(modal, "InventoryPanel", new Vector2(-470f, -20f), new Vector2(400f, 620f));
            CreateText(inventoryPanel, "InventoryHeader", "Inventory Snapshot", 24f, new Vector2(0f, 260f), new Vector2(340f, 34f));
            CreateScrollableText(inventoryPanel, "InventoryScroll", "InventoryText", new Vector2(0f, -10f), new Vector2(360f, 500f));

            RectTransform tradePanel = CreatePanel(modal, "TradePanel", new Vector2(220f, -20f), new Vector2(920f, 620f), true);
            BuildTradePanel(tradePanel);

            RectTransform auctionPanel = CreatePanel(modal, "AuctionPanel", new Vector2(220f, -20f), new Vector2(920f, 620f), false);
            BuildAuctionPanel(auctionPanel);

            RectTransform craftPanel = CreatePanel(modal, "CraftPanel", new Vector2(220f, -20f), new Vector2(920f, 620f), false);
            BuildCraftPanel(craftPanel);

            RectTransform walletPanel = CreatePanel(modal, "WalletPanel", new Vector2(220f, -20f), new Vector2(920f, 620f), false);
            BuildWalletPanel(walletPanel);

            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out success);
            Object.DestroyImmediate(root);

            if (success)
                Debug.Log($"[EconomyUiPrefabBuilder] Prefab generated: {OutputPath}");
            else
                Debug.LogError("[EconomyUiPrefabBuilder] Failed to generate EconomyUI prefab.");
        }

        private static void BuildTradePanel(RectTransform parent)
        {
            CreateText(parent, "TradeTitle", "Secure Trade", 26f, new Vector2(0f, 270f), new Vector2(400f, 32f));
            CreateInput(parent, "TradeTargetInput", "Target Player", new Vector2(-250f, 220f), new Vector2(220f, 44f));
            CreateButton(parent, "TradeCreateButton", "Create Trade", new Vector2(10f, 220f), new Vector2(180f, 44f), new Color(0.20f, 0.52f, 0.80f, 1f));
            CreateInput(parent, "TradeSessionInput", "Session Id", new Vector2(240f, 220f), new Vector2(240f, 44f));

            CreateInput(parent, "TradeItemInput", "Item Instance Id", new Vector2(-250f, 150f), new Vector2(220f, 44f));
            CreateButton(parent, "TradeAddItemButton", "Add Item", new Vector2(10f, 150f), new Vector2(180f, 44f), new Color(0.20f, 0.52f, 0.80f, 1f));

            CreateInput(parent, "TradeZenInput", "Zen", new Vector2(-320f, 70f), new Vector2(120f, 42f));
            CreateInput(parent, "TradeBlessInput", "Bless", new Vector2(-180f, 70f), new Vector2(120f, 42f));
            CreateInput(parent, "TradeSoulInput", "Soul", new Vector2(-40f, 70f), new Vector2(120f, 42f));
            CreateInput(parent, "TradeChaosInput", "Chaos", new Vector2(100f, 70f), new Vector2(120f, 42f));
            CreateInput(parent, "TradeLifeInput", "Life", new Vector2(240f, 70f), new Vector2(120f, 42f));
            CreateButton(parent, "TradeSetCurrencyButton", "Set Offer", new Vector2(0f, 5f), new Vector2(200f, 44f), new Color(0.68f, 0.45f, 0.18f, 1f));

            CreateButton(parent, "TradeConfirmButton", "Confirm", new Vector2(-120f, -70f), new Vector2(180f, 48f), new Color(0.18f, 0.60f, 0.32f, 1f));
            CreateButton(parent, "TradeCancelButton", "Cancel", new Vector2(120f, -70f), new Vector2(180f, 48f), new Color(0.65f, 0.22f, 0.22f, 1f));
            CreateText(parent, "TradeStatusText", "No active trade.", 18f, new Vector2(0f, -150f), new Vector2(760f, 140f));
        }

        private static void BuildAuctionPanel(RectTransform parent)
        {
            CreateText(parent, "AuctionTitle", "Global Auction House", 26f, new Vector2(0f, 270f), new Vector2(420f, 32f));
            CreateInput(parent, "AuctionItemInput", "Item Instance Id", new Vector2(-250f, 220f), new Vector2(220f, 44f));
            CreateInput(parent, "AuctionBuyNowInput", "Buy Now Zen", new Vector2(0f, 220f), new Vector2(180f, 44f));
            CreateInput(parent, "AuctionOpeningBidInput", "Opening Bid", new Vector2(220f, 220f), new Vector2(180f, 44f));
            CreateButton(parent, "AuctionCreateButton", "Create Listing", new Vector2(0f, 165f), new Vector2(220f, 44f), new Color(0.20f, 0.52f, 0.80f, 1f));

            CreateDropdown(parent, "AuctionClassDropdown", new Vector2(-300f, 95f), new Vector2(160f, 42f));
            CreateDropdown(parent, "AuctionCategoryDropdown", new Vector2(-110f, 95f), new Vector2(180f, 42f));
            CreateDropdown(parent, "AuctionRarityDropdown", new Vector2(100f, 95f), new Vector2(160f, 42f));
            CreateInput(parent, "AuctionMinLevelInput", "Min Lvl", new Vector2(280f, 95f), new Vector2(120f, 42f));
            CreateInput(parent, "AuctionMaxLevelInput", "Max Lvl", new Vector2(420f, 95f), new Vector2(120f, 42f));
            CreateButton(parent, "AuctionRefreshButton", "Refresh Listings", new Vector2(0f, 35f), new Vector2(220f, 42f), new Color(0.22f, 0.60f, 0.68f, 1f));

            CreateInput(parent, "AuctionBidListingInput", "Listing Id", new Vector2(-210f, -35f), new Vector2(260f, 42f));
            CreateInput(parent, "AuctionBidAmountInput", "Bid Zen", new Vector2(60f, -35f), new Vector2(180f, 42f));
            CreateButton(parent, "AuctionBidButton", "Bid", new Vector2(270f, -35f), new Vector2(120f, 42f), new Color(0.72f, 0.45f, 0.12f, 1f));
            CreateButton(parent, "AuctionBuyNowButton", "Buy Now", new Vector2(410f, -35f), new Vector2(140f, 42f), new Color(0.18f, 0.60f, 0.32f, 1f));

            CreateScrollableText(parent, "AuctionListingsScroll", "AuctionListingsText", new Vector2(-180f, -210f), new Vector2(430f, 260f));
            CreateScrollableText(parent, "PriceHistoryScroll", "PriceHistoryText", new Vector2(250f, -210f), new Vector2(290f, 260f));
            CreateText(parent, "AuctionStatusText", "Auction ready.", 18f, new Vector2(0f, -290f), new Vector2(760f, 40f));
        }

        private static void BuildCraftPanel(RectTransform parent)
        {
            CreateText(parent, "CraftTitle", "Chaos Machine", 26f, new Vector2(0f, 270f), new Vector2(320f, 32f));
            CreateInput(parent, "CombineInput", "Instance IDs: a,b,c", new Vector2(-160f, 210f), new Vector2(340f, 44f));
            CreateButton(parent, "CraftCombineButton", "Combine Items", new Vector2(180f, 210f), new Vector2(200f, 44f), new Color(0.20f, 0.52f, 0.80f, 1f));

            CreateInput(parent, "WingBaseInput", "Wing Base Item Id", new Vector2(-180f, 130f), new Vector2(280f, 44f));
            CreateDropdown(parent, "WingLevelDropdown", new Vector2(110f, 130f), new Vector2(120f, 44f));
            CreateButton(parent, "CraftWingButton", "Create Wing", new Vector2(280f, 130f), new Vector2(180f, 44f), new Color(0.64f, 0.32f, 0.72f, 1f));

            CreateInput(parent, "SocketItemInput", "Socket Item Id", new Vector2(-180f, 50f), new Vector2(280f, 44f));
            CreateButton(parent, "CraftSocketButton", "Add Socket", new Vector2(160f, 50f), new Vector2(180f, 44f), new Color(0.68f, 0.45f, 0.18f, 1f));

            CreateInput(parent, "ElementalItemInput", "Elemental Item Id", new Vector2(-180f, -30f), new Vector2(280f, 44f));
            CreateDropdown(parent, "ElementDropdown", new Vector2(120f, -30f), new Vector2(160f, 44f));
            CreateButton(parent, "CraftElementButton", "Apply Element", new Vector2(320f, -30f), new Vector2(190f, 44f), new Color(0.18f, 0.60f, 0.32f, 1f));

            CreateText(parent, "CraftStatusText", "Chaos Machine idle.", 20f, new Vector2(0f, -160f), new Vector2(760f, 180f));
        }

        private static void BuildWalletPanel(RectTransform parent)
        {
            CreateText(parent, "WalletTitle", "Wallet & VIP", 26f, new Vector2(0f, 270f), new Vector2(300f, 32f));
            CreateText(parent, "WalletText", "Balances", 22f, new Vector2(-180f, 80f), new Vector2(320f, 340f));
            CreateText(parent, "VipText", "VIP Info", 22f, new Vector2(180f, 80f), new Vector2(320f, 340f));
            CreateButton(parent, "VipActivateButton", "Activate VIP Monthly", new Vector2(-150f, -170f), new Vector2(260f, 52f), new Color(0.85f, 0.62f, 0.14f, 1f));
            CreateButton(parent, "WalletRefreshButton", "Refresh Wallet", new Vector2(150f, -170f), new Vector2(220f, 52f), new Color(0.20f, 0.52f, 0.80f, 1f));
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Vector2 pos, Vector2 size, bool visible = true)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            AddImage(rt.gameObject, new Color(0.11f, 0.13f, 0.17f, 0.95f));
            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
            return rt;
        }

        private static void CreateScrollableText(RectTransform parent, string scrollName, string textName, Vector2 pos, Vector2 size)
        {
            RectTransform scrollRt = CreateRect(scrollName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            AddImage(scrollRt.gameObject, new Color(0.08f, 0.10f, 0.14f, 0.88f));
            ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            RectTransform viewport = CreateRect("Viewport", scrollRt, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size - new Vector2(12f, 12f));
            Image viewportImage = AddImage(viewport.gameObject, new Color(1f, 1f, 1f, 0.02f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -8f), size - new Vector2(24f, 24f));
            content.anchorMax = new Vector2(1f, 1f);
            content.sizeDelta = Vector2.zero;
            scroll.content = content;

            TMP_Text text = CreateText(content, textName, string.Empty, 18f, new Vector2(0f, 0f), size - new Vector2(40f, 24f));
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, Vector2 pos, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            TMP_Text tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static void CreateInput(RectTransform parent, string name, string placeholder, Vector2 pos, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            AddImage(rt.gameObject, new Color(0.16f, 0.18f, 0.22f, 0.96f));
            TMP_InputField input = rt.gameObject.AddComponent<TMP_InputField>();

            TMP_Text text = CreateText(rt, "Text", string.Empty, 18f, Vector2.zero, size - new Vector2(20f, 8f));
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10f, 4f);
            text.rectTransform.offsetMax = new Vector2(-10f, -4f);

            TMP_Text placeholderText = CreateText(rt, "Placeholder", placeholder, 18f, Vector2.zero, size - new Vector2(20f, 8f));
            placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.rectTransform.anchorMin = Vector2.zero;
            placeholderText.rectTransform.anchorMax = Vector2.one;
            placeholderText.rectTransform.offsetMin = new Vector2(10f, 4f);
            placeholderText.rectTransform.offsetMax = new Vector2(-10f, -4f);

            input.textViewport = rt;
            input.textComponent = text as TextMeshProUGUI;
            input.placeholder = placeholderText;
        }

        private static void CreateDropdown(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            AddImage(rt.gameObject, new Color(0.16f, 0.18f, 0.22f, 0.96f));
            TMP_Dropdown dropdown = rt.gameObject.AddComponent<TMP_Dropdown>();

            TMP_Text label = CreateText(rt, "Label", "Option", 18f, Vector2.zero, size - new Vector2(44f, 8f));
            label.alignment = TextAlignmentOptions.Left;
            label.rectTransform.offsetMin = new Vector2(10f, 4f);
            label.rectTransform.offsetMax = new Vector2(-34f, -4f);
            dropdown.captionText = label as TextMeshProUGUI;

            RectTransform arrow = CreateRect("Arrow", rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(16f, 16f));
            TMP_Text arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v";
            arrowText.fontSize = 18f;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = Color.white;

            RectTransform template = CreateRect("Template", rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, 180f));
            AddImage(template.gameObject, new Color(0.08f, 0.10f, 0.14f, 0.98f));
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            template.gameObject.SetActive(false);
            dropdown.template = template;

            RectTransform viewport = CreateRect("Viewport", template, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(size.x - 12f, 170f));
            AddImage(viewport.gameObject, new Color(1f, 1f, 1f, 0.02f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(size.x - 20f, 170f));
            scroll.content = content;

            RectTransform item = CreateRect("Item", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(size.x - 20f, 36f));
            AddImage(item.gameObject, new Color(0.14f, 0.16f, 0.20f, 0.96f));
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            dropdown.itemText = CreateText(item, "Item Label", "Option", 18f, Vector2.zero, new Vector2(size.x - 36f, 36f)) as TextMeshProUGUI;
            dropdown.itemText.alignment = TextAlignmentOptions.Left;
            dropdown.itemText.rectTransform.offsetMin = new Vector2(10f, 4f);
            dropdown.itemText.rectTransform.offsetMax = new Vector2(-10f, -4f);
            dropdown.itemText.color = Color.white;
            dropdown.itemImage = null;
            dropdown.template = template;
        }

        private static void CreateTabButton(RectTransform parent, string name, string label, Vector2 pos)
        {
            CreateButton(parent, name, label, pos, new Vector2(200f, 48f), new Color(0.14f, 0.24f, 0.38f, 1f));
        }

        private static void CreateButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            AddImage(rt.gameObject, color);
            rt.gameObject.AddComponent<Button>();
            CreateText(rt, "Label", label, 18f, Vector2.zero, size);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            Image img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}