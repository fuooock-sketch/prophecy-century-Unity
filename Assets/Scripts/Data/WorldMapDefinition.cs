using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class WorldMapDefinition
    {
        public string id;
        public string name;
        public string startNodeId;
        public WorldMapLayerDefinition[] layers;
        public WorldMapNodeDefinition[] nodes;
        public WorldMapConnectionDefinition[] connections;
    }

    [Serializable]
    public sealed class WorldMapLayerDefinition
    {
        public int index;
        public string name;
    }

    [Serializable]
    public sealed class WorldMapNodeDefinition
    {
        public string id;
        public string name;
        public int layer;
        public string type;
        public string enemyPresetId;
        public WorldMapRewardDefinition reward;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class WorldMapConnectionDefinition
    {
        public string fromNodeId;
        public string toNodeId;
    }

    [Serializable]
    public sealed class WorldMapRewardDefinition
    {
        public int gold;
        public string treasureId;
        public string unitId;
    }
}
