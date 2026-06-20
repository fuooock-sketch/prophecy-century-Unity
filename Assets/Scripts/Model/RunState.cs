using System;
using System.Collections.Generic;

namespace ProphecyCentury.Model
{
    public enum GamePhase
    {
        DayExplore,
        NightManage,
        Battle,
        Settle,
        Victory,
        GameOver
    }

    [Serializable]
    public sealed class RunState
    {
        public int saveVersion;
        public string campaignId;
        public string heroId;
        public string state;
        public GamePhase phase;
        public int gold;
        public int round;
        public int dayCount;
        public int remainingMovePoints;
        public int maxMovePoints;
        public string currentNodeId;
        public int playerHp;
        public int fateValue;
        public int maxFateValue;
        public int shopLevel;
        public int shopUpgradeAnchorRound;
        public int campaignRoundLimit;
        public int campaignWins;
        public int campaignLosses;
        public bool campaignCompleted;
        public bool isShopLocked;
        public string lastBattleSummary;
        public bool isExplorationBattle;
        public string explorationBattleNodeId;
        public string explorationBattleEnemyPresetId;
        public string explorationBattleNodeType;
        public List<BattleHistoryEntryState> battleHistory = new List<BattleHistoryEntryState>();
        public List<BoardUnitState> boardUnits = new List<BoardUnitState>();
        public List<UnitCardState> handCards = new List<UnitCardState>();
        public List<UnitCardState> pendingHandCards = new List<UnitCardState>();
        public List<UnitCardState> shopCards = new List<UnitCardState>();
        public List<ShopPoolEntryState> shopPool = new List<ShopPoolEntryState>();
        public List<WorldMapNodeState> worldMapNodes = new List<WorldMapNodeState>();
        public List<InventoryItemState> inventoryItems = new List<InventoryItemState>();
        public HeroRuntimeState heroState = new HeroRuntimeState();
        public ManageResourceState manageResources = new ManageResourceState();
        public BattleRewardState pendingBattleRewards = new BattleRewardState();
        public BattleUnitPickState pendingBattleUnitPick;
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
        public int boardAuraAttack;
        public int baseCount;
        public int maxCount;
        public int forestGemCount;
        public int roundTempCount;
        public int roundTempAttack;
        public int roundTempPower;
        public int roundTempMorale;
        public int forestGemsAttached;
        public int forestGemsReceived;
        public int manageEntryEffectTriggerCount;
        public int manageRoundEntryEffectTriggerCount;
        public int manageFaithCountGainBucket;
        public int manageRoundForestGemGiftBonusCount;
        public bool manageRoundStatRetriggerTriggered;
        public int manageGiftActionBucket;
        public int manageAttackGainBucket;
        public int manageReceiveGiftPowerBucket;
        public bool manageReceiveGiftDiscoverTriggered;
        public bool manageRoundAttackRewardTriggered;
        public int pendingNextRoundTempCount;
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
        public int gainedCount;
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
    public sealed class CountGainEventState
    {
        public string sourceSlotId;
        public string sourceName;
        public string targetSlotId;
        public string targetName;
        public int amount;
        public string label;
    }

    [Serializable]
    public sealed class EntryEffectEventState
    {
        public string targetSlotId;
        public string targetName;
    }

    [Serializable]
    public sealed class HandAddEventState
    {
        public string sourceSlotId;
        public string sourceName;
        public string unitId;
        public string unitName;
        public int handIndex;
    }

    [Serializable]
    public sealed class AttackChangeEventState
    {
        public string targetSlotId;
        public string targetName;
        public int amount;
    }

    [Serializable]
    public sealed class ShopBuffEventState
    {
        public string sourceSlotId;
        public string sourceName;
        public int attack;
        public int count;
        public List<int> shopIndices = new List<int>();
    }

    [Serializable]
    public sealed class ManageFeedbackEventsState
    {
        public List<ForestGemGiftEventState> forestGemGiftEvents = new List<ForestGemGiftEventState>();
        public List<UnitEvolveEventState> evolveEvents = new List<UnitEvolveEventState>();
        public List<CountGainEventState> countGainEvents = new List<CountGainEventState>();
        public List<EntryEffectEventState> entryEffectEvents = new List<EntryEffectEventState>();
        public List<HandAddEventState> handAddEvents = new List<HandAddEventState>();
        public List<AttackChangeEventState> attackChangeEvents = new List<AttackChangeEventState>();
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
        public int countGainProgress;
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
        public int shopGeneratedCountBonus;
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
    public sealed class BattleUnitPickState
    {
        public List<BattleUnitPickChoice> choices = new List<BattleUnitPickChoice>();
        public int remainingPicks;
        public int remainingRerolls;
        public int choiceStar;
    }

    [Serializable]
    public sealed class BattleUnitPickChoice
    {
        public string unitId;
        public string name;
        public int star;
        public bool selected;
    }

    [Serializable]
    public sealed class BattleProgressCounterState
    {
        public string key;
        public int value;
    }
}
