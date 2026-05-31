# Title Astrolabe Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the runtime title/login entry screen as a mystic astrolabe ritual interface without adding bitmap background assets.

**Architecture:** Keep the change local to `RuntimeUiBootstrap.CreateGeneratedUi()` and private helper methods in the same file. Add one lightweight source validation script to verify the expected generated UI structure is present.

**Tech Stack:** Unity C#, Unity UI (`Image`, `Text`, `Dropdown`, `Button`, `RectTransform`), PowerShell validation.

---

### Task 1: Source Validation

**Files:**
- Create: `tools/validate_title_astrolabe_ui.ps1`

- [x] **Step 1: Write the failing validation**

Create a PowerShell script that reads `Assets/Scripts/UI/RuntimeUiBootstrap.cs` and checks for the new title helpers and for removal of the title background binding.

- [x] **Step 2: Run validation and verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File tools/validate_title_astrolabe_ui.ps1`

Expected: FAIL before implementation because astrolabe helper names are missing.

### Task 2: Runtime Title UI

**Files:**
- Modify: `Assets/Scripts/UI/RuntimeUiBootstrap.cs`

- [x] **Step 1: Replace the first-pass title construction**

Remove the title background image binding and replace the title area with a generated astrolabe scene, two selection panels, and restyled primary/secondary actions.

- [x] **Step 2: Add local helper methods**

Add private helpers for title labels, astrolabe rings, line segments, small star marks, selection panels, and button styling.

- [x] **Step 3: Run validation and verify it passes**

Run: `powershell -ExecutionPolicy Bypass -File tools/validate_title_astrolabe_ui.ps1`

Expected: PASS.

### Task 3: Compile-Oriented Check

**Files:**
- Modify: `Assets/Scripts/UI/RuntimeUiBootstrap.cs`

- [x] **Step 1: Run the available syntax check if present**

Run: `powershell -Command "if (Test-Path .\run_syntax_check.js) { & 'C:\Program Files\nodejs\node.exe' .\run_syntax_check.js } else { Write-Output 'run_syntax_check.js not present' }"`

Expected: Either the existing syntax check passes, or the repository reports that the script is not present.

- [x] **Step 2: Review changed files**

Run: `git diff -- Assets/Scripts/UI/RuntimeUiBootstrap.cs tools/validate_title_astrolabe_ui.ps1 docs/superpowers`

Expected: Diff only contains the title UI implementation, validation script, and planning/spec docs.

---

## Battle Spectator UI Design Notes

This section records the battle-stage UI direction discussed after the title astrolabe work. It is design documentation only; it does not add implementation scope to the completed title-entry tasks above.

**Design Goal:** Make the battle stage feel like a player-first tactical spectator view. The default screen should help the player understand who is fighting, where pressure is building, which skills just triggered, and which side is winning the current exchange. Detailed numerical verification remains available, but it should not dominate the default view.

**Reference Mockups:**
- Unity workspace HTML mockup: `docs/mockups/battle-spectator-ui.html`
- Unity workspace default PNG: `docs/mockups/battle-spectator-ui.png`
- Unity workspace debug-state HTML: `docs/mockups/battle-spectator-ui-debug.html`
- Unity workspace debug-state PNG: `docs/mockups/battle-spectator-ui-debug.png`
- Knowledge base copy: `D:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Database\mockups\`
- Knowledge base preview tab: `D:\Prophecy-Century\GameProject\prophecy_century\prophecy-century-Database\index.html#mockups`

### Layout Direction

The battle UI should not be a battle-flavored version of the management screen. It should behave more like a tactical broadcast overlay:

- **Center battlefield first:** Reserve roughly 70-78% of screen width and 68-76% of screen height for the active battlefield.
- **Thin top HUD:** Use a 64-80px status strip for round, phase, battle timeline, pause, speed, skip, and a small debug entry button.
- **Side overlays, not side dashboards:** Left and right panels should be translucent summaries, not full unit lists.
- **Bottom key-event ticker:** Show only the latest high-value events by default. Full logs belong in the debug/analytics panel.
- **Debug as an entry point:** Keep one compact `Debug` button in the top-right. It opens a separate or overlay panel for event rows, unit stats, damage data, seeds, target resolution, and other validation details.

### Default Spectator Content

Default visible information should prioritize player comprehension:

- Player and enemy units as compact tactical badges instead of large cards.
- Unit badge content: short name, count, HP bar, 1-2 status labels/icons.
- Active attack or skill relationship lines in the battlefield.
- Floating event badges near the relevant unit, such as `士气高涨`, `暴击 -342`, `数量 +3`.
- Ally summary: hero HP, alive count, active buffs, formation pressure.
- Enemy summary: alive count, elite count, current target, danger level, top threats.
- Bottom ticker: skill trigger, count change, high-threat warning, casualty or count loss, frontline state.

### Debug / Numeric Validation Content

The debug view should be reachable but hidden by default. It can be an overlay panel in the first pass and later become a separate full panel if needed.

Recommended debug tabs:

- **Events:** timestamped event stream with source, target, amount, hp/count delta, flags, and random seed when available.
- **Units:** current runtime unit state, target, attack timer, HP/count, buffs, debuffs, and derived combat stats.
- **Damage:** damage calculation inputs and outputs, crit flag, defense reduction, multiplier source, and final applied result.

The debug panel should not replace the spectator layer. Its job is to answer "why did this happen?" after the player or developer notices something in the battle view.

### Visual Language

Use a restrained tactical fantasy style:

- Ally side: cold cyan / teal accents.
- Enemy side: ember red / dark orange accents.
- Important triggers: muted gold.
- Debug / analytics: violet accent, visually distinct from normal gameplay UI.
- Background: dark battlefield field with subtle grid, side color washes, and a faint conflict band at the center.
- Avoid large decorative frames, repeated full unit cards, and long default logs.

### Implementation Notes For Unity

When implementing in Unity, prefer small reusable runtime UI helpers rather than scene-specific hardcoding:

- `CreateBattleTopHud(...)`
- `CreateBattlefieldRoot(...)`
- `CreateBattleUnitBadge(...)`
- `CreateBattleSideSummary(...)`
- `CreateBattleEventTicker(...)`
- `CreateBattleDebugPanel(...)`

The first implementation pass can use generated Unity UI primitives (`Image`, `Text`, `Button`, `RectTransform`) and existing unit icon assets. Unit detail tooltips can reuse the improved tooltip direction from the card UI: compact stat grid, skill sections, and screen-safe placement.

### Acceptance Criteria

- The battlefield remains the dominant visual area at common 16:9 and 2:1 game view sizes.
- Default view is readable without opening debug tools.
- Unit badges do not overlap at representative battle positions.
- Skill triggers are visible in-world, not only in the event ticker.
- Bottom ticker shows concise key events and does not become a scroll log.
- Debug panel can be opened from a small top-right entry and closed without disrupting the battle view.
- The design can be implemented with Unity runtime UI primitives and existing project assets.
