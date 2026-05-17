# Prophecy Century Unity Restore Context

Last updated: 2026-05-17

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

### 2026-05-12 - Process 30: Runtime unit tooltip restoration

- User reported hovering units does not show tips.
- Confirmed Unity runtime UI had drag/drop pointer handlers but no tooltip component.
- Added `Assets/Scripts/UI/RuntimeUnitTooltip.cs` and `.meta`.
- Tooltip behavior:
  - implements `IPointerEnterHandler`, `IPointerMoveHandler`, and `IPointerExitHandler`
  - creates one runtime tooltip panel under the canvas on first hover
  - follows the mouse pointer with screen-edge clamping
  - hides when pointer exits the unit surface
- Tooltip content currently shows:
  - unit name, star, and golden state
  - race / faith / type
  - attack / HP / defense / power / speed
  - shop-buffed attack/HP/defense values
  - management talent text
  - battle skill text
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - shop cards and hand cards receive `RuntimeUnitTooltip`
  - occupied board slots receive `RuntimeUnitTooltip`
- Verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warning for `RunSceneController.cs`.
  - Unity Editor has not yet logged a fresh compile after this new script in the sampled tail; let Unity refresh and check Play Mode hover.

## Next Suggested Work

Recommended next steps:

1. Let Unity refresh/compile the new `RuntimeUnitTooltip.cs`, then Play Mode check hover tips on shop cards, hand cards, and board units.
2. Play Mode visual check the manage screen against the original HTML layout: top status/action bar, horizontal shop strip, lower-left board, right hand, right log.
3. Tune card spacing/fonts after the first Play Mode visual check, especially shop card text, hand grid buttons, board cell readability, and tooltip size/position.
4. Add the original HTML enemy preview panel above/near the board once the manage layout is stable.
5. Play test the full run loop after card/board polish: title selection, buy/deploy/sell, save, load, several battles, milestone round, gameover, and campaign victory.
6. Verify audio playback in Play Mode: manage BGM, button click, battle victory/defeat, save/load.
7. Compare representative original units against Unity behavior and tune approximations where gameplay diverges too much.

## 2026-05-13 - Unit Skill Completion Worklist

Goal: close the currently identified unit-skill gaps before the next broader combat tuning pass.

### Scope

1. Continuous aura skills
   - `while_on_board_per_ally_id_buff_type_attack`
     - representative unit: `water_elemental` / 水元素
   - `while_on_board_race_threshold_team_speed`
     - representative unit: `wind_elemental` / 风元素
2. Missing battle skill handlers
   - `first_hits_counterattack`
     - representative unit: `stubborn_apprentice` / 犟嘴学徒
   - `on_ally_death_tagged_units_temp_power`
     - representative unit: `academy_gardener` / 学院园丁
   - `on_attack_count_formula_aoe`
     - representative unit: `martial_master` / 武学大师

### Acceptance Criteria

- Each scoped `kind` has an explicit runtime implementation path.
- Behavior is integrated into the existing resolver style instead of introducing parallel one-off systems.
- Skill-kind coverage audit no longer reports these five items as unresolved.
- `RESTORE_CONTEXT.md` is updated after each completed milestone with current progress and any remaining validation notes.

### Progress

- [x] Worklist drafted.
- [x] Continuous aura skills implemented.
- [x] Missing battle skill handlers implemented.
- [x] Static verification and skill-kind diff rerun.
- [ ] Unity refresh / Play Mode validation notes captured after code completion.

### 2026-05-13 - Skill Completion Implementation Pass

