using ProphecyCentury.Core;

namespace ProphecyCentury.Systems
{
    public sealed class RunFlowController
    {
        public readonly ShopSystem ShopSystem = new ShopSystem();
        public readonly BoardSystem BoardSystem = new BoardSystem();

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
            ProphecyGameSession.Instance.CurrentRun.state = "battle";
        }

        public void FinishBattlePhase()
        {
            ProphecyGameSession.Instance.CurrentRun.state = "settle";
        }

        public void NextRound()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var income = ProphecyGameSession.Instance.Data.Config?.roundIncomeBase ?? 2;
            run.round += 1;
            run.gold += income;
            run.state = "manage";
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
            return ShopSystem.BuyFromShop(ProphecyGameSession.Instance.CurrentRun, shopIndex);
        }

        public bool DeployUnit(int handIndex)
        {
            return BoardSystem.DeployFromHand(ProphecyGameSession.Instance.CurrentRun, handIndex);
        }

        public bool SellHandUnit(int handIndex)
        {
            return BoardSystem.SellFromHand(ProphecyGameSession.Instance.CurrentRun, handIndex);
        }

        public bool SellBoardUnit(string boardSlotId)
        {
            return BoardSystem.SellFromBoard(ProphecyGameSession.Instance.CurrentRun, boardSlotId);
        }
    }
}
