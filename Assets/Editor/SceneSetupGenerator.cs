using ProphecyCentury.Core;
using ProphecyCentury.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProphecyCentury.Editor
{
    public static class SceneSetupGenerator
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string RunScenePath = "Assets/Scenes/RunScene.unity";

        [MenuItem("Prophecy Century/Generate Migration Scenes")]
        public static void GenerateAll()
        {
            GenerateBootstrapScene();
            GenerateRunScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Prophecy Century migration scenes generated.");
        }

        private static void GenerateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<BootstrapInstaller>();
            bootstrap.AddComponent<BootstrapSceneController>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void GenerateRunScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(18, 23, 31, 255);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<BootstrapInstaller>();

            CreateEventSystem();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1800, 900);
            scaler.matchWidthOrHeight = 0.5f;

            var controllerObject = new GameObject("RunSceneController");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            var controller = controllerObject.AddComponent<RunSceneController>();

            var topBar = CreatePanel("TopBar", canvasObject.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -80f));
            var gold = CreateText("GoldLabel", topBar.transform, "Gold: 0", 24, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(12f, 0f), new Vector2(0f, 0f));
            var round = CreateText("RoundLabel", topBar.transform, "Round: 1", 24, TextAnchor.MiddleLeft, new Vector2(0.25f, 0f), new Vector2(0.5f, 1f), new Vector2(12f, 0f), new Vector2(0f, 0f));
            var hp = CreateText("HpLabel", topBar.transform, "HP: 100", 24, TextAnchor.MiddleLeft, new Vector2(0.5f, 0f), new Vector2(0.75f, 1f), new Vector2(12f, 0f), new Vector2(0f, 0f));
            var state = CreateText("StateLabel", topBar.transform, "State: manage", 24, TextAnchor.MiddleLeft, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));

            var leftPanel = CreatePanel("ShopPanel", canvasObject.transform, new Color32(30, 43, 57, 255), new Vector2(0f, 0.2f), new Vector2(0.3f, 0.9f), new Vector2(0f, 0f), new Vector2(-12f, 0f));
            var centerPanel = CreatePanel("MidPanel", canvasObject.transform, new Color32(35, 50, 63, 255), new Vector2(0.3f, 0.2f), new Vector2(0.68f, 0.9f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var rightPanel = CreatePanel("InfoPanel", canvasObject.transform, new Color32(25, 34, 45, 255), new Vector2(0.68f, 0.2f), new Vector2(1f, 0.9f), new Vector2(12f, 0f), new Vector2(0f, 0f));
            var bottomBar = CreatePanel("BottomBar", canvasObject.transform, new Color32(28, 37, 49, 255), new Vector2(0f, 0f), new Vector2(1f, 0.18f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var shopMetaText = CreateText("ShopMetaText", leftPanel.transform, "Shop L1", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.85f), new Vector2(1f, 1f), new Vector2(18f, -12f), new Vector2(-18f, 8f));
            var shopText = CreateText("ShopText", leftPanel.transform, "Shop", 22, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.85f), new Vector2(18f, 18f), new Vector2(-18f, -8f));
            var handText = CreateText("HandText", centerPanel.transform, "Hand", 22, TextAnchor.UpperLeft, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 18f));
            var boardText = CreateText("BoardText", centerPanel.transform, "Board", 22, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(18f, 18f), new Vector2(-18f, -18f));
            var campaignText = CreateText("CampaignText", rightPanel.transform, "Campaign", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.8f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, 18f));
            var heroText = CreateText("HeroText", rightPanel.transform, "Hero", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.65f), new Vector2(1f, 0.8f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var battlePreviewText = CreateText("BattlePreviewText", rightPanel.transform, "Battle Preview", 20, TextAnchor.UpperLeft, new Vector2(0f, 0.3f), new Vector2(1f, 0.65f), new Vector2(18f, -8f), new Vector2(-18f, 8f));
            var logText = CreateText("LogText", rightPanel.transform, "Log", 20, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(18f, 18f), new Vector2(-18f, -18f));

            var buttonWidth = 220f;
            var gap = 12f;
            var firstButtonX = 120f;
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

            EditorSceneManager.SaveScene(scene, RunScenePath);
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
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
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
