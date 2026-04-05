using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MuLike.Input;
using MuLike.UI;

namespace MuLike.Tools.Editor
{
    /// <summary>
    /// Editor tool that generates a MobileUI.prefab with the full touch HUD layout:
    ///   - Floating joystick (left)
    ///   - Radial skill ring: central attack button + 6 skill slots around it (right)
    ///   - HP / MP potion quick buttons
    ///   - Mount / Muun button (double-tap handled by MuTouchControls)
    ///   - MuTouchControls component wired to Joystick + right cluster RectTransform
    ///
    /// Menu: MuLike / Build / Create MobileUI Prefab
    /// Output: Assets/_Project/Prefabs/UI/MobileUI.prefab
    /// </summary>
    public static class MobileUiPrefabBuilder
    {
        private const string PrefabOutputPath = "Assets/_Project/Prefabs/UI/MobileUI.prefab";

        [MenuItem("MuLike/Build/Create MobileUI Prefab")]
        public static void Build()
        {
            EnsureDirectory("Assets/_Project/Prefabs/UI");

            // ── Root canvas ────────────────────────────────────────────────────────
            GameObject root = new("MobileUI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // ── EventSystem (added to scene, not to prefab root) ───────────────────
            // Note: EventSystem must be in the scene hierarchy separately.

            // ── HUD scale root ─────────────────────────────────────────────────────
            RectTransform hudRoot = CreateStretchRect("HudRoot", root.transform);

            // ── Joystick (left side, bottom) ───────────────────────────────────────
            RectTransform joystickGroup = CreateAnchoredRect(
                "JoystickGroup", hudRoot,
                anchor: new Vector2(0f, 0f), pivot: new Vector2(0f, 0f),
                anchoredPos: new Vector2(180f, 180f), size: new Vector2(220f, 220f));

            // Joystick background
            RectTransform joystickBg = CreateAnchoredRect(
                "JoystickBackground", joystickGroup.transform,
                anchor: Vector2.one * 0.5f, pivot: Vector2.one * 0.5f,
                anchoredPos: Vector2.zero, size: new Vector2(180f, 180f));

            AddImage(joystickBg.gameObject, new Color(1f, 1f, 1f, 0.12f));
            CanvasGroup joystickCg = joystickBg.gameObject.AddComponent<CanvasGroup>();

            // Joystick knob
            RectTransform joystickKnob = CreateAnchoredRect(
                "JoystickKnob", joystickBg.transform,
                anchor: Vector2.one * 0.5f, pivot: Vector2.one * 0.5f,
                anchoredPos: Vector2.zero, size: new Vector2(80f, 80f));

            AddImage(joystickKnob.gameObject, new Color(1f, 1f, 1f, 0.35f));

            // Joystick component
            Joystick joystick = joystickGroup.gameObject.AddComponent<Joystick>();
            SerializedObject joystickSo = new(joystick);
            joystickSo.FindProperty("_background").objectReferenceValue   = joystickBg;
            joystickSo.FindProperty("_knob").objectReferenceValue         = joystickKnob;
            joystickSo.FindProperty("_canvasGroup").objectReferenceValue  = joystickCg;
            joystickSo.FindProperty("_floating").boolValue                = true;
            joystickSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Right button cluster ───────────────────────────────────────────────
            RectTransform rightCluster = CreateAnchoredRect(
                "RightButtonCluster", hudRoot,
                anchor: new Vector2(1f, 0f), pivot: new Vector2(1f, 0f),
                anchoredPos: new Vector2(-40f, 40f), size: new Vector2(340f, 340f));

            // Attack button (center of radial ring)
            RectTransform attackBtn = CreateAnchoredRect(
                "AttackButton", rightCluster.transform,
                anchor: new Vector2(0.5f, 0.5f), pivot: Vector2.one * 0.5f,
                anchoredPos: Vector2.zero, size: new Vector2(110f, 110f));

            AddImage(attackBtn.gameObject, new Color(0.85f, 0.15f, 0.15f, 0.90f));
            attackBtn.gameObject.AddComponent<Button>();
            AddLabel(attackBtn.gameObject, "ATK", 18f);

            // 6 radial skill slots (45° steps around attack button)
            float ringRadius = 130f;
            string[] icons = { "S1", "S2", "S3", "S4", "S5", "S6" };
            float[] angles  = { 0f, 60f, 120f, 180f, 240f, 300f };

            for (int i = 0; i < 6; i++)
            {
                float rad  = angles[i] * Mathf.Deg2Rad;
                float x    = Mathf.Cos(rad) * ringRadius;
                float y    = Mathf.Sin(rad) * ringRadius;

                RectTransform skillSlot = CreateAnchoredRect(
                    $"SkillSlot_{i + 1}", rightCluster.transform,
                    anchor: new Vector2(0.5f, 0.5f), pivot: Vector2.one * 0.5f,
                    anchoredPos: new Vector2(x, y), size: new Vector2(75f, 75f));

                AddImage(skillSlot.gameObject, new Color(0.18f, 0.22f, 0.45f, 0.88f));
                skillSlot.gameObject.AddComponent<Button>();
                AddLabel(skillSlot.gameObject, icons[i], 14f);

                // Cooldown fill overlay
                RectTransform fill = CreateAnchoredRect(
                    "CooldownFill", skillSlot.transform,
                    anchor: Vector2.one * 0.5f, pivot: Vector2.one * 0.5f,
                    anchoredPos: Vector2.zero, size: new Vector2(75f, 75f));

                Image fillImg = AddImage(fill.gameObject, new Color(0f, 0f, 0f, 0.5f));
                fillImg.type      = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Radial360;
                fillImg.fillAmount = 0f;
            }

            // Potion strip: HP and MP buttons (top-left of right cluster)
            RectTransform potionStrip = CreateAnchoredRect(
                "PotionStrip", rightCluster.transform,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(-120f, 0f), size: new Vector2(90f, 180f));

            BuildPotionButton("BtnHpPotion", potionStrip.transform,
                anchoredPos: new Vector2(45f, -40f),
                color: new Color(0.8f, 0.15f, 0.15f, 0.9f), label: "HP");

            BuildPotionButton("BtnMpPotion", potionStrip.transform,
                anchoredPos: new Vector2(45f, -130f),
                color: new Color(0.15f, 0.30f, 0.85f, 0.9f), label: "MP");

            // Mount / Muun button (top-right of right cluster)
            RectTransform mountBtn = CreateAnchoredRect(
                "MountMuunButton", rightCluster.transform,
                anchor: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                anchoredPos: new Vector2(30f, 30f), size: new Vector2(70f, 70f));

            AddImage(mountBtn.gameObject, new Color(0.6f, 0.4f, 0.1f, 0.88f));
            mountBtn.gameObject.AddComponent<Button>();
            AddLabel(mountBtn.gameObject, "◆", 16f);

            // ── Auto-hide non-critical panels group ────────────────────────────────
            RectTransform nonCritical = CreateAnchoredRect(
                "NonCriticalPanels", hudRoot,
                anchor: Vector2.one, pivot: Vector2.one,
                anchoredPos: Vector2.zero, size: new Vector2(200f, 400f));

            CanvasGroup nonCriticalCg = nonCritical.gameObject.AddComponent<CanvasGroup>();

            // ── MuTouchControls ────────────────────────────────────────────────────
            MuTouchControls touchControls = root.AddComponent<MuTouchControls>();
            SerializedObject tcSo = new(touchControls);
            tcSo.FindProperty("_joystick").objectReferenceValue           = joystick;
            tcSo.FindProperty("_rightButtonCluster").objectReferenceValue = rightCluster;
            tcSo.FindProperty("_hudScaleRoot").objectReferenceValue       = hudRoot;

            // Wire auto-hide panel
            SerializedProperty hidePanels = tcSo.FindProperty("_autoHidePanels");
            hidePanels.arraySize = 1;
            hidePanels.GetArrayElementAtIndex(0).objectReferenceValue = nonCriticalCg;

            tcSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Save as prefab ─────────────────────────────────────────────────────
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabOutputPath, out success);
            Object.DestroyImmediate(root);

            if (success)
                Debug.Log($"[MobileUiPrefabBuilder] Created {PrefabOutputPath}");
            else
                Debug.LogError($"[MobileUiPrefabBuilder] Failed to save prefab at {PrefabOutputPath}");
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

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

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform CreateAnchoredRect(
            string name, Transform parent,
            Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin       = anchor;
            rt.anchorMax       = anchor;
            rt.pivot           = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta       = size;
            return rt;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img   = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void AddLabel(GameObject parent, string text, float fontSize)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.transform, worldPositionStays: false);
            var rt = labelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color     = Color.white;
        }

        private static void BuildPotionButton(string name, Transform parent, Vector2 anchoredPos, Color color, string label)
        {
            RectTransform btn = CreateAnchoredRect(
                name, parent,
                anchor: new Vector2(0.5f, 0.5f), pivot: Vector2.one * 0.5f,
                anchoredPos: anchoredPos, size: new Vector2(72f, 72f));

            AddImage(btn.gameObject, color);
            btn.gameObject.AddComponent<Button>();
            AddLabel(btn.gameObject, label, 13f);

            // Cooldown fill
            RectTransform fill = CreateAnchoredRect(
                "CooldownFill", btn.transform,
                anchor: Vector2.one * 0.5f, pivot: Vector2.one * 0.5f,
                anchoredPos: Vector2.zero, size: new Vector2(72f, 72f));

            Image fillImg  = AddImage(fill.gameObject, new Color(0f, 0f, 0f, 0.5f));
            fillImg.type   = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillAmount = 0f;
        }
    }
}
