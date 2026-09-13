using System;
using System.IO;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using UnityEngine;

namespace ProphecyCentury.Systems
{
    public sealed class SaveGameSystem
    {
        private const string SaveFileName = "prophecy_century_run.json";

        public string LegacySavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public string SavePath
        {
            get
            {
                if (CasualPvpSystem.IsCasual(ProphecyGameSession.Instance?.CurrentRun))
                {
                    var casualSlots = new CasualPvpSaveSlotService();
                    return casualSlots.CurrentSaveSlotIndex > 0 ? casualSlots.GetSlotPath(casualSlots.CurrentSaveSlotIndex) : casualSlots.SaveDirectory;
                }
                var slots = new SaveSlotService();
                return slots.CurrentSaveSlotIndex > 0 ? slots.GetSlotPath(slots.CurrentSaveSlotIndex) : LegacySavePath;
            }
        }

        public bool SaveCurrentRun()
        {
            if (CasualPvpSystem.IsCasual(ProphecyGameSession.Instance?.CurrentRun))
            {
                return new CasualPvpSaveSlotService().SaveCurrentRun().Success;
            }
            return new SaveSlotService().SaveCurrentRun().Success;
        }

        public bool LoadCurrentRun()
        {
            if (CasualPvpSystem.IsCasual(ProphecyGameSession.Instance?.CurrentRun))
            {
                var casualSlots = new CasualPvpSaveSlotService();
                var casualResult = casualSlots.CurrentSaveSlotIndex > 0
                    ? casualSlots.LoadGame(casualSlots.CurrentSaveSlotIndex)
                    : casualSlots.LoadMostRecentValid();
                if (casualResult.Success) Normalize(casualResult.Save.gameData);
                return casualResult.Success;
            }

            var slots = new SaveSlotService();
            slots.ImportLegacySave(LegacySavePath);
            var result = slots.CurrentSaveSlotIndex > 0
                ? slots.LoadGame(slots.CurrentSaveSlotIndex)
                : slots.LoadMostRecentValid();
            if (result.Success) Normalize(result.Save.gameData);
            return result.Success;
        }

        internal static void Normalize(RunState run)
        {
            var isLegacySave = run.saveVersion <= 0;
            if (string.IsNullOrWhiteSpace(run.gameMode)) run.gameMode = GameModeIds.Campaign;
            if (isLegacySave) run.saveVersion = 1;
            if (run.dayCount <= 0) run.dayCount = isLegacySave ? Math.Max(1, run.round) : 0;
            if (run.maxMovePoints <= 0) run.maxMovePoints = 4;
            if (run.remainingMovePoints < 0) run.remainingMovePoints = 0;
            if (string.IsNullOrWhiteSpace(run.currentNodeId)) run.currentNodeId = "start";
            if (isLegacySave) run.phase = ResolvePhase(run.state);
            CasualPvpSystem.Normalize(run);
            if (run.boardUnits == null) run.boardUnits = new System.Collections.Generic.List<BoardUnitState>();
            if (run.handCards == null) run.handCards = new System.Collections.Generic.List<UnitCardState>();
            if (run.pendingHandCards == null) run.pendingHandCards = new System.Collections.Generic.List<UnitCardState>();
            if (run.shopCards == null) run.shopCards = new System.Collections.Generic.List<UnitCardState>();
            if (run.shopPool == null) run.shopPool = new System.Collections.Generic.List<ShopPoolEntryState>();
            if (run.worldMapNodes == null) run.worldMapNodes = new System.Collections.Generic.List<WorldMapNodeState>();
            if (run.inventoryItems == null) run.inventoryItems = new System.Collections.Generic.List<InventoryItemState>();
            if (run.battleHistory == null) run.battleHistory = new System.Collections.Generic.List<BattleHistoryEntryState>();
            if (run.heroState == null) run.heroState = new HeroRuntimeState();
            if (run.manageResources == null) run.manageResources = new ManageResourceState();
            if (run.pendingBattleRewards == null) run.pendingBattleRewards = new BattleRewardState();
            if (run.pendingBattleRewards.discoverFaithRewards == null) run.pendingBattleRewards.discoverFaithRewards = new System.Collections.Generic.List<DiscoverFaithRewardState>();
            CustomChallengeSystem.Normalize(run);
            NormalizeWorldMapState(run);
            NormalizeExplorationBattleState(run);
            if (!CasualPvpSystem.IsCasual(run) && run.campaignRoundLimit <= 0)
            {
                run.campaignRoundLimit = ProphecyGameSession.Instance?.Data?.Config?.victoryRound > 0
                    ? ProphecyGameSession.Instance.Data.Config.victoryRound
                    : 20;
            }
        }

        private static void NormalizeWorldMapState(RunState run)
        {
            var map = ResolveCurrentMap(run);
            if (map?.nodes == null || map.nodes.Length == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(run.currentNodeId) || map.nodes.All(node => node == null || node.id != run.currentNodeId))
            {
                run.currentNodeId = string.IsNullOrWhiteSpace(map.startNodeId) ? "start" : map.startNodeId;
            }

            if (run.worldMapNodes.Count == 0)
            {
                foreach (var node in map.nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.id))
                    {
                        continue;
                    }

                    var isStartNode = node.id == run.currentNodeId || node.id == map.startNodeId;
                    run.worldMapNodes.Add(new WorldMapNodeState
                    {
                        nodeId = node.id,
                        isVisible = isStartNode || node.layer <= 1,
                        isVisited = isStartNode,
                        isCleared = node.type == "start"
                    });
                }
            }

            foreach (var node in map.nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.id) || run.worldMapNodes.Any(state => state != null && state.nodeId == node.id))
                {
                    continue;
                }

                run.worldMapNodes.Add(new WorldMapNodeState
                {
                    nodeId = node.id,
                    isVisible = node.layer <= 1,
                    isVisited = false,
                    isCleared = node.type == "start"
                });
            }
        }

        private static void NormalizeExplorationBattleState(RunState run)
        {
            if (!run.isExplorationBattle)
            {
                return;
            }

            run.isExplorationBattle = false;
            run.explorationBattleEnemyPresetId = null;
            run.explorationBattleNodeType = null;
            if (!string.IsNullOrWhiteSpace(run.explorationBattleNodeId))
            {
                run.currentNodeId = run.explorationBattleNodeId;
            }

            run.explorationBattleNodeId = null;
            if (run.playerHp > 0 && !run.campaignCompleted)
            {
                run.phase = GamePhase.DayExplore;
                run.state = "day";
            }
        }

        private static WorldMapDefinition ResolveCurrentMap(RunState run)
        {
            var data = ProphecyGameSession.Instance?.Data;
            if (CustomChallengeSystem.IsCustomChallengeId(run?.campaignId))
            {
                return CustomChallengeSystem.ResolveCustomChallengeMap(data);
            }

            var campaign = data?.FindCampaign(run?.campaignId);
            return data?.FindWorldMap(campaign?.mapId) ?? data?.WorldMaps?.FirstOrDefault();
        }

        private static GamePhase ResolvePhase(string state)
        {
            switch (state)
            {
                case "battle":
                    return GamePhase.Battle;
                case "settle":
                    return GamePhase.Settle;
                case "victory":
                    return GamePhase.Victory;
                case "gameover":
                    return GamePhase.GameOver;
                case "day":
                case "explore":
                    return GamePhase.DayExplore;
                case "manage":
                default:
                    return GamePhase.NightManage;
            }
        }
    }
}
