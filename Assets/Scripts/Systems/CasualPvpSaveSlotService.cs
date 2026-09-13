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
    public sealed class CasualPvpSaveSlotService
    {
        public const int SlotCount = 3;
        private const string ActiveSlotKey = "ProphecyCentury.CasualPvp.ActiveSaveSlot";
        private const string DirectoryName = "CasualPvpSaves";
        private readonly string _saveDirectoryOverride;
        private readonly bool _persistActiveSlot;

        public string SaveDirectory => string.IsNullOrWhiteSpace(_saveDirectoryOverride)
            ? Path.Combine(Application.persistentDataPath, DirectoryName)
            : _saveDirectoryOverride;
        public int CurrentSaveSlotIndex { get; private set; } = -1;

        public CasualPvpSaveSlotService(string saveDirectoryOverride = null, bool persistActiveSlot = true)
        {
            _saveDirectoryOverride = saveDirectoryOverride;
            _persistActiveSlot = persistActiveSlot;
            CurrentSaveSlotIndex = persistActiveSlot ? PlayerPrefs.GetInt(ActiveSlotKey, -1) : -1;
            if (!IsSlotIndex(CurrentSaveSlotIndex)) CurrentSaveSlotIndex = -1;
        }

        public string GetSlotPath(int slotIndex) => Path.Combine(SaveDirectory, $"casual_slot_{slotIndex}.json");

        public IReadOnlyList<SaveSlotInfo> GetAllSlots()
        {
            var slots = new List<SaveSlotInfo>(SlotCount);
            for (var index = 1; index <= SlotCount; index += 1) slots.Add(GetSlot(index));
            return slots;
        }

        public SaveSlotInfo GetSlot(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return new SaveSlotInfo { SlotIndex = slotIndex, State = SaveSlotState.Corrupted, Error = "休闲对战存档槽编号无效。" };
            if (!File.Exists(GetSlotPath(slotIndex))) return new SaveSlotInfo { SlotIndex = slotIndex, State = SaveSlotState.Empty };
            var result = ReadAndValidate(slotIndex);
            return new SaveSlotInfo
            {
                SlotIndex = slotIndex,
                State = result.Success ? SaveSlotState.Valid : SaveSlotState.Corrupted,
                Metadata = result.Save?.metadata,
                Error = result.Error
            };
        }

        public bool HasAnyValidSave() => GetAllSlots().Any(slot => slot.State == SaveSlotState.Valid);

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
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("休闲对战存档槽编号无效。");
            if (GetSlot(slotIndex).State != SaveSlotState.Empty) return SaveOperationResult.Fail("该槽位不是空白存档。");
            // Reserve the independent slot first. The actual run is created and saved
            // only after the player confirms a hero, so backing out cannot leave a
            // playable save with an implicitly selected default hero.
            SetCurrentSlot(slotIndex);
            return SaveOperationResult.Ok();
        }

        public SaveOperationResult SaveCurrentRun()
        {
            if (!IsSlotIndex(CurrentSaveSlotIndex)) return SaveOperationResult.Fail("尚未选择休闲对战存档槽。");
            var run = ProphecyGameSession.Instance?.CurrentRun;
            if (!CasualPvpSystem.IsCasual(run)) return SaveOperationResult.Fail("当前不是休闲对战进度。");
            var previous = ReadAndValidate(CurrentSaveSlotIndex);
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return WriteSlot(CurrentSaveSlotIndex, run, previous.Save?.metadata?.createdAtUtc ?? now, now,
                previous.Save?.metadata?.saveId ?? Guid.NewGuid().ToString("N"), previous.Save?.metadata?.totalPlaySeconds ?? 0f);
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
            var latest = GetMostRecentlyPlayedSave();
            return latest == null ? SaveOperationResult.Fail("没有休闲对战存档。") : LoadGame(latest.SlotIndex);
        }

        public SaveOperationResult DeleteSave(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("休闲对战存档槽编号无效。");
            try
            {
                var path = GetSlotPath(slotIndex);
                if (File.Exists(path)) File.Delete(path);
                if (CurrentSaveSlotIndex == slotIndex) SetCurrentSlot(-1);
                return SaveOperationResult.Ok();
            }
            catch (Exception ex)
            {
                return SaveOperationResult.Fail("删除休闲对战存档失败：" + ex.Message);
            }
        }

        private SaveOperationResult ReadAndValidate(int slotIndex)
        {
            if (!IsSlotIndex(slotIndex)) return SaveOperationResult.Fail("休闲对战存档槽编号无效。");
            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path)) return SaveOperationResult.Fail("存档文件不存在。");
            try
            {
                var save = JsonUtility.FromJson<SaveSlotFile>(File.ReadAllText(path));
                if (save?.metadata == null || save.gameData == null) return SaveOperationResult.Fail("存档内容不完整。");
                if (save.formatVersion <= 0 || save.formatVersion > SaveSlotService.CurrentFormatVersion) return SaveOperationResult.Fail("存档版本不兼容。");
                if (save.metadata.slotIndex != slotIndex || string.IsNullOrWhiteSpace(save.metadata.saveId)) return SaveOperationResult.Fail("存档标识无效。");
                if (!CasualPvpSystem.IsCasual(save.gameData)) return SaveOperationResult.Fail("该文件不是休闲对战存档。");
                if (!TryParseUtc(save.metadata.createdAtUtc, out _) || !TryParseUtc(save.metadata.lastSavedAtUtc, out _)) return SaveOperationResult.Fail("存档时间格式无效。");
                return SaveOperationResult.Ok(save);
            }
            catch (Exception ex)
            {
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
                    formatVersion = SaveSlotService.CurrentFormatVersion,
                    metadata = BuildMetadata(slotIndex, run, createdAt, savedAt, saveId, totalSeconds),
                    gameData = run
                };
                var path = GetSlotPath(slotIndex);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(save, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return SaveOperationResult.Ok(save);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Casual PVP save failed: {ex.Message}");
                return SaveOperationResult.Fail("写入休闲对战存档失败：" + ex.Message);
            }
        }

        private static SaveSlotMetadata BuildMetadata(int slotIndex, RunState run, string createdAt, string savedAt, string saveId, float totalSeconds)
        {
            var phase = run.casualPvpEndless ? "无尽阶段" : $"第 {Math.Max(1, run.round)} / {CasualPvpSystem.SurvivalMilestoneRound} 回合";
            return new SaveSlotMetadata
            {
                slotIndex = slotIndex,
                saveId = saveId,
                createdAtUtc = createdAt,
                lastSavedAtUtc = savedAt,
                totalPlaySeconds = Mathf.Max(0f, totalSeconds),
                gameVersion = Application.version,
                chapter = "休闲对战",
                mapOrLevel = phase,
                day = run.round,
                activeScene = SceneManager.GetActiveScene().name,
                summary = $"{phase} · 生命 {Math.Max(0, run.playerHp)} · {Math.Max(0, run.campaignWins)}胜{Math.Max(0, run.campaignLosses)}负"
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
        private static bool TryParseUtc(string value, out DateTime parsed) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed);
    }
}
