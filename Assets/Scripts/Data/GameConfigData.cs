using System;
using System.Collections.Generic;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class GameConfigData
    {
        public int width;
        public int height;
        public int playerStartHp;
        public int startGold;
        public int roundIncomeBase;
        public int[] roundIncomeByRound;
        public int goldCarryLimit;
        public int[] worldMapExpectedPlayerScoreByDay;
        public int[] worldMapMinEnemyUnitsByDay;
        public int[] shopUpgradeCost;
        public int[] shopSlots;
        public int[] shopMaxStar;
        public float goldUnitRate;
        public int unitBuyCost;
        public int unitSellReward;
        public float battleSpeedScale;
        public float attackIntervalBase;
        public int battleTime;
        public float critRateCap;
        public float critDamageMultiple;
        public float moraleExtraAttackRate;
        public float moraleCounterRate;
        public int synthesizeCount;
        public int goldUnitMultiple;
        public int manageTime;
        public int settleTime;
        public int victoryRound;
        public int[] milestoneRewardRounds;
        public int milestoneRewardChoices;
        public BoardRowData[] boardLayout;
        public BoardPositionData[] boardPositions;

        public IReadOnlyList<string> GetBoardOrder()
        {
            var order = new List<string>();
            if (boardLayout == null)
            {
                return order;
            }

            foreach (var row in boardLayout)
            {
                if (row?.slots == null)
                {
                    continue;
                }

                order.AddRange(row.slots);
            }

            return order;
        }
    }

    [Serializable]
    public sealed class BoardRowData
    {
        public string[] slots;
    }

    [Serializable]
    public sealed class BoardPositionData
    {
        public string id;
        public float x;
        public float y;
    }
}
