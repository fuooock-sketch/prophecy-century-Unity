using System;

namespace ProphecyCentury.Model
{
    [Serializable]
    public sealed class InventoryItemState
    {
        public string itemId;
        public int count;
        public string sourceNodeId;
        public int acquiredDay;
    }
}
