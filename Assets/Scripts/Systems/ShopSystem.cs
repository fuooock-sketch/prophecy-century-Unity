using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class ShopSystem
    {
        private readonly Random _random = new Random();

        public void InitializeShop(RunState runState)
        {
            if (runState.shopCards.Count == 0)
            {
                RefreshShop(runState);
            }
        }

        public void RefreshShop(RunState runState)
        {
            var session = ProphecyGameSession.Instance;
            var config = session.Data.Config;
            var slots = ResolveShopSlots(config?.shopSlots, runState.shopLevel);
            var maxStar = ResolveShopMaxStar(config?.shopMaxStar, runState.shopLevel);
            var pool = session.Data.Units.Where(unit => unit.star <= maxStar).ToList();

            runState.shopCards.Clear();
            if (pool.Count == 0)
            {
                return;
            }

            for (var i = 0; i < slots; i += 1)
            {
                var unit = pool[_random.Next(pool.Count)];
                runState.shopCards.Add(new UnitCardState
                {
                    unitId = unit.id,
                    name = unit.name,
                    star = unit.star
                });
            }
        }

        public bool BuyFromShop(RunState runState, int index)
        {
            var session = ProphecyGameSession.Instance;
            var config = session.Data.Config;
            if (index < 0 || index >= runState.shopCards.Count)
            {
                return false;
            }

            var cost = config?.unitBuyCost ?? 3;
            if (runState.gold < cost)
            {
                return false;
            }

            runState.gold -= cost;
            runState.handCards.Add(runState.shopCards[index]);
            runState.shopCards.RemoveAt(index);
            return true;
        }

        public int GetCurrentShopUpgradeCost(RunState runState)
        {
            var costs = ProphecyGameSession.Instance.Data.Config?.shopUpgradeCost;
            if (costs == null || costs.Length == 0)
            {
                return 0;
            }

            var index = Clamp(runState.shopLevel, 0, costs.Length - 1);
            return costs[index];
        }

        public bool UpgradeShop(RunState runState)
        {
            var costs = ProphecyGameSession.Instance.Data.Config?.shopUpgradeCost;
            var maxLevel = costs == null || costs.Length == 0 ? 6 : costs.Length;
            if (runState.shopLevel >= maxLevel)
            {
                return false;
            }

            var cost = GetCurrentShopUpgradeCost(runState);
            if (runState.gold < cost)
            {
                return false;
            }

            runState.gold -= cost;
            runState.shopLevel += 1;
            RefreshShop(runState);
            return true;
        }

        public bool RefreshShopForCost(RunState runState)
        {
            var cost = 1;
            if (runState.gold < cost)
            {
                return false;
            }

            runState.gold -= cost;
            runState.isShopLocked = false;
            RefreshShop(runState);
            return true;
        }

        public bool ToggleShopLock(RunState runState)
        {
            runState.isShopLocked = !runState.isShopLocked;
            return runState.isShopLocked;
        }

        public void RefreshForNewRound(RunState runState)
        {
            if (runState.isShopLocked)
            {
                runState.isShopLocked = false;
                return;
            }

            RefreshShop(runState);
        }

        private static int ResolveShopSlots(IReadOnlyList<int> slots, int shopLevel)
        {
            if (slots == null || slots.Count == 0)
            {
                return 3;
            }

            var clampedIndex = Clamp(shopLevel, 0, slots.Count - 1);
            return slots[clampedIndex];
        }

        private static int ResolveShopMaxStar(IReadOnlyList<int> stars, int shopLevel)
        {
            if (stars == null || stars.Count == 0)
            {
                return 1;
            }

            var clampedIndex = Clamp(shopLevel, 0, stars.Count - 1);
            return stars[clampedIndex];
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
