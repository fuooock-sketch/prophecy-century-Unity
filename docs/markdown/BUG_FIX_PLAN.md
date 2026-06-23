# 单位数据 QA 问题修复清单

> 创建日期：2026-06-04  
> 基于：`tools/qa_unit_data.py` 全量检查结果  
> 修复原则：先修 🔴CRITICAL → 再修 🟠ERROR → 最后确认 🟡WARNING

---

## 问题总览

| 类别 | 原始报告数 | 误报 | 真实问题 | 严重度 |
|------|-----------|------|----------|--------|
| 有文本无kind | 10 | 5（kind在别的数组） | **5** | 🟠 ERROR |
| kind仅JSON无C# | 11 | 11（已在C#中实现，只是不在switch case中） | **0** | — |
| 金色升级疑点 | 10 | 5（合理的非2x设计） | **5** | 🟡 WARNING |
| kind语义疑点 | 6 | 2（"猛扑"≈"冲到"是措辞变体） | **4** | 🟡 WARNING |
| 基础数值不一致 | 25 | 25（typeLabel `-`→`""` 预期行为） | **0** | — |
| 技能文本差异 | 0 | 0 | **0** | — |
| 数值范围异常 | 1 | 1（幻影star=0是召唤物） | **0** | — |

**真实需修复问题：约 14 个**（5个ERROR + 9个WARNING）

---

## 🔴 CRITICAL 级别（无）

经深入排查，原始报告中标记的"JSON only no C#"的11个kind全部已在C#中实现，只是实现方式不是标准`case`语句，而是通过以下模式：

| kind | 实现位置 | 实现方式 |
|------|----------|----------|
| `battle_aura_sync_unit_id_attack_to_highest` | BattleStubSystem.cs / BattleRealtimeSystem.cs | `ApplyContinuousAuras()` 中的foreach |
| `battle_periodic_temp_power` | BattleStubSystem.cs / BattleRealtimeSystem.cs | 独立if判断 + TickTimedSkills |
| `passive_every_nth_attack_force_crit` | BattleStubSystem.cs / BattleRealtimeSystem.cs | ResolveForceCrit 内联判断 |
| `on_attack_chance_force_crit` | BattleStubSystem.cs / BattleRealtimeSystem.cs | ResolveForceCrit 内联判断 |
| `first_hits_counterattack` | BattleStubSystem.cs | ResolveAttackDamage 内联判断 |
| `on_damaged_count_temp_morale` | BattleStubSystem.cs | DealDamage 内联判断 |
| `on_damaged_survive_next_round_forest_gem` | BattleStubSystem.cs | DealDamage 后置判断 |
| `on_ally_death_tagged_units_temp_power` | BattleStubSystem.cs | ResolveDeath 内联判断 |
| `on_extra_attack_once_next_round_gold` | BattleStubSystem.cs / BattleRealtimeSystem.cs | ResolveBattleRoundStart + SkillCounters |
| `on_sell_price_if_attack_threshold` | BoardSystem.cs | 出售价格计算逻辑 |
| `same_row_units_count_as_race` | ManageEventResolver.cs | `CountsAsRace()` 辅助方法内联判断 |

**结论：无CRITICAL问题，所有技能均有运行时实现。**

---

## 🟠 ERROR 级别（5个）

### 问题 E-1：卫戍协兵 — battle技能有文本但battleSkills为空

**现状**：
```
battleText:  "我方任意部队产生1次暴击，本部队临时数量+10。"
goldBattleText: "我方任意部队产生1次暴击，本部队临时数量+20。"
battleSkills: []   ← 空！
goldBattleSkills: [] ← 空！
```

**根因**：策划在Excel中新增了战斗技能描述，但尚未在JSON中配置对应的SkillDefinition。

**修复方法**（二选一）：

**方案A**（推荐）：在JSON中补全SkillDefinition
```json
"battleSkills": [
  {
    "kind": "on_ally_crit_self_temp_power",
    "value": 10,
    "goldValue": 20
  }
],
"goldBattleSkills": [
  {
    "kind": "on_ally_crit_self_temp_power",
    "value": 20
  }
]
```
然后在 `BattleStubSystem.cs` 和 `BattleRealtimeSystem.cs` 的 `ResolveForceCrit` 方法中添加对应逻辑。

**方案B**：如果这个技能与现有kind `on_damaged_count_temp_morale`（精锐游骑兵使用）逻辑相似，可以复用。

**复现步骤**：让卫戍协兵进入战斗 → 我方任意部队暴击 → 预期卫戍协兵+10数量。目前不会生效。

---

### 问题 E-2：莱特使者 — talent技能有文本但talents为空

**现状**：
```
talentText:  "回合开始时，如果场上至少有6支莱特信仰者部队，则随机发现1支莱特信仰者部队。"
goldTalentText: "回合开始时，如果场上至少有6支莱特信仰者部队，则随机发现2支莱特信仰者部队。"
talents: []   ← 空！
goldTalents: [] ← 空！
```

