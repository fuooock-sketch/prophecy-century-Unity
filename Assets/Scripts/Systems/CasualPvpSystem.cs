using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProphecyCentury.Core;
using ProphecyCentury.Data;
using ProphecyCentury.Model;
using UnityEngine;

namespace ProphecyCentury.Systems
{
    public static class CasualPvpSystem
    {
        public const int SurvivalMilestoneRound = 15;
        public const string LockedState = "casual_locked";
        public const string CombatVersion = "battle_v2";
        private const string StoreFileName = "casual_pvp_mirror_pool.json";
        private const string LegacyLogFileName = "player_state_log.jsonl";
        private const int RecentOpponentLimit = 5;
        private const int MaximumStoredSnapshots = 5000;
        private static bool _legacyImportAttempted;

        private static string StorePath => Path.Combine(Application.persistentDataPath, StoreFileName);
        private static string LegacyLogPath => Path.Combine(Application.persistentDataPath, LegacyLogFileName);

        public static bool IsCasual(RunState run)
        {
            return run != null && string.Equals(run.gameMode, GameModeIds.CasualPvp, StringComparison.Ordinal);
        }

        public static void InitializeRun(RunState run)
        {
            if (run == null) return;
            run.gameMode = GameModeIds.CasualPvp;
            run.campaignId = GameModeIds.CasualPvp;
            run.campaignRoundLimit = 0;
            run.playerHp = 100;
            run.fateValue = 100;
            run.maxFateValue = 100;
            run.casualPvpRunId = string.IsNullOrWhiteSpace(run.casualPvpRunId)
                ? Guid.NewGuid().ToString("N")
                : run.casualPvpRunId;
            run.casualPvpMilestoneOffered = false;
            run.casualPvpEndless = false;
            run.casualPvpMatchId = null;
            run.casualPvpOpponent = null;
            run.casualPvpRoundEndResolvedRound = 0;
            run.casualPvpMatchEndpoint = null;
            run.casualPvpPendingReports = new List<CasualPvpPendingReport>();
            run.casualPvpPlayerSnapshot = null;
            run.casualPvpRecentOpponentIds = run.casualPvpRecentOpponentIds ?? new List<string>();
            run.isExplorationBattle = false;
            run.explorationBattleNodeId = null;
            run.explorationBattleEnemyPresetId = null;
            run.explorationBattleNodeType = null;
        }

        public static void Normalize(RunState run)
        {
            if (!IsCasual(run)) return;
            run.casualPvpPendingReports = run.casualPvpPendingReports ?? new List<CasualPvpPendingReport>();
            run.casualPvpPendingReports.RemoveAll(item => item?.report == null || string.IsNullOrWhiteSpace(item.report.matchId)
                || string.IsNullOrWhiteSpace(item.endpoint));
            // Unity inline serialization can restore a null class as an empty object.
            if (string.IsNullOrWhiteSpace(run.casualPvpOpponent?.snapshotId)) run.casualPvpOpponent = null;
            if (string.IsNullOrWhiteSpace(run.casualPvpPlayerSnapshot?.snapshotId)) run.casualPvpPlayerSnapshot = null;
            run.casualPvpRecentOpponentIds = run.casualPvpRecentOpponentIds ?? new List<string>();
            if (string.IsNullOrWhiteSpace(run.casualPvpRunId)) run.casualPvpRunId = Guid.NewGuid().ToString("N");
            run.campaignRoundLimit = 0;
            run.maxFateValue = Math.Max(100, run.maxFateValue);
            // Older matched saves were written while still in the manage phase.
            if (run.casualPvpOpponent != null || run.state == LockedState)
            {
                run.casualPvpRoundEndResolvedRound = run.round;
                MarkLocked(run);
            }
        }

        public static bool IsLocked(RunState run)
        {
            return IsCasual(run) && run.state == LockedState;
        }

        public static void MarkLocked(RunState run)
        {
            run.state = LockedState;
            run.phase = GamePhase.Battle;
        }

