# 怪物与敌人预设配置文档

> 版本：2026-06-06  
> 状态：基于 `Assets/Resources/Data/boss_enemies.json` 与当前战斗结算实现编写  
> 目标读者：战斗策划、数值策划、客户端程序、QA

## 1. 文档目标

本文档说明地图战斗使用的敌人预设如何配置、字段含义、与地图节点的引用关系、普通敌人和 Boss 的区别，以及当前工程实现中的权威结算边界。

当前敌人预设文件：

```text
Assets/Resources/Data/boss_enemies.json
```

虽然文件名包含 boss，目前它同时存放普通地图战斗和 Boss 战斗的敌人预设。

## 2. 当前敌人预设概览

当前 MVP 有 3 个敌人预设：

| id | name | type | 用途 |
| --- | --- | --- | --- |
| `mvp_road_bandits` | Road Bandits | `normal` | 第一层普通战斗 |
| `mvp_ruin_guards` | Ruin Guards | `normal` | 第二层普通战斗 |
| `mvp_gate_boss` | Gate Boss | `boss` | 终局 Boss 战 |

地图节点通过 `enemyPresetId` 引用这些预设。

示例：

```json
{
  "id": "road_fight_1",
  "type": "battle",
  "enemyPresetId": "mvp_road_bandits"
}
```

## 3. 敌人预设顶层字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 是 | 敌人预设唯一 ID |
| `name` | string | 是 | 策划识别名或显示名 |
| `type` | string | 是 | `normal` 或 `boss` |
| `units` | array | 是 | 敌方单位列表 |

示例：

```json
{
  "id": "mvp_road_bandits",
  "name": "Road Bandits",
  "type": "normal",
  "units": []
}
```

## 4. 敌方单位字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `slotId` | string | 是 | 敌方阵位 ID |
| `unitId` | string | 是 | 引用单位配置中的单位 ID |
| `count` | int | 是 | 单位数量 |
| `star` | int | 是 | 单位星级 |

当前示例：

```json
{ "slotId": "enemy_1", "unitId": "bright_warrior", "count": 12, "star": 1 }
```

## 5. 当前预设详情

### 5.1 mvp_road_bandits

用途：第一层普通战斗，作为玩家首次地图战斗压力测试。

```json
{
  "id": "mvp_road_bandits",
  "name": "Road Bandits",
  "type": "normal",
  "units": [
    { "slotId": "enemy_1", "unitId": "bright_warrior", "count": 12, "star": 1 },
    { "slotId": "enemy_2", "unitId": "archer", "count": 10, "star": 1 }
  ]
}
```

设计意图：

- 使用低星单位。
- 让玩家感受到基础阵容检查。
- 不应高到阻断大多数合理开局。

### 5.2 mvp_ruin_guards

用途：第二层普通战斗，作为中段阵容检查。

```json
{
  "id": "mvp_ruin_guards",
  "name": "Ruin Guards",
  "type": "normal",
  "units": [
    { "slotId": "enemy_1", "unitId": "knight", "count": 10, "star": 1 },
    { "slotId": "enemy_2", "unitId": "priest", "count": 8, "star": 1 }
  ]
}
```

设计意图：

- 通过骑士和牧师组合形成更高生存压力。
- 检查玩家是否在第二回合完成有效补强。

### 5.3 mvp_gate_boss

用途：终局 Boss。

```json
{
  "id": "mvp_gate_boss",
  "name": "Gate Boss",
  "type": "boss",
  "units": [
    { "slotId": "enemy_1", "unitId": "demon_lord", "count": 1, "star": 3 },
    { "slotId": "enemy_2", "unitId": "shadow_butcher", "count": 12, "star": 2 }
  ]
}
```

设计意图：

- 通过高星核心单位提供终局压力。
- 搭配数量型副单位，避免 Boss 战只有单体比较。
- 胜利后直接通关。

## 6. 普通战斗与 Boss 战区别

| 维度 | normal | boss |
| --- | --- | --- |
| 地图节点类型 | `battle` | `boss` |
| 失败结果 | GameOver | GameOver |
| 胜利结果 | 清除节点，进入下一回合经营 | 清除 Boss，Victory |
| 奖励 | 节点奖励进入下一回合经营后发放 | 胜利结算，直接通关 |
| 设计目标 | 阵容阶段检查 | 终局检查 |

## 7. 战斗结算边界

当前权威结算系统：

```text
BattleStubSystem
```

表现或预览相关系统：

```text
BattleRealtimeSystem
```

重要约束：

- 权威输赢以 `BattleStubSystem.Resolve()` 为准。
- Realtime 当前不能作为最终规则来源。
- QA 已知 Stub 和 Realtime 的 skill kind 覆盖存在差异。
- 新增敌人使用的单位技能必须确认 Stub 侧可结算。

## 8. 敌人强度设计建议

### 8.1 第一层普通战斗

目标：

- 验证玩家基础阵容。
- 给出轻度压力。
- 不要求玩家已经完成复杂合成。

建议：

- 使用 1 星单位。
- 单位数量偏低。
- 尽量避免使用复杂技能组合。

### 8.2 第二层普通战斗

目标：

- 检查玩家是否利用第二回合经营补强。
- 允许明显淘汰错误阵容。

建议：

- 仍可使用 1 星单位，但数量或组合更强。
- 引入防御、治疗、后排输出等简单组合。

### 8.3 Boss 战

目标：

- 成为一局的终局检查。
- 要求玩家已经完成关键部署、购买和合成。

建议：

- 使用高星核心单位。
- 搭配副单位形成前后排或输出/承伤结构。
- Boss 失败直接 GameOver，因此难度不宜依赖隐藏机制。

## 9. 配置 QA 清单

新增或修改敌人预设时检查：

- `id` 是否唯一。
- `type` 是否为当前支持的 `normal` 或 `boss`。
- `units` 是否非空。
- 每个 `unitId` 是否能在单位数据中找到。
- `count` 是否大于 0。
- `star` 是否处于合理范围。
- `slotId` 是否重复。
- 地图节点引用的 `enemyPresetId` 是否存在。
- 新增单位技能是否被 `BattleStubSystem` 支持。

推荐运行：

```powershell
python tools\qa_unit_data.py
python tools\qa_battle_consistency.py
powershell -ExecutionPolicy Bypass -File tools\validate_world_map_mvp.ps1
```

如果 PowerShell 控制台出现编码问题，使用：

```powershell
$env:PYTHONIOENCODING='utf-8'
```

## 10. 后续扩展池

敌人预设后续可扩展：

- 敌人等级。
- 敌人词缀。
- 敌人阵型坐标。
- 随天数或地图层数缩放。
- Boss 专属技能。
- 精英敌人预设。
- 战斗奖励预览。
- 敌人图鉴。

扩展建议：

1. 先扩数据结构。
2. 再扩 `BattleStubSystem` 生成敌方阵容的逻辑。
3. 再扩 QA。
4. 最后扩 UI 表现。
