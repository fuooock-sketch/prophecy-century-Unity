using System;
using System.IO;
using ProphecyCentury.Model;
using ProphecyCentury.Systems;
using UnityEditor;
using UnityEngine;

public static class SaveSlotSelfTest
{
    [MenuItem("Tools/Prophecy Century/Run Save Slot Self Test")]
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "prophecy-century-save-slot-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new SaveSlotService(directory, false);
            Assert(service.GetAllSlots().Count == SaveSlotService.SlotCount, "must expose eight slots");
            Assert(service.GetSlot(1).State == SaveSlotState.Empty, "missing file must be Empty");
            Assert(!service.HasAnyValidSave(), "empty store must not enable Continue");

            Directory.CreateDirectory(directory);
            File.WriteAllText(service.GetSlotPath(3), "{ broken json");
            Assert(service.GetSlot(3).State == SaveSlotState.Corrupted, "invalid JSON must be Corrupted");
            Assert(!service.LoadGame(3).Success, "corrupted save must not load");
            Assert(service.DeleteSave(3).Success, "corrupted save must be deletable");
            Assert(service.GetSlot(3).State == SaveSlotState.Empty, "deleted slot must become Empty");

            WriteValidSlot(service, 2, "2026-01-01T00:00:00.0000000Z");
            WriteValidSlot(service, 1, "2026-01-02T00:00:00.0000000Z");
            Assert(service.HasAnyValidSave(), "valid save must enable Continue");
            Assert(service.GetMostRecentlyPlayedSave().SlotIndex == 1, "Continue must select newest save");
            Assert(service.DeleteSave(1).Success, "newest save must be deletable");
            Assert(service.GetMostRecentlyPlayedSave().SlotIndex == 2, "deleting newest must recalculate Continue target");
            Assert(service.DeleteSave(2).Success && !service.HasAnyValidSave(), "deleting last valid save must disable Continue");
            Debug.Log("Save slot self test passed.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void WriteValidSlot(SaveSlotService service, int slotIndex, string savedAt)
    {
        var file = new SaveSlotFile
        {
            formatVersion = SaveSlotService.CurrentFormatVersion,
            metadata = new SaveSlotMetadata
            {
                slotIndex = slotIndex,
                saveId = Guid.NewGuid().ToString("N"),
                createdAtUtc = savedAt,
                lastSavedAtUtc = savedAt,
                gameVersion = "test"
            },
            gameData = new RunState { saveVersion = 1, campaignId = "test", currentNodeId = "start" }
        };
        File.WriteAllText(service.GetSlotPath(slotIndex), JsonUtility.ToJson(file));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Save slot self test failed: " + message);
    }
}
