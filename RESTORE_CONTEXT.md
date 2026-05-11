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

### 2026-05-11 - Process 22: First battle skill resolver pass

- User requested continuing after reviewing the migration context.
- Expanded `Assets/Scripts/Data/UnitDefinition.cs` skill parsing fields so imported battle skill JSON parameters are retained, including:
  - shield layers, target counts, value-per-faith, summon/transform ids
  - chance/duration/delay/reduce/ratio/radius
  - attack multipliers, stun/invincible timing, speed multipliers, forced crit flags, direct damage
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs` with a lightweight battle skill resolver inside the existing headless simulator.
- Newly handled battle-start effects:
  - team shield and self refreshing shield
  - self/team faith-count attack/stat buffs
  - adjacent-faith attack buff
  - first-attack crit/speedup setup
  - stealth first-attack crit approximation
  - speed-threshold attack interval reduction/halving
  - lowest-power ally gains source power
  - self temporary morale
  - start summons and start-plus-death summons
  - summon-and-buff-type
  - pounce nearest enemy damage/stun/invincibility approximation
  - lock highest-HP targets as a stun/lock approximation
- Newly handled attack/death effects:
  - chance self shield on attack
  - attack-count summon
  - multi-nearest-target attacks
  - chance forced crit and every-Nth forced crit
  - ally-crit self temporary power
  - death summon
  - death explosion damage approximation
- Continuous aura handling was narrowed to the configured `targetUnitId` instead of syncing every same-id unit indiscriminately.
- Known boundaries:
  - This is still a headless approximation of `BattleSkillManager.js`, not a full visual battle manager.
  - Next-round battle rewards, kill/counter milestone rewards, discover/evolve rewards, fire rain DOT, delayed snipe, true stealth targeting, mount-transform, and full post-battle state application still need dedicated migration.
  - The current death explosion next-round reward skill only applies explosion damage; its future-round attack reward is still pending.
- Verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
  - Sampled Unity Editor log search showed no current `error CS` matches after the edit; if Unity was not actively refreshing, let the Editor compile once before Play testing.

### 2026-05-11 - Process 23: Battle post-reward pending flow

- User requested continuing from the battle skill resolver pass.
- Added pending battle reward state to `Assets/Scripts/Model/RunState.cs`:
  - run-level `pendingBattleRewards`
  - next-round gold
  - next-round shop generated attack buff
  - next-round faith/race discover rewards
  - per-unit pending next-round temp attack/power
  - per-unit pending permanent HP/power/luck
  - per-unit pending forest gems
  - per-unit pending evolve target
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs` so battle runtime units retain their source `UnitCardState` and write battle results back as pending rewards.
- Added first pass post-battle reward support:
  - `on_extra_attack_once_next_round_gold`
  - `on_counter_count_next_round_gain_forest_gem`
  - `on_attack_mark_target_next_round_forest_gem_on_death`
  - `on_kill_count_next_round_evolve`
  - `battle_start_if_team_faith_count_next_round_discover`
  - `battle_start_team_temp_defense_if_win_next_round_self_hp`
  - `battle_end_survivors_next_round_team_temp_attack`
  - `on_death_explode_if_hits_next_round_team_attack`
  - `on_death_next_round_shop_cards_gain_attack`
  - `on_death_next_round_forest_gem`
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - `NextRound()` now consumes pending battle rewards after round-start temp reset.
  - next-round gold is added on top of normal round income.
  - next-round shop attack buff is added before the new shop refresh.
  - pending unit temp/permanent stat rewards are applied to board units.
  - pending forest gem rewards are added to manage resources.
  - pending evolve rewards update the unit definition id/name/star.
  - pending discover rewards add random matching units to hand.
- Expanded `Assets/Scripts/Data/UnitDefinition.cs` for `hitThreshold` and `nextRoundAttack`.
- Known boundaries:
  - Kill-count evolve currently uses this battle's kill count only; original persistent progression counters are still pending.
  - Discover rewards are added directly to hand as a simple random grant; original modal/discover-choice UX is still pending.
  - Round-start ordering may still need tuning if original behavior requires pending battle rewards to apply before manage `on_round_start` talent dispatch.
- Verification:
  - `git diff --check` reported no whitespace errors, only LF-to-CRLF warnings.
  - Sampled Unity Editor log search showed no current `error CS` matches after the edit.