- Expanded `Assets/Scripts/Data/UnitDefinition.cs` skill parsing with:
  - `deadTag`
  - `allyId`
  - `repeat`
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs`:
  - implemented `while_on_board_per_ally_id_buff_type_attack`
    - current representative unit: 水元素
    - implemented as a recalculated continuous attack aura so bonuses do not stack every simulation tick
  - implemented `while_on_board_race_threshold_team_speed`
    - current representative unit: 风元素
    - implemented as a recalculated continuous team speed aura with attack interval refresh
  - implemented `first_hits_counterattack`
    - current representative unit: 犟嘴学徒
    - triggers forced retaliation on the first configured number of damaging hits, honoring `repeat`
  - implemented `on_ally_death_tagged_units_temp_power`
    - current representative unit: 学院园丁
    - when a tagged ally dies, surviving tagged allies gain temporary battle power
  - implemented `on_attack_count_formula_aoe`
    - current representative unit: 武学大师
    - counts attacks and applies radius-based splash damage around the struck target
- Static verification:
  - skill-kind diff rerun now reports:
    - `MANAGE_UNHANDLED=0`
    - `BATTLE_UNHANDLED=0`
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Remaining validation:
  - Unity should refresh scripts and compile this code.
  - Play Mode should specifically check:
    - 水元素 / 风元素 aura application and removal after deaths
    - 犟嘴学徒 retaliation count and golden repeat count
    - 学院园丁 death-trigger timing
    - 武学大师 splash radius for normal and golden forms

### 2026-05-13 - Manage Screen UI Structure Assembly

- User provided a new target structure and reference image for the operating-phase main screen.
- The new target replaces the prior top-bar / side-log emphasis with a five-region composition:
  - left player information panel
  - central board formation panel
  - right-side shop panel
  - bottom hand panel
  - bottom-right primary battle action panel
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - hides the prior runtime manage layout panels at runtime
  - assembles the new five-region layout on top of the run surface
  - adds a player portrait surface, HP bar with fill image, currency/state labels, and a `2 x 3` resource slot grid
  - moves round display into the board panel
  - rebuilds the shop as a right-side `2 x 3` card area with shop-level stars and grouped shop buttons
  - rebuilds the hand area as a bottom single-row card strip
  - creates a dedicated bottom-right large `开战` action button
  - preserves save/load through compact utility buttons in the player area
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - wires HP bar fill updates from current run HP
  - changes round display to `第 X 回合`
  - changes shop meta to a star-style shop-level line
  - adjusts shared shop/hand card text toward the provided layout:
    - star row
    - unit name
    - attack / power line
    - race / profession / faith tags
  - keeps summary text fields optional so the new layout is not forced to display old debug summaries
  - expands shared card icon/text spacing for the larger new card surfaces
  - differentiates minimum visible counts for shop cards (`6`) and hand cards (`9`)
  - enlarges the board formation cells to better support richer board-unit presentation
- Static verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Remaining validation:
  - Unity refresh / compile is still required.
  - Play Mode should verify:
    - panel proportions at the current reference resolution
    - no card overlap in shop / hand rows
    - board formation readability after larger cells
    - new HP bar fill behavior
    - shop and battle buttons still invoke the existing flow correctly

### 2026-05-13 - UI Reference Resolution Upgrade

- User requested moving the game UI reference size to `2560x1280`.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - `CanvasScaler.referenceResolution`
    - from `1600x900`
    - to `2560x1280`
  - title-screen dropdown anchor conversion baseline
    - from `1800x900`
    - to `2560x1280`
- Updated `UI_FORMALIZATION_PLAN.md`:
  - new intended reference layout is `2560x1280`
  - `1600x900` is retained only as a downscaled compatibility check
- Implication for upcoming pixel-alignment work:
  - the attached reference image should now be matched against a `2560x1280` Unity canvas baseline
  - future absolute offsets, control sizing, and screenshot comparisons should all use this resolution unless changed again
- Static verification:
  - code and planning docs now reference the new resolution baseline consistently where updated.

### 2026-05-13 - Pixel Alignment Pass And Screenshot Deviation Table

- User requested completing all three pixel-alignment stages:
  1. lock the overall five-region frame to the reference image pixel proportions
  2. align cards, buttons, and board-unit nodes
  3. produce a Unity screenshot deviation/correction table
- Stage 1 implementation:
  - added pixel-rect placement helper in `RuntimeUiBootstrap.cs`
  - locked the main five regions against the `2560x1280` reference image:
    - player panel: `x=22 y=24 w=418 h=862`
    - board panel: `x=454 y=24 w=1260 h=862`
    - shop panel: `x=1727 y=24 w=806 h=862`
    - hand panel: `x=22 y=898 w=2131 h=357`
    - battle button area: `x=2198 y=947 w=335 h=261`
  - locked major child regions:
    - hero portrait
    - HP bar
    - resource grid
    - shop grid
    - hand grid
    - round label
    - shop buttons
    - large battle button
- Stage 2 implementation:
  - card grid cell size for shop and hand is now `221x286`, matching the reference card footprint.
  - shop buttons are locked to `221x104`.
  - battle button is locked to `335x261`.
  - board layout now uses explicit pixel positions for the original `4-3-2-1` formation instead of layout-group columns.
  - board slots are positioned inside the board panel at:
    - `4-1`: `0,0`
    - `4-2`: `0,198`
    - `4-3`: `0,396`
    - `4-4`: `0,594`
    - `3-1`: `288,94`
    - `3-2`: `288,292`
    - `3-3`: `288,490`
    - `2-1`: `576,218`
    - `2-2`: `576,416`
    - `1-1`: `864,312`
  - card icons/text were enlarged to match the larger reference-card scale.
- Stage 3 screenshot deviation table from the user-provided Unity screenshot:

| Area | Observed Deviation | Correction Applied / Required |
| --- | --- | --- |
| Whole screen | Screenshot aspect appears closer to `16:9`, not `2560x1280` / `2:1`; black padding appears around the UI. | Required: set Unity Game View to exact `2560x1280` before judging pixel match. |
| Canvas scaling | UI was being scaled with width/height midpoint matching, which can drift in non-2:1 Game Views. | Applied: `CanvasScaler.matchWidthOrHeight = 0`, so width is the fixed pixel baseline. |
| Left top text | Old campaign/hero text leaked into the top-left edge as narrow wrapped text. | Applied: hidden the compact campaign/hero labels in the pixel-aligned manage layout. |
| Five main panels | Overall regions are close structurally but cannot be judged against the reference while Game View aspect is wrong. | Required: retake screenshot at exact `2560x1280`; then measure per-panel deltas. |
| Shop grid | Current screenshot only shows three populated cards plus empty/sold slots because runtime data had only three available cards. | Data-dependent; layout now reserves the same `2 x 3` footprint. |
| Hand row | Empty hand placeholders are very tall/dark compared with reference cards because hand runtime data is empty. | Data-dependent; layout reserves `9` card slots at reference footprint. |
| Board nodes | Screenshot still showed old empty-slot rectangles rather than art-backed unit nodes because board was mostly empty. | Layout now uses explicit pixel node positions; Play Mode with deployed units is needed for final visual tuning. |

- Remaining validation:
  - Retake Play Mode screenshot with Game View set to exact `2560x1280`.
  - Use a test state with:
    - 6 shop units
    - 9 hand cards
    - several deployed board units
  - Then compare actual screenshot to the reference image and perform a second numeric delta pass.

### 2026-05-13 - Visible Runtime Feedback Restoration

- Continued the manage-screen migration without running Unity batchmode, because a second Unity process can hang when the Editor already has the project open.
- Fixed the new five-region runtime UI wiring so core feedback is visible in the current main screen:
  - added `BattlePreviewTextV2` to the bottom of the board panel
  - added `LogTextV2` next to the battle preview in the board panel
  - wired `RunSceneController.logLabel` to `LogTextV2` instead of the hidden old log panel
  - wired `RunSceneController.battlePreviewText` to `BattlePreviewTextV2` instead of `null`
  - restored visible player gold and run-state labels in the left player panel
  - restored visible save/load buttons in the left player panel
  - moved the resource grid down slightly so it does not cover the restored gold/state labels
- Adjusted compact visible text:
  - rolling log now keeps four recent entries to fit the pixel-aligned board footer
  - battle preview now uses a shorter summary line for progress, score, pending rewards, and latest history
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Validation still pending:
  - Let the already-open Unity Editor refresh scripts and confirm Console has no compile errors.
  - In Play Mode, confirm the board footer shows battle preview and recent log after buying/deploying/fighting.
  - Confirm save/load buttons are clickable at the restored left-panel positions.

### 2026-05-13 - Code Complete / Playtest Ready Tooling

- Added editor-only runtime playtest tooling:
  - `Assets/Scripts/UI/RuntimePlaytestTools.cs`
  - `Assets/Scripts/UI/RuntimePlaytestTools.cs.meta`
- `RuntimeUiBootstrap.cs` now attaches `RuntimePlaytestTools` to `RuntimeCanvas` only under `UNITY_EDITOR`, so the helper is not part of player builds.
- Playtest hotkeys in Play Mode:
  - `F9`: seed a full UI playtest state
    - shop level 6
    - 6 visible shop cards
    - 9 visible hand cards
    - 5 deployed board units across the original board slots
    - enough gold/HP to test buy/deploy/sell/save/load/battle interactions
  - `F10`: resolve one battle using the current board and refresh the UI
  - `F8`: force-refresh the current runtime view
- The playtest seed uses representative units that exercise the current UI and battle skill paths:
  - shop: `small_merchant`, `bright_warrior`, `elf`, `fire_elemental`, `forest_guard`, `ger_beast`
  - hand: `blacksmith`, `monk`, `knight`, `assassin`, `priest`, `wanderer`, `water_elemental`, `forest_scout`, `caller`
  - board: `stubborn_apprentice`, `academy_gardener`, `martial_master`, `wind_elemental`, `water_elemental`
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Current Code Complete / Playtest Ready definition:
  - code-side migration hooks are in place for the main run loop, manage UI, board interactions, battle preview/log visibility, hover tooltip, save/load, and representative seeded playtest states
  - next required work should happen in the already-open Unity Editor Console and Play Mode, not through another batchmode Unity process

#### Playtest Ready Checklist

Use the already-open Unity Editor. Do not start a second Unity batchmode process.

1. Let Unity refresh scripts and confirm the Console has no compile errors.
2. Enter Play Mode and start a run from the title screen.
3. Press `F9`.
4. Verify:
   - 6 shop cards render in the right panel
   - 9 hand cards render in the bottom hand strip
   - 5 board units render on the board
   - player HP/gold/state are visible in the left panel
   - save/load buttons are visible and clickable
   - battle preview and log are visible at the bottom of the board panel
5. Click/drag:
   - buy one shop card
   - deploy one hand card
   - move or swap one board unit
   - sell one hand or board unit
6. Hover shop, hand, and board units and confirm tooltip content appears.
7. Press `F10` and confirm:
   - battle resolves without Console errors
   - battle result appears in the log/preview
   - next round returns to manage state unless victory/gameover is reached
8. Save, change the board/hand state, load, and confirm the saved state restores.

### 2026-05-13 - UI Clarity And Hidden Text Fix

- User Play Mode screenshot showed:
  - text rendered blurry in the Game view
  - a vertical stack of incorrect text at the upper-left of the run UI
- Root cause for the upper-left text:
  - `CampaignTextV2` and `HeroTextV2` were hidden by setting their rects to `0 x 0`
  - generated uGUI `Text` used `VerticalWrapMode.Overflow`, so the text still drew outside the zero-size rect
- Applied fixes in `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - `CampaignTextV2` and `HeroTextV2` now use `gameObject.SetActive(false)` instead of zero-size hiding
  - default generated text now uses `VerticalWrapMode.Truncate` to avoid off-rect overflow leaks
  - `CanvasScaler.dynamicPixelsPerUnit` increased from `3` to `8` to improve dynamic font texture sharpness
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Remaining visual note:
  - The screenshot Game tab was using `Free Aspect` and `Scale 2x`.
  - For real UI clarity judgment, set Game view to a fixed 2:1 resolution such as `2560x1280` or `1920x960`, and set Game view scale to `1x`.
  - Editor zoom/scaling can blur the entire rendered output even when runtime text generation is correct.

