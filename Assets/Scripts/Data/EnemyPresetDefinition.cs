using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class EnemyPresetDefinition
    {
        public string id;
        public string name;
        public string type;
        public EnemyPresetUnitDefinition[] units;
    }

    [Serializable]
    public sealed class EnemyPresetUnitDefinition
    {
        public string unitId;
        public int count;
        public int star;
        public string slotId;
    }
}
