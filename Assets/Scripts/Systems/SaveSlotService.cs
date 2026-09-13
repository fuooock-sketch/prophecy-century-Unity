using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Model;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProphecyCentury.Systems
{
    public sealed class SaveSlotService
    {
        public const int SlotCount = 8;
        public const int CurrentFormatVersion = 1;
        private const string ActiveSlotKey = "ProphecyCentury.ActiveSaveSlot";
        private const string DirectoryName = "Saves";
        private readonly string _saveDirectoryOverride;
        private readonly bool _persistActiveSlot;

        public string SaveDirectory => string.IsNullOrWhiteSpace(_saveDirectoryOverride)
            ? Path.Combine(Application.persistentDataPath, DirectoryName)
            : _saveDirectoryOverride;
        public int CurrentSaveSlotIndex { get; private set; } = -1;

        public SaveSlotService(string saveDirectoryOverride = null, bool persistActiveSlot = true)
        {
            _saveDirectoryOverride = saveDirectoryOverride;
            _persistActiveSlot = persistActiveSlot;
            CurrentSaveSlotIndex = _persistActiveSlot ? PlayerPrefs.GetInt(ActiveSlotKey, -1) : -1;
            if (!IsSlotIndex(CurrentSaveSlotIndex)) CurrentSaveSlotIndex = -1;
        }

        public string GetSlotPath(int slotIndex)
        {
            return Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");
        }

        public IReadOnlyList<SaveSlotInfo> GetAllSlots()
        {
            var slots = new List<SaveSlotInfo>(SlotCount);
            for (var i = 1; i <= SlotCount; i += 1) slots.Add(GetSlot(i));
            return slots;
        }

        public SaveSlotInfo GetSlot(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex))
                return new SaveSlotInfo { SlotIndex = slotIndex, State = SaveSlotState.Corrupted, Error = "存档槽位编号无效。" };

            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path)) return new SaveSlotInfo { SlotIndex = slotIndex, State = SaveSlotState.Empty };

            var result = ReadAndValidate(slotIndex);
            return new SaveSlotInfo
            {
                SlotIndex = slotIndex,
                State = result.Success ? SaveSlotState.Valid : SaveSlotState.Corrupted,
                Metadata = result.Save?.metadata,
                Error = result.Error
            };
        }

        public bool HasAnyValidSave()
        {
            return GetAllSlots().Any(slot => slot.State == SaveSlotState.Valid);
        }

        public SaveSlotInfo GetMostRecentlyPlayedSave()
        {
            return GetAllSlots()
                .Where(slot => slot.State == SaveSlotState.Valid)
                .OrderByDescending(slot => ParseUtc(slot.Metadata?.lastSavedAtUtc))
                .ThenBy(slot => slot.SlotIndex)
                .FirstOrDefault();
        }

        public SaveOperationResult CreateNewGame(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("存档槽位编号无效。");
            if (GetSlot(slotIndex).State != SaveSlotState.Empty) return SaveOperationResult.Fail("该槽位不是空白存档，不能覆盖。");

            var session = ProphecyGameSession.EnsureInstance();
            session.StartNewRun();
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var result = WriteSlot(slotIndex, session.CurrentRun, now, now, Guid.NewGuid().ToString("N"), 0f);
            if (result.Success) SetCurrentSlot(slotIndex);
            return result;
        }

        public SaveOperationResult SaveCurrentRun()
        {
            if (!IsSlotIndex(CurrentSaveSlotIndex)) return SaveOperationResult.Fail("尚未选择活动存档槽位。");
            var run = ProphecyGameSession.Instance?.CurrentRun;
            if (run == null) return SaveOperationResult.Fail("当前没有可保存的游戏进度。");

            var previous = ReadAndValidate(CurrentSaveSlotIndex);
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return WriteSlot(
                CurrentSaveSlotIndex,
                run,
                previous.Save?.metadata?.createdAtUtc ?? now,
                now,
                previous.Save?.metadata?.saveId ?? Guid.NewGuid().ToString("N"),
                previous.Save?.metadata?.totalPlaySeconds ?? 0f);
        }

        public SaveOperationResult LoadGame(int slotIndex)
        {
            var result = ReadAndValidate(slotIndex);
            if (!result.Success) return result;
            SaveGameSystem.Normalize(result.Save.gameData);
            ProphecyGameSession.EnsureInstance().RestoreRun(result.Save.gameData);
            SetCurrentSlot(slotIndex);
            return result;
        }

        public SaveOperationResult LoadMostRecentValid()
        {
            var candidates = GetAllSlots()
                .Where(slot => slot.State == SaveSlotState.Valid)
                .OrderByDescending(slot => ParseUtc(slot.Metadata?.lastSavedAtUtc))
                .ThenBy(slot => slot.SlotIndex)
                .ToList();
            foreach (var candidate in candidates)
            {
                var result = LoadGame(candidate.SlotIndex);
                if (result.Success) return result;
                Debug.LogWarning($"Skipping unreadable save slot {candidate.SlotIndex}: {result.Error}");
            }
            return SaveOperationResult.Fail("没有可读取的有效存档。");
        }

        public SaveOperationResult DeleteSave(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("存档槽位编号无效。");
            try
            {
                var path = GetSlotPath(slotIndex);
                if (File.Exists(path)) File.Delete(path);
                if (CurrentSaveSlotIndex == slotIndex) SetCurrentSlot(-1);
                Debug.Log($"Deleted save slot {slotIndex}.");
                return SaveOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Delete save slot {slotIndex} failed: {ex.Message}");
                return SaveOperationResult.Fail("删除存档失败：" + ex.Message);
            }
        }

        public SaveOperationResult ImportLegacySave(string legacyPath)
        {
            var slots = GetAllSlots();
            if (slots.Any(slot => slot.State == SaveSlotState.Valid) || !File.Exists(legacyPath)) return SaveOperationResult.Ok();
            var targetSlot = slots.FirstOrDefault(slot => slot.State == SaveSlotState.Empty)?.SlotIndex ?? -1;
            if (targetSlot < 1) return SaveOperationResult.Fail("没有空白槽位可迁移旧存档。");
            try
            {
                var run = JsonUtility.FromJson<RunState>(File.ReadAllText(legacyPath));
                if (run == null) return SaveOperationResult.Fail("旧存档无法解析。");
                var timestamp = File.GetLastWriteTimeUtc(legacyPath).ToString("O", CultureInfo.InvariantCulture);
                var result = WriteSlot(targetSlot, run, timestamp, timestamp, Guid.NewGuid().ToString("N"), 0f);
                if (result.Success) Debug.Log($"Imported legacy save into slot {targetSlot}.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Legacy save import skipped: {ex.Message}");
                return SaveOperationResult.Fail(ex.Message);
            }
        }

        private SaveOperationResult ReadAndValidate(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("存档槽位编号无效。");
            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path)) return SaveOperationResult.Fail("存档文件不存在。");
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return SaveOperationResult.Fail("存档文件为空。");
                var save = JsonUtility.FromJson<SaveSlotFile>(json);
                if (save?.metadata == null || save.gameData == null) return SaveOperationResult.Fail("存档缺少元数据或游戏数据。");
                if (save.formatVersion <= 0 || save.formatVersion > CurrentFormatVersion) return SaveOperationResult.Fail("存档版本不兼容。");
                if (save.metadata.slotIndex != slotIndex || string.IsNullOrWhiteSpace(save.metadata.saveId)) return SaveOperationResult.Fail("存档标识或槽位编号无效。");
                if (!TryParseUtc(save.metadata.createdAtUtc, out _) || !TryParseUtc(save.metadata.lastSavedAtUtc, out _)) return SaveOperationResult.Fail("存档时间格式无效。");
                if (save.gameData.saveVersion < 0) return SaveOperationResult.Fail("游戏数据版本无效。");
                return SaveOperationResult.Ok(save);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Read save slot {slotIndex} failed: {ex.Message}");
                return SaveOperationResult.Fail("存档无法读取：" + ex.Message);
            }
        }

        private SaveOperationResult WriteSlot(int slotIndex, RunState run, string createdAt, string savedAt, string saveId, float totalSeconds)
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                var save = new SaveSlotFile
                {
                    formatVersion = CurrentFormatVersion,
                    metadata = BuildMetadata(slotIndex, run, createdAt, savedAt, saveId, totalSeconds),
                    gameData = run
                };
                var path = GetSlotPath(slotIndex);
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(save, true));
                if (File.Exists(path)) File.Replace(tempPath, path, null);
                else File.Move(tempPath, path);
                Debug.Log($"Saved slot {slotIndex}: {path}");
                return SaveOperationResult.Ok(save);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save slot {slotIndex} failed: {ex.Message}");
                return SaveOperationResult.Fail("写入存档失败：" + ex.Message);
            }
        }

        private static SaveSlotMetadata BuildMetadata(int slotIndex, RunState run, string createdAt, string savedAt, string saveId, float totalSeconds)
        {
            var scene = SceneManager.GetActiveScene().name;
            return new SaveSlotMetadata
            {
                slotIndex = slotIndex,
                saveId = saveId,
                createdAtUtc = createdAt,
                lastSavedAtUtc = savedAt,
                totalPlaySeconds = Mathf.Max(0f, totalSeconds),
                gameVersion = Application.version,
                chapter = string.IsNullOrWhiteSpace(run.campaignId) ? "未选择战役" : run.campaignId,
                mapOrLevel = string.IsNullOrWhiteSpace(run.currentNodeId) ? "start" : run.currentNodeId,
                day = run.dayCount,
                activeScene = scene,
                summary = $"第 {Mathf.Max(0, run.dayCount)} 天 · {run.currentNodeId ?? "start"}"
            };
        }

        private void SetCurrentSlot(int slotIndex)
        {
            CurrentSaveSlotIndex = IsSlotIndex(slotIndex) ? slotIndex : -1;
            if (!_persistActiveSlot) return;
            PlayerPrefs.SetInt(ActiveSlotKey, CurrentSaveSlotIndex);
            PlayerPrefs.Save();
        }

        private static bool IsSlotIndex(int slotIndex) => slotIndex >= 1 && slotIndex <= SlotCount;
        private static DateTime ParseUtc(string value) => TryParseUtc(value, out var parsed) ? parsed : DateTime.MinValue;
        private static bool TryParseUtc(string value, out DateTime parsed)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed);
        }
    }
}