### 2026-05-13 - Runtime Card Encyclopedia Migration

- User requested adding a `图鉴` button in the upper-right shop area and migrating encyclopedia functionality.
- Added:
  - `Assets/Scripts/UI/RuntimeEncyclopediaPanel.cs`
  - `Assets/Scripts/UI/RuntimeEncyclopediaPanel.cs.meta`
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - attaches `RuntimeEncyclopediaPanel` to `RuntimeCanvas`
  - adds `EncyclopediaButtonV2` in the shop panel header, positioned at `x=648 y=17 w=121 h=88`
  - slightly narrows `ShopMetaTextV2` so the shop level line and encyclopedia button do not overlap
- Implemented Unity runtime encyclopedia features:
  - full-screen overlay panel
  - all units from loaded `unit_data.json`, including hidden/derived units
  - default sorting by star, then race, then name
  - card grid with portrait, star row, name, stats, race/type/faith tags
  - click card to show detail
  - detail panel with base stats, manage talent text, gold manage talent text, battle skill text, and gold battle skill text
  - cycle filters for race, faith, type, and star
  - reset filters
  - close button
- Current scope boundary:
  - This is a runtime uGUI migration of the original encyclopedia data browsing and card-detail behavior.
  - Original web encyclopedia related-card link sections are not yet fully reproduced as clickable relation rows; the critical card list/filter/detail path is present.
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - click `图鉴` in the shop header
  - confirm all cards render and scroll
  - cycle each filter and reset
  - click several cards, including hidden/derived cards, and verify detail text
  - confirm `关闭` returns to the manage screen

### 2026-05-14 - Shop Click Purchase And Drag-To-Sell Interaction

- User clarified expected original-style interactions:
  - buying from shop should happen by clicking the shop card surface itself
  - do not create a separate purchase button on each shop card
  - selling should happen by dragging a hand card or board unit into the shop/sell area and releasing
  - while dragging a sellable unit, the shop area should change into a sell drop zone
- Added:
  - `Assets/Scripts/UI/RuntimeSellDropTarget.cs`
  - `Assets/Scripts/UI/RuntimeSellDropTarget.cs.meta`
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - shop cards now bind their whole card button to `BuyShopCard(index)`
  - shop cards no longer create per-card `购买` buttons
  - hand cards no longer create per-card `出售` buttons
  - board units no longer create small `出售` buttons
  - dragging a hand card or board unit now calls `RebuildShopAsSellArea()`
  - sell area text currently shows `拖动到此处出售单位 / 售价 1 金币`
  - dropping on the sell area sells the dragged hand card or board unit
  - ending a drag restores the normal shop grid
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - click a shop card body and confirm it buys the card
  - confirm shop cards no longer display a separate purchase button
  - drag a hand card and confirm the shop grid changes into the sell drop zone
  - release the hand card on the sell zone and confirm it sells
  - drag a board unit and confirm the same sell behavior
  - release outside the sell zone and confirm the normal shop grid returns

