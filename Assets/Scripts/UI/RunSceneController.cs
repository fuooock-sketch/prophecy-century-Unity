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
        [SerializeField] private Text campaignLabel;
        [SerializeField] private Text heroLabel;
        [SerializeField] private Text logLabel;
        [SerializeField] private Text shopMetaLabel;

        [Header("Panels")]
        [SerializeField] private Text shopText;
        [SerializeField] private Text handText;
        [SerializeField] private Text boardText;
        [SerializeField] private Text battlePreviewText;

        private readonly RunFlowController _flow = new RunFlowController();
        private readonly BattleStubSystem _battleStub = new BattleStubSystem();

        private RunState Run => ProphecyGameSession.Instance.CurrentRun;

        private void Start()
        {
            if (ProphecyGameSession.Instance == null)
            {
                Debug.LogError("ProphecyGameSession is missing.");
                return;
            }

            if (!ProphecyGameSession.Instance.HasCurrentRun)
            {
                _flow.PrepareNewRun(null, null);
            }

            EnsureShopInitialized();
            WriteLog("Run scene ready.");
            RefreshView();
        }

        public void RefreshShop()
        {
            var success = _flow.RefreshShop();
            WriteLog(success ? "Shop refreshed for 1 gold." : "Could not refresh shop.");
            RefreshView();
        }

        public void UpgradeShop()
        {
            var success = _flow.UpgradeShop();
            WriteLog(success ? "Shop upgraded." : "Could not upgrade shop.");
            RefreshView();
        }

        public void ToggleShopLock()
        {
            var locked = _flow.ToggleShopLock();
            WriteLog(locked ? "Shop locked for next round." : "Shop unlocked.");
            RefreshView();
        }

        public void BuyFirstCard()
        {
            var success = _flow.BuyUnit(0);
            WriteLog(success ? "Bought the first shop card." : "Could not buy the first shop card.");
            RefreshView();
        }

        public void DeployFirstCard()
        {
            var success = _flow.DeployUnit(0);
            WriteLog(success ? "Deployed the first hand card." : "Could not deploy the first hand card.");
            RefreshView();
        }

        public void SellLastHandCard()
        {
            var success = _flow.SellHandUnit(Run.handCards.Count - 1);
            WriteLog(success ? "Sold the last hand card." : "Could not sell a hand card.");
            RefreshView();
        }

        public void SellLastBoardUnit()
        {
            var target = Run.boardUnits.LastOrDefault()?.boardSlotId;
            var success = !string.IsNullOrWhiteSpace(target) && _flow.SellBoardUnit(target);
            WriteLog(success ? "Sold the last board unit." : "Could not sell a board unit.");
            RefreshView();
        }

        public void StartBattle()
        {
            _flow.EnterBattlePhase();
            var result = _battleStub.Resolve(Run);
            _flow.FinishBattlePhase();

            if (result.Victory)
            {
                _flow.NextRound();
            }

            if (Run.playerHp <= 0)
            {
                Run.state = "gameover";
            }

            Run.lastBattleSummary = result.Summary;
            WriteLog(result.Summary);
            RefreshView();
        }

        public void StartNewRun()
        {
            _flow.PrepareNewRun(null, null);
            WriteLog("Started a new run.");
            RefreshView();
        }

        public void ReturnToTitle()
        {
            RuntimeUiBootstrap.ShowTitleScreen();
        }

        public void RefreshView()
        {
            var data = ProphecyGameSession.Instance.Data;
            goldLabel.text = $"Gold: {Run.gold}";
            roundLabel.text = $"Round: {Run.round}";
            hpLabel.text = $"HP: {Run.playerHp}";
            stateLabel.text = $"State: {Run.state}";
            if (shopMetaLabel != null)
            {
                shopMetaLabel.text = $"Shop L{Run.shopLevel}  Upgrade {_flow.ShopSystem.GetCurrentShopUpgradeCost(Run)}g  {(Run.isShopLocked ? "Locked" : "Unlocked")}";
            }

            var campaign = data.Campaigns.FirstOrDefault(item => item.id == Run.campaignId);
            var hero = data.Heroes.FirstOrDefault(item => item.id == Run.heroId);
            campaignLabel.text = $"Campaign: {(campaign != null ? campaign.name : Run.campaignId)}";
            heroLabel.text = $"Hero: {(hero != null ? hero.name : Run.heroId)}";

            shopText.text = FormatShop();
            handText.text = FormatHand();
            boardText.text = FormatBoard();
            battlePreviewText.text = FormatBattlePreview();
        }

        private void EnsureShopInitialized()
        {
            _flow.ShopSystem.InitializeShop(Run);
        }

        private void WriteLog(string message)
        {
            logLabel.text = $"Log:\n{message}";
        }

        private string FormatShop()
        {
            if (Run.shopCards.Count == 0)
            {
                return "Shop\n(empty)";
            }

            var lines = Run.shopCards.Select((card, index) =>
                card == null
                    ? $"{index + 1}. SOLD"
                    : $"{index + 1}. {card.name}  {card.star}*{(card.isGolden ? " GOLD" : string.Empty)}");
            return "Shop\n" + string.Join("\n", lines);
        }

        private string FormatHand()
        {
            if (Run.handCards.Count == 0)
            {
                return "Hand\n(empty)";
            }

            var lines = Run.handCards.Select((card, index) => $"{index + 1}. {card.name}  {card.star}*{(card.isGolden ? " GOLD" : string.Empty)}");
            return "Hand\n" + string.Join("\n", lines);
        }

        private string FormatBoard()
        {
            if (Run.boardUnits.Count == 0)
            {
                return "Board\n(empty)";
            }

            var lines = Run.boardUnits.Select(unit => $"{unit.boardSlotId}: {unit.name}  {unit.star}*{(unit.isGolden ? " GOLD" : string.Empty)}");
            return "Board\n" + string.Join("\n", lines);
        }

        private string FormatBattlePreview()
        {
            var unitCount = Run.boardUnits.Count;
            var playerScore = Run.boardUnits.Sum(unit => unit.star * 80 + 140) + unitCount * 25;
            var enemyScore = 180 + (Run.round - 1) * 90;
            return $"Battle Preview\nPlayer score: {playerScore}\nEnemy score: {enemyScore}\nLast battle: {Run.lastBattleSummary ?? "None"}";
        }
    }
}
