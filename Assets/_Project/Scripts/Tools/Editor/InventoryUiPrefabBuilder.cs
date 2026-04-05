using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Generates a baseline InventoryUI prefab for MU-style mobile inventory screen.
    /// Output: Assets/_Project/Prefabs/UI/InventoryUI.prefab
    /// </summary>
    public static class InventoryUiPrefabBuilder
    {
        private const string OutputPath = "Assets/_Project/Prefabs/UI/InventoryUI.prefab";

        [MenuItem("MuLike/Build/Create Inventory UI Prefab")]
        public static void Build()
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            EnsureDirectory("Assets/_Project/Prefabs/UI");

            GameObject root = new("InventoryUI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            RectTransform panel = CreateRect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1320f, 820f));
            Image panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            CreateTitle(panel, "Inventory - MU Immortal");
            CreateGrid(panel, "InventoryGrid", new Vector2(-290f, -20f), new Vector2(760f, 480f), 8, 4);
            CreateButton(panel, "AutoOrganizeButton", "Auto Organizar", new Vector2(140f, -330f), new Vector2(240f, 56f));
            CreateGrid(panel, "BankGrid", new Vector2(300f, 30f), new Vector2(500f, 340f), 10, 12);
            CreateButton(panel, "CashShopButton", "Cash Shop", new Vector2(450f, -330f), new Vector2(220f, 56f));

            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, OutputPath, out success);
            Object.DestroyImmediate(root);

            if (success)
                Debug.Log($"[InventoryUiPrefabBuilder] Prefab generated: {OutputPath}");
            else
                Debug.LogError("[InventoryUiPrefabBuilder] Failed to generate InventoryUI prefab.");
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

        private static void CreateTitle(RectTransform parent, string text)
        {
            RectTransform rt = CreateRect("Title", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 40f));
            TMP_Text tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28f;
            tmp.color = Color.white;
        }

        private static void CreateGrid(RectTransform parent, string name, Vector2 pos, Vector2 size, int cols, int rows)
        {
            RectTransform container = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image bg = container.gameObject.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.15f, 0.18f, 0.92f);

            GridLayoutGroup grid = container.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.spacing = new Vector2(5f, 5f);

            float cellW = (size.x - (cols - 1) * 5f - 16f) / cols;
            float cellH = (size.y - (rows - 1) * 5f - 16f) / rows;
            grid.cellSize = new Vector2(cellW, cellH);
            grid.padding = new RectOffset(8, 8, 8, 8);
        }

        private static void CreateButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.18f, 0.42f, 0.8f, 1f);
            rt.gameObject.AddComponent<Button>();

            RectTransform lblRt = CreateRect("Label", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            TMP_Text tmp = lblRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24f;
            tmp.color = Color.white;
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
