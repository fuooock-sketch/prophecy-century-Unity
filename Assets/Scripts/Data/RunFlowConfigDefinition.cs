using System;

namespace ProphecyCentury.Data
{
    [Serializable]
    public sealed class RunFlowConfigDefinition
    {
        public RunFlowPhaseDefinition[] phases;
        public RunFlowTriggerDefinition[] triggers;
    }

    [Serializable]
    public sealed class RunFlowPhaseDefinition
    {
        public string phase;
        public string state;
        public string displayName;
        public string playerAction;
        public string enterCondition;
        public string exitCondition;
        public string engineeringEntry;
        public string notes;
    }

    [Serializable]
    public sealed class RunFlowTriggerDefinition
    {
        public string id;
        public string name;
        public string phase;
        public string timing;
        public string condition;
        public string effectSummary;
        public string engineeringEntry;
        public bool configurableNow;
        public string notes;
    }
}