### 2026-05-14 - First Visual Battle Stage

- User requested starting the third step: replacing instant battle settlement with an actual battle phase.
- Implemented first-pass visual battle playback without rewriting the battle resolver:
  - `Assets/Scripts/UI/RuntimeUiBootstrap.cs`
    - adds `BattleStagePanel` as a full-screen overlay under the run panel
    - adds battle title, status text, log text, progress bar, player unit root, and enemy unit root
    - wires these objects into `RunSceneController`
  - `Assets/Scripts/UI/RunSceneController.cs`
    - `StartBattle()` now starts a coroutine instead of resolving immediately
    - coroutine enters battle phase, shows the battle overlay, renders player and enemy preview units, advances a progress bar, displays staged battle text, then calls the existing `BattleStubSystem.Resolve()`
    - after a short result display, the overlay hides and the normal run view refreshes
  - `Assets/Scripts/UI/RuntimePlaytestTools.cs`
    - `F10` now calls `RunSceneController.StartBattle()` so the hotkey uses the visual battle flow
- Current scope boundary:
  - This is a visual phase bridge, not a tick-perfect combat replay.
  - The resolver still performs headless combat at the end of the short playback.
  - Enemy units shown are deterministic preview units selected from current round/star rules, not the exact private enemy runtime list from `BattleStubSystem`.
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - click `开战` and confirm the battle overlay appears
  - confirm both player and enemy unit rows render
  - confirm progress text and progress bar advance
  - confirm result text appears, battle result audio plays, and the UI returns to manage view
  - press `F10` after `F9` and confirm it uses the same visual battle flow

### 2026-05-14 - Battle Result Unit Snapshots

- Continued improving the visual battle stage from a simple bridge toward useful battle feedback.
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs`:
  - `BattleStubResult` now carries `PlayerUnits` and `EnemyUnits`
  - added public `BattleUnitSnapshot`
  - snapshots include:
    - unit id/name/star
    - golden flag
    - slot id
    - max/current HP
    - damage done
    - kills
    - summoned flag
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - after resolver completion, the battle overlay rebuilds both unit rows from the actual battle result snapshots
  - result units show HP, damage, and kills
  - result text now includes total player/enemy damage and player HP loss/no-loss status
  - result display duration increased slightly so the user can read the battle outcome
- Current scope boundary:
  - this still does not replay every attack tick
  - it now displays actual post-battle unit data instead of only a generic summary
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - start a battle and confirm the pre-battle unit preview appears
  - wait for result and confirm unit rows update to HP / damage / kills
  - confirm summary includes total damage and HP delta
  - confirm the result remains visible long enough, then returns to manage view

### 2026-05-14 - Battle Event Playback Foundation

- User asked to proceed with the three-step battle restoration plan:
  1. battle event stream foundation
  2. UI event playback
  3. skill events and original-parity tuning pass
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs`:
  - `BattleStubResult` now carries `Events`
  - added public `BattleEvent`
  - battle resolution records start, attack, damage, critical damage, shield block, immunity, death, summon, skill, victory, and defeat events
  - core attack/counter/morale-extra attack paths now pass event context through the resolver
  - major skill damage paths now emit visible events:
    - battle-start pounce
    - attack-count summon
    - multi-target/area/fire-rain/gift attacks
    - death summon and death explosion
    - delayed snipe
    - periodic attack
  - event output is capped to avoid huge replay logs on long fights
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - battle stage now resolves the fight into an event list and plays those events through the overlay
  - progress bar advances by event playback instead of fixed placeholder lines
  - battle log displays rolling attack/damage/skill/death/result text
  - large event lists are sampled while preserving death and final result events, keeping playback duration usable
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed in the already-open Unity Editor:
  - press `F9`, then `F10`
  - confirm the battle overlay shows `战斗回放 x/y`
  - confirm attack/damage/death/skill lines scroll before the final result
  - confirm the final unit rows still show HP / damage / kills
  - confirm the UI returns to the manage screen after the result delay

### 2026-05-14 - First Positional Battle Presentation

- User requested restoring battle presentation where both sides begin from their board positions, move toward each other, and fight.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - battle stage now rebuilds player and enemy combatants as positioned fighter cards instead of only horizontal rows during playback
  - player units spawn on the left side using their board slot id
  - enemy units spawn on the right side using mirrored slot ids from the generated battle result
  - battle start animates both sides forward toward the center before event playback begins
  - attack and skill events make the source unit lunge toward the opposing side
  - damage and critical damage events flash the target and update its HP label
  - death events mark the target as defeated by greying and shrinking the unit card
  - final result still switches back to the readable HP / damage / kills result rows
- Current scope boundary:
  - this is a fast uGUI card-based battlefield presentation, not final sprite/body animation
  - units now move from positional left/right formations and react to events, but do not yet pathfind or play per-skill visual effects
  - summon events are visible in the event log; spawned summoned-unit card insertion during playback is still pending
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - press `F9`, then `F10`
  - confirm units appear left/right based on slots
  - confirm both sides move toward the middle before the log replay
  - confirm attacks make units lunge and damage/death updates are visible
  - confirm final result rows remain readable

### 2026-05-14 - Realtime Battle Presentation Pass

- User clarified that the previous event playback still looked like turn-based combat and should behave like realtime battle.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - battle playback now runs on a realtime timeline instead of waiting for each event one by one
  - `BattleEvent.Time` drives when attacks, damage, skills, deaths, and result messages trigger
  - all fighter cards update movement every frame during playback
  - movement speed is read from `UnitDefinition.speed`, using the original web-side movement scale pattern (`max(45, speed * 8.4)`) with a Unity UI multiplier
  - engagement distance is derived from `UnitDefinition.range` and `UnitDefinition.size`
  - melee units move closer to the center/front line
  - ranged units hold farther back according to their range value
  - attack/skill/damage/death visual feedback now happens as non-blocking coroutines while movement continues
  - status text now reports `即时战斗 x/y` during playback
