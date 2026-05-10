using System.IO;
using ProphecyCentury.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public static class RuntimeUiBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunSceneUi()
        {
            if (Object.FindObjectOfType<RunSceneController>() != null)
            {
                return;
            }

            EnsureSession();
            EnsureEventSystem();
            BuildUi();
        }

        private static void EnsureSession()
        {
            if (ProphecyGameSession.Instance != null)
            {
                return;
            }

            var bootstrap = new GameObject("RuntimeBootstrap");
            bootstrap.AddComponent<BootstrapInstaller>();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static void BuildUi()
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 3f;
            canvasObject.AddComponent<AudioSource>();
            canvasObject.AddComponent<RuntimeBgmPlayer>();

            var controllerObject = new GameObject("RunSceneController");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            var controller = controllerObject.AddComponent<RunSceneController>();

            var titlePanel = CreatePanel("TitlePanel", canvasObject.transform, new Color32(16, 22, 30, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplySpriteFromProjectPath(titlePanel.GetComponent<Image>(), "Art/bg/loading_image.png");
            CreateText("TitleText", titlePanel.transform, "预言世纪", 48, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.88f), Vector2.zero, Vector2.zero);
            CreateText("CampaignSelectLabel", titlePanel.transform, "战役", 22, TextAnchor.MiddleLeft, new Vector2(0.32f, 0.57f), new Vector2(0.46f, 0.63f), Vector2.zero, Vector2.zero);
            var campaignDropdown = CreateDropdown("CampaignDropdown", titlePanel.transform, new Vector2(0.58f, 0.6f), new Vector2(480f, 54f));
            CreateText("HeroSelectLabel", titlePanel.transform, "英雄", 22, TextAnchor.MiddleLeft, new Vector2(0.32f, 0.46f), new Vector2(0.46f, 0.52f), Vector2.zero, Vector2.zero);
            var heroDropdown = CreateDropdown("HeroDropdown", titlePanel.transform, new Vector2(0.58f, 0.49f), new Vector2(480f, 54f));
            var campaignDescription = CreateText("CampaignDescription", titlePanel.transform, string.Empty, 21, TextAnchor.UpperLeft, new Vector2(0.22f, 0.30f), new Vector2(0.48f, 0.42f), Vector2.zero, Vector2.zero);
            var heroDescription = CreateText("HeroDescription", titlePanel.transform, string.Empty, 20, TextAnchor.UpperLeft, new Vector2(0.52f, 0.26f), new Vector2(0.78f, 0.42f), Vector2.zero, Vector2.zero);
            CreateButton("StartSelectedRunButton", titlePanel.transform, "开始游戏", new Vector2(900f, 300f), new Vector2(260f, 64f), controller.StartSelectedRun);

            var runPanel = CreatePanel("RunPanel", canvasObject.transform, new Color32(20, 27, 35, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var topBar = CreatePanel("TopBar", runPanel.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -80f), Vector2.zero);
            var gold = CreateIconText("GoldLabel", topBar.transform, "\u91d1\u5e01", "金币：0", 24, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var round = CreateIconText("RoundLabel", topBar.transform, "\u65f6\u95f4", "回合：1", 24, new Vector2(0.25f, 0f), new Vector2(0.5f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var hp = CreateIconText("HpLabel", topBar.transform, "\u8840\u74f6", "生命：100", 24, new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var state = CreateIconText("StateLabel", topBar.transform, "\u9f7f\u8f6e", "阶段：经营", 24, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

            var leftPanel = CreatePanel("ShopPanel", runPanel.transform, new Color32(30, 43, 57, 255), new Vector2(0f, 0.2f), new Vector2(0.3f, 0.9f), Vector2.zero, new Vector2(-12f, 0f));
            var centerPanel = CreatePanel("CenterPanel", runPanel.transform, new Color32(35, 50, 63, 255), new Vector2(0.3f, 0.2f), new Vector2(0.68f, 0.9f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var rightPanel = CreatePanel("InfoPanel", runPanel.transform, new Color32(25, 34, 45, 255), new Vector2(0.68f, 0.2f), new Vector2(1f, 0.9f), new Vector2(12f, 0f), Vector2.zero);
            var bottomBar = CreatePanel("BottomBar", runPanel.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 0f), new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);

            var shopMetaText = CreateText("ShopMetaText", leftPanel.transform, "商店 L1", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.85f), Vector2.one, new Vector2(18f, -12f), new Vector2(-18f, 8f));
            var shopText = CreateText("ShopText", leftPanel.transform, "商店", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.74f), new Vector2(1f, 0.85f), new Vector2(18f, 0f), new Vector2(-18f, -8f));
            var shopCardRoot = CreateCardListRoot("ShopCardRoot", leftPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.74f), new Vector2(18f, 18f), new Vector2(-18f, -8f));
            var handText = CreateText("HandText", centerPanel.transform, "手牌", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.88f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 8f));
            var handCardRoot = CreateCardListRoot("HandCardRoot", centerPanel.transform, new Vector2(0f, 0.53f), new Vector2(1f, 0.88f), new Vector2(18f, 0f), new Vector2(-18f, -8f));
            var boardText = CreateText("BoardText", centerPanel.transform, "棋盘", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.43f), new Vector2(1f, 0.53f), new Vector2(18f, 0f), new Vector2(-18f, -8f));
            var boardCardRoot = CreateCardListRoot("BoardCardRoot", centerPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.43f), new Vector2(18f, 18f), new Vector2(-18f, -8f));
            var campaignText = CreateText("CampaignText", rightPanel.transform, "战役", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.8f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 18f));
            var heroText = CreateText("HeroText", rightPanel.transform, "英雄", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.65f), new Vector2(1f, 0.8f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var battlePreviewText = CreateText("BattlePreviewText", rightPanel.transform, "战斗预览", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.3f), new Vector2(1f, 0.65f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var logText = CreateText("LogText", rightPanel.transform, "日志", 20, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(18f, 18f), new Vector2(-18f, -18f));

            const float buttonWidth = 220f;
            const float gap = 12f;
            const float firstButtonX = 120f;
            CreateButton("RefreshShopButton", bottomBar.transform, "刷新商店", new Vector2(firstButtonX, 50f), new Vector2(buttonWidth, 56f), controller.RefreshShop, "\u5546\u5e97");
            CreateButton("BuyButton", bottomBar.transform, "快速购买", new Vector2(firstButtonX + (buttonWidth + gap) * 1f, 50f), new Vector2(buttonWidth, 56f), controller.BuyFirstCard, "\u91d1\u5e01");
            CreateButton("DeployButton", bottomBar.transform, "快速部署", new Vector2(firstButtonX + (buttonWidth + gap) * 2f, 50f), new Vector2(buttonWidth, 56f), controller.DeployFirstCard, "\u519b\u65d7");
            CreateButton("BattleButton", bottomBar.transform, "结算战斗", new Vector2(firstButtonX + (buttonWidth + gap) * 3f, 50f), new Vector2(buttonWidth, 56f), controller.StartBattle, "\u957f\u5251");
            CreateButton("NewRunButton", bottomBar.transform, "新开一局", new Vector2(firstButtonX + (buttonWidth + gap) * 4f, 50f), new Vector2(buttonWidth, 56f), controller.StartNewRun, "\u7687\u51a0");
            CreateButton("UpgradeShopButton", bottomBar.transform, "升级商店", new Vector2(firstButtonX + (buttonWidth + gap) * 5f, 50f), new Vector2(buttonWidth, 56f), controller.UpgradeShop, "\u94bb\u77f3");
            CreateButton("LockShopButton", bottomBar.transform, "锁定商店", new Vector2(firstButtonX + (buttonWidth + gap) * 6f, 50f), new Vector2(buttonWidth, 56f), controller.ToggleShopLock, "\u94c1\u9501");

            AssignField(controller, "goldLabel", gold);
            AssignField(controller, "roundLabel", round);
            AssignField(controller, "hpLabel", hp);
            AssignField(controller, "stateLabel", state);
            AssignField(controller, "titlePanel", titlePanel);
            AssignField(controller, "runPanel", runPanel);
            AssignField(controller, "campaignDropdown", campaignDropdown);
            AssignField(controller, "heroDropdown", heroDropdown);
            AssignField(controller, "campaignDescriptionLabel", campaignDescription);
            AssignField(controller, "heroDescriptionLabel", heroDescription);
            AssignField(controller, "campaignLabel", campaignText);
            AssignField(controller, "heroLabel", heroText);
            AssignField(controller, "logLabel", logText);
            AssignField(controller, "shopMetaLabel", shopMetaText);
            AssignField(controller, "shopCardRoot", shopCardRoot);
            AssignField(controller, "handCardRoot", handCardRoot);
            AssignField(controller, "boardCardRoot", boardCardRoot);
            AssignField(controller, "shopText", shopText);
            AssignField(controller, "handText", handText);
            AssignField(controller, "boardText", boardText);
            AssignField(controller, "battlePreviewText", battlePreviewText);
        }

        public static void ShowTitleScreen()
        {
            if (Application.CanStreamedLevelBeLoaded("Bootstrap"))
            {
                SceneManager.LoadScene("Bootstrap");
                return;
            }

            if (ProphecyGameSession.Instance != null)
            {
                ProphecyGameSession.Instance.StartNewRun();
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = new GameObject(name, typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.resizeTextForBestFit = false;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        private static Text CreateIconText(string name, Transform parent, string iconName, string value, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(container.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(24f, 0f);
            iconRect.sizeDelta = new Vector2(36f, 36f);
            RuntimeFeatureIconCache.ApplyTo(iconObject.GetComponent<Image>(), iconName);

            return CreateText("Text", container.transform, value, fontSize, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(54f, 0f), Vector2.zero);
        }

        private static Transform CreateCardListRoot(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = new GameObject(name, typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            return root.transform;
        }

        private static void CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction callback, string iconName = null)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(70, 108, 145, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);

            if (!string.IsNullOrWhiteSpace(iconName))
            {
                var iconObject = new GameObject("Icon", typeof(Image));
                iconObject.transform.SetParent(buttonObject.transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(28f, 0f);
                iconRect.sizeDelta = new Vector2(30f, 30f);
                RuntimeFeatureIconCache.ApplyTo(iconObject.GetComponent<Image>(), iconName);
            }

            var labelOffsetMin = string.IsNullOrWhiteSpace(iconName) ? Vector2.zero : new Vector2(44f, 0f);
            var labelText = CreateText("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, labelOffsetMin, Vector2.zero);
            labelText.color = Color.white;
        }

        private static Dropdown CreateDropdown(string name, Transform parent, Vector2 anchorPosition, Vector2 size)
        {
            var dropdownObject = new GameObject(name, typeof(Image), typeof(Dropdown));
            dropdownObject.transform.SetParent(parent, false);
            var rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2((anchorPosition.x - 0.5f) * 1800f, (anchorPosition.y - 0.5f) * 900f);

            var image = dropdownObject.GetComponent<Image>();
            image.color = new Color32(42, 58, 74, 255);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.captionText = CreateText("Caption", dropdownObject.transform, string.Empty, 20, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-44f, 0f));
            CreateDropdownTemplate(dropdownObject.transform, dropdown);

            return dropdown;
        }

        private static void CreateDropdownTemplate(Transform parent, Dropdown dropdown)
        {
            var template = CreatePanel("Template", parent, new Color32(30, 43, 57, 255), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -220f), new Vector2(0f, 0f));
            template.SetActive(false);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.pivot = new Vector2(0.5f, 1f);
            template.AddComponent<ScrollRect>();

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color32(30, 43, 57, 255);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var item = new GameObject("Item", typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(0f, 42f);

            var itemBackground = CreatePanel("Item Background", item.transform, new Color32(48, 68, 86, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var itemCheckmark = CreatePanel("Item Checkmark", item.transform, new Color32(96, 146, 190, 255), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 10f), new Vector2(18f, -10f));
            var itemLabel = CreateText("Item Label", item.transform, string.Empty, 18, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(34f, 0f), new Vector2(-12f, 0f));

            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBackground.GetComponent<Image>();
            toggle.graphic = itemCheckmark.GetComponent<Image>();
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;

            var scrollRect = template.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
        }

        private static void AssignField(Object target, string fieldName, Object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static void ApplySpriteFromProjectPath(Image image, string relativeAssetPath)
        {
            if (image == null)
            {
                return;
            }

            var fullPath = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(fullPath))
            {
                return;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                return;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 2;
            texture.wrapMode = TextureWrapMode.Clamp;
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }
    }
}