        public static RunState CreateBattleCopy(RunState run)
        {
            // Simulation mutates HP and reward fields. Keep the saved checkpoint untouched
            // throughout playback, and commit the copy only when settlement begins.
            var noReward = run.pendingBattleUnitPick == null;
            var noOpponent = run.casualPvpOpponent == null;
            var noPlayer = run.casualPvpPlayerSnapshot == null;
            var shopEmpty = run.shopCards.Select(card => card == null).ToArray();
            var handEmpty = run.handCards.Select(card => card == null).ToArray();
            var pendingEmpty = run.pendingHandCards.Select(card => card == null).ToArray();
            var boardEmpty = run.boardUnits.Select(card => card == null).ToArray();
            var copy = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(run));
            if (noReward) run.pendingBattleUnitPick = copy.pendingBattleUnitPick = null;
            if (noOpponent) run.casualPvpOpponent = copy.casualPvpOpponent = null;
            if (noPlayer) run.casualPvpPlayerSnapshot = copy.casualPvpPlayerSnapshot = null;
            PreserveEmptySlots(shopEmpty, run.shopCards, copy.shopCards);
            PreserveEmptySlots(handEmpty, run.handCards, copy.handCards);
            PreserveEmptySlots(pendingEmpty, run.pendingHandCards, copy.pendingHandCards);
            PreserveEmptySlots(boardEmpty, run.boardUnits, copy.boardUnits);
            return copy;
        }

        private static void PreserveEmptySlots<T>(bool[] empty, List<T> source, List<T> copy) where T : class
        {
            if (source == null || copy == null) return;
            for (var i = 0; i < empty.Length; i++) if (empty[i]) source[i] = copy[i] = null;
        }

