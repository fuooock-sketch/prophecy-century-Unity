using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class CasualPvpUiController : MonoBehaviour
    {
        private readonly List<SaveSlotView> _slotViews = new List<SaveSlotView>();
        private RunSceneController _runController;
        private SaveSlotMenuController _campaignSaveMenu;
        private CasualPvpSaveSlotService _saveService;
        private GameObject _titlePanel;
        private GameObject _modeScreen;
        private GameObject _slotScreen;
        private GameObject _introScreen;
        private GameObject _overlay;
        private Text _overlayTitle;
        private Text _overlayContent;
        private Button _overlayLeftButton;
        private Button _overlayRightButton;
        private Button _continueButton;
        private Text _slotStatus;
        private Button _manageButton;
        private bool _managementMode;
        private ConfirmDialog _dialog;

        public void Initialize(RunSceneController runController, GameObject titlePanel, SaveSlotMenuController campaignSaveMenu, Button continueButton)
        {
            _runController = runController;
            _titlePanel = titlePanel;
            _campaignSaveMenu = campaignSaveMenu;
            _continueButton = continueButton;
            _saveService = new CasualPvpSaveSlotService();
            if (_campaignSaveMenu != null) _campaignSaveMenu.ReturnOverride = OpenModeSelection;
            _dialog = ConfirmDialog.FindOrCreate(transform);
            BuildModeScreen();
            BuildSlotScreen();
            BuildIntroScreen();
            BuildOverlay();
            WireTitleButtons();
            RefreshMainMenu();
        }

        public void OpenModeSelection()
        {
            HideNavigationScreens();
            _modeScreen.SetActive(true);
            _modeScreen.transform.SetAsLastSibling();
        }

        public void RewireTitleButtons()
        {
            WireTitleButtons();
            RefreshMainMenu();
        }

        public void ShowIntro()
        {
            HideNavigationScreens();
            _introScreen.SetActive(true);
            _introScreen.transform.SetAsLastSibling();
        }

        public void ReturnFromHeroSelection()
        {
            _runController.HideHeroSelectionScreen();
            ShowIntro();
        }

        public void HideForRun()
        {
            _modeScreen?.SetActive(false);
            _slotScreen?.SetActive(false);
            _introScreen?.SetActive(false);
            HideOverlay();
        }

        public void RefreshMainMenu()
        {
            if (_continueButton == null) return;
            var campaign = _campaignSaveMenu?.Service?.GetMostRecentlyPlayedSave();
            var casual = _saveService?.GetMostRecentlyPlayedSave();
            var hasAny = campaign != null || casual != null;
            _continueButton.interactable = hasAny;
            var label = _continueButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = casual != null && (campaign == null || SavedAt(casual) >= SavedAt(campaign))
                    ? "继续游戏 · 休闲对战"
                    : "继续游戏";
                label.color = hasAny ? new Color32(255, 243, 200, 255) : new Color32(120, 120, 120, 180);
            }
        }

        public void ShowMatchProgress(string message)
        {
            ShowOverlay("正在寻找命运镜像", message, null, null, null, null);
        }

        public void ShowRecoveryRetry(string message, Action retry)
        {
            ShowOverlay("对战进度尚未保存", message, "重试", retry, null, null);
        }

        public void ShowOpponentReveal(CasualPvpSnapshotState snapshot, bool fallback, Action startBattle)
        {
            if (snapshot == null)
            {
                ShowMatchProgress("正在启用同回合系统保底……");
                return;
            }
            var units = string.Join("\n", snapshot.units
                .OrderBy(unit => unit.slotId)
                .Select(unit => $"{unit.slotId}  {unit.name}{(unit.isGolden ? " · 金色" : "")}  ★{Math.Max(1, unit.star)}  数量 {Math.Max(1, unit.count)}"));
            var source = fallback || snapshot.sourceType == "system_fallback" ? "系统保底" : "玩家镜像";
            ShowOverlay(
                $"已找到第 {snapshot.round} 回合对手",
                $"{snapshot.displayName ?? "训练镜像"}  ·  {source}\n战力 {Math.Max(0, snapshot.powerScore)}\n\n{units}\n\n本方阵容已锁定，无法返回经营。",
                "开始战斗", startBattle, null, null);
        }

        public void ShowMilestone(Action finish, Action continueEndless)
        {
            var run = ProphecyCentury.Core.ProphecyGameSession.Instance?.CurrentRun;
            ShowOverlay(
                "你已度过十五轮交锋",
                $"当前生命 {Math.Max(0, run?.playerHp ?? 0)} / 100\n战绩 {Math.Max(0, run?.campaignWins ?? 0)}胜{Math.Max(0, run?.campaignLosses ?? 0)}负\n\n你可以带着当前成绩结束本局，\n或进入无尽挑战，直到生命归零。",
                "结束本局", finish,
                "继续挑战", () => _dialog.Show("进入无尽挑战", "进入后，本局将持续到生命归零。确定继续吗？", continueEndless));
        }

        public void HideOverlay()
        {
            if (_overlay != null) _overlay.SetActive(false);
        }

        private void WireTitleButtons()
        {
            var start = FindDeepChild(_titlePanel?.transform, "StartGameButton")?.GetComponent<Button>();
            if (start != null)
            {
                start.onClick.RemoveAllListeners();
                start.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                start.onClick.AddListener(OpenModeSelection);
                var label = start.GetComponentInChildren<Text>();
                if (label != null) label.text = "开始游戏";
            }
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                _continueButton.onClick.AddListener(ContinueLatest);
            }
        }

        private void ContinueLatest()
        {
            var campaign = _campaignSaveMenu?.Service?.GetMostRecentlyPlayedSave();
            var casual = _saveService.GetMostRecentlyPlayedSave();
            SaveOperationResult result;
            if (casual != null && (campaign == null || SavedAt(casual) >= SavedAt(campaign))) result = _saveService.LoadGame(casual.SlotIndex);
            else result = _campaignSaveMenu?.Service?.LoadMostRecentValid() ?? SaveOperationResult.Fail("没有可读取的存档。");
            if (!result.Success)
            {
                _dialog.Show("读取失败", result.Error, null);
                RefreshMainMenu();
                return;
            }
            HideNavigationScreens();
            _runController.EnterLoadedRun();
        }

        private void BuildModeScreen()
        {
            _modeScreen = CreateFullScreen("GameModeSelectionScreen");
            CreateText("Title", _modeScreen.transform, "选择预言的方式", 54, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.87f), new Vector2(0.75f, 0.97f), new Color32(239, 204, 126, 255));
            CreateText("Subtitle", _modeScreen.transform, "踏入既定征途，或与其他预言者留下的镜像交锋", 24, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.81f), new Vector2(0.8f, 0.88f), new Color32(156, 205, 211, 255));
            CreateModeCard("CampaignCard", new Vector2(0.12f, 0.25f), new Vector2(0.48f, 0.78f),
                "命运征途", "探索地图、挑战敌人\n完成一段完整战役", "选择战役",
                "Resources/Art/Mode/mode_campaign_adventure.jpeg", () =>
            {
                _modeScreen.SetActive(false);
                _campaignSaveMenu.OpenNewGame();
            });
            CreateModeCard("CasualCard", new Vector2(0.52f, 0.25f), new Vector2(0.88f, 0.78f),
                "休闲对战", "异步镜像对战\n100 初始生命 · 15 回合里程碑\n之后可进入无尽挑战", "进入模式",
                "Resources/Art/Mode/mode_casual_battle.jpeg", OpenCasualSlots);
            CreateButton("Back", _modeScreen.transform, "返回主菜单", new Vector2(0.42f, 0.08f), new Vector2(0.58f, 0.15f), ReturnToTitle);
            _modeScreen.SetActive(false);
        }

        private void CreateModeCard(string name, Vector2 min, Vector2 max, string title, string description, string action, string backgroundPath, UnityEngine.Events.UnityAction callback)
        {
            var card = CreatePanel(name, _modeScreen.transform, min, max, new Color32(14, 28, 46, 248));
            var background = CreatePanel("Background", card.transform, Vector2.zero, Vector2.one, Color.white);
            ApplySpriteFromProjectPath(background.GetComponent<Image>(), backgroundPath);
            background.GetComponent<Image>().raycastTarget = false;

            var shade = CreatePanel("Shade", card.transform, Vector2.zero, Vector2.one, new Color32(3, 8, 16, 132));
            shade.GetComponent<Image>().raycastTarget = false;

            var lowerShade = CreatePanel("LowerShade", card.transform, new Vector2(0f, 0f), new Vector2(1f, 0.62f), new Color32(3, 7, 14, 178));
            lowerShade.GetComponent<Image>().raycastTarget = false;

            var rim = CreatePanel("Rim", card.transform, Vector2.zero, Vector2.one, new Color32(239, 204, 126, 68));
            rim.GetComponent<Image>().raycastTarget = false;

            CreateText("Title", card.transform, title, 46, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.86f), new Color32(255, 228, 151, 255));
            CreateText("Description", card.transform, description, 27, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.61f), new Color32(230, 240, 242, 255));
            CreateButton("Select", card.transform, action, new Vector2(0.28f, 0.09f), new Vector2(0.72f, 0.23f), callback);
        }

        private void BuildSlotScreen()
        {
            _slotScreen = CreateFullScreen("CasualPvpSaveSlotScreen");
            CreateText("Title", _slotScreen.transform, "休闲对战存档", 52, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.87f), new Vector2(0.75f, 0.97f), new Color32(239, 204, 126, 255));
            CreateText("Subtitle", _slotScreen.transform, "三个独立槽位，不会占用战役存档", 22, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.81f), new Vector2(0.75f, 0.87f), new Color32(150, 199, 207, 255));
            var gridObject = new GameObject("SlotGrid", typeof(GridLayoutGroup));
            gridObject.transform.SetParent(_slotScreen.transform, false);
            var rect = gridObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.31f);
            rect.anchorMax = new Vector2(0.92f, 0.75f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(580f, 230f);
            grid.spacing = new Vector2(42f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;
            for (var i = 0; i < CasualPvpSaveSlotService.SlotCount; i += 1) _slotViews.Add(SaveSlotView.Create(gridObject.transform));
            _slotStatus = CreateText("Status", _slotScreen.transform, string.Empty, 22, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.27f), Color.white);
            _manageButton = CreateButton("Manage", _slotScreen.transform, "管理存档", new Vector2(0.32f, 0.08f), new Vector2(0.47f, 0.15f), ToggleManagement);
            CreateButton("Back", _slotScreen.transform, "返回玩法选择", new Vector2(0.53f, 0.08f), new Vector2(0.68f, 0.15f), OpenModeSelection);
            _slotScreen.SetActive(false);
        }

        public void OpenCasualSlots()
        {
            HideNavigationScreens();
            _slotScreen.SetActive(true);
            _slotScreen.transform.SetAsLastSibling();
            _managementMode = false;
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            var slots = _saveService.GetAllSlots();
            for (var i = 0; i < slots.Count; i += 1)
            {
                var info = slots[i];
                var slotIndex = info.SlotIndex;
                _slotViews[i].Bind(info, _managementMode, () => SelectSlot(slotIndex), () => ConfirmDelete(slotIndex), true);
            }
            _manageButton.GetComponentInChildren<Text>().text = _managementMode ? "返回选择" : "管理存档";
            _slotStatus.text = _managementMode ? "管理模式：可以查看或删除休闲对战存档" : "选择已有进度继续，或选择空槽开始新局";
        }

        private void SelectSlot(int slotIndex)
        {
            var info = _saveService.GetSlot(slotIndex);
            if (_managementMode)
            {
                if (info.State == SaveSlotState.Valid) _dialog.Show($"休闲存档 {slotIndex}", info.Metadata?.summary ?? "休闲对战进度", null);
                return;
            }
            if (info.State == SaveSlotState.Valid)
            {
                var loaded = _saveService.LoadGame(slotIndex);
                if (!loaded.Success) _dialog.Show("读取失败", loaded.Error, null);
                else
                {
                    HideNavigationScreens();
                    _runController.EnterLoadedRun();
                }
                return;
            }
            if (info.State != SaveSlotState.Empty) return;
            _dialog.Show("开始休闲对战", $"是否在休闲存档 {slotIndex} 开始新局？", () => CreateCasualRun(slotIndex));
        }

        private void CreateCasualRun(int slotIndex)
        {
            var result = _saveService.CreateNewGame(slotIndex);
            if (!result.Success)
            {
                _dialog.Show("创建失败", result.Error, null);
                RefreshSlots();
                return;
            }
            _runController.BeginCasualPvpSetup();
            ShowIntro();
        }

        private void ConfirmDelete(int slotIndex)
        {
            _dialog.Show("删除休闲对战存档", $"确定删除休闲存档 {slotIndex} 吗？删除后无法恢复。", () =>
            {
                var result = _saveService.DeleteSave(slotIndex);
                if (!result.Success) _dialog.Show("删除失败", result.Error, null);
                RefreshSlots();
                RefreshMainMenu();
            });
        }

        private void ToggleManagement()
        {
            _managementMode = !_managementMode;
            RefreshSlots();
        }

        private void BuildIntroScreen()
        {
            _introScreen = CreateFullScreen("CasualPvpIntroScreen");
            CreateText("Title", _introScreen.transform, "休闲对战", 56, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.97f), new Color32(239, 204, 126, 255));
            var rules = "初始生命　100\n生存里程碑　第15回合\n失败　沿用当前公式扣除生命\n对手　相同回合玩家镜像\n\n第15回合后可结束本局，或进入无尽挑战直到生命归零。\n对手是历史阵容镜像，并非真人实时操作。\n锁定阵容前不会展示对手。";
            CreateText("Rules", _introScreen.transform, rules, 28, TextAnchor.MiddleLeft, new Vector2(0.18f, 0.3f), new Vector2(0.62f, 0.8f), new Color32(220, 229, 235, 255));
            CreateText("PoolStatus", _introScreen.transform, "● 本地镜像库\n\n连接失败时自动使用\n同回合系统保底", 25, TextAnchor.MiddleCenter, new Vector2(0.65f, 0.42f), new Vector2(0.85f, 0.72f), new Color32(127, 215, 211, 255));
            CreateButton("Start", _introScreen.transform, "选择英雄并开始", new Vector2(0.41f, 0.14f), new Vector2(0.59f, 0.22f), () => _runController.OpenCasualHeroSelection());
            CreateButton("Back", _introScreen.transform, "返回存档", new Vector2(0.05f, 0.88f), new Vector2(0.14f, 0.94f), OpenCasualSlots);
            _introScreen.SetActive(false);
        }

        private void BuildOverlay()
        {
            _overlay = CreateFullScreen("CasualPvpOverlay", new Color32(3, 7, 14, 235));
            var panel = CreatePanel("Panel", _overlay.transform, new Vector2(0.25f, 0.18f), new Vector2(0.75f, 0.82f), new Color32(15, 32, 50, 252));
            _overlayTitle = CreateText("Title", panel.transform, string.Empty, 42, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), new Color32(239, 204, 126, 255));
            _overlayContent = CreateText("Content", panel.transform, string.Empty, 24, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.76f), new Color32(220, 232, 238, 255));
            _overlayLeftButton = CreateButton("Left", panel.transform, string.Empty, new Vector2(0.16f, 0.07f), new Vector2(0.46f, 0.18f), null);
            _overlayRightButton = CreateButton("Right", panel.transform, string.Empty, new Vector2(0.54f, 0.07f), new Vector2(0.84f, 0.18f), null);
            _overlay.SetActive(false);
        }

        private void ShowOverlay(string title, string content, string leftLabel, Action left, string rightLabel, Action right)
        {
            _overlayTitle.text = title;
            _overlayContent.text = content;
            ConfigureOverlayButton(_overlayLeftButton, leftLabel, left);
            ConfigureOverlayButton(_overlayRightButton, rightLabel, right);
            _overlay.SetActive(true);
            _overlay.transform.SetAsLastSibling();
        }

        private static void ConfigureOverlayButton(Button button, string label, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(!string.IsNullOrWhiteSpace(label));
            if (!button.gameObject.activeSelf) return;
            button.GetComponentInChildren<Text>().text = label;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            if (action != null) button.onClick.AddListener(() => action());
        }

        private void ReturnToTitle()
        {
            HideNavigationScreens();
            _titlePanel?.SetActive(true);
            _titlePanel?.transform.SetAsLastSibling();
            RefreshMainMenu();
        }

        private void HideNavigationScreens()
        {
            _titlePanel?.SetActive(false);
            _modeScreen?.SetActive(false);
            _slotScreen?.SetActive(false);
            _introScreen?.SetActive(false);
        }

        private GameObject CreateFullScreen(string name, Color? color = null)
        {
            return CreatePanel(name, transform, Vector2.zero, Vector2.one, color ?? new Color32(5, 9, 18, 255));
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var panel = new GameObject(name, typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Vector2 min, Vector2 max, Color color)
        {
            var obj = new GameObject(name, typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction callback)
        {
            var obj = new GameObject(name, typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color32(45, 91, 105, 255);
            var button = obj.GetComponent<Button>();
            if (callback != null)
            {
                button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                button.onClick.AddListener(callback);
            }
            CreateText("Label", obj.transform, label, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Color.white);
            return button;
        }

        private static void ApplySpriteFromProjectPath(Image image, string relativeAssetPath)
        {
            if (image == null || string.IsNullOrWhiteSpace(relativeAssetPath)) return;
            var fullPath = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(fullPath)) return;

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes)) return;

            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 2;
            texture.wrapMode = TextureWrapMode.Clamp;
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static DateTime SavedAt(SaveSlotInfo info)
        {
            return DateTime.TryParse(info?.Metadata?.lastSavedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }
}
