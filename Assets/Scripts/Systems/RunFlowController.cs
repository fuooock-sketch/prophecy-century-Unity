using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class RunFlowController
    {
        private const int HandMaxCount = 9;
        public readonly ShopSystem ShopSystem = new ShopSystem();
        public readonly BoardSystem BoardSystem = new BoardSystem();
        public readonly SynthesisSystem SynthesisSystem = new SynthesisSystem();
        public readonly ManageEventResolver ManageEventResolver;
        private readonly Random _random = new Random();
        private bool _synthesizedSinceLastConsume;
        private bool _abilityTriggeredSinceLastConsume;

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
            ResolveRoundEndBeforeBattle();
            SetBattlePhase();
        }

        public void ResolveRoundEndBeforeBattle()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            ManageEventResolver.ResolveRoundEnd(run);
            CaptureAbilityTrigger();
            TrySynthesizeAll(run);
        }

        public void SetBattlePhase()
        {
            ProphecyGameSession.Instance.CurrentRun.state = "battle";
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
            CaptureAbilityTrigger();
            ApplyPendingBattleRewards(run);
            TrySynthesizeAll(run);
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
                CaptureAbilityTrigger();
                TrySynthesizeAll(run);
            }

            return success;
        }

        public bool DeployUnit(int handIndex, string boardSlotId = null, bool deferSynthesis = false)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var success = BoardSystem.DeployFromHand(run, handIndex, boardSlotId);
            if (success)
            {
                var deployed = string.IsNullOrWhiteSpace(boardSlotId)
                    ? run.boardUnits.LastOrDefault()
                    : run.boardUnits.LastOrDefault(unit => unit.boardSlotId == boardSlotId);
                ManageEventResolver.ResolveEntry(run, deployed);
                CaptureAbilityTrigger();
                if (!deferSynthesis)
                {
                    TrySynthesizeAll(run);
                }
            }

            return success;
        }

        public bool HasTargetedEntryPower(UnitCardState unit)
        {
            return ManageEventResolver.HasTargetedEntryPower(unit);
        }

        public int ResolveTargetedEntryPower(string sourceSlotId, string targetSlotId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var source = run?.boardUnits.FirstOrDefault(unit => unit.boardSlotId == sourceSlotId);
            var target = run?.boardUnits.FirstOrDefault(unit => unit.boardSlotId == targetSlotId);
            var value = ManageEventResolver.ResolveTargetedEntryPower(run, source, target);
            if (value > 0)
            {
                CaptureAbilityTrigger();
                TrySynthesizeAll(run);
            }

            return value;
        }

        public bool UseForestGemCard(int handIndex, string boardSlotId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var success = ManageEventResolver.UseForestGemCardOnBoardUnit(run, handIndex, boardSlotId);
            if (success)
            {
                CaptureAbilityTrigger();
                TrySynthesizeAll(run);
            }

            return success;
        }

        public IReadOnlyList<UnitDefinition> CreateGoldDeployRewardChoices(out int actualStar)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var targetStar = Math.Min((run?.shopLevel ?? 1) + 1, 6);
            var data = ProphecyGameSession.Instance.Data;
            var pool = data.Units
                .Where(unit => unit != null && !unit.hidden && unit.star == targetStar)
                .ToList();

            actualStar = targetStar;
            if (pool.Count == 0)
            {
                actualStar = data.Units
                    .Where(unit => unit != null && !unit.hidden && unit.star <= targetStar)
                    .Select(unit => unit.star)
                    .DefaultIfEmpty(1)
                    .Max();
                var fallbackStar = actualStar;
                pool = data.Units
                    .Where(unit => unit != null && !unit.hidden && unit.star == fallbackStar)
                    .ToList();
            }

            var choices = new List<UnitDefinition>();
            var available = new List<UnitDefinition>(pool);
            var count = Math.Max(1, data.Config?.milestoneRewardChoices ?? 3);
            for (var i = 0; i < count && pool.Count > 0; i += 1)
            {
                if (available.Count > 0)
                {
                    var index = _random.Next(available.Count);
                    choices.Add(available[index]);
                    available.RemoveAt(index);
                }
                else
                {
                    choices.Add(pool[_random.Next(pool.Count)]);
                }
            }

            return choices;
        }

        public bool ChooseGoldDeployReward(UnitDefinition definition)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            if (run == null || definition == null || run.handCards.Count >= HandMaxCount)
            {
                return false;
            }

            var gained = new UnitCardState
            {
                unitId = definition.id,
                name = definition.name,
                star = definition.star,
                baseCount = ResolveStartCount(definition),
                maxCount = 0
            };
            run.handCards.Add(gained);
            ManageEventResolver.ResolveGainUnit(run, gained);
            CaptureAbilityTrigger();
            TrySynthesizeAll(run);
            return true;
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
            CaptureAbilityTrigger();
            var success = BoardSystem.SellFromHand(run, handIndex);
            if (success)
            {
                TrySynthesizeAll(run);
            }

            return success;
        }

        public bool SellBoardUnit(string boardSlotId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var target = run.boardUnits.LastOrDefault(unit => unit.boardSlotId == boardSlotId);
            ManageEventResolver.ResolveSell(run, target);
            CaptureAbilityTrigger();
            var success = BoardSystem.SellFromBoard(run, boardSlotId);
            if (success)
            {
                ManageEventResolver.ResolveLeave(run, target, "sell");
                CaptureAbilityTrigger();
                TrySynthesizeAll(run);
            }

            return success;
        }

        public bool ConsumeSynthesisFlag()
        {
            var synthesized = _synthesizedSinceLastConsume;
            _synthesizedSinceLastConsume = false;
            return synthesized;
        }

        public bool ConsumeAbilityTriggerFlag()
        {
            var triggered = _abilityTriggeredSinceLastConsume;
            _abilityTriggeredSinceLastConsume = false;
            return triggered;
        }

        public List<DevourShopEventState> ConsumeDevourShopEvents()
        {
            return ManageEventResolver.ConsumeDevourShopEvents();
        }

        public ManageFeedbackEventsState ConsumeManageFeedbackEvents()
        {
            return ManageEventResolver.ConsumeFeedbackEvents();
        }

        private void CaptureAbilityTrigger()
        {
            _abilityTriggeredSinceLastConsume |= ManageEventResolver.ConsumeAbilityTriggered();
        }

        private bool TrySynthesizeAll(RunState run)
        {
            var synthesized = SynthesisSystem.TrySynthesizeAll(run);
            ManageEventResolver.RefreshBoardAuras(run);
            _synthesizedSinceLastConsume |= synthesized;
            return synthesized;
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
                AddForestGemCardsToHand(run, Math.Max(0, unit.pendingNextRoundForestGems));

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
            for (var i = 0; i < reward.count && run.handCards.Count < HandMaxCount && pool.Count > 0; i += 1)
            {
                var unit = pool[_random.Next(pool.Count)];
                run.handCards.Add(new UnitCardState
                {
                    unitId = unit.id,
                    name = unit.name,
                    star = unit.star,
                    baseCount = ResolveStartCount(unit),
                    maxCount = 0
                });
                added += 1;
            }

            return added;
        }

        private static int AddForestGemCardsToHand(RunState run, int amount)
        {
            if (run?.handCards == null || amount <= 0)
            {
                return 0;
            }

            var added = 0;
            while (added < amount && run.handCards.Count < HandMaxCount)
            {
                run.handCards.Add(ManageEventResolver.CreateForestGemCard());
                added += 1;
            }

            return added;
        }

        private static int ResolveStartCount(UnitDefinition unit)
        {
            if (unit == null)
            {
                return 1;
            }

            return Math.Max(1, unit.defaultCount > 0 ? unit.defaultCount : unit.startCount > 0 ? unit.startCount : unit.baseCount > 0 ? unit.baseCount : 1);
        }
    }
}
