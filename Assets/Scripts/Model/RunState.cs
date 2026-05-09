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
        public bool isShopLocked;
        public string lastBattleSummary;
        public List<BoardUnitState> boardUnits = new List<BoardUnitState>();
        public List<UnitCardState> handCards = new List<UnitCardState>();
        public List<UnitCardState> shopCards = new List<UnitCardState>();
        public HeroRuntimeState heroState = new HeroRuntimeState();
        public ManageResourceState manageResources = new ManageResourceState();
    }

    [Serializable]
    public class UnitCardState
    {
        public string unitId;
        public string name;
        public int star;
        public bool isGolden;
    }

    [Serializable]
    public sealed class BoardUnitState : UnitCardState
    {
        public string boardSlotId;
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
}