- Current scope boundary:
  - this is still a uGUI card presentation, not the final canvas/sprite battlefield from the original web implementation
  - target selection for movement is approximate and visual-only; authoritative combat is still resolved by `BattleStubSystem`
  - projectile visuals for `range > 1` are not yet implemented
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - press `F9`, then `F10`
  - confirm status shows `即时战斗`
  - confirm units continue moving while events fire
  - confirm high-range units stop farther back than melee units
  - confirm faster units visibly reach their engagement positions sooner

### 2026-05-14 - Visual Realtime Simulation Loop

- User confirmed the original web battle should be restored as realtime combat, not event playback.
- Rechecked original source:
  - `work_v1_final/BattleManager.js`
  - `work_v1_final/BattleUnit.js`
  - original update loop finds targets every frame, moves units until in range, attacks when cooldown reaches zero, resolves collisions, projectiles, effects, and floating text
  - original movement uses `max(45, speed * 8.4)`
  - original attack range uses `range * 60 + unit.size + target.size`
  - `speed` affects movement, `attackInterval` affects attack frequency, `range` affects engagement distance
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs`:
  - `BattleUnitSnapshot` now includes attack, defense, power, speed, range, size, and attack interval
  - these values reflect battle-start skill/aura adjustments from the resolver snapshot
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - replaced the event-timeline playback loop with a UI-side visual realtime simulation loop
  - fighter cards now have visual HP, attack, defense, power, speed, range, size, attack interval, and attack timer
  - every frame:
    - alive units find the nearest living opponent
    - move toward target if outside `range * 60 + size + target.size`
    - attack when inside range and attack timer is ready
    - decrement attack timers
    - apply visual damage and death
    - resolve simple unit collision separation
  - visual movement follows the original movement scale pattern with a Unity UI multiplier
  - visual damage uses the original-style defense factor formula:
    - `attack * (1 - defense / (defense + power))`
- Current scope boundary:
  - authoritative combat result still comes from `BattleStubSystem.Resolve()`, so this is a visual realtime simulation pass rather than a full authoritative `BattleManager.js` port
  - projectiles, floating damage text, exact skill-trigger visuals, control locks, and summon insertion remain to be ported
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - press `F9`, then `F10`
  - confirm combat no longer advances by event count
  - confirm units attack at their own cooldown rhythms
  - confirm melee and ranged units keep different engagement distances
  - confirm visual deaths can occur before the final authoritative result screen

### 2026-05-14 - Battle Coordinate Space Fix

- User reported all units still appeared to fight from far away, without a clear melee/ranged distinction.
- Root cause:
  - the first realtime visual pass created player units under `PlayerBattleRoot` and enemy units under `EnemyBattleRoot`
  - those two UI roots occupy different halves of the overlay, so unit local coordinates were not comparable
  - movement/range logic could say units were close in local coordinates while the actual screen still showed them far apart
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - added a shared full-overlay `BattleFieldRoot` for active battle fighters
  - player and enemy fighter cards are now created under the same coordinate space during realtime playback
  - `range * 60 + size + target.size` now operates on positions that match the rendered battlefield
  - the shared battlefield root is cleared before switching to final result rows
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation needed:
  - press `F9`, then `F10`
  - confirm melee units close the distance much more than ranged units
  - confirm units no longer appear to attack across the whole screen because of separate UI roots

### 2026-05-14 - Battle Visual Effects And Realtime System Skeleton

- User requested adding:
  - ranged projectiles
  - floating text
  - area skill effects
  - control effects: stun, movement lock, attack lock
  - realtime summon entry
  - eventual authoritative Unity port of `BattleManager.js`
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - added runtime battle projectile objects
  - ranged visual attacks now spawn a projectile and apply damage on impact
  - melee attacks still apply immediate damage
  - added floating text for damage, death, control, and summon messages
  - added simple expanding burst effects for death and occasional area-skill style hits
  - added visual stun / movement lock / attack lock timers on fighter cards
  - controlled units stop moving and/or attacking until timers expire
  - summon events create temporary fighter cards in the shared battlefield root
  - summon cards enter near their source unit and expire after a short duration
- Updated `Assets/Scripts/Systems/BattleStubSystem.cs`:
  - pounce stun and battle-start lock effects now emit `control` events
  - `BattleUnitSnapshot` already carries stats needed by the visual realtime loop
- Added `Assets/Scripts/Systems/BattleRealtimeSystem.cs` and `.meta`:
  - first Unity-side authoritative realtime battle system skeleton
  - ports the core original loop shape:
    - alive units find targets
    - move toward target using `max(45, speed * 8.4)`
    - attack when within `range * 60 + size + target.size`
    - attack cadence uses `attackInterval`
    - damage uses original-style defense factor
    - units separate with collision resolution
    - output includes battle events and final unit snapshots
  - not wired into the run loop yet; current authoritative outcome still comes from `BattleStubSystem`
- Current scope boundary:
  - visual control effects are partly event-driven and partly presentation-side approximations
  - area-skill effects are still generic bursts, not one-to-one skill VFX
  - the new realtime system is a migration foundation, not yet the active production resolver
- Static verification:
  - UTF-8 string quote scan across `Assets/Scripts/**/*.cs` found no odd-quote lines.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Next battle restoration step:
  - wire `BattleRealtimeSystem` behind a test path or feature flag
  - compare its output against `BattleStubSystem`
  - then migrate `BattleSkillManager.js` triggers into the realtime system before making it authoritative

### 2026-05-15 - Realtime Battle Test Path Wiring

- Continued from the unfinished battle restoration step.
- Fixed malformed generated-UI and race-style Chinese strings in `Assets/Scripts/UI/RuntimeUiBootstrap.cs` and `Assets/Scripts/UI/UnitCardRaceStyleLibrary.cs` that left several C# string literals unterminated and would block compilation.
- Wired `BattleRealtimeSystem` behind an explicit runtime test path:
  - added `RealtimeBattleToggleButtonV2` to the generated battle action panel
  - wired prefab/generated button lookup to `RunSceneController.ToggleRealtimeBattlePreview()`
  - right-side battle preview now shows whether realtime preview is on or off
  - when enabled, battle playback uses realtime-system snapshots/events for presentation while final run settlement still uses the stable resolver
  - each realtime-preview battle writes a comparison log line against the stable resolver result
- Static verification:
  - odd quote scan across `Assets/Scripts/**/*.cs` returned no malformed lines
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
- Play Mode validation needed:
  - click `实时` and confirm battle preview changes to `实时预览：开`
  - start battle and confirm the log reports realtime preview vs stable settlement consistency
  - confirm actual HP/round progression still follows the stable resolver outcome

### 2026-05-16 - Encyclopedia Layout And Detail Restoration

- User reported the Unity card encyclopedia opened off-position, with the close button outside the visible screen.
- Updated `Assets/Scripts/UI/RuntimeEncyclopediaPanel.cs`:
  - opening the encyclopedia now forces the modal root to stretch full-screen
  - the main encyclopedia panel is anchored inside the screen instead of relying on fixed offsets from a non-stretched parent
  - closing the encyclopedia also closes any open detail modal
  - list sorting now follows the original web order: star, race, class/type label, name, id
  - clicking a card now opens a second-level detail modal, matching the original web encyclopedia flow
  - detail modal includes portrait, source/meta, basic info, full stat block, normal/golden manage skills, normal/golden battle skills, and related cards
  - related-card buttons can be clicked to navigate to another unit detail, including outgoing and incoming skill references such as summon, transform, add-to-hand, evolve, mount, and sync relations
- Static verification:
  - odd quote scan across `Assets/Scripts/**/*.cs` returned no malformed lines
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
- Play Mode validation needed:
  - open `图鉴` at 2560x1280, 1920x1080, and 1366x768 Game View sizes and confirm the close button is visible
  - click a unit and confirm the second-level detail modal appears
  - click a related card in the detail modal and confirm it changes to that unit's detail
  - close the detail modal, then close the encyclopedia

### 2026-05-16 - Unit Card UI Composition Pass

- User provided a single-card reference image and requested card UI polish:
  - star row on top
  - portrait in the middle
  - faction/race background behind portrait, kept square and not stretched
  - name below portrait
  - attack/power and other metadata below the name
  - dark gray/black base panel
- Reworked `Assets/Scripts/UI/UnitCardView.cs`:
  - card root is now a dark gray/black base
  - added a separate `RaceBackgroundImage` layer so race/faction art is square and `preserveAspect`
  - portrait remains a separate aspect-preserved layer over the faction background
  - added `InfoPanelImage` as the lower dark information band
  - fixed visible card text from mojibake to `★`, `金色`, `攻`, `防`, and `力`
  - non-compact card layout now scales from the actual card rect so shop/hand and encyclopedia card sizes both fit
  - old prefab instances are tolerated at runtime by creating missing visual layers before binding
- Rewrote `Assets/Editor/UnitCardPrefabGenerator.cs` with clean Chinese asset names:
  - `图标底_甘地.png`
  - `图标底_甘德.png`
  - `图标底_甘席.png`
  - `图标底_甘格尔.png`
- Static verification:
  - odd quote scan across `Assets/Scripts/**/*.cs` and `Assets/Editor/**/*.cs` returned no malformed lines
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
- Play Mode validation needed:
  - open shop, hand, board, and encyclopedia card grids
  - confirm card background art is square and not stretched
  - confirm portrait, name, attack/power, and race/class/faith rows do not overlap
  - optionally regenerate `Assets/Resources/Prefabs/UI/UnitCard.prefab` via `Prophecy Century/Generate Unit Card Prefab` after visual confirmation

### 2026-05-16 - Board Unit Formation Reference Pass

- User provided board-unit references and clarified that on-board units should use a separate layout:
  - no card frame on board units
  - golden board units use gold name text only
  - no faction background on board units
  - top name, centered portrait, lower-right forest gem progress, bottom `攻/防/力`
  - board formation should match the 4-3-2-1 reference proportions with wide column spacing
- Updated `Assets/Scripts/UI/UnitCardView.cs`:
  - Board presentation now hides stars, faction background, info panel, and frame
  - Board name turns gold when `isGolden`
  - Board gem label displays `◆ current/threshold`, using receive-gift evolve thresholds when available and defaulting to 10
  - Board layout is tuned around a 166x166 reference card with slightly overflowing bottom stat text to match the reference
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - V2 board slots now render as 166x166 cells
  - V2 board pixel placement was retuned to a 4-3-2-1 staggered formation with wider column spacing
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - V2 board root is taller
  - lower board preview/log labels are hidden so the formation has the same open space as the reference
- Static verification:
  - odd quote scan across `Assets/Scripts/**/*.cs` and `Assets/Editor/**/*.cs` returned no malformed lines
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
- Play Mode validation needed:
  - seed board units and confirm the 4-3-2-1 spacing matches the reference
  - confirm board unit name, portrait, forest gem progress, and bottom stats match the reference proportions

### 2026-05-17 - Board Unit Card Layout Guard

- Continued from the board-unit formation/card polish pass after quota interruption.
- Rechecked current dirty state and confirmed the active continuation point is card/board UI polish.
- Verified the apparent `★` / `◆` display issue in `UnitCardView.cs` was PowerShell output encoding, not malformed source.
- Verified `UnitCardPrefabGenerator.cs` references the real imported race background files:
  - `图标底_甘地.png`
  - `图标底_甘德.png`
  - `图标底_甘席.png`
  - `图标底_甘格尔.png`
- Updated `Assets/Scripts/UI/UnitCardView.cs` so unit portrait binding no longer calls native sprite sizing. This keeps the prefab/scripted card dimensions stable instead of letting each source image override fixed card and board cell proportions.
- Static verification:
  - odd quote / replacement character / mojibake scan across `Assets/Scripts` and `Assets/Editor` returned no matches
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
  - Unity Editor log showed recent `Tundra build success` entries and no sampled `error CS` lines
- Play Mode validation still needed:
  - seed board units and confirm board portraits keep consistent size across different unit images
  - confirm golden board unit name, forest gem progress, and bottom `攻/防/力` row do not overlap
  - if the prefab still uses the older serialized board layout, regenerate it through `Prophecy Century/Generate Unit Card Prefab` after visual confirmation

### 2026-05-17 - Devour Shop Card Feedback

- Continued the interrupted discussion about making 格尔兽 / shop-devour effects readable.
- Implemented the lightweight version first, using existing uGUI objects and the already-added `DevourShopEventState` snapshots.
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - exposed `ConsumeDevourShopEvents()` so the UI can read devour snapshots after management effects resolve
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - deploy actions now consume fresh devour events after successful `DeployUnit()`
  - stale devour snapshots are cleared before new player actions to avoid showing old round-end effects on a later deploy
  - after `RefreshView()`, devour feedback waits briefly, highlights the affected shop slot and devourer board slot, creates a temporary visual copy of the eaten card, flies it toward the devourer along a short curve, shrinks/fades it, then shows a `吞噬成功 +攻击` floating hint
  - fallback behavior still plays a devour hint/SFX if the UI slot lookup fails
- Updated `Assets/Scripts/UI/RuntimeSfxPlayer.cs`:
  - added procedural `PlayDevour()` SFX, so the feedback has a distinct sound without requiring a new imported audio file
- Static verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
  - odd quote / replacement character / mojibake scan across `Assets/Scripts` and `Assets/Editor` returned no matches
- Play Mode validation needed:
  - put a shop-devour unit such as 格尔兽 on board from hand while shop has cards
  - confirm the eaten shop card is visible as the temporary flying card even though data has already removed it
  - confirm the affected shop slot and devourer board slot flash before the card flies
  - confirm repeated devours animate one after another without blocking normal play

### 2026-05-17 - Unit Number Change Floating Text

- User requested the original web behavior where any unit number change shows floating text at that unit's current position.
- Implemented a UI-side before/after visible-unit snapshot system in `Assets/Scripts/UI/RunSceneController.cs`.
- The snapshot tracks visible shop, hand, and board units and compares:
  - HP
  - attack
  - defense
  - power
  - speed
  - morale
  - attached forest gems
- The feedback is now triggered after common state-changing actions:
  - refresh shop
  - upgrade shop
  - buy card
  - deploy card, including deploy-to-slot
  - sell hand or board units
  - choose golden deploy reward
  - round-end manage effects before battle
  - post-battle settlement / next-round rewards
- Floating text is anchored to the unit's current rendered location:
  - shop slot for shop units
  - hand slot for hand units
  - board slot for board units
- Positive totals use green text; negative totals use red text.
- Matching first prefers the same visible location, then falls back to same unit id/name/golden state so deploy-triggered stat changes can appear on the new board position.
- Static verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
  - odd quote / replacement character / mojibake scan across `Assets/Scripts` and `Assets/Editor` returned no matches
- Play Mode validation needed:
  - trigger entry buffs and confirm `攻+...` / `防+...` floats over affected board units
  - trigger forest gem gift and confirm `◆+...` floats over the recipient
  - trigger shop-card stat buffs and confirm text appears over affected shop cards
  - confirm dense multi-unit buffs are readable and do not hide the whole board

### 2026-05-17 - Conditional Board Gem Progress

- User requested hiding the board gem progress UI when a unit has no forest-gem evolve skill.
- Updated `Assets/Scripts/UI/UnitCardView.cs`:
  - board gem text now only appears when the unit's active talent set has a `receive_gift` + `evolve` skill with a positive threshold
  - removed the old fallback that displayed `◆ 0/10` for units without that skill
  - if a golden unit has no separate gold evolve threshold, it still falls back to the normal talent threshold, matching the previous behavior
- Static verification:
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings
  - odd quote / replacement character / mojibake scan across `Assets/Scripts` and `Assets/Editor` returned no matches

### 2026-05-17 - Excel Unit Data Priority Sync

- User confirmed the unit sync rule:
  - `unit_202604280006.xlsx` is the source of truth for unit numbers, text, and effects.
  - Hidden/derived Unity-only units are retained.
  - Excel `size` is treated as `sizeTier`; Unity pixel `size` is not overwritten.
- Parsed `C:\projectZhongxu\excel\unit_202604280006.xlsx` and compared it against `Assets/Resources/Data/unit_data.json`:
  - Excel units: 71.
  - Unity units: 73.
  - Matched scalar/text diffs: 0.
  - Extra Unity units retained: `精锐羽卫`, `幻影`.
- Updated `Assets/Resources/Data/unit_data.json` skill structures to follow the confirmed Excel text for the divergent units:
  - `精灵`: entry attack gain is now `+20/+40`.
  - `僧侣`: same-row units count as `甘地`; battle start gains `+50` attack per `莱特` believer, golden also gains `+50` morale per believer.
  - `刺客`: battle skill now includes stealth plus every-3/every-2 forced crit using the Excel thresholds/multipliers.
  - `牧师`: every 5 `甘地` entries grants random `甘地` units (`1/2`).
  - `流浪者`: per-round 50 total attack-gain threshold grants random `甘地` cards (`1/2`).
  - `林地卫兵`: entry grants `1/2` forest gems instead of round-end.
  - `游侠`: entry grants `1/2` forest gems and evolves to `神剑游侠` after 50 total attack gained by board units.
  - `神剑游侠`: gains `+15/+30` attack when gaining forest gems.
  - `羽卫`: round end gains `+10/+20` attack when no forest gems are held; battle attacks multiple targets and marks deaths for next-round forest gems.
  - `格尔兽`: devours a random shop card for half attack / full attack.
  - `苦工`: round-end `+10` attack plus Excel sell-price threshold.
- Updated manage/battle support:
  - added same-row race counting for manage-phase race checks.
  - added every-N race-entry hand grants.
  - added attack-gain threshold rewards/evolution.
  - added no-forest-gem round-end self attack.
  - added devour stat ratio support.
  - added realtime preview support for battle-start self faith stats and stealth.
- Static verification completed:
  - Excel scalar/text comparison still reports no matched-unit diffs.
  - Hidden/derived units remain present.

### 2026-05-17 - Card Font And Board HUD Polish

- User requested card font optimization, board round display to include current gold, and removal of redundant hero-panel counters.
- Updated `Assets/Scripts/UI/UnitCardView.cs`:
  - reduced grid/list card font sizes for stars, name, stats, and tag lines.
  - enabled best-fit bounds for normal card name/stat/tag text so long names and large numbers fit better.
  - constrained board-unit prefab text sizing for name, forest-gem progress, and stat line.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - board HUD text now reserves enough width for both current gold and round.
  - hidden the V2 hero-panel `金币` and `阶段` labels.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - board HUD now displays `金币 {gold}    第 {round} 回合`.
  - left hero-panel gold/state labels are left blank when refreshed.
- Validation still needed:
  - Play Mode check card names with long Chinese names and high stat values.
  - Check board HUD at `2560x1280` and smaller Game View sizes.

### 2026-05-17 - Manage Feedback Event Pass

- Continued the Excel/data parity follow-up and feedback priority list.
- Re-audited current `unit_data.json` skill coverage:
  - current data contains 94 distinct skill `kind` values.
  - all 94 have code-side handling paths.
- Added lightweight manage feedback event snapshots in `Assets/Scripts/Model/RunState.cs`:
  - forest-gem gift events
  - unit evolve events
  - shop attack-buff events
- Updated `Assets/Scripts/Systems/ManageEventResolver.cs`:
  - records feedback-only events when forest gems are gifted.
  - records feedback-only events when a unit evolves.
  - records feedback-only events when entry effects buff shop cards.
  - these events do not affect save data or gameplay resolution.
- Updated `Assets/Scripts/Systems/RunFlowController.cs`:
  - exposes `ConsumeManageFeedbackEvents()` for UI playback.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - plays forest-gem gift feedback with a small gem icon flying from source to target.
  - plays evolve feedback with target pulse/flash and a floating evolve hint.
  - plays shop-buff feedback with shop card flash/pulse and `攻+N` hint.
  - hooks this playback after deploy, sell, and round-end manage resolution.
  - adds a battle-reward summary hint after battle settlement for gold, shop attack, discovered cards, forest gems, temporary stats, and queued evolves.
- Static verification:
  - JSON parse succeeded for `Assets/Resources/Data/unit_data.json`.
  - no mojibake/replacement-character matches in the touched files.
  - `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings.
