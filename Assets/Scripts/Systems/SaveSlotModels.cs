using System;
using ProphecyCentury.Model;

namespace ProphecyCentury.Systems
{
    public enum SaveSlotState
    {
        Empty,
        Valid,
        Corrupted
    }

    [Serializable]
    public sealed class SaveSlotMetadata
    {
        public int slotIndex;
        public string saveId;
        public string createdAtUtc;
        public string lastSavedAtUtc;
        public float totalPlaySeconds;
        public string gameVersion;
        public string chapter;
        public string mapOrLevel;
        public int day;
        public string activeScene;
        public string summary;
    }

    [Serializable]
    public sealed class SaveSlotFile
    {
        public int formatVersion = 1;
        public SaveSlotMetadata metadata;
        public RunState gameData;
    }

    public sealed class SaveSlotInfo
    {
        public int SlotIndex { get; internal set; }
        public SaveSlotState State { get; internal set; }
        public SaveSlotMetadata Metadata { get; internal set; }
        public string Error { get; internal set; }
    }

    public sealed class SaveOperationResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        public SaveSlotFile Save { get; private set; }

        public static SaveOperationResult Ok(SaveSlotFile save = null)
        {
            return new SaveOperationResult { Success = true, Save = save };
        }

        public static SaveOperationResult Fail(string error)
        {
            return new SaveOperationResult { Success = false, Error = error };
        }
    }
}
