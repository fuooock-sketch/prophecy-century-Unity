#if UNITY_EDITOR
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace ProphecyCentury.UI
{
    public sealed class RuntimePlaytestTools : MonoBehaviour
    {
        private readonly RunFlowController _flow = new RunFlowController();
        private RunSceneController _controller;
        private GameObject _toolbar;
        private const int PlaytestMaxLuck = 16;
        private const int PlaytestMaxMorale = 24;

        private static readonly string[] ShopIds =
        {
            "small_merchant", "bright_warrior", "elf",
            "fire_elemental", "forest_guard", "ger_beast"
        };

        private static readonly string[] HandIds =
        {
            "blacksmith", "monk", "knight",
            "assassin", "priest", "wanderer",
            "water_elemental", "forest_scout", "caller"
        };

        private static readonly string[] BoardIds =
        {
            "stubborn_apprentice", "academy_gardener", "martial_master",
            "wind_elemental", "water_elemental"
        };

        private static readonly string[] BoardSlots =
        {
            "4-1", "3-1", "2-1", "1-1", "4-3"
        };

        private void Awake()
        {
            _controller = FindObjectOfType<RunSceneController>();
            EnsureToolbar();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                SeedUiPlaytestState();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                ResolveOneBattle();
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                RefreshView();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                ToggleRealtimeBattlePreview();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                TestSmallMerchantSellCount();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                AddGold();
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                MaximizeLuck();
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                MaximizeMorale();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                ResetPlaytestStats();
            }
        }

        [ContextMenu("Seed UI Playtest State")]
        public void SeedUiPlaytestState()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot seed playtest state: session missing.");
                return;
            }

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            var run = session.CurrentRun;
            run.state = "manage";
            run.round = Mathf.Max(3, run.round);
            run.gold = Mathf.Max(30, run.gold);
            run.playerHp = Mathf.Clamp(run.playerHp <= 0 ? 100 : run.playerHp, 1, 100);
            run.shopLevel = 6;
            run.shopUpgradeAnchorRound = run.round;
            run.isShopLocked = false;
            run.shopCards.Clear();
            run.handCards.Clear();
            run.boardUnits.Clear();

            _flow.ShopSystem.InitializeShop(run);
            foreach (var id in ShopIds)
            {
                run.shopCards.Add(CreateCard(id));
            }

            foreach (var id in HandIds)
            {
                run.handCards.Add(CreateCard(id));
            }

            for (var i = 0; i < BoardIds.Length && i < BoardSlots.Length; i += 1)
            {
                var card = CreateCard(BoardIds[i]);
                run.boardUnits.Add(CloneToBoard(card, BoardSlots[i]));
            }

            run.lastBattleSummary = "Playtest seed ready";
            Debug.Log("[ProphecyCentury] Seeded UI playtest state. F10 starts visual battle playback, F8 refreshes the view.");
            RefreshView();
        }

        [ContextMenu("Resolve One Battle")]
        public void ResolveOneBattle()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null || !session.HasCurrentRun)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot resolve playtest battle: run missing.");
                return;
            }

            var run = session.CurrentRun;
            if (run.boardUnits.Count == 0)
            {
                SeedUiPlaytestState();
            }

            if (_controller == null)
            {
                _controller = FindObjectOfType<RunSceneController>();
            }

            if (_controller != null)
            {
                _controller.StartBattle();
            }
        }

        [ContextMenu("Refresh View")]
        public void RefreshView()
        {
            if (_controller == null)
            {
                _controller = FindObjectOfType<RunSceneController>();
            }

            _controller?.ShowRun();
            _controller?.RefreshView();
        }

        [ContextMenu("Toggle Realtime Battle Preview")]
        public void ToggleRealtimeBattlePreview()
        {
            if (_controller == null)
            {
                _controller = FindObjectOfType<RunSceneController>();
            }

            _controller?.ToggleRealtimeBattlePreview();
        }

        [ContextMenu("Add 10 Gold")]
        public void AddGold()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot add gold: session missing.");
                return;
            }

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            session.CurrentRun.gold += 10;
            Debug.Log($"[ProphecyCentury] GM added 10 gold. Gold={session.CurrentRun.gold}");
            RefreshView();
        }

        [ContextMenu("Maximize Luck")]
        public void MaximizeLuck()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot maximize luck: session missing.");
                return;
            }

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            if (session.Data?.Config != null)
            {
                session.Data.Config.critRateCap = Mathf.Max(session.Data.Config.critRateCap, 0.95f);
            }

            var run = session.CurrentRun;
            var changed = 0;
            var definitionChanged = 0;

            ForEachUnitDefinition(session, definition => definitionChanged += MaximizeDefinitionLuck(definition) ? 1 : 0);
            ForEachRunCard(run, card => changed += MaximizeCardLuck(card) ? 1 : 0);

            var critCap = session.Data?.Config?.critRateCap ?? 0f;
            Debug.Log($"[ProphecyCentury] GM maximized luck for {changed} cards and {definitionChanged} unit definitions. Luck>={PlaytestMaxLuck}, CritCap={critCap:0.##}.");
            RefreshView();
        }

        [ContextMenu("Maximize Morale")]
        public void MaximizeMorale()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot maximize morale: session missing.");
                return;
            }

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            var run = session.CurrentRun;
            var changed = 0;
            var definitionChanged = 0;

            ForEachUnitDefinition(session, definition => definitionChanged += MaximizeDefinitionMorale(definition) ? 1 : 0);
            ForEachRunCard(run, card => changed += MaximizeCardMorale(card) ? 1 : 0);

            Debug.Log($"[ProphecyCentury] GM maximized morale for {changed} cards and {definitionChanged} unit definitions. Morale>={PlaytestMaxMorale}.");
            RefreshView();
        }

        [ContextMenu("Reset Playtest Stats")]
        public void ResetPlaytestStats()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot reset playtest stats: session missing.");
                return;
            }

            session.Data?.LoadAll();

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            var changed = 0;
            ForEachRunCard(session.CurrentRun, card => changed += ResetCardLuckAndMorale(card) ? 1 : 0);

            Debug.Log($"[ProphecyCentury] GM reset playtest luck and morale. Cleared {changed} cards and reloaded unit definitions/config.");
            RefreshView();
        }

        [ContextMenu("Test Small Merchant Sell Count")]
        public void TestSmallMerchantSellCount()
        {
            var session = ProphecyGameSession.Instance;
            if (session == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot test small merchant sell count: session missing.");
                return;
            }

            if (!session.HasCurrentRun)
            {
                session.StartNewRun();
            }

            var merchant = session.Data.FindUnit("small_merchant");
            if (merchant == null)
            {
                Debug.LogWarning("[ProphecyCentury] Cannot test small merchant sell count: unit small_merchant missing.");
                return;
            }

            var originalRun = session.CurrentRun;
            var flow = new RunFlowController();
            var startCount = ResolveDefinitionStartCount(merchant);
            var testRun = CreateSmallMerchantSellTestRun(merchant);
            var successFirst = false;
            var successSecond = false;
            var finalCount = 0;
            var finalBucket = -1;

            try
            {
                session.RestoreRun(testRun);
                successFirst = flow.SellHandUnit(0);
                successSecond = flow.SellHandUnit(0);
                var merchantState = session.CurrentRun.boardUnits.FirstOrDefault(unit => unit.unitId == "small_merchant");
                finalCount = merchantState?.baseCount ?? 0;
                finalBucket = merchantState?.manageSellCountBucket ?? -1;
            }
            finally
            {
                session.RestoreRun(originalRun);
            }

            var expected = startCount + 2;
            Debug.Log(
                $"[ProphecyCentury] Small merchant sell-count test: sells=2, " +
                $"success_first={successFirst}, success_second={successSecond}, " +
                $"count={finalCount}, expected={expected}, bucket={finalBucket}, " +
                $"passed={successFirst && successSecond && finalCount == expected && finalBucket == 0}.");
        }

        private void EnsureToolbar()
        {
            if (_toolbar != null)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var existing = canvas.transform.Find("GMToolbar");
            if (existing != null)
            {
                _toolbar = existing.gameObject;
            }
            else
            {
                _toolbar = new GameObject("GMToolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                _toolbar.transform.SetParent(canvas.transform, false);
            }

            for (var i = _toolbar.transform.childCount - 1; i >= 0; i -= 1)
            {
                Destroy(_toolbar.transform.GetChild(i).gameObject);
            }

            var rect = _toolbar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(14f, -14f);
            rect.sizeDelta = new Vector2(1144f, 40f);

            var background = _toolbar.GetComponent<Image>();
            background.color = new Color32(8, 8, 16, 190);
            background.raycastTarget = true;

            var layout = _toolbar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 5, 5);
            layout.spacing = 6f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            CreateToolbarButton("GM\u79cd\u5b50 F9", SeedUiPlaytestState);
            CreateToolbarButton("GM\u6218\u6597 F10", ResolveOneBattle);
            CreateToolbarButton("\u5237\u65b0 F8", RefreshView);
            CreateToolbarButton("\u5b9e\u65f6 F7", ToggleRealtimeBattlePreview);
            CreateToolbarButton("出售测试 F6", TestSmallMerchantSellCount);
            CreateToolbarButton("\u91d1\u5e01 +10 G", AddGold);
            CreateToolbarButton("\u5e78\u8fd0 B", MaximizeLuck);
            CreateToolbarButton("\u58eb\u6c14 N", MaximizeMorale);
            CreateToolbarButton("\u91cd\u7f6e M", ResetPlaytestStats);
            _toolbar.transform.SetAsLastSibling();
        }

        private void CreateToolbarButton(string label, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(_toolbar.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(118f, 30f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(72, 112, 160, 240);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 118f;
            layout.preferredHeight = 30f;

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static UnitCardState CreateCard(string unitId)
        {
            var definition = FindUnit(unitId);
            return new UnitCardState
            {
                unitId = definition?.id ?? unitId,
                name = definition?.name ?? unitId,
                star = definition?.star ?? 1,
                shopPoolCost = 0,
                shopPoolReserved = false,
                shopPoolContribution = 0,
                fromShopPurchase = false
            };
        }

        private static RunState CreateSmallMerchantSellTestRun(UnitDefinition definition)
        {
            var startCount = ResolveDefinitionStartCount(definition);
            return new RunState
            {
                campaignId = "south_town_adventure",
                heroId = "playtest",
                state = "manage",
                gold = 0,
                round = 1,
                playerHp = 9999,
                shopLevel = 1,
                shopUpgradeAnchorRound = 1,
                campaignRoundLimit = 9999,
                pendingBattleRewards = new BattleRewardState(),
                handCards =
                {
                    CreateCard("bright_warrior"),
                    CreateCard("elf")
                },
                boardUnits =
                {
                    new BoardUnitState
                    {
                        unitId = definition.id,
                        name = definition.name,
                        star = definition.star,
                        boardSlotId = "4-1",
                        baseCount = startCount
                    }
                }
            };
        }

        private static int ResolveDefinitionStartCount(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Mathf.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
        }

        private static BoardUnitState CloneToBoard(UnitCardState card, string slotId)
        {
            return new BoardUnitState
            {
                unitId = card.unitId,
                name = card.name,
                star = card.star,
                isGolden = card.isGolden,
                shopBuffHp = card.shopBuffHp,
                shopBuffAttack = card.shopBuffAttack,
                shopBuffDefense = card.shopBuffDefense,
                shopBuffPower = card.shopBuffPower,
                shopBuffSpeed = card.shopBuffSpeed,
                shopBuffLuck = card.shopBuffLuck,
                shopBuffMorale = card.shopBuffMorale,
                boardAuraAttack = card.boardAuraAttack,
                baseCount = card.baseCount,
                maxCount = card.maxCount,
                manageCountGainEventProgress = card.manageCountGainEventProgress,
                manageSellCountBucket = card.manageSellCountBucket,
                boardSlotId = slotId
            };
        }

        private static void ForEachUnitDefinition(ProphecyGameSession session, System.Action<UnitDefinition> action)
        {
            if (session?.Data?.Units == null || action == null)
            {
                return;
            }

            foreach (var definition in session.Data.Units)
            {
                if (definition != null)
                {
                    action(definition);
                }
            }
        }

        private static void ForEachRunCard(RunState run, System.Action<UnitCardState> action)
        {
            if (run == null || action == null)
            {
                return;
            }

            foreach (var unit in run.boardUnits)
            {
                action(unit);
            }

            foreach (var card in run.handCards)
            {
                action(card);
            }

            foreach (var card in run.shopCards)
            {
                action(card);
            }
        }

        private static bool MaximizeDefinitionLuck(UnitDefinition definition)
        {
            if (definition == null || definition.luck >= PlaytestMaxLuck)
            {
                return false;
            }

            definition.luck = PlaytestMaxLuck;
            return true;
        }

        private static bool MaximizeDefinitionMorale(UnitDefinition definition)
        {
            if (definition == null || definition.morale >= PlaytestMaxMorale)
            {
                return false;
            }

            definition.morale = PlaytestMaxMorale;
            return true;
        }

        private static bool MaximizeCardLuck(UnitCardState card)
        {
            if (card == null)
            {
                return false;
            }

            var definition = FindUnit(card.unitId);
            var baseLuck = definition?.luck ?? 0;
            var currentLuck = baseLuck + card.shopBuffLuck;

            if (currentLuck < PlaytestMaxLuck)
            {
                card.shopBuffLuck += PlaytestMaxLuck - currentLuck;
                return true;
            }

            return false;
        }

        private static bool MaximizeCardMorale(UnitCardState card)
        {
            if (card == null)
            {
                return false;
            }

            var definition = FindUnit(card.unitId);
            var baseMorale = definition?.morale ?? 0;
            var currentMorale = baseMorale + card.shopBuffMorale + card.roundTempMorale;

            if (currentMorale < PlaytestMaxMorale)
            {
                card.shopBuffMorale += PlaytestMaxMorale - currentMorale;
                return true;
            }

            return false;
        }

        private static bool ResetCardLuckAndMorale(UnitCardState card)
        {
            if (card == null)
            {
                return false;
            }

            var changed = card.shopBuffLuck != 0
                || card.shopBuffMorale != 0
                || card.roundTempMorale != 0
                || card.pendingNextRoundPermanentLuck != 0;

            card.shopBuffLuck = 0;
            card.shopBuffMorale = 0;
            card.roundTempMorale = 0;
            card.pendingNextRoundPermanentLuck = 0;
            return changed;
        }

        private static UnitDefinition FindUnit(string unitId)
        {
            var data = ProphecyGameSession.Instance?.Data;
            if (data == null)
            {
                return null;
            }

            return data.FindUnit(unitId) ?? data.Units.FirstOrDefault(unit => unit != null && !unit.hidden);
        }
    }
}
#endif
