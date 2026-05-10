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
                shopUpgradeAnchorRound = 1
            };
        }
    }
}
