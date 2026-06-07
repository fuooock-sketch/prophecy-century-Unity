# -*- coding: utf-8 -*-
"""Add player state logging to RunFlowController.cs."""
import re

path = "Assets/Scripts/Systems/RunFlowController.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add using System.IO if not present
if "using System.IO;" not in content:
    content = content.replace(
        "using ProphecyCentury.Systems;",
        "using System.IO;\nusing ProphecyCentury.Systems;"
    )

# 2. Add LogPlayerState method after NextRound()
log_player = r"""
        private static void LogPlayerState(RunState run)
        {
            try
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "player_state_log.jsonl");
                var def = ProphecyGameSession.Instance.Data;
                var boardJson = string.Join(",", run.boardUnits.Select(u =>
                {
                    var d = def.FindUnit(u.unitId);
                    var cnt = u.baseCount > 0 ? u.baseCount : (d != null && d.defaultCount > 0 ? d.defaultCount : d != null && d.startCount > 0 ? d.startCount : 1);
                    return "{\"id\":\"" + u.unitId + "\",\"name\":\"" + u.name + "\",\"star\":" + u.star + ",\"count\":" + cnt + ",\"slot\":\"" + (u is BoardUnitState b ? b.boardSlotId : "?") + "\"}";
                }));
                var line = "{\"round\":" + run.round + ",\"type\":\"state\",\"gold\":" + run.gold + ",\"shopLevel\":" + run.shopLevel + ",\"hp\":" + run.playerHp + ",\"wins\":" + run.campaignWins + ",\"board\":[" + boardJson + "],\"handCount\":" + run.handCards.Count + "}";
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }
""".strip("\n")

# Insert after NextRound method's closing brace
# Find 'ShopSystem.RefreshForNewRound(run);\n        }'
marker = "ShopSystem.RefreshForNewRound(run);\n        }"
idx = content.index(marker) + len(marker)
content = content[:idx] + "\n" + log_player + "\n" + content[idx:]

# 3. Add LogBattleResult after ResolveBattleOutcome
log_battle = r"""
        private static void LogBattleResult(RunState run, BattleStubResult result)
        {
            try
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "player_state_log.jsonl");
                var summary = (result.Summary ?? "").Replace("\"", "'").Replace("\\", "/");
                var line = "{\"round\":" + run.round + ",\"type\":\"battle\",\"victory\":" + (result.Victory ? "true" : "false") + ",\"playerScore\":" + result.PlayerScore + ",\"enemyScore\":" + result.EnemyScore + ",\"hpDelta\":" + result.HpDelta + ",\"summary\":\"" + summary + "\"}";
                System.IO.File.AppendAllText(path, line + System.Environment.NewLine);
            }
            catch { }
        }
""".strip("\n")

# Insert before 'private static void ApplyNodeReward' or similar
marker2 = "private static void ApplyNodeReward"
idx2 = content.index(marker2)
content = content[:idx2] + log_battle + "\n\n        " + content[idx2:]

# 4. Call LogPlayerState at end of NextRound
content = content.replace(
    "ShopSystem.RefreshForNewRound(run);\n            LogPlayerState",
    "ShopSystem.RefreshForNewRound(run);\n            LogPlayerState(run);\n            LogPlayerState"
)
# Fix double insert
content = content.replace("LogPlayerState(run);\n            LogPlayerState(run);", "LogPlayerState(run);")

# 5. Call LogBattleResult in ResolveBattleOutcome (after victory check)
content = content.replace(
    "ClearExplorationBattleContext(run);\n                return;\n            }\n\n            StartNextNightManageAfterDayNode(run);\n            ApplyNodeReward(run, nodeResult, nodeId);",
    "ClearExplorationBattleContext(run);\n                LogBattleResult(run, result);\n                return;\n            }\n\n            StartNextNightManageAfterDayNode(run);\n            ApplyNodeReward(run, nodeResult, nodeId);\n            LogBattleResult(run, result);"
)

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write(content)

print("Done! Added LogPlayerState and LogBattleResult to RunFlowController.cs")
print("Log file: Application.persistentDataPath/player_state_log.jsonl")