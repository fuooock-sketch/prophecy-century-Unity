# Codex Continuation Notes

This document is the handoff context for continuing the unit migration work in a new conversation.

## Project

- Unity project: `c:\projectZhongxu\prophecy_century\prophecy-century-Unity\prophecy_century`
- Current source Excel: `c:\projectZhongxu\excel\unit_202605250127_数量修订版02_攻防血压缩版.xlsx`
- Unit data output: `Assets/Resources/Data/unit_data.json`
- Heatmap: `docs/UNIT_SKILL_MIGRATION_HEATMAP.md`
- Excel import script: `tools/import_unit_excel.py`
- Heatmap audit script: `tools/audit_unit_skill_migration.py`

## Vocabulary

Do not use the old visible term for count growth. Player-facing wording must use the approved Chinese terms:

- `获得数量`
- `数量 +X`

The old term has already been removed from current project text and data. Keep this rule for all future UI, tooltip, logs, docs, and skill text.

## Current Migration Baseline

The project has been moved toward the new troop stack model:

- A card represents a troop.
- Unit base stats are fixed.
- Main growth is quantity.
- No quantity cap.
- No roster cap.
- No hidden standard size.
- No overflow experience.
- No percentage-based quantity gain.
- `power` may remain for compatibility, but must not drive the new core damage formula.

Core runtime fields now include:

- `startCount`
- `defaultCount`
- `baseCount`
- `currentCount`
- `hpPerUnit`
- `currentTotalHp`
- `attack`
- `defense`
- `damageMin`
- `damageMax`
- `initiative`
- `speed`
- `morale`
- `luck`
- `attackRange`
- `size`
- `designOnlyInitialBaseDamage`

`maxCount` still exists as a compatibility field in serialized state and definitions, but all imported units currently have `maxCount = 0`, and core logic should not use it as a cap.

## Completed Work

### Excel Import

`tools/import_unit_excel.py` now:

- Reads the 25-column new Excel layout.
- Maps `start_count`, `hp_per_unit`, `atk`, `def`, `damage_min`, `damage_max`, `initiative`, `speed`, `morale`, `luck`, `range`, `size`.
- Writes `designOnlyInitialBaseDamage` from `first_avg_damage`.
- Preserves executable skill arrays:
  - `talents`
  - `goldTalents`
  - `battleSkills`
  - `goldBattleSkills`
- Does not clear skill arrays.
- Sets imported `maxCount` to `0`.
- Normalizes visible terminology to `获得数量`.
- Extracts simple `+N数量` values from text and writes them back into existing skill `value` fields.
- Extracts fixed default-count divisor formulas such as `默认数量/10` and writes the matching `ratio` into existing skill definitions.

### Heatmap

`tools/audit_unit_skill_migration.py` generates:

- Mechanism heatmap.
- Unit heatmap.
- Risk heatmap.
- Next sprint notes.

Run:

```powershell
python -B tools\audit_unit_skill_migration.py
```

Current high-level heatmap state:

- Visible runtime units: 59
- Visible units with executable skill arrays: 59
- Mechanism families detected: 17
- `获得数量`: text imported, logic partially connected, static acceptance complete.

### New Damage Model

Implemented in:

- `Assets/Scripts/Systems/BattleStubSystem.cs`
- `Assets/Scripts/Systems/BattleRealtimeSystem.cs`
- visual playback mirrors in `Assets/Scripts/UI/RunSceneController.cs`

Formula:

```text
baseDamage = currentCount * unitDamage * (20 + attack) / (20 + target.defense)
```

Where `unitDamage` is random from `damageMin` to `damageMax` when random is available, otherwise average.

Power is no longer the core damage driver.

### Receiving Damage

Implemented in:

- `BattleStubSystem.DealDamage`
- `BattleRealtimeSystem.DealDamage`
- visual replay damage handling in `RunSceneController`

Rule:

```text
currentTotalHp -= damage
currentCount = currentTotalHp <= 0 ? 0 : ceil(currentTotalHp / hpPerUnit)
```

### Luck and Morale

Constants exist in battle systems:

```csharp
LUCK_CRIT_CHANCE_PER_POINT = 0.06f
LUCK_CRIT_DAMAGE_MULTIPLIER = 1.5f
MORALE_EXTRA_ATTACK_CHANCE_PER_POINT = 0.04f
```

- Luck drives crit chance.
- Morale drives extra attack chance.

### Shop Purchase

Implemented in:

- `Assets/Scripts/Systems/ShopSystem.cs`

Shop cards use Excel default count through `ResolveStartCount(unit)`.

### Synthesis Quantity

Implemented in:

- `Assets/Scripts/Systems/SynthesisSystem.cs`

Rule:

```text
golden baseCount = sum(source baseCount) - fixed star loss
```

Loss table:

```text
1 star: 3
2 star: 2
3 star: 2
4 star: 1
5 star: 1
6 star: 0
```

At least 1 quantity remains.

### Forest Gem

Implemented in:

- `Assets/Scripts/Systems/ManageEventResolver.cs`
- related UI in `RunSceneController.cs` and `UnitCardView.cs`

Current rule:

```text
forestGemCount += amount
forestGemsAttached += amount
forestGemsReceived += amount
baseCount += amount
```

Old attack gain has been removed from the basic forest gem use path.

### Fixed Quantity Gain Sprint

Started and partially implemented in:

- `Assets/Scripts/Systems/ManageEventResolver.cs`

New helper:

