using System.Linq;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEngine;

namespace ProphecyCentury.Core
{
    public sealed class ProphecyGameSession : MonoBehaviour
    {
        public static ProphecyGameSession Instance { get; private set; }

        public GameDataRepository Data { get; private set; }
        public RunState CurrentRun { get; private set; }
        public bool HasCurrentRun => CurrentRun != null;

        public static ProphecyGameSession EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var sessionObject = new GameObject("ProphecyGameSession");
            return sessionObject.AddComponent<ProphecyGameSession>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Data = new GameDataRepository();
            Data.LoadAll();

            CurrentRun = null;
        }

        public void StartNewRun(string campaignId = null, string heroId = null)
        {
            var campaign = campaignId ?? Data.Campaigns.FirstOrDefault()?.id ?? "south_town_adventure";
            var hero = heroId ?? Data.Heroes.FirstOrDefault()?.id ?? "james";
            var customChallengeId = CustomChallengeSystem.IsCustomChallengeId(campaign) ? campaign : null;
            var map = ResolveCampaignMap(campaign);
            var startNodeId = map?.startNodeId ?? "start";

            var startFateValue = Data.Config?.playerStartHp ?? 100;
            CurrentRun = new RunState
            {
                saveVersion = 1,
                campaignId = campaign,
                heroId = hero,
                state = "manage",
                phase = GamePhase.NightManage,
                gold = Data.Config?.startGold ?? 3,
                round = 1,
                dayCount = 0,
                maxMovePoints = 1,
                remainingMovePoints = 0,
                currentNodeId = startNodeId,
                playerHp = startFateValue,
                fateValue = startFateValue,
                maxFateValue = startFateValue,
                shopLevel = 1,
                shopUpgradeAnchorRound = 1,
                campaignRoundLimit = ResolveCampaignRoundLimit(campaign),
                customChallengeId = customChallengeId
            };

            InitializeWorldMapNodeStates(CurrentRun, map);
        }

        public void RestoreRun(RunState runState)
        {
            CurrentRun = runState;
        }

        private int ResolveCampaignRoundLimit(string campaignId)
        {
            if (CustomChallengeSystem.IsCustomChallengeId(campaignId))
            {
                return 20;
            }

            switch (campaignId)
            {
                case "shadow_elemental_challenge":
                case "shadow_light_challenge":
                    return 20;
            }

            var configuredVictoryRound = Data?.Config?.victoryRound ?? 0;
            if (configuredVictoryRound > 0)
            {
                return configuredVictoryRound;
            }

            switch (campaignId)
            {
                case "snow_peak_defense":
                    return 20;
                case "song_of_sang_city":
                    return 24;
                default:
                    return 20;
            }
        }

        private WorldMapDefinition ResolveCampaignMap(string campaignId)
        {
            if (CustomChallengeSystem.IsCustomChallengeId(campaignId))
            {
                return CustomChallengeSystem.ResolveCustomChallengeMap(Data);
            }

            var campaign = Data?.FindCampaign(campaignId);
            var mapId = campaign?.mapId;
            if (!string.IsNullOrWhiteSpace(mapId))
            {
                var configuredMap = Data.FindWorldMap(mapId);
                if (configuredMap != null)
                {
                    return configuredMap;
                }
            }

            return Data?.WorldMaps?.FirstOrDefault();
        }

        private static void InitializeWorldMapNodeStates(RunState run, WorldMapDefinition map)
        {
            run.worldMapNodes.Clear();
            if (map?.nodes == null)
            {
                return;
            }

            var startNodeId = string.IsNullOrWhiteSpace(map.startNodeId) ? run.currentNodeId : map.startNodeId;
            foreach (var node in map.nodes)
            {
                if (string.IsNullOrWhiteSpace(node.id))
                {
                    continue;
                }

                var isStartNode = node.id == startNodeId;
                run.worldMapNodes.Add(new WorldMapNodeState
                {
                    nodeId = node.id,
                    isVisible = isStartNode || node.layer <= 1,
                    isVisited = isStartNode,
                    isCleared = node.type == "start"
                });
            }

            WorldMapSystem.RevealFutureLayers(run, map, startNodeId);
        }
    }
}
