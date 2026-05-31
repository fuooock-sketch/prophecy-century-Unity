using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class HeroDefinition
    {
        public string id;
        public string name;
        public string title;
        public string epithet;
        public string faction;
        public string portrait_glyph;
        public string portrait_icon;
        public int threshold;
        public int battle_start_attack;
        public string primary_label;
        public string secondary_label;
        public string passive_text;
        public string active_text;
        public string short_text;
        public string story;
    }
}
