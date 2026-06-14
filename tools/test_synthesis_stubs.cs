using System.Collections.Generic;
using System.Linq;

namespace ProphecyCentury.Core
{
    public sealed class ProphecyGameSession
    {
        public static ProphecyGameSession Instance { get; } = new ProphecyGameSession();
        public TestGameDataRepository Data { get; } = new TestGameDataRepository();
    }

    public sealed class TestGameDataRepository
    {
        public ProphecyCentury.Data.GameConfigData Config { get; } = new ProphecyCentury.Data.GameConfigData { synthesizeCount = 3 };
        public List<ProphecyCentury.Data.UnitDefinition> Units { get; } = new List<ProphecyCentury.Data.UnitDefinition>();

        public ProphecyCentury.Data.UnitDefinition FindUnit(string id)
        {
            return Units.FirstOrDefault(unit => unit != null && unit.id == id);
        }
    }
}

namespace ProphecyCentury.Data
{
    public sealed class GameConfigData
    {
        public int synthesizeCount;
    }

    public sealed class UnitDefinition
    {
        public string id;
        public string name;
        public int star;
        public int hp;
        public int attack;
        public int defense;
        public int power;
        public int speed;
        public int luck;
        public int morale;
        public int defaultCount;
        public int startCount;
        public int baseCount;
    }
}

namespace ProphecyCentury.Model
{
    public class UnitCardState
    {
        public string unitId;
        public string name;
        public int star;
        public bool isGolden;
        public int shopPoolCost;
        public int shopPoolContribution;
        public bool fromShopPurchase;
        public int shopBuffHp;
        public int shopBuffAttack;
        public int shopBuffDefense;
        public int shopBuffPower;
        public int shopBuffSpeed;
        public int shopBuffLuck;
        public int shopBuffMorale;
        public int baseCount;
        public int maxCount;
        public int forestGemCount;
        public int forestGemsAttached;
        public int forestGemsReceived;
        public int roundTempCount;
        public int manageRoundEntryEffectTriggerCount;
        public bool manageRoundStatRetriggerTriggered;
        public int manageGiftActionBucket;
        public int manageAttackGainBucket;
        public int manageReceiveGiftPowerBucket;
        public bool manageReceiveGiftDiscoverTriggered;
        public bool manageRoundAttackRewardTriggered;
        public List<BattleProgressCounterState> battleProgressCounters = new List<BattleProgressCounterState>();
    }

    public sealed class BoardUnitState : UnitCardState
    {
        public string boardSlotId;
    }

    public sealed class BattleProgressCounterState
    {
        public string key;
        public int value;
    }

    public sealed class RunState
    {
        public List<UnitCardState> handCards = new List<UnitCardState>();
        public List<BoardUnitState> boardUnits = new List<BoardUnitState>();
    }
}

namespace ProphecyCentury.Systems
{
    public static class ManageEventResolver
    {
        public static bool IsForestGemCard(ProphecyCentury.Model.UnitCardState card)
        {
            return card != null && card.unitId == "forest_gem";
        }
    }
}
