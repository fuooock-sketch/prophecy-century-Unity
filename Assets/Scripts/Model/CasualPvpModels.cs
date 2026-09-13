using System;
using System.Collections.Generic;

namespace ProphecyCentury.Model
{
    public static class GameModeIds
    {
        public const string Campaign = "campaign";
        public const string CasualPvp = "casual_async_pvp";
    }

    [Serializable]
    public sealed class CasualPvpSnapshotState
    {
        public int schemaVersion = 1;
        public string snapshotId;
        public string sourceRunId;
        public string sourceType;
        public string displayName;
        public int round;
        public string contentVersion;
        public string combatVersion = "battle_v1";
        public bool isCheatRun;
        public int powerScore;
        public int teamForestGiftTotal;
        public string capturedAtUtc;
        public List<CasualPvpUnitState> units = new List<CasualPvpUnitState>();
    }

    [Serializable]
    public sealed class CasualPvpUnitState
    {
        public string unitId;
        public string name;
        public string slotId;
        public int star;
        public bool isGolden;
        public int count;
        public int maxHp;
        public int attack;
        public int defense;
        public int power;
        public int speed;
        public int luck;
        public int morale;
        public int forestGemsReceived;
        public int forestGemsAttached;
        public List<BattleProgressCounterState> battleProgressCounters = new List<BattleProgressCounterState>();
    }

    [Serializable]
    public sealed class CasualPvpSnapshotStore
    {
        public int schemaVersion = 1;
        public List<CasualPvpSnapshotState> snapshots = new List<CasualPvpSnapshotState>();
    }

    [Serializable]
    public sealed class CasualPvpMatchRequest
    {
        public int round;
        public string requesterRunId;
        public string contentVersion;
        public string combatVersion;
        public List<string> excludeSnapshotIds = new List<string>();
    }

    [Serializable]
    public sealed class CasualPvpMatchResponse
    {
        public bool success;
        public string error;
        public string matchId;
        public bool fallback;
        public CasualPvpSnapshotState snapshot;
    }

    [Serializable]
    public sealed class CasualPvpBattleReport
    {
        public string matchId;
        public string snapshotId;
        public string requesterRunId;
        public int round;
        public bool victory;
        public int hpDelta;
        public int playerScore;
        public int enemyScore;
        public string reportedAtUtc;
    }

    [Serializable]
    public sealed class CasualPvpPendingReport
    {
        public string endpoint;
        public CasualPvpBattleReport report;
        public int attempts;
        public string lastError;
    }
}
