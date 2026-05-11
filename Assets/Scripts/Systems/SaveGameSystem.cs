using System;
using System.IO;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using UnityEngine;

namespace ProphecyCentury.Systems
{
    public sealed class SaveGameSystem
    {
        private const string SaveFileName = "prophecy_century_run.json";

        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public bool SaveCurrentRun()
        {
            var run = ProphecyGameSession.Instance?.CurrentRun;
            if (run == null)
            {
                return false;
            }

            try
            {
                var json = JsonUtility.ToJson(run, true);
                File.WriteAllText(SavePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save failed: {ex.Message}");
                return false;
            }
        }

        public bool LoadCurrentRun()
        {
            if (!File.Exists(SavePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(SavePath);
                var run = JsonUtility.FromJson<RunState>(json);
                if (run == null)
                {
                    return false;
                }

                Normalize(run);
                ProphecyGameSession.Instance.RestoreRun(run);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Load failed: {ex.Message}");
                return false;
            }
        }

        private static void Normalize(RunState run)
        {
            if (run.boardUnits == null) run.boardUnits = new System.Collections.Generic.List<BoardUnitState>();
            if (run.handCards == null) run.handCards = new System.Collections.Generic.List<UnitCardState>();
            if (run.shopCards == null) run.shopCards = new System.Collections.Generic.List<UnitCardState>();
            if (run.shopPool == null) run.shopPool = new System.Collections.Generic.List<ShopPoolEntryState>();
            if (run.battleHistory == null) run.battleHistory = new System.Collections.Generic.List<BattleHistoryEntryState>();
            if (run.heroState == null) run.heroState = new HeroRuntimeState();
            if (run.manageResources == null) run.manageResources = new ManageResourceState();
            if (run.pendingBattleRewards == null) run.pendingBattleRewards = new BattleRewardState();
            if (run.pendingBattleRewards.discoverFaithRewards == null) run.pendingBattleRewards.discoverFaithRewards = new System.Collections.Generic.List<DiscoverFaithRewardState>();
            if (run.campaignRoundLimit <= 0)
            {
                run.campaignRoundLimit = 20;
            }
        }
    }
}
