# Prophecy Century Docs

Current migration references:

- `CODEX_CONTINUATION_NOTES.md`: handoff context for the unit-data and skill migration.
- `UNIT_SKILL_MIGRATION_HEATMAP.md`: current mechanism heatmap and risk heatmap.
- `RESTORE_CONTEXT.md`: older restore context.
- `UI_FORMALIZATION_PLAN.md`: UI planning notes.

Current unit source:

`c:\projectZhongxu\excel\unit_202605250127_数量修订版02_攻防血压缩版.xlsx`

Important terminology:

Player-facing count growth text should use `获得数量`.

Current heatmap snapshot:

- Excel units: 71
- Visible runtime units: 59
- Units with executable skill arrays: 59
- Mechanism families detected: 17

Current priority:

1. Validate default-count divisor formulas such as `默认数量/10`.
2. Continue implementing unverified mechanism families: evolve/transform, damage-received, death trigger, summon, control/movement.
3. Unity batchmode remains blocked until the editor can start and write logs normally.

Latest acceptance:

- Fixed `获得数量` static acceptance passed.
- The heatmap now marks `获得数量` test status as done.
