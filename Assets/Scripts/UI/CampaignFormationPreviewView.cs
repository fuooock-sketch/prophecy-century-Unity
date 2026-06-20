using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class CampaignFormationPreviewView : MonoBehaviour
    {
        private readonly Dictionary<string, RectTransform> _slotRects = new Dictionary<string, RectTransform>();
        private readonly List<CampaignFormationPreviewRound> _rounds = new List<CampaignFormationPreviewRound>();
        private RunSceneController _controller;
        private Transform _boardRoot;
        private Text _titleText;
        private Text _roundText;
        private Text _sourceText;
        private Text _scoreText;
        private Text _emptyText;
        private Button _previousButton;
        private Button _nextButton;
        private int _roundIndex;
        private int _difficultyScore;

        public void Build(RunSceneController controller)
        {
            _controller = controller;
            ClearChildren(transform);

            CreateTitleLine(transform, "TopLine", new Vector2(1280f, -80f), new Vector2(2200f, 2f), new Color32(92, 188, 200, 86));
            CreateButton("BackToCampaignListButton", transform, "返回列表", new Vector2(120f, -40f), new Vector2(160f, 48f), () => _controller.ReturnToCampaignFromFormationPreview(), false);

            _titleText = CreateText("PreviewTitle", transform, "阵型预览", 40, TextAnchor.MiddleCenter);
            SetCentered(_titleText.rectTransform, new Vector2(1280f, -42f), new Vector2(760f, 58f));
            _titleText.color = new Color32(239, 204, 126, 255);

            _roundText = CreateText("RoundLabel", transform, string.Empty, 30, TextAnchor.MiddleCenter);
            SetCentered(_roundText.rectTransform, new Vector2(1280f, -126f), new Vector2(540f, 46f));
            _roundText.color = new Color32(154, 226, 255, 255);

            _sourceText = CreateText("SourceLabel", transform, string.Empty, 20, TextAnchor.MiddleCenter);
            SetCentered(_sourceText.rectTransform, new Vector2(1280f, -168f), new Vector2(900f, 34f));
            _sourceText.color = new Color32(205, 218, 224, 235);

            _scoreText = CreateText("DifficultyLabel", transform, string.Empty, 22, TextAnchor.MiddleCenter);
            SetCentered(_scoreText.rectTransform, new Vector2(1280f, -198f), new Vector2(760f, 34f));
            _scoreText.color = new Color32(239, 204, 126, 255);

            var boardPanel = CreatePanel("FormationBoardPanel", transform, new Color32(9, 18, 31, 245));
            SetTopLeft(boardPanel.GetComponent<RectTransform>(), 610f, 235f, 1340f, 830f);
            _boardRoot = boardPanel.transform;

            CreateBoardSlots(_boardRoot);

            _emptyText = CreateText("EmptyFormationLabel", boardPanel.transform, "暂无可预览阵型", 30, TextAnchor.MiddleCenter);
            SetCentered(_emptyText.rectTransform, new Vector2(670f, -410f), new Vector2(600f, 80f));
            _emptyText.color = new Color32(205, 218, 224, 210);

            _previousButton = CreateButton("PreviousRoundButton", transform, "上一回合", new Vector2(790f, -1110f), new Vector2(180f, 52f), PreviousRound, false);
            _nextButton = CreateButton("NextRoundButton", transform, "下一回合", new Vector2(1590f, -1110f), new Vector2(180f, 52f), NextRound, false);
        }

        public void ShowCampaign(string campaignId)
        {
            if (_titleText == null || _boardRoot == null)
            {
                Build(_controller);
            }

            _rounds.Clear();
            var summary = CampaignFormationPreviewSystem.BuildPreviewSummary(campaignId);
            _rounds.AddRange(summary.Rounds);
            _difficultyScore = summary.DifficultyScore;
            _roundIndex = 0;

            var data = ProphecyGameSession.Instance?.Data;
            var campaign = data?.FindCampaign(campaignId);
            _titleText.text = campaign?.name
                ?? CustomChallengeSystem.LoadAll().FirstOrDefault(item => item != null && item.id == campaignId)?.name
                ?? campaignId
                ?? "阵型预览";

            RefreshRound();
        }

        public void PreviousRound()
        {
            if (_roundIndex <= 0)
            {
                return;
            }

            _roundIndex -= 1;
            RefreshRound();
        }

        public void NextRound()
        {
            if (_roundIndex >= _rounds.Count - 1)
            {
                return;
            }

            _roundIndex += 1;
            RefreshRound();
        }

        public void RefreshRound()
        {
            ClearFormationUnits();
            var hasRound = _rounds.Count > 0 && _roundIndex >= 0 && _roundIndex < _rounds.Count;
            _emptyText.gameObject.SetActive(!hasRound);
            _previousButton.interactable = hasRound && _roundIndex > 0;
            _nextButton.interactable = hasRound && _roundIndex < _rounds.Count - 1;

            if (!hasRound)
            {
                _roundText.text = "无明确配置阵型";
                _sourceText.text = "该关卡没有记录或配置过的固定阵型";
                _scoreText.text = "关卡难度：未知";
                return;
            }

            var round = _rounds[_roundIndex];
            _roundText.text = $"第 {round.Round} 回合 / 共 {_rounds.Count} 回合";
            _sourceText.text = string.IsNullOrWhiteSpace(round.SourceName) ? string.Empty : round.SourceName;
            _scoreText.text = $"关卡难度 {_difficultyScore} / 本回合强度 {round.RoundScore}";

            foreach (var unit in round.Units)
            {
                CreateFormationUnit(unit);
            }
        }

        private void CreateBoardSlots(Transform parent)
        {
            _slotRects.Clear();
            var slots = new[]
            {
                new PreviewSlot("4-1", 100f, 86f),
                new PreviewSlot("4-2", 100f, 254f),
                new PreviewSlot("4-3", 100f, 422f),
                new PreviewSlot("4-4", 100f, 590f),
                new PreviewSlot("3-1", 382f, 170f),
                new PreviewSlot("3-2", 382f, 338f),
                new PreviewSlot("3-3", 382f, 506f),
                new PreviewSlot("2-1", 664f, 254f),
                new PreviewSlot("2-2", 664f, 422f),
                new PreviewSlot("1-1", 946f, 338f)
            };

            foreach (var slot in slots)
            {
                var cell = CreatePanel("BoardSlot_" + slot.Id, parent, new Color32(36, 48, 62, 220));
                SetTopLeft(cell.GetComponent<RectTransform>(), slot.Left, slot.Top, 138f, 138f);
                var label = CreateText("SlotLabel", cell.transform, slot.Id, 18, TextAnchor.LowerCenter);
                label.color = new Color32(170, 190, 202, 170);
                SetStretch(label.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                _slotRects[slot.Id] = cell.GetComponent<RectTransform>();
            }
        }

        private void CreateFormationUnit(CampaignFormationPreviewUnit unit)
        {
            var slotId = string.IsNullOrWhiteSpace(unit.SlotId) ? "1-1" : unit.SlotId;
            if (!_slotRects.TryGetValue(slotId, out var slotRect))
            {
                slotRect = _slotRects["1-1"];
            }

            var unitObject = CreatePanel("FormationUnit_" + unit.UnitId, slotRect, unit.IsGolden ? new Color32(142, 104, 30, 245) : new Color32(52, 78, 100, 245));
            SetStretch(unitObject.GetComponent<RectTransform>(), new Vector2(8f, 8f), new Vector2(-8f, -8f));

            var iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(unitObject.transform, false);
            var iconImage = iconObject.GetComponent<Image>();
            RuntimeUnitIconCache.ApplyTo(iconImage, unit.Name);
            iconImage.raycastTarget = false;
            SetTopLeft(iconImage.rectTransform, 20f, 14f, 82f, 76f);

            var nameBack = CreatePanel("NameBack", unitObject.transform, new Color32(10, 16, 22, 210));
            SetTopLeft(nameBack.GetComponent<RectTransform>(), 6f, 82f, 110f, 30f);

            var name = CreateText("Name", unitObject.transform, unit.Name, 15, TextAnchor.MiddleCenter);
            name.color = Color.white;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 10;
            name.resizeTextMaxSize = 15;
            SetTopLeft(name.rectTransform, 8f, 84f, 106f, 26f);

            var starBack = CreatePanel("StarBack", unitObject.transform, new Color32(24, 16, 12, 225));
            SetTopLeft(starBack.GetComponent<RectTransform>(), 6f, 6f, 42f, 24f);

            var star = CreateText("Star", unitObject.transform, $"★{unit.Star}", 16, TextAnchor.MiddleLeft);
            star.color = new Color32(239, 204, 126, 255);
            SetTopLeft(star.rectTransform, 10f, 6f, 38f, 24f);

            var countBack = CreatePanel("CountBack", unitObject.transform, new Color32(12, 28, 39, 230));
            SetTopLeft(countBack.GetComponent<RectTransform>(), 66f, 6f, 50f, 30f);

            var count = CreateText("Count", unitObject.transform, unit.Count.ToString(), 24, TextAnchor.MiddleRight);
            count.color = new Color32(154, 226, 255, 255);
            count.fontStyle = FontStyle.Bold;
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = 14;
            count.resizeTextMaxSize = 24;
            SetTopLeft(count.rectTransform, 68f, 5f, 46f, 30f);

            var tooltip = unitObject.AddComponent<RuntimeUnitTooltip>();
            tooltip.Unit = new UnitCardState
            {
                unitId = unit.UnitId,
                name = unit.Name,
                star = unit.Star,
                isGolden = unit.IsGolden,
                baseCount = unit.Count,
                maxCount = 0
            };
        }

        private void ClearFormationUnits()
        {
            foreach (var slot in _slotRects.Values)
            {
                for (var i = slot.childCount - 1; i >= 0; i -= 1)
                {
                    var child = slot.GetChild(i);
                    if (child != null && child.name.StartsWith("FormationUnit_"))
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = color;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 center, Vector2 size, UnityEngine.Events.UnityAction callback, bool primary)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            SetCentered(buttonObject.GetComponent<RectTransform>(), center, size);
            var image = buttonObject.GetComponent<Image>();
            image.color = primary ? new Color32(158, 105, 38, 255) : new Color32(31, 55, 69, 230);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(callback);

            var text = CreateText("Label", buttonObject.transform, label, 20, TextAnchor.MiddleCenter);
            text.color = Color.white;
            SetStretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void CreateTitleLine(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            var line = CreatePanel(name, parent, color);
            SetCentered(line.GetComponent<RectTransform>(), center, size);
            line.GetComponent<Image>().raycastTarget = false;
        }

        private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetCentered(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private struct PreviewSlot
        {
            public PreviewSlot(string id, float left, float top)
            {
                Id = id;
                Left = left;
                Top = top;
            }

            public string Id;
            public float Left;
            public float Top;
        }
    }
}