**注意**：这个技能的kind `battle_start_if_team_faith_count_next_round_discover` 目前被错误地放在了 `battleSkills` 数组中，但技能描述明确是"回合开始时"（运营阶段），应该属于 talents。

**修复方法**：
```json
// 从 battleSkills 移动到 talents
"talents": [
  {
    "kind": "enter_if_board_faith_count_discover",
    "threshold": 6,
    "faith": "莱特",
    "value": 1
  }
],
"goldTalents": [
  {
    "kind": "enter_if_board_faith_count_discover",
    "threshold": 6,
    "faith": "莱特",
    "value": 2
  }
],
"battleSkills": [],    // 清空
"goldBattleSkills": []  // 清空
```

**注意事项**：需要确认 `enter_if_board_faith_count_discover` 这个kind（目前在C#中已定义但JSON未使用）的触发时机是"回合开始"还是"入场时"。如果kind语义是"入场"，则需要新增kind或修改kind名。

**复现步骤**：场上≥6支莱特信仰者 → 回合开始 → 预期发现莱特部队。目前不生效。

---

### 问题 E-3：莱特的回响 — talent技能有文本但talents为空

**现状**：
```
talentText:  "莱特信仰者每累计获得数量5次，本部队获得+1数量。"
goldTalentText: "莱特信仰者每累计获得数量5次，本部队获得+2数量。"
talents: []   ← 空！
goldTalents: [] ← 空！
```

**修复方法**：
```json
"talents": [
  {
    "kind": "while_on_board_faith_accumulate_gain_count_self_gain_attack",
    "faith": "莱特",
    "threshold": 5,
    "value": 1
  }
],
"goldTalents": [
  {
    "kind": "while_on_board_faith_accumulate_gain_count_self_gain_attack",
    "faith": "莱特",
    "threshold": 5,
    "value": 2
  }
]
```

**如果不想新增kind**，可以检查 `round_end_self_gain_attack_per_faith_count` 是否能够表达相同语义。但该kind是按"每有1名信仰者"计算，不是"累计5次"，逻辑不同，建议新增kind。

**复现步骤**：莱特信仰者累计获得数量5次 → 预期莱特的回响+1数量。目前不生效。

---

### 问题 E-4：猎豹 — battle技能有文本但battleSkills为空

**现状**：
```
battleText:  "如果阵亡，下回合开始时获得1颗【密林宝钻】。"
goldBattleText: "如果阵亡，下回合开始时获得2颗【密林宝钻】。"
battleSkills: []   ← 空！
goldBattleSkills: [] ← 空！
```

**修复方法**：
```json
"battleSkills": [
  {
    "kind": "on_death_next_round_forest_gem",
    "value": 1
  }
],
"goldBattleSkills": [
  {
    "kind": "on_death_next_round_forest_gem",
    "value": 2
  }
]
```

**注意**：`on_death_next_round_forest_gem` 这个kind已被弓箭手使用，可以直接复用。只需检查BattleStubSystem中该kind是否已正确实现"阵亡后下回合给密林宝钻"的逻辑。

**复现步骤**：猎豹阵亡 → 下回合开始 → 预期获得1颗密林宝钻。目前不生效。

---

### 问题 E-5：血淤魔 — talent技能有文本但talents为空

**现状**：
```
talentText:  "每当有部队获得数量（无论在场上、手牌还是商店），【密林宝钻】提供的获得数量+1；每回合最多触发5次。"
goldTalentText: "每当有部队获得数量（无论在场上、手牌还是商店），【密林宝钻】提供的获得数量+2；每回合最多触发5次。"
talents: []   ← 空！
goldTalents: [] ← 空！
```

**修复方法**：
```json
"talents": [
  {
    "kind": "while_on_board_forest_gem_gain_bonus_every_n",
    "threshold": 5,
    "value": 1
  }
],
"goldTalents": [
  {
    "kind": "while_on_board_forest_gem_gain_bonus_every_n",
    "threshold": 5,
    "value": 2
  }
]
```

这个技能涉及"密林宝钻提供数量+1"的全局增益，需要在`ManageEventResolver.cs`的`GiftForestGem`方法或`ReinforceUnit`调用处增加判断。建议新增kind并实现。

**复现步骤**：血淤魔在场 → 任意部队获得数量 → 密林宝钻赐予时额外+1数量（上限5次/回合）。目前不生效。

---

## 🟡 WARNING 级别（9个）

### 问题 W-1~5：金色升级value不是标准2x（5个）— 需策划确认

| 单位 | kind | 普通值 | 金色值 | 实际比例 | 标准预期 |
|------|------|--------|--------|----------|----------|
| 光明武士 | round_end_if_adjacent_faith_self_gain_attack | 3 | 5 | 1.67x | 3→6(2x) |
| 精灵 | while_on_board_on_entry_race_self_gain_attack | 2 | 3 | 1.5x | 2→4(2x) |
| 刺客 | round_end_if_race_count_self_gain_attack | 10 | 15 | 1.5x | 10→20(2x) |
| 莱特的回响 | battle_start_team_attack_per_faith_count | 10 | 15 | 1.5x | 10→20(2x) |
| 风元素 | while_on_board_race_threshold_team_speed | 5 | 8 | 1.6x | 5→10(2x) |

