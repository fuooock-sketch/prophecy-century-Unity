# 单位数据 QA 测试 Skill 文档

> 适用项目：prophecy_century  
> 生效日期：2026-06-04  
> 版本：v1.0  
> 数据链路：Excel(unit_data.xlsx) → Python(import_unit_excel.py) → JSON(unit_data.json) → Unity C#(UnitDefinition.cs / ManageEventResolver.cs / BattleStubSystem.cs / BattleRealtimeSystem.cs)

---

## 目录

1. [QA 测试总览](#1-qa-测试总览)
2. [前置准备](#2-前置准备)
3. [阶段一：基础数值列 Excel → JSON 一致性](#3-阶段一基础数值列-excel--json-一致性)
4. [阶段二：技能文本列 Excel → JSON 一致性](#4-阶段二技能文本列-excel--json-一致性)
5. [阶段三：SkillDefinition 参数完整性](#5-阶段三skilldefinition-参数完整性)
6. [阶段四：JSON kind ⇔ C# 代码双向覆盖率](#6-阶段四json-kind--c-代码双向覆盖率)
7. [阶段五：边界与异常数据检查](#7-阶段五边界与异常数据检查)
8. [问题分级标准](#8-问题分级标准)
9. [自动化脚本使用](#9-自动化脚本使用)
10. [常见问题与修复指南](#10-常见问题与修复指南)

---

## 1. QA 测试总览

### 1.1 数据流

```
Excel 策划配置表                      JSON 运行时数据                     C# 代码执行层
┌──────────────────┐     ┌──────────────────┐     ┌────────────────────────┐
│ unit_data.xlsx   │ ──► │ unit_data.json   │ ──► │ UnitDefinition.cs       │
│  71 个单位       │     │  73 个定义       │     │ SkillDefinition.cs      │
│  25 列           │     │                  │     │ ManageEventResolver.cs  │
│  Sheet: 单位数值设定 │  │  96 种 skill kind │     │ BattleStubSystem.cs     │
└──────────────────┘     └──────────────────┘     │ BattleRealtimeSystem.cs │
                                                  └────────────────────────┘
```

### 1.2 检查维度

| 维度 | 检查内容 | 工具 |
|------|----------|------|
| A~T 列数值 | star, race, faith, startCount, hpPerUnit, attack, defense, initiative, speed, morale, luck, range, size, firstPurchaseHp 等 | `tools/qa_unit_data.py` Step 1 |
| U~X 列技能文本 | talentText, goldTalentText, battleText, goldBattleText 文本一致性 | `tools/qa_unit_data.py` Step 2 |
| SkillDefinition 参数 | kind非空, value/threshold 合理性, 金色升级2x规律 | `tools/qa_unit_data.py` Step 3 |
| kind 代码覆盖率 | JSON 96种 kind ⇔ C# case 语句 双向匹配 | `tools/qa_unit_data.py` Step 4 |
| 边界数据 | 空值, 重复, 范围越界, star≥1 | `tools/qa_unit_data.py` Step 5 |

### 1.3 执行频率

- **每次 Excel 修改后**：执行完整 5 步检查
- **每次 import_unit_excel.py 修改后**：重点关注 Step 2（sanitize 影响）
- **每次 C# 技能新增后**：重点关注 Step 3、Step 4（kind 覆盖率）
- **发版前**：全量 + 手动抽查 10 个重点单位

---

## 2. 前置准备

### 2.1 环境要求

```bash
pip install openpyxl
```

### 2.2 必需文件确认

在执行 QA 前，请确认以下文件存在且是最新版本：

- [ ] `docs/excel/unit_data.xlsx` — Excel 策划表
- [ ] `Assets/Resources/Data/unit_data.json` — 由 `tools/import_unit_excel.py` 生成
- [ ] `Assets/Scripts/Data/UnitDefinition.cs` — 单位数据结构定义
- [ ] `Assets/Scripts/Systems/ManageEventResolver.cs` — 运营阶段技能实现
- [ ] `Assets/Scripts/Systems/BattleStubSystem.cs` — 战斗阶段简化实现
- [ ] `Assets/Scripts/Systems/BattleRealtimeSystem.cs` — 战斗阶段实时实现
- [ ] `tools/import_unit_excel.py` — Excel→JSON 导入脚本

### 2.3 执行命令

```bash
cd c:\projectZhongxu\prophecy_century\prophecy-century-Unity\prophecy_century
python tools/qa_unit_data.py
```

---

## 3. 阶段一：基础数值列 Excel → JSON 一致性

### 3.1 检查范围

Excel A~T 列（共 71 行数据行）与 JSON 中对应字段的逐值对比。

### 3.2 字段映射

| Excel 列 | JSON key | 说明 |
|----------|----------|------|
| A(1) | name | 单位名称 |
| B(2) | star | 星级 (1~6) |
| C(3) | race | 种族 |
| D(4) | typeLabel | 类型标签 |
| E(5) | faith | 信仰 |
| G(7) | startCount | 初始数量 |
| H(8) | hpPerUnit | 单体血量 |
| I(9) | attack | 攻击力 |
| J(10) | defense | 防御力 |
| M(13) | initiative | 先机 |
| N(14) | speed | 速度 |
| O(15) | morale | 士气 |
| P(16) | luck | 幸运 |
| Q(17) | range | 射程 |
| R(18) | size | 体型 |
| S(19) | firstPurchaseHp | 首次购买总血量 |
| T(20) | firstPurchaseAverageDamage | 首次购买均伤 |

### 3.3 已知忽略项

- `typeLabel`（D列）：Excel 中为 `-` 时，import 脚本会转换为空字符串 `""`。这是一个**低优先级设计决策**，不是 bug。
- 列 F(tag)、K(damageMin)、L(damageMax) 不在本次检查范围内（JSON 中有独立计算逻辑）。

### 3.4 验收标准

- `PASS`：所有字段值完全一致（typeLabel 的 `-` → `""` 转换视为预期行为）
- `WARNING`：typeLabel 以外的字段有差异
- `FAIL`：star/race/faith/hpPerUnit 等核心字段不一致

---

## 4. 阶段二：技能文本列 Excel → JSON 一致性

### 4.1 检查范围

U~X 列（talent_1, talent_2, battle_1, battle_2）的文本内容对比。

### 4.2 Sanitize 预期差异

`import_unit_excel.py` 第 67~84 行定义了以下关键词替换，这些差异是**预期行为**：

| Excel 原文 | JSON 替换后 | 原因 |
|------------|-------------|------|
| `补员` | `获得数量` | 术语统一（第 72 行） |
| `获得其当前数量50%` | `获得固定数量+1` | 比例→固定值重构（第 73 行） |
| `50%的额外数量` | `固定数量+3` | 同上（第 76 行） |
| `100%的额外数量` | `固定数量+6` | 同上（第 77 行） |
| `数量与场上数量最多的火元素一致` | `固定数量为22` | 动态→固定值重构（第 78 行） |
| `比场上数量最多的火元素多20%` | `固定数量为26` | 同上（第 79 行） |
| `25%/30% 的本驯兽师/兽骑兵数量` | `格尔兽默认数量` | 百分比→固定值重构（第 80~83 行） |

### 4.3 验收标准

- `PASS`：所有文本差异都是 sanitize 预期行为
- `WARNING`：存在非 sanitize 的文本差异（可能是 Excel 版本比 JSON 新）
- `FAIL`：核心技能描述（触发方式/效果类型）在 Excel 和 JSON 之间不一致

---

## 5. 阶段三：SkillDefinition 参数完整性

### 5.1 检查项

#### 5.1.1 kind 非空检查
每个 SkillDefinition 必须有 `kind` 字段，且值不能为空字符串。

#### 5.1.2 技能文本 ⇔ kind 对应检查
如果某个单位的 talentText/battleText 有文字描述，则对应的 talents/battleSkills 数组**必须有至少一个定义**。反之，如果文本为 `—`，则数组可以为空。

#### 5.1.3 金色升级数值规律检查
普通形态 → 金色升级形态的数值变化应符合以下规律之一：

| 模式 | 示例 | 判断依据 |
|------|------|----------|
| **2x 翻倍** | +1→+2, +4→+8, +10→+20 | `goldValue == normalValue * 2` |
| **1.5x 线性** | +2→+3, +10→+15, +5→+8 | `goldValue == normalValue + normalValue/2`（少数单位） |
| **等值** | 阈值不变, kind 不变 | `goldValue == normalValue` |
| **kind 变更** | 质变技能 | 如普通减速→金色半速攻击间隔 |

如果金色值和普通值的比例超出以上模式，需人工确认是否是策划有意为之。

#### 5.1.4 kind 语义检查
抽样检查 kind 名称与其对应文本中的关键词是否匹配。例如：
- `kind=round_end_if_adjacent_faith_self_gain_attack` 的文本应包含 `回合结束`、`相邻`、`信仰`
- `kind=battle_start_pounce_nearest_damage` 的文本应包含 `冲到`、`暴击`（如雪狮的"猛扑"替代了"冲到"，需确认）

### 5.2 验收标准

- `PASS`：所有 kind 非空、文本有则 kind 有、金色升级规律符合模式
- `WARNING`：存在非标准金色升级比例（需策划确认）
- `FAIL`：存在 kind 为空的技能定义

---

## 6. 阶段四：JSON kind ⇔ C# 代码双向覆盖率

### 6.1 检查方法

从以下 C# 文件中提取所有 `case "kind_name":` 语句，与 JSON 中的 kind 做差集运算：

```
Assets/Scripts/Systems/ManageEventResolver.cs    (运营阶段)
Assets/Scripts/Systems/BattleStubSystem.cs        (战斗简化)
Assets/Scripts/Systems/BattleRealtimeSystem.cs    (战斗实时)
```

### 6.2 问题分类

#### 6.2.1 kind 仅在 JSON 中存在（缺少 C# 实现）
这是**最严重的 bug**。如果某个 kind 在 JSON 中定义了但 C# 中没有对应的 `case` 语句，则该技能在运行时不会生效。

检查时需要确定是否：
- kind 被实现为"被动光环"（如 `same_row_units_count_as_race`），需要在 `ApplyContinuousAuras` 中查找
- kind 被实现在其他 CS 文件中尚未被 QA 脚本扫描
- kind 确实遗漏了实现（需补充代码）

#### 6.2.2 kind 仅在 C# 中存在（JSON 未使用）
这通常是"已废弃的技能"或"未上线的新技能"。建议定期清理或标记为 `[Obsolete]`。

### 6.3 验收标准

- `PASS`：JSON kind 100% 在 C# 中找到对应实现
- `FAIL`：存在 JSON 中有定义但 C# 中无实现的 kind

---

## 7. 阶段五：边界与异常数据检查

### 7.1 检查项

| 检查项 | 标准 | 严重度 |
|--------|------|--------|
| id 为空 | 不应存在 | 🔴 CRITICAL |
| name 为空 | 不应存在 | 🔴 CRITICAL |
| 重复 name | 不应存在（同名不同级需区分） | 🟡 WARNING |
| star < 1 或 star > 6 | 应在 1~6 之间 | 🟠 ERROR |
| hp <= 0 | 应 > 0 | 🟠 ERROR |
| startCount <= 0 | 应 > 0 | 🟠 ERROR |
| hidden=true 且无技能 | 确认是否是占位单位 | 🟡 WARNING |

### 7.2 特殊单位说明

- **幻影**（id=`phantom`）：由光明导师召唤的幻影部队，不是直接上阵单位。`star=0` 是预期设计，因为它是战斗中召唤的单位而非从商店购买的单位。
- **巫兽师**（id=`witch_beast_master`）：由邪恶女巫骑乘格尔兽后临时变身，`talents` 为空是预期行为（属性继承自邪恶女巫）。

### 7.3 验收标准

- `PASS`：所有可上阵单位通过边界检查，特殊单位有合理解释
- `FAIL`：存在 CRITICAL 级别的异常数据

---

## 8. 问题分级标准

| 级别 | 标识 | 含义 | 处理时限 |
|------|------|------|----------|
| 🔴 CRITICAL | 严重 | 会导致运行时崩溃或技能完全失效 | 立即修复 |
| 🟠 ERROR | 错误 | 数据不一致可能导致玩家体验异常 | 本次迭代内修复 |
| 🟡 WARNING | 警告 | 风味文本/术语差异或非标准数值 | 需策划确认 |
| ⚪ INFO | 信息 | 已知预期差异或文档记录用途 | 无需处理 |

---

## 9. 自动化脚本使用

### 9.1 主要脚本

```bash
# 全量 QA 检查（推荐每次使用）
python tools/qa_unit_data.py

# 仅检查基础数值列
python tools/qa_unit_data.py --step 1

# 输出详细 JSON 报告
python tools/qa_unit_data.py --output report.json
```

### 9.2 报告解读

脚本输出包含：
1. **Step 1~5** 的逐项检查结果
2. 每个 issue 包含单位名称、具体字段、期望值 vs 实际值
3. 最后汇总：总问题数 + 结论（PASS / 少量问题 / 问题较多）

### 9.3 持续集成建议

```yaml
# GitHub Actions 示例
- name: QA Unit Data
  run: python tools/qa_unit_data.py
  continue-on-error: true  # QA 不阻塞构建，但生成报告供审查
```

---

## 10. 常见问题与修复指南

### 10.1 Excel 修改后 typeLabel 不一致

**现象**：`Excel='-' ≠ JSON=''`

**原因**：`import_unit_excel.py` 第 170 行 `first_non_empty(row.get(4))` 将 `-` 视为空值。

**修复**：无需修复，这是预期行为。如果策划需要 typeLabel 显示 `-`，请修改 import 脚本。

### 10.2 技能文本有描述但 kind 为空

**现象**：`有文本但 {arrKey} 为空`

**原因**：策划在 Excel 中写了技能描述，但对应的 JSON SkillDefinition 尚未配置。

**修复步骤**：
1. 确认该技能需要新增 kind 还是复用现有 kind
2. 在 JSON 对应字段中添加 `{ "kind": "对应的kind名称", "value": N, ... }`
3. 如果 kind 是新的，同时在对应的 C# 文件中添加 `case` 实现
4. 重新运行 import_unit_excel.py 或手动编辑 JSON

### 10.3 金色升级 value 不是 2x

**现象**：`value N→M (不是2x)`

**修复步骤**：
1. 确认这是策划有意设计的非 2x 数值（如 +2→+3 是 1.5x）
2. 如果是，更新文档记录
3. 如果不是，修正 JSON 或 Excel 中的数值

### 10.4 kind 仅在 JSON 中存在

**现象**：`仅在JSON中存在(缺少C#实现)`

**修复步骤**：
1. 搜索项目中所有 `.cs` 文件确认该 kind 是否在其他文件中实现
2. 如果是被动光环/持续效果，检查 `ApplyContinuousAuras` 方法
3. 如果确实未实现，在对应的 Resolver 中添加 `case` 实现
4. 更新本 QA 文档的检查脚本以覆盖新文件

---

## 附录 A：当前检查结果摘要 (2026-06-04)

| 检查项 | 问题数 | 状态 |
|--------|--------|------|
| 基础数值不一致 | 25 | 🟡 均为 typeLabel `-` → `""` 转换，属于预期行为 |
| 技能文本差异 | 14 | 🟡 均为 sanitize 预期替换或术语统一 |
| kind 为空 | 0 | ✅ PASS |
| 有文本无 kind | 10 | 🟠 需补充 SkillDefinition（卫戍协兵/莱特使者/莱特的回响/猎豹/血淤魔） |
| 金色升级疑点 | 10 | 🟡 非标准比例需策划确认 |
| kind 语义疑点 | 6 | 🟡 雪狮"猛扑"vs"冲到"，火元素"每获得1次数量"vs kind 名 |
| kind 仅 JSON 无 C# | 11 | 🟠 需确认是否在其他文件中实现 |
| 数值范围异常 | 1 | 🟡 幻影 star=0 是召唤物，属于预期设计 |

**有效问题数**：约 10~15 个（排除 typeLabel、sanitize、特殊单位后的真实问题）

---

## 附录 B：Skill kind 快速索引

项目中目前定义 **96 种** skill kind，分为以下大类：

| 类别 | 数量 | 典型 kind |
|------|------|-----------|
| 运营入场 | ~15 | `while_on_board_on_entry_race_self_gain_attack`, `on_entry_*` |
| 运营离场 | ~6 | `leave_board_gain_gold`, `on_leave_*` |
| 运营回合结束 | ~8 | `round_end_*` |
| 运营回合开始 | ~4 | `round_start_*` |
| 运营获得数量 | ~8 | `on_gain_*` |
| 运营吞噬 | ~6 | `*devour*` |
| 运营赐予 | ~10 | `*gift*`, `*receive_gift*`, `*forest_gem*` |
| 运营商店/手牌 | ~8 | `*shop*`, `*hand*` |
| 战斗开战 | ~18 | `battle_start_*` |
| 战斗攻击中 | ~10 | `on_attack_*`, `*hits_*` |
| 战斗阵亡 | ~5 | `on_death_*`, `on_ally_death_*` |
| 其他 | ~8 | 被动持续效果、光环等 |

---

> **文档维护**：每次 Excel 结构变更或新增 skill kind 时，请同步更新此文档的附录 A 和 B。