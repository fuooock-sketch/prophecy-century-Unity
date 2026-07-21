using System;
using System.Collections.Generic;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public sealed class ShopSystem
    {
        private const int HandMaxCount = 9;
        private readonly Random _random = new Random();
        private static readonly Dictionary<string, int> ShopLimitByUnitName = new Dictionary<string, int>();
        public void InitializeShop(RunState runState)
        {
            InitializeShopPool(runState);
            if (runState.shopCards.Count == 0)
            {
                RefreshShop(runState);
            }
        }

        public void RefreshShop(RunState runState)
        {
            InitializeShopPool(runState);
            var session = ProphecyGameSession.Instance;
            var config = session.Data.Config;
            var slots = ResolveShopSlots(config?.shopSlots, runState.shopLevel);
            var maxStar = ResolveShopMaxStar(config?.shopMaxStar, runState.shopLevel);
            ReleaseShopCardReservations(runState, runState.shopCards);
            var pool = session.Data.Units
                .Where(unit => IsAvailableForShop(runState, unit, maxStar))
                .ToList();

            runState.shopCards.Clear();
            for (var i = 0; i < slots; i += 1)
            {
                runState.shopCards.Add(CreateRandomShopCard(runState, pool));
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

            var card = runState.shopCards[index];
            if (card == null || runState.handCards.Count >= HandMaxCount)
            {
                return false;
            }

            var cost = config?.unitBuyCost ?? 3;
            if (runState.gold < cost)
            {
                return false;
            }

            runState.gold -= cost;
            card.shopPoolReserved = false;
            card.shopPoolContribution = GetShopCardPoolCost(card);
            card.fromShopPurchase = card.shopPoolContribution > 0;
            runState.handCards.Add(card);
            runState.shopCards[index] = null;
            return true;
        }

        public UnitCardState RemoveShopCardForDevour(RunState runState, int index)
        {
            if (runState == null || index < 0 || index >= runState.shopCards.Count)
            {
                return null;
            }

            var card = runState.shopCards[index];
            if (card == null)
            {
                return null;
            }

            ReleaseShopCardPool(runState, card);
            runState.shopCards[index] = null;
            return card;
        }

        public int GetCurrentShopUpgradeCost(RunState runState)
        {
            var costs = ProphecyGameSession.Instance.Data.Config?.shopUpgradeCost;
            if (costs == null || costs.Length == 0)
            {
                return 0;
            }

            if (runState.shopLevel >= costs.Length)
            {
                return 0;
            }

            var index = Clamp(runState.shopLevel, 0, costs.Length - 1);
            var baseCost = costs[index];
            var anchorRound = runState.shopUpgradeAnchorRound > 0 ? runState.shopUpgradeAnchorRound : runState.round;
            var roundDiscount = Math.Max(0, runState.round - anchorRound);
            return Math.Max(0, baseCost - roundDiscount);
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
            runState.shopUpgradeAnchorRound = runState.round;
            PreserveAndFillShop(runState);
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
                PreserveAndFillShop(runState);
                runState.isShopLocked = false;
                return;
            }

            RefreshShop(runState);
        }

        private void PreserveAndFillShop(RunState runState)
        {
            InitializeShopPool(runState);
            var session = ProphecyGameSession.Instance;
            var config = session.Data.Config;
            var slots = ResolveShopSlots(config?.shopSlots, runState.shopLevel);
            var maxStar = ResolveShopMaxStar(config?.shopMaxStar, runState.shopLevel);
            var pool = session.Data.Units
                .Where(unit => IsAvailableForShop(runState, unit, maxStar))
                .ToList();
            var preserved = runState.shopCards.Take(slots).ToList();

            runState.shopCards.Clear();
            for (var i = 0; i < slots; i += 1)
            {
                var oldCard = i < preserved.Count ? preserved[i] : null;
                runState.shopCards.Add(oldCard ?? CreateRandomShopCard(runState, pool));
            }
        }

        public void RefundShopPoolFromUnit(UnitCardState unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.unitId) || unit.shopPoolContribution <= 0)
            {
                return;
            }

            AdjustShopPoolRemain(ProphecyGameSession.Instance.CurrentRun, unit.unitId, unit.shopPoolContribution);
            unit.shopPoolContribution = 0;
            unit.fromShopPurchase = false;
        }

        public bool IsUnitAvailableInPool(RunState runState, string unitId, int count = 1)
        {
            if (runState == null || string.IsNullOrWhiteSpace(unitId) || count <= 0)
            {
                return false;
            }

            InitializeShopPool(runState);
            return GetShopPoolRemain(runState, unitId) >= count;
        }

        public bool TryTakeDiscoveredCardFromPool(RunState runState, UnitCardState card)
        {
            if (runState == null || card == null || string.IsNullOrWhiteSpace(card.unitId))
            {
                return false;
            }

            InitializeShopPool(runState);
            var cost = GetShopCardPoolCost(card);
            if (cost <= 0 || GetShopPoolRemain(runState, card.unitId) < cost)
            {
                return false;
            }

            AdjustShopPoolRemain(runState, card.unitId, -cost);
            card.shopPoolCost = cost;
            card.shopPoolReserved = false;
            card.shopPoolContribution = cost;
            card.fromShopPurchase = false;
            return true;
        }

        private void InitializeShopPool(RunState runState)
        {
            if (runState == null)
            {
                return;
            }

            if (runState.shopPool == null)
            {
                runState.shopPool = new List<ShopPoolEntryState>();
            }

            foreach (var unit in ProphecyGameSession.Instance.Data.Units)
            {
                if (unit == null || unit.hidden || string.IsNullOrWhiteSpace(unit.id))
                {
                    continue;
                }

                var limit = ResolveShopPoolLimit(unit);
                if (limit <= 0)
                {
                    continue;
                }

                var entry = FindShopPoolEntry(runState, unit.id);
                if (entry == null)
                {
                    runState.shopPool.Add(new ShopPoolEntryState
                    {
                        unitId = unit.id,
                        baseLimit = limit,
                        remain = limit
                    });
                    continue;
                }

                if (entry.baseLimit != limit)
                {
                    var checkedOut = Math.Max(0, entry.baseLimit - entry.remain);
                    entry.baseLimit = limit;
                    entry.remain = Clamp(limit - checkedOut, 0, limit);
                }
            }
        }

        private static int ResolveShopPoolLimit(UnitDefinition unit)
        {
            if (unit.limit > 0)
            {
                return unit.limit;
            }

            return unit.name != null && ShopLimitByUnitName.TryGetValue(unit.name, out var limit) ? limit : 0;
        }

        private static ShopPoolEntryState FindShopPoolEntry(RunState runState, string unitId)
        {
            return runState.shopPool.FirstOrDefault(entry => entry.unitId == unitId);
        }

        private static int GetShopPoolRemain(RunState runState, string unitId)
        {
            var entry = FindShopPoolEntry(runState, unitId);
            return entry == null ? 0 : Math.Max(0, entry.remain);
        }

        private static void AdjustShopPoolRemain(RunState runState, string unitId, int delta)
        {
            if (runState == null)
            {
                return;
            }

            var entry = FindShopPoolEntry(runState, unitId);
            if (entry == null)
            {
                return;
            }

            entry.remain = Clamp(entry.remain + delta, 0, entry.baseLimit);
        }

        private static bool IsAvailableForShop(RunState runState, UnitDefinition unit, int maxStar)
        {
            return unit != null
                && !unit.hidden
                && unit.star <= maxStar
                && unit.id != "light_illusion"
                && GetShopPoolRemain(runState, unit.id) >= 1;
        }

        private static int GetShopCardPoolCost(UnitCardState card)
        {
            if (card == null)
            {
                return 0;
            }

            return card.shopPoolCost > 0 ? card.shopPoolCost : card.isGolden ? 3 : 1;
        }

        private static bool ReserveShopCardPool(RunState runState, UnitCardState card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.unitId) || card.shopPoolReserved)
            {
                return true;
            }

            var cost = GetShopCardPoolCost(card);
            if (cost <= 0)
            {
                card.shopPoolReserved = true;
                return true;
            }

            if (GetShopPoolRemain(runState, card.unitId) < cost)
            {
                return false;
            }

            AdjustShopPoolRemain(runState, card.unitId, -cost);
            card.shopPoolCost = cost;
            card.shopPoolReserved = true;
            return true;
        }

        private static void ReleaseShopCardPool(RunState runState, UnitCardState card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.unitId) || !card.shopPoolReserved)
            {
                return;
            }

            card.shopPoolReserved = false;
            AdjustShopPoolRemain(runState, card.unitId, GetShopCardPoolCost(card));
        }

        private static void ReleaseShopCardReservations(RunState runState, IEnumerable<UnitCardState> cards)
        {
            foreach (var card in cards)
            {
                ReleaseShopCardPool(runState, card);
            }
        }

        private UnitCardState CreateRandomShopCard(RunState runState, IReadOnlyList<UnitDefinition> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            for (var attempt = 0; attempt < pool.Count * 2; attempt += 1)
            {
                var unit = pool[_random.Next(pool.Count)];
                if (GetShopPoolRemain(runState, unit.id) < 1)
                {
                    continue;
                }

                var countBonus = Math.Max(0, runState.manageResources?.shopGeneratedCountBonus ?? 0);
                var card = new UnitCardState
                {
                    unitId = unit.id,
                    name = unit.name,
                    star = unit.star,
                    baseCount = ResolveStartCount(unit) + countBonus,
                    maxCount = 0,
                    shopPoolCost = 1,
                    shopBuffAttack = Math.Max(0, runState.manageResources?.shopGeneratedBuffAttack ?? 0),
                    shopPoolReserved = false
                };

                return ReserveShopCardPool(runState, card) ? card : null;
            }

            return null;
        }

        private static int ResolveShopSlots(IReadOnlyList<int> slots, int shopLevel)
        {
            if (slots == null || slots.Count == 0)
            {
                return 3;
            }

            var clampedIndex = Clamp(shopLevel - 1, 0, slots.Count - 1);
            return slots[clampedIndex];
        }

        private static int ResolveShopMaxStar(IReadOnlyList<int> stars, int shopLevel)
        {
            if (stars == null || stars.Count == 0)
            {
                return 1;
            }

            var clampedIndex = Clamp(shopLevel - 1, 0, stars.Count - 1);
            return stars[clampedIndex];
        }

        private static int ResolveStartCount(UnitDefinition unit)
        {
            if (unit == null)
            {
                return 1;
            }

            return Math.Max(1, unit.defaultCount > 0 ? unit.defaultCount : unit.startCount > 0 ? unit.startCount : unit.baseCount > 0 ? unit.baseCount : 1);
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
