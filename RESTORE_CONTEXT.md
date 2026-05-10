# Prophecy Century Unity Restore Context

Last updated: 2026-05-11

## Goal

Continue restoring/migrating the original `prophecy-century-ChatGpt/work_v1_final` project into the Unity project at `prophecy-century-Unity/prophecy_century`.

## Current Source And Target

- Original source project: `C:\projectZhongxu\prophecy_century\prophecy-century-ChatGpt\work_v1_final`
- Unity target project: `C:\projectZhongxu\prophecy_century\prophecy-century-Unity\prophecy_century`
- Git branch: `main`
- Remote: `https://github.com/fuooock-sketch/prophecy-century-Unity`

## Current Unity State

- Existing migration notes are in `Assets/README_MIGRATION.md`.
- Current Unity project already has core data, run state, shop, board, placeholder battle, and runtime debug UI.
- Current known missing areas from migration notes:
  - real title/shop/hand/board/battle UI parity
  - battle simulator parity with `BattleManager.js`
  - skill resolver parity with `BattleSkillManager.js`
  - save/load persistence
  - art/audio import and binding

## Process Log

### 2026-05-10 - Process 1: Initial orientation

- Checked Unity repo status: `main...origin/main`.
- Dirty state before this process:
  - modified `ProjectSettings/GraphicsSettings.asset`
  - untracked local Git command backup note at repo root
- Found original Web/Electron source project in sibling path.
- Chosen first continuation flow: restore original art/icon/audio assets into Unity asset folders, because `Assets/Art` and `Assets/Audio` are currently empty.

### 2026-05-10 - Process 2: Original asset import

- Copied original image assets from `work_v1_final/asset/image` into `Assets/Art`.
- Copied original icon assets from `work_v1_final/asset/icon` into `Assets/Art/icon`.
- Copied original audio assets from `work_v1_final/asset/audio` into `Assets/Audio`.
- Verification counts after import:
  - `Assets/Art`: 125 files
  - `Assets/Audio`: 58 files
- Note: first copy attempt was blocked by sandbox directory write permissions; the same copy operation succeeded after explicit approval.
- Git dirty state after import now includes:
  - modified `ProjectSettings/GraphicsSettings.asset`
  - untracked `Assets/Art/`
  - untracked `Assets/Audio/`
  - untracked `RESTORE_CONTEXT.md`
  - untracked local Git command backup note at repo root

### 2026-05-10 - Process 3: Runtime title selection flow

- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` to generate a runtime title screen before the run UI.
- Added runtime campaign and hero dropdowns plus a `Start Run` button.
- Moved the existing shop/hand/board/battle UI under a generated `RunPanel`, so it is hidden while the title panel is active.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - initializes dropdown options from loaded campaign and hero data
  - starts a selected run with the chosen campaign and hero
  - changes `New Run` behavior to return to title selection
  - lazily initializes shop/run state before refreshing the run view
- This is still a first-pass Unity UI, not full parity with the original Web title/menu styling.

### 2026-05-10 - Process 4: Bind imported title background

- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` so the generated title panel loads `Assets/Art/bg/loading_image.png`.
- The image is loaded at runtime from `Application.dataPath`, converted into a `Sprite`, and assigned to the title panel `Image`.
- This makes the first imported original visual asset visible in Play Mode.

### 2026-05-10 - Process 5: Bind imported BGM

- Unity Editor import was checked after the project was opened:
  - `Assets/Art` has generated `.meta` files.
  - `Assets/Audio` has generated `.meta` files.
  - Editor log showed successful script compilation and import, with one MP3 truncation warning for `BladeDBCampaign.mp3`.
- Added `Assets/Scripts/UI/RuntimeBgmPlayer.cs`.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` to add an `AudioSource` plus `RuntimeBgmPlayer` to the runtime canvas.
- `RuntimeBgmPlayer` loads `Assets/Audio/manage-bgm.mp3` from `Application.dataPath` and plays it looped at runtime.

### 2026-05-10 - Process 6: Title selection previews

- Updated `Assets/Scripts/UI/RunSceneController.cs` to refresh title preview text when campaign or hero dropdown values change.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` to generate campaign and hero preview text blocks on the title screen.
- Campaign preview currently shows campaign name and description.
- Hero preview currently shows hero name, title, short text, passive text, and active text.
- This improves first-pass title/hero flow readability before full original UI parity is rebuilt.

### 2026-05-10 - Process 7: RuntimeBgmPlayer meta file

