using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private Image hpFillImage;

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
        [SerializeField] private GameObject battleStagePanel;
        [SerializeField] private Transform battlePlayerRoot;
        [SerializeField] private Transform battleEnemyRoot;
        [SerializeField] private Text battleStageStatusLabel;
        [SerializeField] private Text battleStageLogLabel;
        [SerializeField] private Image battleStageProgressFill;
        [SerializeField] private GameObject battleUnitPrefab;
        [SerializeField] private bool useRealtimeBattlePreview;
        [SerializeField] private UnitCardRaceStyleLibrary unitCardRaceStyles;

        private readonly RunFlowController _flow = new RunFlowController();
        private readonly BattleStubSystem _battleStub = new BattleStubSystem();
        private readonly BattleRealtimeSystem _battleRealtime = new BattleRealtimeSystem();
        private readonly SaveGameSystem _saveGame = new SaveGameSystem();
        private const string BattleUnitPrefabResourcePath = "Prefabs/UI/BattleUnitView";
        private const int BattleHexColumnCount = 11;
        private const float BattleHexHorizontalStep = 0.75f;
        private const float BattleHexHeightRatio = 0.8660254f;
        private static readonly int[] BattleHexRowsByColumn = { 4, 3, 4, 3, 4, 3, 4, 3, 4, 3, 4 };
        private const int HandMaxCount = 9;
        private const float TargetSearchInterval = 1f;
        private const float VisualAttackRangeSlack = 36f;
        private const float BattleFloatingTextDuration = 0.75f;
        private const float CountLossFloatingTextDelay = 0.62f;
        private const float CountLossActionPauseDuration = 1.34f;
        private int _selectedHandIndex = -1;
        private string _selectedBoardSlotId;
        private string _pendingTargetedEntrySourceSlotId;
        private string _pendingTargetedEntrySourceName;
        private bool _pendingTargetedEntryGoldReward;
        private string _dragSource;
        private int _dragHandIndex = -1;
        private string _dragBoardSlotId;
        private bool _dragSellMode;
        private bool _battlePlaybackRunning;
        private Transform _battleFieldRoot;
        private GameObject _battleStartActionButton;
        private GameObject _battlePlaybackSpeedRoot;
        private float _battlePlaybackSpeed = 0.5f;
        private bool _battleStartActionRequested;
        private bool _battleSetupDraggingEnabled;
        private float _visualBattleActionPauseRemaining;
        private GameObject _goldDeployRewardModal;
        private Transform _goldDeployRewardOptionsRoot;
        private Text _goldDeployRewardTitleLabel;
        private Text _goldDeployRewardSubtitleLabel;
        private int _goldDeployRewardActualStar;
        private GameObject _battleLogButton;
        private GameObject _battleLogModal;
        private Text _battleLogContentLabel;
        private const float DragSnapRadius = 58f;
        private readonly List<RuntimeDragBoardSlotVisual> _dragBoardSlots = new List<RuntimeDragBoardSlotVisual>();
        private GameObject _dragIndicatorRoot;
        private RectTransform _dragIndicatorRect;
        private RectTransform _dragArrowBody;
        private RectTransform _dragArrowHead;
        private Canvas _dragCanvas;
        private Camera _dragEventCamera;
        private Vector2 _dragArrowStartScreen;
        private string _dragSnapBoardSlotId;
        private readonly List<string> _recentLogs = new List<string>();
        private readonly List<string> _latestBattleLogLines = new List<string>();
        private readonly Dictionary<GameObject, bool> _battleUiVisibility = new Dictionary<GameObject, bool>();
        private readonly Dictionary<RectTransform, BattleRectTransformState> _battleRectTransformStates = new Dictionary<RectTransform, BattleRectTransformState>();
        private readonly Dictionary<Image, BattleImageState> _battleImageStates = new Dictionary<Image, BattleImageState>();
        private readonly Dictionary<string, Vector2> _battlePlayerPositionOverrides = new Dictionary<string, Vector2>();
        private static GameObject _cachedBattleUnitPrefab;
        private static Sprite _cachedBattleHexCellSprite;

        private RunState Run => ProphecyGameSession.Instance.CurrentRun;

        public void BindBattleStagePanel(GameObject panel, Transform playerRoot, Transform enemyRoot, Text statusLabel, Text logText, Image progressFill)
        {
            battleStagePanel = panel;
            battlePlayerRoot = playerRoot;
            battleEnemyRoot = enemyRoot;
            battleStageStatusLabel = statusLabel;
            battleStageLogLabel = logText;
            battleStageProgressFill = progressFill;
        }

        private sealed class RuntimeDragBoardSlotVisual
        {
            public string SlotId;
            public RectTransform Rect;
            public Image Image;
            public bool Occupied;
            public Color BaseColor;
            public Color AvailableColor;
            public Color HoverColor;
        }

        private struct BattleRectTransformState
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
        }

        private struct BattleImageState
        {
            public Color Color;
            public bool RaycastTarget;
        }

        private sealed class BattleStageUnitView
        {
            public RectTransform Rect;
            public Image Backing;
            public Text Label;
            public Vector2 StartPosition;
            public Vector2 FightPosition;
            public string Name;
            public string UnitId;
            public string SlotId;
            public int Star;
            public int Speed;
            public float Range;
            public int Size;
            public int Hp;
            public int MaxHp;
            public int CurrentCount;
            public int MaxCount;
            public int HpPerUnit;
            public int DamageMin;
            public int DamageMax;
            public BattleUnitView UnitView;
            public int Attack;
            public int Defense;
            public int Power;
            public float AttackInterval;
            public float AttackTimer;
            public float AttackAnim;
            public bool HasStartedAttacking;
            public bool HasAttackAnchor;
            public bool IsHitShaking;
            public Vector2 AttackAnchorPosition;
            public BattleStageUnitView Target;
            public float TargetSearchTimer;
            public bool PlayerSide;
            public bool Dead;
            public bool IsSummon;
            public float SummonDuration;
            public float StunRemaining;
            public float MoveLockRemaining;
            public float AttackLockRemaining;
        }

        private sealed class BattleProjectileView
        {
            public RectTransform Rect;
            public Image Image;
            public BattleStageUnitView Target;
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float Duration;
            public int Damage;
        }

        private sealed class BattleFloatingTextView
        {
            public RectTransform Rect;
            public Text Text;
            public Vector2 Start;
            public float Life;
            public float Duration;
            public bool UseScaleAnimation;
        }

        private sealed class BattleEffectBurstView
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 StartSize;
            public Vector2 EndSize;
            public float Life;
            public float Duration;
        }

        private sealed class BattleStagePrefabParts
        {
            public RectTransform Rect;
            public Image Backing;
            public Text Label;
            public BattleUnitView View;
        }

        private struct BoardSlotPixel
        {
            public BoardSlotPixel(string id, float left, float top)
            {
                Id = id;
                Left = left;
                Top = top;
            }

            public string Id;
            public float Left;
            public float Top;
        }

        private sealed class UnitNumberSnapshot
        {
            public string Zone;
            public int Index;
            public string SlotId;
            public string UnitId;
            public string Name;
            public bool Golden;
            public int Hp;
            public int Attack;
            public int Defense;
            public int Power;
            public int Speed;
            public int Morale;
            public int ForestGems;
            public int Count;
        }

        private void Start()
        {
            ProphecyGameSession.EnsureInstance();
            var canvas = GetComponentInParent<Canvas>();
            RuntimeUiBootstrap.WirePrefabButtons(canvas != null ? canvas.gameObject : gameObject);

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
            RestoreOperationalUiAfterBattle();
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
            RestoreOperationalUiAfterBattle();
            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (runPanel != null)
            {
                runPanel.SetActive(true);
            }

            if (battleStagePanel != null)
            {
                battleStagePanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (string.IsNullOrWhiteSpace(_dragSource))
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                CancelRuntimeDrag();
                return;
            }

            UpdateRuntimeDrag(Input.mousePosition);
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

        public void StartSmallMerchantChaseTest()
        {
            if (_battlePlaybackRunning)
            {
                return;
            }

            StartCoroutine(PlaySmallMerchantChaseTest());
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
            var before = CaptureUnitNumberSnapshots();
            var lackedGold = Run != null && Run.gold < 1;
            var success = _flow.RefreshShop();
            if (!success)
            {
                PlayFailureSfx(lackedGold);
                ShowFloatingText(lackedGold ? "金币不足" : "无法刷新商店");
            }

            WriteLog(success ? "已花费 1 金币刷新商店。" : "无法刷新商店。");
            RefreshView();
            PlayNumberChangeFeedback(before);
        }

        public void UpgradeShop()
        {
            var before = CaptureUnitNumberSnapshots();
            var upgradeCost = Run == null ? 0 : _flow.ShopSystem.GetCurrentShopUpgradeCost(Run);
            var lackedGold = Run != null && upgradeCost > 0 && Run.gold < upgradeCost;
            var success = _flow.UpgradeShop();
            if (!success)
            {
                PlayFailureSfx(lackedGold);
                ShowFloatingText(lackedGold ? "金币不足" : "无法升级商店");
            }

            WriteLog(success ? "商店已升级。" : "无法升级商店。");
            RefreshView();
            PlayNumberChangeFeedback(before);
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
            var before = CaptureUnitNumberSnapshots();
            var buyCost = ProphecyGameSession.Instance.Data.Config?.unitBuyCost ?? 3;
            var lackedGold = Run != null && Run.gold < buyCost;
            var handFull = Run != null && Run.handCards.Count >= HandMaxCount;
            ClearPendingSynthesisSfx();
            var success = _flow.BuyUnit(index);
            if (success)
            {
                RuntimeSfxPlayer.PlayBuyCard();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
            }
            else
            {
                PlayFailureSfx(lackedGold);
                ShowFloatingText(handFull ? "手牌已满" : lackedGold ? "金币不足" : "无法购买卡牌");
            }

            WriteLog(success ? $"已购买商店第 {index + 1} 张。" : $"无法购买商店第 {index + 1} 张。");
            RefreshView();
            PlayNumberChangeFeedback(before);
        }

        private void DeployHandCard(int index)
        {
            if (IsGoldDeployRewardOpen())
            {
                RuntimeSfxPlayer.PlayError();
                return;
            }

            if (IsForestGemHandCard(index))
            {
                var selectedGemTargetSlot = Run?.boardUnits.Any(unit => unit.boardSlotId == _selectedBoardSlotId) == true ? _selectedBoardSlotId : null;
                if (UseForestGemCardOnSlot(index, selectedGemTargetSlot))
                {
                    return;
                }

                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请选择阵上单位");
                return;
            }

            var targetSlot = GetSelectedEmptyBoardSlot();
            var deployedGoldenCard = IsGoldenHandCard(index);
            var needsTargetSelection = IsTargetedEntryPowerHandCard(index);
            var noBoardSlot = string.IsNullOrWhiteSpace(targetSlot);
            var before = CaptureUnitNumberSnapshots();
            ClearPendingSynthesisSfx();
            var success = _flow.DeployUnit(index, targetSlot, needsTargetSelection);
            var devourEvents = success ? _flow.ConsumeDevourShopEvents() : new List<DevourShopEventState>();
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                var deployed = FindJustDeployedBoardUnit(targetSlot);
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
                RuntimeSfxPlayer.PlayMove();
                PlayAbilitySfxIfNeeded();
                if (needsTargetSelection)
                {
                    BeginTargetedEntrySelection(deployed, deployedGoldenCard);
                }
                else
                {
                    PlaySynthesisSfxIfNeeded();
                }
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText(noBoardSlot ? "没有可用阵位" : "无法上阵");
            }

            WriteLog(success ? $"已部署手牌第 {index + 1} 张。" : $"无法部署手牌第 {index + 1} 张。");
            RefreshView();
            PlayNumberChangeFeedback(before);
            PlayDevourFeedbackIfNeeded(devourEvents);
            PlayManageFeedbackIfNeeded(feedbackEvents);
            if (success && deployedGoldenCard && !needsTargetSelection)
            {
                OpenGoldDeployRewardModal();
            }
        }

        private void DeployHandCardToSlot(int index, string boardSlotId)
        {
            if (IsGoldDeployRewardOpen())
            {
                RuntimeSfxPlayer.PlayError();
                return;
            }

            if (IsForestGemHandCard(index))
            {
                UseForestGemCardOnSlot(index, boardSlotId);
                return;
            }

            var deployedGoldenCard = IsGoldenHandCard(index);
            var needsTargetSelection = IsTargetedEntryPowerHandCard(index);
            var slotOccupied = Run != null && Run.boardUnits.Any(unit => unit.boardSlotId == boardSlotId);
            var before = CaptureUnitNumberSnapshots();
            ClearPendingSynthesisSfx();
            var success = _flow.DeployUnit(index, boardSlotId, needsTargetSelection);
            var devourEvents = success ? _flow.ConsumeDevourShopEvents() : new List<DevourShopEventState>();
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                var deployed = FindJustDeployedBoardUnit(boardSlotId);
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
                RuntimeSfxPlayer.PlayMove();
                PlayAbilitySfxIfNeeded();
                if (needsTargetSelection)
                {
                    BeginTargetedEntrySelection(deployed, deployedGoldenCard);
                }
                else
                {
                    PlaySynthesisSfxIfNeeded();
                }
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText(slotOccupied ? "目标位置已有单位" : "无法上阵");
            }

            WriteLog(success ? $"已部署手牌第 {index + 1} 张到 {boardSlotId}。" : $"无法部署手牌第 {index + 1} 张到 {boardSlotId}。");
            RefreshView();
            PlayNumberChangeFeedback(before);
            PlayDevourFeedbackIfNeeded(devourEvents);
            PlayManageFeedbackIfNeeded(feedbackEvents);
            if (success && deployedGoldenCard && !needsTargetSelection)
            {
                OpenGoldDeployRewardModal();
            }
        }

        private BoardUnitState FindJustDeployedBoardUnit(string boardSlotId)
        {
            if (Run == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(boardSlotId)
                ? Run.boardUnits.LastOrDefault()
                : Run.boardUnits.LastOrDefault(unit => unit.boardSlotId == boardSlotId);
        }

        private void BeginTargetedEntrySelection(BoardUnitState source, bool opensGoldReward)
        {
            if (source == null)
            {
                return;
            }

            _pendingTargetedEntrySourceSlotId = source.boardSlotId;
            _pendingTargetedEntrySourceName = source.name;
            _pendingTargetedEntryGoldReward = opensGoldReward;
            ShowFloatingText("选择祝福目标");
            WriteLog($"{source.name} 入场：请选择一个阵上单位获得力量。");
        }

        private bool ResolvePendingTargetedEntryOnSlot(string targetSlotId)
        {
            if (string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                return false;
            }

            var target = Run?.boardUnits.FirstOrDefault(unit => unit.boardSlotId == targetSlotId);
            if (target == null)
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请选择阵上单位");
                return true;
            }

            var sourceName = _pendingTargetedEntrySourceName;
            var sourceSlotId = _pendingTargetedEntrySourceSlotId;
            var before = CaptureUnitNumberSnapshots();
            ClearPendingSynthesisSfx();
            var value = _flow.ResolveTargetedEntryPower(sourceSlotId, targetSlotId);
            var feedbackEvents = _flow.ConsumeManageFeedbackEvents();
            var openGoldReward = _pendingTargetedEntryGoldReward;
            _pendingTargetedEntrySourceSlotId = null;
            _pendingTargetedEntrySourceName = null;
            _pendingTargetedEntryGoldReward = false;
            _selectedBoardSlotId = null;

            if (value > 0)
            {
                RuntimeSfxPlayer.PlayAbilityTrigger();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
                WriteLog($"{sourceName} 祝福 {target.name}，获得数量 +{value}。");
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("祝福失败");
                WriteLog($"{sourceName} 的祝福没有生效。");
            }

            RefreshView();
            PlayNumberChangeFeedback(before);
            PlayManageFeedbackIfNeeded(feedbackEvents);
            if (openGoldReward)
            {
                OpenGoldDeployRewardModal();
            }

            return true;
        }

        private bool UseForestGemCardOnSlot(int index, string boardSlotId)
        {
            var target = Run?.boardUnits.FirstOrDefault(unit => unit.boardSlotId == boardSlotId);
            if (target == null)
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请选择阵上单位");
                return false;
            }

            var before = CaptureUnitNumberSnapshots();
            var success = _flow.UseForestGemCard(index, boardSlotId);
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                _selectedHandIndex = -1;
                _selectedBoardSlotId = null;
                RuntimeSfxPlayer.PlayAbilityTrigger();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
                ShowUnitNumberFloatingText(GetBoardSlotRect(boardSlotId), $"获得数量+{ManageEventResolver.ForestGemReinforceCount}", new Color32(112, 236, 166, 255));
                WriteLog($"已对 {target.name} 使用密林宝钻，获得数量 +{ManageEventResolver.ForestGemReinforceCount}。");
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("无法使用密林宝钻");
            }

            RefreshView();
            PlayManageFeedbackIfNeeded(FilterForestGemUseFeedback(feedbackEvents));
            return success;
        }

        private void MoveBoardUnitToSlot(string fromSlotId, string toSlotId)
        {
            var success = _flow.MoveBoardUnit(fromSlotId, toSlotId);
            if (success)
            {
                _selectedBoardSlotId = null;
                RuntimeSfxPlayer.PlayMove();
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("无法移动单位");
            }

            WriteLog(success ? $"已移动棋盘单位：{fromSlotId} -> {toSlotId}。" : $"无法移动棋盘单位：{fromSlotId} -> {toSlotId}。");
            RefreshView();
        }

        private void SelectHandCard(int index)
        {
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请先选择祝福目标");
                return;
            }

            if (index < 0 || index >= Run.handCards.Count)
            {
                return;
            }

            _selectedHandIndex = index;
            _selectedBoardSlotId = null;
            RuntimeSfxPlayer.PlayCardSelect();
            WriteLog(IsForestGemHandCard(index)
                ? $"已选择密林宝钻，点击阵上单位使用。"
                : $"已选择手牌第 {index + 1} 张，点击空棋盘格部署。");
            RefreshView();
        }

        private void HandleBoardSlotClicked(string boardSlotId)
        {
            if (string.IsNullOrWhiteSpace(boardSlotId))
            {
                return;
            }

            var unit = Run.boardUnits.FirstOrDefault(item => item.boardSlotId == boardSlotId);
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                ResolvePendingTargetedEntryOnSlot(boardSlotId);
                return;
            }

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

        public void BeginRuntimeDrag(string source, int handIndex, string boardSlotId, PointerEventData eventData = null, RectTransform originRect = null)
        {
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请先选择祝福目标");
                return;
            }

            _dragSource = source;
            _dragHandIndex = handIndex;
            _dragBoardSlotId = boardSlotId;
            _dragSellMode = source == "hand" || source == "board";
            _dragEventCamera = eventData?.pressEventCamera;
            EnsureRuntimeDragIndicator();
            _dragArrowStartScreen = originRect != null
                ? RectTransformUtility.WorldToScreenPoint(GetRuntimeDragCamera(), originRect.TransformPoint(originRect.rect.center))
                : eventData != null ? eventData.pressPosition : (Vector2)Input.mousePosition;
            _dragSnapBoardSlotId = null;
            RuntimeSfxPlayer.PlayCardSelect();
            RuntimeUnitTooltip.SetSuppressed(true);
            CacheRuntimeDragBoardSlots();
            UpdateRuntimeDrag(eventData);
            if (_dragSellMode)
            {
                RebuildShopAsSellArea();
            }
        }

        public void UpdateRuntimeDrag(PointerEventData eventData)
        {
            UpdateRuntimeDrag(eventData != null ? eventData.position : (Vector2)Input.mousePosition);
        }

        private void UpdateRuntimeDrag(Vector2 pointerScreenPosition)
        {
            if (string.IsNullOrWhiteSpace(_dragSource))
            {
                return;
            }

            var targetScreenPosition = ResolveRuntimeDragTarget(pointerScreenPosition);
            UpdateRuntimeDragIndicator(targetScreenPosition);
        }

        public void EndRuntimeDrag()
        {
            var wasSellMode = _dragSellMode;
            _dragSource = null;
            _dragHandIndex = -1;
            _dragBoardSlotId = null;
            _dragSellMode = false;
            _dragSnapBoardSlotId = null;
            _dragEventCamera = null;
            RuntimeUnitTooltip.SetSuppressed(false);
            ClearRuntimeDragIndicator();
            ClearRuntimeDragBoardSlots();
            if (wasSellMode)
            {
                RefreshCardLists();
            }
        }

        public void CancelRuntimeDrag()
        {
            RuntimeSfxPlayer.PlayError();
            EndRuntimeDrag();
        }

        public void CompleteRuntimeDrag(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_dragSource))
            {
                return;
            }

            UpdateRuntimeDrag(eventData);
            var targetSlot = _dragSnapBoardSlotId;
            if (IsValidRuntimeDragBoardTarget(targetSlot))
            {
                DropRuntimeDragOnBoardSlot(targetSlot);
                return;
            }

            EndRuntimeDrag();
        }

        public void DropRuntimeDragOnBoardSlot(string boardSlotId)
        {
            if (!IsValidRuntimeDragBoardTarget(boardSlotId))
            {
                EndRuntimeDrag();
                return;
            }

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

        public void DropRuntimeDragOnSellArea()
        {
            var source = _dragSource;
            var handIndex = _dragHandIndex;
            var boardSlotId = _dragBoardSlotId;

            if (source == "hand")
            {
                SellHandCard(handIndex);
            }
            else if (source == "board")
            {
                SellBoardSlot(boardSlotId);
            }

            EndRuntimeDrag();
        }

        private void EnsureRuntimeDragIndicator()
        {
            if (_dragIndicatorRoot != null)
            {
                _dragIndicatorRoot.SetActive(true);
                _dragIndicatorRoot.transform.SetAsLastSibling();
                return;
            }

            _dragCanvas = GetComponentInParent<Canvas>();
            var parent = _dragCanvas != null ? _dragCanvas.transform : runPanel != null ? runPanel.transform : transform;
            _dragIndicatorRoot = new GameObject("RuntimeDragIndicator", typeof(RectTransform), typeof(CanvasGroup));
            _dragIndicatorRoot.transform.SetParent(parent, false);
            _dragIndicatorRoot.transform.SetAsLastSibling();
            _dragIndicatorRect = _dragIndicatorRoot.GetComponent<RectTransform>();
            _dragIndicatorRect.anchorMin = Vector2.zero;
            _dragIndicatorRect.anchorMax = Vector2.one;
            _dragIndicatorRect.offsetMin = Vector2.zero;
            _dragIndicatorRect.offsetMax = Vector2.zero;

            var group = _dragIndicatorRoot.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var bodyObject = new GameObject("ArrowBody", typeof(Image));
            bodyObject.transform.SetParent(_dragIndicatorRoot.transform, false);
            _dragArrowBody = bodyObject.GetComponent<RectTransform>();
            _dragArrowBody.anchorMin = new Vector2(0.5f, 0.5f);
            _dragArrowBody.anchorMax = new Vector2(0.5f, 0.5f);
            _dragArrowBody.pivot = new Vector2(0f, 0.5f);
            var bodyImage = bodyObject.GetComponent<Image>();
            bodyImage.color = new Color32(105, 220, 255, 190);
            bodyImage.raycastTarget = false;

            var headObject = new GameObject("ArrowHead", typeof(Text));
            headObject.transform.SetParent(_dragIndicatorRoot.transform, false);
            _dragArrowHead = headObject.GetComponent<RectTransform>();
            _dragArrowHead.anchorMin = new Vector2(0.5f, 0.5f);
            _dragArrowHead.anchorMax = new Vector2(0.5f, 0.5f);
            _dragArrowHead.pivot = new Vector2(0.5f, 0.5f);
            _dragArrowHead.sizeDelta = new Vector2(42f, 42f);
            var headText = headObject.GetComponent<Text>();
            headText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headText.text = ">";
            headText.fontSize = 38;
            headText.alignment = TextAnchor.MiddleCenter;
            headText.color = new Color32(160, 245, 255, 230);
            headText.raycastTarget = false;
        }

        private void ClearRuntimeDragIndicator()
        {
            if (_dragIndicatorRoot != null)
            {
                Destroy(_dragIndicatorRoot);
            }

            _dragIndicatorRoot = null;
            _dragIndicatorRect = null;
            _dragArrowBody = null;
            _dragArrowHead = null;
            _dragCanvas = null;
        }

        private void CacheRuntimeDragBoardSlots()
        {
            ClearRuntimeDragBoardSlots();
            if (boardCardRoot == null)
            {
                return;
            }

            var targets = boardCardRoot.GetComponentsInChildren<RuntimeBoardSlotDropTarget>(false);
            foreach (var target in targets)
            {
                var rect = target.GetComponent<RectTransform>();
                var image = target.GetComponent<Image>();
                if (rect == null || image == null || string.IsNullOrWhiteSpace(target.BoardSlotId))
                {
                    continue;
                }

                var occupied = Run != null && Run.boardUnits.Any(unit => unit.boardSlotId == target.BoardSlotId);
                var visual = new RuntimeDragBoardSlotVisual
                {
                    SlotId = target.BoardSlotId,
                    Rect = rect,
                    Image = image,
                    Occupied = occupied,
                    BaseColor = image.color,
                    AvailableColor = new Color32(72, 114, 96, 235),
                    HoverColor = new Color32(100, 178, 130, 255)
                };

                _dragBoardSlots.Add(visual);
                if (IsValidRuntimeDragBoardTarget(visual.SlotId))
                {
                    image.color = visual.AvailableColor;
                }
            }
        }

        private void ClearRuntimeDragBoardSlots()
        {
            foreach (var visual in _dragBoardSlots)
            {
                if (visual?.Image != null)
                {
                    visual.Image.color = visual.BaseColor;
                }
            }

            _dragBoardSlots.Clear();
        }

        private Vector2 ResolveRuntimeDragTarget(Vector2 pointerScreenPosition)
        {
            _dragSnapBoardSlotId = null;
            RuntimeDragBoardSlotVisual best = null;
            var bestDistance = float.MaxValue;
            var camera = GetRuntimeDragCamera();

            foreach (var visual in _dragBoardSlots)
            {
                if (visual == null || visual.Rect == null || visual.Image == null)
                {
                    continue;
                }

                if (!IsValidRuntimeDragBoardTarget(visual.SlotId))
                {
                    visual.Image.color = visual.BaseColor;
                    continue;
                }

                var center = RectTransformUtility.WorldToScreenPoint(camera, visual.Rect.TransformPoint(visual.Rect.rect.center));
                var containsPointer = RectTransformUtility.RectangleContainsScreenPoint(visual.Rect, pointerScreenPosition, camera);
                var distance = Vector2.Distance(pointerScreenPosition, center);
                if (containsPointer || distance <= DragSnapRadius)
                {
                    if (containsPointer || distance < bestDistance)
                    {
                        best = visual;
                        bestDistance = containsPointer ? 0f : distance;
                    }
                }

                visual.Image.color = visual.AvailableColor;
            }

            if (best == null)
            {
                return pointerScreenPosition;
            }

            best.Image.color = best.HoverColor;
            _dragSnapBoardSlotId = best.SlotId;
            return RectTransformUtility.WorldToScreenPoint(camera, best.Rect.TransformPoint(best.Rect.rect.center));
        }

        private void UpdateRuntimeDragIndicator(Vector2 targetScreenPosition)
        {
            if (_dragIndicatorRect == null || _dragArrowBody == null || _dragArrowHead == null)
            {
                return;
            }

            var camera = GetRuntimeDragCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragIndicatorRect, _dragArrowStartScreen, camera, out var startLocal)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragIndicatorRect, targetScreenPosition, camera, out var endLocal))
            {
                return;
            }

            var delta = endLocal - startLocal;
            var length = Mathf.Max(12f, delta.magnitude);
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _dragArrowBody.anchoredPosition = startLocal;
            _dragArrowBody.sizeDelta = new Vector2(length, 7f);
            _dragArrowBody.localRotation = Quaternion.Euler(0f, 0f, angle);
            _dragArrowHead.anchoredPosition = endLocal;
            _dragArrowHead.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool IsValidRuntimeDragBoardTarget(string boardSlotId)
        {
            if (string.IsNullOrWhiteSpace(boardSlotId) || Run == null)
            {
                return false;
            }

            if (_dragSource == "hand")
            {
                return IsForestGemHandCard(_dragHandIndex)
                    ? Run.boardUnits.Any(unit => unit.boardSlotId == boardSlotId)
                    : !Run.boardUnits.Any(unit => unit.boardSlotId == boardSlotId);
            }

            if (_dragSource == "board")
            {
                return boardSlotId != _dragBoardSlotId;
            }

            return false;
        }

        private Camera GetRuntimeDragCamera()
        {
            if (_dragCanvas != null && _dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return _dragEventCamera != null ? _dragEventCamera : Camera.main;
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
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请先选择祝福目标");
                return;
            }

            var before = CaptureUnitNumberSnapshots();
            var goldBefore = Run != null ? Run.gold : 0;
            ClearPendingSynthesisSfx();
            var success = _flow.SellHandUnit(index);
            var devourEvents = success ? _flow.ConsumeDevourShopEvents() : new List<DevourShopEventState>();
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                RuntimeSfxPlayer.PlaySell();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("无法出售手牌");
            }

            WriteLog(success ? $"已出售手牌第 {index + 1} 张。" : $"无法出售手牌第 {index + 1} 张。");
            RefreshView();
            ShowGoldChangeFeedback(goldBefore);
            PlayNumberChangeFeedback(before);
            PlayDevourFeedbackIfNeeded(devourEvents);
            PlayManageFeedbackIfNeeded(feedbackEvents);
        }

        private void SellBoardCard(int index)
        {
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请先选择祝福目标");
                return;
            }

            var before = CaptureUnitNumberSnapshots();
            var goldBefore = Run != null ? Run.gold : 0;
            var target = index >= 0 && index < Run.boardUnits.Count ? Run.boardUnits[index].boardSlotId : null;
            ClearPendingSynthesisSfx();
            var success = !string.IsNullOrWhiteSpace(target) && _flow.SellBoardUnit(target);
            var devourEvents = success ? _flow.ConsumeDevourShopEvents() : new List<DevourShopEventState>();
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                RuntimeSfxPlayer.PlaySell();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("无法出售单位");
            }

            WriteLog(success ? $"已出售棋盘第 {index + 1} 个单位。" : $"无法出售棋盘第 {index + 1} 个单位。");
            RefreshView();
            ShowGoldChangeFeedback(goldBefore);
            PlayNumberChangeFeedback(before);
            PlayDevourFeedbackIfNeeded(devourEvents);
            PlayManageFeedbackIfNeeded(feedbackEvents);
        }

        private void SellBoardSlot(string boardSlotId)
        {
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("请先选择祝福目标");
                return;
            }

            var before = CaptureUnitNumberSnapshots();
            var goldBefore = Run != null ? Run.gold : 0;
            ClearPendingSynthesisSfx();
            var success = !string.IsNullOrWhiteSpace(boardSlotId) && _flow.SellBoardUnit(boardSlotId);
            var devourEvents = success ? _flow.ConsumeDevourShopEvents() : new List<DevourShopEventState>();
            var feedbackEvents = success ? _flow.ConsumeManageFeedbackEvents() : null;
            if (success)
            {
                RuntimeSfxPlayer.PlaySell();
                PlayAbilitySfxIfNeeded();
                PlaySynthesisSfxIfNeeded();
            }
            else
            {
                RuntimeSfxPlayer.PlayError();
                ShowFloatingText("无法出售单位");
            }

            WriteLog(success ? $"已出售棋盘 {boardSlotId} 的单位。" : $"无法出售棋盘 {boardSlotId} 的单位。");
            RefreshView();
            ShowGoldChangeFeedback(goldBefore);
            PlayNumberChangeFeedback(before);
            PlayDevourFeedbackIfNeeded(devourEvents);
            PlayManageFeedbackIfNeeded(feedbackEvents);
        }

        private static void PlayFailureSfx(bool notEnoughGold)
        {
            if (notEnoughGold)
            {
                RuntimeSfxPlayer.PlayNotEnoughGold();
                return;
            }

            RuntimeSfxPlayer.PlayError();
        }

        private void ClearPendingSynthesisSfx()
        {
            _flow.ConsumeAbilityTriggerFlag();
            _flow.ConsumeSynthesisFlag();
            _flow.ConsumeDevourShopEvents();
            _flow.ConsumeManageFeedbackEvents();
        }

        private void PlayAbilitySfxIfNeeded()
        {
            if (_flow.ConsumeAbilityTriggerFlag())
            {
                RuntimeSfxPlayer.PlayAbilityTrigger();
            }
        }

        private void PlaySynthesisSfxIfNeeded()
        {
            if (_flow.ConsumeSynthesisFlag())
            {
                RuntimeSfxPlayer.PlaySynthesis();
            }
        }

        private bool IsGoldenHandCard(int index)
        {
            return Run != null
                && index >= 0
                && index < Run.handCards.Count
                && Run.handCards[index] != null
                && Run.handCards[index].isGolden;
        }

        private bool IsForestGemHandCard(int index)
        {
            return Run != null
                && index >= 0
                && index < Run.handCards.Count
                && ManageEventResolver.IsForestGemCard(Run.handCards[index]);
        }

        private bool IsTargetedEntryPowerHandCard(int index)
        {
            return Run != null
                && index >= 0
                && index < Run.handCards.Count
                && _flow.HasTargetedEntryPower(Run.handCards[index]);
        }

        private int CountForestGemCardsInHand()
        {
            return Run?.handCards?.Count(ManageEventResolver.IsForestGemCard) ?? 0;
        }

        private bool IsGoldDeployRewardOpen()
        {
            return _goldDeployRewardModal != null && _goldDeployRewardModal.activeSelf;
        }

        private void OpenGoldDeployRewardModal()
        {
            var choices = _flow.CreateGoldDeployRewardChoices(out _goldDeployRewardActualStar).ToList();
            if (choices.Count == 0)
            {
                WriteLog("金色上阵奖励没有可用卡池。");
                return;
            }

            EnsureGoldDeployRewardModal();
            _goldDeployRewardTitleLabel.text = "金色上阵奖励";
            _goldDeployRewardSubtitleLabel.text = $"选择 1 张 {_goldDeployRewardActualStar} 星卡牌加入手牌";
            ClearChildren(_goldDeployRewardOptionsRoot);

            foreach (var choice in choices)
            {
                CreateGoldDeployRewardChoice(choice);
            }

            _goldDeployRewardModal.SetActive(true);
            _goldDeployRewardModal.transform.SetAsLastSibling();
            RuntimeSfxPlayer.PlayAbilityTrigger();
            WriteLog($"金色卡牌上阵，触发 {_goldDeployRewardActualStar} 星奖励三选一。");
        }

        private void EnsureGoldDeployRewardModal()
        {
            if (_goldDeployRewardModal != null)
            {
                return;
            }

            var parent = runPanel != null ? runPanel.transform : transform;
            _goldDeployRewardModal = new GameObject("GoldDeployRewardModal", typeof(Image));
            _goldDeployRewardModal.transform.SetParent(parent, false);
            var modalRect = _goldDeployRewardModal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            _goldDeployRewardModal.GetComponent<Image>().color = new Color32(4, 3, 12, 210);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_goldDeployRewardModal.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1060f, 640f);
            panelRect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color32(30, 38, 70, 252);

            _goldDeployRewardTitleLabel = CreateAnchoredText(panel.transform, "Title", "金色上阵奖励", 38, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.94f));
            _goldDeployRewardTitleLabel.color = new Color32(255, 216, 107, 255);
            _goldDeployRewardSubtitleLabel = CreateAnchoredText(panel.transform, "Subtitle", string.Empty, 22, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.82f));

            var options = new GameObject("Options", typeof(GridLayoutGroup));
            options.transform.SetParent(panel.transform, false);
            var optionsRect = options.GetComponent<RectTransform>();
            optionsRect.anchorMin = new Vector2(0.11f, 0.19f);
            optionsRect.anchorMax = new Vector2(0.89f, 0.70f);
            optionsRect.offsetMin = Vector2.zero;
            optionsRect.offsetMax = Vector2.zero;
            var layout = options.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(221f, 286f);
            layout.spacing = new Vector2(48f, 0f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            _goldDeployRewardOptionsRoot = options.transform;

            var hint = CreateAnchoredText(panel.transform, "Hint", "奖励卡牌会加入手牌", 18, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.14f));
            hint.color = new Color32(214, 220, 232, 255);
            _goldDeployRewardModal.SetActive(false);
        }

        private void CreateGoldDeployRewardChoice(UnitDefinition definition)
        {
            var cardState = new UnitCardState
            {
                unitId = definition.id,
                name = definition.name,
                star = definition.star
            };
            var view = UnitCardView.CreateRuntimeInstance(_goldDeployRewardOptionsRoot);
            var rect = view.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(221f, 286f);
            var layout = view.GetComponent<LayoutElement>() ?? view.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 221f;
            layout.preferredHeight = 286f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
            view.Bind(definition, cardState, UnitCardPresentationMode.Grid, GetUnitCardRaceStyles());

            var button = view.GetComponent<Button>() ?? view.gameObject.AddComponent<Button>();
            button.targetGraphic = view.BackgroundImage != null ? view.BackgroundImage : view.GetComponent<Image>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => SelectGoldDeployReward(definition));

            var tooltip = view.gameObject.AddComponent<RuntimeUnitTooltip>();
            tooltip.Unit = cardState;
        }

        private void SelectGoldDeployReward(UnitDefinition definition)
        {
            var before = CaptureUnitNumberSnapshots();
            var success = _flow.ChooseGoldDeployReward(definition);
            if (!success)
            {
                RuntimeSfxPlayer.PlayError();
                WriteLog("无法领取金色上阵奖励：手牌已满。");
                ShowFloatingText("手牌已满");
                RefreshView();
                return;
            }

            _goldDeployRewardModal.SetActive(false);
            RuntimeSfxPlayer.PlayBuyCard();
            PlayAbilitySfxIfNeeded();
            PlaySynthesisSfxIfNeeded();
            WriteLog($"已选择金色上阵奖励：{definition.name}（{_goldDeployRewardActualStar} 星）。");
            RefreshView();
            PlayNumberChangeFeedback(before);
        }

        private static Text CreateAnchoredText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

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

        private void ShowFloatingText(string message, [CallerMemberName] string source = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            LogFloatingTextSource("FloatingHint", message, source);
            var parent = runPanel != null ? runPanel.transform : transform;
            var textObject = new GameObject("FloatingHint", typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 155f);
            rect.sizeDelta = new Vector2(520f, 72f);

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color32(255, 216, 107, 255);
            text.text = message;
            AddFloatingTextOutline(textObject);
            textObject.transform.SetAsLastSibling();
            StartCoroutine(FloatingTextRoutine(text, rect));
        }

        private static void LogFloatingTextSource(string channel, string message, string source)
        {
            Debug.Log($"[FloatingText] {channel} from {source ?? "unknown"}: {message}");
        }

        private static IEnumerator FloatingTextRoutine(Text text, RectTransform rect)
        {
            const float duration = 1.15f;
            var elapsed = 0f;
            var start = rect.anchoredPosition;
            var color = text.color;
            rect.localScale = Vector3.one;
            while (elapsed < duration && text != null && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + new Vector2(0f, Mathf.Lerp(0f, 74f, t));
                color.a = Mathf.Lerp(1f, 0f, t);
                text.color = color;
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }

        private List<UnitNumberSnapshot> CaptureUnitNumberSnapshots()
        {
            var snapshots = new List<UnitNumberSnapshot>();
            if (Run == null || ProphecyGameSession.Instance == null)
            {
                return snapshots;
            }

            for (var i = 0; i < Run.shopCards.Count; i += 1)
            {
                AddUnitNumberSnapshot(snapshots, Run.shopCards[i], "shop", i, null);
            }

            for (var i = 0; i < Run.handCards.Count; i += 1)
            {
                AddUnitNumberSnapshot(snapshots, Run.handCards[i], "hand", i, null);
            }

            foreach (var unit in Run.boardUnits)
            {
                AddUnitNumberSnapshot(snapshots, unit, "board", -1, unit.boardSlotId);
            }

            return snapshots;
        }

        private void AddUnitNumberSnapshot(List<UnitNumberSnapshot> snapshots, UnitCardState card, string zone, int index, string slotId)
        {
            if (card == null)
            {
                return;
            }

            var definition = ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            snapshots.Add(new UnitNumberSnapshot
            {
                Zone = zone,
                Index = index,
                SlotId = slotId,
                UnitId = card.unitId,
                Name = card.name,
                Golden = card.isGolden,
                Hp = (definition?.hp ?? 0) + card.shopBuffHp,
                Attack = (definition?.attack ?? 0) + card.shopBuffAttack + card.roundTempAttack,
                Defense = (definition?.defense ?? 0) + card.shopBuffDefense,
                Power = (definition?.power ?? 0) + card.shopBuffPower + card.roundTempPower,
                Speed = (definition?.speed ?? 0) + card.shopBuffSpeed,
                Morale = (definition?.morale ?? 0) + card.shopBuffMorale + card.roundTempMorale,
                ForestGems = card.forestGemsAttached,
                Count = ResolveCardCount(definition, card)
            });
        }

        private void PlayNumberChangeFeedback(List<UnitNumberSnapshot> before)
        {
            if (before == null || before.Count == 0)
            {
                return;
            }

            StartCoroutine(PlayNumberChangeFeedbackAfterLayout(before));
        }

        private IEnumerator PlayNumberChangeFeedbackAfterLayout(List<UnitNumberSnapshot> before)
        {
            yield return null;

            var after = CaptureUnitNumberSnapshots();
            var used = new bool[before.Count];
            foreach (var current in after)
            {
                var previousIndex = FindPreviousSnapshot(before, used, current);
                if (previousIndex < 0)
                {
                    continue;
                }

                used[previousIndex] = true;
                var previous = before[previousIndex];
                var message = FormatNumberChange(previous, current, out var positiveTotal);
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                var target = GetSnapshotRect(current);
                if (target == null)
                {
                    continue;
                }

                var color = positiveTotal >= 0
                    ? new Color32(116, 236, 154, 255)
                    : new Color32(255, 116, 116, 255);
                ShowUnitNumberFloatingText(target, message, color);
            }
        }

        private static int FindPreviousSnapshot(IReadOnlyList<UnitNumberSnapshot> before, bool[] used, UnitNumberSnapshot current)
        {
            var locationMatch = FindPreviousSnapshotByLocation(before, used, current);
            return locationMatch;
        }

        private static int FindPreviousSnapshotByLocation(IReadOnlyList<UnitNumberSnapshot> before, bool[] used, UnitNumberSnapshot current)
        {
            for (var i = 0; i < before.Count; i += 1)
            {
                if (used[i] || !SameUnitIdentity(before[i], current) || before[i].Zone != current.Zone)
                {
                    continue;
                }

                if (current.Zone == "board" && before[i].SlotId == current.SlotId)
                {
                    return i;
                }

                if (current.Zone != "board" && before[i].Index == current.Index)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool SameUnitIdentity(UnitNumberSnapshot left, UnitNumberSnapshot right)
        {
            return left != null
                && right != null
                && left.UnitId == right.UnitId
                && left.Name == right.Name
                && left.Golden == right.Golden;
        }

        private RectTransform GetSnapshotRect(UnitNumberSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            switch (snapshot.Zone)
            {
                case "shop":
                    return GetIndexedChildRect(shopCardRoot, snapshot.Index);
                case "hand":
                    return GetIndexedChildRect(handCardRoot, snapshot.Index);
                case "board":
                    return GetBoardSlotRect(snapshot.SlotId);
                default:
                    return null;
            }
        }

        private static RectTransform GetIndexedChildRect(Transform root, int index)
        {
            if (root == null || index < 0 || index >= root.childCount)
            {
                return null;
            }

            return root.GetChild(index) as RectTransform;
        }

        private static string FormatNumberChange(UnitNumberSnapshot before, UnitNumberSnapshot after, out int positiveTotal)
        {
            positiveTotal = 0;
            var parts = new List<string>();
            AddNumberChangePart(parts, "血", after.Hp - before.Hp, ref positiveTotal);
            AddNumberChangePart(parts, "攻", after.Attack - before.Attack, ref positiveTotal);
            AddNumberChangePart(parts, "防", after.Defense - before.Defense, ref positiveTotal);
            AddNumberChangePart(parts, "力", after.Power - before.Power, ref positiveTotal);
            AddNumberChangePart(parts, "速", after.Speed - before.Speed, ref positiveTotal);
            AddNumberChangePart(parts, "气", after.Morale - before.Morale, ref positiveTotal);
            AddNumberChangePart(parts, "◆", after.ForestGems - before.ForestGems, ref positiveTotal);
            return parts.Count == 0 ? null : string.Join("  ", parts);
        }

        private static int ResolveCardCount(UnitDefinition definition, UnitCardState card)
        {
            if (card == null)
            {
                return 0;
            }

            var startCount = definition != null
                ? Mathf.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1)
                : 1;
            return Mathf.Max(1, (card.baseCount > 0 ? card.baseCount : startCount) + card.roundTempCount);
        }

        private static void AddNumberChangePart(List<string> parts, string label, int delta, ref int positiveTotal)
        {
            if (delta == 0)
            {
                return;
            }

            positiveTotal += delta;
            parts.Add($"{label}{(delta > 0 ? "+" : string.Empty)}{delta}");
        }

        private void ShowUnitNumberFloatingText(RectTransform target, string message, Color color, [CallerMemberName] string source = null)
        {
            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            if (target == null || overlay == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            LogFloatingTextSource("UnitNumberFloatingText", message, source);
            var textObject = new GameObject("UnitNumberFloatingText", typeof(Text));
            textObject.transform.SetParent(overlay, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetCenterInOverlay(target, overlay) + new Vector2(0f, 38f);
            rect.sizeDelta = new Vector2(220f, 52f);

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.text = message;
            AddFloatingTextOutline(textObject);
            textObject.transform.SetAsLastSibling();
            StartCoroutine(FloatingTextRoutine(text, rect));
        }

        private void ShowGoldChangeFeedback(int beforeGold)
        {
            if (Run == null)
            {
                return;
            }

            var delta = Run.gold - beforeGold;
            if (delta <= 0)
            {
                return;
            }

            var root = GetUiSearchRoot();
            var target = FindDeepChild(root, "RoundLabelV2") as RectTransform
                ?? roundLabel?.rectTransform
                ?? goldLabel?.rectTransform;
            ShowUnitNumberFloatingText(target, $"金币+{delta}", new Color32(255, 216, 107, 255));
        }

        private void PlayDevourFeedbackIfNeeded(List<DevourShopEventState> devourEvents)
        {
            if (devourEvents == null || devourEvents.Count == 0)
            {
                return;
            }

            StartCoroutine(PlayDevourFeedbackRoutine(devourEvents));
        }

        private void PlayManageFeedbackIfNeeded(ManageFeedbackEventsState feedbackEvents)
        {
            if (feedbackEvents == null)
            {
                return;
            }

            var hasEvents =
                (feedbackEvents.forestGemGiftEvents != null && feedbackEvents.forestGemGiftEvents.Count > 0)
                || (feedbackEvents.evolveEvents != null && feedbackEvents.evolveEvents.Count > 0)
                || (feedbackEvents.countGainEvents != null && feedbackEvents.countGainEvents.Count > 0)
                || (feedbackEvents.entryEffectEvents != null && feedbackEvents.entryEffectEvents.Count > 0)
                || (feedbackEvents.handAddEvents != null && feedbackEvents.handAddEvents.Count > 0)
                || (feedbackEvents.attackChangeEvents != null && feedbackEvents.attackChangeEvents.Count > 0)
                || (feedbackEvents.shopBuffEvents != null && feedbackEvents.shopBuffEvents.Count > 0);
            if (!hasEvents)
            {
                return;
            }

            PrepareHandAddFeedback(feedbackEvents.handAddEvents);
            StartCoroutine(PlayManageFeedbackRoutine(feedbackEvents));
        }

        private void PrepareHandAddFeedback(List<HandAddEventState> handEvents)
        {
            if (handEvents == null)
            {
                return;
            }

            foreach (var handEvent in handEvents)
            {
                var target = handEvent == null ? null : GetIndexedChildRect(handCardRoot, handEvent.handIndex);
                if (target == null)
                {
                    continue;
                }

                var group = EnsureCanvasGroup(target);
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        private static ManageFeedbackEventsState FilterForestGemUseFeedback(ManageFeedbackEventsState feedbackEvents)
        {
            if (feedbackEvents == null)
            {
                return null;
            }

            return new ManageFeedbackEventsState
            {
                forestGemGiftEvents = feedbackEvents.forestGemGiftEvents?
                    .Where(item => item == null || item.sourceName != ManageEventResolver.ForestGemCardName)
                    .ToList() ?? new List<ForestGemGiftEventState>(),
                evolveEvents = feedbackEvents.evolveEvents ?? new List<UnitEvolveEventState>(),
                countGainEvents = new List<CountGainEventState>(),
                entryEffectEvents = feedbackEvents.entryEffectEvents ?? new List<EntryEffectEventState>(),
                handAddEvents = feedbackEvents.handAddEvents ?? new List<HandAddEventState>(),
                attackChangeEvents = feedbackEvents.attackChangeEvents ?? new List<AttackChangeEventState>(),
                shopBuffEvents = feedbackEvents.shopBuffEvents ?? new List<ShopBuffEventState>()
            };
        }

        private IEnumerator PlayManageFeedbackRoutine(ManageFeedbackEventsState feedbackEvents)
        {
            yield return null;
            yield return new WaitForSeconds(0.18f);

            foreach (var entryEvent in feedbackEvents.entryEffectEvents ?? new List<EntryEffectEventState>())
            {
                PlayEntryEffectFeedback(entryEvent);
                yield return new WaitForSeconds(0.08f);
            }

            foreach (var handEvent in feedbackEvents.handAddEvents ?? new List<HandAddEventState>())
            {
                yield return PlayHandAddFeedback(handEvent);
                yield return new WaitForSeconds(0.05f);
            }

            foreach (var giftEvent in feedbackEvents.forestGemGiftEvents ?? new List<ForestGemGiftEventState>())
            {
                yield return PlayForestGemGiftFeedback(giftEvent);
                yield return new WaitForSeconds(0.04f);
            }

            foreach (var evolveEvent in feedbackEvents.evolveEvents ?? new List<UnitEvolveEventState>())
            {
                PlayEvolveFeedback(evolveEvent);
                yield return new WaitForSeconds(0.14f);
            }

            foreach (var countEvent in feedbackEvents.countGainEvents ?? new List<CountGainEventState>())
            {
                PlayCountGainFeedback(countEvent);
                yield return new WaitForSeconds(0.08f);
            }

            foreach (var attackEvent in feedbackEvents.attackChangeEvents ?? new List<AttackChangeEventState>())
            {
                PlayAttackChangeFeedback(attackEvent);
                yield return new WaitForSeconds(0.08f);
            }

            foreach (var buffEvent in feedbackEvents.shopBuffEvents ?? new List<ShopBuffEventState>())
            {
                yield return PlayShopBuffFeedback(buffEvent);
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator PlayForestGemGiftFeedback(ForestGemGiftEventState giftEvent)
        {
            if (giftEvent == null || giftEvent.amount <= 0)
            {
                yield break;
            }

            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            var target = GetBoardSlotRect(giftEvent.targetSlotId);
            if (overlay == null || target == null)
            {
                ShowFloatingText($"{giftEvent.targetName} 获得密林宝钻 +{giftEvent.amount}");
                yield break;
            }

            var source = GetBoardSlotRect(giftEvent.sourceSlotId) ?? target;
            var start = GetCenterInOverlay(source, overlay);
            var end = GetCenterInOverlay(target, overlay);
            var gem = CreateFeedbackIcon(overlay, "宝石", new Vector2(34f, 34f), start);
            if (gem == null)
            {
                yield break;
            }

            StartCoroutine(PulseTransform(target, 1.1f, 0.22f));
            var rect = gem.rectTransform;
            var mid = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, 48f);
            var elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = Bezier(start, mid, end, Mathf.SmoothStep(0f, 1f, t));
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
                yield return null;
            }

            if (gem != null)
            {
                Destroy(gem.gameObject);
            }

            var targetImage = target.GetComponent<Image>();
            if (targetImage != null)
            {
                StartCoroutine(FlashImage(targetImage, new Color32(77, 202, 255, 255), 0.32f));
            }

            ShowFloatingText($"{giftEvent.targetName} ◆+{giftEvent.amount}");
        }

        private void PlayEvolveFeedback(UnitEvolveEventState evolveEvent)
        {
            if (evolveEvent == null)
            {
                return;
            }

            var target = GetBoardSlotRect(evolveEvent.slotId);
            if (target != null)
            {
                var image = target.GetComponent<Image>();
                if (image != null)
                {
                    StartCoroutine(FlashImage(image, new Color32(255, 220, 96, 255), 0.5f));
                }

                StartCoroutine(PulseTransform(target, 1.22f, 0.32f));
            }

            ShowFloatingText($"{evolveEvent.oldName} 进阶为 {evolveEvent.newName}");
        }

        private void PlayEntryEffectFeedback(EntryEffectEventState entryEvent)
        {
            if (entryEvent == null)
            {
                return;
            }

            var target = GetBoardSlotRect(entryEvent.targetSlotId);
            if (target != null)
            {
                var image = target.GetComponent<Image>();
                if (image != null)
                {
                    StartCoroutine(FlashImage(image, new Color32(94, 214, 255, 255), 0.28f));
                }

                StartCoroutine(PulseTransform(target, 1.1f, 0.22f));
                StartCoroutine(PlayEntryEffectBurst(target));
                ShowUnitNumberFloatingText(target, "入场！", new Color32(118, 232, 255, 255));
                return;
            }

            ShowFloatingText($"{entryEvent.targetName} 入场！");
        }

        private IEnumerator PlayHandAddFeedback(HandAddEventState handEvent)
        {
            if (handEvent == null)
            {
                yield break;
            }

            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            var source = GetBoardSlotRect(handEvent.sourceSlotId);
            var target = GetIndexedChildRect(handCardRoot, handEvent.handIndex);
            if (overlay == null || source == null || target == null)
            {
                RevealHandAddTarget(target);
                ShowFloatingText($"{handEvent.sourceName} 获得 {handEvent.unitName}");
                yield break;
            }

            var targetGroup = EnsureCanvasGroup(target);
            targetGroup.alpha = 0f;
            targetGroup.blocksRaycasts = false;

            var sourceImage = source.GetComponent<Image>();
            if (sourceImage != null)
            {
                StartCoroutine(FlashImage(sourceImage, new Color32(118, 232, 255, 255), 0.24f));
            }

            StartCoroutine(PulseTransform(source, 1.06f, 0.18f));

            var start = GetCenterInOverlay(source, overlay);
            var end = GetCenterInOverlay(target, overlay);
            var iconObject = new GameObject("HandAddLink", typeof(Image));
            iconObject.transform.SetParent(overlay, false);
            var rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(42f, 42f);
            rect.anchoredPosition = start;

            var image = iconObject.GetComponent<Image>();
            if (handEvent.unitId == ManageEventResolver.ForestGemCardId)
            {
                RuntimeFeatureIconCache.ApplyTo(image, "宝石");
            }
            else
            {
                RuntimeUnitIconCache.ApplyTo(image, string.IsNullOrWhiteSpace(handEvent.unitName) ? handEvent.unitId : handEvent.unitName);
            }
            image.color = Color.white;
            image.raycastTarget = false;
            iconObject.transform.SetAsLastSibling();

            var mid = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, 54f);
            const float duration = 0.4f;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = Bezier(start, mid, end, eased);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.62f, eased);
                yield return null;
            }

            if (iconObject != null)
            {
                Destroy(iconObject);
            }

            RevealHandAddTarget(target);
            StartCoroutine(PulseTransform(target, 1.08f, 0.2f));
            ShowUnitNumberFloatingText(target, handEvent.unitName, new Color32(118, 232, 255, 255));
        }

        private static void RevealHandAddTarget(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            var group = target.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null)
            {
                return null;
            }

            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void PlayCountGainFeedback(CountGainEventState countEvent)
        {
            if (countEvent == null || countEvent.amount <= 0)
            {
                return;
            }

            var target = GetBoardSlotRect(countEvent.targetSlotId);
            if (target != null)
            {
                var source = GetBoardSlotRect(countEvent.sourceSlotId);
                if (source != null && source != target)
                {
                    StartCoroutine(PlayLinkedCountGainFeedback(source, target, countEvent.amount));
                    return;
                }

                PlayCountGainArriveFeedback(target, countEvent.amount);
                return;
            }

            ShowFloatingText($"{countEvent.targetName} 数量+{countEvent.amount}");
        }

        private IEnumerator PlayLinkedCountGainFeedback(RectTransform source, RectTransform target, int amount)
        {
            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            if (source == null || target == null || overlay == null)
            {
                PlayCountGainArriveFeedback(target, amount);
                yield break;
            }

            var sourceImage = source.GetComponent<Image>();
            if (sourceImage != null)
            {
                StartCoroutine(FlashImage(sourceImage, new Color32(118, 232, 255, 255), 0.24f));
            }

            StartCoroutine(PulseTransform(source, 1.05f, 0.16f));

            var start = GetCenterInOverlay(source, overlay);
            var end = GetCenterInOverlay(target, overlay);
            var orbObject = new GameObject("CountGainLink", typeof(Image));
            orbObject.transform.SetParent(overlay, false);
            var rect = orbObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(24f, 24f);
            rect.anchoredPosition = start;

            var image = orbObject.GetComponent<Image>();
            image.color = new Color32(116, 236, 154, 230);
            image.raycastTarget = false;
            orbObject.transform.SetAsLastSibling();

            var mid = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, 42f);
            const float duration = 0.34f;
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                rect.anchoredPosition = Bezier(start, mid, end, eased);
                rect.localScale = Vector3.one * Mathf.Lerp(0.85f, 0.55f, eased);
                yield return null;
            }

            if (orbObject != null)
            {
                Destroy(orbObject);
            }

            PlayCountGainArriveFeedback(target, amount);
        }

        private void PlayCountGainArriveFeedback(RectTransform target, int amount)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                StartCoroutine(FlashImage(image, new Color32(116, 236, 154, 255), 0.24f));
            }

            StartCoroutine(PulseTransform(target, 1.08f, 0.2f));
            ShowUnitNumberFloatingText(target, $"数量+{amount}", new Color32(116, 236, 154, 255));
        }

        private void PlayAttackChangeFeedback(AttackChangeEventState attackEvent)
        {
            if (attackEvent == null || attackEvent.amount == 0)
            {
                return;
            }

            var target = GetBoardSlotRect(attackEvent.targetSlotId);
            var message = $"攻击{(attackEvent.amount > 0 ? "+" : string.Empty)}{attackEvent.amount}";
            var color = attackEvent.amount > 0
                ? new Color32(116, 236, 154, 255)
                : new Color32(255, 116, 116, 255);
            if (target != null)
            {
                StartCoroutine(PulseTransform(target, 1.06f, 0.18f));
                ShowUnitNumberFloatingText(target, message, color);
                return;
            }

            ShowFloatingText($"{attackEvent.targetName} {message}");
        }

        private IEnumerator PlayShopBuffFeedback(ShopBuffEventState buffEvent)
        {
            var count = buffEvent != null ? Mathf.Max(0, buffEvent.count) : 0;
            if (buffEvent == null || count <= 0 || buffEvent.shopIndices == null)
            {
                yield break;
            }

            foreach (var index in buffEvent.shopIndices)
            {
                var slot = GetShopSlotRect(index);
                if (slot == null)
                {
                    continue;
                }

                var image = slot.GetComponent<Image>();
                if (image != null)
                {
                    StartCoroutine(FlashImage(image, new Color32(92, 205, 126, 255), 0.28f));
                }

                StartCoroutine(PulseTransform(slot, 1.05f, 0.2f));
                ShowFloatingText($"商店卡 数量+{count}");
                yield return new WaitForSeconds(0.03f);
            }
        }

        private IEnumerator PlayDevourFeedbackRoutine(List<DevourShopEventState> devourEvents)
        {
            yield return null;
            yield return new WaitForSeconds(0.36f);

            foreach (var devourEvent in devourEvents)
            {
                if (devourEvent == null || devourEvent.devouredCard == null)
                {
                    continue;
                }

                yield return PlayDevourFeedbackEvent(devourEvent);
                yield return new WaitForSeconds(0.08f);
            }
        }

        private IEnumerator PlayDevourFeedbackEvent(DevourShopEventState devourEvent)
        {
            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            if (overlay == null)
            {
                yield break;
            }

            var source = GetShopSlotRect(devourEvent.shopIndex);
            var target = GetBoardSlotRect(devourEvent.devourerSlotId);
            if (source == null || target == null)
            {
                var fallbackCount = ResolveDevourGainedCount(devourEvent);
                RuntimeSfxPlayer.PlayDevour();
                ShowFloatingText($"{devourEvent.devourerName} 吞噬成功 数量+{fallbackCount}");
                yield break;
            }

            var sourceImage = source.GetComponent<Image>();
            var targetImage = target.GetComponent<Image>();
            if (sourceImage != null)
            {
                StartCoroutine(FlashImage(sourceImage, new Color32(174, 82, 116, 255), 0.34f));
            }

            if (targetImage != null)
            {
                StartCoroutine(FlashImage(targetImage, new Color32(138, 68, 126, 255), 0.44f));
            }

            StartCoroutine(PulseTransform(target, 1.08f, 0.28f));
            RuntimeSfxPlayer.PlayDevour();

            var start = GetCenterInOverlay(source, overlay);
            var end = GetCenterInOverlay(target, overlay);
            var sourceSize = source.rect.size;
            if (sourceSize.x < 20f || sourceSize.y < 20f)
            {
                sourceSize = new Vector2(126f, 150f);
            }

            var definition = ProphecyGameSession.Instance.Data.FindUnit(devourEvent.devouredCard.unitId);
            var ghost = UnitCardView.CreateRuntimeInstance(overlay);
            var ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.anchoredPosition = start;
            ghostRect.sizeDelta = new Vector2(Mathf.Clamp(sourceSize.x, 96f, 150f), Mathf.Clamp(sourceSize.y, 118f, 170f));
            ghost.Bind(definition, devourEvent.devouredCard, UnitCardPresentationMode.Grid, GetUnitCardRaceStyles(), null, false);
            ghost.transform.SetAsLastSibling();

            var group = ghost.gameObject.AddComponent<CanvasGroup>();
            var mid = Vector2.Lerp(start, end, 0.52f) + new Vector2(0f, 56f);
            const float duration = 0.46f;
            var elapsed = 0f;
            while (elapsed < duration && ghostRect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                ghostRect.anchoredPosition = Bezier(start, mid, end, eased);
                ghostRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.24f, eased);
                ghostRect.Rotate(0f, 0f, Time.deltaTime * 220f);
                group.alpha = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
                yield return null;
            }

            if (ghost != null)
            {
                Destroy(ghost.gameObject);
            }

            StartCoroutine(PulseTransform(target, 1.16f, 0.18f));
            ShowFloatingText($"{devourEvent.devourerName} 吞噬成功 数量+{ResolveDevourGainedCount(devourEvent)}");
        }

        private RectTransform GetShopSlotRect(int shopIndex)
        {
            if (shopCardRoot == null || shopIndex < 0 || shopIndex >= shopCardRoot.childCount)
            {
                return null;
            }

            return shopCardRoot.GetChild(shopIndex) as RectTransform;
        }

        private RectTransform GetBoardSlotRect(string boardSlotId)
        {
            if (boardCardRoot == null || string.IsNullOrWhiteSpace(boardSlotId))
            {
                return null;
            }

            var child = FindChildRecursive(boardCardRoot, "BoardSlot_" + boardSlotId);
            return child as RectTransform;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i += 1)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Vector2 GetCenterInOverlay(RectTransform source, RectTransform overlay)
        {
            if (source == null || overlay == null)
            {
                return Vector2.zero;
            }

            return overlay.InverseTransformPoint(source.TransformPoint(source.rect.center));
        }

        private static Vector2 Bezier(Vector2 start, Vector2 middle, Vector2 end, float t)
        {
            var a = Vector2.Lerp(start, middle, t);
            var b = Vector2.Lerp(middle, end, t);
            return Vector2.Lerp(a, b, t);
        }

        private static int GetEffectiveCount(UnitCardState card)
        {
            if (card == null)
            {
                return 0;
            }

            var definition = ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            return ResolveCardCount(definition, card);
        }

        private static int ResolveDevourGainedCount(DevourShopEventState devourEvent)
        {
            if (devourEvent == null)
            {
                return 0;
            }

            return devourEvent.gainedCount > 0
                ? devourEvent.gainedCount
                : GetEffectiveCount(devourEvent.devouredCard);
        }

        private string FormatPendingBattleRewardFeedback()
        {
            if (Run == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var rewards = Run.pendingBattleRewards;
            if (rewards != null)
            {
                if (rewards.nextRoundGold > 0)
                {
                    parts.Add($"金币+{rewards.nextRoundGold}");
                }

                if (rewards.nextRoundShopBuffAttack > 0)
                {
                    parts.Add($"商店攻+{rewards.nextRoundShopBuffAttack}");
                }

                var discoverCount = rewards.discoverFaithRewards?.Sum(reward => Mathf.Max(0, reward.count)) ?? 0;
                if (discoverCount > 0)
                {
                    parts.Add($"发现+{discoverCount}");
                }
            }

            var forestGems = Run.boardUnits.Sum(unit => Mathf.Max(0, unit.pendingNextRoundForestGems));
            var tempAttack = Run.boardUnits.Sum(unit => Mathf.Max(0, unit.pendingNextRoundTempAttack));
            var tempPower = Run.boardUnits.Sum(unit => Mathf.Max(0, unit.pendingNextRoundTempPower));
            var evolves = Run.boardUnits.Count(unit => !string.IsNullOrWhiteSpace(unit.pendingNextRoundEvolveTo));
            if (forestGems > 0)
            {
                parts.Add($"密林宝钻+{forestGems}");
            }

            if (tempAttack > 0)
            {
                parts.Add($"临时攻+{tempAttack}");
            }

            if (tempPower > 0)
            {
                parts.Add($"临时力+{tempPower}");
            }

            if (evolves > 0)
            {
                parts.Add($"进阶×{evolves}");
            }

            return parts.Count == 0 ? string.Empty : "战斗奖励：" + string.Join("  ", parts);
        }

        private static Image CreateFeedbackIcon(RectTransform parent, string iconName, Vector2 size, Vector2 position)
        {
            if (parent == null)
            {
                return null;
            }

            var obj = new GameObject("FeedbackIcon", typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = obj.GetComponent<Image>();
            RuntimeFeatureIconCache.ApplyTo(image, iconName);
            image.raycastTarget = false;
            obj.transform.SetAsLastSibling();
            return image;
        }

        private static IEnumerator FlashImage(Image image, Color flashColor, float duration)
        {
            if (image == null)
            {
                yield break;
            }

            var original = image.color;
            var elapsed = 0f;
            while (elapsed < duration && image != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                image.color = Color.Lerp(flashColor, original, t);
                yield return null;
            }

            if (image != null)
            {
                image.color = original;
            }
        }

        private IEnumerator PlayEntryEffectBurst(RectTransform target)
        {
            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            if (target == null || overlay == null)
            {
                yield break;
            }

            var burstObject = new GameObject("EntryEffectBurst", typeof(Image));
            burstObject.transform.SetParent(overlay, false);
            var rect = burstObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = GetCenterInOverlay(target, overlay);
            rect.sizeDelta = new Vector2(68f, 68f);

            var image = burstObject.GetComponent<Image>();
            image.color = new Color32(118, 232, 255, 120);
            image.raycastTarget = false;
            burstObject.transform.SetAsLastSibling();

            var elapsed = 0f;
            const float duration = 0.34f;
            while (elapsed < duration && rect != null && image != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.65f, t);
                image.color = Color.Lerp(new Color32(118, 232, 255, 120), new Color32(118, 232, 255, 0), t);
                yield return null;
            }

            if (burstObject != null)
            {
                Destroy(burstObject);
            }
        }

        private static IEnumerator PulseTransform(RectTransform rect, float scale, float duration)
        {
            if (rect == null)
            {
                yield break;
            }

            var original = rect.localScale;
            var target = original * scale;
            var half = duration * 0.5f;
            var elapsed = 0f;
            while (elapsed < half && rect != null)
            {
                elapsed += Time.deltaTime;
                rect.localScale = Vector3.Lerp(original, target, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half && rect != null)
            {
                elapsed += Time.deltaTime;
                rect.localScale = Vector3.Lerp(target, original, Mathf.Clamp01(elapsed / half));
                yield return null;
            }

            if (rect != null)
            {
                rect.localScale = original;
            }
        }

        public void StartBattle()
        {
            if (!string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                RuntimeSfxPlayer.PlayError();
                WriteLog("请先选择祝福目标。");
                ShowFloatingText("请先选择祝福目标");
                RefreshView();
                return;
            }

            if (IsGoldDeployRewardOpen())
            {
                RuntimeSfxPlayer.PlayError();
                WriteLog("请先选择金色上阵奖励。");
                ShowFloatingText("请先选择奖励");
                RefreshView();
                return;
            }

            if (_battlePlaybackRunning)
            {
                return;
            }

            StartCoroutine(PlayBattleStage());
        }

        public void ToggleRealtimeBattlePreview()
        {
            useRealtimeBattlePreview = !useRealtimeBattlePreview;
            WriteLog(useRealtimeBattlePreview ? "实时战斗预览已开启，结算仍使用稳定系统。" : "实时战斗预览已关闭。");
            Debug.Log($"[ProphecyCentury] Realtime battle preview: {(useRealtimeBattlePreview ? "ON" : "OFF")}");
            RefreshView();
        }

        private IEnumerator PlaySmallMerchantChaseTest()
        {
            _battlePlaybackRunning = true;
            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (runPanel != null)
            {
                runPanel.SetActive(true);
            }

            ClearChildren(battlePlayerRoot);
            ClearChildren(battleEnemyRoot);
            DisableLayout(battlePlayerRoot);
            DisableLayout(battleEnemyRoot);
            ClearBattleFieldRoot();
            ShowBattleStage();
            SetBattleStageProgress(0f);

            var smallMerchant = ProphecyGameSession.Instance.Data.FindUnit("small_merchant");
            var enemies = ProphecyGameSession.Instance.Data.Units
                .Where(unit => unit != null && unit.id != "small_merchant")
                .ToList();
            var enemy = enemies.Count > 0 ? enemies[Random.Range(0, enemies.Count)] : smallMerchant;
            if (smallMerchant == null || enemy == null)
            {
                SetBattleStageText("追击测试失败", "找不到小商人或敌人数据。");
                yield return new WaitForSeconds(2.5f);
                _battlePlaybackRunning = false;
                ShowTitle();
                yield break;
            }

            var fieldRoot = CreateBattleFieldRoot();
            const int testHp = 999999;
            var merchantSnapshot = CreateChaseTestSnapshot(smallMerchant, "2-2", testHp);
            var enemySnapshot = CreateChaseTestSnapshot(enemy, "2-2", testHp);
            var merchantView = CreateBattleStagePositionedUnit(fieldRoot, merchantSnapshot, true);
            var enemyView = CreateBattleStagePositionedUnit(fieldRoot, enemySnapshot, false);
            if (merchantView == null || enemyView == null)
            {
                SetBattleStageText("追击测试失败", "战场单位创建失败。");
                yield return new WaitForSeconds(2.5f);
                _battlePlaybackRunning = false;
                ShowTitle();
                yield break;
            }

            var chance = CalculateMoraleExtraChance(merchantSnapshot.Morale);
            var expected = chance > 0f ? 1f / chance : 0f;
            var intro = $"小商人士气 {merchantSnapshot.Morale}，追击概率 {chance:P0}，理论平均 {expected:0.##} 次攻击触发 1 次。\n敌人：{enemy.name}，双方 HP={testHp}";
            var floatingTexts = new List<BattleFloatingTextView>();
            SetBattleStageText("小商人追击测试", intro);
            yield return new WaitForSeconds(1f);

            var attackCount = 0;
            var triggered = false;
            const int maxAttempts = 500;
            while (attackCount < maxAttempts && !triggered)
            {
                attackCount += 1;
                SetBattleStageProgress(Mathf.Clamp01(attackCount / Mathf.Max(1f, expected * 2f)));
                SetBattleStageText(
                    "小商人追击测试",
                    $"{intro}\n第 {attackCount} 次攻击中...");
                yield return PulseAttacker(merchantView, 0.16f);
                AddFloatingText($"攻击 {attackCount}", enemyView.Rect.anchoredPosition + new Vector2(0f, 54f), Color.white, 18, floatingTexts);
                yield return FlashTarget(enemyView, new Color32(210, 64, 64, 255), 0.08f);

                if (Random.value < chance)
                {
                    triggered = true;
                    SetBattleStageProgress(1f);
                    AddFloatingText("士气高涨！", merchantView.Rect.anchoredPosition + new Vector2(0f, 86f), new Color32(255, 218, 96, 255), 28, floatingTexts);
                    yield return PulseAttacker(merchantView, 0.22f);
                    SetBattleStageText(
                        "追击触发",
                        $"{intro}\n第 {attackCount} 次攻击触发追击。\n本次实测平均：{attackCount} 次攻击 / 1 次追击。");
                }
                else
                {
                    yield return UpdateTestFloatingTexts(floatingTexts, 0.18f);
                }
            }

            if (!triggered)
            {
                SetBattleStageText(
                    "追击测试结束",
                    $"{intro}\n连续 {maxAttempts} 次攻击未触发，属于极低概率情况。");
            }

            yield return UpdateTestFloatingTexts(floatingTexts, 5f);
            ClearBattleFieldRoot();
            _battlePlaybackRunning = false;
            ShowTitle();
        }

        private static IEnumerator UpdateTestFloatingTexts(List<BattleFloatingTextView> floatingTexts, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                yield return null;
            }
        }

        private BattleUnitSnapshot CreateChaseTestSnapshot(UnitDefinition definition, string slotId, int hp)
        {
            var hpPerUnit = Mathf.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp > 0 ? definition.hp : 1);
            var currentCount = Mathf.Max(1, Mathf.CeilToInt(hp / (float)hpPerUnit));
            return new BattleUnitSnapshot
            {
                UnitId = definition.id,
                Name = definition.name,
                Star = Mathf.Max(1, definition.star),
                SlotId = slotId,
                MaxHp = hp,
                CurrentHp = hp,
                BaseCount = currentCount,
                CurrentCount = currentCount,
                MaxCount = currentCount,
                HpPerUnit = hpPerUnit,
                CurrentTotalHp = hp,
                Attack = Mathf.Max(1, definition.attack),
                Defense = Mathf.Max(0, definition.defense),
                Power = Mathf.Max(1, definition.power),
                DamageMin = Mathf.Max(1, definition.damageMin),
                DamageMax = Mathf.Max(Mathf.Max(1, definition.damageMin), definition.damageMax),
                Speed = Mathf.Max(1, definition.speed),
                Luck = Mathf.Max(0, definition.luck),
                Morale = Mathf.Max(0, definition.morale),
                Range = Mathf.Max(1f, definition.range),
                Size = Mathf.Max(20, definition.size),
                AttackInterval = Mathf.Max(0.2f, definition.attackInterval)
            };
        }

        private static float CalculateMoraleExtraChance(int morale)
        {
            var rate = ProphecyGameSession.Instance.Data.Config?.moraleExtraAttackRate ?? 0.08f;
            return Mathf.Min(0.95f, Mathf.Max(0f, morale * Mathf.Max(0f, rate)));
        }

        private IEnumerator PlayBattleStage()
        {
            _battlePlaybackRunning = true;
            var hpBeforeBattle = Run != null ? Run.playerHp : 0;
            var roundEndGoldBefore = Run != null ? Run.gold : 0;
            var previousForestGems = CountForestGemCardsInHand();
            var roundEndBefore = CaptureUnitNumberSnapshots();
            _flow.ResolveRoundEndBeforeBattle();
            var roundEndFeedback = _flow.ConsumeManageFeedbackEvents();
            PlayAbilitySfxIfNeeded();
            PlaySynthesisSfxIfNeeded();
            var gainedForestGems = CountForestGemCardsInHand() - previousForestGems;
            var roundEndLine = gainedForestGems > 0
                ? $"回合结束效果已结算，密林宝钻 +{gainedForestGems}。"
                : "回合结束效果已结算。";
            WriteLog(roundEndLine);
            RefreshView();
            ShowGoldChangeFeedback(roundEndGoldBefore);
            PlayNumberChangeFeedback(roundEndBefore);
            PlayManageFeedbackIfNeeded(roundEndFeedback);
            yield return PlayBattleStartCountdown();

            _battlePlayerPositionOverrides.Clear();
            _flow.SetBattlePhase();
            ShowBattleStage();
            SetBattleStageProgress(0f);
            var preview = _battleStub.CreatePreview(Run);
            var previewPlayerScore = preview.PlayerScore;
            var previewEnemyScore = preview.EnemyScore;
            var setupResult = CreateBattlePreviewStageResult(preview);
            var unitViews = RebuildBattleStagePlaybackUnits(setupResult);
            EnableBattleSetupDragging(unitViews);
            SetBattleStageText("战斗准备", $"我方战力 {previewPlayerScore}，敌方战力 {previewEnemyScore}\n可拖动我方单位调整本场临时站位，点击开始行动后双方才会移动和攻击。");
            yield return WaitForBattleStartAction();

            var authoritativeResult = _battleStub.Resolve(Run);
            WriteBattleTurnDebugLog(authoritativeResult);
            SetPlayerHpDisplay(hpBeforeBattle);
            var visualResult = authoritativeResult;
            useRealtimeBattlePreview = false;

            var result = visualResult;
            _battleSetupDraggingEnabled = false;
            unitViews = RebuildBattleStagePlaybackUnits(setupResult);
            yield return PlayVisualTurnBattle(unitViews, result, $"我方战力 {previewPlayerScore}，敌方战力 {previewEnemyScore}");

            var settleBefore = CaptureUnitNumberSnapshots();
            var settleGoldBefore = Run != null ? Run.gold : 0;
            var battleRewardFeedback = FormatPendingBattleRewardFeedback();
            result = authoritativeResult;
            if (!result.Victory && result.HpDelta < 0)
            {
                yield return PlayWinnerStarsToPlayerHp(unitViews.Values.Distinct().Where(unit => unit != null && !unit.PlayerSide && !unit.Dead).ToList(), hpBeforeBattle, Run.playerHp);
            }

            _flow.FinishBattlePhase();
            _flow.ResolveBattleOutcome(result);
            RuntimeSfxPlayer.PlayBattleResult(result.Victory);
            result = visualResult;
            SetBattleStageProgress(1f);
            ClearBattleFieldRoot();
            RebuildBattleStageResultUnits(result);
            SetBattleStageText(result.Victory ? "胜利" : "失败", FormatBattleStageResult(result));
            WriteLog(result.Summary);
            yield return new WaitForSeconds(2.25f);

            if (battleStagePanel != null)
            {
                battleStagePanel.SetActive(false);
            }

            SetBattlePlaybackSpeedControlsVisible(false);
            SetBattleStartActionButtonVisible(false);
            _battleSetupDraggingEnabled = false;
            _battlePlayerPositionOverrides.Clear();
            RestoreOperationalUiAfterBattle();
            _battlePlaybackRunning = false;
            RefreshView();
            ShowGoldChangeFeedback(settleGoldBefore);
            PlayNumberChangeFeedback(settleBefore);
            if (!string.IsNullOrWhiteSpace(battleRewardFeedback))
            {
                ShowFloatingText(battleRewardFeedback);
            }
        }

        private IEnumerator PlayVisualRealtimeBattle(Dictionary<string, BattleStageUnitView> views, BattleStubResult result, string openingLine)
        {
            var uniqueViews = views.Values.Distinct().ToList();
            var summonEvents = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind == "summon")
                .OrderBy(item => item.Time)
                .ToList();
            var controlEvents = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind == "control")
                .OrderBy(item => item.Time)
                .ToList();
            var pendingMoraleExtraEvents = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind == "morale_extra")
                .OrderBy(item => item.Time)
                .ToList();
            var pendingLuckyCritEvents = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind == "lucky_crit")
                .OrderBy(item => item.Time)
                .ToList();
            var pendingCriticalDamageEvents = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind == "critical_damage")
                .OrderBy(item => item.Time)
                .ToList();
            var summonIndex = 0;
            var controlIndex = 0;
            var luckyCritIndex = 0;
            var criticalIndex = 0;
            var projectiles = new List<BattleProjectileView>();
            var floatingTexts = new List<BattleFloatingTextView>();
            var bursts = new List<BattleEffectBurstView>();
            var rollingLines = new List<string> { openingLine };
            var elapsed = 0f;
            const float maxBattlePlaybackSeconds = 40f;
            _visualBattleActionPauseRemaining = 0f;

            while (elapsed < maxBattlePlaybackSeconds && uniqueViews.Any(unit => unit.PlayerSide && !unit.Dead) && uniqueViews.Any(unit => !unit.PlayerSide && !unit.Dead))
            {
                while (summonIndex < summonEvents.Count && summonEvents[summonIndex].Time <= elapsed)
                {
                    var summoned = CreateSummonFromEvent(summonEvents[summonIndex], views, uniqueViews, floatingTexts);
                    if (summoned != null)
                    {
                        rollingLines.Insert(0, summonEvents[summonIndex].Message);
                    }

                    summonIndex += 1;
                }

                while (controlIndex < controlEvents.Count && controlEvents[controlIndex].Time <= elapsed)
                {
                    ApplyControlEvent(controlEvents[controlIndex], views, floatingTexts);
                    rollingLines.Insert(0, controlEvents[controlIndex].Message);
                    while (rollingLines.Count > 7)
                    {
                        rollingLines.RemoveAt(rollingLines.Count - 1);
                    }

                    controlIndex += 1;
                }

                while (luckyCritIndex < pendingLuckyCritEvents.Count && pendingLuckyCritEvents[luckyCritIndex].Time <= elapsed)
                {
                    ApplyBattleSourceCue(views, pendingLuckyCritEvents[luckyCritIndex], "幸运！", new Color32(255, 226, 112, 255), floatingTexts);
                    _visualBattleActionPauseRemaining = Mathf.Max(_visualBattleActionPauseRemaining, 0.18f);
                    luckyCritIndex += 1;
                }

                while (criticalIndex < pendingCriticalDamageEvents.Count && pendingCriticalDamageEvents[criticalIndex].Time <= elapsed)
                {
                    ApplyCriticalDamageFeedback(pendingCriticalDamageEvents[criticalIndex], views, floatingTexts, bursts);
                    if (!string.IsNullOrWhiteSpace(pendingCriticalDamageEvents[criticalIndex].Message))
                    {
                        rollingLines.Insert(0, pendingCriticalDamageEvents[criticalIndex].Message);
                    }

                    while (rollingLines.Count > 7)
                    {
                        rollingLines.RemoveAt(rollingLines.Count - 1);
                    }

                    criticalIndex += 1;
                }

                var playbackSpeed = elapsed >= 20f ? 2f : 1f;
                var deltaTime = Time.deltaTime * playbackSpeed;
                if (_visualBattleActionPauseRemaining > 0f)
                {
                    _visualBattleActionPauseRemaining = Mathf.Max(0f, _visualBattleActionPauseRemaining - deltaTime);
                }
                else
                {
                    UpdateVisualRealtimeBattle(uniqueViews, deltaTime, elapsed, rollingLines, projectiles, floatingTexts, bursts, pendingMoraleExtraEvents);
                }
                UpdateBattleProjectiles(projectiles, floatingTexts, bursts, deltaTime);
                UpdateBattleFloatingTexts(floatingTexts, deltaTime);
                UpdateBattleEffectBursts(bursts, deltaTime);
                var progress = Mathf.Lerp(0.05f, 0.95f, Mathf.Clamp01(elapsed / maxBattlePlaybackSeconds));
                SetBattleStageProgress(progress);
                SetBattleStageText($"实时战斗 {elapsed:0.0}s", string.Join("\n", rollingLines));
                elapsed += deltaTime;
                yield return null;
            }
        }

        private IEnumerator PlayVisualTurnBattle(Dictionary<string, BattleStageUnitView> views, BattleStubResult result, string openingLine)
        {
            EnsureBattlePlaybackSpeedControls();
            SetBattlePlaybackSpeedControlsVisible(true);
            var events = (result?.Events ?? new List<BattleEvent>())
                .OrderBy(item => item.Time)
                .ToList();
            var floatingTexts = new List<BattleFloatingTextView>();
            var bursts = new List<BattleEffectBurstView>();
            var rollingLines = new List<string> { openingLine };
            var total = Mathf.Max(1, events.Count);
            _latestBattleLogLines.Clear();
            _latestBattleLogLines.Add(openingLine);

            for (var i = 0; i < events.Count; i += 1)
            {
                var battleEvent = events[i];
                var line = FormatBattleEvent(battleEvent);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _latestBattleLogLines.Add(line);
                    rollingLines.Insert(0, line);
                    while (rollingLines.Count > 7)
                    {
                        rollingLines.RemoveAt(rollingLines.Count - 1);
                    }
                }

                SetBattleStageProgress(Mathf.Lerp(0.05f, 0.95f, i / (float)total));
                SetBattleStageText("轮次战斗", string.Join("\n", rollingLines));

                switch (battleEvent.Kind)
                {
                    case "round":
                        yield return WaitAndUpdateBattleEffects(0.65f, floatingTexts, bursts);
                        break;
                    case "move":
                        yield return PlayBattleMoveEvent(views, battleEvent, floatingTexts, bursts);
                        break;
                    case "attack":
                    case "skill":
                        yield return PlayBattleAttackEffect(views, battleEvent, floatingTexts, bursts);
                        break;
                    case "morale_extra":
                        ApplyBattleSourceCue(views, battleEvent, "士气高涨！", new Color32(255, 218, 96, 255), floatingTexts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    case "lucky_crit":
                        ApplyBattleSourceCue(views, battleEvent, "幸运！", new Color32(255, 226, 112, 255), floatingTexts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    case "damage":
                    case "critical_damage":
                    case "block":
                    case "immune":
                        var damageWait = ApplyBattleDamageFeedback(views, battleEvent, floatingTexts, bursts);
                        yield return WaitAndUpdateBattleEffects(damageWait, floatingTexts, bursts);
                        break;
                    case "death":
                        ApplyBattleDeathFeedback(views, battleEvent, floatingTexts, bursts);
                        yield return WaitAndUpdateBattleEffects(0.12f, floatingTexts, bursts);
                        break;
                    case "summon":
                        CreateSummonFromEvent(battleEvent, views, views.Values.Distinct().ToList(), floatingTexts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    case "control":
                        ApplyControlEvent(battleEvent, views, floatingTexts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    case "count_gain":
                        ApplyBattleCountGainFeedback(views, battleEvent, floatingTexts, bursts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    case "buff_attack":
                        ApplyBattleAttackBuffFeedback(views, battleEvent, floatingTexts, bursts);
                        yield return WaitAndUpdateBattleEffects(0.18f, floatingTexts, bursts);
                        break;
                    default:
                        yield return WaitAndUpdateBattleEffects(0.06f, floatingTexts, bursts);
                        break;
                }
            }

            SetBattleStageProgress(0.95f);
            SetBattlePlaybackSpeedControlsVisible(false);
        }

        private IEnumerator PlayBattleMoveEvent(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            var source = FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            var destinationSlotId = string.IsNullOrWhiteSpace(battleEvent.DestinationSlotId)
                ? battleEvent.SourceSlotId
                : battleEvent.DestinationSlotId;
            if (source?.Rect == null || !TryGetBattleHexSlotCenter(source.Rect.parent is RectTransform parent ? parent.rect.size : new Vector2(1420f, 720f), destinationSlotId, battleEvent.SourcePlayerSide, out var target))
            {
                yield return WaitAndUpdateBattleEffects(0.08f, floatingTexts, bursts);
                yield break;
            }

            var start = source.Rect.anchoredPosition;
            var routePoints = BuildBattleRoutePoints(source.Rect.parent as RectTransform, start, target, battleEvent.RouteSlotIds, battleEvent.SourcePlayerSide);
            var routeLine = CreateBattleRoutePreview(source.Rect.parent, routePoints);
            source.Rect.SetAsLastSibling();
            var elapsed = 0f;
            var duration = ScaledBattlePlaybackDuration(Mathf.Clamp(0.22f * Mathf.Max(1, routePoints.Count - 1), 0.28f, 0.9f));
            while (elapsed < duration && source.Rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                source.Rect.anchoredPosition = SampleBattleRoute(routePoints, t);
                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                UpdateBattleEffectBursts(bursts, Time.deltaTime);
                yield return null;
            }

            if (source.Rect != null)
            {
                source.Rect.anchoredPosition = target;
                source.StartPosition = target;
                source.FightPosition = target;
                source.SlotId = destinationSlotId;
                views[BattleStageKey(source.PlayerSide, destinationSlotId)] = source;
            }

            if (routeLine != null)
            {
                Destroy(routeLine.gameObject);
            }
        }

        private static List<Vector2> BuildBattleRoutePoints(RectTransform parent, Vector2 start, Vector2 fallbackEnd, string routeSlotIds, bool playerSide)
        {
            var points = new List<Vector2> { start };
            var rootSize = parent != null && parent.rect.size.sqrMagnitude > 1f ? parent.rect.size : new Vector2(1420f, 720f);
            if (!string.IsNullOrWhiteSpace(routeSlotIds))
            {
                foreach (var slotId in routeSlotIds.Split('|'))
                {
                    if (TryGetBattleHexSlotCenter(rootSize, slotId, playerSide, out var point))
                    {
                        points.Add(point);
                    }
                }
            }

            if (points.Count <= 1)
            {
                points.Add(fallbackEnd);
            }

            return points;
        }

        private static Vector2 SampleBattleRoute(IReadOnlyList<Vector2> points, float t)
        {
            if (points == null || points.Count == 0)
            {
                return Vector2.zero;
            }

            if (points.Count == 1)
            {
                return points[0];
            }

            var totalLength = 0f;
            for (var i = 1; i < points.Count; i += 1)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
            }

            var targetLength = Mathf.Clamp01(t) * Mathf.Max(0.001f, totalLength);
            var walked = 0f;
            for (var i = 1; i < points.Count; i += 1)
            {
                var segmentLength = Vector2.Distance(points[i - 1], points[i]);
                if (walked + segmentLength >= targetLength)
                {
                    var segmentT = (targetLength - walked) / Mathf.Max(0.001f, segmentLength);
                    return Vector2.Lerp(points[i - 1], points[i], segmentT);
                }

                walked += segmentLength;
            }

            return points[points.Count - 1];
        }

        private static RectTransform CreateBattleRoutePreview(Transform parent, IReadOnlyList<Vector2> points)
        {
            if (parent == null || points == null || points.Count < 2)
            {
                return null;
            }

            var routeObject = new GameObject("BattleMoveRoutePreview", typeof(RectTransform));
            routeObject.transform.SetParent(parent, false);
            var rect = routeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling();

            for (var i = 1; i < points.Count; i += 1)
            {
                var start = points[i - 1];
                var end = points[i];
                var delta = end - start;
                if (delta.sqrMagnitude < 4f)
                {
                    continue;
                }

                var segmentObject = new GameObject($"Segment_{i}", typeof(Image));
                segmentObject.transform.SetParent(routeObject.transform, false);
                var segmentRect = segmentObject.GetComponent<RectTransform>();
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                segmentRect.anchoredPosition = (start + end) * 0.5f;
                segmentRect.sizeDelta = new Vector2(delta.magnitude, 5f);
                segmentRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

                var image = segmentObject.GetComponent<Image>();
                image.color = new Color32(95, 228, 255, 210);
                image.raycastTarget = false;
            }

            return rect;
        }

        private IEnumerator PlayBattleAttackEffect(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (battleEvent == null || views == null)
            {
                yield break;
            }

            var source = FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName);
            if (source?.Rect == null && target?.Rect == null)
            {
                yield return WaitAndUpdateBattleEffects(0.08f, floatingTexts, bursts);
                yield break;
            }

            if (source?.Rect != null && target?.Rect != null)
            {
                RuntimeSfxPlayer.PlayAttack(source.Range);
                if (source.Range > 1.05f || battleEvent.Kind == "skill")
                {
                    yield return PlayProjectileAttackEffect(source, target, floatingTexts, bursts, battleEvent.Kind == "skill");
                }
                else
                {
                    yield return PlayMeleeAttackEffect(source, target, floatingTexts, bursts);
                }

                yield break;
            }

            var origin = target?.Rect != null ? target.Rect.anchoredPosition : source.Rect.anchoredPosition;
            SpawnEffectBurst(origin, new Color32(255, 205, 92, 150), bursts);
            yield return WaitAndUpdateBattleEffects(0.22f, floatingTexts, bursts);
        }

        private IEnumerator PlayProjectileAttackEffect(BattleStageUnitView source, BattleStageUnitView target, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts, bool skill)
        {
            if (_battleFieldRoot == null || source?.Rect == null || target?.Rect == null)
            {
                yield break;
            }

            var projectileObject = new GameObject(skill ? "BattleSkillProjectile" : "BattleAttackProjectile", typeof(Image));
            projectileObject.transform.SetParent(_battleFieldRoot, false);
            var rect = projectileObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = skill ? new Vector2(28f, 28f) : new Vector2(20f, 20f);
            rect.anchoredPosition = source.Rect.anchoredPosition;
            var image = projectileObject.GetComponent<Image>();
            image.color = skill
                ? new Color32(255, 210, 86, 235)
                : source.PlayerSide ? new Color32(130, 220, 255, 235) : new Color32(255, 150, 120, 235);
            image.raycastTarget = false;

            var start = source.Rect.anchoredPosition;
            var end = target.Rect.anchoredPosition;
            var duration = ScaledBattlePlaybackDuration(skill ? 0.42f : 0.34f);
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                end = target.Rect != null ? target.Rect.anchoredPosition : end;
                var arc = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * (skill ? 32f : 18f));
                rect.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t)) + arc;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, skill ? 1.35f : 1.15f, Mathf.Sin(t * Mathf.PI));
                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                UpdateBattleEffectBursts(bursts, Time.deltaTime);
                yield return null;
            }

            if (rect != null)
            {
                var hit = target.Rect != null ? target.Rect.anchoredPosition : end;
                SpawnEffectBurst(hit, skill ? new Color32(255, 210, 86, 145) : new Color32(140, 220, 255, 125), bursts);
                Destroy(rect.gameObject);
            }
        }

        private IEnumerator PlayMeleeAttackEffect(BattleStageUnitView source, BattleStageUnitView target, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (source?.Rect == null || target?.Rect == null)
            {
                yield break;
            }

            var origin = source.Rect.anchoredPosition;
            var direction = target.Rect.anchoredPosition - origin;
            var distance = Mathf.Max(1f, direction.magnitude);
            var lunge = origin + direction / distance * Mathf.Min(42f, distance * 0.28f);
            SpawnMeleeSlash(source, target, bursts);

            var duration = ScaledBattlePlaybackDuration(0.26f);
            var elapsed = 0f;
            while (elapsed < duration && source.Rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var phase = t < 0.5f ? t / 0.5f : (1f - t) / 0.5f;
                source.Rect.anchoredPosition = Vector2.Lerp(origin, lunge, Mathf.SmoothStep(0f, 1f, phase));
                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                UpdateBattleEffectBursts(bursts, Time.deltaTime);
                yield return null;
            }

            if (source.Rect != null)
            {
                source.Rect.anchoredPosition = origin;
            }
        }

        private void SpawnMeleeSlash(BattleStageUnitView source, BattleStageUnitView target, List<BattleEffectBurstView> bursts)
        {
            if (_battleFieldRoot == null || source?.Rect == null || target?.Rect == null)
            {
                return;
            }

            var slashObject = new GameObject("BattleMeleeSlash", typeof(Image));
            slashObject.transform.SetParent(_battleFieldRoot, false);
            var rect = slashObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.Lerp(source.Rect.anchoredPosition, target.Rect.anchoredPosition, 0.58f);
            rect.sizeDelta = new Vector2(82f, 18f);
            var direction = target.Rect.anchoredPosition - source.Rect.anchoredPosition;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var image = slashObject.GetComponent<Image>();
            image.color = new Color32(255, 238, 180, 210);
            image.raycastTarget = false;
            bursts?.Add(new BattleEffectBurstView
            {
                Rect = rect,
                Image = image,
                StartSize = new Vector2(82f, 18f),
                EndSize = new Vector2(112f, 8f),
                Life = 0f,
                Duration = 0.22f
            });
        }

        private float ApplyBattleDamageFeedback(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (battleEvent == null || views == null)
            {
                return 0.16f;
            }

            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName);
            if (target == null || target.Dead)
            {
                return 0.16f;
            }

            var critical = battleEvent.Kind == "critical_damage";
            if (battleEvent.Kind == "block" || battleEvent.Kind == "immune")
            {
                AddFloatingText(battleEvent.Kind == "block" ? "格挡" : "免疫", target.Rect.anchoredPosition + new Vector2(0f, 60f), new Color32(150, 210, 255, 255), 20, floatingTexts);
                SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(150, 210, 255, 130), bursts);
                return 0.16f;
            }

            var previousCount = Mathf.Max(0, target.CurrentCount);
            target.Hp = Mathf.Clamp(battleEvent.TargetHp, 0, Mathf.Max(1, battleEvent.TargetMaxHp));
            target.MaxHp = Mathf.Max(1, battleEvent.TargetMaxHp);
            target.CurrentCount = ResolveCurrentCount(target.Hp, target.HpPerUnit);
            var countLoss = Mathf.Max(0, previousCount - target.CurrentCount);
            RuntimeSfxPlayer.PlayHit();
            UpdateBattleStageLabel(target, battleEvent.TargetName, target.Hp, target.MaxHp);
            if (target.Rect != null)
            {
                StartCoroutine(ShakeHitTarget(target, critical));
                SpawnEffectBurst(target.Rect.anchoredPosition, critical ? new Color32(255, 196, 78, 165) : new Color32(255, 92, 92, 130), bursts);
                AddFloatingText(
                    $"-{Mathf.Max(1, battleEvent.Amount)}❤️",
                    target.Rect.anchoredPosition + new Vector2(0f, 54f),
                    critical ? new Color32(255, 226, 112, 255) : Color.white,
                    critical ? 28 : 20,
                    floatingTexts,
                    critical);
                if (countLoss > 0)
                {
                    AddDelayedFloatingText(
                        $"-{countLoss}兵",
                        target.Rect.anchoredPosition + new Vector2(0f, 54f),
                        new Color32(255, 150, 110, 255),
                        22,
                        floatingTexts,
                        CountLossFloatingTextDelay);
                }
            }

            return countLoss > 0 ? CountLossActionPauseDuration : 0.16f;
        }

        private void ApplyBattleAttackBuffFeedback(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (battleEvent == null || views == null)
            {
                return;
            }

            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName);
            if (target?.Rect == null || target.Dead)
            {
                return;
            }

            AddFloatingText($"攻击+{Mathf.Max(0, battleEvent.Amount)}", target.Rect.anchoredPosition + new Vector2(0f, 76f), new Color32(255, 218, 96, 255), 60, floatingTexts);
            SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(255, 218, 96, 130), bursts);
        }

        private void ApplyBattleCountGainFeedback(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (battleEvent == null || views == null)
            {
                return;
            }

            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName)
                ?? FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            if (target?.Rect == null || target.Dead)
            {
                return;
            }

            target.Hp = Mathf.Max(target.Hp, battleEvent.TargetHp);
            target.MaxHp = Mathf.Max(target.MaxHp, battleEvent.TargetMaxHp);
            target.CurrentCount = Mathf.Max(target.CurrentCount, ResolveCurrentCount(target.Hp, target.HpPerUnit));
            target.MaxCount = Mathf.Max(target.MaxCount, target.CurrentCount);
            UpdateBattleStageLabel(target, battleEvent.TargetName, target.Hp, target.MaxHp);

            var source = FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            if (source?.Rect != null && source != target)
            {
                StartCoroutine(PlayCountGainLinkEffect(source, target, battleEvent.Amount, floatingTexts, bursts));
                return;
            }

            AddFloatingText($"数量+{Mathf.Max(0, battleEvent.Amount)}", target.Rect.anchoredPosition + new Vector2(0f, 76f), new Color32(116, 236, 154, 255), 26, floatingTexts);
            SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(116, 236, 154, 125), bursts);
        }

        private IEnumerator PlayCountGainLinkEffect(BattleStageUnitView source, BattleStageUnitView target, int amount, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (_battleFieldRoot == null || source?.Rect == null || target?.Rect == null)
            {
                yield break;
            }

            var orbObject = new GameObject("BattleCountGainLink", typeof(Image));
            orbObject.transform.SetParent(_battleFieldRoot, false);
            var rect = orbObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(18f, 18f);
            rect.anchoredPosition = source.Rect.anchoredPosition;

            var image = orbObject.GetComponent<Image>();
            image.color = new Color32(116, 236, 154, 235);
            image.raycastTarget = false;

            var start = source.Rect.anchoredPosition + new Vector2(0f, 36f);
            var end = target.Rect.anchoredPosition + new Vector2(0f, 36f);
            var duration = ScaledBattlePlaybackDuration(0.26f);
            var elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (target.Rect != null)
                {
                    end = target.Rect.anchoredPosition + new Vector2(0f, 36f);
                }

                var arc = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 22f);
                rect.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t)) + arc;
                rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.35f, Mathf.Sin(t * Mathf.PI));
                if (image != null)
                {
                    image.color = Color.Lerp(new Color32(116, 236, 154, 235), new Color32(255, 236, 142, 235), t);
                }

                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                UpdateBattleEffectBursts(bursts, Time.deltaTime);
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }

            if (target?.Rect != null && !target.Dead)
            {
                AddFloatingText($"数量+{Mathf.Max(0, amount)}", target.Rect.anchoredPosition + new Vector2(0f, 76f), new Color32(116, 236, 154, 255), 26, floatingTexts);
                SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(116, 236, 154, 125), bursts);
            }
        }

        private void ApplyBattleSourceCue(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, string message, Color color, List<BattleFloatingTextView> floatingTexts)
        {
            if (battleEvent == null || views == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var source = FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            if (source?.Rect == null || source.Dead)
            {
                return;
            }

            AddFloatingText(message, source.Rect.anchoredPosition + new Vector2(0f, 86f), color, 28, floatingTexts);
        }

        private void ApplyBattleDeathFeedback(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            if (battleEvent == null || views == null)
            {
                return;
            }

            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName);
            if (target == null)
            {
                return;
            }

            var position = target.Rect != null ? target.Rect.anchoredPosition : Vector2.zero;
            UpdateBattleStageLabel(target, battleEvent.TargetName, 0, Mathf.Max(1, battleEvent.TargetMaxHp));
            MarkBattleStageDead(target);
            RuntimeSfxPlayer.PlayDeath();
            AddFloatingText("阵亡", position + new Vector2(0f, 78f), new Color32(255, 120, 120, 255), 20, floatingTexts);
            SpawnEffectBurst(position, new Color32(220, 72, 72, 160), bursts);
        }

        private IEnumerator WaitAndUpdateBattleEffects(float duration, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            duration = ScaledBattlePlaybackDuration(duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                UpdateBattleFloatingTexts(floatingTexts, Time.deltaTime);
                UpdateBattleEffectBursts(bursts, Time.deltaTime);
                yield return null;
            }
        }

        private float ScaledBattlePlaybackDuration(float baseDuration)
        {
            return Mathf.Max(0.02f, baseDuration / Mathf.Max(0.1f, _battlePlaybackSpeed));
        }

        private Dictionary<string, BattleStageUnitView> RebuildBattleStagePlaybackUnits(BattleStubResult result)
        {
            ClearChildren(battlePlayerRoot);
            ClearChildren(battleEnemyRoot);
            DisableLayout(battlePlayerRoot);
            DisableLayout(battleEnemyRoot);
            var fieldRoot = CreateBattleFieldRoot();

            var views = new Dictionary<string, BattleStageUnitView>();
            if (result == null)
            {
                return views;
            }

            foreach (var unit in result.PlayerUnits.Where(unit => !unit.Summoned).OrderBy(unit => unit.SlotId))
            {
                var view = CreateBattleStagePositionedUnit(fieldRoot, unit, true);
                ApplyBattleSetupPositionOverride(view);
                AddBattleStageView(views, true, unit.SlotId, unit.Name, view);
            }

            foreach (var unit in result.EnemyUnits.Where(unit => !unit.Summoned).OrderBy(unit => unit.SlotId))
            {
                var view = CreateBattleStagePositionedUnit(fieldRoot, unit, false);
                AddBattleStageView(views, false, unit.SlotId, unit.Name, view);
            }

            return views;
        }

        private static BattleStubResult CreateBattlePreviewStageResult(BattlePreviewResult preview)
        {
            return new BattleStubResult
            {
                PlayerScore = preview?.PlayerScore ?? 0,
                EnemyScore = preview?.EnemyScore ?? 0,
                PlayerUnits = preview?.PlayerUnits?.ToList() ?? new List<BattleUnitSnapshot>(),
                EnemyUnits = preview?.EnemyUnits?.ToList() ?? new List<BattleUnitSnapshot>(),
                Summary = "Battle setup"
            };
        }

        private void EnableBattleSetupDragging(Dictionary<string, BattleStageUnitView> views)
        {
            _battleSetupDraggingEnabled = true;
            if (views == null)
            {
                return;
            }

            foreach (var view in views.Values.Where(item => item != null && item.PlayerSide && item.Rect != null).Distinct())
            {
                EnableBattleSetupDragItem(view);
            }
        }

        private void EnableBattleSetupDragItem(BattleStageUnitView view)
        {
            if (view?.Rect == null)
            {
                return;
            }

            var graphic = view.Backing != null ? view.Backing : view.Rect.GetComponent<Image>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            var dragItem = view.Rect.GetComponent<RuntimeBattleSetupDragItem>() ?? view.Rect.gameObject.AddComponent<RuntimeBattleSetupDragItem>();
            dragItem.Controller = this;
            dragItem.PositionKey = BattleSetupPositionKey(view);
        }

        public void BeginBattleSetupUnitDrag(string positionKey, RectTransform rect)
        {
            if (!_battleSetupDraggingEnabled || rect == null || string.IsNullOrWhiteSpace(positionKey))
            {
                return;
            }

            rect.SetAsLastSibling();
            RuntimeSfxPlayer.PlayCardSelect();
        }

        public void DragBattleSetupUnit(string positionKey, RectTransform rect, PointerEventData eventData)
        {
            if (!_battleSetupDraggingEnabled || rect == null || string.IsNullOrWhiteSpace(positionKey) || _battleFieldRoot == null)
            {
                return;
            }

            var fieldRect = _battleFieldRoot as RectTransform;
            if (fieldRect == null)
            {
                return;
            }

            var camera = GetBattleSetupDragCamera(eventData, fieldRect);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fieldRect, eventData.position, camera, out var localPoint))
            {
                return;
            }

            localPoint = ClampBattleFieldLocalPoint(fieldRect, localPoint);
            rect.anchoredPosition = localPoint;
            _battlePlayerPositionOverrides[positionKey] = localPoint;
        }

        public void EndBattleSetupUnitDrag(string positionKey, RectTransform rect)
        {
            if (!_battleSetupDraggingEnabled || rect == null || string.IsNullOrWhiteSpace(positionKey))
            {
                return;
            }

            _battlePlayerPositionOverrides[positionKey] = rect.anchoredPosition;
            RuntimeSfxPlayer.PlayMove();
        }

        private void ApplyBattleSetupPositionOverride(BattleStageUnitView view)
        {
            if (view?.Rect == null || !view.PlayerSide)
            {
                return;
            }

            if (!_battlePlayerPositionOverrides.TryGetValue(BattleSetupPositionKey(view), out var position))
            {
                return;
            }

            var fightDelta = view.FightPosition - view.StartPosition;
            view.Rect.anchoredPosition = position;
            view.StartPosition = position;
            view.FightPosition = position + fightDelta;
            view.HasAttackAnchor = false;
        }

        private static string BattleSetupPositionKey(BattleStageUnitView view)
        {
            return string.IsNullOrWhiteSpace(view?.SlotId) ? view?.Name ?? string.Empty : view.SlotId;
        }

        private static Vector2 ClampBattleFieldLocalPoint(RectTransform fieldRect, Vector2 localPoint)
        {
            var rect = fieldRect.rect;
            return new Vector2(
                Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax),
                Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax));
        }

        private static Camera GetBattleSetupDragCamera(PointerEventData eventData, RectTransform fieldRect)
        {
            var canvas = fieldRect != null ? fieldRect.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return eventData != null && eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : Camera.main;
        }

        private IEnumerator WaitForBattleStartAction()
        {
            _battleStartActionRequested = false;
            SetBattleStartActionButtonVisible(true);
            while (!_battleStartActionRequested)
            {
                yield return null;
            }

            SetBattleStartActionButtonVisible(false);
        }

        private void SetBattleStartActionButtonVisible(bool visible)
        {
            EnsureBattleStartActionButton();
            if (_battleStartActionButton == null)
            {
                return;
            }

            _battleStartActionButton.SetActive(visible);
            if (visible)
            {
                _battleStartActionButton.transform.SetAsLastSibling();
            }
        }

        private IEnumerator PlayBattleStartCountdown()
        {
            var parent = runPanel != null ? runPanel.transform : transform;
            var overlay = new GameObject("BattleStartCountdown", typeof(RectTransform), typeof(CanvasGroup));
            overlay.transform.SetParent(parent, false);
            overlay.transform.SetAsLastSibling();

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var group = overlay.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var textObject = new GameObject("CountdownText", typeof(Text));
            textObject.transform.SetParent(overlay.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(760f, 220f);

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 150;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(255, 238, 126, 255);
            text.raycastTarget = false;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 230);
            outline.effectDistance = new Vector2(5f, -5f);

            var steps = new[] { "3", "2", "1", "开战" };
            foreach (var step in steps)
            {
                text.text = step;
                textRect.localScale = Vector3.one * 0.78f;
                group.alpha = 0f;
                RuntimeSfxPlayer.PlayClick();

                var elapsed = 0f;
                const float duration = 0.62f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    textRect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.18f, Mathf.SmoothStep(0f, 1f, t));
                    group.alpha = t < 0.18f
                        ? Mathf.Lerp(0f, 1f, t / 0.18f)
                        : Mathf.Lerp(1f, 0.08f, Mathf.Clamp01((t - 0.72f) / 0.28f));
                    yield return null;
                }

                yield return new WaitForSeconds(0.08f);
            }

            Destroy(overlay);
        }

        private void SetBattlePlaybackSpeedControlsVisible(bool visible)
        {
            EnsureBattlePlaybackSpeedControls();
            if (_battlePlaybackSpeedRoot == null)
            {
                return;
            }

            _battlePlaybackSpeedRoot.SetActive(visible);
            if (visible)
            {
                _battlePlaybackSpeedRoot.transform.SetAsLastSibling();
            }
        }

        private void EnsureBattlePlaybackSpeedControls()
        {
            if (_battlePlaybackSpeedRoot != null)
            {
                RefreshBattlePlaybackSpeedButtons();
                return;
            }

            var parent = battleStagePanel != null ? battleStagePanel.transform : transform;
            _battlePlaybackSpeedRoot = new GameObject("BattlePlaybackSpeedControls", typeof(RectTransform));
            _battlePlaybackSpeedRoot.transform.SetParent(parent, false);
            var rootRect = _battlePlaybackSpeedRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 188f);
            rootRect.sizeDelta = new Vector2(270f, 44f);

            CreateBattlePlaybackSpeedButton("SpeedSlow", "0.5x", -90f, 0.5f);
            CreateBattlePlaybackSpeedButton("SpeedNormal", "1x", 0f, 1f);
            CreateBattlePlaybackSpeedButton("SpeedFast", "2x", 90f, 2f);
            RefreshBattlePlaybackSpeedButtons();
            _battlePlaybackSpeedRoot.SetActive(false);
        }

        private void CreateBattlePlaybackSpeedButton(string name, string label, float x, float speed)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_battlePlaybackSpeedRoot.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(78f, 38f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(42, 68, 104, 245);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() =>
            {
                _battlePlaybackSpeed = speed;
                RefreshBattlePlaybackSpeedButtons();
            });

            var text = CreateChildText(buttonObject.transform, label, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.color = Color.white;
        }

        private void RefreshBattlePlaybackSpeedButtons()
        {
            if (_battlePlaybackSpeedRoot == null)
            {
                return;
            }

            foreach (Transform child in _battlePlaybackSpeedRoot.transform)
            {
                var image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                var selected = (child.name == "SpeedSlow" && Mathf.Approximately(_battlePlaybackSpeed, 0.5f))
                    || (child.name == "SpeedNormal" && Mathf.Approximately(_battlePlaybackSpeed, 1f))
                    || (child.name == "SpeedFast" && Mathf.Approximately(_battlePlaybackSpeed, 2f));
                image.color = selected
                    ? new Color32(78, 132, 190, 255)
                    : new Color32(42, 68, 104, 230);
            }
        }

        private void EnsureBattleStartActionButton()
        {
            if (_battleStartActionButton != null)
            {
                return;
            }

            var parent = battleStagePanel != null ? battleStagePanel.transform : transform;
            _battleStartActionButton = new GameObject("BattleStartActionButton", typeof(Image), typeof(Button));
            _battleStartActionButton.transform.SetParent(parent, false);
            var rect = _battleStartActionButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.16f);
            rect.anchorMax = new Vector2(0.5f, 0.16f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(260f, 64f);

            var image = _battleStartActionButton.GetComponent<Image>();
            image.color = new Color32(58, 100, 148, 245);

            var button = _battleStartActionButton.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => _battleStartActionRequested = true);

            var text = CreateChildText(_battleStartActionButton.transform, "开始行动", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.color = Color.white;
            _battleStartActionButton.SetActive(false);
        }

        private Transform CreateBattleFieldRoot()
        {
            ClearBattleFieldRoot();
            var parent = battleStagePanel != null ? battleStagePanel.transform : transform;
            var rootObject = new GameObject("BattleFieldRoot", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            PositionBattleFieldLayer(rootObject.transform);
            var rect = rootObject.GetComponent<RectTransform>();
            ApplyBattleFieldRootRect(rect);
            _battleFieldRoot = rootObject.transform;
            CreateBattleHexGrid(_battleFieldRoot);
            return _battleFieldRoot;
        }

        private static void CreateBattleHexGrid(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var rootRect = root as RectTransform;
            var rootSize = rootRect != null && rootRect.rect.size.sqrMagnitude > 1f ? rootRect.rect.size : new Vector2(1420f, 720f);
            var cellSize = CalculateBattleHexCellSize(rootSize);
            var gridObject = new GameObject("BattleHexGrid", typeof(RectTransform));
            gridObject.transform.SetParent(root, false);
            var rect = gridObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            var sprite = GetBattleHexCellSprite();
            for (var column = 0; column < BattleHexColumnCount; column += 1)
            {
                var rows = BattleHexRowsByColumn[column];
                for (var row = 0; row < rows; row += 1)
                {
                    if (!TryGetBattleHexCenter(rootSize, column, row, out var center))
                    {
                        continue;
                    }

                    var cellObject = new GameObject($"Hex_{column + 1}_{row + 1}", typeof(Image));
                    cellObject.transform.SetParent(gridObject.transform, false);
                    var cellRect = cellObject.GetComponent<RectTransform>();
                    cellRect.anchorMin = new Vector2(0.5f, 0.5f);
                    cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cellRect.pivot = new Vector2(0.5f, 0.5f);
                    cellRect.anchoredPosition = center;
                    cellRect.sizeDelta = cellSize;

                    var image = cellObject.GetComponent<Image>();
                    image.sprite = sprite;
                    image.raycastTarget = false;
                    image.color = column < 4
                        ? new Color32(104, 205, 255, 235)
                        : column > 6
                            ? new Color32(255, 124, 146, 235)
                            : new Color32(255, 235, 148, 220);
                }
            }

            CreateBattleCenterGuide(gridObject.transform, rootSize, cellSize);
        }

        private static void CreateBattleCenterGuide(Transform gridRoot, Vector2 rootSize, Vector2 cellSize)
        {
            if (gridRoot == null
                || !TryGetBattleHexCenter(rootSize, 5, 1, out var top)
                || !TryGetBattleHexCenter(rootSize, 5, 2, out var bottom))
            {
                return;
            }

            var guideObject = new GameObject("BattleCenterGuide", typeof(Image));
            guideObject.transform.SetParent(gridRoot, false);
            var guideRect = guideObject.GetComponent<RectTransform>();
            guideRect.anchorMin = new Vector2(0.5f, 0.5f);
            guideRect.anchorMax = new Vector2(0.5f, 0.5f);
            guideRect.pivot = new Vector2(0.5f, 0.5f);
            guideRect.anchoredPosition = new Vector2(top.x, (top.y + bottom.y) * 0.5f);
            guideRect.sizeDelta = new Vector2(5f, Mathf.Abs(top.y - bottom.y) + cellSize.y * 1.2f);

            var image = guideObject.GetComponent<Image>();
            image.color = new Color32(255, 222, 98, 180);
            image.raycastTarget = false;
        }

        private static Sprite GetBattleHexCellSprite()
        {
            if (_cachedBattleHexCellSprite != null)
            {
                return _cachedBattleHexCellSprite;
            }

            const int width = 128;
            const int height = 112;
            const float border = 5.5f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "RuntimeBattleHexCell";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var clear = new Color32(0, 0, 0, 0);
            var fill = new Color32(255, 255, 255, 35);
            var line = new Color32(255, 255, 255, 255);

            for (var y = 0; y < height; y += 1)
            {
                for (var x = 0; x < width; x += 1)
                {
                    var point = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                    if (!IsInsideBattleHex(point, 0f))
                    {
                        texture.SetPixel(x, y, clear);
                    }
                    else if (!IsInsideBattleHex(point, border / Mathf.Min(width, height)))
                    {
                        texture.SetPixel(x, y, line);
                    }
                    else
                    {
                        texture.SetPixel(x, y, fill);
                    }
                }
            }

            texture.Apply();
            _cachedBattleHexCellSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedBattleHexCellSprite;
        }

        private static bool IsInsideBattleHex(Vector2 point, float inset)
        {
            var x = Mathf.Abs(point.x - 0.5f);
            var y = Mathf.Abs(point.y - 0.5f);
            var halfWidth = Mathf.Max(0.01f, 0.5f - inset);
            var halfHeight = Mathf.Max(0.01f, 0.5f - inset);
            if (x > halfWidth || y > halfHeight)
            {
                return false;
            }

            var shoulder = halfWidth * 0.5f;
            if (x <= shoulder)
            {
                return true;
            }

            var t = (x - shoulder) / Mathf.Max(0.001f, halfWidth - shoulder);
            return y <= halfHeight * (1f - t);
        }

        private void PositionBattleFieldLayer(Transform battleField)
        {
            if (battleField == null || battleStagePanel == null)
            {
                return;
            }

            var background = battleStagePanel.transform.Find("Background");
            if (background == null)
            {
                battleField.SetSiblingIndex(0);
                return;
            }

            background.SetAsFirstSibling();
            var backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
            }

            battleField.SetSiblingIndex(background.GetSiblingIndex() + 1);
        }

        private void ApplyBattleFieldRootRect(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            var area = FindBattleFieldAreaRect();
            if (area != null)
            {
                CopyRectTransformBounds(rect, area);
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(80f, 100f);
            rect.offsetMax = new Vector2(-80f, -140f);
        }

        private RectTransform FindBattleFieldAreaRect()
        {
            if (battleStagePanel == null)
            {
                return null;
            }

            var area = FindDeepChild(battleStagePanel.transform, "BattleFieldArea");
            if (area != null)
            {
                return area as RectTransform;
            }

            return null;
        }

        private static void CopyRectTransformBounds(RectTransform target, RectTransform source)
        {
            var parent = target != null ? target.parent as RectTransform : null;
            if (target == null || source == null || parent == null)
            {
                return;
            }

            var corners = new Vector3[4];
            source.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < corners.Length; i += 1)
            {
                var local = parent.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = (min + max) * 0.5f;
            target.sizeDelta = max - min;
        }

        private void ClearBattleFieldRoot()
        {
            if (_battleFieldRoot != null)
            {
                Destroy(_battleFieldRoot.gameObject);
                _battleFieldRoot = null;
                return;
            }

            if (battleStagePanel == null)
            {
                return;
            }

            var existing = battleStagePanel.transform.Find("BattleFieldRoot");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        private static void AddBattleStageView(Dictionary<string, BattleStageUnitView> views, bool playerSide, string slotId, string unitName, BattleStageUnitView view)
        {
            if (view == null)
            {
                return;
            }

            var slotKey = BattleStageKey(playerSide, slotId);
            if (!string.IsNullOrWhiteSpace(slotId) && !views.ContainsKey(slotKey))
            {
                views.Add(slotKey, view);
            }

            var nameKey = BattleStageKey(playerSide, unitName);
            if (!string.IsNullOrWhiteSpace(unitName) && !views.ContainsKey(nameKey))
            {
                views.Add(nameKey, view);
            }
        }

        private void UpdateVisualRealtimeBattle(
            IReadOnlyList<BattleStageUnitView> views,
            float deltaTime,
            float elapsed,
            List<string> rollingLines,
            List<BattleProjectileView> projectiles,
            List<BattleFloatingTextView> floatingTexts,
            List<BattleEffectBurstView> bursts,
            List<BattleEvent> pendingMoraleExtraEvents)
        {
            if (views == null || views.Count == 0)
            {
                return;
            }

            foreach (var view in views)
            {
                if (view?.Rect == null || view.Dead)
                {
                    continue;
                }

                if (view.IsSummon && view.SummonDuration > 0f)
                {
                    view.SummonDuration -= deltaTime;
                    if (view.SummonDuration <= 0f)
                    {
                        MarkBattleStageDead(view);
                        continue;
                    }
                }

                RestoreVisualUnitTint(view);
                UpdateVisualControlTimers(view, deltaTime);
                var target = ResolveVisualTarget(views, view, deltaTime);
                if (target == null && view.HasStartedAttacking)
                {
                    target = FindLivingOpponentInVisualRange(views, view);
                    view.Target = target;
                }

                if (target == null)
                {
                    continue;
                }

                view.AttackAnim = Mathf.Max(0f, view.AttackAnim - deltaTime * 4.2f);
                view.AttackTimer = Mathf.Max(0f, view.AttackTimer - deltaTime);
                if (view.HasStartedAttacking)
                {
                    var inRangeTarget = FindLivingOpponentInVisualRange(views, view);
                    if (inRangeTarget != null)
                    {
                        LockVisualAttackPosition(view, "in_range_target");
                        target = inRangeTarget;
                        view.Target = target;
                    }
                    else
                    {
                        view.HasAttackAnchor = false;
                    }
                }

                var distance = Vector2.Distance(view.Rect.anchoredPosition, target.Rect.anchoredPosition);
                var attackRange = VisualAttackRange(view, target);
                if (distance > attackRange + VisualAttackRangeSlack)
                {
                    if (view.StunRemaining <= 0f && view.MoveLockRemaining <= 0f)
                    {
                        MoveVisualUnitToTarget(view, target, views, deltaTime, distance, attackRange);
                    }
                }
                else if (view.AttackTimer <= 0f && view.StunRemaining <= 0f && view.AttackLockRemaining <= 0f)
                {
                    VisualAttack(view, target, elapsed, rollingLines, projectiles, floatingTexts, bursts, pendingMoraleExtraEvents);
                }
            }

            ResolveVisualCollisions(views);
        }

        private static void UpdateVisualControlTimers(BattleStageUnitView view, float deltaTime)
        {
            view.StunRemaining = Mathf.Max(0f, view.StunRemaining - deltaTime);
            view.MoveLockRemaining = Mathf.Max(0f, view.MoveLockRemaining - deltaTime);
            view.AttackLockRemaining = Mathf.Max(0f, view.AttackLockRemaining - deltaTime);
            if (view.Label != null && (view.StunRemaining > 0f || view.MoveLockRemaining > 0f || view.AttackLockRemaining > 0f))
            {
                view.Label.color = new Color32(150, 210, 255, 255);
            }
            else if (view.Label != null && !view.Dead)
            {
                view.Label.color = Color.white;
            }
        }

        private static void MoveVisualUnitToTarget(BattleStageUnitView unit, BattleStageUnitView target, IReadOnlyList<BattleStageUnitView> allUnits, float deltaTime, float distance, float attackRange)
        {
            var current = unit.Rect.anchoredPosition;
            var targetPosition = target.Rect.anchoredPosition;
            var direction = targetPosition - current;
            distance = Mathf.Max(0.001f, distance);
            var move = direction / distance;
            var forwardBlock = 0f;

            foreach (var other in allUnits)
            {
                if (other == null || other == unit || other == target || other.Dead || other.Rect == null)
                {
                    continue;
                }

                var away = current - other.Rect.anchoredPosition;
                var spacing = unit.Size + other.Size + 18f;
                var blockDistance = Mathf.Max(0.001f, away.magnitude);
                if (blockDistance >= spacing)
                {
                    continue;
                }

                var overlapRatio = (spacing - blockDistance) / spacing;
                var normAway = away / blockDistance;
                var rel = other.Rect.anchoredPosition - current;
                var aheadDot = Vector2.Dot(rel, move);
                var isAhead = aheadDot > 0f;
                var repelWeight = overlapRatio * (isAhead ? 1.35f : 0.55f);
                move += normAway * repelWeight;

                if (isAhead)
                {
                    forwardBlock += overlapRatio;
                    var tangent = new Vector2(-move.y, move.x) * (unit.PlayerSide ? 1f : -1f);
                    move += tangent.normalized * overlapRatio * 1.1f;
                }
            }

            var moveSpeed = Mathf.Max(45f, unit.Speed * 8.4f) * 1.85f;
            var stepScale = Mathf.Max(0.45f, 1f - Mathf.Min(0.42f, forwardBlock * 0.35f));
            var step = Mathf.Min(moveSpeed * stepScale * deltaTime, Mathf.Max(0f, distance - attackRange));
            unit.Rect.anchoredPosition += move.normalized * step;
        }

        private static float VisualAttackRange(BattleStageUnitView attacker, BattleStageUnitView target)
        {
            return attacker.Range * 60f + attacker.Size + target.Size;
        }

        private static void LockVisualAttackPosition(BattleStageUnitView view, string reason)
        {
            if (view?.Rect == null || !view.HasAttackAnchor || view.IsHitShaking)
            {
                return;
            }

            var current = view.Rect.anchoredPosition;
            if ((current - view.AttackAnchorPosition).sqrMagnitude <= 0.01f)
            {
                return;
            }

            Debug.LogWarning($"[BattlePosition] {view.Name} moved after attacking ({reason}) {current} -> {view.AttackAnchorPosition}");
            view.Rect.anchoredPosition = view.AttackAnchorPosition;
        }

        private void VisualAttack(
            BattleStageUnitView attacker,
            BattleStageUnitView target,
            float elapsed,
            List<string> rollingLines,
            List<BattleProjectileView> projectiles,
            List<BattleFloatingTextView> floatingTexts,
            List<BattleEffectBurstView> bursts,
            List<BattleEvent> pendingMoraleExtraEvents)
        {
            attacker.AttackTimer = Mathf.Max(0.2f, attacker.AttackInterval);
            attacker.AttackAnim = 1f;
            attacker.HasStartedAttacking = true;
            attacker.HasAttackAnchor = attacker.Rect != null;
            if (attacker.HasAttackAnchor)
            {
                attacker.AttackAnchorPosition = attacker.Rect.anchoredPosition;
            }

            RuntimeSfxPlayer.PlayAttack(attacker.Range);
            var unitDamage = Random.Range(Mathf.Max(1, attacker.DamageMin), Mathf.Max(Mathf.Max(1, attacker.DamageMin), attacker.DamageMax) + 1);
            var attackFactor = (20f + Mathf.Max(0, attacker.Attack)) / Mathf.Max(1f, 20f + Mathf.Max(0, target.Defense));
            var damage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, attacker.CurrentCount) * unitDamage * attackFactor));
            if (attacker.Range > 1.1f)
            {
                SpawnProjectile(attacker, target, damage, projectiles);
            }
            else
            {
                ApplyVisualDamage(attacker, target, damage, floatingTexts, bursts);
            }

            if (!target.Dead && target.Rect != null && Random.value < 0.08f)
            {
                SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(255, 126, 70, 150), bursts);
            }

            if (!target.Dead && target.Rect != null && Random.value < 0.08f)
            {
                target.StunRemaining = Mathf.Max(target.StunRemaining, 0.45f);
                AddFloatingText("眩晕", target.Rect.anchoredPosition + new Vector2(0f, 70f), new Color32(150, 210, 255, 255), 18, floatingTexts);
            }

            if (!target.Dead && target.Rect != null && Random.value < 0.05f)
            {
                target.MoveLockRemaining = Mathf.Max(target.MoveLockRemaining, 0.7f);
                target.AttackLockRemaining = Mathf.Max(target.AttackLockRemaining, 0.45f);
                AddFloatingText("锁定", target.Rect.anchoredPosition + new Vector2(0f, 88f), new Color32(255, 220, 130, 255), 17, floatingTexts);
            }

            if (attacker.IsSummon)
            {
                attacker.Hp = Mathf.Max(0, attacker.Hp - 1);
            }

            rollingLines.Insert(0, $"{attacker.Name} -> {target.Name} {damage}伤害");
            ApplyPendingMoraleExtraFeedback(attacker, elapsed, rollingLines, floatingTexts, pendingMoraleExtraEvents);
            while (rollingLines.Count > 7)
            {
                rollingLines.RemoveAt(rollingLines.Count - 1);
            }

            LockVisualAttackPosition(attacker, "after_visual_attack");
        }

        private void ApplyPendingMoraleExtraFeedback(
            BattleStageUnitView attacker,
            float elapsed,
            List<string> rollingLines,
            List<BattleFloatingTextView> floatingTexts,
            List<BattleEvent> pendingMoraleExtraEvents)
        {
            if (attacker?.Rect == null || pendingMoraleExtraEvents == null || pendingMoraleExtraEvents.Count == 0)
            {
                return;
            }

            var eventIndex = pendingMoraleExtraEvents.FindIndex(item =>
                item != null &&
                item.Time <= elapsed + 0.35f &&
                IsBattleEventSource(attacker, item));
            if (eventIndex < 0)
            {
                return;
            }

            var battleEvent = pendingMoraleExtraEvents[eventIndex];
            pendingMoraleExtraEvents.RemoveAt(eventIndex);
            AddFloatingText("士气高涨！", attacker.Rect.anchoredPosition + new Vector2(0f, 86f), new Color32(255, 218, 96, 255), 28, floatingTexts);
            if (!string.IsNullOrWhiteSpace(battleEvent.Message))
            {
                rollingLines.Insert(0, battleEvent.Message);
            }
        }

        private static bool IsBattleEventSource(BattleStageUnitView attacker, BattleEvent battleEvent)
        {
            if (attacker == null || battleEvent == null)
            {
                return false;
            }

            if (attacker.PlayerSide != battleEvent.SourcePlayerSide)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(battleEvent.SourceSlotId) && attacker.SlotId == battleEvent.SourceSlotId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(battleEvent.SourceUnitId) && attacker.UnitId == battleEvent.SourceUnitId)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(battleEvent.SourceName) && attacker.Name == battleEvent.SourceName;
        }

        private void ApplyVisualDamage(
            BattleStageUnitView attacker,
            BattleStageUnitView target,
            int damage,
            List<BattleFloatingTextView> floatingTexts,
            List<BattleEffectBurstView> bursts,
            bool critical = false)
        {
            var previousCount = Mathf.Max(0, target.CurrentCount);
            target.Hp = Mathf.Max(0, target.Hp - damage);
            target.CurrentCount = target.Hp <= 0 ? 0 : Mathf.CeilToInt(target.Hp / (float)Mathf.Max(1, target.HpPerUnit));
            var countLoss = Mathf.Max(0, previousCount - target.CurrentCount);
            RuntimeSfxPlayer.PlayHit();
            UpdateBattleStageLabel(target, target.Name, target.Hp, target.MaxHp);
            if (target.Rect != null)
            {
                StartCoroutine(ShakeHitTarget(target, critical));
            }

            var hitImage = target.UnitView?.UnitIconImage;
            if (hitImage != null)
            {
                hitImage.color = critical ? new Color32(255, 196, 78, 255) : new Color32(255, 96, 96, 255);
            }

            AddFloatingText(
                $"-{damage}❤️",
                target.Rect.anchoredPosition + new Vector2(0f, 54f),
                critical ? new Color32(255, 226, 112, 255) : Color.white,
                critical ? 28 : 20,
                floatingTexts,
                critical);
            if (countLoss > 0)
            {
                AddDelayedFloatingText(
                    $"-{countLoss}兵",
                    target.Rect.anchoredPosition + new Vector2(0f, 54f),
                    new Color32(255, 150, 110, 255),
                    22,
                    floatingTexts,
                    CountLossFloatingTextDelay);
                _visualBattleActionPauseRemaining = Mathf.Max(_visualBattleActionPauseRemaining, CountLossActionPauseDuration);
            }

            if (target.Hp <= 0)
            {
                var deathPosition = target.Rect != null ? target.Rect.anchoredPosition : Vector2.zero;
                MarkBattleStageDead(target);
                RuntimeSfxPlayer.PlayDeath();
                AddFloatingText("阵亡", deathPosition + new Vector2(0f, 78f), new Color32(255, 120, 120, 255), 20, floatingTexts);
                SpawnEffectBurst(deathPosition, new Color32(220, 72, 72, 150), bursts);
            }
        }

        private static void RestoreVisualUnitTint(BattleStageUnitView view)
        {
            var hitImage = view?.UnitView?.UnitIconImage;
            if (hitImage == null || view.Dead)
            {
                return;
            }

            hitImage.color = Color.Lerp(hitImage.color, Color.white, Time.deltaTime * 10f);
        }

        private static IEnumerator ShakeHitTarget(BattleStageUnitView target, bool critical)
        {
            if (target?.Rect == null)
            {
                yield break;
            }

            var origin = target.HasAttackAnchor ? target.AttackAnchorPosition : target.Rect.anchoredPosition;
            var amplitude = critical ? 12f : 6f;
            var duration = critical ? 0.18f : 0.12f;
            var elapsed = 0f;
            target.IsHitShaking = true;
            while (elapsed < duration && target.Rect != null)
            {
                var progress = Mathf.Clamp01(elapsed / duration);
                var damp = 1f - progress;
                var x = Mathf.Sin(progress * Mathf.PI * 8f) * amplitude * damp;
                var y = Mathf.Sin(progress * Mathf.PI * 11f) * amplitude * 0.35f * damp;
                target.Rect.anchoredPosition = origin + new Vector2(x, y);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (target.Rect != null)
            {
                target.Rect.anchoredPosition = origin;
            }

            target.IsHitShaking = false;
        }

        private void SpawnProjectile(BattleStageUnitView attacker, BattleStageUnitView target, int damage, List<BattleProjectileView> projectiles)
        {
            if (_battleFieldRoot == null || attacker?.Rect == null || target?.Rect == null)
            {
                return;
            }

            var projectileObject = new GameObject("BattleProjectile", typeof(Image));
            projectileObject.transform.SetParent(_battleFieldRoot, false);
            var rect = projectileObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(18f, 18f);
            rect.anchoredPosition = attacker.Rect.anchoredPosition;
            var image = projectileObject.GetComponent<Image>();
            image.color = attacker.PlayerSide ? new Color32(130, 220, 255, 235) : new Color32(255, 150, 120, 235);

            projectiles.Add(new BattleProjectileView
            {
                Rect = rect,
                Image = image,
                Target = target,
                Start = attacker.Rect.anchoredPosition,
                End = target.Rect.anchoredPosition,
                Duration = 0.28f,
                Life = 0f,
                Damage = damage
            });
        }

        private void UpdateBattleProjectiles(
            List<BattleProjectileView> projectiles,
            List<BattleFloatingTextView> floatingTexts,
            List<BattleEffectBurstView> bursts,
            float deltaTime)
        {
            for (var i = projectiles.Count - 1; i >= 0; i -= 1)
            {
                var projectile = projectiles[i];
                if (projectile?.Rect == null)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                projectile.Life += deltaTime;
                projectile.End = projectile.Target?.Rect != null ? projectile.Target.Rect.anchoredPosition : projectile.End;
                var t = Mathf.Clamp01(projectile.Life / Mathf.Max(0.01f, projectile.Duration));
                projectile.Rect.anchoredPosition = Vector2.Lerp(projectile.Start, projectile.End, t);
                if (projectile.Image != null)
                {
                    projectile.Image.color = Color.Lerp(projectile.Image.color, new Color(projectile.Image.color.r, projectile.Image.color.g, projectile.Image.color.b, 0.2f), deltaTime * 2f);
                }

                if (t < 1f)
                {
                    continue;
                }

                if (projectile.Target != null && !projectile.Target.Dead)
                {
                    ApplyVisualDamage(null, projectile.Target, projectile.Damage, floatingTexts, bursts);
                }

                Destroy(projectile.Rect.gameObject);
                projectiles.RemoveAt(i);
            }
        }

        private void AddFloatingText(string text, Vector2 position, Color color, int fontSize, List<BattleFloatingTextView> floatingTexts, bool useScaleAnimation = false, [CallerMemberName] string source = null)
        {
            if (_battleFieldRoot == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            LogFloatingTextSource("BattleFloatText", text, source);
            var textObject = new GameObject("BattleFloatText", typeof(Text));
            textObject.transform.SetParent(_battleFieldRoot, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 78f);
            rect.anchoredPosition = position;
            rect.localScale = useScaleAnimation ? Vector3.one * 2f : Vector3.one;
            var label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 60;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            AddFloatingTextOutline(textObject);

            floatingTexts?.Add(new BattleFloatingTextView
            {
                Rect = rect,
                Text = label,
                Start = position,
                Life = 0f,
                Duration = BattleFloatingTextDuration,
                UseScaleAnimation = useScaleAnimation
            });
        }

        private static void UpdateBattleFloatingTexts(List<BattleFloatingTextView> floatingTexts, float deltaTime)
        {
            const float scaleHoldDuration = 0.24f;
            const float scaleShrinkDuration = 0.48f;
            for (var i = floatingTexts.Count - 1; i >= 0; i -= 1)
            {
                var item = floatingTexts[i];
                if (item?.Rect == null)
                {
                    floatingTexts.RemoveAt(i);
                    continue;
                }

                item.Life += deltaTime;
                var t = Mathf.Clamp01(item.Life / Mathf.Max(0.01f, item.Duration));
                if (item.UseScaleAnimation)
                {
                    var scaleT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((item.Life - scaleHoldDuration) / scaleShrinkDuration));
                    item.Rect.localScale = Vector3.one * Mathf.Lerp(2f, 1f, scaleT);
                }
                else
                {
                    item.Rect.localScale = Vector3.one;
                }
                item.Rect.anchoredPosition = item.Start + new Vector2(0f, t * 42f);
                if (item.Text != null)
                {
                    var color = item.Text.color;
                    color.a = 1f - t;
                    item.Text.color = color;
                }

                if (t >= 1f)
                {
                    Destroy(item.Rect.gameObject);
                    floatingTexts.RemoveAt(i);
                }
            }
        }

        private void SpawnEffectBurst(Vector2 position, Color color, List<BattleEffectBurstView> bursts)
        {
            if (_battleFieldRoot == null)
            {
                return;
            }

            var burstObject = new GameObject("BattleEffectBurst", typeof(Image));
            burstObject.transform.SetParent(_battleFieldRoot, false);
            var rect = burstObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.anchoredPosition = position;
            burstObject.GetComponent<Image>().color = color;
            bursts?.Add(new BattleEffectBurstView
            {
                Rect = rect,
                Image = burstObject.GetComponent<Image>(),
                StartSize = new Vector2(30f, 30f),
                EndSize = new Vector2(132f, 132f),
                Life = 0f,
                Duration = 0.45f
            });
        }

        private static void UpdateBattleEffectBursts(List<BattleEffectBurstView> bursts, float deltaTime)
        {
            for (var i = bursts.Count - 1; i >= 0; i -= 1)
            {
                var burst = bursts[i];
                if (burst?.Rect == null)
                {
                    bursts.RemoveAt(i);
                    continue;
                }

                burst.Life += deltaTime;
                var t = Mathf.Clamp01(burst.Life / Mathf.Max(0.01f, burst.Duration));
                var startSize = burst.StartSize.sqrMagnitude > 0f ? burst.StartSize : new Vector2(30f, 30f);
                var endSize = burst.EndSize.sqrMagnitude > 0f ? burst.EndSize : new Vector2(132f, 132f);
                burst.Rect.sizeDelta = Vector2.Lerp(startSize, endSize, t);
                if (burst.Image != null)
                {
                    var color = burst.Image.color;
                    color.a = 1f - t;
                    burst.Image.color = color;
                }

                if (t >= 1f)
                {
                    Destroy(burst.Rect.gameObject);
                    bursts.RemoveAt(i);
                }
            }
        }

        private static void ResolveVisualCollisions(IReadOnlyList<BattleStageUnitView> views)
        {
            for (var i = 0; i < views.Count; i += 1)
            {
                var left = views[i];
                if (left?.Rect == null || left.Dead)
                {
                    continue;
                }

                for (var j = i + 1; j < views.Count; j += 1)
                {
                    var right = views[j];
                    if (right?.Rect == null || right.Dead)
                    {
                        continue;
                    }

                    var delta = right.Rect.anchoredPosition - left.Rect.anchoredPosition;
                    var distance = Mathf.Max(0.001f, delta.magnitude);
                    var minDistance = left.Size + right.Size;
                    if (distance >= minDistance)
                    {
                        continue;
                    }

                    var overlap = minDistance - distance;
                    var normal = delta / distance;
                    var leftLocked = left.HasStartedAttacking;
                    var rightLocked = right.HasStartedAttacking;
                    if (leftLocked && rightLocked)
                    {
                        continue;
                    }

                    if (!leftLocked && !rightLocked)
                    {
                        left.Rect.anchoredPosition -= normal * overlap * 0.5f;
                        right.Rect.anchoredPosition += normal * overlap * 0.5f;
                    }
                    else if (leftLocked)
                    {
                        right.Rect.anchoredPosition += normal * overlap;
                    }
                    else
                    {
                        left.Rect.anchoredPosition -= normal * overlap;
                    }
                }
            }
        }

        private static BattleStageUnitView FindNearestLivingOpponent(IReadOnlyList<BattleStageUnitView> views, BattleStageUnitView source)
        {
            BattleStageUnitView best = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in views)
            {
                if (candidate == null || candidate.Dead || candidate.PlayerSide == source.PlayerSide || candidate.Rect == null || source.Rect == null)
                {
                    continue;
                }

                var distance = Vector2.Distance(source.Rect.anchoredPosition, candidate.Rect.anchoredPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static BattleStageUnitView FindLivingOpponentInVisualRange(IReadOnlyList<BattleStageUnitView> views, BattleStageUnitView source)
        {
            BattleStageUnitView best = null;
            var bestDistance = float.MaxValue;
            foreach (var candidate in views)
            {
                if (candidate == null || candidate.Dead || candidate.PlayerSide == source.PlayerSide || candidate.Rect == null || source.Rect == null)
                {
                    continue;
                }

                var distance = Vector2.Distance(source.Rect.anchoredPosition, candidate.Rect.anchoredPosition);
                if (distance > VisualAttackRange(source, candidate) + VisualAttackRangeSlack || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private static BattleStageUnitView ResolveVisualTarget(IReadOnlyList<BattleStageUnitView> views, BattleStageUnitView source, float deltaTime)
        {
            source.TargetSearchTimer = Mathf.Max(0f, source.TargetSearchTimer - deltaTime);
            if (source.Target != null && !source.Target.Dead && source.Target.Rect != null)
            {
                return source.Target;
            }

            if (source.TargetSearchTimer > 0f)
            {
                return null;
            }

            source.Target = FindNearestLivingOpponent(views, source);
            source.TargetSearchTimer = TargetSearchInterval;
            return source.Target;
        }

        private static Vector2 PositionForRangeAgainstTarget(BattleStageUnitView source, BattleStageUnitView target)
        {
            var y = Mathf.Lerp(source.FightPosition.y, target.FightPosition.y, 0.18f);
            var rangePixels = Mathf.Clamp(Mathf.Max(1f, source.Range) * 60f + Mathf.Max(20, source.Size), 90f, 520f);
            var centerOffset = Mathf.Clamp(rangePixels * 0.42f, 34f, 220f);
            var x = source.PlayerSide ? -centerOffset : centerOffset;
            return new Vector2(x, y);
        }

        private static IEnumerator PlayBattleEventMotion(Dictionary<string, BattleStageUnitView> views, BattleEvent battleEvent, float duration)
        {
            if (battleEvent == null || views == null)
            {
                yield break;
            }

            var source = FindBattleStageView(views, battleEvent.SourcePlayerSide, battleEvent.SourceSlotId, battleEvent.SourceName);
            var target = FindBattleStageView(views, battleEvent.TargetPlayerSide, battleEvent.TargetSlotId, battleEvent.TargetName);

            if (battleEvent.Kind == "summon")
            {
                yield break;
            }

            if (battleEvent.Kind == "attack" || battleEvent.Kind == "skill" || battleEvent.Kind == "morale_extra")
            {
                yield return PulseAttacker(source, duration);
            }

            if (battleEvent.Kind == "damage" || battleEvent.Kind == "critical_damage" || battleEvent.Kind == "block" || battleEvent.Kind == "immune")
            {
                UpdateBattleStageLabel(target, battleEvent.TargetName, battleEvent.TargetHp, battleEvent.TargetMaxHp);
                yield return ShakeHitTarget(target, battleEvent.Kind == "critical_damage");
                yield return FlashTarget(target, battleEvent.Kind == "critical_damage" ? new Color32(255, 196, 78, 255) : new Color32(210, 64, 64, 255), duration);
            }
            else if (battleEvent.Kind == "death")
            {
                UpdateBattleStageLabel(target, battleEvent.TargetName, 0, Mathf.Max(1, battleEvent.TargetMaxHp));
                MarkBattleStageDead(target);
            }
        }

        private static IEnumerator PulseAttacker(BattleStageUnitView view, float duration)
        {
            if (view?.Rect == null)
            {
                yield break;
            }

            var originScale = view.Rect.localScale;
            var pulseScale = new Vector3(originScale.x * 1.06f, originScale.y * 1.06f, originScale.z);
            var half = Mathf.Max(0.02f, duration * 0.5f);
            var elapsed = 0f;
            while (elapsed < half)
            {
                view.Rect.localScale = Vector3.Lerp(originScale, pulseScale, elapsed / half);
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                view.Rect.localScale = Vector3.Lerp(pulseScale, originScale, elapsed / half);
                elapsed += Time.deltaTime;
                yield return null;
            }

            view.Rect.localScale = originScale;
        }

        private static IEnumerator FlashTarget(BattleStageUnitView view, Color color, float duration)
        {
            var hitImage = view?.UnitView?.UnitIconImage;
            if (hitImage == null)
            {
                yield break;
            }

            var original = hitImage.color;
            hitImage.color = color;
            yield return new WaitForSeconds(Mathf.Max(0.04f, duration));
            if (hitImage != null)
            {
                hitImage.color = original;
            }
        }

        private static void UpdateBattleStageLabel(BattleStageUnitView view, string fallbackName, int hp, int maxHp)
        {
            if (view == null)
            {
                return;
            }

            view.Hp = Mathf.Clamp(hp, 0, Mathf.Max(1, maxHp));
            view.MaxHp = Mathf.Max(1, maxHp);
            view.CurrentCount = ResolveCurrentCount(view.Hp, view.HpPerUnit);
            var unitHpMax = Mathf.Max(1, view.HpPerUnit);
            var currentUnitHp = ResolveCurrentUnitHp(view.Hp, unitHpMax);
            if (view.UnitView != null)
            {
                view.UnitView.SetHealth(currentUnitHp, unitHpMax, $"{currentUnitHp}/{unitHpMax}");
            }

            if (view.Label == null)
            {
                return;
            }

            var name = string.IsNullOrWhiteSpace(view.Name) ? fallbackName : view.Name;
            view.Label.text = $"{new string('*', Mathf.Clamp(view.Star, 0, 6))}\n{name}\n数量 {Mathf.Max(0, view.CurrentCount)}";
        }

        private static int ResolveCurrentCount(int totalHp, int hpPerUnit)
        {
            return totalHp <= 0 ? 0 : Mathf.CeilToInt(totalHp / (float)Mathf.Max(1, hpPerUnit));
        }

        private static int ResolveCurrentUnitHp(int totalHp, int hpPerUnit)
        {
            if (totalHp <= 0)
            {
                return 0;
            }

            var max = Mathf.Max(1, hpPerUnit);
            var remainder = totalHp % max;
            return remainder == 0 ? max : remainder;
        }

        private static void MarkBattleStageDead(BattleStageUnitView view)
        {
            if (view == null)
            {
                return;
            }

            view.Dead = true;
            view.Target = null;

            if (view.Backing != null)
            {
                view.Backing.color = new Color32(48, 48, 54, 80);
            }

            if (view.Label != null)
            {
                view.Label.color = new Color32(185, 185, 190, 255);
            }

            if (view.Rect != null)
            {
                Destroy(view.Rect.gameObject);
                view.Rect = null;
            }

            view.Backing = null;
            view.Label = null;
            view.UnitView = null;
        }

        private static BattleStageUnitView FindBattleStageView(Dictionary<string, BattleStageUnitView> views, bool playerSide, string slotId, string unitName)
        {
            if (views == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(slotId) && views.TryGetValue(BattleStageKey(playerSide, slotId), out var bySlot))
            {
                return bySlot;
            }

            if (!string.IsNullOrWhiteSpace(unitName) && views.TryGetValue(BattleStageKey(playerSide, unitName), out var byName))
            {
                return byName;
            }

            return null;
        }

        private BattleStageUnitView CreateSummonFromEvent(
            BattleEvent summonEvent,
            Dictionary<string, BattleStageUnitView> views,
            List<BattleStageUnitView> uniqueViews,
            List<BattleFloatingTextView> floatingTexts)
        {
            if (summonEvent == null || _battleFieldRoot == null)
            {
                return null;
            }

            var source = FindBattleStageView(views, summonEvent.SourcePlayerSide, summonEvent.SourceSlotId, summonEvent.SourceName);
            var definition = ProphecyGameSession.Instance.Data.FindUnit(summonEvent.TargetUnitId);
            if (definition == null)
            {
                return null;
            }

            var startCount = Mathf.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
            var hpPerUnit = Mathf.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp > 0 ? definition.hp : 1);
            var totalHp = startCount * hpPerUnit;
            var snapshot = new BattleUnitSnapshot
            {
                UnitId = definition.id,
                Name = definition.name,
                Star = definition.star,
                SlotId = string.IsNullOrWhiteSpace(summonEvent.TargetSlotId) ? summonEvent.SourceSlotId : summonEvent.TargetSlotId,
                MaxHp = totalHp,
                CurrentHp = totalHp,
                BaseCount = startCount,
                CurrentCount = startCount,
                MaxCount = startCount,
                HpPerUnit = hpPerUnit,
                CurrentTotalHp = totalHp,
                Attack = Mathf.Max(1, definition.attack),
                Defense = Mathf.Max(0, definition.defense),
                Power = Mathf.Max(1, definition.power),
                DamageMin = Mathf.Max(1, definition.damageMin),
                DamageMax = Mathf.Max(Mathf.Max(1, definition.damageMin), definition.damageMax),
                Speed = Mathf.Max(1, definition.speed),
                Luck = Mathf.Max(0, definition.luck),
                Morale = Mathf.Max(0, definition.morale),
                Range = Mathf.Max(1f, definition.range),
                Size = Mathf.Max(20, definition.size),
                AttackInterval = Mathf.Max(0.2f, definition.attackInterval),
                Summoned = true
            };
            var view = CreateBattleStagePositionedUnit(_battleFieldRoot, snapshot, summonEvent.SourcePlayerSide);
            if (view == null)
            {
                return null;
            }

            view.IsSummon = true;
            view.SummonDuration = 4f;
            if (source?.Rect != null && view.Rect != null && string.IsNullOrWhiteSpace(summonEvent.TargetSlotId))
            {
                var offset = new Vector2(summonEvent.SourcePlayerSide ? -54f : 54f, 42f);
                view.Rect.anchoredPosition = source.Rect.anchoredPosition + offset;
            }

            uniqueViews.Add(view);
            AddBattleStageView(views, summonEvent.SourcePlayerSide, view.SlotId, summonEvent.TargetName, view);
            AddFloatingText($"召唤{view.Name}", view.Rect.anchoredPosition + new Vector2(0f, 78f), new Color32(160, 232, 255, 255), 18, floatingTexts);
            return view;
        }

        private void ApplyControlEvent(BattleEvent controlEvent, Dictionary<string, BattleStageUnitView> views, List<BattleFloatingTextView> floatingTexts)
        {
            if (controlEvent == null)
            {
                return;
            }

            var target = FindBattleStageView(views, controlEvent.TargetPlayerSide, controlEvent.TargetSlotId, controlEvent.TargetName);
            if (target?.Rect == null || target.Dead)
            {
                return;
            }

            var duration = Mathf.Max(0.2f, controlEvent.Amount / 1000f);
            target.StunRemaining = Mathf.Max(target.StunRemaining, duration);
            target.MoveLockRemaining = Mathf.Max(target.MoveLockRemaining, duration);
            target.AttackLockRemaining = Mathf.Max(target.AttackLockRemaining, duration);
            AddFloatingText("锁定", target.Rect.anchoredPosition + new Vector2(0f, 88f), new Color32(150, 210, 255, 255), 18, floatingTexts);
        }

        private void ApplyCriticalDamageFeedback(BattleEvent damageEvent, Dictionary<string, BattleStageUnitView> views, List<BattleFloatingTextView> floatingTexts, List<BattleEffectBurstView> bursts)
        {
            var target = FindBattleStageView(views, damageEvent.TargetPlayerSide, damageEvent.TargetSlotId, damageEvent.TargetName);
            if (target?.Rect == null || target.Dead)
            {
                return;
            }

            AddFloatingText($"-{Mathf.Max(1, damageEvent.Amount)}❤️", target.Rect.anchoredPosition + new Vector2(0f, 64f), new Color32(255, 226, 112, 255), 28, floatingTexts, true);
            StartCoroutine(ShakeHitTarget(target, true));
            SpawnEffectBurst(target.Rect.anchoredPosition, new Color32(255, 196, 78, 150), bursts);
        }

        private static string BattleStageKey(bool playerSide, string key)
        {
            return $"{(playerSide ? "P" : "E")}:{key}";
        }

        private static List<BattleEvent> SelectBattlePlaybackEvents(BattleStubResult result)
        {
            var source = (result?.Events ?? new List<BattleEvent>())
                .Where(item => item.Kind != "morale_check")
                .ToList();
            if (source.Count <= 120)
            {
                return source;
            }

            var forced = new HashSet<int>();
            var selected = new List<BattleEvent>();
            var step = Mathf.Max(1, Mathf.CeilToInt(source.Count / 110f));
            for (var i = 0; i < source.Count; i += 1)
            {
                var battleEvent = source[i];
                var important = battleEvent.Kind == "death"
                    || battleEvent.Kind == "victory"
                    || battleEvent.Kind == "defeat"
                    || battleEvent.Kind == "count_gain"
                    || battleEvent.Kind == "lucky_crit"
                    || battleEvent.Kind == "morale_extra";
                if (important || i % step == 0)
                {
                    forced.Add(i);
                    selected.Add(battleEvent);
                }

                if (battleEvent.Kind == "morale_extra")
                {
                    ForceMoraleExtraFollowupEvents(source, i, forced, selected);
                }
            }

            if (selected.Count == 0 || selected[selected.Count - 1] != source[source.Count - 1])
            {
                selected.Add(source[source.Count - 1]);
            }

            return selected
                .Distinct()
                .OrderBy(item => item.Time)
                .ThenBy(item => source.IndexOf(item))
                .ToList();
        }

        private static void ForceMoraleExtraFollowupEvents(List<BattleEvent> source, int moraleExtraIndex, HashSet<int> forced, List<BattleEvent> selected)
        {
            if (source == null || forced == null || selected == null)
            {
                return;
            }

            var sourceEvent = source[moraleExtraIndex];
            for (var i = moraleExtraIndex + 1; i < source.Count && i <= moraleExtraIndex + 6; i += 1)
            {
                var battleEvent = source[i];
                if (battleEvent.SourceUnitId != sourceEvent.SourceUnitId || battleEvent.SourcePlayerSide != sourceEvent.SourcePlayerSide)
                {
                    continue;
                }

                if (battleEvent.Kind == "morale_extra")
                {
                    break;
                }

                if (forced.Add(i))
                {
                    selected.Add(battleEvent);
                }

                if (battleEvent.Kind == "death")
                {
                    break;
                }
            }
        }

        private void WriteBattleTurnDebugLog(BattleStubResult result)
        {
            if (result?.Events == null || result.Events.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Battle turn debug {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Result: {(result.Victory ? "Victory" : "Defeat")}  PlayerScore={result.PlayerScore}  EnemyScore={result.EnemyScore}  HpDelta={result.HpDelta}");
            builder.AppendLine("Flags: DUP_TURN=同一单位同一轮出现多次行动开始；DUP_ATTACK=同一单位同一轮出现多次普通攻击。");

            var currentRound = 0;
            var roundTurnCounts = new Dictionary<string, int>();
            var roundAttackCounts = new Dictionary<string, int>();
            var duplicateFound = false;

            foreach (var battleEvent in result.Events.OrderBy(item => item.Time))
            {
                if (battleEvent.Kind == "round")
                {
                    currentRound = battleEvent.Amount;
                    roundTurnCounts.Clear();
                    roundAttackCounts.Clear();
                    builder.AppendLine();
                    builder.AppendLine($"=== Round {currentRound} @ {battleEvent.Time:0.00}s ===");
                    continue;
                }

                var flags = string.Empty;
                var sourceKey = BuildBattleDebugSourceKey(battleEvent);
                if (battleEvent.Kind == "turn" && !string.IsNullOrWhiteSpace(sourceKey))
                {
                    roundTurnCounts.TryGetValue(sourceKey, out var count);
                    count += 1;
                    roundTurnCounts[sourceKey] = count;
                    if (count > 1)
                    {
                        duplicateFound = true;
                        flags = AppendBattleDebugFlag(flags, $"DUP_TURN#{count}");
                    }
                }
                else if (battleEvent.Kind == "attack" && !string.IsNullOrWhiteSpace(sourceKey))
                {
                    roundAttackCounts.TryGetValue(sourceKey, out var count);
                    count += 1;
                    roundAttackCounts[sourceKey] = count;
                    if (count > 1)
                    {
                        duplicateFound = true;
                        flags = AppendBattleDebugFlag(flags, $"DUP_ATTACK#{count}");
                    }
                }

                builder.AppendLine(FormatBattleDebugEvent(battleEvent, currentRound, flags));
            }

            builder.AppendLine();
            builder.AppendLine(duplicateFound ? "Duplicate flag detected." : "No duplicate turn/attack flag detected in authoritative battle events.");

            try
            {
                var path = Path.Combine(Application.persistentDataPath, "battle_turn_debug.log");
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
                Debug.Log($"[BattleTurnLog] Saved to {path}. Events={result.Events.Count}, DuplicateFlag={duplicateFound}");
                WriteLog($"战斗日志已写入：{path}");
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[BattleTurnLog] Failed to write battle log: {exception}");
                WriteLog("战斗日志写入失败，已输出到 Unity Console。");
            }
        }

        private static string BuildBattleDebugSourceKey(BattleEvent battleEvent)
        {
            if (battleEvent == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(battleEvent.SourceInstanceId))
            {
                return battleEvent.SourceInstanceId;
            }

            return $"{(battleEvent.SourcePlayerSide ? "P" : "E")}:{battleEvent.SourceSlotId}:{battleEvent.SourceUnitId}:{battleEvent.SourceName}";
        }

        private static string AppendBattleDebugFlag(string existing, string flag)
        {
            return string.IsNullOrWhiteSpace(existing) ? flag : $"{existing},{flag}";
        }

        private static string FormatBattleDebugEvent(BattleEvent battleEvent, int round, string flags)
        {
            if (battleEvent == null)
            {
                return string.Empty;
            }

            var prefix = string.IsNullOrWhiteSpace(flags) ? string.Empty : $" [{flags}]";
            var source = FormatBattleDebugUnit(
                battleEvent.SourceName,
                battleEvent.SourcePlayerSide,
                battleEvent.SourceSlotId,
                battleEvent.SourceInstanceId,
                battleEvent.SourceHp,
                battleEvent.SourceMaxHp);
            var target = FormatBattleDebugUnit(
                battleEvent.TargetName,
                battleEvent.TargetPlayerSide,
                battleEvent.TargetSlotId,
                battleEvent.TargetInstanceId,
                battleEvent.TargetHp,
                battleEvent.TargetMaxHp);
            var destination = string.IsNullOrWhiteSpace(battleEvent.DestinationSlotId) ? string.Empty : $" -> {battleEvent.DestinationSlotId}";
            var message = string.IsNullOrWhiteSpace(battleEvent.Message) ? battleEvent.Kind : battleEvent.Message;

            switch (battleEvent.Kind)
            {
                case "turn":
                case "turn_skip":
                    return $"{battleEvent.Time:0.00}s R{round} {battleEvent.Kind}{prefix}: {source}";
                case "move":
                    return $"{battleEvent.Time:0.00}s R{round} move{prefix}: {source}{destination} target={target}";
                case "attack":
                case "skill":
                case "morale_extra":
                case "lucky_crit":
                    return $"{battleEvent.Time:0.00}s R{round} {battleEvent.Kind}{prefix}: {source} -> {target}";
                case "morale_check":
                    return $"{battleEvent.Time:0.00}s R{round} morale_check{prefix}: {source} -> {target} chance={battleEvent.Amount / 1000f:0.000} {message}";
                case "damage":
                case "critical_damage":
                case "block":
                case "immune":
                case "count_gain":
                    return $"{battleEvent.Time:0.00}s R{round} {battleEvent.Kind}{prefix}: {source} -> {target} amount={battleEvent.Amount}";
                case "death":
                case "summon":
                case "control":
                    return $"{battleEvent.Time:0.00}s R{round} {battleEvent.Kind}{prefix}: {source} -> {target} amount={battleEvent.Amount}";
                default:
                    return $"{battleEvent.Time:0.00}s R{round} {battleEvent.Kind}{prefix}: {message}";
            }
        }

        private static string FormatBattleDebugUnit(string name, bool playerSide, string slotId, string instanceId, int hp, int maxHp)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "-";
            }

            var side = playerSide ? "P" : "E";
            var id = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : $" id={instanceId}";
            return $"{side}:{name}@{slotId}{id} HP={hp}/{Mathf.Max(1, maxHp)}";
        }

        private static string FormatBattleEvent(BattleEvent battleEvent)
        {
            if (battleEvent == null)
            {
                return string.Empty;
            }

            var time = $"{battleEvent.Time:0.0}s";
            switch (battleEvent.Kind)
            {
                case "damage":
                    return $"{time} {battleEvent.SourceName} -> {battleEvent.TargetName} 伤害 {battleEvent.Amount}  HP {battleEvent.TargetHp}/{Mathf.Max(1, battleEvent.TargetMaxHp)}";
                case "critical_damage":
                    return $"{time} Critical: {battleEvent.SourceName} -> {battleEvent.TargetName} damage {battleEvent.Amount}  HP {battleEvent.TargetHp}/{Mathf.Max(1, battleEvent.TargetMaxHp)}";
                case "attack":
                case "morale_extra":
                case "lucky_crit":
                case "block":
                case "immune":
                case "death":
                case "victory":
                case "defeat":
                case "start":
                case "skill":
                case "count_gain":
                case "summon":
                    return $"{time} {battleEvent.Message}";
                default:
                    return string.IsNullOrWhiteSpace(battleEvent.Message) ? $"{time} {battleEvent.Kind}" : $"{time} {battleEvent.Message}";
            }
        }

        private void ShowBattleStage()
        {
            HideOperationalUiForBattle();
            if (battleStagePanel != null)
            {
                battleStagePanel.SetActive(true);
                var image = battleStagePanel.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color32(10, 8, 26, 0);
                    image.raycastTarget = false;
                }

                battleStagePanel.transform.SetAsLastSibling();
                BringBattleLeftStatusPanelToFront();
            }
        }

        private void BringBattleLeftStatusPanelToFront()
        {
            if (runPanel == null)
            {
                return;
            }

            foreach (Transform child in runPanel.transform)
            {
                if (IsBattleLeftStatusPanel(child.name) && child.gameObject.activeSelf)
                {
                    child.SetAsLastSibling();
                }
            }
        }

        private void HideOperationalUiForBattle()
        {
            if (runPanel == null || _battleUiVisibility.Count > 0)
            {
                return;
            }

            foreach (Transform child in runPanel.transform)
            {
                var obj = child.gameObject;
                if (obj == battleStagePanel)
                {
                    RememberAndSetBattleActive(obj, true);
                    continue;
                }

                if (IsBattleLeftStatusPanel(obj.name))
                {
                    RememberAndSetBattleActive(obj, true);
                    ApplyBattlePlayerPanelVisibility(child);
                    ApplyBattlePlayerPanelCompactLayout(child);
                    child.SetAsLastSibling();
                    continue;
                }

                RememberAndSetBattleActive(obj, false);
            }
        }

        private void ApplyBattlePlayerPanelVisibility(Transform playerPanel)
        {
            foreach (Transform child in playerPanel)
            {
                var keep = IsBattleLeftStatusChild(child.name);
                RememberAndSetBattleActive(child.gameObject, keep);
            }
        }

        private void ApplyBattlePlayerPanelCompactLayout(Transform playerPanel)
        {
            if (playerPanel == null)
            {
                return;
            }

            var panelRect = playerPanel as RectTransform;
            RememberBattleRectTransform(panelRect);
            SetLocalTopLeft(panelRect, 22f, 24f, 220f, 260f);

            var panelImage = playerPanel.GetComponent<Image>();
            RememberBattleImage(panelImage);
            if (panelImage != null)
            {
                panelImage.color = new Color32(16, 10, 39, 0);
                panelImage.raycastTarget = false;
            }

            var hero = playerPanel.Find("HeroPortrait") as RectTransform;
            RememberBattleRectTransform(hero);
            SetLocalTopLeft(hero, 10f, 10f, 200f, 200f);
            RememberBattleImage(hero != null ? hero.GetComponent<Image>() : null);

            var hpBar = playerPanel.Find("HpBar") as RectTransform;
            RememberBattleRectTransform(hpBar);
            SetLocalTopLeft(hpBar, 10f, 218f, 200f, 28f);
            RememberBattleImage(hpBar != null ? hpBar.GetComponent<Image>() : null);
        }

        private static bool IsBattleLeftStatusPanel(string objectName)
        {
            return objectName == "PlayerPanelV2" || objectName == "PlayerPanel";
        }

        private static bool IsBattleLeftStatusChild(string objectName)
        {
            return objectName == "HeroPortrait"
                || objectName == "HpBar";
        }

        private void RememberBattleRectTransform(RectTransform rect)
        {
            if (rect == null || _battleRectTransformStates.ContainsKey(rect))
            {
                return;
            }

            _battleRectTransformStates.Add(rect, new BattleRectTransformState
            {
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                OffsetMin = rect.offsetMin,
                OffsetMax = rect.offsetMax
            });
        }

        private void AddDelayedFloatingText(string text, Vector2 position, Color color, int fontSize, List<BattleFloatingTextView> floatingTexts, float delay)
        {
            StartCoroutine(AddDelayedFloatingTextRoutine(text, position, color, fontSize, floatingTexts, delay));
        }

        private static void AddFloatingTextOutline(GameObject textObject)
        {
            if (textObject == null || textObject.GetComponent<Outline>() != null)
            {
                return;
            }

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 230);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private IEnumerator AddDelayedFloatingTextRoutine(string text, Vector2 position, Color color, int fontSize, List<BattleFloatingTextView> floatingTexts, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            AddFloatingText(text, position, color, fontSize, floatingTexts);
        }

        private void RememberBattleImage(Image image)
        {
            if (image == null || _battleImageStates.ContainsKey(image))
            {
                return;
            }

            _battleImageStates.Add(image, new BattleImageState
            {
                Color = image.color,
                RaycastTarget = image.raycastTarget
            });
        }

        private void RememberAndSetBattleActive(GameObject obj, bool active)
        {
            if (obj == null)
            {
                return;
            }

            if (!_battleUiVisibility.ContainsKey(obj))
            {
                _battleUiVisibility.Add(obj, obj.activeSelf);
            }

            obj.SetActive(active);
        }

        private void RestoreOperationalUiAfterBattle()
        {
            foreach (var pair in _battleRectTransformStates.ToList())
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.anchorMin = pair.Value.AnchorMin;
                pair.Key.anchorMax = pair.Value.AnchorMax;
                pair.Key.pivot = pair.Value.Pivot;
                pair.Key.anchoredPosition = pair.Value.AnchoredPosition;
                pair.Key.sizeDelta = pair.Value.SizeDelta;
                pair.Key.offsetMin = pair.Value.OffsetMin;
                pair.Key.offsetMax = pair.Value.OffsetMax;
            }

            foreach (var pair in _battleImageStates.ToList())
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.color = pair.Value.Color;
                pair.Key.raycastTarget = pair.Value.RaycastTarget;
            }

            foreach (var pair in _battleUiVisibility.ToList())
            {
                if (pair.Key != null)
                {
                    pair.Key.SetActive(pair.Value);
                }
            }

            _battleUiVisibility.Clear();
            _battleRectTransformStates.Clear();
            _battleImageStates.Clear();
        }

        private void SetBattleStageProgress(float amount)
        {
            var progress = Mathf.Clamp01(amount);
            if (battleStageProgressFill != null)
            {
                battleStageProgressFill.fillAmount = progress;
                var rect = battleStageProgressFill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(progress, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private IEnumerator PlayWinnerStarsToPlayerHp(IReadOnlyList<BattleStageUnitView> winners, int hpBefore, int hpAfter)
        {
            var overlay = (runPanel != null ? runPanel.transform : transform) as RectTransform;
            var hpRect = ResolveHpBarRect();
            if (overlay == null || hpRect == null || winners == null || winners.Count == 0)
            {
                yield return AnimatePlayerHpDisplay(hpBefore, hpAfter);
                yield break;
            }

            var target = GetCenterInOverlay(hpRect, overlay);
            var stars = new List<Text>();
            foreach (var winner in winners.Where(unit => unit?.Rect != null))
            {
                var start = GetCenterInOverlay(winner.Rect, overlay) + new Vector2(0f, 74f);
                var count = Mathf.Clamp(winner.Star, 1, 6);
                for (var i = 0; i < count; i += 1)
                {
                    var starObject = new GameObject("DamageStar", typeof(Text));
                    starObject.transform.SetParent(overlay, false);
                    var rect = starObject.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(42f, 42f);
                    rect.anchoredPosition = start + new Vector2((i - (count - 1) * 0.5f) * 22f, 0f);

                    var text = starObject.GetComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 30;
                    text.fontStyle = FontStyle.Bold;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.text = "\u2b50";
                    text.color = new Color32(255, 222, 88, 255);
                    text.raycastTarget = false;
                    var outline = starObject.AddComponent<Outline>();
                    outline.effectColor = new Color32(0, 0, 0, 220);
                    outline.effectDistance = new Vector2(2f, -2f);
                    stars.Add(text);
                }
            }

            var starts = stars.Select(star => star.rectTransform.anchoredPosition).ToList();
            var elapsed = 0f;
            const float duration = 0.72f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                for (var i = 0; i < stars.Count; i += 1)
                {
                    if (stars[i] == null)
                    {
                        continue;
                    }

                    var arc = new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 90f);
                    stars[i].rectTransform.anchoredPosition = Vector2.Lerp(starts[i], target, t) + arc;
                    stars[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                }

                yield return null;
            }

            foreach (var star in stars.Where(star => star != null))
            {
                Destroy(star.gameObject);
            }

            yield return AnimatePlayerHpDisplay(hpBefore, hpAfter);
        }

        private IEnumerator AnimatePlayerHpDisplay(int hpBefore, int hpAfter)
        {
            var from = Mathf.Clamp(hpBefore, 0, 100);
            var to = Mathf.Clamp(hpAfter, 0, 100);
            var elapsed = 0f;
            const float duration = 0.65f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var value = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration))));
                SetPlayerHpDisplay(value);
                yield return null;
            }

            SetPlayerHpDisplay(to);
        }

        private void SetPlayerHpDisplay(int hp)
        {
            var clamped = Mathf.Clamp(hp, 0, 100);
            if (hpLabel != null)
            {
                hpLabel.text = $"{clamped}/100";
            }

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = Mathf.Clamp01(clamped / 100f);
            }
        }

        private RectTransform ResolveHpBarRect()
        {
            if (hpFillImage == null)
            {
                return hpLabel != null ? hpLabel.rectTransform : null;
            }

            return hpFillImage.transform.parent as RectTransform ?? hpFillImage.rectTransform;
        }

        private void SetBattleStageText(string status, string log)
        {
            if (battleStageStatusLabel != null)
            {
                battleStageStatusLabel.text = status;
            }

            if (battleStageLogLabel != null)
            {
                battleStageLogLabel.text = log;
            }
        }

        private void RebuildBattleStageUnits()
        {
            ClearChildren(battlePlayerRoot);
            ClearChildren(battleEnemyRoot);
            EnableLayout(battlePlayerRoot);
            EnableLayout(battleEnemyRoot);

            if (battlePlayerRoot != null)
            {
                foreach (var unit in Run.boardUnits.OrderBy(unit => unit.boardSlotId))
                {
                    CreateBattleStageUnit(battlePlayerRoot, unit.name, unit.star, unit.unitId, unit.name, true);
                }
            }

            if (battleEnemyRoot != null)
            {
                foreach (var unit in BuildBattleStageEnemyPreview())
                {
                    CreateBattleStageUnit(battleEnemyRoot, unit.name, unit.star, unit.id, unit.name, false);
                }
            }
        }

        private static BattleStubResult CreateRealtimePreviewStageResult(BattleRealtimeResult realtime, BattleStubResult authoritative, BattlePreviewResult preview)
        {
            if (realtime == null)
            {
                return authoritative;
            }

            var hpLoss = realtime.Victory ? 0 : Mathf.Max(1, authoritative?.HpDelta < 0 ? -authoritative.HpDelta : 1);
            var comparison = authoritative == null
                ? string.Empty
                : $"\nStable resolver: {(authoritative.Victory ? "win" : "loss")}, HP {authoritative.HpDelta}.";

            return new BattleStubResult
            {
                Victory = realtime.Victory,
                PlayerScore = preview?.PlayerScore ?? authoritative?.PlayerScore ?? 0,
                EnemyScore = preview?.EnemyScore ?? authoritative?.EnemyScore ?? 0,
                HpDelta = -hpLoss,
                PlayerDamage = realtime.PlayerDamage,
                EnemyDamage = realtime.EnemyDamage,
                Summary = $"{realtime.Summary}{comparison}",
                Events = realtime.Events ?? new List<BattleEvent>(),
                PlayerUnits = realtime.PlayerUnits ?? new List<BattleUnitSnapshot>(),
                EnemyUnits = realtime.EnemyUnits ?? new List<BattleUnitSnapshot>()
            };
        }

        private static string FormatRealtimePreviewComparison(BattleRealtimeResult realtime, BattleStubResult authoritative)
        {
            if (realtime == null || authoritative == null)
            {
                return "实时战斗预览：缺少对比结果。";
            }

            var stableState = authoritative.Victory ? "胜" : "败";
            var realtimeState = realtime.Victory ? "胜" : "败";
            var match = realtime.Victory == authoritative.Victory ? "一致" : "不一致";
            return $"实时预览 {realtimeState} / 稳定结算 {stableState}，结果{match}。";
        }

        private void RebuildBattleStageResultUnits(BattleStubResult result)
        {
            ClearChildren(battlePlayerRoot);
            ClearChildren(battleEnemyRoot);
            EnableLayout(battlePlayerRoot);
            EnableLayout(battleEnemyRoot);

            if (result == null)
            {
                return;
            }

            if (battlePlayerRoot != null)
            {
                foreach (var unit in result.PlayerUnits.Where(unit => !unit.Summoned).OrderBy(unit => unit.SlotId))
                {
                    CreateBattleStageUnit(battlePlayerRoot, unit, true);
                }
            }

            if (battleEnemyRoot != null)
            {
                foreach (var unit in result.EnemyUnits.Where(unit => !unit.Summoned).OrderBy(unit => unit.SlotId))
                {
                    CreateBattleStageUnit(battleEnemyRoot, unit, false);
                }
            }
        }

        private static string FormatBattleStageResult(BattleStubResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            var hpLine = result.HpDelta < 0 ? $"玩家生命 {result.HpDelta}" : "玩家生命无损失";
            return $"{result.Summary}\n我方伤害 {result.PlayerDamage}  敌方伤害 {result.EnemyDamage}  {hpLine}";
        }

        private IEnumerable<ProphecyCentury.Data.UnitDefinition> BuildBattleStageEnemyPreview()
        {
            var maxStar = Mathf.Min(6, 1 + Run.round / 3 + (Run.round % 5 == 0 ? 1 : 0));
            return ProphecyGameSession.Instance.Data.Units
                .Where(unit => unit != null && !unit.hidden && unit.id != "light_illusion" && unit.star <= maxStar)
                .OrderByDescending(unit => unit.star)
                .ThenBy(unit => unit.name)
                .Take(Mathf.Max(3, Mathf.Min(6, Run.boardUnits.Count + 1)));
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private static void EnableLayout(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var layout = root.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.enabled = true;
            }
        }

        private static void DisableLayout(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var layout = root.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
            }
        }

        private static void CreateBattleStageUnit(Transform root, string label, int star, string unitId, string iconName, bool playerSide, int hp = 1, int maxHp = 1)
        {
            var unitObject = new GameObject(playerSide ? "PlayerBattleUnit" : "EnemyBattleUnit", typeof(Image), typeof(LayoutElement));
            unitObject.transform.SetParent(root, false);
            unitObject.GetComponent<Image>().color = playerSide ? new Color32(38, 70, 96, 245) : new Color32(92, 44, 58, 245);
            var layout = unitObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 176f;
            layout.preferredHeight = 214f;

            var iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(unitObject.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 24f);
            iconRect.sizeDelta = new Vector2(112f, 112f);
            iconRect.localScale = playerSide ? Vector3.one : new Vector3(-1f, 1f, 1f);
            RuntimeUnitIconCache.ApplyTo(iconObject.GetComponent<Image>(), iconName);

            var text = CreateChildText(unitObject.transform, $"{new string('*', Mathf.Clamp(star, 0, 6))}\n{label}", 32, TextAnchor.LowerCenter, new Vector2(8f, 8f), new Vector2(-8f, -136f));
            text.color = Color.white;

            var healthBackObject = new GameObject("HealthBar", typeof(Image));
            healthBackObject.transform.SetParent(unitObject.transform, false);
            var healthBackRect = healthBackObject.GetComponent<RectTransform>();
            healthBackRect.anchorMin = new Vector2(0.5f, 0f);
            healthBackRect.anchorMax = new Vector2(0.5f, 0f);
            healthBackRect.pivot = new Vector2(0.5f, 0.5f);
            healthBackRect.anchoredPosition = new Vector2(0f, 58f);
            healthBackRect.sizeDelta = new Vector2(136f, 12f);
            healthBackObject.GetComponent<Image>().color = new Color32(50, 18, 24, 230);

            var healthFillObject = new GameObject("Fill", typeof(Image));
            healthFillObject.transform.SetParent(healthBackObject.transform, false);
            var healthFillRect = healthFillObject.GetComponent<RectTransform>();
            var healthAmount = Mathf.Clamp01(Mathf.Max(0, hp) / (float)Mathf.Max(1, maxHp));
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = new Vector2(healthAmount, 1f);
            healthFillRect.offsetMin = Vector2.zero;
            healthFillRect.offsetMax = Vector2.zero;
            var healthFill = healthFillObject.GetComponent<Image>();
            healthFill.color = new Color32(212, 38, 48, 255);
            healthFill.fillAmount = healthAmount;
        }

        private BattleStageUnitView CreateBattleStagePositionedUnit(Transform root, string label, int star, string unitId, string iconName, string slotId, bool playerSide)
        {
            if (root == null)
            {
                return null;
            }

            var rootRect = root.GetComponent<RectTransform>();
            var rootSize = rootRect != null && rootRect.rect.size.sqrMagnitude > 1f ? rootRect.rect.size : new Vector2(720f, 340f);
            var definition = ProphecyGameSession.Instance.Data.FindUnit(unitId);
            var start = BattleStartPosition(rootSize, slotId, playerSide);
            var range = Mathf.Max(1f, definition?.range ?? 1f);
            var rangeHold = Mathf.Clamp(range * rootSize.x * 0.035f, rootSize.x * 0.05f, rootSize.x * 0.28f);
            var fight = start + new Vector2(playerSide ? rootSize.x * 0.38f - rangeHold : -rootSize.x * 0.38f + rangeHold, 0f);
            var unitView = CreateBattleUnitObject(root, label, star, iconName, playerSide);
            if (unitView?.Rect == null)
            {
                return null;
            }

            unitView.Rect.anchoredPosition = start;
            unitView.Rect.localScale = Vector3.one * CalculateBattleUnitScale(rootSize);

            return new BattleStageUnitView
            {
                Rect = unitView.Rect,
                Backing = unitView.Backing,
                Label = unitView.Label,
                UnitView = unitView.View,
                StartPosition = start,
                FightPosition = fight,
                Name = label,
                UnitId = unitId,
                SlotId = slotId,
                Star = star,
                Speed = Mathf.Max(1, definition?.speed ?? 3),
                Range = range,
                Size = Mathf.Max(20, definition?.size ?? 35),
                Hp = Mathf.Max(1, definition?.hp ?? 100),
                MaxHp = Mathf.Max(1, definition?.hp ?? 100),
                CurrentCount = Mathf.Max(1, definition != null && definition.defaultCount > 0 ? definition.defaultCount : definition != null && definition.startCount > 0 ? definition.startCount : definition != null && definition.baseCount > 0 ? definition.baseCount : 1),
                MaxCount = Mathf.Max(1, definition != null && definition.defaultCount > 0 ? definition.defaultCount : definition != null && definition.startCount > 0 ? definition.startCount : definition != null && definition.baseCount > 0 ? definition.baseCount : 1),
                HpPerUnit = Mathf.Max(1, definition?.hpPerUnit ?? definition?.hp ?? 1),
                DamageMin = Mathf.Max(1, definition?.damageMin ?? 1),
                DamageMax = Mathf.Max(1, definition?.damageMax ?? 1),
                Attack = Mathf.Max(1, definition?.attack ?? 10),
                Defense = Mathf.Max(0, definition?.defense ?? 0),
                Power = Mathf.Max(1, definition?.power ?? 1),
                AttackInterval = Mathf.Max(0.2f, definition?.attackInterval ?? 1f),
                PlayerSide = playerSide
            };
        }

        private BattleStageUnitView CreateBattleStagePositionedUnit(Transform root, BattleUnitSnapshot unit, bool playerSide)
        {
            var view = CreateBattleStagePositionedUnit(root, unit.Name, unit.Star, unit.UnitId, unit.Name, unit.SlotId, playerSide);
            if (view == null)
            {
                return null;
            }

            view.Hp = Mathf.Max(0, unit.CurrentHp > 0 ? unit.CurrentHp : unit.MaxHp);
            view.MaxHp = Mathf.Max(1, unit.MaxHp);
            view.CurrentCount = Mathf.Max(0, unit.CurrentCount);
            view.MaxCount = Mathf.Max(1, unit.MaxCount > 0 ? unit.MaxCount : view.CurrentCount);
            view.HpPerUnit = Mathf.Max(1, unit.HpPerUnit);
            view.DamageMin = Mathf.Max(1, unit.DamageMin);
            view.DamageMax = Mathf.Max(view.DamageMin, unit.DamageMax);
            view.Attack = Mathf.Max(1, unit.Attack);
            view.Defense = Mathf.Max(0, unit.Defense);
            view.Power = Mathf.Max(1, unit.Power);
            view.Speed = Mathf.Max(1, unit.Speed);
            view.Range = Mathf.Max(1f, unit.Range);
            view.Size = Mathf.Max(20, unit.Size);
            view.AttackInterval = Mathf.Max(0.2f, unit.AttackInterval);
            UpdateBattleStageLabel(view, unit.Name, view.Hp, view.MaxHp);
            return view;
        }

        private BattleStagePrefabParts CreateBattleUnitObject(Transform root, string label, int star, string iconName, bool playerSide)
        {
            var prefab = ResolveBattleUnitPrefab();
            if (prefab != null)
            {
                var instance = Instantiate(prefab, root, false);
                instance.name = playerSide ? "PlayerBattleFighter" : "EnemyBattleFighter";
                var rect = instance.GetComponent<RectTransform>() ?? instance.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);

                var view = instance.GetComponent<BattleUnitView>() ?? instance.AddComponent<BattleUnitView>();
                view.Bind(iconName, label, star, playerSide);
                var backing = view.BackingImage;
                if (backing != null)
                {
                    backing.color = new Color(1f, 1f, 1f, 0f);
                }

                return new BattleStagePrefabParts
                {
                    Rect = rect,
                    Backing = backing,
                    Label = view.LabelText,
                    View = view
                };
            }

            return CreateScriptedBattleUnitObject(root, label, star, iconName, playerSide);
        }

        private GameObject ResolveBattleUnitPrefab()
        {
            if (battleUnitPrefab != null)
            {
                return battleUnitPrefab;
            }

            if (_cachedBattleUnitPrefab == null)
            {
                _cachedBattleUnitPrefab = Resources.Load<GameObject>(BattleUnitPrefabResourcePath);
            }

            return _cachedBattleUnitPrefab;
        }

        private static BattleStagePrefabParts CreateScriptedBattleUnitObject(Transform root, string label, int star, string iconName, bool playerSide)
        {
            var unitObject = new GameObject(playerSide ? "PlayerBattleFighter" : "EnemyBattleFighter", typeof(Image), typeof(BattleUnitView));
            unitObject.transform.SetParent(root, false);
            var rect = unitObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(190f, 230f);

            var backing = unitObject.GetComponent<Image>();
            backing.color = new Color(1f, 1f, 1f, 0f);
            backing.raycastTarget = false;

            var iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(unitObject.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 36f);
            iconRect.sizeDelta = new Vector2(128f, 128f);
            iconRect.localScale = playerSide ? Vector3.one : new Vector3(-1f, 1f, 1f);
            RuntimeUnitIconCache.ApplyTo(iconObject.GetComponent<Image>(), iconName);

            var text = CreateChildText(unitObject.transform, $"{new string('*', Mathf.Clamp(star, 0, 6))}\n{label}", 32, TextAnchor.LowerCenter, new Vector2(8f, 8f), new Vector2(-8f, -144f));
            text.name = "Label";
            text.color = Color.white;

            var healthBackObject = new GameObject("HealthBar", typeof(Image));
            healthBackObject.transform.SetParent(unitObject.transform, false);
            var healthBackRect = healthBackObject.GetComponent<RectTransform>();
            healthBackRect.anchorMin = new Vector2(0.5f, 0f);
            healthBackRect.anchorMax = new Vector2(0.5f, 0f);
            healthBackRect.pivot = new Vector2(0.5f, 0.5f);
            healthBackRect.anchoredPosition = new Vector2(0f, 68f);
            healthBackRect.sizeDelta = new Vector2(150f, 14f);
            healthBackObject.GetComponent<Image>().color = new Color32(50, 18, 24, 230);

            var healthFillObject = new GameObject("Fill", typeof(Image));
            healthFillObject.transform.SetParent(healthBackObject.transform, false);
            var healthFillRect = healthFillObject.GetComponent<RectTransform>();
            healthFillRect.anchorMin = Vector2.zero;
            healthFillRect.anchorMax = Vector2.one;
            healthFillRect.offsetMin = Vector2.zero;
            healthFillRect.offsetMax = Vector2.zero;
            var healthFill = healthFillObject.GetComponent<Image>();
            healthFill.color = new Color32(212, 38, 48, 255);
            healthFill.fillAmount = 1f;

            var scriptedView = unitObject.GetComponent<BattleUnitView>();
            scriptedView.SetHealth(1, 1);

            return new BattleStagePrefabParts
            {
                Rect = rect,
                Backing = backing,
                Label = text,
                View = scriptedView
            };
        }

        private static Vector2 BattleStartPosition(Vector2 rootSize, string slotId, bool playerSide)
        {
            if (TryGetBattleHexSlotCenter(rootSize, slotId, playerSide, out var hexCenter))
            {
                return hexCenter;
            }

            return playerSide
                ? new Vector2(rootSize.x * -0.36f, 0f)
                : new Vector2(rootSize.x * 0.36f, 0f);
        }

        private static bool TryGetBattleHexSlotCenter(Vector2 rootSize, string slotId, bool playerSide, out Vector2 center)
        {
            center = Vector2.zero;
            if (!TryMapBattleSlotToHex(slotId, playerSide, out var column, out var row))
            {
                return false;
            }

            return TryGetBattleHexCenter(rootSize, column, row, out center);
        }

        private static bool TryMapBattleSlotToHex(string slotId, bool playerSide, out int column, out int row)
        {
            column = 0;
            row = 0;
            if (TryParseBattleHexSlot(slotId, out column, out row))
            {
                return true;
            }

            switch (slotId)
            {
                case "4-1":
                    column = 0;
                    row = 0;
                    break;
                case "4-2":
                    column = 0;
                    row = 1;
                    break;
                case "4-3":
                    column = 0;
                    row = 2;
                    break;
                case "4-4":
                    column = 0;
                    row = 3;
                    break;
                case "3-1":
                    column = 1;
                    row = 0;
                    break;
                case "3-2":
                    column = 1;
                    row = 1;
                    break;
                case "3-3":
                    column = 1;
                    row = 2;
                    break;
                case "2-1":
                    column = 2;
                    row = 1;
                    break;
                case "2-2":
                    column = 2;
                    row = 2;
                    break;
                case "1-1":
                    column = 3;
                    row = 1;
                    break;
                default:
                    return false;
            }

            if (!playerSide)
            {
                column = BattleHexColumnCount - 1 - column;
            }

            return true;
        }

        private static bool TryParseBattleHexSlot(string slotId, out int column, out int row)
        {
            column = 0;
            row = 0;
            if (string.IsNullOrWhiteSpace(slotId) || !slotId.StartsWith("h-", System.StringComparison.Ordinal))
            {
                return false;
            }

            var parts = slotId.Split('-');
            if (parts.Length != 3 || !int.TryParse(parts[1], out var parsedColumn) || !int.TryParse(parts[2], out var parsedRow))
            {
                return false;
            }

            column = parsedColumn - 1;
            row = parsedRow - 1;
            return column >= 0
                && column < BattleHexColumnCount
                && row >= 0
                && row < BattleHexRowsByColumn[column];
        }

        private static bool TryGetBattleHexCenter(Vector2 rootSize, int column, int row, out Vector2 center)
        {
            center = Vector2.zero;
            if (column < 0 || column >= BattleHexColumnCount)
            {
                return false;
            }

            var rows = BattleHexRowsByColumn[column];
            if (row < 0 || row >= rows)
            {
                return false;
            }

            var cellSize = CalculateBattleHexCellSize(rootSize);
            var hexWidth = cellSize.x;
            var hexHeight = cellSize.y;
            var totalWidth = hexWidth + (BattleHexColumnCount - 1) * hexWidth * BattleHexHorizontalStep;
            var totalHeight = hexHeight * 4f;
            var left = -totalWidth * 0.5f + hexWidth * 0.5f;
            var top = totalHeight * 0.5f - hexHeight * 0.5f;
            var x = left + column * hexWidth * BattleHexHorizontalStep;
            var yOffset = rows == 3 ? -hexHeight * 0.5f : 0f;
            var y = top - row * hexHeight + yOffset;
            center = new Vector2(x, y);
            return true;
        }

        private static Vector2 CalculateBattleHexCellSize(Vector2 rootSize)
        {
            var widthLimit = Mathf.Max(1f, rootSize.x * 0.92f) / (1f + (BattleHexColumnCount - 1) * BattleHexHorizontalStep);
            var heightLimit = Mathf.Max(1f, rootSize.y * 0.82f) / (4f * BattleHexHeightRatio);
            var hexWidth = Mathf.Clamp(Mathf.Min(widthLimit, heightLimit), 76f, 280f);
            return new Vector2(hexWidth, hexWidth * BattleHexHeightRatio);
        }

        private static float CalculateBattleUnitScale(Vector2 rootSize)
        {
            var cellSize = CalculateBattleHexCellSize(rootSize);
            return Mathf.Clamp(Mathf.Min(cellSize.x / 260f, cellSize.y / 250f), 0.42f, 0.95f);
        }

        private static void ParseBattleSlot(string slotId, out int row, out int col)
        {
            row = 0;
            col = 0;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            var parts = slotId.Split('-');
            if (parts.Length != 2)
            {
                return;
            }

            int.TryParse(parts[0], out row);
            int.TryParse(parts[1], out col);
        }

        private static void CreateBattleStageUnit(Transform root, BattleUnitSnapshot unit, bool playerSide)
        {
            var label = $"{unit.Name}\n伤害 {unit.DamageDone}  击杀 {unit.Kills}";
            CreateBattleStageUnit(root, label, unit.Star, unit.UnitId, unit.Name, playerSide, unit.CurrentHp, unit.MaxHp);
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

            WriteLog(success ? $"已读取：{_saveGame.SavePath}" : "读取失败：没有可用存档。");
            RefreshView();
        }

        public void ReturnToTitle()
        {
            RuntimeUiBootstrap.ShowTitleScreen();
        }

        public void RefreshView()
        {
            StartRunIfNeeded();
            HideLegacyBoardInfoLabels();
            var data = ProphecyGameSession.Instance.Data;
            if (goldLabel != null)
            {
                goldLabel.text = string.Empty;
            }
            var roundText = $"{Run.gold}    第 {Run.round} 回合";
            if (roundLabel != null)
            {
                roundLabel.text = roundText;
            }

            SetTextLabel("RoundLabelV2", roundText);
            UpdateRoundGoldIcon();
            hpLabel.text = $"{Run.playerHp}/100";
            if (stateLabel != null)
            {
                stateLabel.text = string.Empty;
            }
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = Mathf.Clamp01(Run.playerHp / 100f);
            }
            RefreshShopMetaStars();
            RefreshShopActionLabels();

            var campaign = data.Campaigns.FirstOrDefault(item => item.id == Run.campaignId);
            var hero = data.Heroes.FirstOrDefault(item => item.id == Run.heroId);
            campaignLabel.text = $"战役：{(campaign != null ? campaign.name : Run.campaignId)}";
            heroLabel.text = $"英雄：{(hero != null ? hero.name : Run.heroId)}";

            if (shopText != null)
            {
                shopText.text = FormatShop();
            }

            if (handText != null)
            {
                handText.text = FormatHand();
            }

            if (boardText != null)
            {
                boardText.text = FormatBoard();
            }

            if (battlePreviewText != null)
            {
                battlePreviewText.text = FormatBattlePreview();
            }
            RefreshCardLists();
            EnsureBattleLogButton();
            RefreshBattleLogButton();
        }

        private void EnsureBattleLogButton()
        {
            if (_battleLogButton != null)
            {
                return;
            }

            var parent = runPanel != null ? runPanel.transform : transform;
            _battleLogButton = new GameObject("BattleLogButton", typeof(Image), typeof(Button));
            _battleLogButton.transform.SetParent(parent, false);
            var rect = _battleLogButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-18f, 18f);
            rect.sizeDelta = new Vector2(132f, 42f);

            var image = _battleLogButton.GetComponent<Image>();
            image.color = new Color32(36, 52, 82, 235);

            var button = _battleLogButton.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(OpenBattleLogModal);

            var text = CreateChildText(_battleLogButton.transform, "战斗记录", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.color = Color.white;
        }

        private void RefreshBattleLogButton()
        {
            if (_battleLogButton == null)
            {
                return;
            }

            _battleLogButton.SetActive(runPanel == null || runPanel.activeInHierarchy);
            _battleLogButton.transform.SetAsLastSibling();
        }

        private void OpenBattleLogModal()
        {
            EnsureBattleLogModal();
            if (_battleLogContentLabel != null)
            {
                _battleLogContentLabel.text = _latestBattleLogLines.Count == 0
                    ? "暂无战斗记录。"
                    : string.Join("\n", _latestBattleLogLines);
            }

            _battleLogModal.SetActive(true);
            _battleLogModal.transform.SetAsLastSibling();
        }

        private void EnsureBattleLogModal()
        {
            if (_battleLogModal != null)
            {
                return;
            }

            var parent = runPanel != null ? runPanel.transform : transform;
            _battleLogModal = new GameObject("BattleLogModal", typeof(Image));
            _battleLogModal.transform.SetParent(parent, false);
            var modalRect = _battleLogModal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            _battleLogModal.GetComponent<Image>().color = new Color32(4, 3, 12, 196);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_battleLogModal.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(980f, 680f);
            panel.GetComponent<Image>().color = new Color32(22, 28, 48, 252);

            var title = CreateAnchoredText(panel.transform, "Title", "最近战斗记录", 34, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.89f), new Vector2(0.92f, 0.97f));
            title.color = new Color32(255, 226, 132, 255);

            var closeButton = new GameObject("CloseButton", typeof(Image), typeof(Button));
            closeButton.transform.SetParent(panel.transform, false);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);
            closeRect.sizeDelta = new Vector2(96f, 42f);
            var closeImage = closeButton.GetComponent<Image>();
            closeImage.color = new Color32(70, 82, 112, 245);
            var button = closeButton.GetComponent<Button>();
            button.targetGraphic = closeImage;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => _battleLogModal.SetActive(false));
            CreateChildText(closeButton.transform, "关闭", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);

            var scrollObject = new GameObject("Scroll", typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            var scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.06f, 0.08f);
            scrollRect.anchorMax = new Vector2(0.94f, 0.86f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color32(8, 12, 24, 210);

            var content = new GameObject("Content", typeof(Text), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.offsetMin = new Vector2(18f, 0f);
            contentRect.offsetMax = new Vector2(-18f, -16f);

            _battleLogContentLabel = content.GetComponent<Text>();
            _battleLogContentLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _battleLogContentLabel.fontSize = 50;
            _battleLogContentLabel.alignment = TextAnchor.UpperLeft;
            _battleLogContentLabel.color = new Color32(230, 236, 248, 255);
            _battleLogContentLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _battleLogContentLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _battleLogContentLabel.raycastTarget = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            _battleLogModal.SetActive(false);
        }

        private void HideLegacyBoardInfoLabels()
        {
            if (battlePreviewText != null && battlePreviewText.name.Contains("V2"))
            {
                battlePreviewText.gameObject.SetActive(false);
            }

            if (logLabel != null && logLabel.name.Contains("V2"))
            {
                logLabel.gameObject.SetActive(false);
            }
        }

        private void EnsureShopInitialized()
        {
            _flow.ShopSystem.InitializeShop(Run);
        }

        private void RefreshShopActionLabels()
        {
            var refreshCost = GetShopRefreshCost();
            var upgradeCost = Run == null ? 0 : _flow.ShopSystem.GetCurrentShopUpgradeCost(Run);
            var isShopMaxLevel = IsShopMaxLevel();
            var upgradeText = isShopMaxLevel ? "升级\n满级" : $"升级\n{upgradeCost}金";
            var upgradeTextV2 = isShopMaxLevel ? "升级 满级" : $"升级 {upgradeCost}金";
            var lockText = Run != null && Run.isShopLocked ? "解锁" : "锁定";
            var lockIcon = Run != null && Run.isShopLocked ? "钥匙" : "铁锁";
            SetButtonLabel("RefreshShopButton", $"刷新\n{refreshCost}金", "金币");
            SetButtonLabel("RefreshShopButtonV2", $"刷新 {refreshCost}金", "金币");
            SetButtonLabel("UpgradeShopButton", upgradeText, isShopMaxLevel ? null : "金币");
            SetButtonLabel("UpgradeShopButtonV2", upgradeTextV2, isShopMaxLevel ? null : "金币");
            SetButtonLabel("LockShopButton", lockText, lockIcon);
            SetButtonLabel("LockShopButtonV2", lockText, lockIcon);
        }

        private static int GetShopRefreshCost()
        {
            return 1;
        }

        private bool IsShopMaxLevel()
        {
            if (Run == null)
            {
                return false;
            }

            var costs = ProphecyGameSession.Instance.Data.Config?.shopUpgradeCost;
            var maxLevel = costs == null || costs.Length == 0 ? 6 : costs.Length;
            return Run.shopLevel >= maxLevel;
        }

        private void SetButtonLabel(string buttonName, string label, string iconName = null)
        {
            var button = FindDeepChild(GetUiSearchRoot(), buttonName);
            var labelTransform = button != null ? FindDeepChild(button, "Label") : null;
            var text = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                return;
            }

            text.text = label;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);
            var iconTransform = button != null ? FindDeepChild(button, "Icon") : null;
            ApplyPrefabIcon(iconTransform, iconName);
        }

        private static void ApplyPrefabIcon(Transform iconTransform, string iconName)
        {
            if (iconTransform == null)
            {
                return;
            }

            var iconObject = iconTransform.gameObject;
            iconObject.SetActive(!string.IsNullOrWhiteSpace(iconName));
            if (string.IsNullOrWhiteSpace(iconName))
            {
                return;
            }

            var iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                RuntimeFeatureIconCache.ApplyTo(iconImage, iconName);
                iconImage.color = Color.white;
                iconImage.raycastTarget = false;
            }
        }

        private void SetTextLabel(string labelName, string label)
        {
            var labelTransform = FindDeepChild(GetUiSearchRoot(), labelName);
            var text = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                return;
            }

            text.text = label;
        }

        private void UpdateRoundGoldIcon()
        {
            var roundTransform = FindDeepChild(GetUiSearchRoot(), "RoundLabelV2");
            var roundText = roundTransform != null ? roundTransform.GetComponent<Text>() : roundLabel;
            var roundRect = roundText != null ? roundText.GetComponent<RectTransform>() : null;
            if (roundRect == null)
            {
                return;
            }

            var icon = FindDeepChild(roundRect, "RoundGoldIcon");
            ApplyPrefabIcon(icon, "金币");
        }

        private void RefreshShopMetaStars()
        {
            var starRoot = FindDeepChild(GetUiSearchRoot(), "ShopMetaStarV2");
            if (starRoot == null || starRoot.childCount == 0)
            {
                return;
            }

            var level = Mathf.Clamp(Run != null ? Run.shopLevel : 1, 1, 6);
            var template = starRoot.GetChild(0) as RectTransform;
            if (template == null)
            {
                return;
            }

            while (starRoot.childCount < level)
            {
                var clone = Instantiate(template.gameObject, starRoot);
                clone.name = $"star_{starRoot.childCount}";
            }

            var starSize = template.sizeDelta;
            var starWidth = starSize.x > 0f ? starSize.x : template.rect.width;
            var spacing = Mathf.Max(4f, starWidth * 0.12f);
            var step = starWidth + spacing;
            var startX = -step * (level - 1) * 0.5f;
            var y = template.anchoredPosition.y;

            for (var i = 0; i < starRoot.childCount; i += 1)
            {
                var child = starRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var visible = i < level;
                child.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.sizeDelta = starSize;
                child.anchoredPosition = new Vector2(startX + step * i, y);
            }
        }

        private Transform GetUiSearchRoot()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return canvas.transform;
            }

            return transform.root != null ? transform.root : transform;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var match = FindDeepChild(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
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

            while (_recentLogs.Count > 4)
            {
                _recentLogs.RemoveAt(_recentLogs.Count - 1);
            }

            logLabel.text = "日志：\n" + string.Join("\n", _recentLogs);
        }

        private void RefreshCardLists()
        {
            RuntimeUnitTooltip.HideCurrent();
            RebuildUnitCardList(shopCardRoot, Run.shopCards, (card, _) => FormatUnitCardLabel(card), null, BuyShopCard, null, null);
            RebuildUnitCardList(handCardRoot, Run.handCards, (card, index) => card == null ? string.Empty : FormatUnitCardLabel(card, _selectedHandIndex == index ? ">" : null), null, null, null, null, "hand");
            RebuildBoardSlotGrid();
        }

        private void RebuildShopAsSellArea()
        {
            RuntimeUnitTooltip.HideCurrent();
            if (shopCardRoot == null)
            {
                return;
            }

            for (var i = shopCardRoot.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(shopCardRoot.GetChild(i).gameObject);
            }

            var grid = shopCardRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.enabled = false;
            }

            var sellObject = new GameObject("SellDropArea", typeof(Image), typeof(RuntimeSellDropTarget), typeof(LayoutElement));
            sellObject.transform.SetParent(shopCardRoot, false);
            var rect = sellObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var layout = sellObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 735f;
            layout.preferredHeight = 589f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;

            var image = sellObject.GetComponent<Image>();
            image.color = new Color32(34, 24, 58, 245);
            var dropTarget = sellObject.GetComponent<RuntimeSellDropTarget>();
            dropTarget.Controller = this;

            var label = CreateChildText(sellObject.transform, "拖动到此处出售单位\n售价 1 金币", 46, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            label.color = Color.white;
        }

        private string FormatUnitCardLabel(UnitCardState card, string prefix = null)
        {
            if (card == null)
            {
                return "已售出";
            }

            if (ManageEventResolver.IsForestGemCard(card))
            {
                var gemTitle = string.IsNullOrWhiteSpace(prefix) ? ManageEventResolver.ForestGemCardName : $"{prefix}  {ManageEventResolver.ForestGemCardName}";
                return $"{gemTitle}\n使用：阵上单位获得数量 +{ManageEventResolver.ForestGemReinforceCount}\n计为被赐予1颗";
            }

            var unit = ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            var title = string.IsNullOrWhiteSpace(prefix) ? card.name : $"{prefix}  {card.name}";
            var goldSuffix = card.isGolden ? " 金色" : string.Empty;
            if (unit == null)
            {
                return $"{title}  {card.star}*{goldSuffix}";
            }

            var tags = $"{unit.race}  {unit.typeLabel}  {unit.faith}";
            var attack = unit.attack + card.shopBuffAttack + card.roundTempAttack + card.boardAuraAttack;
            var count = Mathf.Max(1, (card.baseCount > 0 ? card.baseCount : unit.defaultCount > 0 ? unit.defaultCount : unit.startCount > 0 ? unit.startCount : unit.baseCount > 0 ? unit.baseCount : 1) + card.roundTempCount);
            var stars = new string('*', Mathf.Clamp(unit.star, 1, 6));
            return $"{stars}\n{title}{goldSuffix}\n数{count}  攻{attack}\n{tags}";
        }

        private UnitCardRaceStyleLibrary GetUnitCardRaceStyles()
        {
            if (unitCardRaceStyles == null)
            {
                unitCardRaceStyles = UnitCardRaceStyleLibrary.LoadDefault();
            }

            return unitCardRaceStyles;
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
            if (isGridRoot)
            {
                root.GetComponent<GridLayoutGroup>().enabled = true;
            }

            var isShopGrid = isGridRoot && root.name.Contains("Shop");
            var displayCount = cards.Count;
            if (isGridRoot)
            {
                displayCount = Mathf.Max(isShopGrid ? 6 : 9, displayCount);
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
            var isShopCard = string.IsNullOrWhiteSpace(dragSource);
            var isGridCard = root != null && root.GetComponent<GridLayoutGroup>() != null;
            var mode = isShopCard || isGridCard ? UnitCardPresentationMode.Grid : UnitCardPresentationMode.List;
            var prefabMode = dragSource == "hand" ? UnitCardPresentationMode.List : mode;
            var view = UnitCardView.Instantiate(root, prefabMode);
            var cardObject = view.gameObject;
            if (cardObject.GetComponent<Button>() == null)
            {
                cardObject.AddComponent<Button>();
            }
            if (cardObject.GetComponent<LayoutElement>() == null)
            {
                cardObject.AddComponent<LayoutElement>();
            }
            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            var gridLayout = root != null ? root.GetComponent<GridLayoutGroup>() : null;
            var gridCell = gridLayout != null ? gridLayout.cellSize : Vector2.zero;
            var cardWidth = isGridCard ? gridCell.x : isShopCard ? 124f : 0f;
            var cardHeight = isGridCard ? gridCell.y : isShopCard ? 148f : 82f;
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            var layoutElement = cardObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = cardWidth > 0f ? cardWidth : -1f;
            layoutElement.preferredHeight = cardHeight;
            layoutElement.flexibleWidth = cardWidth > 0f ? 0f : 1f;

            var unitDefinition = card == null ? null : ProphecyGameSession.Instance.Data.FindUnit(card.unitId);
            var selected = dragSource == "hand" && _selectedHandIndex == index;
            var prefix = selected ? ">" : null;
            view.Bind(
                unitDefinition,
                card,
                mode,
                GetUnitCardRaceStyles(),
                prefix,
                selected);
            if (dragSource == "board")
            {
                ApplyBoardCountBadge(view, unitDefinition, card);
            }

            var background = view.BackgroundImage != null ? view.BackgroundImage : cardObject.GetComponent<Image>();
            var cardButton = cardObject.GetComponent<Button>();
            cardButton.targetGraphic = background;
            cardButton.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            if (card != null)
            {
                var tooltip = cardObject.AddComponent<RuntimeUnitTooltip>();
                tooltip.Unit = card;
            }

            if (dragSource == "hand" && card != null)
            {
                cardButton.onClick.AddListener(() => SelectHandCard(index));
            }
            else if (isShopCard && card != null && primaryAction != null)
            {
                cardButton.onClick.AddListener(() => primaryAction(index));
            }
            else if (dragSource == "board" && !string.IsNullOrWhiteSpace(boardSlotId))
            {
                cardButton.onClick.AddListener(() => HandleBoardSlotClicked(boardSlotId));
            }

            if (card != null && !string.IsNullOrWhiteSpace(dragSource))
            {
                var dragItem = cardObject.AddComponent<RuntimeUnitDragItem>();
                dragItem.Controller = this;
                dragItem.Source = dragSource;
                dragItem.HandIndex = dragSource == "hand" ? index : -1;
                dragItem.BoardSlotId = boardSlotId;
            }

            if (!isShopCard && card != null && !string.IsNullOrWhiteSpace(primaryLabel) && primaryAction != null)
            {
                CreateCardActionButton(cardObject.transform, primaryLabel, isShopCard || isGridCard ? new Vector2(isGridCard ? -46f : 0f, 22f) : new Vector2(-116f, 16f), () => primaryAction(index), isShopCard || isGridCard);
            }

            if (!isShopCard && card != null && !string.IsNullOrWhiteSpace(secondaryLabel) && secondaryAction != null)
            {
                CreateCardActionButton(cardObject.transform, secondaryLabel, isGridCard ? new Vector2(46f, 22f) : new Vector2(-52f, 16f), () => secondaryAction(index), isShopCard || isGridCard);
            }
        }

        private void ApplyBoardCountBadge(UnitCardView view, UnitDefinition definition, UnitCardState card)
        {
            if (view == null || definition == null || card == null || Run?.boardUnits == null)
            {
                return;
            }

            var text = FormatBoardCountBadge(definition, card, out var achieved);
            if (view.GemLabel != null && !string.IsNullOrWhiteSpace(text))
            {
                view.GemLabel.text = text;
                view.GemLabel.color = achieved ? new Color32(90, 238, 132, 255) : Color.white;
            }

            ApplyBoardSkillProgress(view.transform, definition, card);
        }

        private void ApplyBoardSkillProgress(Transform parent, UnitDefinition definition, UnitCardState card)
        {
            var existing = parent.Find("BoardSkillProgress");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            if (!TryGetBoardSkillProgress(definition, card, out var progress))
            {
                return;
            }

            var root = new GameObject("BoardSkillProgress", typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.14f, 0f);
            rect.anchorMax = new Vector2(0.86f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 31f);
            rect.sizeDelta = new Vector2(0f, 12f);

            var background = root.GetComponent<Image>();
            background.color = new Color32(12, 18, 26, 205);
            background.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(root.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress.Current / (float)Mathf.Max(1, progress.Threshold)), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillObject.GetComponent<Image>();
            fill.color = progress.Triggered ? new Color32(82, 218, 118, 210) : new Color32(236, 164, 56, 210);
            fill.raycastTarget = false;

            var label = CreateChildText(root.transform, FormatBoardSkillProgressLabel(progress), 9, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            label.color = Color.white;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 7;
            label.resizeTextMaxSize = 9;
            root.transform.SetAsLastSibling();
        }

        private static string FormatBoardSkillProgressLabel(BoardSkillProgress progress)
        {
            return progress.Triggered
                ? $"{progress.Label} 已触发"
                : $"{progress.Label} {Mathf.Clamp(progress.Current, 0, progress.Threshold)}/{progress.Threshold}";
        }

        private bool TryGetBoardSkillProgress(UnitDefinition definition, UnitCardState card, out BoardSkillProgress progress)
        {
            progress = new BoardSkillProgress();
            foreach (var skill in GetActiveBoardCountSkills(definition, card))
            {
                if (skill == null || skill.threshold <= 0)
                {
                    continue;
                }

                var threshold = Mathf.Max(1, skill.threshold);
                switch (skill.kind)
                {
                    case "while_on_board_attack_gain_threshold_add_random_unit_to_hand":
                        progress = new BoardSkillProgress("累计", card.manageRoundAttackRewardTriggered ? threshold : card.manageAttackGainBucket, threshold, card.manageRoundAttackRewardTriggered);
                        return true;
                    case "while_on_board_attack_gain_threshold_evolve":
                        progress = new BoardSkillProgress("进阶", card.manageAttackGainBucket, threshold, false);
                        return true;
                    case "while_on_board_every_n_entry_race_add_random_unit_to_hand":
                        progress = new BoardSkillProgress(BoardCountLabel(string.IsNullOrWhiteSpace(skill.race) ? definition.race : skill.race), card.manageEntryEffectTriggerCount, threshold, false);
                        return true;
                    case "on_any_entry_effect_count_evolve":
                        progress = new BoardSkillProgress("入场", card.manageEntryEffectTriggerCount, threshold, false);
                        return true;
                    case "on_gift_action_team_gain_attack_every_n":
                        var giftActions = Run?.manageResources == null ? 0 : Mathf.Max(0, Run.manageResources.forestGiftActions);
                        var giftCurrent = giftActions - Mathf.Max(0, card.manageGiftActionBucket) * threshold;
                        progress = new BoardSkillProgress("赐宝", giftCurrent, threshold, false);
                        return true;
                    case "on_receive_gift_total_team_gain_power_every_n":
                        var receiveCurrent = card.forestGemsReceived - Mathf.Max(0, card.manageReceiveGiftPowerBucket) * threshold;
                        progress = new BoardSkillProgress("受赐", receiveCurrent, threshold, false);
                        return true;
                    case "on_receive_gift_self_evolve":
                        progress = new BoardSkillProgress("进阶", card.forestGemsReceived, threshold, card.forestGemsReceived >= threshold);
                        return true;
                    case "on_receive_gift_total_discover_race_unit_once":
                        progress = new BoardSkillProgress("发现", card.forestGemsReceived, threshold, card.manageReceiveGiftDiscoverTriggered);
                        return true;
                    case "on_kill_count_next_round_evolve":
                        var key = $"{skill.kind}:{skill.targetUnitId}";
                        var killCount = card.battleProgressCounters?.FirstOrDefault(counter => counter != null && counter.key == key)?.value ?? 0;
                        progress = new BoardSkillProgress("击杀", killCount, threshold, false);
                        return true;
                }
            }

            return false;
        }

        private readonly struct BoardSkillProgress
        {
            public BoardSkillProgress(string label, int current, int threshold, bool triggered)
            {
                Label = label;
                Current = Mathf.Max(0, current);
                Threshold = Mathf.Max(1, threshold);
                Triggered = triggered;
            }

            public string Label { get; }
            public int Current { get; }
            public int Threshold { get; }
            public bool Triggered { get; }
        }

        private string FormatBoardCountBadge(UnitDefinition definition, UnitCardState card, out bool achieved)
        {
            achieved = false;
            var skill = GetBoardCountBadgeSkill(definition, card);
            if (skill == null)
            {
                return string.Empty;
            }

            var count = 0;
            var label = string.Empty;
            var threshold = Mathf.Max(0, skill.threshold);

            switch (skill.kind)
            {
                case "battle_start_if_team_faith_count_next_round_discover":
                case "battle_start_self_attack_per_faith_count":
                case "battle_start_team_attack_per_faith_count":
                case "battle_start_self_stats_per_faith_count":
                case "round_end_self_gain_attack_per_faith_count":
                    var faith = string.IsNullOrWhiteSpace(skill.faith) ? definition.faith : skill.faith;
                    count = CountBoardFaith(faith);
                    label = BoardCountLabel(faith);
                    break;
                case "round_start_if_race_count_temp_power":
                case "while_on_board_race_threshold_team_speed":
                case "round_end_if_race_count_self_gain_attack":
                case "round_end_self_temp_morale_per_race_count":
                    var race = string.IsNullOrWhiteSpace(skill.race) ? definition.race : skill.race;
                    count = CountBoardRace(race);
                    label = BoardCountLabel(race);
                    break;
                default:
                    return string.Empty;
            }

            if (threshold > 0)
            {
                achieved = count >= threshold;
                return $"{label} {count}/{threshold}";
            }

            return $"{label} {count}";
        }

        private static SkillDefinition GetBoardCountBadgeSkill(UnitDefinition definition, UnitCardState card)
        {
            foreach (var skill in GetActiveBoardCountSkills(definition, card))
            {
                switch (skill.kind)
                {
                    case "battle_start_if_team_faith_count_next_round_discover":
                    case "battle_start_self_attack_per_faith_count":
                    case "battle_start_team_attack_per_faith_count":
                    case "battle_start_self_stats_per_faith_count":
                    case "round_start_if_race_count_temp_power":
                    case "while_on_board_race_threshold_team_speed":
                    case "round_end_self_gain_attack_per_faith_count":
                    case "round_end_if_race_count_self_gain_attack":
                    case "round_end_self_temp_morale_per_race_count":
                        return skill;
                }
            }

            return null;
        }

        private static IEnumerable<SkillDefinition> GetActiveBoardCountSkills(UnitDefinition definition, UnitCardState card)
        {
            if (definition == null)
            {
                yield break;
            }

            var talents = card != null && card.isGolden ? definition.goldTalents : definition.talents;
            foreach (var skill in talents ?? new SkillDefinition[0])
            {
                if (skill != null)
                {
                    yield return skill;
                }
            }

            var battleSkills = card != null && card.isGolden ? definition.goldBattleSkills : definition.battleSkills;
            foreach (var skill in battleSkills ?? new SkillDefinition[0])
            {
                if (skill != null)
                {
                    yield return skill;
                }
            }
        }

        private int CountBoardFaith(string faith)
        {
            if (Run?.boardUnits == null || string.IsNullOrWhiteSpace(faith))
            {
                return 0;
            }

            return Run.boardUnits.Count(unit => ProphecyGameSession.Instance.Data.FindUnit(unit.unitId)?.faith == faith);
        }

        private int CountBoardRace(string race)
        {
            if (Run?.boardUnits == null || string.IsNullOrWhiteSpace(race))
            {
                return 0;
            }

            return Run.boardUnits.Count(unit => CountsAsBoardRace(unit, race));
        }

        private bool CountsAsBoardRace(UnitCardState unit, string race)
        {
            var definition = unit == null ? null : ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
            return definition?.race == race || SameRowCountsAsRace(unit, race);
        }

        private bool SameRowCountsAsRace(UnitCardState unit, string race)
        {
            if (!(unit is BoardUnitState boardUnit) || string.IsNullOrWhiteSpace(race) || !TryParseBoardSlot(boardUnit.boardSlotId, out var row, out _))
            {
                return false;
            }

            return Run?.boardUnits?.Any(owner =>
            {
                if (owner == null || !TryParseBoardSlot(owner.boardSlotId, out var ownerRow, out _) || ownerRow != row)
                {
                    return false;
                }

                var ownerDefinition = ProphecyGameSession.Instance.Data.FindUnit(owner.unitId);
                var skills = owner.isGolden ? ownerDefinition?.goldTalents : ownerDefinition?.talents;
                return (skills ?? new SkillDefinition[0]).Any(skill => skill != null && skill.kind == "same_row_units_count_as_race" && (string.IsNullOrWhiteSpace(skill.race) || skill.race == race));
            }) == true;
        }

        private static bool TryParseBoardSlot(string slotId, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            var parts = slotId.Split('-');
            return parts.Length == 2 && int.TryParse(parts[0], out row) && int.TryParse(parts[1], out column);
        }

        private static string BoardCountLabel(string value)
        {
            switch (value)
            {
                case "莱特":
                    return "莱";
                case "甘地":
                    return "甘";
                case "甘德":
                    return "德";
                default:
                    return string.IsNullOrWhiteSpace(value) ? "数" : value.Substring(0, 1);
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

            if (boardCardRoot.name.Contains("V2"))
            {
                const float slotSize = 146f;
                var pixelSlots = new[]
                {
                    new BoardSlotPixel("4-1", 6f, 14f),
                    new BoardSlotPixel("4-2", 6f, 188f),
                    new BoardSlotPixel("4-3", 6f, 362f),
                    new BoardSlotPixel("4-4", 6f, 536f),
                    new BoardSlotPixel("3-1", 256f, 88f),
                    new BoardSlotPixel("3-2", 256f, 262f),
                    new BoardSlotPixel("3-3", 256f, 436f),
                    new BoardSlotPixel("2-1", 506f, 188f),
                    new BoardSlotPixel("2-2", 506f, 362f),
                    new BoardSlotPixel("1-1", 756f, 276f)
                };

                foreach (var slot in pixelSlots)
                {
                    var cell = CreateBoardSlotCell(boardCardRoot, slot.Id);
                    SetLocalTopLeft(cell.GetComponent<RectTransform>(), slot.Left, slot.Top, slotSize, slotSize);
                }

                return;
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
                columnLayout.spacing = 18f;
                columnLayout.childControlWidth = false;
                columnLayout.childControlHeight = false;
                columnLayout.childForceExpandWidth = false;
                columnLayout.childForceExpandHeight = false;
                columnLayout.childAlignment = TextAnchor.MiddleCenter;
                var columnElement = columnObject.GetComponent<LayoutElement>();
                columnElement.preferredWidth = 146f;
                columnElement.flexibleWidth = 0f;

                foreach (var slotId in column)
                {
                    CreateBoardSlotCell(columnObject.transform, slotId);
                }
            }
        }

        private GameObject CreateBoardSlotCell(Transform parent, string slotId)
        {
            var unit = Run.boardUnits.FirstOrDefault(item => item.boardSlotId == slotId);
            var isSelected = _selectedBoardSlotId == slotId;
            var cellObject = new GameObject("BoardSlot_" + slotId, typeof(Image), typeof(Button), typeof(LayoutElement), typeof(RuntimeBoardSlotDropTarget));
            cellObject.transform.SetParent(parent, false);
            var layout = cellObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 146f;
            layout.preferredHeight = 146f;
            layout.flexibleWidth = 0f;

            var image = cellObject.GetComponent<Image>();
            image.color = isSelected
                ? new Color32(76, 92, 68, 255)
                : unit == null
                    ? new Color32(54, 38, 40, 210)
                    : new Color32(60, 42, 43, 255);

            var button = cellObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
            button.onClick.AddListener(() => HandleBoardSlotClicked(slotId));

            var dropTarget = cellObject.GetComponent<RuntimeBoardSlotDropTarget>();
            dropTarget.Controller = this;
            dropTarget.BoardSlotId = slotId;

            if (unit != null)
            {
                var tooltip = cellObject.AddComponent<RuntimeUnitTooltip>();
                tooltip.Unit = unit;

                var view = UnitCardView.Instantiate(cellObject.transform, UnitCardPresentationMode.Board);
                var viewRect = view.GetComponent<RectTransform>();
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                viewRect.offsetMin = Vector2.zero;
                viewRect.offsetMax = Vector2.zero;
                var definition = ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
                view.Bind(definition, unit, UnitCardPresentationMode.Board, GetUnitCardRaceStyles(), null, isSelected);
                ApplyBoardCountBadge(view, definition, unit);

                var dragItem = cellObject.AddComponent<RuntimeUnitDragItem>();
                dragItem.Controller = this;
                dragItem.Source = "board";
                dragItem.BoardSlotId = slotId;
            }

            var unitDefinition = unit == null ? null : ProphecyGameSession.Instance.Data.FindUnit(unit.unitId);
            if (unit == null || unitDefinition == null)
            {
                var text = CreateChildText(cellObject.transform, $"{slotId}\n空位", 24, TextAnchor.MiddleCenter, new Vector2(4f, 8f), new Vector2(-4f, -26f));
                text.color = Color.white;
                text.text = $"{slotId}\n空位";
            }

            if (unit != null && !string.IsNullOrWhiteSpace(_pendingTargetedEntrySourceSlotId))
            {
                CreateSmallBoardActionButton(cellObject.transform, "祝福", () => ResolvePendingTargetedEntryOnSlot(slotId));
            }
            else if (unit == null && _selectedHandIndex >= 0)
            {
                CreateSmallBoardActionButton(cellObject.transform, "部署", () => DeployHandCardToSlot(_selectedHandIndex, slotId));
            }
            else if (!string.IsNullOrWhiteSpace(_selectedBoardSlotId))
            {
                CreateSmallBoardActionButton(cellObject.transform, "移动", () => MoveBoardUnitToSlot(_selectedBoardSlotId, slotId));
            }

            return cellObject;
        }

        private static void SetLocalTopLeft(RectTransform rect, float left, float top, float width, float height)
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

        private static void CreateSmallBoardActionButton(Transform parent, string label, UnityEngine.Events.UnityAction callback)
        {
            var buttonObject = new GameObject(label + "Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(64f, 22f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(72, 104, 132, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
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
            button.onClick.AddListener(RuntimeSfxPlayer.PlayClick);
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
                ? "奖励：无"
                : $"奖励：金币{rewards.nextRoundGold} 商店攻{rewards.nextRoundShopBuffAttack} 发现{rewards.discoverFaithRewards?.Count ?? 0}";
            var lastEntries = Run.battleHistory == null
                ? Enumerable.Empty<string>()
                : Run.battleHistory
                    .OrderByDescending(item => item.round)
                    .Take(1)
                    .Select(item => $"R{item.round} {(item.victory ? "胜" : "败")}  {item.playerScore}:{item.enemyScore}  {item.hpDelta}");
            var history = string.Join("\n", lastEntries);
            if (string.IsNullOrWhiteSpace(history))
            {
                history = "无";
            }

            var realtimeLine = useRealtimeBattlePreview ? "实时预览：开" : "实时预览：关";
            return $"战斗预览\n进度 {Run.round}/{limit}  胜{Run.campaignWins}/败{Run.campaignLosses}  {realtimeLine}\n战力 {playerScore} : {enemyScore}\n{rewardLine}\n最近：{history}";
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

        private static string RepeatStar(string value, int count)
        {
            return string.Concat(Enumerable.Repeat(value, Mathf.Max(0, count)));
        }
    }

}