### 2026-05-11 - Process 24: Complete roadmap items 1-3 first-pass

- User requested directly completing remaining roadmap items 1, 2, and 3.

#### Item 1 - Battle Skill Resolver Completion Pass

- Expanded `Assets/Scripts/Data/UnitDefinition.cs` for additional battle parameters:
  - `targetId`
  - `selfHpLoss`
  - `giftThreshold`
  - `interval`
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs` with additional battle behavior:
  - timed skill ticking before each attack step
  - summon duration expiry
  - delayed snipe/backline targeting
  - stealth-assassinate lowest-HP targeting approximation
  - mount-transform into hidden transform units
  - periodic temporary power
  - periodic self HP loss plus team attack buff
  - periodic nearby enemy damage
  - on-damaged count temporary morale
  - fire-rain attack-count AOE approximation
  - team forest-gift total AOE approximation
  - persistent kill-count evolve counters stored on source unit state
- Updated `BoardSystem` and `SynthesisSystem` to preserve/merge battle progression counters across deploy and golden synthesis.

#### Item 2 - Enemy Plan And Campaign Progression

- Added campaign progression fields to `RunState`:
  - campaign round limit
  - wins/losses
  - completed flag
  - battle history
- Updated `ProphecyGameSession.StartNewRun()` to assign a campaign round limit:
  - `south_town_adventure`: 20 rounds
  - `snow_peak_defense`: 18 rounds
  - `song_of_sang_city`: 24 rounds
- Updated enemy generation in `BattleStubSystem`:
  - campaign-specific enemy scaling
  - round-limit progress scaling
  - stronger milestone rounds every 5 rounds
  - higher max-star allowance on milestones
- Added `RunFlowController.ResolveBattleOutcome()`:
  - appends battle history
  - tracks wins/losses
  - handles gameover
  - handles campaign victory when the round limit is cleared
  - advances to next round otherwise
- Updated `RunSceneController.StartBattle()` to use the new battle outcome flow.

#### Item 3 - Save/Load Persistence

- Added `Assets/Scripts/Systems/SaveGameSystem.cs` and `.meta`.
- Saves current `RunState` as pretty JSON to:
  - `Application.persistentDataPath/prophecy_century_run.json`
- Loads and normalizes saved `RunState`, including lists and nested state that may be absent in older saves.
- Added `ProphecyGameSession.RestoreRun()` for save restoration.
- Added runtime UI actions:
  - `RunSceneController.SaveGame()`
  - `RunSceneController.LoadGame()`
  - generated bottom-bar Save and Load buttons in `RuntimeUiBootstrap`.

- Known boundaries:
  - Battle skill item 1 is now a broad first-pass resolver, but still headless and approximate compared with the original canvas battle manager.
  - Original discover-choice modal is represented as direct random hand grants.
  - Campaign definitions still do not contain authored enemy rosters, so enemy plans are deterministic procedural plans derived from campaign id, round, and milestone status.
  - Save/load currently persists one local run slot; multi-slot saves and versioned migrations are not implemented.
- Verification:
  - `git diff --check` reported no whitespace errors, only LF-to-CRLF warnings.
  - Sampled Unity Editor log search showed no current `error CS` matches.

### 2026-05-11 - Process 25: Complete roadmap items 4-5 first-pass

- User requested completing remaining roadmap items 4 and 5.

#### Item 4 - UI Parity Pass

- Confirmed `RuntimeUiBootstrap.cs` and `RunSceneController.cs` now show normal Chinese runtime strings in the current file view.
- Improved runtime UI flow and readability:
  - top shop meta now shows shop level, upgrade cost, lock state, campaign wins, and losses
  - battle preview now shows campaign progress, win/loss count, player/enemy score, pending rewards, recent battle history, and last battle summary
  - runtime log now keeps the latest 7 messages instead of replacing the log every time
  - victory run state is displayed as `胜利`
  - Save/Load buttons remain in the bottom action bar
- Improved card/board readability:
  - golden unit cards now use a distinct gold/brown card background
  - board slots now show unit portrait icons using `RuntimeUnitIconCache`
  - board slot text offsets now account for the icon area
- This keeps the first screen as the actual playable runtime, not a landing page.

#### Item 5 - Asset/Audio/Battle Presentation Polish

- Added `Assets/Scripts/UI/RuntimeSfxPlayer.cs` and `.meta`.
- `RuntimeSfxPlayer` loads imported MP3 files from `Assets/Audio` on demand and caches clips.
- Runtime UI now creates a dedicated SFX audio source.
- Button clicks now play a light imported audio cue.
- Battle result now plays imported victory/defeat audio:
  - victory: `Win Battle.mp3`
  - defeat: `LoseCombat.mp3`
- Save/load feedback now plays imported success/failure audio cues.
- Existing title background, unit icons, feature icons, and looped manage BGM remain bound from earlier processes.

- Known boundaries:
  - UI is now clearer and more complete, but still a runtime-generated Unity UI rather than a scene-authored replica of the original Web/Electron layout.
  - Battle presentation is still headless; audio and result/history presentation are bound, but no animated battle field or floating combat text has been authored yet.
  - Imported art/audio are used in the primary runtime loop, but not every original effect has a one-to-one binding.
- Verification:
  - `git diff --check` reported no whitespace errors, only LF-to-CRLF warnings.
  - Sampled Unity Editor log search showed no current `error CS` matches.

### 2026-05-11 - Process 26: Formal UI Direction Planning

- User asked what should be done next and said they want the UI to look more formal.
- User explicitly requested not to rush into code changes and to update this context document first.

#### Current UI Assessment

- The game now has a playable runtime-generated Unity UI with title selection, shop, hand, board, battle preview, logs, save/load, icons, BGM, and SFX.
- The current UI is still best understood as a functional migration/debug UI, not a formal product UI.
- Main reasons it still feels informal:
  - layout is generated entirely in code and uses mostly flat panels
  - information density is not yet prioritized by player task flow
  - typography, spacing, hierarchy, and button grouping are utilitarian
  - card presentation is readable but not yet polished enough for a card-battler/auto-battler
  - battle preview is text-heavy and does not yet feel like a dedicated battle/reward surface
  - title/manage/battle/result flows are present but not yet visually differentiated enough

#### Recommended Next Priority

The next best step should be **UI foundation and interaction design**, before adding more gameplay systems.

Recommended first UI task:

1. Create a formal runtime UI layout spec for the main manage screen.
   - Define stable regions:
     - top status bar
     - left shop column
     - center hand and board area
     - right run/battle/history panel
     - bottom action bar
   - Define visual hierarchy:
     - primary action: start/resolve battle
     - economy actions: refresh, upgrade, lock
     - card actions: buy, deploy, sell
     - utility actions: save, load, new run
   - Define card component rules:
     - unit portrait
     - name/star/gold state
     - race/faith/type
     - attack/hp/defense/power/speed
     - action buttons
     - selected/drag/empty/sold states
   - Define board slot states:
     - empty
     - selected empty
     - occupied
     - selected occupied
     - valid drop target
     - invalid drop target

Recommended second UI task:

2. Replace the current bottom action strip with a more formal command bar.
   - Group shop commands together.
   - Keep battle as the dominant button.
   - Move save/load/new-run to a utility group.
   - Reduce button crowding at 1600x900 and common laptop aspect ratios.

Recommended third UI task:

3. Redesign the right panel into tabs or stacked sections.
   - Run summary
   - Battle preview
   - Rewards/pending effects
   - Recent battle history
   - Log

Recommended fourth UI task:

4. Upgrade unit cards and board cells.
   - Use consistent card height and icon sizing.
   - Make gold cards visibly premium but not noisy.
   - Add compact stat chips or aligned stat rows.
   - Avoid text overlap and truncation at smaller Game View sizes.

Recommended fifth UI task:

5. Only after the manage screen is formalized, do title/result/battle presentation polish.
   - Title screen can use imported background more intentionally.
   - Battle result should become a clear result panel instead of only log text.
   - Animated battle field/floating combat text can come after layout stability.

#### Proposed UI Style Direction

- Treat the game as a fantasy strategy/card-battler management interface.
- Prefer a polished tactical dashboard over a decorative landing-page feel.
- Visual tone:
  - dark neutral base
  - restrained gold accents for economy/golden units
  - muted blue/steel panels for system surfaces
  - clear red/green feedback only for damage/loss/victory states
- Avoid:
  - oversized hero marketing sections
  - excessive gradients or decorative blobs
  - text-heavy buttons where icons or grouped controls are clearer
  - nested cards inside cards
  - one-note color palettes

#### Implementation Guidance For The Next Coding Pass

- Do not start by rewriting every UI surface.
- Start with the manage screen because it is the highest-frequency player surface.
- Keep `RuntimeUiBootstrap` if speed matters, but consider splitting generated UI helpers into smaller builder methods/classes before adding more polish.
- If the UI grows further, consider moving toward scene/prefab-authored UI for maintainability.
- Before editing code, capture the target layout in this document or a separate `UI_FORMALIZATION_PLAN.md`.
- After UI edits, verify in Play Mode at minimum:
  - 1600x900
  - 1366x768
  - 1920x1080
  - a narrow-ish Game View size
  - no button/card text overlap
  - card icons and action buttons remain readable
  - battle preview/log/history do not crowd each other

#### Immediate Recommendation

The next concrete action should be:

1. Draft `UI_FORMALIZATION_PLAN.md` with a manage-screen wireframe and component rules.
2. Then implement only the manage screen layout refactor.
3. Then Play Mode screenshot/check the layout before touching title or battle-result visuals.

### 2026-05-11 - Process 27: UI formalization plan and first layout pass

- Added `UI_FORMALIZATION_PLAN.md`.
- The plan defines:
  - target manage-screen regions: top status bar, left shop column, center hand/board area, right intelligence panel, bottom command bar
  - visual direction: dark neutral base, muted steel panels, restrained gold accents, clear red/green feedback only where meaningful
  - command grouping: shop economy, primary battle, and utility groups
  - card component rules, board slot states, right-panel sections, implementation passes, and Play Mode verification checklist
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs` first-pass manage layout:
  - adjusted main region proportions to give the command bar and right panel more deliberate structure
  - renamed the bottom runtime surface to `CommandBar`
  - split the right panel into `RunInfoSection`, `BattlePreviewSection`, and `LogSection`
  - replaced the single crowded bottom strip with grouped command panels:
    - `ShopCommandGroup`: refresh, upgrade, lock, quick buy
    - `BattleCommandGroup`: dominant battle button plus quick deploy
    - `UtilityCommandGroup`: save, load, new run
- Verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
  - Unity Play Mode visual verification is still pending and should be the next step.

### 2026-05-11 - Process 28: Pivot UI layout toward original HTML manage screen

- User clarified the goal is to restore the previous HTML/Web/Electron UI layout.
- Compared Unity runtime UI against original source files:
  - `work_v1_final/index.html`
  - `work_v1_final/style.css`
  - `work_v1_final/Game.js`
- Key original manage-screen layout found:
  - `status-bar` at the top contains economy/status and core buttons
  - `shop-area` is a horizontal full-width panel below the top bar
  - `board-area` sits below the shop on the left
  - `hand-area` sits below the shop on the right
  - `combat-log` sits below the hand area on the right
- Updated `UI_FORMALIZATION_PLAN.md` so the target is now HTML layout restoration rather than a generic formal dashboard.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - moved main command buttons back into the top status/action bar, matching the original HTML direction
  - replaced the left-shop/center/right-panel structure with HTML-like panels:
    - `ShopPanel` full-width top shop strip
    - `BoardPanel` lower-left formation area
    - `HandPanel` lower-right hand area
    - `CombatLogPanel` lower-right log/info area
  - added horizontal shop card root helper and grid hand card root helper
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - shop cards now use fixed card dimensions suitable for horizontal display
  - hand cards now use fixed grid-card dimensions
  - compact card action buttons now anchor at the bottom of shop/hand cards instead of right-side list buttons
- Verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
  - Unity Play Mode visual verification is still pending; this pass is a structural layout pass and may need spacing/font tuning after seeing the actual Game View.

### 2026-05-11 - Process 29: HTML layout spacing and slot parity pass

- User confirmed Unity reported no errors after the HTML-layout pass and requested continuing.
- Continued matching original HTML manage-screen proportions:
  - original board is a horizontal set of vertical columns, not a row list
  - original board cells are square, roughly `88x88`
  - original shop always displays six shop slots
  - original hand area keeps visible empty slots, at least nine positions
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - added `CreateBoardGridRoot()` so the board root uses a horizontal layout suitable for column-based board rendering
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - `RebuildBoardSlotGrid()` now renders the original `4-3-2-1` column layout:
    - column 1: `4-1`, `4-2`, `4-3`, `4-4`
    - column 2: `3-1`, `3-2`, `3-3`
    - column 3: `2-1`, `2-2`
    - column 4: `1-1`
  - board cells now use fixed `88x88` dimensions
  - board unit icons are centered in the cell and larger
  - board cell text is compacted to name/star/attack/defense so it fits the smaller original-style square
  - shop card lists now render at least six slots
  - hand card grids now render at least nine slots
  - empty card slots use a subdued background and no actions
- Verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
  - Unity Editor log showed a fresh `Tundra build success` with no sampled `error CS`.
  - Play Mode visual verification is still needed for card text overlap and board readability.

