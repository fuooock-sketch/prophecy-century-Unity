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
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1800f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            var controllerObject = new GameObject("RunSceneController");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            var controller = controllerObject.AddComponent<RunSceneController>();

            var topBar = CreatePanel("TopBar", canvasObject.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -80f), Vector2.zero);
            var gold = CreateText("GoldLabel", topBar.transform, "Gold: 0", 24, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var round = CreateText("RoundLabel", topBar.transform, "Round: 1", 24, TextAnchor.MiddleLeft, new Vector2(0.25f, 0f), new Vector2(0.5f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var hp = CreateText("HpLabel", topBar.transform, "HP: 100", 24, TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var state = CreateText("StateLabel", topBar.transform, "State: manage", 24, TextAnchor.MiddleLeft, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

            var leftPanel = CreatePanel("ShopPanel", canvasObject.transform, new Color32(30, 43, 57, 255), new Vector2(0f, 0.2f), new Vector2(0.3f, 0.9f), Vector2.zero, new Vector2(-12f, 0f));
            var centerPanel = CreatePanel("CenterPanel", canvasObject.transform, new Color32(35, 50, 63, 255), new Vector2(0.3f, 0.2f), new Vector2(0.68f, 0.9f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var rightPanel = CreatePanel("InfoPanel", canvasObject.transform, new Color32(25, 34, 45, 255), new Vector2(0.68f, 0.2f), new Vector2(1f, 0.9f), new Vector2(12f, 0f), Vector2.zero);
            var bottomBar = CreatePanel("BottomBar", canvasObject.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 0f), new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);

            var shopMetaText = CreateText("ShopMetaText", leftPanel.transform, "Shop L1", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.85f), Vector2.one, new Vector2(18f, -12f), new Vector2(-18f, 8f));
            var shopText = CreateText("ShopText", leftPanel.transform, "Shop", 22, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1f, 0.85f), new Vector2(18f, 18f), new Vector2(-18f, -8f));
            var handText = CreateText("HandText", centerPanel.transform, "Hand", 22, TextAnchor.UpperLeft, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 18f));
            var boardText = CreateText("BoardText", centerPanel.transform, "Board", 22, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(18f, 18f), new Vector2(-18f, -18f));
            var campaignText = CreateText("CampaignText", rightPanel.transform, "Campaign", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.8f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 18f));
            var heroText = CreateText("HeroText", rightPanel.transform, "Hero", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.65f), new Vector2(1f, 0.8f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var battlePreviewText = CreateText("BattlePreviewText", rightPanel.transform, "Battle Preview", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.3f), new Vector2(1f, 0.65f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var logText = CreateText("LogText", rightPanel.transform, "Log", 20, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(18f, 18f), new Vector2(-18f, -18f));

            const float buttonWidth = 220f;
            const float gap = 12f;
            const float firstButtonX = 120f;
            CreateButton("RefreshShopButton", bottomBar.transform, "Refresh Shop", new Vector2(firstButtonX, 50f), new Vector2(buttonWidth, 56f), controller.RefreshShop);
            CreateButton("BuyButton", bottomBar.transform, "Buy First", new Vector2(firstButtonX + (buttonWidth + gap) * 1f, 50f), new Vector2(buttonWidth, 56f), controller.BuyFirstCard);
            CreateButton("DeployButton", bottomBar.transform, "Deploy First", new Vector2(firstButtonX + (buttonWidth + gap) * 2f, 50f), new Vector2(buttonWidth, 56f), controller.DeployFirstCard);
            CreateButton("BattleButton", bottomBar.transform, "Resolve Battle", new Vector2(firstButtonX + (buttonWidth + gap) * 3f, 50f), new Vector2(buttonWidth, 56f), controller.StartBattle);
            CreateButton("NewRunButton", bottomBar.transform, "New Run", new Vector2(firstButtonX + (buttonWidth + gap) * 4f, 50f), new Vector2(buttonWidth, 56f), controller.StartNewRun);
            CreateButton("UpgradeShopButton", bottomBar.transform, "Upgrade Shop", new Vector2(firstButtonX + (buttonWidth + gap) * 5f, 50f), new Vector2(buttonWidth, 56f), controller.UpgradeShop);
            CreateButton("LockShopButton", bottomBar.transform, "Lock Shop", new Vector2(firstButtonX + (buttonWidth + gap) * 6f, 50f), new Vector2(buttonWidth, 56f), controller.ToggleShopLock);

            AssignField(controller, "goldLabel", gold);
            AssignField(controller, "roundLabel", round);
            AssignField(controller, "hpLabel", hp);
            AssignField(controller, "stateLabel", state);
            AssignField(controller, "campaignLabel", campaignText);
            AssignField(controller, "heroLabel", heroText);
            AssignField(controller, "logLabel", logText);
            AssignField(controller, "shopMetaLabel", shopMetaText);
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
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        private static void CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction callback)
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

            var labelText = CreateText("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelText.color = Color.white;
        }

        private static void AssignField(Object target, string fieldName, Object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
