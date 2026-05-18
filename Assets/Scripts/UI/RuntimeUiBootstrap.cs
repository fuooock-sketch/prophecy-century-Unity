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
        public const string RuntimeUiPrefabAssetPath = "Assets/Resources/Prefabs/RuntimeCanvas.prefab";
        private const string RuntimeUiPrefabResourcePath = "Prefabs/RuntimeCanvas";
        private const string BattleStagePanelPrefabResourcePath = "Prefabs/UI/BattleStagePanel";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunSceneUi()
        {
            EnsureSession();

            var existingController = Object.FindObjectOfType<RunSceneController>();
            if (existingController != null)
            {
                EnsureEventSystem();
                var canvas = existingController.GetComponentInParent<Canvas>();
                WirePrefabButtons(canvas != null ? canvas.gameObject : existingController.gameObject);
                return;
            }

            EnsureEventSystem();
            BuildUi();
        }

        public static void EnsureSession()
        {
            ProphecyGameSession.EnsureInstance();
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
            if (TryInstantiatePrefabUi())
            {
                return;
            }

            CreateGeneratedUi();
        }

        private static bool TryInstantiatePrefabUi()
        {
            var prefab = Resources.Load<GameObject>(RuntimeUiPrefabResourcePath);
            if (prefab == null)
            {
                return false;
            }

            var instance = Object.Instantiate(prefab);
#if UNITY_EDITOR
            if (instance.GetComponent<RuntimePlaytestTools>() == null)
            {
                instance.AddComponent<RuntimePlaytestTools>();
            }
#endif
            WirePrefabButtons(instance);
            return true;
        }

        public static void WirePrefabButtons(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var controller = root.GetComponentInChildren<RunSceneController>(true);
            var encyclopedia = root.GetComponentInChildren<RuntimeEncyclopediaPanel>(true);
            if (controller == null)
            {
                return;
            }

            TryInstallBattleStagePanelPrefab(root.transform, controller);
            EnsureTitleChaseTestButton(root.transform, controller);

            WireButton(root.transform, "StartSelectedRunButton", controller.StartSelectedRun);
            WireButton(root.transform, "ChaseTestButton", controller.StartSmallMerchantChaseTest);
            WireButton(root.transform, "RefreshShopButton", controller.RefreshShop);
            WireButton(root.transform, "UpgradeShopButton", controller.UpgradeShop);
            WireButton(root.transform, "LockShopButton", controller.ToggleShopLock);
            WireButton(root.transform, "BattleButton", controller.StartBattle);
            WireButton(root.transform, "SaveGameButton", controller.SaveGame);
            WireButton(root.transform, "LoadGameButton", controller.LoadGame);
            WireButton(root.transform, "RefreshShopButtonV2", controller.RefreshShop);
            WireButton(root.transform, "UpgradeShopButtonV2", controller.UpgradeShop);
            WireButton(root.transform, "LockShopButtonV2", controller.ToggleShopLock);
            WireButton(root.transform, "RealtimeBattleToggleButtonV2", controller.ToggleRealtimeBattlePreview);
            WireButton(root.transform, "BattleButtonV2", controller.StartBattle);
            WireButton(root.transform, "SaveGameButtonV2", controller.SaveGame);
            WireButton(root.transform, "LoadGameButtonV2", controller.LoadGame);
            if (encyclopedia != null)
            {
                WireButton(root.transform, "EncyclopediaButtonV2", encyclopedia.Open);
            }
        }

        private static void EnsureTitleChaseTestButton(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null || FindDeepChild(root, "ChaseTestButton") != null)
            {
                return;
            }

            var titlePanel = FindDeepChild(root, "TitlePanel");
            if (titlePanel == null)
            {
                return;
            }

            CreateButton("ChaseTestButton", titlePanel, "追击测试", new Vector2(900f, 220f), new Vector2(260f, 56f), controller.StartSmallMerchantChaseTest);
        }

        private static bool TryInstallBattleStagePanelPrefab(Transform root, RunSceneController controller)
        {
            var prefab = Resources.Load<GameObject>(BattleStagePanelPrefabResourcePath);
            if (prefab == null || root == null || controller == null)
            {
                return false;
            }

            var runPanel = FindDeepChild(root, "RunPanel");
            if (runPanel == null)
            {
                return false;
            }

            var oldPanel = FindDeepChild(runPanel, "BattleStagePanel");
            var oldView = oldPanel != null ? oldPanel.GetComponent<BattleStagePanelView>() : null;
            if (oldView != null)
            {
                oldView.Bind(controller);
                return true;
            }

            var instance = Object.Instantiate(prefab, runPanel, false);
            instance.name = "BattleStagePanel";
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            instance.SetActive(false);
            var view = instance.GetComponent<BattleStagePanelView>() ?? instance.AddComponent<BattleStagePanelView>();
            view.Bind(controller);

            if (oldPanel != null && oldPanel.gameObject != instance)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(oldPanel.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(oldPanel.gameObject);
                }
            }

            return true;
        }

        private static void WireButton(Transform root, string name, UnityEngine.Events.UnityAction callback)
        {
            var target = FindDeepChild(root, name);
            var button = target != null ? target.GetComponent<Button>() : null;
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(callback);
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var match = FindDeepChild(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public static GameObject CreateGeneratedUi(bool includeEditorPlaytestTools = true)
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1280f);
            scaler.matchWidthOrHeight = 0f;
            scaler.dynamicPixelsPerUnit = 8f;
            canvasObject.AddComponent<AudioSource>();
            canvasObject.AddComponent<RuntimeBgmPlayer>();
#if UNITY_EDITOR
            if (includeEditorPlaytestTools)
            {
                canvasObject.AddComponent<RuntimePlaytestTools>();
            }
#endif
            var sfxObject = new GameObject("RuntimeSfxPlayer", typeof(AudioSource), typeof(RuntimeSfxPlayer));
            sfxObject.transform.SetParent(canvasObject.transform, false);

            var controllerObject = new GameObject("RunSceneController");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            var controller = controllerObject.AddComponent<RunSceneController>();
            var encyclopedia = canvasObject.AddComponent<RuntimeEncyclopediaPanel>();

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
            CreateButton("ChaseTestButton", titlePanel.transform, "追击测试", new Vector2(900f, 220f), new Vector2(260f, 56f), controller.StartSmallMerchantChaseTest);

            var runPanel = CreatePanel("RunPanel", canvasObject.transform, new Color32(18, 24, 31, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var topBar = CreatePanel("TopBar", runPanel.transform, new Color32(25, 34, 44, 255), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero);
            var gold = CreateIconText("GoldLabel", topBar.transform, "\u91d1\u5e01", "金币：0", 20, new Vector2(0f, 0f), new Vector2(0.13f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var round = CreateIconText("RoundLabel", topBar.transform, "\u65f6\u95f4", "回合：1", 20, new Vector2(0.13f, 0f), new Vector2(0.26f, 1f), new Vector2(4f, 0f), Vector2.zero);
            var hp = CreateIconText("HpLabel", topBar.transform, "\u8840\u74f6", "生命：100", 20, new Vector2(0.26f, 0f), new Vector2(0.39f, 1f), new Vector2(4f, 0f), Vector2.zero);
            var state = CreateIconText("StateLabel", topBar.transform, "\u65f6\u95f4", "阶段：经营", 20, new Vector2(0.39f, 0f), new Vector2(0.52f, 1f), new Vector2(4f, 0f), Vector2.zero);

            CreateButton("RefreshShopButton", topBar.transform, "刷新", new Vector2(910f, 0f), new Vector2(110f, 42f), controller.RefreshShop, "\u5546\u5e97");
            CreateButton("UpgradeShopButton", topBar.transform, "升级", new Vector2(1030f, 0f), new Vector2(110f, 42f), controller.UpgradeShop, "\u94bb\u77f3");
            CreateButton("LockShopButton", topBar.transform, "锁定", new Vector2(1150f, 0f), new Vector2(110f, 42f), controller.ToggleShopLock, "\u94c1\u9501");
            CreateButton("BattleButton", topBar.transform, "战斗", new Vector2(1288f, 0f), new Vector2(138f, 48f), controller.StartBattle, "\u957f\u5251");
            CreateButton("SaveGameButton", topBar.transform, "保存", new Vector2(1430f, 0f), new Vector2(98f, 38f), controller.SaveGame, "\u7fbd\u6bdb");
            CreateButton("LoadGameButton", topBar.transform, "读取", new Vector2(1538f, 0f), new Vector2(98f, 38f), controller.LoadGame, "\u5377\u8f74");

            var shopPanel = CreatePanel("ShopPanel", runPanel.transform, new Color32(24, 30, 48, 245), new Vector2(0f, 0.675f), new Vector2(1f, 0.91f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
            var boardPanel = CreatePanel("BoardPanel", runPanel.transform, new Color32(16, 24, 38, 180), new Vector2(0.015f, 0.16f), new Vector2(0.62f, 0.65f), Vector2.zero, Vector2.zero);
            var handPanel = CreatePanel("HandPanel", runPanel.transform, new Color32(24, 30, 48, 245), new Vector2(0.63f, 0.34f), new Vector2(0.985f, 0.65f), Vector2.zero, Vector2.zero);
            var logPanel = CreatePanel("CombatLogPanel", runPanel.transform, new Color32(13, 18, 30, 235), new Vector2(0.63f, 0.045f), new Vector2(0.985f, 0.325f), Vector2.zero, Vector2.zero);

            var shopMetaText = CreateText("ShopMetaText", shopPanel.transform, "商店 L1", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.77f), Vector2.one, new Vector2(18f, -12f), new Vector2(-18f, 0f));
            var shopText = CreateText("ShopText", shopPanel.transform, "商店", 16, TextAnchor.UpperLeft, new Vector2(0f, 0.77f), new Vector2(1f, 0.92f), new Vector2(220f, -12f), new Vector2(-18f, 0f));
            var shopCardRoot = CreateHorizontalCardListRoot("ShopCardRoot", shopPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.76f), new Vector2(18f, 12f), new Vector2(-18f, -4f));

            var boardText = CreateText("BoardText", boardPanel.transform, "棋盘", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.88f), Vector2.one, new Vector2(14f, -10f), new Vector2(-14f, 0f));
            var boardCardRoot = CreateBoardGridRoot("BoardCardRoot", boardPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.86f), new Vector2(18f, 18f), new Vector2(-18f, -8f));

            var handText = CreateText("HandText", handPanel.transform, "手牌", 18, TextAnchor.UpperLeft, new Vector2(0f, 0.84f), Vector2.one, new Vector2(14f, -10f), new Vector2(-14f, 0f));
            var handCardRoot = CreateGridCardRoot("HandCardRoot", handPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(14f, 14f), new Vector2(-14f, -8f));

            var campaignText = CreateText("CampaignText", logPanel.transform, "战役", 15, TextAnchor.UpperLeft, new Vector2(0f, 0.72f), Vector2.one, new Vector2(12f, -8f), new Vector2(-12f, -2f));
            var heroText = CreateText("HeroText", logPanel.transform, "英雄", 15, TextAnchor.UpperLeft, new Vector2(0f, 0.58f), new Vector2(1f, 0.74f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var battlePreviewText = CreateText("BattlePreviewText", logPanel.transform, "战斗预览", 15, TextAnchor.UpperLeft, new Vector2(0f, 0.28f), new Vector2(1f, 0.58f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            var logText = CreateText("LogText", logPanel.transform, "日志", 15, TextAnchor.UpperLeft, Vector2.zero, new Vector2(1f, 0.28f), new Vector2(12f, 8f), new Vector2(-12f, -4f));

            topBar.SetActive(false);
            shopPanel.SetActive(false);
            boardPanel.SetActive(false);
            handPanel.SetActive(false);
            logPanel.SetActive(false);

            runPanel.GetComponent<Image>().color = new Color32(48, 47, 103, 255);
            var playerPanelV2 = CreatePanel("PlayerPanelV2", runPanel.transform, new Color32(16, 10, 39, 245), new Vector2(0.012f, 0.225f), new Vector2(0.172f, 0.978f), Vector2.zero, Vector2.zero);
            var boardPanelV2 = CreatePanel("BoardPanelV2", runPanel.transform, new Color32(16, 10, 39, 235), new Vector2(0.178f, 0.225f), new Vector2(0.678f, 0.978f), Vector2.zero, Vector2.zero);
            var shopPanelV2 = CreatePanel("ShopPanelV2", runPanel.transform, new Color32(16, 10, 39, 245), new Vector2(0.684f, 0.225f), new Vector2(0.988f, 0.978f), Vector2.zero, Vector2.zero);
            var handPanelV2 = CreatePanel("HandPanelV2", runPanel.transform, new Color32(16, 10, 39, 245), new Vector2(0.012f, 0.03f), new Vector2(0.838f, 0.214f), Vector2.zero, Vector2.zero);
            var battlePanelV2 = CreatePanel("BattleActionPanelV2", runPanel.transform, new Color32(16, 10, 39, 0), new Vector2(0.852f, 0.03f), new Vector2(0.988f, 0.214f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(playerPanelV2.GetComponent<RectTransform>(), 22f, 24f, 418f, 862f);
            SetPixelRectTopLeft(boardPanelV2.GetComponent<RectTransform>(), 454f, 24f, 1260f, 862f);
            SetPixelRectTopLeft(shopPanelV2.GetComponent<RectTransform>(), 1727f, 24f, 806f, 862f);
            SetPixelRectTopLeft(handPanelV2.GetComponent<RectTransform>(), 22f, 898f, 2131f, 357f);
            SetPixelRectTopLeft(battlePanelV2.GetComponent<RectTransform>(), 2198f, 947f, 335f, 261f);

            var heroPortrait = CreatePanel("HeroPortrait", playerPanelV2.transform, Color.white, new Vector2(0.11f, 0.51f), new Vector2(0.89f, 0.95f), Vector2.zero, Vector2.zero);
            ApplySpriteFromProjectPath(heroPortrait.GetComponent<Image>(), "Art/bg/loading_image.png");
            SetPixelRectTopLeft(heroPortrait.GetComponent<RectTransform>(), 46f, 51f, 306f, 397f);
            var hpBar = CreatePanel("HpBar", playerPanelV2.transform, new Color32(221, 221, 221, 255), new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.465f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(hpBar.GetComponent<RectTransform>(), 46f, 478f, 311f, 36f);
            var hpFill = CreatePanel("HpFill", hpBar.transform, new Color32(226, 28, 31, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hpFillImage = hpFill.GetComponent<Image>();
            hpFillImage.type = Image.Type.Filled;
            hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            hpFillImage.fillOrigin = 0;
            hpFillImage.fillAmount = 1f;
            var hpV2 = CreateText("HpLabelV2", hpBar.transform, "100/100", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var goldV2 = CreateText("GoldLabelV2", playerPanelV2.transform, "金币：0", 18, TextAnchor.MiddleLeft, new Vector2(0.12f, 0.37f), new Vector2(0.88f, 0.405f), Vector2.zero, Vector2.zero);
            var stateV2 = CreateText("StateLabelV2", playerPanelV2.transform, "阶段：经营", 18, TextAnchor.MiddleLeft, new Vector2(0.12f, 0.335f), new Vector2(0.88f, 0.37f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(goldV2.GetComponent<RectTransform>(), 46f, 520f, 311f, 24f);
            SetPixelRectTopLeft(stateV2.GetComponent<RectTransform>(), 46f, 548f, 311f, 24f);
            goldV2.gameObject.SetActive(false);
            stateV2.gameObject.SetActive(false);

            var resourcePanel = CreatePanel("ResourcePanel", playerPanelV2.transform, new Color32(34, 22, 78, 255), new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.29f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(resourcePanel.GetComponent<RectTransform>(), 34f, 585f, 347f, 243f);
            var resourceGrid = new GameObject("ResourceGrid", typeof(GridLayoutGroup));
            resourceGrid.transform.SetParent(resourcePanel.transform, false);
            var resourceGridRect = resourceGrid.GetComponent<RectTransform>();
            resourceGridRect.anchorMin = new Vector2(0.06f, 0.08f);
            resourceGridRect.anchorMax = new Vector2(0.94f, 0.92f);
            resourceGridRect.offsetMin = Vector2.zero;
            resourceGridRect.offsetMax = Vector2.zero;
            var resourceLayout = resourceGrid.GetComponent<GridLayoutGroup>();
            resourceLayout.cellSize = new Vector2(62f, 62f);
            resourceLayout.spacing = new Vector2(8f, 10f);
            resourceLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            resourceLayout.constraintCount = 3;
            resourceLayout.childAlignment = TextAnchor.MiddleCenter;
            for (var resourceIndex = 0; resourceIndex < 6; resourceIndex += 1)
            {
                var slot = CreatePanel("ResourceSlot", resourceGrid.transform, new Color32(28, 20, 58, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var icon = new GameObject("GemIcon", typeof(Image));
                icon.transform.SetParent(slot.transform, false);
                var iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(44f, 44f);
                RuntimeFeatureIconCache.ApplyTo(icon.GetComponent<Image>(), "\u5b9d\u77f3");
            }

            var roundV2 = CreateText("RoundLabelV2", boardPanelV2.transform, "💰 0   第 1 回合", 38, TextAnchor.MiddleRight, new Vector2(0.42f, 0.03f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero);
            roundV2.resizeTextForBestFit = true;
            roundV2.resizeTextMinSize = 26;
            roundV2.resizeTextMaxSize = 38;
            roundV2.horizontalOverflow = HorizontalWrapMode.Overflow;
            var boardCardRootV2 = CreateBoardGridRoot("BoardCardRootV2", boardPanelV2.transform, new Vector2(0.02f, 0.12f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(roundV2.GetComponent<RectTransform>(), 548f, 748f, 668f, 82f);
            SetPixelRectTopLeft(boardCardRootV2.GetComponent<RectTransform>(), 70f, 50f, 1128f, 772f);
            boardCardRootV2.GetComponent<HorizontalLayoutGroup>().enabled = false;
            var battlePreviewTextV2 = CreateText("BattlePreviewTextV2", boardPanelV2.transform, "战斗预览", 18, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var logTextV2 = CreateText("LogTextV2", boardPanelV2.transform, "日志", 17, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(battlePreviewTextV2.GetComponent<RectTransform>(), 42f, 735f, 575f, 105f);
            SetPixelRectTopLeft(logTextV2.GetComponent<RectTransform>(), 632f, 735f, 286f, 105f);
            battlePreviewTextV2.gameObject.SetActive(false);
            logTextV2.gameObject.SetActive(false);

            var shopMetaTextV2 = CreateText("ShopMetaTextV2", shopPanelV2.transform, "\u5546\u5e97\u7b49\u7ea7\uff1a\u2B50", 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.89f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);
            var shopCardRootV2 = CreateGridCardRoot("ShopCardRootV2", shopPanelV2.transform, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(shopMetaTextV2.GetComponent<RectTransform>(), 34f, 17f, 560f, 88f);
            SetPixelRectTopLeft(shopCardRootV2.GetComponent<RectTransform>(), 34f, 110f, 735f, 589f);
            var shopGrid = shopCardRootV2.GetComponent<GridLayoutGroup>();
            shopGrid.cellSize = new Vector2(221f, 286f);
            shopGrid.spacing = new Vector2(28f, 12f);
            shopGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            shopGrid.constraintCount = 3;
            CreateButton("UpgradeShopButtonV2", shopPanelV2.transform, "升级", new Vector2(88f, -282f), new Vector2(142f, 58f), controller.UpgradeShop, "\u94bb\u77f3");
            CreateButton("LockShopButtonV2", shopPanelV2.transform, "锁定", new Vector2(246f, -282f), new Vector2(142f, 58f), controller.ToggleShopLock, "\u94c1\u9501");
            CreateButton("RefreshShopButtonV2", shopPanelV2.transform, "刷新", new Vector2(404f, -282f), new Vector2(142f, 58f), controller.RefreshShop, "\u5546\u5e97");
            CreateButton("EncyclopediaButtonV2", shopPanelV2.transform, "图鉴", new Vector2(660f, -62f), new Vector2(120f, 88f), encyclopedia.Open);
            SetPixelRectTopLeft(shopPanelV2.transform.Find("UpgradeShopButtonV2")?.GetComponent<RectTransform>(), 33f, 721f, 221f, 104f);
            SetPixelRectTopLeft(shopPanelV2.transform.Find("LockShopButtonV2")?.GetComponent<RectTransform>(), 291f, 721f, 221f, 104f);
            SetPixelRectTopLeft(shopPanelV2.transform.Find("RefreshShopButtonV2")?.GetComponent<RectTransform>(), 549f, 721f, 221f, 104f);
            SetPixelRectTopLeft(shopPanelV2.transform.Find("EncyclopediaButtonV2")?.GetComponent<RectTransform>(), 648f, 17f, 121f, 88f);

            var handCardRootV2 = CreateGridCardRoot("HandCardRootV2", handPanelV2.transform, new Vector2(0.018f, 0.08f), new Vector2(0.982f, 0.92f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(handCardRootV2.GetComponent<RectTransform>(), 36f, 35f, 2059f, 286f);
            var handGrid = handCardRootV2.GetComponent<GridLayoutGroup>();
            handGrid.cellSize = new Vector2(221f, 286f);
            handGrid.spacing = new Vector2(10f, 0f);
            handGrid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            handGrid.constraintCount = 1;

            CreateButton("RealtimeBattleToggleButtonV2", battlePanelV2.transform, "实时", new Vector2(0f, -42f), new Vector2(98f, 36f), controller.ToggleRealtimeBattlePreview);
            CreateButton("BattleButtonV2", battlePanelV2.transform, "开战", new Vector2(106f, 0f), new Vector2(212f, 188f), controller.StartBattle);
            SetPixelRectTopLeft(battlePanelV2.GetComponent<RectTransform>(), 2198f, 947f, 335f, 261f);
            CreateButton("SaveGameButtonV2", playerPanelV2.transform, "保存", new Vector2(28f, -284f), new Vector2(92f, 34f), controller.SaveGame);
            CreateButton("LoadGameButtonV2", playerPanelV2.transform, "读取", new Vector2(130f, -284f), new Vector2(92f, 34f), controller.LoadGame);
            SetPixelRectTopLeft(battlePanelV2.transform.Find("RealtimeBattleToggleButtonV2")?.GetComponent<RectTransform>(), 0f, 0f, 98f, 36f);
            SetPixelRectTopLeft(battlePanelV2.transform.Find("BattleButtonV2")?.GetComponent<RectTransform>(), 106f, 0f, 229f, 261f);
            SetPixelRectTopLeft(playerPanelV2.transform.Find("SaveGameButtonV2")?.GetComponent<RectTransform>(), 34f, 833f, 150f, 28f);
            SetPixelRectTopLeft(playerPanelV2.transform.Find("LoadGameButtonV2")?.GetComponent<RectTransform>(), 204f, 833f, 150f, 28f);

            var campaignTextV2 = CreateText("CampaignTextV2", playerPanelV2.transform, "战役", 13, TextAnchor.UpperLeft, new Vector2(0.1f, 0.295f), new Vector2(0.9f, 0.325f), Vector2.zero, Vector2.zero);
            var heroTextV2 = CreateText("HeroTextV2", playerPanelV2.transform, "英雄", 13, TextAnchor.UpperLeft, new Vector2(0.1f, 0.305f), new Vector2(0.9f, 0.34f), Vector2.zero, Vector2.zero);
            campaignTextV2.gameObject.SetActive(false);
            heroTextV2.gameObject.SetActive(false);

            var battleStagePanel = CreatePanel("BattleStagePanel", runPanel.transform, new Color32(10, 8, 26, 248), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var battleStageTitle = CreateText("BattleStageTitle", battleStagePanel.transform, "战斗阶段", 56, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -114f), Vector2.zero);
            var battleStageStatus = CreateText("BattleStageStatus", battleStagePanel.transform, "准备战斗", 34, TextAnchor.MiddleCenter, new Vector2(0.24f, 0.78f), new Vector2(0.76f, 0.86f), Vector2.zero, Vector2.zero);
            var battleStageLog = CreateText("BattleStageLog", battleStagePanel.transform, string.Empty, 26, TextAnchor.MiddleCenter, new Vector2(0.22f, 0.16f), new Vector2(0.78f, 0.26f), Vector2.zero, Vector2.zero);
            var playerBattleRoot = CreateBattleUnitRoot("PlayerBattleRoot", battleStagePanel.transform, new Vector2(0.05f, 0.34f), new Vector2(0.47f, 0.72f));
            var enemyBattleRoot = CreateBattleUnitRoot("EnemyBattleRoot", battleStagePanel.transform, new Vector2(0.53f, 0.34f), new Vector2(0.95f, 0.72f));
            CreateText("PlayerBattleLabel", battleStagePanel.transform, "我方", 32, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.73f), new Vector2(0.47f, 0.78f), Vector2.zero, Vector2.zero);
            CreateText("EnemyBattleLabel", battleStagePanel.transform, "敌方", 32, TextAnchor.MiddleCenter, new Vector2(0.53f, 0.73f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero);
            var battleProgressBack = CreatePanel("BattleProgressBack", battleStagePanel.transform, new Color32(48, 48, 70, 255), new Vector2(0.24f, 0.28f), new Vector2(0.76f, 0.31f), Vector2.zero, Vector2.zero);
            var battleProgressFill = CreatePanel("BattleProgressFill", battleProgressBack.transform, new Color32(224, 136, 34, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var battleProgressImage = battleProgressFill.GetComponent<Image>();
            battleProgressImage.type = Image.Type.Filled;
            battleProgressImage.fillMethod = Image.FillMethod.Horizontal;
            battleProgressImage.fillOrigin = 0;
            battleProgressImage.fillAmount = 0f;
            battleStagePanel.SetActive(false);

            AssignField(controller, "goldLabel", goldV2);
            AssignField(controller, "roundLabel", roundV2);
            AssignField(controller, "hpLabel", hpV2);
            AssignField(controller, "stateLabel", stateV2);
            AssignField(controller, "hpFillImage", hpFillImage);
            AssignField(controller, "titlePanel", titlePanel);
            AssignField(controller, "runPanel", runPanel);
            AssignField(controller, "campaignDropdown", campaignDropdown);
            AssignField(controller, "heroDropdown", heroDropdown);
            AssignField(controller, "campaignDescriptionLabel", campaignDescription);
            AssignField(controller, "heroDescriptionLabel", heroDescription);
            AssignField(controller, "campaignLabel", campaignTextV2);
            AssignField(controller, "heroLabel", heroTextV2);
            AssignField(controller, "logLabel", logTextV2);
            AssignField(controller, "shopMetaLabel", shopMetaTextV2);
            AssignField(controller, "shopCardRoot", shopCardRootV2);
            AssignField(controller, "handCardRoot", handCardRootV2);
            AssignField(controller, "boardCardRoot", boardCardRootV2);
            AssignField(controller, "shopText", null);
            AssignField(controller, "handText", null);
            AssignField(controller, "boardText", null);
            AssignField(controller, "battlePreviewText", battlePreviewTextV2);
            AssignField(controller, "battleStagePanel", battleStagePanel);
            AssignField(controller, "battlePlayerRoot", playerBattleRoot);
            AssignField(controller, "battleEnemyRoot", enemyBattleRoot);
            AssignField(controller, "battleStageStatusLabel", battleStageStatus);
            AssignField(controller, "battleStageLogLabel", battleStageLog);
            AssignField(controller, "battleStageProgressFill", battleProgressImage);
            return canvasObject;
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

        private static void SetPixelRectTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
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
            text.verticalOverflow = VerticalWrapMode.Truncate;
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

        private static Transform CreateHorizontalCardListRoot(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = new GameObject(name, typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return root.transform;
        }

        private static Transform CreateGridCardRoot(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = new GameObject(name, typeof(GridLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var layout = root.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(108f, 138f);
            layout.spacing = new Vector2(10f, 10f);
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.Flexible;
            return root.transform;
        }

        private static Transform CreateBoardGridRoot(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = new GameObject(name, typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return root.transform;
        }

        private static Transform CreateBattleUnitRoot(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = new GameObject(name, typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
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
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
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
            rect.anchoredPosition = new Vector2((anchorPosition.x - 0.5f) * 2560f, (anchorPosition.y - 0.5f) * 1280f);

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