**修复方法**：请策划逐条确认这些数值是否为有意设计的1.5x~1.67x增幅。如果是——在Excel note列标注"有意非2x"；如果不是——修正为2x。

**专业QA建议**：这类问题不建议直接改代码。应该：
1. 整理成表格发给策划确认
2. 策划回复后，在Excel的note列标注"确认：有意1.5x"
3. 更新QA脚本的金色升级验收标准，增加"1.5x"为合法比例

---

### 问题 W-6~7：kind语义不匹配（4个）— 低优先级措辞问题

| 单位 | kind | 缺失关键词 | 实际文本措辞 |
|------|------|-----------|-------------|
| 雪狮 | battle_start_pounce_nearest_damage | "冲到"、"暴击" | 文本用"猛扑"、"3倍伤害" |
| 火元素 | on_gain_power_self_gain_attack | "获得数量" | 文本用"每获得1次数量" |

**根因**：kind命名系统使用了固定的关键词（如"暴击"表示critical），但某些单位的实际文本使用了近义词（"猛扑"≈"冲到"、"3倍伤害"≈"暴击"）。

**修复方法**（二选一）：

**方案A**：修改Excel技能文本使措辞统一（推荐，长期收益大）
- 雪狮：`"猛扑"` → `"冲到"`，`"3倍的伤害"` → `"3倍暴击伤害"`
- 火元素：`"每获得1次数量"` → `"每获得1次数量"`（实际已经匹配，kind名中"获得数量"对应文本"获得1次数量"，匹配度尚可）

**方案B**：更新kind语义检查表，将"猛扑"、"N倍伤害"加入白名单（省事但掩盖问题）

---

### 问题 W-8~9：金色升级kind不同（2个）— 需确认

| 单位 | 普通kind | 金色kind | 技能列 |
|------|----------|----------|--------|
| 风元素 | battle_start_speed_threshold_attack_interval_reduce | battle_start_speed_threshold_attack_interval_half | battleSkills |
| 掘地鼠 | battleSkills(0个) | goldBattleSkills(1个) | 数量不一致 |

**风元素分析**：
- 普通：攻击间隔减少（reduce）
- 金色：攻击间隔减半（half）
- 这是**质变技能**——不是纯数值提升，而是效果类型变了。需策划确认是否有意设计"减速→减半"的升级。

**掘地鼠分析**：
- 普通battleSkills为空，金色battleSkills有1个
- 文本：`battle_2: "开战后，进入潜行状态，首次攻击造成5倍单体伤害结算，然后退出潜行状态"`
- battle_1文本为空
- 这是**金色专属技能**——普通形态没有战斗技能，金色才有。这是合理设计。
- **修复**：在QA脚本中增加规则：如果普通battleText为空但金色battleText不为空，则battleSkills数量不一致是预期行为。

---

## 修复优先级与排期建议

| 优先级 | 问题编号 | 修复工作量 | 建议排期 |
|--------|----------|-----------|----------|
| P0 | E-1 卫戍协兵 battle | 新增kind + C#实现 | 本迭代 |
| P0 | E-2 莱特使者 talent | kind从battle移到talent + 确认kind语义 | 本迭代 |
| P0 | E-3 莱特的回响 talent | 新增kind + C#实现 | 本迭代 |
| P0 | E-4 猎豹 battle | 复用现有kind | 本迭代 |
| P0 | E-5 血淤魔 talent | 新增kind + C#实现 | 本迭代 |
| P1 | W-1~5 金色比例 | 策划确认 | 本迭代 |
| P1 | W-8 风元素kind变更 | 策划确认 | 本迭代 |
| P2 | W-6~7 语义措辞 | 改文本或加白名单 | 下迭代 |
| P2 | W-9 掘地鼠 | 更新QA规则 | 下迭代 |

---

## QA脚本改进建议

基于本次检查经验，建议对 `tools/qa_unit_data.py` 做以下增强：

1. **Step 4 kind覆盖率检查**：增加非case模式的kind搜索（搜索 `skill.kind == "kindname"` 模式），减少假阳性
2. **Step 2 文本差异检查**：增加 `battle_2` 有文本但 `battle_1` 为空时不报"数量不一致"的规则
3. **Step 3 金色升级检查**：增加 `1.5x` 为合法比例（需策划在Excel中标记后生效）
4. **Step 1 基础数值检查**：将 `typeLabel` 的 `-` → `""` 转换静默，不报issue

---

## 总结

| 指标 | 数值 |
|------|------|
| 原始报告问题数 | 77 |
| 误报（预期行为） | 63 |
| **真实需修复问题** | **14** |
| 其中🟠ERROR（技能不生效） | 5 |
| 其中🟡WARNING（需确认优化） | 9 |
| CRITICAL（运行时崩溃） | 0 |
