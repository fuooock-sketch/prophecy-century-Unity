using System;
using System.Collections.Generic;

namespace ProphecyCentury.Model
{
    [Serializable]
    public sealed class RunState
    {
        public string campaignId;
        public string heroId;
        public string state;
        public int gold;
        public int round;
        public int playerHp;
        public int shopLevel;
        public int shopUpgradeAnchorRound;
        public int campaignRoundLimit;
        public int campaignWins;
        public int campaignLosses;
        public bool campaignCompleted;
        public bool isShopLocked;
        public string lastBattleSummary;
        public List<BattleHistoryEntryState> battleHistory = new List<BattleHistoryEntryState>();
        public List<BoardUnitState> boardUnits = new List<BoardUnitState>();
        public List<UnitCardState> handCards = new List<UnitCardState>();
        public List<UnitCardState> shopCards = new List<UnitCardState>();
        public List<ShopPoolEntryState> shopPool = new List<ShopPoolEntryState>();
        public HeroRuntimeState heroState = new HeroRuntimeState();
        public ManageResourceState manageResources = new ManageResourceState();
        public BattleRewardState pendingBattleRewards = new BattleRewardState();
    }

    [Serializable]
    public class UnitCardState
    {
        public string unitId;
        public string name;
        public int star;
        public bool isGolden;
        public int shopPoolCost;
        public bool shopPoolReserved;
        public int shopPoolContribution;
        public bool fromShopPurchase;
        public int shopBuffHp;
        public int shopBuffAttack;
        public int shopBuffDefense;
        public int shopBuffPower;
        public int shopBuffSpeed;
        public int shopBuffLuck;
        public int shopBuffMorale;
        public int roundTempAttack;
        public int roundTempPower;
        public int roundTempMorale;
        public int forestGemsAttached;
        public int forestGemsReceived;
        public int manageEntryEffectTriggerCount;
        public int manageGiftActionBucket;
        public int manageAttackGainBucket;
        public int manageReceiveGiftPowerBucket;
        public bool manageReceiveGiftDiscoverTriggered;
        public bool manageRoundAttackRewardTriggered;
        public int pendingNextRoundTempAttack;
        public int pendingNextRoundTempPower;
        public int pendingNextRoundPermanentHp;
        public int pendingNextRoundPermanentPower;
        public int pendingNextRoundPermanentLuck;
        public int pendingNextRoundForestGems;
        public string pendingNextRoundEvolveTo;
        public List<BattleProgressCounterState> battleProgressCounters = new List<BattleProgressCounterState>();
    }

    [Serializable]
    public sealed class BoardUnitState : UnitCardState
    {
        public string boardSlotId;
    }

    [Serializable]
    public sealed class DevourShopEventState
    {
        public int shopIndex;
        public string devourerSlotId;
        public string devourerUnitId;
        public string devourerName;
        public UnitCardState devouredCard;
    }

    [Serializable]
    public sealed class ForestGemGiftEventState
    {
        public string sourceSlotId;
        public string sourceName;
        public string targetSlotId;
        public string targetName;
        public int amount;
    }

    [Serializable]
    public sealed class UnitEvolveEventState
    {
        public string slotId;
        public string oldName;
        public string newName;
    }

    [Serializable]
    public sealed class ShopBuffEventState
    {
        public string sourceSlotId;
        public string sourceName;
        public int attack;
        public List<int> shopIndices = new List<int>();
    }

    [Serializable]
    public sealed class ManageFeedbackEventsState
    {
        public List<ForestGemGiftEventState> forestGemGiftEvents = new List<ForestGemGiftEventState>();
        public List<UnitEvolveEventState> evolveEvents = new List<UnitEvolveEventState>();
        public List<ShopBuffEventState> shopBuffEvents = new List<ShopBuffEventState>();
    }

    [Serializable]
    public sealed class ShopPoolEntryState
    {
        public string unitId;
        public int baseLimit;
        public int remain;
    }

    [Serializable]
    public sealed class HeroRuntimeState
    {
        public int primaryResource;
        public int secondaryResource;
    }

    [Serializable]
    public sealed class ManageResourceState
    {
        public int forestGems;
        public int forestGiftTotal;
        public int forestGiftActions;
        public int forestGiftRoundTotal;
        public int forestGiftRoundActions;
        public int shopGeneratedBuffAttack;
    }

    [Serializable]
    public sealed class BattleRewardState
    {
        public int nextRoundGold;
        public int nextRoundShopBuffAttack;
        public List<DiscoverFaithRewardState> discoverFaithRewards = new List<DiscoverFaithRewardState>();
    }

    [Serializable]
    public sealed class DiscoverFaithRewardState
    {
        public string faith;
        public string race;
        public int count;
        public string label;
    }

    [Serializable]
    public sealed class BattleHistoryEntryState
    {
        public int round;
        public bool victory;
        public int playerScore;
        public int enemyScore;
        public int hpDelta;
        public int playerDamage;
        public int enemyDamage;
        public string summary;
    }

    [Serializable]
    public sealed class BattleProgressCounterState
    {
        public string key;
        public int value;
    }
}