        public static CasualPvpSnapshotState CapturePlayerSnapshot(RunState run)
        {
            var data = ProphecyGameSession.Instance?.Data;
            var snapshot = new CasualPvpSnapshotState
            {
                schemaVersion = 1,
                snapshotId = "snap_" + Guid.NewGuid().ToString("N"),
                sourceRunId = run?.casualPvpRunId,
                sourceType = "player",
                displayName = "玩家镜像",
                round = Math.Max(1, run?.round ?? 1),
                contentVersion = Application.version,
                combatVersion = CombatVersion,
                isCheatRun = run?.casualPvpCheatUsed ?? false,
                teamForestGiftTotal = run?.manageResources?.forestGiftTotal ?? 0,
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                powerScore = run != null ? Math.Max(0, BattleStubSystem.EstimatePlayerScore(run)) : 0
            };

            foreach (var unit in run?.boardUnits ?? new List<BoardUnitState>())
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.unitId) || string.IsNullOrWhiteSpace(unit.boardSlotId)) continue;
                var definition = data?.FindUnit(unit.unitId);
                if (definition == null) continue;
                var startCount = Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
                var count = Math.Max(1, (unit.baseCount > 0 ? unit.baseCount : startCount) + unit.roundTempCount);
                snapshot.units.Add(new CasualPvpUnitState
                {
                    unitId = unit.unitId,
                    name = string.IsNullOrWhiteSpace(unit.name) ? definition.name : unit.name,
                    slotId = unit.boardSlotId,
                    star = unit.star > 0 ? unit.star : Math.Max(1, definition.star),
                    isGolden = unit.isGolden,
                    count = count,
                    maxHp = Math.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp),
                    attack = Math.Max(0, definition.attack + unit.shopBuffAttack + unit.roundTempAttack + unit.boardAuraAttack),
                    defense = Math.Max(0, definition.defense + unit.shopBuffDefense),
                    power = Math.Max(0, definition.power + unit.shopBuffPower + unit.roundTempPower),
                    speed = Math.Max(0, definition.speed + unit.shopBuffSpeed),
                    luck = Math.Max(0, definition.luck + unit.shopBuffLuck),
                    morale = Math.Max(0, definition.morale + unit.shopBuffMorale + unit.roundTempMorale),
                    forestGemsReceived = unit.forestGemsReceived,
                    forestGemsAttached = unit.forestGemsAttached,
                    battleProgressCounters = (unit.battleProgressCounters ?? new List<BattleProgressCounterState>())
                        .Where(counter => counter != null).Select(counter => new BattleProgressCounterState { key = counter.key, value = counter.value }).ToList()
                });
            }

            snapshot.units = snapshot.units.OrderBy(unit => unit.slotId).ToList();
            return snapshot;
        }

        public static CasualPvpMatchResponse MatchLocally(RunState run, CasualPvpSnapshotState playerSnapshot)
        {
            TryImportLegacyLog();
            var store = LoadStore();
            // Debug-assisted runs may still play locally, but must never seed an opponent pool.
            if (playerSnapshot?.isCheatRun != true) AddSnapshot(store, playerSnapshot);
            var excluded = new HashSet<string>(run?.casualPvpRecentOpponentIds ?? new List<string>());
            var candidates = store.snapshots
                .Where(snapshot => IsValidOpponent(snapshot, run, excluded))
                .ToList();
            var fallback = false;
            CasualPvpSnapshotState selected;
            if (candidates.Count > 0)
            {
                var seed = unchecked((run.round * 104729) ^ StableHash(run.casualPvpRunId) ^ store.snapshots.Count);
                selected = candidates[new System.Random(seed).Next(candidates.Count)];
            }
            else
            {
                selected = CreateSystemFallback(run.round);
                fallback = true;
                AddSnapshot(store, selected);
            }

            TrimAndSave(store);
            LockOpponent(run, selected);
            return new CasualPvpMatchResponse
            {
                success = selected != null,
                error = selected == null ? "无法生成同回合对手。" : null,
                matchId = run.casualPvpMatchId,
                fallback = fallback || selected.sourceType == "system_fallback",
                snapshot = selected
            };
        }

        public static void LockOpponent(RunState run, CasualPvpSnapshotState snapshot, string matchId = null, string endpoint = null)
        {
            if (run == null || snapshot == null) return;
            run.casualPvpOpponent = snapshot;
            run.casualPvpMatchEndpoint = endpoint;
            MarkLocked(run);
            run.casualPvpMatchId = string.IsNullOrWhiteSpace(matchId) ? "match_" + Guid.NewGuid().ToString("N") : matchId;
            run.casualPvpRecentOpponentIds = run.casualPvpRecentOpponentIds ?? new List<string>();
            if (!string.IsNullOrWhiteSpace(snapshot.snapshotId)) run.casualPvpRecentOpponentIds.Add(snapshot.snapshotId);
            while (run.casualPvpRecentOpponentIds.Count > RecentOpponentLimit) run.casualPvpRecentOpponentIds.RemoveAt(0);
        }

        public static void ClearOpponent(RunState run)
        {
            if (run == null) return;
            run.casualPvpMatchId = null;
            run.casualPvpMatchEndpoint = null;
            run.casualPvpOpponent = null;
            run.casualPvpPlayerSnapshot = null;
        }

        public static bool QueueReport(RunState run, CasualPvpBattleReport report)
        {
            if (!IsCasual(run) || report == null || string.IsNullOrWhiteSpace(report.matchId)
                || string.IsNullOrWhiteSpace(run.casualPvpMatchEndpoint)) return false;
            run.casualPvpPendingReports = run.casualPvpPendingReports ?? new List<CasualPvpPendingReport>();
            if (run.casualPvpPendingReports.Any(item => item?.report?.matchId == report.matchId
                && item.endpoint == run.casualPvpMatchEndpoint)) return false;
            run.casualPvpPendingReports.Add(new CasualPvpPendingReport
            {
                endpoint = run.casualPvpMatchEndpoint,
                report = JsonUtility.FromJson<CasualPvpBattleReport>(JsonUtility.ToJson(report))
            });
            return true;
        }

        public static void CompleteReportAttempt(RunState run, CasualPvpPendingReport pending, bool acknowledged, string error)
        {
            if (run?.casualPvpPendingReports == null || !run.casualPvpPendingReports.Contains(pending)) return;
            pending.attempts++;
            pending.lastError = error;
            if (acknowledged) run.casualPvpPendingReports.Remove(pending);
        }

        private static bool IsValidOpponent(CasualPvpSnapshotState snapshot, RunState run, HashSet<string> excluded)
        {
            if (snapshot == null || snapshot.isCheatRun || snapshot.units == null || snapshot.units.Count == 0) return false;
            if (snapshot.round != run.round || snapshot.sourceRunId == run.casualPvpRunId) return false;
            if (!string.Equals(snapshot.contentVersion, Application.version, StringComparison.Ordinal)) return false;
            if (!string.Equals(snapshot.combatVersion, CombatVersion, StringComparison.Ordinal)) return false;
            if (!string.IsNullOrWhiteSpace(snapshot.snapshotId) && excluded.Contains(snapshot.snapshotId)) return false;
            return HasValidLineup(snapshot);
        }

        public static bool HasValidLineup(CasualPvpSnapshotState snapshot)
        {
            if (snapshot?.units == null || snapshot.units.Count == 0 || snapshot.units.Count > 16) return false;
            var data = ProphecyGameSession.Instance?.Data;
            if (data == null) return false;
            var validSlots = new HashSet<string>(data.Config.GetBoardOrder());
            var occupied = new HashSet<string>();
            foreach (var unit in snapshot.units)
            {
                var definition = unit == null ? null : data.FindUnit(unit.unitId);
                if (definition == null || string.IsNullOrWhiteSpace(unit.slotId)) return false;
                foreach (var slot in BoardSystem.GetOccupiedBoardSlots(definition, unit.slotId))
                    if (!validSlots.Contains(slot) || !occupied.Add(slot)) return false;
            }
            return true;
        }

        private static CasualPvpSnapshotState CreateSystemFallback(int round)
        {
            round = Math.Max(1, Math.Min(100000, round));
            var data = ProphecyGameSession.Instance.Data;
            var maxStar = Math.Min(6, 1 + (round - 1) / 3);
            var pool = data.Units
                .Where(unit => unit != null && !unit.hidden && !string.IsNullOrWhiteSpace(unit.id) && unit.star <= maxStar)
                .OrderBy(unit => unit.id)
                .ToList();
            var snapshot = new CasualPvpSnapshotState
            {
                snapshotId = $"fallback_r{round}_{Guid.NewGuid():N}",
                sourceRunId = "system",
                sourceType = "system_fallback",
                displayName = $"系统保底 · 第{round}回合",
                round = Math.Max(1, round),
                contentVersion = Application.version,
                combatVersion = CombatVersion,
                capturedAtUtc = DateTime.UtcNow.ToString("O")
            };
            if (pool.Count == 0) return snapshot;

            var slots = data.Config.GetBoardOrder();
            var occupied = new HashSet<string>();
            var unitCount = Math.Min(8, Math.Max(2, 2 + (round - 1) / 2));
            var random = new System.Random(round * 7919 + 17);
            for (var i = 0; i < unitCount; i += 1)
            {
                var candidates = pool.Where(unit => slots.Any(anchor => BoardSystem.GetOccupiedBoardSlots(unit, anchor)
                    .All(slot => slots.Contains(slot) && !occupied.Contains(slot)))).ToList();
                if (candidates.Count == 0) break;
                var definition = candidates[random.Next(candidates.Count)];
                var anchorSlot = slots.First(anchor => BoardSystem.GetOccupiedBoardSlots(definition, anchor)
                    .All(slot => slots.Contains(slot) && !occupied.Contains(slot)));
                foreach (var slot in BoardSystem.GetOccupiedBoardSlots(definition, anchorSlot)) occupied.Add(slot);
                var baseCount = Math.Max(1, definition.defaultCount > 0 ? definition.defaultCount : definition.startCount > 0 ? definition.startCount : definition.baseCount > 0 ? definition.baseCount : 1);
                snapshot.units.Add(new CasualPvpUnitState
                {
                    unitId = definition.id,
                    name = definition.name,
                    slotId = anchorSlot,
                    star = Math.Max(1, definition.star),
                    count = Math.Max(1, baseCount + round * 2),
                    maxHp = Math.Max(1, definition.hpPerUnit > 0 ? definition.hpPerUnit : definition.hp),
                    attack = Math.Max(0, definition.attack),
                    defense = Math.Max(0, definition.defense),
                    power = Math.Max(0, definition.power),
                    speed = Math.Max(0, definition.speed),
                    luck = Math.Max(0, definition.luck),
                    morale = Math.Max(0, definition.morale)
                });
            }
            // The UI presents this value before combat. Use the same runtime estimator that
            // scores player and mirrored lineups so fallback strength is comparable.
            snapshot.powerScore = Math.Max(0, BattleStubSystem.EstimateCasualPvpSnapshotScore(snapshot));
            return snapshot;
        }

        private static void TryImportLegacyLog()
        {
            if (_legacyImportAttempted) return;
            _legacyImportAttempted = true;
            if (!File.Exists(LegacyLogPath)) return;
            var store = LoadStore();
            var imported = 0;
            foreach (var line in File.ReadLines(LegacyLogPath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"type\":\"battle\"")) continue;
                try
                {
                    var legacy = JsonUtility.FromJson<LegacyBattleLog>(line);
                    if (legacy == null || legacy.round < 1 || legacy.playerUnits == null || legacy.playerUnits.Count == 0) continue;
                    var snapshot = new CasualPvpSnapshotState
                    {
                        snapshotId = $"legacy_{legacy.round}_{StableHash(line):x8}",
                        sourceRunId = $"legacy_{StableHash(line + legacy.round):x8}",
                        sourceType = "legacy_player",
                        displayName = "训练镜像",
                        round = legacy.round,
                        contentVersion = Application.version,
                        combatVersion = "battle_v1",
                        capturedAtUtc = DateTime.UtcNow.ToString("O"),
                        powerScore = Math.Max(0, legacy.playerScore),
                        units = legacy.playerUnits.Where(unit => unit != null).Select(unit => new CasualPvpUnitState
                        {
                            unitId = unit.id,
                            name = unit.name,
                            slotId = unit.slot,
                            star = Math.Max(1, unit.star),
                            count = Math.Max(1, unit.count),
                            maxHp = Math.Max(1, unit.maxHp),
                            attack = Math.Max(0, unit.attack),
                            defense = Math.Max(0, unit.defense),
                            power = Math.Max(0, unit.power),
                            speed = Math.Max(0, unit.speed)
                        }).ToList()
                    };
                    if (snapshot.units.All(unit => ProphecyGameSession.Instance.Data.FindUnit(unit.unitId) != null))
                    {
                        AddSnapshot(store, snapshot);
                        imported += 1;
                    }
                }
                catch
                {
                    // Old log rows may contain malformed JSON. Import valid rows independently.
                }
            }
            if (imported > 0) TrimAndSave(store);
            Debug.Log($"[CasualPvp] Imported {imported} valid legacy mirror snapshots.");
        }

        private static CasualPvpSnapshotStore LoadStore()
        {
            try
            {
                if (!File.Exists(StorePath)) return new CasualPvpSnapshotStore();
                var store = JsonUtility.FromJson<CasualPvpSnapshotStore>(File.ReadAllText(StorePath));
                if (store == null) store = new CasualPvpSnapshotStore();
                store.snapshots = store.snapshots ?? new List<CasualPvpSnapshotState>();
                return store;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CasualPvp] Mirror store load failed: {ex.Message}");
                return new CasualPvpSnapshotStore();
            }
        }

        private static void AddSnapshot(CasualPvpSnapshotStore store, CasualPvpSnapshotState snapshot)
        {
            if (store == null || snapshot == null || snapshot.isCheatRun || snapshot.units == null || snapshot.units.Count == 0) return;
            if (store.snapshots.Any(item => item != null && item.snapshotId == snapshot.snapshotId)) return;
            store.snapshots.Add(snapshot);
        }

        private static void TrimAndSave(CasualPvpSnapshotStore store)
        {
            try
            {
                store.snapshots = (store.snapshots ?? new List<CasualPvpSnapshotState>())
                    .Where(snapshot => snapshot != null && !snapshot.isCheatRun && snapshot.units != null && snapshot.units.Count > 0)
                    .OrderByDescending(snapshot => snapshot.capturedAtUtc)
                    .Take(MaximumStoredSnapshots)
                    .ToList();
                File.WriteAllText(StorePath, JsonUtility.ToJson(store, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CasualPvp] Mirror store save failed: {ex.Message}");
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty) hash = hash * 31 + character;
                return hash;
            }
        }

        [Serializable]
        private sealed class LegacyBattleLog
        {
            public int round;
            public int playerScore;
            public List<LegacyBattleUnit> playerUnits = new List<LegacyBattleUnit>();
        }

        [Serializable]
        private sealed class LegacyBattleUnit
        {
            public string slot;
            public string id;
            public string name;
            public int star;
            public int count;
            public int maxHp;
            public int attack;
            public int defense;
            public int power;
            public int speed;
        }
    }
}
