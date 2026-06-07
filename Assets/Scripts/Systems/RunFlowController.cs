using System;
using System.Collections.Generic;
using System.IO;
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
        public readonly DayNightCycleController DayNightCycleController = new DayNightCycleController();
        public readonly WorldMapSystem WorldMapSystem = new WorldMapSystem();
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
            var run = ProphecyGameSession.Instance.CurrentRun;
            run.state = "manage";
            run.phase = GamePhase.NightManage;
        }

        public bool EnterNight()
        {
            return DayNightCycleController.EnterNight(ProphecyGameSession.Instance.CurrentRun);
        }

        public bool EndNight()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            if (run == null || run.phase != GamePhase.NightManage)
            {
                return false;
            }

            ResolveNightEndBeforeNewDay(run);
            var advanced = DayNightCycleController.EndNight(run);
            if (advanced)
            {
                StartNewDayFlow(run);
            }

            return advanced;
        }

        public bool StartNewDay()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            if (run != null && run.phase == GamePhase.NightManage)
            {
                run.dayCount = Math.Max(0, run.dayCount) + 1;
            }

            var started = DayNightCycleController.StartNewDay(run);
            if (started)
            {
                StartExplorationDayFlow(run);
            }

            return started;
        }

        public bool BeginMapBattle(string nodeId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var map = ResolveCurrentMap();
            var node = map?.nodes?.FirstOrDefault(item => item != null && item.id == nodeId);
            if (run == null || node == null || (node.type != "battle" && node.type != "boss"))
            {
                return false;
            }

            run.isExplorationBattle = true;
            run.explorationBattleNodeId = node.id;
            run.explorationBattleEnemyPresetId = node.enemyPresetId;
            run.explorationBattleNodeType = node.type;
            SetBattlePhase();
            return true;
        }

        public NodeEventResult MoveToMapNode(string nodeId)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var map = ResolveCurrentMap();
            if (!WorldMapSystem.MoveToNode(run, map, nodeId))
            {
                return null;
            }

            return ResolveCurrentMapNode();
        }

        public NodeEventResult ResolveCurrentMapNode()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var map = ResolveCurrentMap();
            var result = WorldMapSystem.ResolveNode(run, map);
            if (result == null || result.requiresBattle || result.eventType == NodeEventType.None || result.alreadyCleared)
            {
                return result;
            }

            WorldMapSystem.MarkNodeCleared(run, map, result.nodeId);
            StartNextNightManageAfterDayNode(run);
            ApplyNodeReward(run, result, result.nodeId);
            return result;
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
            var run = ProphecyGameSession.Instance.CurrentRun;
            run.state = "battle";
            run.phase = GamePhase.Battle;
        }

        public void FinishBattlePhase()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            run.state = "settle";
            run.phase = GamePhase.Settle;
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
                run.state = "gameover";
                run.phase = GamePhase.GameOver;
                ClearExplorationBattleContext(run);
                return;
            }

            if (run.playerHp <= 0)
            {
                run.state = "gameover";
                run.phase = GamePhase.GameOver;
                ClearExplorationBattleContext(run);
                return;
            }

            if (run.isExplorationBattle)
            {
                ResolveExplorationBattleOutcome(run, result);
                return;
            }

            if (run.campaignRoundLimit > 0 && run.round >= run.campaignRoundLimit)
            {
                run.campaignCompleted = true;
                run.state = "victory";
                run.phase = GamePhase.Victory;
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
            run.phase = GamePhase.NightManage;
            ManageEventResolver.ResolveRoundStart(run);
            CaptureAbilityTrigger();
            ApplyPendingBattleRewards(run);
            TrySynthesizeAll(run);
            ShopSystem.RefreshForNewRound(run);
            LogPlayerState(run);
        }
        private static void LogPlayerState(RunState run)
        {
            try
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "player_state_log.jsonl");
                var def = ProphecyGameSession.Instance.Data;
                var boardJson = string.Join(",", run.boardUnits.Select(u =>
                {
                    var d = def.FindUnit(u.unitId);
                    var cnt = u.baseCount > 0 ? u.baseCount : (d != null && d.defaultCount > 0 ? d.defaultCount : d != null && d.startCount > 0 ? d.startCount : 1);
                    return "{\"id\":\"" + u.unitId + "\",\"name\":\"" + u.name + "\",\"star\":" + u.star + ",\"count\":" + cnt + ",\"slot\":\"" + (u is BoardUnitState b ? b.boardSlotId : "?") + "\"}";
                }));
                var line = "{\"round\":" + run.round + ",\"type\":\"state\",\"gold\":" + run.gold + ",\"shopLevel\":" + run.shopLevel + ",\"hp\":" + run.playerHp + ",\"wins\":" + run.campaignWins + ",\"board\":[" + boardJson + "],\"handCount\":" + run.handCards.Count + "}";
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
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

        
        /// <summary>
        /// After a battle victory, generate a 3-card pick reward.
        /// Harder enemies grant more rerolls. Boss grants 2 picks.
        /// </summary>
        public BattleUnitPickState CreateBattleUnitPickReward(int enemyScore)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var data = ProphecyGameSession.Instance.Data;
            var targetStar = Math.Min((run?.shopLevel ?? 1) + 1, 6);
            int picks, rerolls;
            if (enemyScore > 5000) { picks = 2; rerolls = 3; }
            else if (enemyScore > 2000) { picks = 1; rerolls = 2; }
            else if (enemyScore > 1000) { picks = 1; rerolls = 1; }
            else { picks = 1; rerolls = 0; }
            var state = new BattleUnitPickState { remainingPicks = picks, remainingRerolls = rerolls, choiceStar = targetStar, choices = GeneratePickChoices(targetStar, run) };
            run.pendingBattleUnitPick = state;
            return state;
        }
        public bool RerollBattleUnitPick()
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var state = run?.pendingBattleUnitPick;
            if (state == null || state.remainingRerolls <= 0) return false;
            state.remainingRerolls -= 1;
            state.choices = GeneratePickChoices(state.choiceStar, run);
            return true;
        }
        public bool ChooseBattleUnitPick(int choiceIndex)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            var state = run?.pendingBattleUnitPick;
            if (state == null || state.remainingPicks <= 0 || choiceIndex < 0 || choiceIndex >= (state.choices?.Count ?? 0) || state.choices[choiceIndex].selected) return false;
            var definition = ProphecyGameSession.Instance.Data.FindUnit(state.choices[choiceIndex].unitId);
            if (definition == null || run.handCards.Count >= HandMaxCount) return false;
            state.choices[choiceIndex].selected = true;
            state.remainingPicks -= 1;
            return ChooseUnitPickCard(definition);
        }
        public BattleUnitPickState GetBattleUnitPickState() { return ProphecyGameSession.Instance.CurrentRun?.pendingBattleUnitPick; }
        public void ClearBattleUnitPick() { var r = ProphecyGameSession.Instance.CurrentRun; if (r != null) r.pendingBattleUnitPick = null; }
        private List<BattleUnitPickChoice> GeneratePickChoices(int targetStar, RunState run)
        {
            var data = ProphecyGameSession.Instance.Data;
            var pool = data.Units.Where(u => u != null && !u.hidden && u.star == targetStar).ToList();
            if (pool.Count == 0) { var fs = data.Units.Where(u => u != null && !u.hidden && u.star <= targetStar).Select(u => u.star).DefaultIfEmpty(1).Max(); pool = data.Units.Where(u => u != null && !u.hidden && u.star == fs).ToList(); }
            var br = new HashSet<string>(run.boardUnits.Where(u => u != null).Select(u => data.FindUnit(u.unitId)?.race).Where(r => !string.IsNullOrWhiteSpace(r)));
            var bf = new HashSet<string>(run.boardUnits.Where(u => u != null).Select(u => data.FindUnit(u.unitId)?.faith).Where(f => !string.IsNullOrWhiteSpace(f)));
            var pref = pool.Where(u => br.Contains(u.race) || bf.Contains(u.faith)).ToList();
            var std = pool.Where(u => !pref.Contains(u)).ToList();
            var choices = new List<BattleUnitPickChoice>();
            var picked = new HashSet<string>();
            for (var i = 0; i < 3 && pool.Count > 0; i++)
            {
                UnitDefinition pick;
                if (pref.Count > 0 && _random.NextDouble() < 0.5) { var c2 = pref.Where(u => !picked.Contains(u.id)).ToList(); if (c2.Count == 0) c2 = pref; pick = c2[_random.Next(c2.Count)]; }
                else { var c2 = std.Where(u => !picked.Contains(u.id)).ToList(); if (c2.Count == 0) c2 = pool.Where(u => !picked.Contains(u.id)).ToList(); if (c2.Count == 0) c2 = pool; pick = c2[_random.Next(c2.Count)]; }
                picked.Add(pick.id);
                choices.Add(new BattleUnitPickChoice { unitId = pick.id, name = pick.name, star = pick.star });
            }
            return choices;
        }
        private bool ChooseUnitPickCard(UnitDefinition definition)
        {
            var run = ProphecyGameSession.Instance.CurrentRun;
            if (run == null || definition == null || run.handCards.Count >= HandMaxCount) return false;
            var g = new UnitCardState { unitId = definition.id, name = definition.name, star = definition.star, baseCount = ResolveStartCount(definition), maxCount = 0 };
            run.handCards.Add(g);
            ManageEventResolver.ResolveGainUnit(run, g);
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
                ManageEventResolver.ResolveHeroBoardLeave(run, target);
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
            var boardBefore = run?.boardUnits?.ToList() ?? new List<BoardUnitState>();
            var synthesized = SynthesisSystem.TrySynthesizeAll(run);
            if (synthesized && run != null)
            {
                foreach (var removed in boardBefore.Where(unit => unit != null && !run.boardUnits.Contains(unit)))
                {
                    ManageEventResolver.ResolveHeroBoardLeave(run, removed);
                }

                CaptureAbilityTrigger();
            }

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

        private void ResolveExplorationBattleOutcome(RunState run, BattleStubResult result)
        {
            var map = ResolveCurrentMap();
            var nodeId = run.explorationBattleNodeId;
            var nodeResult = WorldMapSystem.ResolveNode(run, map, nodeId);
            if (result.Victory)
            {
                WorldMapSystem.MarkNodeCleared(run, map, nodeId);
                if (WorldMapSystem.CheckVictoryCondition(run, map))
                {
                    ApplyNodeReward(run, nodeResult, nodeId);
                    LogBattleResult(run, result);
                    ClearExplorationBattleContext(run);
                    return;
                }

                StartNextNightManageAfterDayNode(run);
                ApplyNodeReward(run, nodeResult, nodeId);
                ClearExplorationBattleContext(run);
                return;
            }

            StartNextNightManageAfterDayNode(run);
            ClearExplorationBattleContext(run);
        }

                private static void LogBattleResult(RunState run, BattleStubResult result)
        {
            try
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "player_state_log.jsonl");
                var summary = (result.Summary ?? "").Replace("\"", "'").Replace("\\", "/");
                var line = "{\"round\":" + run.round + ",\"type\":\"battle\",\"victory\":" + (result.Victory ? "true" : "false") + ",\"playerScore\":" + result.PlayerScore + ",\"enemyScore\":" + result.EnemyScore + ",\"hpDelta\":" + result.HpDelta + ",\"summary\":\"" + summary + "\"}";
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }

        private static void ApplyNodeReward(RunState run, NodeEventResult nodeResult, string sourceNodeId)
        {
            if (run == null || nodeResult == null || nodeResult.alreadyCleared)
            {
                return;
            }

            run.gold += Math.Max(0, nodeResult.rewardGold);
            if (!string.IsNullOrWhiteSpace(nodeResult.rewardTreasureId)
                && !run.inventoryItems.Any(item => item != null && item.itemId == nodeResult.rewardTreasureId && item.sourceNodeId == sourceNodeId))
            {
                run.inventoryItems.Add(new InventoryItemState
                {
                    itemId = nodeResult.rewardTreasureId,
                    count = 1,
                    sourceNodeId = sourceNodeId,
                    acquiredDay = Math.Max(1, run.dayCount)
                });
            }
        }

        private WorldMapDefinition ResolveCurrentMap()
        {
            var data = ProphecyGameSession.Instance.Data;
            var run = ProphecyGameSession.Instance.CurrentRun;
            var campaign = data?.FindCampaign(run?.campaignId);
            return data?.FindWorldMap(campaign?.mapId) ?? data?.WorldMaps?.FirstOrDefault();
        }

        private static void ClearExplorationBattleContext(RunState run)
        {
            if (run == null)
            {
                return;
            }

            run.isExplorationBattle = false;
            run.explorationBattleNodeId = null;
            run.explorationBattleEnemyPresetId = null;
            run.explorationBattleNodeType = null;
        }

        private void ResolveNightEndBeforeNewDay(RunState run)
        {
            ManageEventResolver.ResolveRoundEnd(run);
            CaptureAbilityTrigger();
            TrySynthesizeAll(run);
            run.round += 1;
        }

        private void StartNewDayFlow(RunState run)
        {
            var income = (ProphecyGameSession.Instance.Data.Config?.roundIncomeBase ?? 2) + run.round;
            run.gold = income;
            ManageEventResolver.ResolveRoundStart(run);
            CaptureAbilityTrigger();
            ApplyPendingBattleRewards(run);
            TrySynthesizeAll(run);
            ShopSystem.RefreshForNewRound(run);
            LogPlayerState(run);
        }

        private void StartExplorationDayFlow(RunState run)
        {
            if (run == null)
            {
                return;
            }

            run.remainingMovePoints = Math.Min(Math.Max(1, run.maxMovePoints), 1);
        }

        private static void ReturnToNightManage(RunState run)
        {
            if (run == null || run.phase == GamePhase.Victory || run.phase == GamePhase.GameOver)
            {
                return;
            }

            run.remainingMovePoints = 0;
            run.phase = GamePhase.NightManage;
            run.state = "manage";
        }

        private void StartNextNightManageAfterDayNode(RunState run)
        {
            if (run == null || run.phase == GamePhase.Victory || run.phase == GamePhase.GameOver)
            {
                return;
            }

            NextRound();
            run.remainingMovePoints = 0;
            run.phase = GamePhase.NightManage;
            run.state = "manage";
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