```csharp
GainCount(runState, target, amount, source, processed, depth)
```

It:

- Increases permanent `baseCount`.
- Sets compatibility `maxCount = 0`.
- Dispatches `on_gain_count`.

Several old gain-attack / gain-power management paths now call `GainCount`, including:

- entry-trigger self quantity gain
- round-end self quantity gain
- adjacent-faith round-end quantity gain
- random race unit quantity gain
- same-id tagged unit quantity gain
- team quantity gain on quantity-gain events
- forest gem related quantity gain
- devour shop card count transfer paths

Latest implementation step:

- Newly generated hand cards from gold deploy rewards, discovery rewards, and management effects now receive `baseCount = ResolveStartCount(unit)` and `maxCount = 0`.
- Board deployment now fills missing `baseCount` from the unit definition instead of allowing zero-count legacy cards to reach battle.
- Shop-card quantity growth feedback uses `ShopBuffEventState.count`; the floating text now shows `商店卡 数量+X`.
- Ger Beast default-count divisor text is imported as `ratio = 0.1` / `0.2`.
- Floating text now follows the actual changed field: quantity effects use `数量+X` / `获得数量+X`; real attack, power, speed, morale, defense, HP, and gem changes keep their matching attribute labels.

This sprint is not fully tested yet.

Static acceptance for fixed `获得数量` now passes and is recorded in `docs/UNIT_SKILL_MIGRATION_HEATMAP.md`:

- no visible count cap
- no percentage-based quantity text in runtime unit data
- `GainCount` writes `baseCount`, clears compatibility `maxCount`, and dispatches `on_gain_count`
- shop, board deployment, reward, and discovery card sources fill default quantity
- forest gem count rule is `forestGemCount +1` and permanent quantity +1 per gem
- representative units pass fixed-value checks: Bright Warrior, Elf, Fire Elemental, Earth Elemental, Ger Beast, Magic Dragon

## Important Caveats

The project still contains many old skill kind names such as:

- `*_gain_attack`
- `*_gain_power`
- `on_gain_power_*`
- `on_gain_defense_*`

Some now execute quantity logic, but the names are still legacy. Do not assume the kind name describes the current effect exactly.

Old `power` fields and `shopBuffPower` still exist for compatibility and some not-yet-migrated skills. Future work should either:

- migrate them to quantity, morale, luck, speed, shield, summon, or fixed formula effects, or
- explicitly mark them as compatibility-only.

The current Excel still contains several percentage-based quantity texts that conflict with the no-percentage quantity rule and are flagged in the heatmap:

- Resolved in the current working tree:
  - Earth Elemental now grants fixed temporary quantity +3 / +6 to the lowest-count ally.
  - Magic Dragon now summons Fire Elementals at fixed quantity 22 / 26.
  - Beast Tamer now summons Ger Beasts at Ger Beast default quantity.
  - Beast Rider now summons Ger Beasts at Ger Beast default quantity.

The heatmap risk item for percentage-based quantity text is currently green. The remaining related risk is default-count divisor formulas, which are fixed formulas and still need targeted gameplay validation.

## Current Validation Results

Recent static checks:

- No missing new unit fields in `unit_data.json`.
- 59 visible units have executable skill arrays.
- `maxCount` nonzero count is 0.
- Old visible count-growth terminology scan returns 0.
- Python scripts syntax OK.
- `git diff --check` passes.

Unity batchmode validation is still blocked. It previously hung during startup and did not generate the requested log. A Unity process was manually stopped. The likely causes are Unity license, Hub login, project lock, or a startup dialog. Before claiming Unity startup success, manually open the project once or rerun batchmode after confirming no dialogs.

## Next Recommended Step

Continue the first mechanism sprint:

```text
Mechanism: fixed quantity gain
Goal: move heatmap state from logic 🟧 to tested 🟩
```

Recommended test units:

- 光明武士: round-end adjacent faith self quantity gain.
- 精灵: other Gandi unit entry triggers self quantity gain.
- 卫戍协兵: ally quantity gain triggers team quantity gain.
- 火元素: quantity-gain event triggers extra self quantity gain.
- 密林宝钻: use on board unit increments gem count and quantity.

Test goals:

- `baseCount` increases by the expected fixed value.
- `maxCount` remains unused / zero.
- `on_gain_count` triggers dependent effects.
- No percentage-based quantity gain is introduced.
- Heatmap is regenerated after tests.

## Useful Commands

Import latest Excel:

```powershell
python tools\import_unit_excel.py C:\projectZhongxu\excel\unit_202605250127_数量修订版02_攻防血压缩版.xlsx Assets\Resources\Data\unit_data.json
```

Regenerate heatmap:

```powershell
python -B tools\audit_unit_skill_migration.py
```

Static checks:

```powershell
rg -n "<old-count-growth-term>" Assets\Scripts Assets\Resources\Data\unit_data.json tools docs\UNIT_SKILL_MIGRATION_HEATMAP.md
python -B -c "compile(open(r'tools\import_unit_excel.py', encoding='utf-8').read(), r'tools\import_unit_excel.py', 'exec'); compile(open(r'tools\audit_unit_skill_migration.py', encoding='utf-8').read(), r'tools\audit_unit_skill_migration.py', 'exec'); print('python scripts ok')"
git diff --check
```

Unity version from project settings:

```text
2021.3.19f1c1
```

Detected Unity executable:

```text
C:\Program Files\Unity 2021.3.19f1c1\Editor\Unity.exe
```
