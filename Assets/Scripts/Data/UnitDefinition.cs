using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class UnitDefinition
    {
        public string id;
        public string name;
        public bool hidden;
        public int limit;
        public int star;
        public string race;
        public string faith;
        public string type;
        public string typeLabel;
        public int hp;
        public int attack;
        public int defense;
        public int power;
        public int speed;
        public int luck;
        public int morale;
        public float attackInterval;
        public float range;
        public int size;
        public string[] tags;
        public string talentText;
        public string goldTalentText;
        public string battleText;
        public string goldBattleText;
        public SkillDefinition[] talents;
        public SkillDefinition[] goldTalents;
        public SkillDefinition[] battleSkills;
        public SkillDefinition[] goldBattleSkills;
        public int sizeTier;
    }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string kind;
        public int value;
        public int threshold;
        public int price;
        public string faith;
        public string race;
        public string tag;
        public string entryRace;
        public string targetTag;
        public string unitId;
        public string targetUnitId;
        public string mode;
        public string targetMode;
        public int attack;
        public int defense;
        public int hp;
        public int power;
        public int speed;
        public int morale;
        public int count;
        public int times;
        public int gift;
        public int gain;
        public int multiplier;
        public int attackValue;
        public int defenseValue;
        public int goldValue;
        public int goldAttack;
        public int goldDefense;
        public int goldPower;
        public int goldCount;
        public int goldTimes;
        public int goldGift;
        public int goldGain;
        public int goldAttackValue;
        public int goldDefenseValue;
        public string[] targetTags;
        public string[] excludeUnitIds;
        public StatDeltaDefinition stats;
        public bool oncePerBattle;
    }

    [Serializable]
    public sealed class StatDeltaDefinition
    {
        public int hp;
        public int attack;
        public int defense;
        public int power;
        public int speed;
        public int luck;
        public int morale;
    }
}
