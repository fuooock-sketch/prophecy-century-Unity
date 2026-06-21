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
        public int startCount;
        public int defaultCount;
        public int baseCount;
        public int maxCount;
        public int hpPerUnit;
        public int attack;
        public int defense;
        public int power;
        public int damageMin;
        public int damageMax;
        public int initiative;
        public int speed;
        public int luck;
        public int morale;
        public float attackInterval;
        public float range;
        public float attackRange;
        public int size;
        public int firstPurchaseHp;
        public int firstPurchaseAverageDamage;
        public int designOnlyInitialBaseDamage;
        public string[] tags;
        public string skillText;
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
        public string type;
        public string entryRace;
        public string targetTag;
        public string deadTag;
        public string allyId;
        public string unitId;
        public string targetUnitId;
        public string targetId;
        public string mode;
        public string targetMode;
        public int attack;
        public int defense;
        public int hp;
        public int selfHpLoss;
        public int damage;
        public int power;
        public int speed;
        public int morale;
        public int valuePerFaith;
        public int hitThreshold;
        public int nextRoundAttack;
        public int nextRoundCount;
        public int giftThreshold;
        public int roundOffset;
        public int count;
        public int repeat;
        public int times;
        public int layers;
        public int targets;
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
        public float chance;
        public float duration;
        public float refreshSeconds;
        public int refreshRounds;
        public float delay;
        public float interval;
        public int intervalRounds;
        public float reduce;
        public float ratio;
        public float attackMultiplier;
        public float deathAttackMultiplier;
        public float critMultiplier;
        public float percent;
        public float distance;
        public float stunSeconds;
        public int stunTurns;
        public int moveLockTurns;
        public float invincibleSeconds;
        public float speedMultiplier;
        public float radius;
        public float tick;
        public string summonUnitId;
        public string transformUnitId;
        public string[] targetTags;
        public string[] excludeUnitIds;
        public StatDeltaDefinition stats;
        public bool oncePerBattle;
        public bool forceCrit;
        public bool temporary;
        public bool disableAttack;
        public bool byFaith;
        public bool allowEvents;
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
