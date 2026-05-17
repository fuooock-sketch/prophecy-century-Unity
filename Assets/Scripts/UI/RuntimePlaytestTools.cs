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

            if (Input.GetKeyDown(KeyCode.G))
            {
                AddGold();
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
                return;
            }

            _toolbar = new GameObject("GMToolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            _toolbar.transform.SetParent(canvas.transform, false);
            var rect = _toolbar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(14f, -14f);
            rect.sizeDelta = new Vector2(644f, 40f);

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
            CreateToolbarButton("\u91d1\u5e01 +10 G", AddGold);
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
                boardSlotId = slotId
            };
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
