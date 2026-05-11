using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class RunSceneController : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private Text goldLabel;
        [SerializeField] private Text roundLabel;
        [SerializeField] private Text hpLabel;
        [SerializeField] private Text stateLabel;

        [Header("Meta")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject runPanel;
        [SerializeField] private Dropdown campaignDropdown;
        [SerializeField] private Dropdown heroDropdown;
        [SerializeField] private Text campaignDescriptionLabel;
        [SerializeField] private Text heroDescriptionLabel;
        [SerializeField] private Text campaignLabel;
        [SerializeField] private Text heroLabel;
        [SerializeField] private Text logLabel;
        [SerializeField] private Text shopMetaLabel;

        [Header("Panels")]
        [SerializeField] private Transform shopCardRoot;
        [SerializeField] private Transform handCardRoot;
        [SerializeField] private Transform boardCardRoot;
        [SerializeField] private Text shopText;
        [SerializeField] private Text handText;
        [SerializeField] private Text boardText;
        [SerializeField] private Text battlePreviewText;

        private readonly RunFlowController _flow = new RunFlowController();
        private readonly BattleStubSystem _battleStub = new BattleStubSystem();
        private readonly SaveGameSystem _saveGame = new SaveGameSystem();
        private int _selectedHandIndex = -1;
        private string _selectedBoardSlotId;
        private string _dragSource;
        private int _dragHandIndex = -1;
        private string _dragBoardSlotId;
        private readonly List<string> _recentLogs = new List<string>();

        private RunState Run => ProphecyGameSession.Instance.CurrentRun;

        private void Start()
        {
            if (ProphecyGameSession.Instance == null)
            {
                Debug.LogError("ProphecyGameSession is missing.");
                return;
            }

            InitializeTitleSelectors();
            ShowTitle();
        }

        public void ShowTitle()
        {
            if (titlePanel != null)
            {
                titlePanel.SetActive(true);
            }

            if (runPanel != null)
            {
                runPanel.SetActive(false);
            }
        }

        public void ShowRun()
        {
            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (runPanel != null)
            {
                runPanel.SetActive(true);
            }
        }

        public void StartSelectedRun()
        {
            var data = ProphecyGameSession.Instance.Data;
            var campaignId = data.Campaigns.Count > 0
                ? data.Campaigns[Mathf.Clamp(campaignDropdown != null ? campaignDropdown.value : 0, 0, data.Campaigns.Count - 1)].id
                : null;
            var heroId = data.Heroes.Count > 0
                ? data.Heroes[Mathf.Clamp(heroDropdown != null ? heroDropdown.value : 0, 0, data.Heroes.Count - 1)].id
                : null;

            _flow.PrepareNewRun(campaignId, heroId);
            EnsureShopInitialized();
            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (runPanel != null)
            {
                runPanel.SetActive(true);
            }

            WriteLog("已开始所选战役。");
            RefreshView();
        }

        private void InitializeTitleSelectors()
        {
            var data = ProphecyGameSession.Instance.Data;
            if (campaignDropdown != null)
            {
                campaignDropdown.ClearOptions();
                campaignDropdown.AddOptions(data.Campaigns.Select(item => item.name).ToList());
                campaignDropdown.onValueChanged.AddListener(_ => RefreshTitlePreview());
            }

            if (heroDropdown != null)
            {
                heroDropdown.ClearOptions();
                heroDropdown.AddOptions(data.Heroes.Select(item => item.name).ToList());
                heroDropdown.onValueChanged.AddListener(_ => RefreshTitlePreview());
            }

            RefreshTitlePreview();
        }

        private void RefreshTitlePreview()
        {
            var data = ProphecyGameSession.Instance.Data;
            if (campaignDescriptionLabel != null)
            {
                var campaign = data.Campaigns.Count > 0
                    ? data.Campaigns[Mathf.Clamp(campaignDropdown != null ? campaignDropdown.value : 0, 0, data.Campaigns.Count - 1)]
                    : null;
                campaignDescriptionLabel.text = campaign == null
                    ? "未加载战役数据。"
                    : $"{campaign.name}\n{campaign.desc}";
            }

            if (heroDescriptionLabel != null)
            {
                var hero = data.Heroes.Count > 0
                    ? data.Heroes[Mathf.Clamp(heroDropdown != null ? heroDropdown.value : 0, 0, data.Heroes.Count - 1)]
                    : null;
                heroDescriptionLabel.text = hero == null
                    ? "未加载英雄数据。"
                    : $"{hero.name}  {hero.title}\n{hero.short_text}\n{hero.passive_text}\n{hero.active_text}";
            }
        }

        private void StartRunIfNeeded()
        {
            if (!ProphecyGameSession.Instance.HasCurrentRun)
            {
                _flow.PrepareNewRun(null, null);
            }

            EnsureShopInitialized();
        }

        public void RefreshShop()
        {
            var success = _flow.RefreshShop();
            WriteLog(success ? "已花费 1 金币刷新商店。" : "无法刷新商店。");
            RefreshView();
        }

        public void UpgradeShop()
        {
            var success = _flow.UpgradeShop();
            WriteLog(success ? "商店已升级。" : "无法升级商店。");
            RefreshView();
        }

        public void ToggleShopLock()
        {
            var locked = _flow.ToggleShopLock();
            WriteLog(locked ? "商店已锁定到下一回合。" : "商店已解锁。");
            RefreshView();
        }

        public void BuyFirstCard()
        {
            BuyShopCard(Run.shopCards.FindIndex(card => card != null));
        }

        public void DeployFirstCard()
        {
            DeployHandCard(0);
        }

        private void BuyShopCard(int index)
        {
            var success = _flow.BuyUnit(index);
            WriteLog(success ? $"已购买商店第 {index + 1} 张。" : $"无法购买商店第 {index + 1} 张。");
            RefreshView();
        }

        private void DeployHandCard(int index)
        {
            var targetSlot = GetSelectedEmptyBoardSlot();
            var success = _flow.DeployUnit(index, targetSlot);
            if (success)
            {
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
            }
            WriteLog(success ? $"已部署手牌第 {index + 1} 张。" : $"无法部署手牌第 {index + 1} 张。");
            RefreshView();
        }

        private void DeployHandCardToSlot(int index, string boardSlotId)
        {
            var success = _flow.DeployUnit(index, boardSlotId);
            if (success)
            {
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
            }

            WriteLog(success ? $"已部署手牌第 {index + 1} 张到 {boardSlotId}。" : $"无法部署手牌第 {index + 1} 张到 {boardSlotId}。");
            RefreshView();
        }

        private void MoveBoardUnitToSlot(string fromSlotId, string toSlotId)
        {
            var success = _flow.MoveBoardUnit(fromSlotId, toSlotId);
            if (success)
            {
                _selectedBoardSlotId = null;
            }

            WriteLog(success ? $"已移动棋盘单位：{fromSlotId} -> {toSlotId}。" : $"无法移动棋盘单位：{fromSlotId} -> {toSlotId}。");
            RefreshView();
        }

        private void SelectHandCard(int index)
        {
            if (index < 0 || index >= Run.handCards.Count)
            {
                return;
            }

            _selectedHandIndex = index;
            _selectedBoardSlotId = null;
            WriteLog($"已选择手牌第 {index + 1} 张，点击空棋盘格部署。");
            RefreshView();
        }

        private void HandleBoardSlotClicked(string boardSlotId)
        {
            if (string.IsNullOrWhiteSpace(boardSlotId))
            {
                return;
            }

            var unit = Run.boardUnits.FirstOrDefault(item => item.boardSlotId == boardSlotId);
            if (_selectedHandIndex >= 0)
            {
                DeployHandCardToSlot(_selectedHandIndex, boardSlotId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedBoardSlotId) && _selectedBoardSlotId != boardSlotId)
            {
                MoveBoardUnitToSlot(_selectedBoardSlotId, boardSlotId);
                return;
            }

            _selectedBoardSlotId = unit == null ? boardSlotId : unit.boardSlotId;
            WriteLog(unit == null ? $"已选择空格 {boardSlotId}。" : $"已选择棋盘单位：{unit.name}（{boardSlotId}）。");
            RefreshView();
        }

        public void BeginRuntimeDrag(string source, int handIndex, string boardSlotId)
        {
            _dragSource = source;
            _dragHandIndex = handIndex;
            _dragBoardSlotId = boardSlotId;
        }

        public void EndRuntimeDrag()
        {
            _dragSource = null;
            _dragHandIndex = -1;
            _dragBoardSlotId = null;
        }

        public void DropRuntimeDragOnBoardSlot(string boardSlotId)
        {
            if (_dragSource == "hand")
            {
                DeployHandCardToSlot(_dragHandIndex, boardSlotId);
            }
            else if (_dragSource == "board")
            {
                MoveBoardUnitToSlot(_dragBoardSlotId, boardSlotId);
            }

            EndRuntimeDrag();
        }

        public void SellLastHandCard()
        {
            SellHandCard(Run.handCards.Count - 1);
        }

        public void SellLastBoardUnit()
        {
            SellBoardCard(Run.boardUnits.Count - 1);
        }

        private void SellHandCard(int index)
        {
            var success = _flow.SellHandUnit(index);
            WriteLog(success ? $"已出售手牌第 {index + 1} 张。" : $"无法出售手牌第 {index + 1} 张。");
            RefreshView();
        }

        private void SellBoardCard(int index)
        {
            var target = index >= 0 && index < Run.boardUnits.Count ? Run.boardUnits[index].boardSlotId : null;
            var success = !string.IsNullOrWhiteSpace(target) && _flow.SellBoardUnit(target);
            WriteLog(success ? $"已出售棋盘第 {index + 1} 个单位。" : $"无法出售棋盘第 {index + 1} 个单位。");
            RefreshView();
        }

        private void SellBoardSlot(string boardSlotId)
        {
            var success = !string.IsNullOrWhiteSpace(boardSlotId) && _flow.SellBoardUnit(boardSlotId);
            WriteLog(success ? $"已出售棋盘 {boardSlotId} 的单位。" : $"无法出售棋盘 {boardSlotId} 的单位。");
            RefreshView();
        }

        public void StartBattle()
        {
            _flow.EnterBattlePhase();
            var result = _battleStub.Resolve(Run);
            _flow.FinishBattlePhase();

            _flow.ResolveBattleOutcome(result);
            RuntimeSfxPlayer.PlayBattleResult(result.Victory);
            WriteLog(result.Summary);
            RefreshView();
        }

        public void StartNewRun()
        {
            ShowTitle();
        }

        public void SaveGame()
        {
            var success = _saveGame.SaveCurrentRun();
            RuntimeSfxPlayer.PlaySaveLoad(success);
            WriteLog(success ? $"已保存：{_saveGame.SavePath}" : "保存失败");
            RefreshView();
        }

        public void LoadGame()
        {
            var success = _saveGame.LoadCurrentRun();
            RuntimeSfxPlayer.PlaySaveLoad(success);
            if (success)
            {
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
                ShowRun();
            }

            WriteLog(success ? $"已读取：{_saveGame.SavePath}" : "读取失败：没有可用存档");
            RefreshView();
        }

        public void ReturnToTitle()
        {
            RuntimeUiBootstrap.ShowTitleScreen();
        }

        public void RefreshView()
        {
            StartRunIfNeeded();
            var data = ProphecyGameSession.Instance.Data;
            goldLabel.text = $"金币：{Run.gold}";
            roundLabel.text = $"回合：{Run.round}";
            hpLabel.text = $"生命：{Run.playerHp}";
            stateLabel.text = $"阶段：{FormatRunState(Run.state)}";
            if (shopMetaLabel != null)
            {
                shopMetaLabel.text = $"商店 L{Run.shopLevel}  升级 {_flow.ShopSystem.GetCurrentShopUpgradeCost(Run)} 金币  {(Run.isShopLocked ? "已锁定" : "未锁定")}  胜 {Run.campaignWins} / 负 {Run.campaignLosses}";
            }

            var campaign = data.Campaigns.FirstOrDefault(item => item.id == Run.campaignId);
            var hero = data.Heroes.FirstOrDefault(item => item.id == Run.heroId);
            campaignLabel.text = $"战役：{(campaign != null ? campaign.name : Run.campaignId)}";
            heroLabel.text = $"英雄：{(hero != null ? hero.name : Run.heroId)}";

            shopText.text = FormatShop();
            handText.text = FormatHand();
            boardText.text = FormatBoard();
            battlePreviewText.text = FormatBattlePreview();
            RefreshCardLists();
        }

        private void EnsureShopInitialized()
        {
            _flow.ShopSystem.InitializeShop(Run);
        }

        private string GetSelectedEmptyBoardSlot()
        {
            if (string.IsNullOrWhiteSpace(_selectedBoardSlotId))
            {
                return null;
            }

            return Run.boardUnits.Any(unit => unit.boardSlotId == _selectedBoardSlotId) ? null : _selectedBoardSlotId;
        }

        private void WriteLog(string message)
        {
            if (logLabel == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                _recentLogs.Insert(0, message);
            }

            while (_recentLogs.Count > 7)
            {
                _recentLogs.RemoveAt(_recentLogs.Count - 1);
            }

            logLabel.text = "日志：\n" + string.Join("\n", _recentLogs);
        }

        private void RefreshCardLists()
        {
            RebuildUnitCardList(shopCardRoot, Run.shopCards, (card, _) => FormatUnitCardLabel(card), "购买", BuyShopCard, null, null);
            RebuildUnitCardList(handCardRoot, Run.handCards, (card, index) => card == null ? string.Empty : FormatUnitCardLabel(card, _selectedHandIndex == index ? ">" : null), "部署", DeployHandCard, "出售", SellHandCard, "hand");
            RebuildBoardSlotGrid();
        }

        private string FormatUnitCardLabel(UnitCardState card, string prefix = null)
        {
            if (card == null)
            {
                return "已售出";
            }

            var unit = ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            var title = string.IsNullOrWhiteSpace(prefix) ? card.name : $"{prefix}  {card.name}";
            var goldSuffix = card.isGolden ? " 金色" : string.Empty;
            if (unit == null)
            {
                return $"{title}  {card.star}*{goldSuffix}";
            }

            var tags = $"{unit.race} / {unit.faith} / {unit.typeLabel}";
            var attack = unit.attack + card.shopBuffAttack;
            var hp = unit.hp + card.shopBuffHp;
            var defense = unit.defense + card.shopBuffDefense;
            var buffSuffix = card.shopBuffAttack > 0 ? $" +{card.shopBuffAttack}攻" : string.Empty;
            var poolSuffix = card.fromShopPurchase ? " 池" : string.Empty;
            var stats = $"攻 {attack}  血 {hp}  防 {defense}{buffSuffix}{poolSuffix}";
            return $"{title}  {unit.star}*{goldSuffix}\n{tags}  {stats}";
        }

        private void RebuildUnitCardList<T>(
            Transform root,
            System.Collections.Generic.IReadOnlyList<T> cards,
            System.Func<T, int, string> labelFactory,
            string primaryLabel,
            System.Action<int> primaryAction,
            string secondaryLabel,
            System.Action<int> secondaryAction,
            string dragSource = null)
            where T : UnitCardState
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(root.GetChild(i).gameObject);
            }

            var isGridRoot = root.GetComponent<GridLayoutGroup>() != null;
            var isHorizontalRoot = root.GetComponent<HorizontalLayoutGroup>() != null;
            var displayCount = cards.Count;
            if (isGridRoot)
            {
                displayCount = Mathf.Max(9, displayCount);
            }
            else if (isHorizontalRoot)
            {
                displayCount = Mathf.Max(6, displayCount);
            }

            for (var i = 0; i < displayCount; i += 1)
            {
                var card = i < cards.Count ? cards[i] : null;
                CreateUnitCard(root, card, labelFactory(card, i), i, primaryLabel, primaryAction, secondaryLabel, secondaryAction, dragSource, null);
            }
        }

        private void CreateUnitCard(
            Transform root,
            UnitCardState card,
            string label,
            int index,
            string primaryLabel,
            System.Action<int> primaryAction,
            string secondaryLabel,
            System.Action<int> secondaryAction,
            string dragSource,
            string boardSlotId)
        {
            var cardObject = new GameObject("UnitCard", typeof(Image), typeof(Button), typeof(LayoutElement));
            cardObject.transform.SetParent(root, false);
            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var isShopCard = string.IsNullOrWhiteSpace(dragSource);
            var isGridCard = root != null && root.GetComponent<GridLayoutGroup>() != null;
            var cardWidth = isShopCard ? 124f : isGridCard ? 108f : 0f;
            var cardHeight = isShopCard ? 148f : isGridCard ? 138f : 82f;
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            var layoutElement = cardObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = cardWidth > 0f ? cardWidth : -1f;
            layoutElement.preferredHeight = cardHeight;
            layoutElement.flexibleWidth = cardWidth > 0f ? 0f : 1f;

            var background = cardObject.GetComponent<Image>();
            background.color = card == null
                ? new Color32(20, 29, 42, 160)
                : card.isGolden
                    ? new Color32(96, 78, 34, 245)
                    : new Color32(42, 58, 74, 245);
            var cardButton = cardObject.GetComponent<Button>();
            cardButton.targetGraphic = background;
            if (dragSource == "hand" && card != null)
            {
                cardButton.onClick.AddListener(() => SelectHandCard(index));
            }
            else if (dragSource == "board" && !string.IsNullOrWhiteSpace(boardSlotId))
            {
                cardButton.onClick.AddListener(() => HandleBoardSlotClicked(boardSlotId));
            }

            var iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(cardObject.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = isShopCard || isGridCard ? new Vector2(62f, 18f) : new Vector2(36f, 0f);
            iconRect.sizeDelta = isShopCard || isGridCard ? new Vector2(58f, 58f) : new Vector2(52f, 52f);
            RuntimeUnitIconCache.ApplyTo(iconObject.GetComponent<Image>(), card?.name);

            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(cardObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = isShopCard || isGridCard ? new Vector2(8f, 4f) : new Vector2(74f, 0f);
            labelRect.offsetMax = isShopCard || isGridCard ? new Vector2(-8f, -74f) : new Vector2(-170f, 0f);

            var text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = isShopCard || isGridCard ? 12 : 16;
            text.color = Color.white;
            text.alignment = isShopCard || isGridCard ? TextAnchor.LowerCenter : TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label;

            if (card != null && !string.IsNullOrWhiteSpace(dragSource))
            {
                var dragItem = cardObject.AddComponent<RuntimeUnitDragItem>();
                dragItem.Controller = this;
                dragItem.Source = dragSource;
                dragItem.HandIndex = dragSource == "hand" ? index : -1;
                dragItem.BoardSlotId = boardSlotId;
            }

            if (card != null && !string.IsNullOrWhiteSpace(primaryLabel) && primaryAction != null)
            {
                CreateCardActionButton(cardObject.transform, primaryLabel, isShopCard || isGridCard ? new Vector2(isGridCard ? -32f : 0f, 14f) : new Vector2(-116f, 16f), () => primaryAction(index), isShopCard || isGridCard);
            }

            if (card != null && !string.IsNullOrWhiteSpace(secondaryLabel) && secondaryAction != null)
            {
                CreateCardActionButton(cardObject.transform, secondaryLabel, isGridCard ? new Vector2(32f, 14f) : new Vector2(-52f, 16f), () => secondaryAction(index), isShopCard || isGridCard);
            }
        }

        private void RebuildBoardSlotGrid()
        {
            if (boardCardRoot == null)
            {
                return;
            }

            for (var i = boardCardRoot.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(boardCardRoot.GetChild(i).gameObject);
            }

            var displayColumns = new[]
            {
                new[] { "4-1", "4-2", "4-3", "4-4" },
                new[] { "3-1", "3-2", "3-3" },
                new[] { "2-1", "2-2" },
                new[] { "1-1" }
            };

            foreach (var column in displayColumns)
            {
                var columnObject = new GameObject("BoardColumn", typeof(VerticalLayoutGroup), typeof(LayoutElement));
                columnObject.transform.SetParent(boardCardRoot, false);
                var columnLayout = columnObject.GetComponent<VerticalLayoutGroup>();
                columnLayout.spacing = 14f;
                columnLayout.childControlWidth = false;
                columnLayout.childControlHeight = false;
                columnLayout.childForceExpandWidth = false;
                columnLayout.childForceExpandHeight = false;
                columnLayout.childAlignment = TextAnchor.MiddleCenter;
                var columnElement = columnObject.GetComponent<LayoutElement>();
                columnElement.preferredWidth = 92f;
                columnElement.flexibleWidth = 0f;

                foreach (var slotId in column)
                {
                    CreateBoardSlotCell(columnObject.transform, slotId);
                }
            }
        }

        private void CreateBoardSlotCell(Transform parent, string slotId)
        {
            var unit = Run.boardUnits.FirstOrDefault(item => item.boardSlotId == slotId);
            var isSelected = _selectedBoardSlotId == slotId;
            var cellObject = new GameObject("BoardSlot_" + slotId, typeof(Image), typeof(Button), typeof(LayoutElement), typeof(RuntimeBoardSlotDropTarget));
            cellObject.transform.SetParent(parent, false);
            var layout = cellObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 88f;
            layout.preferredHeight = 88f;
            layout.flexibleWidth = 0f;

            var image = cellObject.GetComponent<Image>();
            image.color = isSelected
                ? new Color32(92, 125, 72, 255)
                : unit == null
                    ? new Color32(34, 48, 60, 255)
                    : new Color32(48, 67, 84, 255);

            var button = cellObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => HandleBoardSlotClicked(slotId));

            var dropTarget = cellObject.GetComponent<RuntimeBoardSlotDropTarget>();
            dropTarget.Controller = this;
            dropTarget.BoardSlotId = slotId;

            if (unit != null)
            {
                var iconObject = new GameObject("Icon", typeof(Image));
                iconObject.transform.SetParent(cellObject.transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 10f);
                iconRect.sizeDelta = new Vector2(46f, 46f);
                RuntimeUnitIconCache.ApplyTo(iconObject.GetComponent<Image>(), unit.name);

                var dragItem = cellObject.AddComponent<RuntimeUnitDragItem>();
                dragItem.Controller = this;
                dragItem.Source = "board";
                dragItem.BoardSlotId = slotId;
            }

            var unitDefinition = unit == null ? null : ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
            var stats = unitDefinition == null ? string.Empty : $"攻{unitDefinition.attack + unit.shopBuffAttack} 防{unitDefinition.defense + unit.shopBuffDefense}";
            var title = unit == null ? $"{slotId}\n空位" : $"{unit.name}\n{unit.star}* {stats}";
            var text = CreateChildText(cellObject.transform, title, 11, TextAnchor.MiddleCenter, new Vector2(4f, 2f), new Vector2(-4f, -22f));
            text.color = Color.white;

            if (unit != null)
            {
                CreateSmallBoardActionButton(cellObject.transform, "出售", () => SellBoardSlot(slotId));
            }
            else if (_selectedHandIndex >= 0)
            {
                CreateSmallBoardActionButton(cellObject.transform, "部署", () => DeployHandCardToSlot(_selectedHandIndex, slotId));
            }
            else if (!string.IsNullOrWhiteSpace(_selectedBoardSlotId))
            {
                CreateSmallBoardActionButton(cellObject.transform, "移动", () => MoveBoardUnitToSlot(_selectedBoardSlotId, slotId));
            }
        }

        private static void CreateSmallBoardActionButton(Transform parent, string label, UnityEngine.Events.UnityAction callback)
        {
            var buttonObject = new GameObject(label + "Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(56f, 20f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(72, 104, 132, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);

            var text = CreateChildText(buttonObject.transform, label, 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.color = Color.white;
        }

        private static Text CreateChildText(Transform parent, string value, int fontSize, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject("Label", typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static void CreateCardActionButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction callback, bool bottomAnchored = false)
        {
            var buttonObject = new GameObject(label + "Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = bottomAnchored ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.5f);
            rect.anchorMax = bottomAnchored ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = bottomAnchored ? new Vector2(58f, 24f) : new Vector2(58f, 30f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(72, 104, 132, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);

            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = bottomAnchored ? 12 : 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label;
        }

        private string FormatShop()
        {
            if (Run.shopCards.Count == 0)
            {
                return "商店\n（空）";
            }

            var lines = Run.shopCards.Select((card, index) =>
                card == null
                    ? $"{index + 1}. 已售出"
                    : $"{index + 1}. {card.name}  {card.star}*{(card.isGolden ? " 金色" : string.Empty)}");
            return "商店\n" + string.Join("\n", lines);
        }

        private string FormatHand()
        {
            if (Run.handCards.Count == 0)
            {
                return "手牌\n（空）";
            }

            var lines = Run.handCards.Select((card, index) => $"{index + 1}. {card.name}  {card.star}*{(card.isGolden ? " 金色" : string.Empty)}");
            return "手牌\n" + string.Join("\n", lines);
        }

        private string FormatBoard()
        {
            if (Run.boardUnits.Count == 0)
            {
                return "棋盘\n（空）";
            }

            var lines = Run.boardUnits.Select(unit => $"{unit.boardSlotId}: {unit.name}  {unit.star}*{(unit.isGolden ? " 金色" : string.Empty)}");
            return "棋盘\n" + string.Join("\n", lines);
        }

        private string FormatBattlePreview()
        {
            var playerScore = BattleStubSystem.EstimatePlayerScore(Run);
            var enemyScore = BattleStubSystem.EstimateEnemyScore(Run);
            var limit = Run.campaignRoundLimit > 0 ? Run.campaignRoundLimit : 20;
            var rewards = Run.pendingBattleRewards;
            var rewardLine = rewards == null
                ? "待结算奖励：无"
                : $"待结算奖励：金币 +{rewards.nextRoundGold}，商店攻击 +{rewards.nextRoundShopBuffAttack}，发现 {rewards.discoverFaithRewards?.Count ?? 0}";
            var lastEntries = Run.battleHistory == null
                ? Enumerable.Empty<string>()
                : Run.battleHistory
                    .OrderByDescending(item => item.round)
                    .Take(3)
                    .Select(item => $"R{item.round} {(item.victory ? "胜" : "败")}  {item.playerScore}:{item.enemyScore}  {item.hpDelta}");
            var history = string.Join("\n", lastEntries);
            if (string.IsNullOrWhiteSpace(history))
            {
                history = "无";
            }

            return $"战斗预览\n进度：{Run.round}/{limit}  胜 {Run.campaignWins} / 负 {Run.campaignLosses}\n我方战力：{playerScore}\n敌方战力：{enemyScore}\n{rewardLine}\n最近战斗：\n{history}\n上次战斗：{Run.lastBattleSummary ?? "无"}";
        }

        private static string FormatRunState(string state)
        {
            switch (state)
            {
                case "manage":
                    return "经营";
                case "battle":
                    return "战斗";
                case "settle":
                    return "结算";
                case "gameover":
                    return "失败";
                case "victory":
                    return "胜利";
                default:
                    return string.IsNullOrWhiteSpace(state) ? "未知" : state;
            }
        }
    }
}