- Unity did not immediately refresh after `RuntimeBgmPlayer.cs` was added; `RuntimeBgmPlayer.cs.meta` was still missing after a short wait.
- Added `Assets/Scripts/UI/RuntimeBgmPlayer.cs.meta` manually using the same MonoImporter structure as the existing UI script meta files.
- This keeps the repository complete even before the Editor performs its next asset refresh.

### 2026-05-10 - Process 8: Fix RefreshView recursion

- Unity Play Mode reported a `StackOverflowException`.
- Root cause: `RefreshView()` called `StartRunIfNeeded()`, and `StartRunIfNeeded()` called `RefreshView()` again.
- Fixed `Assets/Scripts/UI/RunSceneController.cs` by making `StartRunIfNeeded()` only ensure run/shop state, without writing logs or refreshing view.
- Added a null guard in `WriteLog()` so missing UI wiring does not throw during early startup.

### 2026-05-10 - Process 9: Runtime unit icon cards

- User confirmed title screen opens with no errors after the recursion fix.
- Added `Assets/Scripts/UI/RuntimeUnitIconCache.cs` and `.meta`.
- `RuntimeUnitIconCache` loads unit portraits from `Assets/Art/icon/unit/{unitName}.png` and caches generated sprites.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - added runtime roots for shop, hand, and board card lists
  - `RefreshView()` now rebuilds visual unit card rows with icon + text
  - clicking a shop card buys that indexed card
  - clicking a hand card deploys that indexed card
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - generated `ShopCardRoot`, `HandCardRoot`, and `BoardCardRoot`
  - assigned those roots to `RunSceneController`
  - kept text summaries as compact headers/fallback context

### 2026-05-10 - Process 10: Runtime feature icons

- Added `Assets/Scripts/UI/RuntimeFeatureIconCache.cs` and `.meta`.
- `RuntimeFeatureIconCache` loads feature icons from `Assets/Art/icon/feature/{iconName}.png` and caches generated sprites.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - top bar labels now use icon + text for gold, round, HP, and state
  - bottom action buttons now show feature icons for refresh shop, buy, deploy, battle, new run, upgrade, and lock
- Source strings for Chinese icon file names use Unicode escapes to keep the C# source mostly ASCII while still matching imported asset names.

### 2026-05-10 - Process 11: Unit card stat details

- Updated `Assets/Scripts/UI/RunSceneController.cs` card labels to look up `UnitDefinition` by `unitId`.
- Shop, hand, and board visual card rows now show:
  - unit name and star
  - race / faith / type label
  - ATK / HP / DEF
- Board cards also include their board slot id as a prefix.
- Card row height and font size were adjusted so the two-line detail label fits better.

### 2026-05-10 - Process 12: Per-card actions

- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - shop card rows now have an explicit `Buy` button for the matching card index
  - hand card rows now have `Deploy` and `Sell` buttons for the matching card index
  - board card rows now have a `Sell` button for the matching board unit
  - old `SellLastHandCard()` / `SellLastBoardUnit()` now delegate to indexed sell helpers
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` bottom quick action labels:
  - `Buy First` -> `Quick Buy`
  - `Deploy First` -> `Quick Deploy`
- This makes detailed card-list actions usable without relying on "last card" shortcuts.

### 2026-05-10 - Process 13: Chinese runtime UI text

- User requested all buttons display in Chinese.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` visible runtime UI text:
  - title `预言世纪`
  - title labels `战役` / `英雄`
  - title button `开始游戏`
  - top bar labels `金币` / `回合` / `生命` / `阶段`
  - panel labels `商店` / `手牌` / `棋盘` / `战役` / `英雄` / `战斗预览` / `日志`
  - bottom buttons `刷新商店` / `快速购买` / `快速部署` / `结算战斗` / `新开一局` / `升级商店` / `锁定商店`