- Play Mode validation still needed:
  - gift forest gems with units such as `机警后援` / `河边队长`.
  - trigger evolve with `游侠` or other evolve units.
  - deploy `叫唤者` and confirm shop cards flash.
  - complete a battle that grants delayed rewards and confirm the reward summary appears.

### 2026-05-17 - Round Label Width Fix

- User reported `RoundLabelV2` text was truncated after adding current gold.
- Updated `Assets/Scripts/UI/RuntimeUiBootstrap.cs`:
  - widened `RoundLabelV2` from `506px` to `668px`.
  - reduced max font size from `40` to `38`.
  - enabled best-fit with `26-38` bounds.
  - changed initial text to the shorter `💰 0   第 1 回合`.
- Updated `Assets/Scripts/UI/RunSceneController.cs`:
  - runtime refresh now writes `💰 {gold}   第 {round} 回合`.

### 2026-05-17 - Card Prefab Split And Drag Placement UX

- Split runtime unit cards into separate editable prefabs:
  - `Assets/Resources/Prefabs/UI/UnitCardShop.prefab`
  - `Assets/Resources/Prefabs/UI/UnitCardHand.prefab`
  - `Assets/Resources/Prefabs/UI/UnitCardBoard.prefab`
  - kept `UnitCard.prefab` as a fallback.
