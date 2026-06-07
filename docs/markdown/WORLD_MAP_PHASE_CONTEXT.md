# World Map MVP Phase Context

> Updated: 2026-06-06  
> Purpose: compact handoff context for continuing the world map MVP in a new conversation.

## Current Product Rule Alignment

The correct MVP loop is:

`Choose hero -> Night manage -> click Explore -> resolve night round-end effects -> Day map -> choose exactly 1 node -> resolve node -> return to Night manage`

Important constraints:

- New runs should start in `NightManage/manage`, not directly on the map.
- The player needs a chance to buy/deploy units before the first map node.
- The button previously labeled `Battle` / `Start Battle` should be treated as `Explore`.
- Clicking `Explore` is equivalent to ending the current night management round.
- Therefore clicking `Explore` must trigger existing round-end management logic before showing the day map.
- The day map is a one-step-per-day choice in MVP.
- A node may be battle, resource, treasure, boss, etc.
- After one node resolves, the day ends immediately and the game returns to `NightManage/manage`.
- Map battle start must not trigger management round-end effects again.
- Boss victory still goes directly to `Victory`.

## Current Code State

Implemented before this document:

- `ProphecyGameSession.StartNewRun()` starts in:
  - `state = "manage"`
  - `phase = GamePhase.NightManage`
  - `dayCount = 0`
  - `maxMovePoints = 1`
  - `remainingMovePoints = 0`
- `RuntimeUiBootstrap` wires `BattleButton` / `BattleButtonV2` to `RunSceneController.StartDayExploreFromManage()` and labels them as `探索`.
- `WorldMapView` exists and renders nodes, connection lines, node state, and an `入夜经营` button.
- `RunFlowController.MoveToMapNode()` moves to a node and resolves non-battle nodes.
- Map combat uses exploration battle context:
  - `RunState.isExplorationBattle`
  - `RunState.explorationBattleNodeId`
  - `RunState.explorationBattleEnemyPresetId`
  - `RunState.explorationBattleNodeType`
- `RunSceneController.PlayBattleStage()` skips management round-end effects when `Run.isExplorationBattle` is true.
- `SaveGameSystem` normalizes missing world map fields and clears interrupted exploration battle context back to day map.

Implemented in this pass:

- `RunSceneController.StartDayExploreFromManage()` now starts `EndNightManageAndStartDayExplore()`.
- Clicking Explore from `NightManage/manage` now blocks unresolved targeted blessing and gold deploy reward choices, calls `RunFlowController.ResolveRoundEndBeforeBattle()`, consumes and plays management feedback, plays ability/synthesis SFX where applicable, shows gold/unit number feedback, then calls `_flow.StartNewDay()` to enter `DayExplore/day`.
- Map combat still skips management round-end effects because `PlayBattleStage()` checks `Run.isExplorationBattle`.
- Day node completion now starts the next night management round. `RunFlowController` uses `StartNextNightManageAfterDayNode()` after non-boss battle/resource/treasure nodes so `round`, income, round-start effects, pending battle rewards, synthesis, and shop refresh advance before returning to `NightManage/manage`.
- Any battle defeat now ends the run immediately with `GamePhase.GameOver`; the game result modal's restart action returns to title and opens hero selection for the next run.
- Victory/GameOver settlement modal now shows a run summary: result, hero, round/day, HP/gold/shop level, win/loss record, map clear/visible counts, current node, board/hand/treasure counts, and last battle score.

## Historical Gap Fixed

The following recommendation is retained as historical context only; it is no longer a pending code gap.

Previously, `RunSceneController.StartDayExploreFromManage()` started exploration too directly through `_flow.StartNewDay()`.

It must be changed so clicking `探索` first performs the same management round-end logic that old battle start used:

- `RunFlowController.ResolveRoundEndBeforeBattle()`
- consume/play management feedback where practical
- apply synthesis feedback and ability SFX where practical
- then enter day exploration

The existing method name can stay, but its semantics should become:

`EndNightManageAndStartDayExplore`

Recommended implementation options:

1. Move the round-end handling into `RunFlowController.StartNewDay()` / a new `StartExplorationFromManage()` method.
2. Or keep UI feedback in `RunSceneController.StartDayExploreFromManage()` and call:
   - capture snapshots
   - `_flow.ResolveRoundEndBeforeBattle()`
   - `_flow.ConsumeManageFeedbackEvents()`
   - play feedback
   - then `_flow.StartNewDay()`

Prefer option 2 if preserving current UI feedback behavior matters, because `RunSceneController.PlayBattleStage()` already has similar presentation logic.

## TODO Status

`docs/WORLD_MAP_MVP_TODO.md` currently says:

- Current phase: `Phase I`
- Progress: `60 / 60`
- MVP loop status:
  - `H7. Full MVP run-through` verified in Unity.

Verified loop:

`Choose hero -> Night manage/deploy -> Explore button triggers round-end effects -> Day map -> choose node -> node resolves -> second Night manage round -> repeat -> Boss -> Victory`

Also verified/fixed:

- Battle defeat ends the run with `GamePhase.GameOver`.
- The restart action returns to title and opens hero selection for the next run.

## QA Already Run

Executed successfully after fixing console encoding:

- `tools/qa_unit_data.py`
- `tools/qa_battle_consistency.py`
- `tools/validate_world_map_mvp.ps1`

Known QA warnings remain from existing data/realtime battle parity:

- Unit data QA has existing warnings around golden upgrade ratios, semantic warnings, and one star range issue.
- Battle consistency QA reports Stub/Realtime kind coverage differences.
- These are not blockers for the current world map MVP because authoritative settlement uses `BattleStubSystem`.
