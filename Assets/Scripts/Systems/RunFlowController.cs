using System;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class RunFlowController
    {
        public readonly ShopSystem ShopSystem = new ShopSystem();
        public readonly BoardSystem BoardSystem = new BoardSystem();
        public readonly SynthesisSystem SynthesisSystem = new SynthesisSystem();
        public readonly ManageEventResolver ManageEventResolver;
        private readonly Random _random = new Random();

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

        public void ResolveBattleOutcome(BattleStubResult result)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            if (run == null || result == null)
            {
                return;
            }

            run.lastBattleSummary = result.Summary;
            run.battleHistory.Add(new BattleHistoryEntryState
            {
                round = run.round,
                victory = result.Victory,
                playerScore = result.PlayerScore,
                enemyScore = result.EnemyScore,
                hpDelta = result.HpDelta,
                playerDamage = result.PlayerDamage,
                enemyDamage = result.EnemyDamage,
                summary = result.Summary
            });

            if (run.battleHistory.Count > 50)
            {
                run.battleHistory.RemoveAt(0);
            }

            if (result.Victory)
            {
                run.campaignWins += 1;
            }
            else
            {
                run.campaignLosses += 1;
            }

            if (run.playerHp <= 0)
            {
                run.state = "gameover";
                return;
            }

            if (result.Victory && run.campaignRoundLimit > 0 && run.round >= run.campaignRoundLimit)
            {
                run.campaignCompleted = true;
                run.state = "victory";
                return;
            }

            NextRound();
        }

        public void NextRound()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            run.round += 1;
            var income = (ProphecyGameSession.Instance.Data.Config?.roundIncomeBase ?? 2) + run.round;
            run.gold = income;
            run.state = "manage";
            ManageEventResolver.ResolveRoundStart(run);
            ApplyPendingBattleRewards(run);
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

        private void ApplyPendingBattleRewards(RunState run)
        {
            if (run == null)
            {
                return;
            }

            var rewards = run.pendingBattleRewards ?? new BattleRewardState();
            run.gold += Math.Max(0, rewards.nextRoundGold);
            run.manageResources.shopGeneratedBuffAttack += Math.Max(0, rewards.nextRoundShopBuffAttack);

            foreach (var unit in run.boardUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                unit.roundTempAttack += Math.Max(0, unit.pendingNextRoundTempAttack);
                unit.roundTempPower += Math.Max(0, unit.pendingNextRoundTempPower);
                unit.shopBuffHp += Math.Max(0, unit.pendingNextRoundPermanentHp);
                unit.shopBuffPower += Math.Max(0, unit.pendingNextRoundPermanentPower);
                unit.shopBuffLuck += Math.Max(0, unit.pendingNextRoundPermanentLuck);
                run.manageResources.forestGems += Math.Max(0, unit.pendingNextRoundForestGems);

                if (!string.IsNullOrWhiteSpace(unit.pendingNextRoundEvolveTo))
                {
                    EvolveWithoutEvents(unit, unit.pendingNextRoundEvolveTo);
                }

                ClearPendingBattleState(unit);
            }

            foreach (var reward in rewards.discoverFaithRewards ?? Enumerable.Empty<DiscoverFaithRewardState>())
            {
                AddRandomUnitsToHand(run, reward);
            }

            run.pendingBattleRewards = new BattleRewardState();
        }

        private static void ClearPendingBattleState(UnitCardState unit)
        {
            unit.pendingNextRoundTempAttack = 0;
            unit.pendingNextRoundTempPower = 0;
            unit.pendingNextRoundPermanentHp = 0;
            unit.pendingNextRoundPermanentPower = 0;
            unit.pendingNextRoundPermanentLuck = 0;
            unit.pendingNextRoundForestGems = 0;
            unit.pendingNextRoundEvolveTo = null;
        }

        private static void EvolveWithoutEvents(UnitCardState unit, string targetUnitId)
        {
            var definition = ProphecyGameSession.Instance.Data.FindUnit(targetUnitId);
            if (unit == null || definition == null)
            {
                return;
            }

            unit.unitId = definition.id;
            unit.name = definition.name;
            unit.star = definition.star;
        }

        private int AddRandomUnitsToHand(RunState run, DiscoverFaithRewardState reward)
        {
            if (run == null || reward == null || reward.count <= 0)
            {
                return 0;
            }

            var pool = ProphecyGameSession.Instance.Data.Units
                .Where(unit => unit != null
                    && !unit.hidden
                    && (string.IsNullOrWhiteSpace(reward.faith) || unit.faith == reward.faith)
                    && (string.IsNullOrWhiteSpace(reward.race) || unit.race == reward.race))
                .ToList();
            var added = 0;
            for (var i = 0; i < reward.count && run.handCards.Count < 10 && pool.Count > 0; i += 1)
            {
                var unit = pool[_random.Next(pool.Count)];
                run.handCards.Add(new UnitCardState { unitId = unit.id, name = unit.name, star = unit.star });
                added += 1;
            }

            return added;
        }
    }
}
