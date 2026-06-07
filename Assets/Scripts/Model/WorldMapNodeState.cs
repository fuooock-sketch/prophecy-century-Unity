using System;

namespace ProphecyCentury.Model
{
    [Serializable]
    public sealed class WorldMapNodeState
    {
        public string nodeId;
        public bool isVisible;
        public bool isVisited;
        public bool isCleared;
    }
}