- Updated `Assets/Scripts/UI/UnitCardView.cs`:
  - loads shop/hand/board prefabs by presentation mode.
  - prefab instances now use prefab-driven layout by default and only receive data, portrait, race background, and frame sprite binding at runtime.
  - runtime fallback cards still use the scripted layout path.
  - added a default layout bake helper so regenerated prefabs preserve the old visual layout before disabling scripted layout.
- Updated `Assets/Editor/UnitCardPrefabGenerator.cs`:
  - generates all card prefabs when run manually.
  - only fills missing prefabs when called by runtime UI generation, so hand-edited prefabs are not accidentally overwritten.
- Restored prefab default layouts:
  - shop and hand cards use the previous large `221x286` card composition.
  - board cards use the compact board-unit composition.
  - shop/hand cards now include a bound `RaceBackgroundImage` layer and keep `FrameImage` as the top rendered child so race icons and normal/golden frames are visible.
- Updated `Assets/Scripts/UI/RunSceneController.cs` card creation:
  - shop cards load `UnitCardShop`.
  - hand cards load `UnitCardHand` even when hosted by a `GridLayoutGroup`.
  - board units load `UnitCardBoard`.
  - removed the redundant hand-card deploy button; selection, drag deploy, and board-slot deploy remain.
- Added drag placement UX:
  - dragging hand cards or board units draws a runtime arrow from the source card/slot toward the pointer.
  - valid targets highlight; hover/snap target highlights more strongly.
  - hand cards can deploy only to empty board slots.
  - board units can move to empty slots or swap with occupied slots, excluding their own source slot.
  - release within snap radius completes the deploy/move/swap.
  - right-click while dragging cancels the current drag.
  - unit tooltips are suppressed during drag and restored afterward.
- Static verification:
  - text-level checks confirmed shop/hand prefabs have `RaceBackgroundImage` bindings and `FrameImage` top sibling order.
- Play Mode validation still needed:
  - confirm race backgrounds and normal/golden frames are visible on shop and hand cards.
  - confirm board-unit swap, hand deploy, snap release, right-click cancel, and tooltip suppression.

## Remaining Migration Roadmap Estimate

As of 2026-05-12 after Process 30, roadmap items 1-5 have first-pass implementations. The next priority is original HTML UI layout restoration verification and card/board/tooltip polish, then QA and tuning:

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
- Runtime tooltip restoration: shop, hand, and board unit hover tips with stats and talent/battle text.

Risk note: remaining battle steps 1-2 are the largest and may split further because original combat and skill code has many trigger-specific edge cases.
