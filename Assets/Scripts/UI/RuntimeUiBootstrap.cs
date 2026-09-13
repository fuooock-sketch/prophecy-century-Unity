using System.Collections.Generic;
using System.IO;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
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
        private const string TitleLogoResourcePath = "Art/Login/prophecy_century_logo";
        private const string ElementalBattleChallengeButtonName = "ElementalBattleChallengeButton";
        private static readonly Dictionary<string, Sprite> HeroPortraitSprites = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRunSceneUi()
        {
            EnsureSession();
            EnsureEventSystem();

            var existingController = Object.FindObjectOfType<RunSceneController>();
            if (existingController != null)
            {
                Debug.Log("Found existing controller, disabling old canvas");
                var canvas = existingController.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
                CreateGeneratedUi();
                return;
            }

            BuildUi();
        }

        public static void EnsureSession()
        {
            ProphecyGameSession.EnsureInstance();
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            // A scene can contain an EventSystem component without an enabled input
            // module. In that state the UI renders normally, but no Button receives
            // pointer or navigation events. Repair that partial setup at runtime.
            eventSystem.enabled = true;
            var inputModule = eventSystem.GetComponent<BaseInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            inputModule.enabled = true;
            if (eventSystem.GetComponent<RuntimeUiClickDiagnostics>() == null)
            {
                eventSystem.gameObject.AddComponent<RuntimeUiClickDiagnostics>();
            }
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

            EnsureTitleVideoLayout(root.transform);
            BindTitlePanel(root.transform, controller);
            TryInstallBattleStagePanelPrefab(root.transform, controller);
            HideTitleSelectionControls(root.transform);
            EnsureTitleShortcutChallengeButton(root.transform, controller);
            EnsureSelectionScreens(root.transform, controller);
            EnsurePlayerHpBar(root.transform, controller);
            EnsureArmyPowerLabel(root.transform, controller);
            EnsureAuxiliaryMenu(root.transform, controller);
            EnsureSaveSlotMenu(root.transform, controller);

            WireButton(root.transform, "StartSelectedRunButton", controller.OpenCampaignSelection);
            WireButton(root.transform, "StartGameButton", controller.OpenCampaignSelection);
            WireButton(root.transform, "ContinueGameButton", controller.ContinueGame);
            WireButton(root.transform, ElementalBattleChallengeButtonName, controller.OpenElementalBattleChallenge);
            WireButton(root.transform, "RefreshShopButton", controller.RefreshShop);
            WireButton(root.transform, "UpgradeShopButton", controller.UpgradeShop);
            WireButton(root.transform, "LockShopButton", controller.ToggleShopLock);
            WireButton(root.transform, "BattleButton", controller.StartDayExploreFromManage);
            WireButton(root.transform, "SaveGameButton", controller.SaveGame);
            WireButton(root.transform, "LoadGameButton", controller.LoadGame);
            WireButton(root.transform, "RefreshShopButtonV2", controller.RefreshShop);
            WireButton(root.transform, "UpgradeShopButtonV2", controller.UpgradeShop);
            WireButton(root.transform, "LockShopButtonV2", controller.ToggleShopLock);
            WireButton(root.transform, "RealtimeBattleToggleButtonV2", controller.ToggleRealtimeBattlePreview);
            WireButton(root.transform, "BattleButtonV2", controller.StartDayExploreFromManage);
            WireButton(root.transform, "SaveGameButtonV2", controller.SaveGame);
            WireButton(root.transform, "LoadGameButtonV2", controller.LoadGame);
            SetButtonText(root.transform, "BattleButton", "探索");
            SetButtonText(root.transform, "BattleButtonV2", "探索");
            if (encyclopedia != null)
            {
                WireButton(root.transform, "EncyclopediaButtonV2", encyclopedia.Open);
            }
            EnsureCasualPvpUi(root.transform, controller);
        }

        private static void EnsureTitleVideoLayout(Transform root)
        {
            if (root == null) return;
            var title = FindDeepChild(root, "TitlePanel");
            if (title == null) return;

            TitleVideoBackground.Create(root);
        }

        private static void EnsureTitleLogo(Transform title)
        {
            var logoTexture = Resources.Load<Texture2D>(TitleLogoResourcePath);
            if (logoTexture == null)
            {
                Debug.LogWarning("Title logo texture was not found at Resources/" + TitleLogoResourcePath + ".");
                return;
            }

            var logo = title.Find("TitleLogo")?.GetComponent<RawImage>();
            if (logo == null)
            {
                var logoObject = new GameObject("TitleLogo", typeof(RectTransform), typeof(RawImage));
                logoObject.transform.SetParent(title, false);
                logo = logoObject.GetComponent<RawImage>();
            }

            logo.texture = logoTexture;
            logo.raycastTarget = false;
            SetTitleCenteredRect(logo.rectTransform, new Vector2(650f, -415f), new Vector2(920f, 652f));

            SetTitleTextVisibility(title, "TitleTextGlow", false);
            SetTitleTextVisibility(title, "TitleText", false);

            var subtitle = title.Find("TitleSubtitle")?.GetComponent<Text>();
            if (subtitle != null)
            {
                subtitle.text = "Prophecy Century";
                subtitle.fontSize = 28;
                subtitle.color = new Color32(235, 212, 157, 220);
                SetTitleCenteredRect(subtitle.rectTransform, new Vector2(650f, -756f), new Vector2(560f, 44f));
            }
        }

        private static void SetTitleTextVisibility(Transform title, string name, bool visible)
        {
            var element = title.Find(name);
            if (element != null) element.gameObject.SetActive(visible);
        }

        private static void EnsureSaveSlotMenu(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null || root.GetComponent<SaveSlotMenuController>() != null) return;
            var titlePanel = FindDeepChild(root, "TitlePanel")?.gameObject;
            var continueButton = FindDeepChild(root, "ContinueGameButton")?.GetComponent<Button>();
            if (titlePanel == null || continueButton == null) return;
            root.gameObject.AddComponent<SaveSlotMenuController>().Initialize(controller, titlePanel, continueButton);
        }

        private static void EnsureCasualPvpUi(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null) return;
            var existing = root.GetComponent<CasualPvpUiController>();
            if (existing != null)
            {
                existing.RewireTitleButtons();
                return;
            }
            var titlePanel = FindDeepChild(root, "TitlePanel")?.gameObject;
            var continueButton = FindDeepChild(root, "ContinueGameButton")?.GetComponent<Button>();
            var saveMenu = root.GetComponent<SaveSlotMenuController>();
            if (titlePanel == null || continueButton == null || saveMenu == null) return;
            root.gameObject.AddComponent<CasualPvpUiController>().Initialize(controller, titlePanel, saveMenu, continueButton);
        }

        private static void EnsureAuxiliaryMenu(Transform root, RunSceneController controller)
        {
            var title = FindDeepChild(root, "TitlePanel");
            if (title == null) return;
            if (FindDeepChild(root, "SettingsModal") == null)
            {
                var settings = CreateSettingsModal(root);
                settings.SetActive(false);
            }
            if (FindDeepChild(root, "CreditsModal") == null)
            {
                var credits = CreateCreditsModal(root);
                credits.SetActive(false);
            }
            if (FindDeepChild(root, "LeaderboardModal") == null)
            {
                var leaderboard = CreateLeaderboardModal(root);
                leaderboard.SetActive(false);
            }
            if (FindDeepChild(title, "CreditsButton") == null)
            {
                CreateButton("CreditsButton", title, "制作组", new Vector2(2050f, -636f), new Vector2(320f, 56f), () => ShowModal(root, "CreditsModal"));
                StyleSecondaryButton(title.Find("CreditsButton"));
            }
            if (FindDeepChild(title, "LeaderboardButton") == null)
            {
                CreateButton("LeaderboardButton", title, "排行榜", new Vector2(0f, 0f), new Vector2(180f, 44f), () => ShowLeaderboardModal(root));
                StyleSecondaryButton(title.Find("LeaderboardButton"));
            }
            var leaderboardButton = FindDeepChild(title, "LeaderboardButton")?.GetComponent<Button>();
            if (leaderboardButton != null)
            {
                leaderboardButton.onClick.RemoveAllListeners();
                leaderboardButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                leaderboardButton.onClick.AddListener(() => ShowLeaderboardModal(root));
            }
            var quitButtonTransform = FindDeepChild(title, "QuitGameButton");
            if (FindDeepChild(title, "TitleEncyclopediaButton") == null)
            {
                CreateButton("TitleEncyclopediaButton", title, "图鉴", Vector2.zero, new Vector2(180f, 44f), () => OpenTitleEncyclopedia(root));
                StyleSecondaryButton(title.Find("TitleEncyclopediaButton"));
            }
            WireButton(title, "TitleEncyclopediaButton", () => OpenTitleEncyclopedia(root));
            PlaceTitleUtilityButton(FindDeepChild(title, "TitleEncyclopediaButton"), 1960f);
            PlaceTitleUtilityButton(leaderboardButton?.transform, 2160f);
            PlaceTitleUtilityButton(quitButtonTransform, 2360f);
            var settingsButton = FindDeepChild(title, "SettingsButton")?.GetComponent<Button>();
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                settingsButton.onClick.AddListener(() => ShowModal(root, "SettingsModal"));
            }
        }

        private static void EnsurePlayerHpBar(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null)
            {
                return;
            }

            var hpBar = FindDeepChild(root, "HpBar");
            if (hpBar == null)
            {
                return;
            }

            var fill = FindDeepChild(hpBar, "HpFill")?.GetComponent<Image>();
            if (fill == null)
            {
                var fillObject = new GameObject("HpFill", typeof(Image));
                fillObject.transform.SetParent(hpBar, false);
                fill = fillObject.GetComponent<Image>();
            }

            ConfigurePlayerHpFill(fill);
            fill.transform.SetAsFirstSibling();
            AssignField(controller, "hpFillImage", fill);
        }

        private static void ConfigurePlayerHpFill(Image fill)
        {
            if (fill == null)
            {
                return;
            }

            fill.type = Image.Type.Simple;
            fill.fillAmount = 1f;
            fill.color = new Color32(226, 28, 31, 255);
            fill.raycastTarget = false;

            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
        }

        private static void EnsureArmyPowerLabel(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null)
            {
                return;
            }

            var label = FindDeepChild(root, "ArmyPowerLabelV2")?.GetComponent<Text>();
            if (label == null)
            {
                var playerPanel = FindDeepChild(root, "PlayerPanelV2");
                if (playerPanel == null)
                {
                    playerPanel = FindDeepChild(root, "PlayerPanel");
                }

                if (playerPanel == null)
                {
                    return;
                }

                label = CreateText("ArmyPowerLabelV2", playerPanel, "全军战力：0", 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SetPixelRectTopLeft(label.GetComponent<RectTransform>(), 46f, 448f, 311f, 30f);
            }

            label.color = new Color32(255, 218, 110, 255);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 26;
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            AssignField(controller, "armyPowerLabel", label);
        }

        private static void EnsureTitleShortcutChallengeButton(Transform root, RunSceneController controller)
        {
        }

        private static void EnsureSelectionScreens(Transform root, RunSceneController controller)
        {
            if (root == null || controller == null)
            {
                return;
            }

            var titlePanel = FindDeepChild(root, "TitlePanel")?.gameObject;
            var campaignScreen = FindDeepChild(root, "CampaignSelectionScreen")?.gameObject;
            var heroScreen = FindDeepChild(root, "HeroSelectionScreen")?.gameObject;
            var formationPreviewScreen = FindDeepChild(root, "CampaignFormationPreviewScreen")?.gameObject;

            if (campaignScreen == null)
            {
                campaignScreen = CreateCampaignSelectionScreen(root, controller);
            }

            if (heroScreen == null)
            {
                heroScreen = CreateHeroSelectionScreen(root, controller);
            }

            if (formationPreviewScreen == null)
            {
                formationPreviewScreen = CreateCampaignFormationPreviewScreen(root, controller);
            }

            campaignScreen.SetActive(false);
            heroScreen.SetActive(false);
            formationPreviewScreen.SetActive(false);
            controller.SetSelectionScreens(titlePanel, campaignScreen, heroScreen, formationPreviewScreen);
        }

        private static void HideTitleSelectionControls(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var names = new[] { "HeroSelectionPanel", "ChaseTestButton" };
            foreach (var name in names)
            {
                var item = FindDeepChild(root, name);
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }
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
            button.onClick.AddListener(() => Debug.Log($"[UI Button] onClick invoked: {RuntimeUiClickDiagnostics.GetHierarchyPath(button.gameObject)}"));
            button.onClick.AddListener(callback);
            if (button.GetComponent<RuntimeButtonClickLogger>() == null)
            {
                button.gameObject.AddComponent<RuntimeButtonClickLogger>();
            }
        }

        private static void SetButtonText(Transform root, string name, string text)
        {
            var target = FindDeepChild(root, name);
            var label = target != null ? target.GetComponentInChildren<Text>(true) : null;
            if (label != null)
            {
                label.text = text;
            }
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

        public const string TitlePanelPrefabAssetPath = "Assets/Resources/Prefabs/UI/TitlePanel.prefab";
        private const string TitlePanelPrefabResourcePath = "Prefabs/UI/TitlePanel";

        public static GameObject CreateTitlePanel(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(TitlePanelPrefabResourcePath);
            if (prefab == null) return CreateDefaultTitlePanel(parent);
            var panel = Object.Instantiate(prefab, parent, false);
            panel.name = "TitlePanel";
            return panel;
        }

        // Used by the editor generator and as a fallback if the asset is missing.
        public static GameObject CreateDefaultTitlePanel(Transform parent)
        {
            var titlePanel = CreatePanel("TitlePanel", parent, new Color32(5, 9, 18, 42), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            titlePanel.GetComponent<Image>().raycastTarget = false;
            CreateTitleText(titlePanel.transform);
            EnsureTitleLogo(titlePanel.transform);

            // 按钮：继续游戏 / 开始游戏 / 设置 / 退出游戏
            CreateButton("ContinueGameButton", titlePanel.transform, "继续游戏", new Vector2(2050f, -380f), new Vector2(320f, 72f), () => { });
            StylePrimaryButton(titlePanel.transform.Find("ContinueGameButton"));

            CreateButton("StartGameButton", titlePanel.transform, "开始游戏", new Vector2(2050f, -472f), new Vector2(320f, 72f), () => { });
            StylePrimaryButton(titlePanel.transform.Find("StartGameButton"));
            CreateButton("SettingsButton", titlePanel.transform, "设置", new Vector2(2050f, -564f), new Vector2(320f, 56f), () => { });
            StyleSecondaryButton(titlePanel.transform.Find("SettingsButton"));
            CreateButton("CreditsButton", titlePanel.transform, "制作组", new Vector2(2050f, -636f), new Vector2(320f, 56f), () => { });
            StyleSecondaryButton(titlePanel.transform.Find("CreditsButton"));
            CreateButton("TitleEncyclopediaButton", titlePanel.transform, "图鉴", Vector2.zero, new Vector2(180f, 44f), () => { });
            StyleSecondaryButton(titlePanel.transform.Find("TitleEncyclopediaButton"));
            PlaceTitleUtilityButton(titlePanel.transform.Find("TitleEncyclopediaButton"), 1960f);
            CreateButton("LeaderboardButton", titlePanel.transform, "排行榜", new Vector2(2160f, -1212f), new Vector2(180f, 44f), () => { });
            StyleSecondaryButton(titlePanel.transform.Find("LeaderboardButton"));
            CreateButton("QuitGameButton", titlePanel.transform, "退出游戏", new Vector2(2360f, -1212f), new Vector2(180f, 44f), () => { });
            StyleSecondaryButton(titlePanel.transform.Find("QuitGameButton"));

            // 版本号
            var versionText = CreateText("VersionText", titlePanel.transform, "v" + Application.version, 16, TextAnchor.LowerRight, Vector2.zero, Vector2.one, new Vector2(0f, 14f), new Vector2(-20f, 0f));
            versionText.color = new Color32(120, 140, 160, 100);
            versionText.raycastTarget = false;

            var names = new[] { "ContinueGameButton", "StartGameButton", "SettingsButton", "CreditsButton" };
            var yPositions = new[] { -380f, -472f, -564f, -636f };
            for (var index = 0; index < names.Length; index++)
            {
                SetTitleCenteredRect(titlePanel.transform.Find(names[index]).GetComponent<RectTransform>(),
                    new Vector2(2050f, yPositions[index]), new Vector2(320f, index < 2 ? 72f : 56f));
            }
            SetTitleCenteredRect(titlePanel.transform.Find("LeaderboardButton")?.GetComponent<RectTransform>(), new Vector2(2160f, -1212f), new Vector2(180f, 44f));
            SetTitleCenteredRect(titlePanel.transform.Find("QuitGameButton")?.GetComponent<RectTransform>(), new Vector2(2360f, -1212f), new Vector2(180f, 44f));
            return titlePanel;
        }

        private static void OpenTitleEncyclopedia(Transform root)
        {
            if (root == null) return;
            var encyclopedia = root.GetComponentInChildren<RuntimeEncyclopediaPanel>(true)
                ?? root.gameObject.AddComponent<RuntimeEncyclopediaPanel>();
            encyclopedia.Open();
        }

        public static void BindTitlePanel(Transform root, RunSceneController controller)
        {
            WireButton(root, "ContinueGameButton", controller.ContinueGame);
            WireButton(root, "StartGameButton", controller.OpenCampaignSelection);
            WireButton(root, "SettingsButton", () => ShowModal(root, "SettingsModal"));
            WireButton(root, "CreditsButton", () => ShowModal(root, "CreditsModal"));
            WireButton(root, "LeaderboardButton", () => ShowLeaderboardModal(root));
            WireButton(root, "TitleEncyclopediaButton", () => OpenTitleEncyclopedia(root));
            WireButton(root, "QuitGameButton", controller.ShowExitConfirmDialog);
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

            // ---- Title Panel ----
            TitleVideoBackground.Create(canvasObject.transform);
            var titlePanel = CreateTitlePanel(canvasObject.transform);
            BindTitlePanel(canvasObject.transform, controller);

            var campaignSelectionScreen = CreateCampaignSelectionScreen(canvasObject.transform, controller);
            var heroSelectionScreen = CreateHeroSelectionScreen(canvasObject.transform, controller);
            var formationPreviewScreen = CreateCampaignFormationPreviewScreen(canvasObject.transform, controller);
            campaignSelectionScreen.SetActive(false);
            heroSelectionScreen.SetActive(false);
            formationPreviewScreen.SetActive(false);

            AssignField(controller, "titlePanel", titlePanel);
            controller.SetSelectionScreens(titlePanel, campaignSelectionScreen, heroSelectionScreen, formationPreviewScreen);

            var settingsModal = CreateSettingsModal(canvasObject.transform);
            settingsModal.SetActive(false);
            var creditsModal = CreateCreditsModal(canvasObject.transform);
            creditsModal.SetActive(false);
            EnsureAuxiliaryMenu(canvasObject.transform, controller);

            // ConfirmDialog
            ConfirmDialog.FindOrCreate(canvasObject.transform);
            EnsureSaveSlotMenu(canvasObject.transform, controller);
            EnsureCasualPvpUi(canvasObject.transform, controller);

            var runPanel = CreatePanel("RunPanel", canvasObject.transform, new Color32(18, 24, 31, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var topBar = CreatePanel("TopBar", runPanel.transform, new Color32(25, 34, 44, 255), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero);
            var gold = CreateIconText("GoldLabel", topBar.transform, "\u91d1\u5e01", "金币：0", 20, new Vector2(0f, 0f), new Vector2(0.13f, 1f), new Vector2(12f, 0f), Vector2.zero);
            var round = CreateIconText("RoundLabel", topBar.transform, "\u65f6\u95f4", "回合：1", 20, new Vector2(0.13f, 0f), new Vector2(0.26f, 1f), new Vector2(4f, 0f), Vector2.zero);
            var hp = CreateIconText("HpLabel", topBar.transform, "\u8840\u74f6", "生命：100", 20, new Vector2(0.26f, 0f), new Vector2(0.39f, 1f), new Vector2(4f, 0f), Vector2.zero);
            var state = CreateIconText("StateLabel", topBar.transform, "\u65f6\u95f4", "阶段：经营", 20, new Vector2(0.39f, 0f), new Vector2(0.52f, 1f), new Vector2(4f, 0f), Vector2.zero);

            CreateButton("RefreshShopButton", topBar.transform, "刷新", new Vector2(910f, 0f), new Vector2(110f, 42f), controller.RefreshShop, "\u5546\u5e97");
            CreateButton("UpgradeShopButton", topBar.transform, "升级", new Vector2(1030f, 0f), new Vector2(110f, 42f), controller.UpgradeShop, "\u94bb\u77f3");
            CreateButton("LockShopButton", topBar.transform, "锁定", new Vector2(1150f, 0f), new Vector2(110f, 42f), controller.ToggleShopLock, "\u94c1\u9501");
            CreateButton("BattleButton", topBar.transform, "探索", new Vector2(1288f, 0f), new Vector2(138f, 48f), controller.StartDayExploreFromManage, "\u957f\u5251");
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

            var heroPortrait = CreatePanel("HeroPortrait", playerPanelV2.transform, Color.clear, new Vector2(0.11f, 0.51f), new Vector2(0.89f, 0.95f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(heroPortrait.GetComponent<RectTransform>(), 46f, 51f, 306f, 397f);
            var hpBar = CreatePanel("HpBar", playerPanelV2.transform, new Color32(221, 221, 221, 255), new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.465f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(hpBar.GetComponent<RectTransform>(), 46f, 478f, 311f, 36f);
            var hpFill = CreatePanel("HpFill", hpBar.transform, new Color32(226, 28, 31, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hpFillImage = hpFill.GetComponent<Image>();
            ConfigurePlayerHpFill(hpFillImage);
            var hpV2 = CreateText("HpLabelV2", hpBar.transform, "100/100", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var armyPowerV2 = CreateText("ArmyPowerLabelV2", playerPanelV2.transform, "全军战力：0", 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            armyPowerV2.color = new Color32(255, 218, 110, 255);
            armyPowerV2.resizeTextForBestFit = true;
            armyPowerV2.resizeTextMinSize = 18;
            armyPowerV2.resizeTextMaxSize = 26;
            var goldV2 = CreateText("GoldLabelV2", playerPanelV2.transform, "金币：0", 18, TextAnchor.MiddleLeft, new Vector2(0.12f, 0.37f), new Vector2(0.88f, 0.405f), Vector2.zero, Vector2.zero);
            var stateV2 = CreateText("StateLabelV2", playerPanelV2.transform, "阶段：经营", 18, TextAnchor.MiddleLeft, new Vector2(0.12f, 0.335f), new Vector2(0.88f, 0.37f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(armyPowerV2.GetComponent<RectTransform>(), 46f, 448f, 311f, 30f);
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

            var roundV2 = CreateText("RoundLabelV2", boardPanelV2.transform, "0    第 1 回合", 38, TextAnchor.MiddleRight, new Vector2(0.42f, 0.03f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero);
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

            var shopMetaStarV2 = CreateShopMetaStarRoot(shopPanelV2.transform);
            var shopCardRootV2 = CreateGridCardRoot("ShopCardRootV2", shopPanelV2.transform, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(shopMetaStarV2.GetComponent<RectTransform>(), 34f, 17f, 560f, 88f);
            SetPixelRectTopLeft(shopCardRootV2.GetComponent<RectTransform>(), 34f, 110f, 735f, 589f);
            var shopGrid = shopCardRootV2.GetComponent<GridLayoutGroup>();
            shopGrid.cellSize = new Vector2(221f, 286f);
            shopGrid.spacing = new Vector2(28f, 12f);
            shopGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            shopGrid.constraintCount = 3;
            CreateButton("UpgradeShopButtonV2", shopPanelV2.transform, "升级", new Vector2(88f, -282f), new Vector2(142f, 58f), controller.UpgradeShop);
            CreateButton("LockShopButtonV2", shopPanelV2.transform, "锁定", new Vector2(246f, -282f), new Vector2(142f, 58f), controller.ToggleShopLock);
            CreateButton("RefreshShopButtonV2", shopPanelV2.transform, "刷新", new Vector2(404f, -282f), new Vector2(142f, 58f), controller.RefreshShop);
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
            CreateButton("BattleButtonV2", battlePanelV2.transform, "探索", new Vector2(106f, 0f), new Vector2(212f, 188f), controller.StartDayExploreFromManage);
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
            AssignField(controller, "armyPowerLabel", armyPowerV2);
            AssignField(controller, "hpFillImage", hpFillImage);
            AssignField(controller, "titlePanel", titlePanel);
            AssignField(controller, "runPanel", runPanel);
            AssignField(controller, "logLabel", logTextV2);
            AssignField(controller, "shopMetaLabel", null);
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

            ProphecyGameSession.EnsureInstance();
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

        private static GameObject CreateShopMetaStarRoot(Transform parent)
        {
            var root = new GameObject("ShopMetaStarV2", typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var starObject = new GameObject("star_1", typeof(Image));
            starObject.transform.SetParent(root.transform, false);
            var starRect = starObject.GetComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0.5f, 0.5f);
            starRect.anchorMax = new Vector2(0.5f, 0.5f);
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.anchoredPosition = Vector2.zero;
            starRect.sizeDelta = new Vector2(76f, 76f);
            ApplySpriteFromProjectPath(starObject.GetComponent<Image>(), "Art/icon/system/star.png");

            return root;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        private static void CreateTitleAstrolabe(Transform parent)
        {
            var root = new GameObject("AstrolabeRoot", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateTitleGlow(root.transform, "OuterVignette", new Vector2(1280f, -650f), new Vector2(2100f, 2100f), new Color32(12, 27, 48, 78));
            CreateTitleGlow(root.transform, "InnerOracleGlow", new Vector2(1280f, -606f), new Vector2(760f, 760f), new Color32(50, 162, 178, 34));
            CreateAstrolabeRing(root.transform, "RingOuter", new Vector2(1280f, -610f), 780f, 4f, new Color32(218, 178, 97, 118));
            CreateAstrolabeRing(root.transform, "RingMiddle", new Vector2(1280f, -610f), 608f, 3f, new Color32(111, 206, 218, 96));
            CreateAstrolabeRing(root.transform, "RingInner", new Vector2(1280f, -610f), 390f, 3f, new Color32(218, 178, 97, 94));
            CreateAstrolabeRing(root.transform, "RingCore", new Vector2(1280f, -610f), 172f, 2f, new Color32(226, 235, 214, 92));

            for (var i = 0; i < 24; i += 1)
            {
                var angle = i * 15f;
                var radians = angle * Mathf.Deg2Rad;
                var radius = i % 2 == 0 ? 390f : 304f;
                var length = i % 2 == 0 ? 98f : 56f;
                var center = new Vector2(1280f + Mathf.Cos(radians) * radius, -610f + Mathf.Sin(radians) * radius);
                CreateTitleLine(root.transform, "AstrolabeTick", center, new Vector2(3f, length), angle + 90f, new Color32(222, 189, 111, i % 2 == 0 ? (byte)138 : (byte)84));
            }

            for (var i = 0; i < 18; i += 1)
            {
                var angle = (i * 47f + 12f) * Mathf.Deg2Rad;
                var radius = 118f + (i % 5) * 88f;
                var position = new Vector2(1280f + Mathf.Cos(angle) * radius, -610f + Mathf.Sin(angle) * radius);
                var size = i % 4 == 0 ? 14f : 8f;
                CreateTitleGlow(root.transform, "StarPoint", position, new Vector2(size, size), new Color32(221, 244, 238, i % 4 == 0 ? (byte)190 : (byte)128));
            }

            CreateTitleLine(root.transform, "FateLineA", new Vector2(1280f, -610f), new Vector2(2f, 1120f), 62f, new Color32(118, 210, 217, 54));
            CreateTitleLine(root.transform, "FateLineB", new Vector2(1280f, -610f), new Vector2(2f, 960f), -48f, new Color32(218, 178, 97, 58));
            CreateTitleLine(root.transform, "FateLineC", new Vector2(1280f, -610f), new Vector2(2f, 860f), 0f, new Color32(226, 235, 214, 38));

            var leftRune = CreateText("RuneLeft", root.transform, "I  II  V  VIII  XIII", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(leftRune.GetComponent<RectTransform>(), new Vector2(490f, -250f), new Vector2(360f, 34f));
            leftRune.color = new Color32(213, 184, 116, 120);

            var rightRune = CreateText("RuneRight", root.transform, "ORACLE  VEIL  OATH", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(rightRune.GetComponent<RectTransform>(), new Vector2(2060f, -1030f), new Vector2(420f, 34f));
            rightRune.color = new Color32(116, 209, 220, 110);
        }

        private static void CreateTitleText(Transform parent)
        {
            var shadow = CreateText("TitleTextGlow", parent, "预言世纪", 86, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(shadow.GetComponent<RectTransform>(), new Vector2(650f, -210f), new Vector2(760f, 126f));
            shadow.color = new Color32(72, 176, 188, 92);

            var title = CreateText("TitleText", parent, "预言世纪", 78, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(title.GetComponent<RectTransform>(), new Vector2(650f, -204f), new Vector2(720f, 118f));
            title.color = new Color32(239, 204, 126, 255);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 54;
            title.resizeTextMaxSize = 78;

            var subtitle = CreateText("TitleSubtitle", parent, "在星盘中选择命运的入口", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(subtitle.GetComponent<RectTransform>(), new Vector2(650f, -302f), new Vector2(560f, 44f));
            subtitle.color = new Color32(195, 223, 220, 188);
        }

        private static GameObject CreateTitleSelectionPanel(string name, Transform parent, string title, string subtitle, float left, float top)
        {
            var panel = CreatePanel(name, parent, new Color32(8, 15, 29, 218), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(panel.GetComponent<RectTransform>(), left, top, 604f, 464f);
            panel.GetComponent<Image>().raycastTarget = false;

            var rim = CreatePanel("RitualRim", panel.transform, new Color32(204, 169, 94, 72), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rim.GetComponent<Image>().raycastTarget = false;
            var inner = CreatePanel("RitualInner", panel.transform, new Color32(12, 28, 45, 210), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            inner.GetComponent<Image>().raycastTarget = false;
            CreateTitleLine(panel.transform, "TopRule", new Vector2(302f, -86f), new Vector2(520f, 2f), 0f, new Color32(214, 184, 104, 130));
            CreateTitleLine(panel.transform, "BottomRule", new Vector2(302f, -438f), new Vector2(520f, 2f), 0f, new Color32(92, 188, 200, 86));

            var titleText = CreateText("PanelTitle", panel.transform, title, 34, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(titleText.GetComponent<RectTransform>(), 42f, 26f, 230f, 46f);
            titleText.color = new Color32(238, 205, 129, 255);

            var subtitleText = CreateText("PanelSubtitle", panel.transform, subtitle, 18, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(subtitleText.GetComponent<RectTransform>(), 278f, 32f, 284f, 34f);
            subtitleText.color = new Color32(166, 207, 205, 150);
            return panel;
        }

        private static void StyleTitleBodyText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.color = new Color32(214, 225, 215, 224);
            text.fontSize = Mathf.Max(18, text.fontSize);
            text.lineSpacing = 1.08f;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void StyleTitleDropdown(Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            var image = dropdown.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color32(16, 31, 48, 245);
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = new Color32(239, 235, 205, 255);
                dropdown.captionText.fontSize = 22;
            }

            if (dropdown.template != null)
            {
                var templateImage = dropdown.template.GetComponent<Image>();
                if (templateImage != null)
                {
                    templateImage.color = new Color32(12, 24, 38, 248);
                }
            }
        }

        private static void StyleTitleButton(Transform buttonTransform, bool primary)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = primary ? new Color32(187, 129, 42, 255) : new Color32(27, 51, 67, 214);
            }

            var label = buttonTransform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = primary ? new Color32(255, 243, 200, 255) : new Color32(183, 220, 217, 205);
                label.fontSize = primary ? 26 : 18;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = primary ? 18 : 14;
                label.resizeTextMaxSize = primary ? 26 : 18;
            }

            if (primary)
            {
                CreateTitleLine(buttonTransform, "ButtonTopGleam", new Vector2(190f, -12f), new Vector2(300f, 2f), 0f, new Color32(255, 229, 154, 150));
                CreateTitleLine(buttonTransform, "ButtonBottomGleam", new Vector2(190f, -74f), new Vector2(300f, 2f), 0f, new Color32(90, 40, 16, 100));
            }
        }

        private static void CreateAstrolabeRing(Transform parent, string name, Vector2 center, float size, float thickness, Color color)
        {
            const int segments = 40;
            var radius = size * 0.5f;
            var segmentLength = Mathf.Max(12f, (2f * Mathf.PI * radius) / segments * 0.72f);
            for (var i = 0; i < segments; i += 1)
            {
                var angle = i * (360f / segments);
                var radians = angle * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
                CreateTitleLine(parent, name + "Segment", position, new Vector2(segmentLength, thickness), angle + 90f, color);
            }
        }

        private static void CreateTitleGlow(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            var glow = CreatePanel(name, parent, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            glow.GetComponent<Image>().raycastTarget = false;
            SetTitleCenteredRect(glow.GetComponent<RectTransform>(), center, size);
        }

        private static void CreateTitleLine(Transform parent, string name, Vector2 center, Vector2 size, float rotation, Color color)
        {
            var line = CreatePanel(name, parent, color, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            line.GetComponent<Image>().raycastTarget = false;
            var rect = line.GetComponent<RectTransform>();
            SetTitleCenteredRect(rect, center, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static void SetTitleCenteredRect(RectTransform rect, Vector2 center, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private static void PlaceTitleUtilityButton(Transform buttonTransform, float centerX)
        {
            if (buttonTransform == null)
            {
                return;
            }

            SetTitleCenteredRect(buttonTransform.GetComponent<RectTransform>(), new Vector2(centerX, -1212f), new Vector2(180f, 44f));
            var label = buttonTransform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.fontSize = 18;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 18;
            }
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
            button.onClick.AddListener(() => Debug.Log($"[UI Button] onClick invoked: {RuntimeUiClickDiagnostics.GetHierarchyPath(buttonObject)}"));
            button.onClick.AddListener(callback);
            buttonObject.AddComponent<RuntimeButtonClickLogger>();

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

        private static void StylePrimaryButton(Transform buttonTransform)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color32(187, 129, 42, 255);
            }

            var label = buttonTransform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = new Color32(255, 243, 200, 255);
                label.fontSize = 26;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 18;
                label.resizeTextMaxSize = 26;
            }

            CreateTitleLine(buttonTransform, "ButtonTopGleam", new Vector2(160f, -12f), new Vector2(300f, 2f), 0f, new Color32(255, 229, 154, 150));
            CreateTitleLine(buttonTransform, "ButtonBottomGleam", new Vector2(160f, -60f), new Vector2(300f, 2f), 0f, new Color32(90, 40, 16, 100));
        }

        private static void StyleSecondaryButton(Transform buttonTransform)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color32(27, 51, 67, 214);
            }

            var label = buttonTransform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = new Color32(183, 220, 217, 205);
                label.fontSize = 20;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 20;
            }
        }

        private static void StyleCampaignListButton(Transform buttonTransform, bool primary)
        {
            if (buttonTransform == null)
            {
                return;
            }

            var image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = primary ? new Color32(158, 105, 38, 255) : new Color32(31, 55, 69, 230);
            }

            var label = buttonTransform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = primary ? new Color32(255, 240, 198, 255) : new Color32(190, 220, 218, 230);
                label.fontSize = primary ? 34 : 32;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 24;
                label.resizeTextMaxSize = primary ? 34 : 32;
            }
        }

        private static GameObject CreateCampaignSelectionScreen(Transform parent, RunSceneController controller)
        {
            var screen = CreatePanel("CampaignSelectionScreen", parent, new Color32(5, 9, 18, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateTitleLine(screen.transform, "TopLine", new Vector2(1280f, -80f), new Vector2(2200f, 2f), 0f, new Color32(92, 188, 200, 86));

            CreateButton("BackButton", screen.transform, "返回", new Vector2(138f, -54f), new Vector2(200f, 72f), () => controller.ReturnToTitleFromCampaign());
            StyleSecondaryButton(screen.transform.Find("BackButton"));
            var backLabel = screen.transform.Find("BackButton/Label")?.GetComponent<Text>();
            if (backLabel != null)
            {
                backLabel.fontSize = 34;
                backLabel.resizeTextMinSize = 26;
                backLabel.resizeTextMaxSize = 34;
            }

            var titleText = CreateText("ScreenTitle", screen.transform, "选择命运的入口", 64, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(titleText.GetComponent<RectTransform>(), new Vector2(1280f, -44f), new Vector2(900f, 76f));
            titleText.color = new Color32(239, 204, 126, 255);

            var subtitleText = CreateText("ScreenSubtitle", screen.transform, "选择战役或载入已通关的 20 回合阵型", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(subtitleText.GetComponent<RectTransform>(), new Vector2(1280f, -104f), new Vector2(1120f, 42f));
            subtitleText.color = new Color32(205, 218, 224, 230);

            var scrollObject = CreatePanel("CampaignListScroll", screen.transform, new Color32(7, 14, 27, 205), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(scrollObject.GetComponent<RectTransform>(), 350f, 170f, 1860f, 1010f);
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreatePanel("CampaignListViewport", scrollObject.transform, new Color32(0, 0, 0, 0), Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f));
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var list = new GameObject("CampaignList", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(viewport.transform, false);
            var listRect = list.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(0.5f, 1f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;
            var listLayout = list.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 18f;
            listLayout.padding = new RectOffset(10, 10, 10, 10);
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            var fitter = list.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = listRect;

            var campaigns = ProphecyGameSession.Instance?.Data?.Campaigns;
            if (campaigns != null && campaigns.Count > 0)
            {
                for (var i = 0; i < campaigns.Count; i += 1)
                {
                    var campaign = campaigns[i];
                    if (campaign == null || string.IsNullOrWhiteSpace(campaign.id))
                    {
                        continue;
                    }

                    CreateCampaignListItem(list.transform, campaign.id, campaign.name, campaign.desc, ResolveCampaignMapImageName(campaign.id, i), false, null, controller);
                }
            }
            else
            {
                var fallbackCampaigns = new[]
                {
                    ("south_town_adventure", "South Town Adventure", "The current Web version routes all campaigns into the same core run flow.", "level 1"),
                    ("snow_peak_defense", "Snow Peak Defense", "A 20-round defense challenge.", "level 2"),
                    ("song_of_sang_city", "Song of Sang City", "A captured Ganger devour board curve.", "level 3"),
                };

                foreach (var (id, name, desc, mapName) in fallbackCampaigns)
                {
                    CreateCampaignListItem(list.transform, id, name, desc, mapName, false, null, controller);
                }
            }

            foreach (var challenge in CustomChallengeSystem.LoadAll())
            {
                if (challenge == null || string.IsNullOrWhiteSpace(challenge.id))
                {
                    continue;
                }

                var desc = $"来源：{challenge.sourceCampaignName}  20 回合阵型";
                CreateCampaignListItem(list.transform, challenge.id, challenge.name, desc, "level 2", true, challenge, controller);
            }

            return screen;
        }

        private static string ResolveCampaignMapImageName(string campaignId, int index)
        {
            switch (campaignId)
            {
                case "south_town_adventure":
                    return "level 1";
                case "snow_peak_defense":
                    return "level 2";
                case "song_of_sang_city":
                    return "level 3";
                default:
                    return "level " + Mathf.Clamp(index + 1, 1, 3);
            }
        }

        private static void CreateCampaignCard(Transform parent, string campaignId, string campaignName, string mapName, RunSceneController controller)
        {
            var card = CreatePanel("CampaignCard_" + campaignId, parent, new Color32(16, 28, 45, 245), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var rim = CreatePanel("CardRim", card.transform, new Color32(204, 169, 94, 72), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rim.GetComponent<Image>().raycastTarget = false;

            var mapImage = CreatePanel("MapImage", card.transform, Color.white, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            ApplySpriteFromProjectPath(mapImage.GetComponent<Image>(), "Art/maps_image/" + mapName + ".png");

            var nameText = CreateText("CampaignName", card.transform, string.IsNullOrWhiteSpace(campaignName) ? campaignId : campaignName, 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12f, -70f), new Vector2(-12f, -110f));
            nameText.color = new Color32(239, 204, 126, 255);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 14;
            nameText.resizeTextMaxSize = 24;

            CreateButton("SelectButton", card.transform, "选择此战役", new Vector2(0f, -180f), new Vector2(200f, 50f), () => controller.SelectCampaignAndOpenHeroSelection(campaignId));
            StylePrimaryButton(card.transform.Find("SelectButton"));
        }

        private static void CreateCampaignListItem(Transform parent, string campaignId, string campaignName, string desc, string mapName, bool custom, CustomChallengeCampaignState challenge, RunSceneController controller)
        {
            var item = CreatePanel((custom ? "CustomChallenge_" : "CampaignListItem_") + campaignId, parent, new Color32(14, 26, 42, 245), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var rowHeight = custom ? 300f : 230f;
            var layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = rowHeight;
            layout.minHeight = rowHeight;

            var rim = CreatePanel("ItemRim", item.transform, new Color32(204, 169, 94, custom ? (byte)110 : (byte)66), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rim.GetComponent<Image>().raycastTarget = false;

            var smallMapImageSize = custom ? new Vector2(320f, 206f) : new Vector2(300f, 170f);
            var mapImage = CreatePanel("MapImage", item.transform, Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(mapImage.GetComponent<RectTransform>(), 24f, (rowHeight - smallMapImageSize.y) * 0.5f, smallMapImageSize.x, smallMapImageSize.y);
            ApplySpriteFromProjectPath(mapImage.GetComponent<Image>(), "Art/maps_image/" + mapName + ".png");

            var nameText = CreateText("CampaignName", item.transform, string.IsNullOrWhiteSpace(campaignName) ? campaignId : campaignName, 48, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(nameText.GetComponent<RectTransform>(), 372f, custom ? 22f : 24f, 940f, 64f);
            nameText.color = custom ? new Color32(154, 226, 255, 255) : new Color32(239, 204, 126, 255);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 30;
            nameText.resizeTextMaxSize = 48;

            var previewSummary = CampaignFormationPreviewSystem.BuildPreviewSummary(campaignId);
            var hasPreview = previewSummary.Rounds != null && previewSummary.Rounds.Count > 0;
            var finalRoundPower = custom && challenge != null && challenge.finalRoundPlayerScore > 0
                ? challenge.finalRoundPlayerScore
                : previewSummary.FinalRoundPower;
            var powerLabel = finalRoundPower > 0 ? $"第20回合实际战力 {finalRoundPower}" : "第20回合实际战力未知";
            var description = string.IsNullOrWhiteSpace(desc) ? "20 回合战役" : desc;
            var createdLabel = custom ? $"新增：{FormatCustomChallengeCreatedAt(challenge)}" : string.Empty;
            var descriptionText = custom
                ? $"{powerLabel}  |  {createdLabel}\n{description}"
                : $"{powerLabel}  |  {description}";
            var descText = CreateText("CampaignDescription", item.transform, descriptionText, 32, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(descText.GetComponent<RectTransform>(), 372f, custom ? 98f : 104f, 900f, custom ? 100f : 86f);
            descText.color = new Color32(205, 218, 224, 245);
            descText.resizeTextForBestFit = true;
            descText.resizeTextMinSize = 24;
            descText.resizeTextMaxSize = 32;

            CreateButton("SelectButton", item.transform, "选择", Vector2.zero, new Vector2(190f, 76f), () => controller.SelectCampaignAndOpenHeroSelection(campaignId));
            SetPixelRectTopLeft(item.transform.Find("SelectButton")?.GetComponent<RectTransform>(), 1540f, custom ? 28f : 28f, 190f, 76f);
            StyleCampaignListButton(item.transform.Find("SelectButton"), true);

            CreateButton("ViewFormationButton", item.transform, hasPreview ? "查看阵型" : "无阵型", Vector2.zero, new Vector2(190f, 76f), () => controller.OpenCampaignFormationPreview(campaignId));
            SetPixelRectTopLeft(item.transform.Find("ViewFormationButton")?.GetComponent<RectTransform>(), 1540f, custom ? 122f : 126f, 190f, 76f);
            StyleCampaignListButton(item.transform.Find("ViewFormationButton"), false);
            var viewButton = item.transform.Find("ViewFormationButton")?.GetComponent<Button>();
            if (viewButton != null)
            {
                viewButton.interactable = hasPreview;
            }

            if (!custom)
            {
                return;
            }

            var input = CreateInputField("RenameInput", item.transform, challenge?.name ?? campaignName);
            SetPixelRectTopLeft(input.GetComponent<RectTransform>(), 372f, 224f, 640f, 50f);

            CreateButton("RenameCustomChallenge", item.transform, "保存名称", Vector2.zero, new Vector2(190f, 50f), () =>
            {
                if (controller.RenameCustomChallenge(campaignId, input.text))
                {
                    nameText.text = input.text;
                }
            });
            SetPixelRectTopLeft(item.transform.Find("RenameCustomChallenge")?.GetComponent<RectTransform>(), 1044f, 224f, 190f, 50f);
            StyleCampaignListButton(item.transform.Find("RenameCustomChallenge"), false);

            CreateButton("DeleteCustomChallenge", item.transform, "删除", Vector2.zero, new Vector2(128f, 50f), () =>
            {
                if (controller.DeleteCustomChallenge(campaignId))
                {
                    UnityEngine.Object.Destroy(item);
                }
            });
            SetPixelRectTopLeft(item.transform.Find("DeleteCustomChallenge")?.GetComponent<RectTransform>(), 1260f, 224f, 128f, 50f);
            StyleCampaignListButton(item.transform.Find("DeleteCustomChallenge"), false);
        }

        private static string FormatCustomChallengeCreatedAt(CustomChallengeCampaignState challenge)
        {
            if (!string.IsNullOrWhiteSpace(challenge?.createdAtLabel))
            {
                return challenge.createdAtLabel;
            }

            var legacy = challenge?.createdLabel;
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                return legacy.Replace("通关挑战", string.Empty).Trim();
            }

            return "未知时间";
        }

        private static GameObject CreateCampaignFormationPreviewScreen(Transform parent, RunSceneController controller)
        {
            var screen = CreatePanel("CampaignFormationPreviewScreen", parent, new Color32(5, 9, 18, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = screen.AddComponent<CampaignFormationPreviewView>();
            view.Build(controller);
            return screen;
        }

        private static InputField CreateInputField(string name, Transform parent, string value)
        {
            var inputObject = new GameObject(name, typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            var image = inputObject.GetComponent<Image>();
            image.color = new Color32(6, 14, 24, 245);

            var text = CreateText("Text", inputObject.transform, value ?? string.Empty, 30, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 22;
            text.resizeTextMaxSize = 30;
            var input = inputObject.GetComponent<InputField>();
            input.textComponent = text;
            input.text = value ?? string.Empty;
            input.targetGraphic = image;
            return input;
        }

        /// <summary>
        /// 从 GameDataRepository.Heroes 动态创建英雄选择界面。
        /// </summary>
        private static GameObject CreateHeroSelectionScreen(Transform parent, RunSceneController controller)
        {
            var screen = CreatePanel("HeroSelectionScreen", parent, new Color32(5, 9, 18, 255), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateTitleLine(screen.transform, "TopLine", new Vector2(1280f, -112f), new Vector2(2200f, 2f), 0f, new Color32(92, 188, 200, 86));

            CreateButton("BackButton", screen.transform, "返回", new Vector2(120f, -40f), new Vector2(140f, 48f), () => controller.ReturnToCampaignFromHero());
            var backButton = screen.transform.Find("BackButton");
            StyleSecondaryButton(backButton);
            SetHeroSelectionFontFloor(backButton?.Find("Label")?.GetComponent<Text>(), 26);

            var titleText = CreateText("ScreenTitle", screen.transform, "选择你的英雄", 46, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(titleText.GetComponent<RectTransform>(), new Vector2(1280f, -34f), new Vector2(720f, 58f));
            titleText.color = new Color32(239, 204, 126, 255);

            var subtitle = CreateText("ScreenSubtitle", screen.transform, "英雄能力会贯穿整局经营与战斗准备，选择后本局不可更换", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(subtitle.GetComponent<RectTransform>(), new Vector2(1280f, -84f), new Vector2(1200f, 34f));
            subtitle.color = new Color32(166, 207, 211, 220);

            var cardContainer = new GameObject("HeroCards", typeof(GridLayoutGroup));
            cardContainer.transform.SetParent(screen.transform, false);
            var cardRect = cardContainer.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, -22f);
            cardRect.sizeDelta = new Vector2(2200f, 800f);

            var layout = cardContainer.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(700f, 780f);
            layout.spacing = new Vector2(50f, 20f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // 从配置数据动态读取英雄列表
            var heroes = ProphecyGameSession.Instance?.Data?.Heroes;
            if (heroes != null && heroes.Count > 0)
            {
                foreach (var hero in heroes)
                {
                    if (hero == null) continue;
                    var portraitPath = ResolveHeroPortraitPath(hero.id, hero.portrait_glyph);
                    CreateHeroCard(cardContainer.transform, hero.id, hero.name, hero.title,
                        portraitPath, hero.epithet, hero.passive_text, controller);
                }
            }
            else
            {
                // 回退硬编码数据
                var fallback = new[]
                {
                    ("james", "詹姆士", "增援统帅", "Art/hero/James.jpg", "让每一次获得数量更有效率", "经营阶段，我方任意已上阵部队获得数量时，额外获得+1数量。"),
                    ("magic", "马吉克", "离阵术士", "Art/hero/Magic.jpg", "把退场转化为新的战力", "经营阶段，我方已上阵部队出售并离场时，场上随机3个我方部队获得+1数量。"),
                    ("shalame", "夏拉美", "征募财务官", "Art/hero/Shirmmy.jpg", "从扩军中整理出预算", "经营阶段，我方已上阵部队每累计获得20数量，额外获得+1金币。"),
                };
                foreach (var (id, name, title, portraitPath, epithet, passiveText) in fallback)
                {
                    CreateHeroCard(cardContainer.transform, id, name, title, portraitPath, epithet, passiveText, controller);
                }
            }

            var footer = CreateText("SelectionHint", screen.transform, "稳定成长 · 出售运营 · 经济循环——选择最适合本局构筑方向的英雄", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetTitleCenteredRect(footer.GetComponent<RectTransform>(), new Vector2(1280f, -1182f), new Vector2(1700f, 46f));
            footer.color = new Color32(143, 176, 188, 190);

            return screen;
        }

        private static void CreateHeroCard(Transform parent, string heroId, string heroName, string heroTitle, string portraitPath, string epithet, string passiveText, RunSceneController controller)
        {
            GetHeroSelectionMeta(heroId, out var style, out var trigger, out var payoff, out var recommendation, out var accent);

            var card = CreatePanel("HeroCard_" + heroId, parent, accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var body = CreatePanel("CardBody", card.transform, new Color32(14, 27, 43, 255), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var accentLine = CreatePanel("AccentLine", body.transform, accent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(accentLine.GetComponent<RectTransform>(), 0f, 0f, 694f, 8f);
            accentLine.GetComponent<Image>().raycastTarget = false;

            var styleBadge = CreatePanel("StyleBadge", body.transform, new Color(accent.r, accent.g, accent.b, 0.25f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(styleBadge.GetComponent<RectTransform>(), 24f, 24f, 168f, 38f);
            var styleText = CreateText("Style", styleBadge.transform, style, 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            styleText.color = new Color32(243, 232, 194, 255);

            var portraitFrame = CreatePanel("PortraitFrame", body.transform, new Color(accent.r, accent.g, accent.b, 0.58f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(portraitFrame.GetComponent<RectTransform>(), 24f, 76f, 218f, 232f);
            var portrait = CreatePanel("Portrait", portraitFrame.transform, new Color32(24, 38, 54, 255), Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var portraitImage = portrait.GetComponent<Image>();
            ApplyHeroPortrait(portraitImage, heroId, portraitPath);
            portraitImage.preserveAspect = true;

            var nameText = CreateText("HeroName", body.transform, heroName, 36, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(nameText.GetComponent<RectTransform>(), 270f, 82f, 390f, 52f);
            nameText.color = new Color32(244, 211, 136, 255);

            var titleText = CreateText("HeroTitle", body.transform, heroTitle, 28, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(titleText.GetComponent<RectTransform>(), 270f, 137f, 390f, 40f);
            titleText.color = new Color32(183, 220, 217, 230);

            var epithetText = CreateText("Epithet", body.transform, epithet, 26, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(epithetText.GetComponent<RectTransform>(), 270f, 184f, 390f, 88f);
            epithetText.color = new Color32(166, 193, 202, 210);
            epithetText.fontStyle = FontStyle.Italic;
            epithetText.resizeTextForBestFit = true;
            epithetText.resizeTextMinSize = 26;
            epithetText.resizeTextMaxSize = 26;

            var abilityPanel = CreatePanel("AbilityPanel", body.transform, new Color32(7, 17, 29, 220), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(abilityPanel.GetComponent<RectTransform>(), 24f, 328f, 646f, 310f);

            var abilityTitle = CreateText("AbilityTitle", abilityPanel.transform, "英雄被动", 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(abilityTitle.GetComponent<RectTransform>(), 20f, 14f, 180f, 34f);
            abilityTitle.color = accent;

            var passive = CreateText("PassiveText", abilityPanel.transform, passiveText, 28, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(passive.GetComponent<RectTransform>(), 20f, 52f, 606f, 100f);
            passive.color = new Color32(225, 232, 235, 255);
            passive.resizeTextForBestFit = true;
            passive.resizeTextMinSize = 26;
            passive.resizeTextMaxSize = 28;

            var divider = CreatePanel("Divider", abilityPanel.transform, new Color32(103, 154, 166, 70), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(divider.GetComponent<RectTransform>(), 20f, 164f, 606f, 1f);
            divider.GetComponent<Image>().raycastTarget = false;

            var triggerText = CreateText("Trigger", abilityPanel.transform, $"触发   {trigger}", 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(triggerText.GetComponent<RectTransform>(), 20f, 178f, 606f, 40f);
            triggerText.color = new Color32(184, 207, 214, 255);

            var payoffText = CreateText("Payoff", abilityPanel.transform, $"收益   {payoff}", 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(payoffText.GetComponent<RectTransform>(), 20f, 224f, 606f, 40f);
            payoffText.color = new Color32(239, 204, 126, 255);

            var recommendationText = CreateText("Recommendation", body.transform, recommendation, 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            SetPixelRectTopLeft(recommendationText.GetComponent<RectTransform>(), 28f, 644f, 638f, 56f);
            recommendationText.color = new Color32(151, 185, 194, 220);
            recommendationText.resizeTextForBestFit = true;
            recommendationText.resizeTextMinSize = 26;
            recommendationText.resizeTextMaxSize = 26;

            CreateButton("SelectButton", body.transform, $"选择 {heroName}", Vector2.zero, new Vector2(370f, 58f), () => controller.StartRunWithHero(heroId));
            var selectButton = body.transform.Find("SelectButton");
            SetPixelRectTopLeft(selectButton.GetComponent<RectTransform>(), 162f, 706f, 370f, 58f);
            StylePrimaryButton(selectButton);
            SetHeroSelectionFontFloor(selectButton.Find("Label")?.GetComponent<Text>(), 28);
        }

        private static void SetHeroSelectionFontFloor(Text text, int preferredSize)
        {
            if (text == null) return;
            text.fontSize = Mathf.Max(26, preferredSize);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 26;
            text.resizeTextMaxSize = Mathf.Max(26, preferredSize);
        }

        internal static void ApplyHeroPortrait(Image image, string heroId, string configuredValue)
        {
            if (image == null) return;

            if (string.IsNullOrWhiteSpace(heroId))
            {
                image.sprite = null;
                image.color = Color.clear;
                return;
            }

            var path = ResolveHeroPortraitPath(heroId, configuredValue);
            if (!HeroPortraitSprites.TryGetValue(path, out var sprite))
            {
                image.sprite = null;
                ApplySpriteFromProjectPath(image, path);
                sprite = image.sprite;
                HeroPortraitSprites[path] = sprite;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = sprite != null ? Color.white : Color.clear;
        }

        private static string ResolveHeroPortraitPath(string heroId, string configuredValue)
        {
            switch (heroId)
            {
                case "james": return "Art/hero/James.jpg";
                case "magic": return "Art/hero/Magic.jpg";
                case "shalame": return "Art/hero/Shirmmy.jpg";
            }

            return !string.IsNullOrWhiteSpace(configuredValue) && (configuredValue.Contains("/") || configuredValue.Contains("\\"))
                ? configuredValue
                : $"Art/hero/{heroId}.jpg";
        }

        private static void GetHeroSelectionMeta(string heroId, out string style, out string trigger, out string payoff, out string recommendation, out Color accent)
        {
            switch (heroId)
            {
                case "james":
                    style = "稳定成长";
                    trigger = "已上阵部队每次获得数量";
                    payoff = "该部队额外获得 +1 数量";
                    recommendation = "适合持续强化核心部队，能力稳定且容易发挥";
                    accent = new Color32(205, 159, 72, 255);
                    return;
                case "magic":
                    style = "出售运营";
                    trigger = "出售一支已上阵部队";
                    payoff = "随机最多 3 支友军各获得 +1 数量";
                    recommendation = "适合频繁调整阵容，把出售节奏转化为场上战力";
                    accent = new Color32(132, 105, 190, 255);
                    return;
                case "shalame":
                    style = "经济循环";
                    trigger = "已上阵部队累计获得 20 数量";
                    payoff = "获得 +1 金币，超出进度继续保留";
                    recommendation = "适合规划数量成长，用额外金币滚动长期经济优势";
                    accent = new Color32(75, 166, 145, 255);
                    return;
                default:
                    style = "独特能力";
                    trigger = "满足英雄能力条件";
                    payoff = "获得对应的整局增益";
                    recommendation = "围绕英雄能力规划你的阵容与经营路线";
                    accent = new Color32(92, 174, 190, 255);
                    return;
            }
        }

        private static GameObject CreateSettingsModal(Transform parent)
        {
            var modal = CreatePanel("SettingsModal", parent, new Color32(5, 9, 18, 240), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400f, -300f), new Vector2(400f, 300f));

            var inner = CreatePanel("ModalInner", modal.transform, new Color32(12, 28, 45, 245), Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            var titleText = CreateText("ModalTitle", modal.transform, "设置", 36, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0f, -40f), new Vector2(0f, -90f));
            titleText.color = new Color32(239, 204, 126, 255);

            var volumeLabel = CreateText("VolumeLabel", modal.transform, "音量", 24, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(-280f, -100f), new Vector2(-80f, -160f));
            volumeLabel.color = new Color32(214, 225, 215, 224);

            var volumeSlider = CreateSlider("VolumeSlider", modal.transform, new Vector2(0.5f, 0.5f), new Vector2(400f, 40f));
            SetPixelRectTopLeft(volumeSlider.GetComponent<RectTransform>(), -180f, 230f, 400f, 40f);

            CreateButton("ConfirmButton", modal.transform, "确定", new Vector2(0f, -220f), new Vector2(180f, 56f), () => modal.SetActive(false));
            StylePrimaryButton(modal.transform.Find("ConfirmButton"));

            modal.SetActive(false);
            return modal;
        }

        private static GameObject CreateSlider(string name, Transform parent, Vector2 anchorPosition, Vector2 size)
        {
            var sliderObject = new GameObject(name, typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            var rect = sliderObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2((anchorPosition.x - 0.5f) * 2560f, (anchorPosition.y - 0.5f) * 1280f);

            var background = CreatePanel("Background", sliderObject.transform, new Color32(42, 58, 74, 255), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, 10f));

            var fillArea = CreatePanel("Fill Area", sliderObject.transform, Color.clear, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -8f), new Vector2(0f, 8f));
            var fill = CreatePanel("Fill", fillArea.transform, new Color32(187, 129, 42, 255), Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

            var handleArea = CreatePanel("Handle Slide Area", sliderObject.transform, Color.clear, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, -12f), new Vector2(-20f, 12f));
            var handle = CreatePanel("Handle", handleArea.transform, new Color32(239, 204, 126, 255), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16f, -16f), new Vector2(16f, 16f));

            var slider = sliderObject.GetComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat("ProphecyCentury.MasterVolume", 0.8f);
            AudioListener.volume = slider.value;
            slider.onValueChanged.AddListener(value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat("ProphecyCentury.MasterVolume", value);
                PlayerPrefs.Save();
            });

            return sliderObject;
        }

        private static void ShowSettingsModal(Transform parent)
        {
            var modal = parent.Find("SettingsModal");
            if (modal != null)
            {
                modal.gameObject.SetActive(true);
                modal.SetAsLastSibling();
            }
        }

        private static GameObject CreateCreditsModal(Transform parent)
        {
            var modal = CreatePanel("CreditsModal", parent, new Color32(5, 9, 18, 245), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -310f), new Vector2(420f, 310f));
            CreatePanel("Inner", modal.transform, new Color32(12, 28, 45, 250), Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var title = CreateText("Title", modal.transform, "预言世纪", 42, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.92f), Vector2.zero, Vector2.zero);
            title.color = new Color32(239, 204, 126, 255);
            var creditsAsset = Resources.Load<TextAsset>("Config/credits");
            var creditsText = creditsAsset != null ? creditsAsset.text : "制作组\n\n游戏设计 / 程序 / 美术 / 音频";
            var content = CreateText("CreditsText", modal.transform, creditsText, 24, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.27f), new Vector2(0.88f, 0.72f), Vector2.zero, Vector2.zero);
            content.color = new Color32(210, 224, 230, 255);
            CreateButton("BackButton", modal.transform, "返回主菜单", new Vector2(0f, -235f), new Vector2(220f, 58f), () => modal.SetActive(false));
            StylePrimaryButton(modal.transform.Find("BackButton"));
            return modal;
        }

        private static GameObject CreateLeaderboardModal(Transform parent)
        {
            var modal = CreatePanel("LeaderboardModal", parent, new Color32(5, 9, 18, 245), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -360f), new Vector2(560f, 360f));
            CreatePanel("Inner", modal.transform, new Color32(12, 28, 45, 250), Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var title = CreateText("Title", modal.transform, "排行榜", 42, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);
            title.color = new Color32(239, 204, 126, 255);

            var campaignTitle = CreateText("CampaignTitle", modal.transform, "命运征途", 30, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.75f), new Vector2(0.48f, 0.82f), Vector2.zero, Vector2.zero);
            campaignTitle.color = new Color32(255, 228, 151, 255);
            var casualTitle = CreateText("CasualTitle", modal.transform, "休闲对战", 30, TextAnchor.MiddleCenter, new Vector2(0.52f, 0.75f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
            casualTitle.color = new Color32(255, 228, 151, 255);

            var campaignList = CreateText("CampaignList", modal.transform, string.Empty, 24, TextAnchor.UpperLeft, new Vector2(0.09f, 0.19f), new Vector2(0.48f, 0.73f), Vector2.zero, Vector2.zero);
            campaignList.color = new Color32(220, 232, 238, 255);
            var casualList = CreateText("CasualList", modal.transform, string.Empty, 24, TextAnchor.UpperLeft, new Vector2(0.53f, 0.19f), new Vector2(0.92f, 0.73f), Vector2.zero, Vector2.zero);
            casualList.color = new Color32(220, 232, 238, 255);

            var hint = CreateText("Hint", modal.transform, "只记录本地成绩；排行榜按分数/战力从高到低排序", 20, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.17f), Vector2.zero, Vector2.zero);
            hint.color = new Color32(150, 199, 207, 220);
            CreateButton("BackButton", modal.transform, "返回主菜单", new Vector2(0f, -300f), new Vector2(220f, 58f), () => modal.SetActive(false));
            StylePrimaryButton(modal.transform.Find("BackButton"));
            RefreshLeaderboardModal(modal.transform);
            return modal;
        }

        private static void ShowLeaderboardModal(Transform parent)
        {
            var modal = FindDeepChild(parent, "LeaderboardModal");
            if (modal == null) return;
            RefreshLeaderboardModal(modal);
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
        }

        private static void RefreshLeaderboardModal(Transform modal)
        {
            var campaignList = FindDeepChild(modal, "CampaignList")?.GetComponent<Text>();
            if (campaignList != null) campaignList.text = LeaderboardSystem.FormatBoard(LeaderboardKind.Campaign, 10);
            var casualList = FindDeepChild(modal, "CasualList")?.GetComponent<Text>();
            if (casualList != null) casualList.text = LeaderboardSystem.FormatBoard(LeaderboardKind.CasualPvp, 10);
        }

        private static void ShowModal(Transform parent, string name)
        {
            var modal = FindDeepChild(parent, name);
            if (modal == null) return;
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
        }
    }
}
