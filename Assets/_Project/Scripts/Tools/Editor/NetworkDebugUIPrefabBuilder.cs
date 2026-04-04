#if UNITY_EDITOR
using MuLike.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Creates a ready-to-use debug UI prefab wired to NetworkDebugPanel.
    /// </summary>
    public static class NetworkDebugUIPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath = PrefabFolder + "/NetworkDebugPanel.prefab";

        [MenuItem("MuLike/Build/Create Network Debug UI Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolders();

            var root = new GameObject("NetworkDebugPanel", typeof(RectTransform));
            var panel = root.AddComponent<NetworkDebugPanel>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var box = CreatePanel(root.transform, "Panel", new Vector2(20f, -20f), new Vector2(360f, 300f));
            var statusText = CreateText(box.transform, "StatusText", "Connected: false | Auth: false", new Vector2(12f, -12f), 22f);
            var logText = CreateText(box.transform, "LogText", "Logs...", new Vector2(12f, -150f), 130f);

            var loginButton = CreateButton(box.transform, "LoginButton", "Login", new Vector2(12f, -50f));
            var moveButton = CreateButton(box.transform, "MoveButton", "Move", new Vector2(126f, -50f));
            var skillButton = CreateButton(box.transform, "SkillButton", "Skill", new Vector2(240f, -50f));

            SerializedObject panelSo = new SerializedObject(panel);
            panelSo.FindProperty("_loginButton").objectReferenceValue = loginButton;
            panelSo.FindProperty("_moveButton").objectReferenceValue = moveButton;
            panelSo.FindProperty("_skillButton").objectReferenceValue = skillButton;
            panelSo.FindProperty("_statusText").objectReferenceValue = statusText;
            panelSo.FindProperty("_logText").objectReferenceValue = logText;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NetworkDebugUIPrefabBuilder] Prefab created: {PrefabPath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
        }

        private static Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string initialText, Vector2 anchoredPosition, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-24f, height);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = 18f;
            text.color = new Color(0.92f, 0.97f, 1f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;

            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(102f, 42f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.17f, 0.34f, 0.62f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return go.GetComponent<Button>();
        }
    }
}
#endif
