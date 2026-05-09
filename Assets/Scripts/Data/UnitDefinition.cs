using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class UnitDefinition
    {
        public string id;
        public string name;
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
        public string faith;
        public bool oncePerBattle;
    }
}
