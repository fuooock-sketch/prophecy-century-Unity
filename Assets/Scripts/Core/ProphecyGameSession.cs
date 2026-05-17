using System.Linq;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
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

            if (CurrentRun == null)
            {
                StartNewRun();
            }
        }

        public void StartNewRun(string campaignId = null, string heroId = null)
        {
            var campaign = campaignId ?? Data.Campaigns.FirstOrDefault()?.id ?? "south_town_adventure";
            var hero = heroId ?? Data.Heroes.FirstOrDefault()?.id ?? "james";

            CurrentRun = new RunState
            {
                campaignId = campaign,
                heroId = hero,
                state = "manage",
                gold = Data.Config?.startGold ?? 3,
                round = 1,
                playerHp = Data.Config?.playerStartHp ?? 100,
                shopLevel = 1,
                shopUpgradeAnchorRound = 1,
                campaignRoundLimit = ResolveCampaignRoundLimit(campaign)
            };
        }

        public void RestoreRun(RunState runState)
        {
            CurrentRun = runState;
        }

        private static int ResolveCampaignRoundLimit(string campaignId)
        {
            switch (campaignId)
            {
                case "snow_peak_defense":
                    return 18;
                case "song_of_sang_city":
                    return 24;
                default:
                    return 20;
            }
        }
    }
}
