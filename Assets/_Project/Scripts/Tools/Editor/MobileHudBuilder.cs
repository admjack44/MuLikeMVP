#if UNITY_EDITOR
using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.UI.MobileHUD;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MuLike.Tools.Editor
{
    public static class MobileHudBuilder
    {
        [MenuItem("MuLike/Build/Create Mobile HUD")]
        public static void CreateMobileHud()
        {
            EnsureEventSystem();

            GameObject canvasGo = new GameObject("MobileHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.65f;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            GameObject safeAreaGo = CreateRect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeAreaGo.AddComponent<HudSafeAreaFitter>();

            GameObject bottomLeft = CreateRect("BottomLeft", safeAreaGo.transform, new Vector2(0f, 0f), new Vector2(0.45f, 0.5f), new Vector2(24f, 24f), new Vector2(-12f, -24f));
            GameObject bottomRight = CreateRect("BottomRight", safeAreaGo.transform, new Vector2(0.55f, 0f), new Vector2(1f, 0.55f), new Vector2(12f, 24f), new Vector2(-24f, -24f));
            GameObject topLeft = CreateRect("TopLeft", safeAreaGo.transform, new Vector2(0f, 0.55f), new Vector2(0.5f, 1f), new Vector2(24f, 24f), new Vector2(-12f, -12f));
            GameObject topRight = CreateRect("TopRight", safeAreaGo.transform, new Vector2(0.5f, 0.55f), new Vector2(1f, 1f), new Vector2(12f, 24f), new Vector2(-24f, -12f));

            VirtualJoystickView joystick = BuildJoystick(bottomLeft.transform);
            SkillButtonStripView skillStrip = BuildSkillStrip(bottomRight.transform);
            PotionQuickSlotStripView potionQuickSlots = BuildPotionQuickSlots(bottomRight.transform);
            Toggle autoAttack = BuildAutoAttack(bottomRight.transform);
            TargetPortraitView portrait = BuildTargetPortrait(topLeft.transform);
            BuildMinimapPlaceholder(topRight.transform, out Button minimapButton);
            BuildTopButtons(topRight.transform, out Button chatButton, out Button inventoryButton, out Button characterButton, out Button mapButton);
            BuildResourceBars(topLeft.transform, out HudResourceBarView hp, out HudResourceBarView mp, out HudResourceBarView sd, out HudResourceBarView stamina);

            MobileHudView view = safeAreaGo.AddComponent<MobileHudView>();
            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("_leftJoystick").objectReferenceValue = joystick;
            viewSo.FindProperty("_skillStrip").objectReferenceValue = skillStrip;
            viewSo.FindProperty("_potionQuickSlots").objectReferenceValue = potionQuickSlots;
            viewSo.FindProperty("_autoAttackToggle").objectReferenceValue = autoAttack;
            viewSo.FindProperty("_targetPortrait").objectReferenceValue = portrait;
            viewSo.FindProperty("_hpBar").objectReferenceValue = hp;
            viewSo.FindProperty("_mpBar").objectReferenceValue = mp;
            viewSo.FindProperty("_sdBar").objectReferenceValue = sd;
            viewSo.FindProperty("_staminaBar").objectReferenceValue = stamina;
            viewSo.FindProperty("_chatButton").objectReferenceValue = chatButton;
            viewSo.FindProperty("_inventoryButton").objectReferenceValue = inventoryButton;
            viewSo.FindProperty("_minimapButton").objectReferenceValue = minimapButton;
            viewSo.FindProperty("_characterButton").objectReferenceValue = characterButton;
            viewSo.FindProperty("_mapButton").objectReferenceValue = mapButton;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            MobileHudController controller = safeAreaGo.AddComponent<MobileHudController>();
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_view").objectReferenceValue = view;
            controllerSo.FindProperty("_characterMotor").objectReferenceValue = Object.FindObjectOfType<CharacterMotor>();
            controllerSo.FindProperty("_combatController").objectReferenceValue = Object.FindObjectOfType<CombatController>();
            controllerSo.FindProperty("_targetingController").objectReferenceValue = Object.FindObjectOfType<TargetingController>();
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeObject = canvasGo;
            EditorUtility.SetDirty(canvasGo);
            if (SceneManager.GetActiveScene().isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("[MobileHudBuilder] Mobile HUD created and wired.");
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            GameObject eventSystemGo = existing != null ? existing.gameObject : new GameObject("EventSystem", typeof(EventSystem));

#if ENABLE_INPUT_SYSTEM
            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemGo.AddComponent<InputSystemUIInputModule>();

            StandaloneInputModule legacy = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);
#else
            if (eventSystemGo.GetComponent<StandaloneInputModule>() == null)
                eventSystemGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private static VirtualJoystickView BuildJoystick(Transform parent)
        {
            GameObject root = CreateRect("Joystick", parent, new Vector2(0f, 0f), new Vector2(0.6f, 0.6f), new Vector2(24f, 24f), new Vector2(-24f, -24f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.33f);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(220f, 220f);
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);

            GameObject knob = CreateRect("Knob", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-45f, -45f), new Vector2(45f, 45f));
            Image knobImage = knob.AddComponent<Image>();
            knobImage.color = new Color(1f, 1f, 1f, 0.75f);

            VirtualJoystickView view = root.AddComponent<VirtualJoystickView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = rootRect;
            so.FindProperty("_knob").objectReferenceValue = knob.GetComponent<RectTransform>();
            so.FindProperty("_background").objectReferenceValue = bg;
            so.FindProperty("_radius").floatValue = 75f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static SkillButtonStripView BuildSkillStrip(Transform parent)
        {
            GameObject strip = CreateRect("SkillStrip", parent, new Vector2(1f, 0f), new Vector2(1f, 0.78f), new Vector2(-300f, 24f), new Vector2(-12f, -12f));
            VerticalLayoutGroup layout = strip.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlHeight = false;
            layout.childControlWidth = false;

            ContentSizeFitter fitter = strip.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            SkillButtonStripView view = strip.AddComponent<SkillButtonStripView>();
            SerializedObject viewSo = new SerializedObject(view);
            SerializedProperty bindingsProp = viewSo.FindProperty("_buttons");
            bindingsProp.arraySize = 4;

            for (int i = 0; i < 4; i++)
            {
                int skillId = i + 1;
                GameObject btnGo = CreateRect($"Skill_{skillId}", strip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-110f, 0f), new Vector2(0f, 110f));
                Image btnBg = btnGo.AddComponent<Image>();
                btnBg.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
                Button button = btnGo.AddComponent<Button>();

                GameObject labelGo = CreateRect("Label", btnGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
                TMP_Text label = AddText(labelGo, $"S{skillId}", 24);

                GameObject cdFillGo = CreateRect("CooldownFill", btnGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Image cdFill = cdFillGo.AddComponent<Image>();
                cdFill.color = new Color(0f, 0f, 0f, 0.55f);
                cdFill.type = Image.Type.Filled;
                cdFill.fillMethod = Image.FillMethod.Radial360;
                cdFill.fillOrigin = 2;
                cdFill.fillAmount = 0f;

                GameObject cdTextGo = CreateRect("CooldownText", btnGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                TMP_Text cdText = AddText(cdTextGo, string.Empty, 32);
                cdText.alignment = TextAlignmentOptions.Center;

                SerializedProperty item = bindingsProp.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("skillId").intValue = skillId;
                item.FindPropertyRelative("button").objectReferenceValue = button;
                item.FindPropertyRelative("nameText").objectReferenceValue = label;
                item.FindPropertyRelative("cooldownFill").objectReferenceValue = cdFill;
                item.FindPropertyRelative("cooldownText").objectReferenceValue = cdText;
            }

            viewSo.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Toggle BuildAutoAttack(Transform parent)
        {
            GameObject root = CreateRect("AutoAttack", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-280f, 24f), new Vector2(-40f, 90f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            Toggle toggle = root.AddComponent<Toggle>();

            GameObject check = CreateRect("Checkmark", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(50f, -10f));
            Image checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.22f, 0.95f, 0.35f, 0.92f);

            GameObject labelGo = CreateRect("Label", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(60f, 0f), new Vector2(-10f, 0f));
            TMP_Text label = AddText(labelGo, "AUTO", 26);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            toggle.graphic = checkImg;
            toggle.targetGraphic = bg;
            toggle.isOn = false;
            return toggle;
        }

        private static TargetPortraitView BuildTargetPortrait(Transform parent)
        {
            GameObject root = CreateRect("TargetPortrait", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -180f), new Vector2(340f, -24f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject portraitGo = CreateRect("Portrait", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 8f), new Vector2(120f, -8f));
            Image portrait = portraitGo.AddComponent<Image>();
            portrait.color = new Color(0.35f, 0.35f, 0.35f, 1f);

            GameObject labelGo = CreateRect("TargetName", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(130f, 0f), new Vector2(-10f, 0f));
            TMP_Text label = AddText(labelGo, "No target", 28);
            label.alignment = TextAlignmentOptions.MidlineLeft;

            TargetPortraitView view = root.AddComponent<TargetPortraitView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_portraitImage").objectReferenceValue = portrait;
            so.FindProperty("_targetNameText").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildResourceBars(Transform parent, out HudResourceBarView hp, out HudResourceBarView mp, out HudResourceBarView sd, out HudResourceBarView stamina)
        {
            GameObject panel = CreateRect("ResourceBars", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 220f));
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            hp = CreateResourceBar(panel.transform, "HP");
            mp = CreateResourceBar(panel.transform, "MP");
            sd = CreateResourceBar(panel.transform, "SD");
            stamina = CreateResourceBar(panel.transform, "STA");
        }

        private static HudResourceBarView CreateResourceBar(Transform parent, string label)
        {
            GameObject root = CreateRect($"{label}Bar", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 34f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            GameObject sliderGo = CreateRect("Slider", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(70f, 6f), new Vector2(-100f, -6f));
            Slider slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject fillArea = CreateRect("FillArea", sliderGo.transform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            GameObject fill = CreateRect("Fill", fillArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.85f, 0.12f, 0.12f, 0.95f);

            GameObject background = CreateRect("Background", sliderGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = bgImage;
            slider.direction = Slider.Direction.LeftToRight;

            GameObject labelGo = CreateRect("Label", root.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(66f, 0f));
            TMP_Text labelText = AddText(labelGo, label, 20);
            labelText.alignment = TextAlignmentOptions.Center;

            GameObject valueGo = CreateRect("Value", root.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-96f, 0f), new Vector2(-4f, 0f));
            TMP_Text valueText = AddText(valueGo, "0/0", 20);
            valueText.alignment = TextAlignmentOptions.Center;

            HudResourceBarView view = root.AddComponent<HudResourceBarView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_labelText").objectReferenceValue = labelText;
            so.FindProperty("_fillBar").objectReferenceValue = slider;
            so.FindProperty("_valueText").objectReferenceValue = valueText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildMinimapPlaceholder(Transform parent, out Button minimapButton)
        {
            GameObject root = CreateRect("MiniMapPlaceholder", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-230f, -230f), new Vector2(-24f, -24f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.09f, 0.12f, 0.92f);
            minimapButton = root.AddComponent<Button>();

            GameObject textGo = CreateRect("Label", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text text = AddText(textGo, "MINIMAP", 26);
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildTopButtons(Transform parent, out Button chatButton, out Button inventoryButton, out Button characterButton, out Button mapButton)
        {
            GameObject panel = CreateRect("TopButtons", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-520f, -88f), new Vector2(-250f, -24f));
            HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            chatButton = CreateSimpleButton(panel.transform, "ChatButton", "CHAT");
            inventoryButton = CreateSimpleButton(panel.transform, "InventoryButton", "INVENTORY");
            characterButton = CreateSimpleButton(panel.transform, "CharacterButton", "CHAR");
            mapButton = CreateSimpleButton(panel.transform, "MapButton", "MAP");
        }

        private static PotionQuickSlotStripView BuildPotionQuickSlots(Transform parent)
        {
            GameObject strip = CreateRect("PotionQuickSlots", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-480f, 24f), new Vector2(-300f, 220f));
            VerticalLayoutGroup layout = strip.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.LowerRight;

            PotionQuickSlotStripView view = strip.AddComponent<PotionQuickSlotStripView>();
            SerializedObject so = new SerializedObject(view);
            SerializedProperty slots = so.FindProperty("_slots");
            slots.arraySize = 2;

            BuildPotionSlotUi(strip.transform, slots.GetArrayElementAtIndex(0), 1, "HP");
            BuildPotionSlotUi(strip.transform, slots.GetArrayElementAtIndex(1), 2, "MP");

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildPotionSlotUi(Transform parent, SerializedProperty slot, int slotId, string label)
        {
            GameObject root = CreateRect($"Potion_{label}", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 0f), new Vector2(0f, 70f));
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.88f);
            Button button = root.AddComponent<Button>();

            GameObject labelGo = CreateRect("Label", root.transform, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(8f, 4f), new Vector2(-4f, -4f));
            TMP_Text labelText = AddText(labelGo, label, 22f);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject countGo = CreateRect("Count", root.transform, new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(4f, 4f), new Vector2(-8f, -4f));
            TMP_Text countText = AddText(countGo, "0", 22f);
            countText.alignment = TextAlignmentOptions.MidlineRight;

            GameObject cooldownGo = CreateRect("CooldownFill", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image cooldownFill = cooldownGo.AddComponent<Image>();
            cooldownFill.color = new Color(0f, 0f, 0f, 0.55f);
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Radial360;
            cooldownFill.fillAmount = 0f;

            GameObject cooldownTextGo = CreateRect("CooldownText", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text cooldownText = AddText(cooldownTextGo, string.Empty, 28f);
            cooldownText.alignment = TextAlignmentOptions.Center;

            slot.FindPropertyRelative("slotId").intValue = slotId;
            slot.FindPropertyRelative("button").objectReferenceValue = button;
            slot.FindPropertyRelative("labelText").objectReferenceValue = labelText;
            slot.FindPropertyRelative("countText").objectReferenceValue = countText;
            slot.FindPropertyRelative("cooldownFill").objectReferenceValue = cooldownFill;
            slot.FindPropertyRelative("cooldownText").objectReferenceValue = cooldownText;
        }

        private static Button CreateSimpleButton(Transform parent, string name, string label)
        {
            GameObject go = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement el = go.AddComponent<LayoutElement>();
            el.preferredWidth = 130f;
            el.preferredHeight = 64f;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            Button button = go.AddComponent<Button>();

            GameObject textGo = CreateRect("Text", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text txt = AddText(textGo, label, 20);
            txt.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return go;
        }

        private static TMP_Text AddText(GameObject go, string content, float fontSize)
        {
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }
    }
}
#endif
