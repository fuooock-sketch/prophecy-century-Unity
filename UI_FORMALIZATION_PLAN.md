# Prophecy Century UI Formalization Plan

Last updated: 2026-05-11

## Goal

Restore the Unity runtime UI toward the original HTML/Web/Electron layout while keeping the Unity version playable during migration.

The first implementation target is the manage screen. Title, battle result, and animated battle presentation should come after the manage screen layout is close to the original HTML structure.

## Current Problem

The current UI is functional but still reads as a debug surface:

- Layout is generated in one large bootstrap method.
- The bottom action bar is crowded and treats all commands as equal.
- The right panel mixes campaign, hero, battle preview, rewards, history, and log text without enough structure.
- Cards and board cells are readable but not yet visually stable across common Game View sizes.
- The main player loop is present, but visual hierarchy does not emphasize the decisions players make most often.

## Target Layout

Reference resolution remains `1600x900`.

Stable regions based on the original `index.html` and `style.css`:

- Top status/action bar, height about `72`.
- Horizontal shop area below the top bar, height about `210`.
- Board area below the shop, left side, about `1080x420` in the original 1800px layout.
- Hand area below the shop, right side, about `640x290`.
- Combat log / compact information area below the hand, right side, about `640x240`.

Recommended proportions:

```text
+--------------------------------------------------------------------------------+
| Top status/action bar: gold | gems | round | shop level | refresh/upgrade/battle |
+--------------------------------------------------------------------------------+
| Horizontal shop area: title/meta + 6 shop cards                                 |
+------------------------------------------------------------+-------------------+
| Board area: enemy preview + formation grid                  | Hand area         |
|                                                            | hand cards grid   |
|                                                            +-------------------+
|                                                            | Combat log/info   |
+------------------------------------------------------------+-------------------+
```

## Visual Direction

- Use a dark neutral base for the app surface.
- Use muted steel-blue panels for shop, board, and system information.
- Use restrained gold accents for currency, golden units, and primary confirmation.
- Use green only for success/valid placement and red only for loss/danger/failure.
- Avoid decorative backgrounds, oversized hero sections, and one-color palettes.
- Prefer crisp boxes, clear alignment, and compact tactical density over large ornamental cards.

## Command Placement

Original HTML places the main economy and battle commands in the top status bar, not in a bottom bar:

- Shop economy group:
  - refresh shop
  - upgrade shop
  - lock shop
- Utility group:
  - save
  - load
  - new run / return to title
- Primary battle command:
  - start/resolve battle
  - visually dominant in the top action row

Rules:

- Do not let utility commands compete visually with battle.
- Keep button widths stable.
- Use icons when available, but Chinese labels must remain readable.
- At `1366x768`, top-bar commands should not overlap status labels.

## Card Component

Every unit card should use a consistent component shape:

- Left: unit portrait, fixed square.
- Top line: unit name, star, golden state.
- Middle line: race / faith / type.
- Bottom line: stat row with attack, HP, defense, power or speed when available.
- Right: contextual action buttons.

States:

- Normal
- Golden
- Selected
- Dragging
- Disabled / sold empty shop slot
- Valid drop target
- Invalid drop target

Card sizing target:

- Shop and hand cards: `88-96` px tall.
- Board cell cards: compact `76-90` px tall depending on available row height.
- Text should wrap only where planned; important stats should remain on one line.

## Board Slot Component

Board slots should represent both placement state and interaction state:

- Empty
- Selected empty
- Occupied
- Selected occupied
- Valid drop target
- Invalid drop target

Rules:

- Empty slots should show slot id and a subtle placement hint only when a hand card is selected.
- Occupied slots should show portrait, name, star, and compact stats.
- Sell/move/deploy actions should not cover the unit name or stats.
- Drag/drop and click selection should remain equivalent ways to operate the board.

## Right Panel

The right panel should stop being one long text block. First-pass implementation can use stacked sections instead of a full tab system:

- Run summary:
  - campaign, hero, round limit, wins, losses, HP.
- Battle preview:
  - player score, enemy score, estimated pressure.
- Pending rewards:
  - next-round gold, shop buffs, discover rewards, forest gems, evolve rewards.
- Recent history:
  - last 3 battles.
- Log:
  - last 5-7 player/system messages.

Rules:

- Each section gets its own header and bounded text area.
- Battle preview should be visible without scrolling.
- Logs should not crowd out battle preview and rewards.

## Implementation Plan

### Pass 1 - HTML Manage Screen Layout

- Refactor `RuntimeUiBootstrap.BuildUi()` into smaller helpers for:
  - top status bar
  - horizontal shop area
  - board area
  - hand area
  - combat log / compact info area
- Move core commands toward the top status/action bar, matching the HTML structure.
- Keep the runtime-generated UI approach for speed.
- Do not rewrite gameplay systems in this pass.

### Pass 2 - Component Polish

- Increase card component consistency.
- Tune board cell sizing and typography.
- Add clearer selected and golden states.
- Make empty shop slots visually distinct from unavailable cards.

### Pass 3 - Right Panel Structure

- Split battle preview/history/log into separate labels or containers.
- If needed, add simple tabs after stacked sections are verified.

### Pass 4 - Title And Result Polish

- Rework title screen composition around imported background.
- Add a battle result panel with victory/defeat, HP delta, rewards, and next action.
- Leave animated battle field for a later dedicated pass.

## Verification Checklist

Play Mode checks:

- Title selection still starts a run.
- Buy, deploy, drag/drop, move, swap, and sell still work.
- Refresh, upgrade, lock, battle, save, load, and new run still work.
- Battle outcome can advance rounds, lose HP, trigger gameover, and trigger campaign victory.
- BGM, button SFX, save/load SFX, and battle result SFX still play.

Visual checks:

- `1600x900`: intended reference layout.
- `1366x768`: laptop minimum check.
- `1920x1080`: large desktop check.
- Narrow-ish Game View: no catastrophic overlap.
- Chinese labels remain readable.
- Card icons preserve aspect and do not blur excessively.
- Right panel sections do not overlap.

## Success Criteria For First Coding Pass

- The manage screen no longer looks like a single debug panel.
- Battle is clearly the primary action.
- Shop commands and utility commands are visually separated.
- Card and board interactions are at least as usable as before.
- No gameplay regression is introduced.
