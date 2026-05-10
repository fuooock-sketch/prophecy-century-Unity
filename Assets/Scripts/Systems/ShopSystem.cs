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
        private const int HandMaxCount = 10;
        private readonly Random _random = new Random();
        private static readonly Dictionary<string, int> ShopLimitByUnitName = new Dictionary<string, int>
        {
            { "冰霜魔灵", 12 }, { "刺客", 8 }, { "打手", 12 }, { "大魔灵", 3 },
            { "低级元素使", 8 }, { "飞毯法师", 5 }, { "风元素", 5 }, { "高翎守望者", 8 },
            { "格尔步兵", 12 }, { "格尔巨兽", 3 }, { "格尔军官", 3 }, { "格尔兽", 12 },
            { "弓箭手", 8 }, { "光明导师", 5 }, { "光明武士", 12 }, { "河边队长", 8 },
            { "幻影射手", 5 }, { "皇家剑士", 3 }, { "火元素", 12 }, { "机警后援", 8 },
            { "犟嘴学徒", 12 }, { "叫唤者", 12 }, { "精灵", 12 }, { "精锐游骑兵", 12 },
            { "酒鬼", 3 }, { "掘地鼠", 5 }, { "苦工", 12 }, { "苦嚎叫兽", 3 },
            { "傀儡魔灵", 5 }, { "莱特的回响", 3 }, { "莱特使者", 3 }, { "劣徒", 8 },
            { "猎豹", 3 }, { "林地将军", 3 }, { "林地密探", 12 }, { "林地卫兵", 12 },
            { "流浪者", 8 }, { "蘑菇夸库", 12 }, { "魔导师", 3 }, { "魔法龙", 3 },
            { "魔尊", 3 }, { "牧师", 8 }, { "骑士", 12 }, { "驱魔师坐骑", 5 },
            { "僧侣", 12 }, { "神剑游侠", 8 }, { "兽骑兵", 5 }, { "双塔术士", 3 },
            { "水元素", 8 }, { "铁匠", 12 }, { "痛苦火苗", 5 }, { "土元素", 5 },
            { "卫戍协兵", 3 }, { "巫兽师", 3 }, { "无魔者", 5 }, { "武学大师", 5 },
            { "席林迪翁", 3 }, { "小商人", 2 }, { "邪恶女巫", 3 }, { "学院园丁", 5 },
            { "雪狮", 8 }, { "血淤魔", 3 }, { "驯兽师", 8 }, { "阴暗屠夫", 3 },
            { "佣兵队长", 5 }, { "游骑兵", 12 }, { "游侠", 12 }, { "淤魔", 3 },
            { "鱼人奴仆", 12 }, { "羽卫", 5 }, { "元素大师", 3 }
        };

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
            var session = ProphecyGameSession.Instance;
            var config = session.Data.Config;
            var slots = ResolveShopSlots(config?.shopSlots, runState.shopLevel);
            var maxStar = ResolveShopMaxStar(config?.shopMaxStar, runState.shopLevel);
            ReleaseShopCardReservations(runState.shopCards);
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

            ReleaseShopCardPool(card);
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

            AdjustShopPoolRemain(unit.unitId, unit.shopPoolContribution);
            unit.shopPoolContribution = 0;
            unit.fromShopPurchase = false;
        }

        private void InitializeShopPool(RunState runState)
        {
            if (runState.shopPool.Count > 0)
            {
                return;
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

                runState.shopPool.Add(new ShopPoolEntryState
                {
                    unitId = unit.id,
                    baseLimit = limit,
                    remain = limit
                });
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
            return entry == null ? int.MaxValue : Math.Max(0, entry.remain);
        }

        private static void AdjustShopPoolRemain(string unitId, int delta)
        {
            var runState = ProphecyGameSession.Instance.CurrentRun;
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

            AdjustShopPoolRemain(card.unitId, -cost);
            card.shopPoolCost = cost;
            card.shopPoolReserved = true;
            return true;
        }

        private static void ReleaseShopCardPool(UnitCardState card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.unitId) || !card.shopPoolReserved)
            {
                return;
            }

            card.shopPoolReserved = false;
            AdjustShopPoolRemain(card.unitId, GetShopCardPoolCost(card));
        }

        private static void ReleaseShopCardReservations(IEnumerable<UnitCardState> cards)
        {
            foreach (var card in cards)
            {
                ReleaseShopCardPool(card);
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

                var card = new UnitCardState
                {
                    unitId = unit.id,
                    name = unit.name,
                    star = unit.star,
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