- Updated `Assets/Scripts/UI/RunSceneController.cs` visible runtime UI text:
  - logs and action results are now Chinese
  - card action buttons now use `购买` / `部署` / `出售`
  - shop/hand/board summaries are now Chinese
  - run states are displayed as `经营` / `战斗` / `结算` / `失败`
  - stat labels use `攻` / `血` / `防`
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs` placeholder battle summaries to Chinese.

### 2026-05-11 - Process 14: Shop and round economy parity

- User reported shop rules and per-round gold income felt incorrect.
- Confirmed Unity implementation was still first-pass and diverged from original `Game.js`.
- Updated `Assets/Scripts/Systems/ShopSystem.cs`:
  - shop level now uses `shopLevel - 1` for slot/star tables, so level 1 correctly uses level-1 rules
  - hidden units and `light_illusion` are excluded from random shop generation
  - buying a shop card now leaves an empty slot instead of shrinking/reindexing the shop
  - hand size is capped at 10 before buying
  - shop upgrade cost now follows original cost indexing plus round discount from `shopUpgradeAnchorRound`
  - shop upgrade now preserves existing slots and fills new/empty slots instead of refreshing the whole shop
  - locked shop now preserves existing cards into the next round, fills empty slots, then auto-unlocks
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - next-round income now resets gold to `roundIncomeBase + currentRound`, matching the original round-start economy instead of adding a flat amount to leftover gold
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - battle resolution now advances to the next round whenever the player is not game over, matching the original flow where non-lethal losses continue
  - quick buy now targets the first non-empty shop slot
  - empty shop slots no longer show action buttons
- Added `shopUpgradeAnchorRound` to `RunState` and initialized it in `ProphecyGameSession`.
- Added `hidden` / `limit` fields to `UnitDefinition` so imported unit data can support shop filtering and later pool-limit parity.
- Verification:
  - Unity Editor log showed script import and compile succeeded with `Tundra build success`.
  - A separate batchmode Unity check was interrupted after taking too long; likely due to already-open Unity/project-lock startup behavior, not a compile error.

### 2026-05-11 - Process 15: Board placement selection and movement

- User requested starting remaining migration step 1: board slot selection, drag, swapping, and deployment rules.
- Updated `Assets/Scripts/Systems/BoardSystem.cs`:
  - validates board slot ids against config board layout
  - deploys hand units into a requested slot when provided
  - keeps original rule that hand units cannot overwrite occupied board slots
  - added `MoveBoardUnit()` to move a board unit to an empty slot or swap with an occupied target slot
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - `DeployUnit()` now accepts an optional board slot id
  - added `MoveBoardUnit()` wrapper
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - board area now renders all configured board slots as a compact 4-row grid
  - clicking a hand card selects it; clicking an empty board slot deploys it there
  - clicking a board unit selects it; clicking another slot moves or swaps it
  - board slots expose contextual small actions for deploy/move/sell
  - quick deploy still works and uses selected empty slot when one is selected, otherwise first open slot
- Added runtime drag/drop helper scripts:
  - `Assets/Scripts/UI/RuntimeUnitDragItem.cs`
  - `Assets/Scripts/UI/RuntimeBoardSlotDropTarget.cs`
  - plus `.meta` files
- Verification status:
  - Static review completed.
  - Unity Editor log tail did not show fresh `error CS` entries after these edits, but it also did not show an explicit fresh import for the two new helper scripts in the sampled tail. User should allow Unity to refresh/compile and test Play Mode interactions.

### 2026-05-11 - Process 16: Runtime UI clarity pass

- User reported the current UI looked blurry and text/images were hard to read.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - enabled `Canvas.pixelPerfect`
  - changed runtime canvas reference resolution from `1800x900` to `1600x900` to reduce downscaling in common Game View sizes
  - set `CanvasScaler.dynamicPixelsPerUnit = 3` to improve legacy `Text` dynamic font bitmap sharpness
  - set runtime-loaded title texture filter/wrap/aniso settings
  - disabled text best-fit resizing in generated text blocks
- Updated `Assets/Scripts/UI/RuntimeUnitIconCache.cs` and `RuntimeFeatureIconCache.cs`:
  - set loaded runtime textures to clamp wrapping, bilinear filtering, and higher anisotropy
  - enabled `Image.preserveAspect` for loaded icons
- Updated `Assets/Scripts/UI/RunSceneController.cs` board slot sizing:
  - slightly enlarged compact board cells and board action button fonts after the previous slot-grid pass made them too small
- Verification:
  - Unity Editor log showed script domain reload and no fresh compile errors in the sampled tail.
  - If UI still appears blurry, also check Unity Game View scale: use a fixed resolution or 1x scale, because the Editor can blur the whole rendered output when the Game tab is zoomed.

### 2026-05-11 - Process 17: Unit lifecycle parity

- User requested continuing with step 2: unit lifecycle rules.
- Updated `Assets/Scripts/Model/RunState.cs`:
  - added serializable `shopPool`
  - added shop lifecycle fields to `UnitCardState`: pool cost/reservation/contribution, purchase source, and shop stat buffs
- Updated `Assets/Scripts/Data/UnitDefinition.cs`:
  - added `threshold` and `price` to `SkillDefinition` for sell-price talents
- Updated `Assets/Scripts/Systems/ShopSystem.cs`:
  - initializes a shop pool from original unit limit overrides
  - refresh releases old shop-card reservations before rolling new shop cards
  - generated shop cards reserve pool count immediately
  - generated shop cards receive `manageResources.shopGeneratedBuffAttack`
  - buying a card keeps the pool consumed by moving reservation into unit `shopPoolContribution`
  - hand cap remains enforced at 10
  - preserve/fill flows for lock and upgrade keep existing reservations and reserve new cards
- Updated `Assets/Scripts/Systems/BoardSystem.cs`:
  - deploying from hand now clones all card lifecycle fields onto the board unit
  - selling hand or board units refunds their shop pool contribution
  - selling now respects `on_sell_price_if_attack_threshold` talents, using shop-buffed attack
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - unit labels now show shop-buffed attack/hp/defense
  - shop-purchased cards show a small pool-source marker in the temporary UI label
- Verification:
  - Unity Editor log showed successful script/domain reload with no `error CS` entries in the sampled log.

### 2026-05-11 - Process 18: Synthesis and golden unit parity

- User requested continuing to the next migration step.
- Added `Assets/Scripts/Systems/SynthesisSystem.cs` and `.meta`.
- Implemented automatic synthesis behavior:
  - finds three non-golden copies with the same `unitId` across hand and board
  - removes the selected copies from their current zones
  - creates one golden unit in hand, matching the original flow
  - repeats until no additional three-copy group exists
- Golden unit state:
  - sums source units' `shopPoolContribution` so selling the golden unit can refund the combined pool usage
  - marks `fromShopPurchase` when any source unit came from shop pool
  - computes inherited gold stats from the max source stat times 1.1, with defense fixed to 10
  - stores inherited stat differences in the existing `shopBuff*` fields so temporary UI and later systems can read the effective stats without another data-shape change
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - successful shop purchases now call `SynthesisSystem.TrySynthesizeAll()`
  - successful deployments now call `SynthesisSystem.TrySynthesizeAll()`
- Verification:
  - Unity Editor log showed script compilation/domain reload success with no `error CS` entries.

### 2026-05-11 - Process 19: Manage-phase skill resolver

- User requested continuing to the next migration step.
- Added `Assets/Scripts/Systems/ManageEventResolver.cs` and `.meta`.
- Expanded skill data parsing in `Assets/Scripts/Data/UnitDefinition.cs` for manage skill fields such as race/tag filters, target ids, count/thresholds, attack/defense/power/hp values, gift/gain values, nested `stats`, and excluded ids.
- Expanded `Assets/Scripts/Model/RunState.cs` unit runtime state:
  - round temporary attack/power/morale
  - forest gem attachment/received counters
  - manage entry/every-N trigger counters
  - one-shot receive-gift discover flag
- Wired manage events into `Assets/Scripts/Systems/RunFlowController.cs`:
  - buying a unit emits `on_gain_unit`
  - deploying emits `on_entry`
  - selling emits `on_sell`
  - selling board units also emits `on_leave`
  - entering battle first resolves `on_round_end`
  - next round resets round gift/temp counters and emits `on_round_start`
- Updated lifecycle copying in `BoardSystem` and `SynthesisSystem` so gems, temporary stats, and manage counters survive deploy and synthesis.
- Added shop devour support through `ShopSystem.RemoveShopCardForDevour()`.
- Implemented the manage event handlers needed by the current `unit_data.json` talents, including:
  - entry race/tag stat buffs
  - round-end self/team/tagged buffs
  - round-start temporary power and round-end retrigger chains
  - leave-board gold, leave tagged buffs, leave retrigger entry effects
  - gain-stat chain reactions
  - forest gem reserve, gift, receive, evolve, and every-N effects
  - shop-card devour effects and devour-triggered team buffs
  - add-unit-to-hand and discover-style hand grants
  - sell-time attached-gem absorb effects
- Known boundary:
  - `while_on_board_per_ally_id_buff_type_attack` and `while_on_board_race_threshold_team_speed` are continuous board/battle aura skills and should be completed with battle simulator/battle skill parity rather than as one-shot manage events.
- Verification:
  - Unity Editor log showed earlier script/domain reload success with no `error CS` entries.
  - After the final small resolver expansion, the sampled Editor log still contained no `error CS`; Unity did not append a fresh import block in the sampled tail, so user should let the open Editor refresh once before Play testing.

### 2026-05-11 - Process 20: Manage resolver compile fix

- User reported Unity compile error:
  - `ManageEventResolver.cs(926,25): error CS1929`
  - Cause: `GetBoardOrder()` returns `IReadOnlyList<string>`, which does not expose `IndexOf()`.
- Fixed `Assets/Scripts/Systems/ManageEventResolver.cs`:
  - replaced `order.IndexOf(unit?.boardSlotId)` with a simple counted loop over `order.Count`.
- Verification:
  - The fix is applied in source.
  - Immediate log search still showed the historical CS1929 entry; wait for Unity to refresh and confirm whether a new compile block appears.

### 2026-05-11 - Process 21: Battle simulator core

- User confirmed the compile error was gone and requested continuing.
- Replaced the old pure score-comparison placeholder inside `Assets/Scripts/Systems/BattleStubSystem.cs` with a deterministic no-render battle simulator while keeping the class name stable for existing UI references.
- New battle core behavior:
  - builds player battle units from board units and current runtime stat buffs
  - generates an enemy team for the current round from available non-hidden unit data
  - applies basic continuous same-unit-id attack sync aura
  - simulates attack cadence over configured battle time
  - chooses nearest living targets by board slot distance
  - applies damage from attack, power, and target defense
  - supports crit chance from luck
  - supports morale-based extra attacks and counters
  - resolves victory by wipeout or timeout remaining HP
  - applies player HP loss on defeat based on surviving enemy pressure
  - records player/enemy damage and a battle summary
- Updated `Assets/Scripts/UI/RunSceneController.cs` battle preview:
  - preview now calls `BattleStubSystem.EstimatePlayerScore()` and `EstimateEnemyScore()` so displayed score matches the simulator's stat model.
- Known boundary:
  - This is still a headless simulator. Full battle skill triggers, summons, shields, locks, death effects, next-round battle rewards, and visual presentation are still the next dedicated steps.
- Verification:
  - Unity Editor log showed a fresh `Tundra build success` after these edits.

## Next Suggested Work

Recommended next steps:

1. Let Unity refresh scripts, then Play test a small manage-flow loop: buy/deploy/sell/start battle/next round.
2. Start replacing `BattleStubSystem` with the real battle simulator core from the original project.
3. Keep `while_on_board_*` continuous aura behavior in mind for the battle simulator/battle skill steps.

## Remaining Migration Roadmap Estimate

As of 2026-05-11 after Process 21, the full migration likely needs about 5 remaining major implementation steps:

1. Battle skill resolver: port `BattleSkillManager.js` triggers and post-battle rewards, including next-round gold, next-round buffs, evolves, discover rewards, death effects, once-per-battle guards, summons, shields/locks, and continuous `while_on_board_*` aura effects.
2. Enemy plan and campaign progression: enemy budget/scaling, roster generation, round milestones, victory round, rewards, defeat/gameover flow, battle history.
3. Save/load persistence: serialize run state, shop pool state, history, options, selected campaign/hero, and restore compatibility.
4. UI parity pass: replace first-pass runtime UI with closer original title/manage/shop/hand/board/battle/reward modal flows, including Chinese text encoding cleanup.
5. Asset/audio/battle presentation polish: bind remaining imported art/audio, battle visuals, sound effects, animations/floats, and final Play Mode regression checks.

Completed from the previous roadmap:

- Board placement parity: selectable board slots, drag/rearrange, swapping, and original no-overwrite hand deployment rule.
- Unit lifecycle parity: shop pool limit accounting, generated shop buffs, buy/deploy/sell state transfer, special sell price, and pool refunds.
- Synthesis parity: three-copy merge, golden unit creation, inherited stats, pool contribution carryover, and post-buy/post-deploy auto-synthesis.
- Manage-phase skill parity: event bus and handlers for current one-shot manage talents, including entry/leave/round/gain/gift/devour/sell chains.
- Battle simulator core: deterministic headless combat using board units, generated enemies, attack cadence, targeting, damage, crit, morale extra attacks/counters, timeout resolution, and HP loss.

Risk note: remaining battle steps 1-2 are the largest and may split further because original combat and skill code has many trigger-specific edge cases.