## Next Suggested Work

Recommended next steps:

1. Play Mode visual check the manage screen against the original HTML layout: top status/action bar, horizontal shop strip, lower-left board, right hand, right log.
2. Tune card spacing/fonts after the first Play Mode visual check, especially shop card text, hand grid buttons, and board cell readability.
3. Add the original HTML enemy preview panel above/near the board once the manage layout is stable.
4. Play test the full run loop after card/board polish: title selection, buy/deploy/sell, save, load, several battles, milestone round, gameover, and campaign victory.
5. Verify audio playback in Play Mode: manage BGM, button click, battle victory/defeat, save/load.
6. Compare representative original units against Unity behavior and tune approximations where gameplay diverges too much.

## Remaining Migration Roadmap Estimate

As of 2026-05-11 after Process 29, roadmap items 1-5 have first-pass implementations. The next priority is original HTML UI layout restoration verification and card/board polish, then QA and tuning:

1. Formal UI foundation: manage-screen layout spec, command grouping, card components, board slot states, right-panel information hierarchy.
2. Play Mode regression: full run loop, save/load restore, campaign victory/gameover, and battle-heavy unit teams.
3. Combat tuning: compare original combat edge cases, campaign difficulty, and unit-specific skill behavior.
4. Optional exact-parity authoring: scene-authored UI, authored campaign rosters, animated battle field, floating combat text, and one-to-one SFX mapping.

Completed from the previous roadmap:

- Board placement parity: selectable board slots, drag/rearrange, swapping, and original no-overwrite hand deployment rule.
- Unit lifecycle parity: shop pool limit accounting, generated shop buffs, buy/deploy/sell state transfer, special sell price, and pool refunds.
- Synthesis parity: three-copy merge, golden unit creation, inherited stats, pool contribution carryover, and post-buy/post-deploy auto-synthesis.
- Manage-phase skill parity: event bus and handlers for current one-shot manage talents, including entry/leave/round/gain/gift/devour/sell chains.
- Battle simulator core: deterministic headless combat using board units, generated enemies, attack cadence, targeting, damage, crit, morale extra attacks/counters, timeout resolution, and HP loss.
- Battle skill first pass: battle-start shields/summons/buffs/pounce/locks, attack shields/summons/multi-target/crit, death summons/explosions, and target-id attack sync aura.
- Battle post-reward first pass: next-round gold, shop buffs, temp/permanent unit rewards, forest gems, discover grants, evolve grants, death-triggered future rewards.
- Battle resolver completion pass: timed skills, summon durations, delayed snipe, stealth targeting approximation, mount transform, periodic effects, on-damaged triggers, fire-rain/gift AOE, and persistent kill-evolve counters.
- Enemy/campaign progression first pass: procedural enemy plan, milestone rounds, campaign round limits, victory/gameover flow, and battle history.
- Save/load persistence first pass: one local JSON save slot, run restore, and runtime Save/Load UI buttons.
- UI parity first pass: readable Chinese runtime UI, richer battle preview/history/reward text, rolling log, victory state, golden-card styling, board portraits.
- Asset/audio polish first pass: runtime SFX loader, button audio, battle result audio, save/load feedback audio, retained title art/BGM/icon bindings.
- Formal UI direction planning: next step should be a manage-screen UI plan/spec before code changes, then focused layout refactor and Play Mode visual verification.
- UI formalization first layout pass: `UI_FORMALIZATION_PLAN.md`, grouped command bar, and right-side section split.
- HTML manage layout pivot: top action/status bar, horizontal shop strip, lower-left board, right hand area, and right combat log/info area.
- HTML slot/board parity pass: six shop slots, nine hand slots, and original `4-3-2-1` board column layout with square cells.

Risk note: remaining battle steps 1-2 are the largest and may split further because original combat and skill code has many trigger-specific edge cases.
