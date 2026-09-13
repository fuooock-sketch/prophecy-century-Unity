using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProphecyCentury.Systems
{
    public enum LeaderboardKind
    {
        Campaign,
        CasualPvp
    }

    [Serializable]
    public sealed class LeaderboardRecord
    {
        public string playerName;
        public int score;
        public string completedAtUtc;
    }

    [Serializable]
    public sealed class LeaderboardStore
    {
        public List<LeaderboardRecord> campaign = new List<LeaderboardRecord>();
        public List<LeaderboardRecord> casualPvp = new List<LeaderboardRecord>();
    }

    public static class LeaderboardSystem
    {
        private const string StoreFileName = "prophecy_century_leaderboard.json";
        private const string DefaultPlayerName = "预言者";
        private const int MaxRecordsPerBoard = 100;

        private static string StorePath => Path.Combine(Application.persistentDataPath, StoreFileName);

        public static LeaderboardRecord Submit(LeaderboardKind kind, string playerName, int score)
        {
            var store = LoadStore();
            var list = GetList(store, kind);
            var record = new LeaderboardRecord
            {
                playerName = SanitizePlayerName(playerName),
                score = Math.Max(0, score),
                completedAtUtc = DateTime.UtcNow.ToString("O")
            };

            list.Add(record);
            SortAndTrim(list);
            SaveStore(store);
            return record;
        }

        public static IReadOnlyList<LeaderboardRecord> Load(LeaderboardKind kind)
        {
            var list = GetList(LoadStore(), kind);
            SortAndTrim(list);
            return list;
        }

        public static string FormatBoard(LeaderboardKind kind, int maxRows = 10)
        {
            var records = Load(kind).Take(Math.Max(1, maxRows)).ToList();
            if (records.Count == 0) return "暂无记录";
            return string.Join("\n", records.Select((record, index) =>
                $"{index + 1}. {record.playerName}    {record.score}"));
        }

        private static string SanitizePlayerName(string playerName)
        {
            var value = string.IsNullOrWhiteSpace(playerName) ? DefaultPlayerName : playerName.Trim();
            return value.Length <= 12 ? value : value.Substring(0, 12);
        }

        private static List<LeaderboardRecord> GetList(LeaderboardStore store, LeaderboardKind kind)
        {
            if (store.campaign == null) store.campaign = new List<LeaderboardRecord>();
            if (store.casualPvp == null) store.casualPvp = new List<LeaderboardRecord>();
            return kind == LeaderboardKind.Campaign ? store.campaign : store.casualPvp;
        }

        private static LeaderboardStore LoadStore()
        {
            try
            {
                if (!File.Exists(StorePath)) return new LeaderboardStore();
                var json = File.ReadAllText(StorePath);
                var store = JsonUtility.FromJson<LeaderboardStore>(json);
                return store ?? new LeaderboardStore();
            }
            catch
            {
                return new LeaderboardStore();
            }
        }

        private static void SaveStore(LeaderboardStore store)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath, JsonUtility.ToJson(store, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Leaderboard] Save failed: {ex.Message}");
            }
        }

        private static void SortAndTrim(List<LeaderboardRecord> records)
        {
            if (records == null) return;
            records.Sort((left, right) =>
            {
                var scoreCompare = Math.Max(0, right?.score ?? 0).CompareTo(Math.Max(0, left?.score ?? 0));
                if (scoreCompare != 0) return scoreCompare;
                return string.CompareOrdinal(right?.completedAtUtc ?? string.Empty, left?.completedAtUtc ?? string.Empty);
            });
            if (records.Count > MaxRecordsPerBoard)
            {
                records.RemoveRange(MaxRecordsPerBoard, records.Count - MaxRecordsPerBoard);
            }
        }
    }
}
