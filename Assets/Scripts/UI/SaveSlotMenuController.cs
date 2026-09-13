using System;
using System.Collections.Generic;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class SaveSlotMenuController : MonoBehaviour
    {
        private readonly List<SaveSlotView> _views = new List<SaveSlotView>();
        private SaveSlotService _service;
        private RunSceneController _runController;
        private GameObject _titlePanel;
        private GameObject _screen;
        private Text _modeLabel;
        private Text _statusLabel;
        private Button _manageButton;
        private Button _continueButton;
        private ConfirmDialog _dialog;
        private bool _managementMode;
        private bool _busy;

        public SaveSlotService Service => _service;
        public Action ReturnOverride { get; set; }

        public void Initialize(RunSceneController runController, GameObject titlePanel, Button continueButton)
        {
            _runController = runController;
            _titlePanel = titlePanel;
            _continueButton = continueButton;
            _service = new SaveSlotService();
            _service.ImportLegacySave(new SaveGameSystem().LegacySavePath);
            _dialog = ConfirmDialog.FindOrCreate(transform);
            BuildScreen();
            WireMainMenuButtons();
            RefreshMainMenu();
        }

        private void WireMainMenuButtons()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
                _continueButton.onClick.AddListener(ContinueLatest);
            }
            var newGameTransform = _titlePanel != null ? FindDeepChild(_titlePanel.transform, "StartGameButton") : null;
            var newGame = newGameTransform != null ? newGameTransform.GetComponent<Button>() : null;
            if (newGame == null) return;
            newGame.onClick.RemoveAllListeners();
            newGame.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            newGame.onClick.AddListener(OpenNewGame);
            var label = newGame.GetComponentInChildren<Text>();
            if (label != null) label.text = "新游戏";
        }

        public void OpenNewGame()
        {
            if (_busy) return;
            _managementMode = false;
            _titlePanel?.SetActive(false);
            _screen.SetActive(true);
            _screen.transform.SetAsLastSibling();
            RefreshSlots();
        }

        public void ContinueLatest()
        {
            if (_busy || !_service.HasAnyValidSave()) return;
            _busy = true;
            var result = _service.LoadMostRecentValid();
            _busy = false;
            if (!result.Success)
            {
                RefreshMainMenu();
                ShowMessage("读取失败", result.Error);
                return;
            }
            _runController.EnterLoadedRun();
        }

        public void ReturnToMainMenu()
        {
            _managementMode = false;
            _busy = false;
            _screen.SetActive(false);
            if (ReturnOverride != null)
            {
                ReturnOverride();
                return;
            }
            _titlePanel?.SetActive(true);
            _titlePanel?.transform.SetAsLastSibling();
            RefreshMainMenu();
        }

        public void RefreshMainMenu()
        {
            if (_continueButton == null || _service == null) return;
            _continueButton.interactable = _service.HasAnyValidSave() && !_busy;
            var label = _continueButton.GetComponentInChildren<Text>();
            if (label != null) label.color = _continueButton.interactable ? new Color32(255, 243, 200, 255) : new Color32(120, 120, 120, 180);
        }

        private void BuildScreen()
        {
            _screen = new GameObject("SaveSlotScreen", typeof(Image));
            _screen.transform.SetParent(transform, false);
            var rect = _screen.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _screen.GetComponent<Image>().color = new Color32(5, 9, 18, 255);

            CreateText("Title", _screen.transform, "选择存档", 48, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.9f), new Vector2(0.75f, 0.98f));
            _modeLabel = CreateText("Mode", _screen.transform, string.Empty, 22, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.84f), new Vector2(0.8f, 0.9f));

            var gridObject = new GameObject("SlotGrid", typeof(GridLayoutGroup));
            gridObject.transform.SetParent(_screen.transform, false);
            var gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.08f, 0.19f);
            gridRect.anchorMax = new Vector2(0.92f, 0.83f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(500f, 190f);
            grid.spacing = new Vector2(28f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.MiddleCenter;
            for (var i = 0; i < SaveSlotService.SlotCount; i += 1) _views.Add(SaveSlotView.Create(gridObject.transform));

            _statusLabel = CreateText("Status", _screen.transform, string.Empty, 20, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.18f));
            _manageButton = CreateButton("Manage", _screen.transform, "管理存档", new Vector2(0.37f, 0.035f), new Vector2(0.49f, 0.105f));
            _manageButton.onClick.AddListener(ToggleManagement);
            var back = CreateButton("Back", _screen.transform, "返回主菜单", new Vector2(0.51f, 0.035f), new Vector2(0.63f, 0.105f));
            back.onClick.AddListener(ReturnToMainMenu);
            _screen.SetActive(false);
        }

        private void ToggleManagement()
        {
            if (_busy) return;
            _managementMode = !_managementMode;
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            var slots = _service.GetAllSlots();
            var validCount = 0;
            var emptyCount = 0;
            for (var i = 0; i < slots.Count; i += 1)
            {
                var slot = slots[i];
                if (slot.State == SaveSlotState.Valid) validCount += 1;
                if (slot.State == SaveSlotState.Empty) emptyCount += 1;
                var index = slot.SlotIndex;
                _views[i].Bind(slot, _managementMode, () => OnSlotSelected(index), () => ConfirmDelete(index, slot.State));
            }
            _modeLabel.text = _managementMode ? "管理模式：查看存档概要或删除存档" : "新游戏模式：请选择一个空白槽位";
            _manageButton.GetComponentInChildren<Text>().text = _managementMode ? "返回选择" : "管理存档";
            _statusLabel.text = !_managementMode && emptyCount == 0
                ? "没有可用的空白存档位置，请先删除一个已有存档。"
                : $"有效存档 {validCount} / {SaveSlotService.SlotCount}";
        }

        private void OnSlotSelected(int slotIndex)
        {
            var slot = _service.GetSlot(slotIndex);
            if (_managementMode)
            {
                if (slot.State == SaveSlotState.Valid) ShowDetails(slot);
                return;
            }
            if (slot.State != SaveSlotState.Empty || _busy) return;
            _dialog.Show("开始新游戏", $"是否在存档 {slotIndex} 开始新游戏？", () => CreateNewGame(slotIndex));
        }

        private void CreateNewGame(int slotIndex)
        {
            if (_busy) return;
            _busy = true;
            var result = _service.CreateNewGame(slotIndex);
            _busy = false;
            if (!result.Success)
            {
                RefreshSlots();
                ShowMessage("创建失败", result.Error);
                return;
            }
            _screen.SetActive(false);
            _runController.OpenCampaignSelection();
        }

        private void ConfirmDelete(int slotIndex, SaveSlotState state)
        {
            var content = state == SaveSlotState.Corrupted
                ? $"该存档可能已经损坏。确定删除存档 {slotIndex} 吗？删除后无法恢复。"
                : $"确定删除存档 {slotIndex} 吗？删除后无法恢复。";
            _dialog.Show("删除存档", content, () => DeleteSlot(slotIndex));
        }

        private void DeleteSlot(int slotIndex)
        {
            var result = _service.DeleteSave(slotIndex);
            if (!result.Success) ShowMessage("删除失败", result.Error);
            RefreshSlots();
            RefreshMainMenu();
        }

        private void ShowDetails(SaveSlotInfo slot)
        {
            var metadata = slot.Metadata;
            ShowMessage($"存档 {slot.SlotIndex}", $"{metadata.summary}\n战役：{metadata.chapter}\n地图：{metadata.mapOrLevel}\n游戏版本：{metadata.gameVersion}\n存档 ID：{metadata.saveId}");
        }

        private void ShowMessage(string title, string content)
        {
            _dialog.Show(title, content, null);
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Vector2 min, Vector2 max)
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
            text.color = new Color32(225, 230, 235, 255);
            text.text = value;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 min, Vector2 max)
        {
            var obj = new GameObject(name, typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color32(39, 78, 91, 255);
            CreateText("Label", obj.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return obj.GetComponent<Button>();
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var match = FindDeepChild(child, name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
