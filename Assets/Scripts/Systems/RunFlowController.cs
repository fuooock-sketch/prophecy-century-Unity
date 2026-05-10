using System.Linq;
using ProphecyCentury.Core;

namespace ProphecyCentury.Systems
{
    public sealed class RunFlowController
    {
        public readonly ShopSystem ShopSystem = new ShopSystem();
        public readonly BoardSystem BoardSystem = new BoardSystem();
        public readonly SynthesisSystem SynthesisSystem = new SynthesisSystem();
        public readonly ManageEventResolver ManageEventResolver;

        public RunFlowController()
        {
            ManageEventResolver = new ManageEventResolver(ShopSystem);
        }

        public void PrepareNewRun(string campaignId, string heroId)
        {
            ProphecyGameSession.Instance.StartNewRun(campaignId, heroId);
        }

        public void EnterManagePhase()
        {
            ProphecyGameSession.Instance.CurrentRun.state = "manage";
        }

        public void EnterBattlePhase()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            ManageEventResolver.ResolveRoundEnd(run);
            SynthesisSystem.TrySynthesizeAll(run);
            run.state = "battle";
        }

        public void FinishBattlePhase()
        {
            ProphecyGameSession.Instance.CurrentRun.state = "settle";
        }

        public void NextRound()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            run.round += 1;
            var income = (ProphecyGameSession.Instance.Data.Config?.roundIncomeBase ?? 2) + run.round;
            run.gold = income;
            run.state = "manage";
            ManageEventResolver.ResolveRoundStart(run);
            SynthesisSystem.TrySynthesizeAll(run);
            ShopSystem.RefreshForNewRound(run);
        }

        public bool RefreshShop()
        {
            return ShopSystem.RefreshShopForCost(ProphecyGameSession.Instance.CurrentRun);
        }

        public bool UpgradeShop()
        {
            return ShopSystem.UpgradeShop(ProphecyGameSession.Instance.CurrentRun);
        }

        public bool ToggleShopLock()
        {
            return ShopSystem.ToggleShopLock(ProphecyGameSession.Instance.CurrentRun);
        }

        public bool BuyUnit(int shopIndex)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var success = ShopSystem.BuyFromShop(run, shopIndex);
            if (success)
            {
                var bought = run.handCards.Count > 0 ? run.handCards[run.handCards.Count - 1] : null;
                ManageEventResolver.ResolveGainUnit(run, bought);
                SynthesisSystem.TrySynthesizeAll(run);
            }

            return success;
        }

        public bool DeployUnit(int handIndex, string boardSlotId = null)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var success = BoardSystem.DeployFromHand(run, handIndex, boardSlotId);
            if (success)
            {
                var deployed = string.IsNullOrWhiteSpace(boardSlotId)
                    ? run.boardUnits.LastOrDefault()
                    : run.boardUnits.LastOrDefault(unit => unit.boardSlotId == boardSlotId);
                ManageEventResolver.ResolveEntry(run, deployed);
                SynthesisSystem.TrySynthesizeAll(run);
            }

            return success;
        }

        public bool MoveBoardUnit(string fromSlotId, string toSlotId)
        {
            return BoardSystem.MoveBoardUnit(ProphecyGameSession.Instance.CurrentRun, fromSlotId, toSlotId);
        }

        public bool SellHandUnit(int handIndex)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var target = handIndex >= 0 && handIndex < run.handCards.Count ? run.handCards[handIndex] : null;
            ManageEventResolver.ResolveSell(run, target);
            var success = BoardSystem.SellFromHand(run, handIndex);
            if (success)
            {
                SynthesisSystem.TrySynthesizeAll(run);
            }

            return success;
        }

        public bool SellBoardUnit(string boardSlotId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var target = run.boardUnits.LastOrDefault(unit => unit.boardSlotId == boardSlotId);
            ManageEventResolver.ResolveSell(run, target);
            var success = BoardSystem.SellFromBoard(run, boardSlotId);
            if (success)
            {
                ManageEventResolver.ResolveLeave(run, target, "sell");
                SynthesisSystem.TrySynthesizeAll(run);
            }

            return success;
        }
    }
}
