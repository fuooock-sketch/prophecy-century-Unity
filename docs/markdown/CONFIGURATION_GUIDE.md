# 策划配置方式说明

> 版本：2026-06-06  
> 范围：单局流程、地图与节点、怪物与敌人预设  
> 用途：说明飞书在线配置表中每一项该如何填写，以及它与当前 Unity 工程实现的对应关系。

## 1. 配置入口

所有策划在线配置表已移动到飞书文件夹：

https://ifosuw0aw4.feishu.cn/drive/folder/BNXQf6TiQl3AQpdSFetcyNLOnLd

本地 CSV 源文件保存在：

```text
docs/markdown/config_tables/
```

当前在线表格：

| 配置域 | 表格 | 链接 |
| --- | --- | --- |
| 单局流程 | 单局流程_阶段状态配置表 | https://ifosuw0aw4.feishu.cn/sheets/PpG3sDuBmh2b8ZtvS93cCgWsnCe |
| 单局流程 | 单局流程_触发时机配置表 | https://ifosuw0aw4.feishu.cn/sheets/BApfsuaSfhO5fbtHqRUc7LQmnug |
| 地图配置 | 地图配置_地图定义表 | https://ifosuw0aw4.feishu.cn/sheets/ESdNsWHuDhXpKEt4UOJcMBrKnHe |
| 地图配置 | 地图配置_层级表 | https://ifosuw0aw4.feishu.cn/sheets/EkuIsoXkBhdUZwtls8FcXGFdnvO |
| 地图配置 | 地图配置_节点表 | https://ifosuw0aw4.feishu.cn/sheets/Xq3wsxwVrhA3Z3tPdTrcoOOpn9b |
| 地图配置 | 地图配置_连接表 | https://ifosuw0aw4.feishu.cn/sheets/THE3szt6HhHgIrtQy7acEUJRnEL |
| 怪物配置 | 怪物配置_敌人预设表 | https://ifosuw0aw4.feishu.cn/sheets/Ac6es3SdthlNJ1twpidcXVZGnOc |
| 怪物配置 | 怪物配置_敌方单位明细表 | https://ifosuw0aw4.feishu.cn/sheets/Hv3ysSdjkhRKsLt0zecc1MA9nnS |

## 2. 配置到工程的关系

飞书表格当前是策划配置源和沟通源，不会自动同步进 Unity。真正运行时读取的配置仍在 Unity 工程中：

| 玩法内容 | 运行时文件 | 主要代码 |
| --- | --- | --- |
| 地图、层级、节点、连接 | `Assets/Resources/Data/world_maps.json` | `WorldMapDefinition`, `WorldMapSystem` |
| 怪物与敌人预设 | `Assets/Resources/Data/boss_enemies.json` | `EnemyPresetDefinition`, `BattleStubSystem` |
| 单局状态流 | C# 代码 | `RunFlowController`, `DayNightCycleController`, `RunSceneController` |

当前推荐流程：

1. 策划先在飞书表格中编辑。
2. 程序或构建工具审核字段合法性。
3. 将表格内容同步到对应 JSON 或代码配置。
4. 运行 QA 脚本。
5. 在 Unity 内完成一次关键流程验证。

## 3. 单局流程_阶段状态配置表

用途：描述单局状态机中每个阶段的含义和跳转边界。当前阶段多数是代码规则，不建议策划频繁改动，但这张表是理解流程和做需求评审的基准。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `phase` | 使用 `GamePhase` 枚举名 | `NightManage` | 必须与 C# 枚举一致 |
| `state` | 使用兼容旧流程的字符串 | `manage` | 必须与 UI 和存档逻辑兼容 |
| `display_name` | 中文显示名 | `夜晚经营` | 给策划和 QA 阅读 |
| `player_action` | 玩家在该阶段可以做什么 | `购买/部署/调整阵容/点击探索` | 不直接参与程序解析 |
| `enter_condition` | 进入该阶段的条件 | `新局开始或节点完成后进入下一回合` | 用于流程审查 |
| `exit_condition` | 离开该阶段的条件 | `点击探索且无未完成选择` | 用于判断是否有缺口 |
| `engineering_entry` | 当前代码入口 | `RunFlowController.StartNewDay` | 程序参考 |
| `notes` | 备注 | `MVP 每天 1 个节点` | 记录约束和例外 |

配置规则：

- `phase` 不应随意新增。新增阶段前必须先扩展 `GamePhase`、存档兼容、UI 刷新逻辑。
- `state` 是旧流程兼容字段，不能只改表格不改代码。
- Victory 和 GameOver 是终局状态，不能再跳回地图或经营，除非明确做复活/续局系统。

常见错误：

- 把 `DayExplore` 写成 `Day`，代码无法识别。
- 只新增阶段说明，没有实现对应 UI 切换。
- 让失败返回经营，这与当前规则冲突；当前任意战斗失败直接 GameOver。

## 4. 单局流程_触发时机配置表

用途：描述一局中关键事件的触发时机、条件和效果。适合策划设计新事件、能力触发、奖励触发时先对齐时机。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `trigger_id` | 小写英文唯一 ID | `round_end` | 建议 snake_case |
| `trigger_name` | 中文名称 | `经营回合结束` | 给策划和 QA 阅读 |
| `phase` | 所属阶段 | `NightManage` | 对应 `GamePhase` |
| `timing` | 精确触发时点 | `点击探索后` | 说明先后顺序 |
| `condition` | 触发条件 | `无未完成选择` | 必须可验证 |
| `effect_summary` | 效果摘要 | `结算回合结束效果/合成/反馈` | 说明产生什么结果 |
| `engineering_entry` | 代码入口 | `RunFlowController.ResolveRoundEndBeforeBattle` | 程序参考 |
| `configurable_now` | 是否当前可配置 | `true` / `false` | `false` 表示目前写死在代码里 |
| `notes` | 备注 | `探索按钮触发` | 记录边界 |

配置规则：

- 同一个 `trigger_id` 只能表示一个时机。
- 若 `configurable_now=false`，策划只能提出需求，不能假设表格修改会生效。
- 涉及战斗胜负、GameOver、Victory 的触发要特别谨慎，因为它们直接影响单局闭环。

关键顺序：

```text
NightManage 点击探索
-> round_end
-> day_start
-> node_select
-> battle_start 或 node_complete
-> battle_defeat / boss_victory / node_complete
```

常见错误：

- 把 `round_end` 放到地图战斗开始时重复触发。当前规则明确禁止地图战斗重复结算经营回合结束。
- 把节点奖励放在 `NextRound()` 之前发金币。这样会被新回合收入覆盖。

## 5. 地图配置_地图定义表

用途：定义一张地图的基础信息。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `map_id` | 英文唯一 ID | `mvp_3_layer_map` | 对应 JSON 的 `id` |
| `name` | 地图名称 | `MVP Three Layer Map` | 可显示或仅策划识别 |
| `start_node_id` | 起点节点 ID | `start` | 必须存在于节点表 |
| `design_goal` | 地图设计目标 | `验证三层地图闭环` | 说明地图用途 |
| `current_status` | 状态 | `implemented` | 建议值：`draft`, `implemented`, `deprecated` |
| `owner_notes` | 备注 | `当前 MVP 主地图` | 负责人或说明 |

配置规则：

- `map_id` 必须唯一。
- `start_node_id` 必须能在“地图配置_节点表”中找到。
- 一张地图至少需要：起点、一个可探索节点、一个 Boss 节点。

对应 JSON：

```json
{
  "id": "mvp_3_layer_map",
  "name": "MVP Three Layer Map",
  "startNodeId": "start"
}
```

## 6. 地图配置_层级表

用途：定义地图从起点到 Boss 的层级结构。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `map_id` | 所属地图 ID | `mvp_3_layer_map` | 必须存在于地图定义表 |
| `layer_index` | 整数，从 0 开始 | `1` | 层级越大越接近 Boss |
| `layer_name` | 层级名称 | `Outskirts` | 策划识别或 UI 显示 |
| `design_role` | 层级设计作用 | `第一组选择` | 说明这一层承担什么体验 |
| `expected_node_count` | 预期节点数 | `2` | 用于检查节点表是否完整 |
| `notes` | 备注 | `普通战斗或资源` | 说明该层特点 |

配置规则：

- `layer_index=0` 通常是起点层。
- Boss 层通常是最后一层。
- MVP 不支持同层横向移动，所以连接应主要从低层指向高层。

对应 JSON：

```json
"layers": [
  { "index": 0, "name": "Camp" },
  { "index": 1, "name": "Outskirts" }
]
```

## 7. 地图配置_节点表

用途：定义地图上的每个节点。这是地图配置中最重要的表。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `map_id` | 所属地图 ID | `mvp_3_layer_map` | 必须存在于地图定义表 |
| `node_id` | 英文唯一 ID | `road_fight_1` | 同一地图内唯一 |
| `node_name` | 节点名称 | `Broken Road` | UI 显示或策划识别 |
| `layer` | 所在层级 | `1` | 必须存在于层级表 |
| `type` | 节点类型 | `battle` | 当前支持：`start`, `battle`, `resource`, `treasure`, `boss` |
| `enemy_preset_id` | 敌人预设 ID | `mvp_road_bandits` | `battle` 和 `boss` 必填 |
| `reward_gold` | 金币奖励 | `2` | 无奖励填 `0` |
| `reward_treasure_id` | 宝物 ID | `mvp_old_relic` | 无宝物留空 |
| `x` | UI 横向位置 | `0.25` | 0 到 1 |
| `y` | UI 纵向位置 | `0.35` | 0 到 1 |
| `design_intent` | 设计意图 | `第一场普通战斗` | 策划说明 |
| `completion_result` | 完成后结果 | `胜利后进入下一经营回合` | QA 和程序参考 |
| `notes` | 备注 | `失败 GameOver` | 记录特殊规则 |

节点类型填写规则：

| type | `enemy_preset_id` | 奖励 | 完成行为 |
| --- | --- | --- | --- |
| `start` | 留空 | 通常无奖励 | 起点，不作为普通领奖节点 |
| `battle` | 必填 | 可配金币/宝物 | 胜利清除节点并进入下一回合经营 |
| `resource` | 留空 | 通常配金币 | 领取后进入下一回合经营 |
| `treasure` | 留空 | 通常配宝物 | 领取后进入下一回合经营 |
| `boss` | 必填 | 可配奖励 | 胜利直接 Victory |

配置规则：

- `node_id` 不能重复。
- `battle` / `boss` 节点必须能在“怪物配置_敌人预设表”中找到 `enemy_preset_id`。
- `x` / `y` 必须在 0 到 1 之间。
- 非 Boss 节点完成后会进入下一经营回合。
- 任意战斗失败都会 GameOver。

对应 JSON：

```json
{
  "id": "road_fight_1",
  "name": "Broken Road",
  "layer": 1,
  "type": "battle",
  "enemyPresetId": "mvp_road_bandits",
  "reward": { "gold": 2 },
  "x": 0.25,
  "y": 0.35
}
```

常见错误：

- `battle` 节点忘填 `enemy_preset_id`。
- `reward_gold` 留空而不是填 `0`，同步脚本如果未做容错可能出错。
- `reward_treasure_id` 填了不存在的宝物 ID。
- `x/y` 超出范围导致 UI 位置异常。

## 8. 地图配置_连接表

用途：定义节点之间的可移动路径。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `map_id` | 所属地图 ID | `mvp_3_layer_map` | 必须存在于地图定义表 |
| `from_node_id` | 起点节点 ID | `start` | 必须存在于节点表 |
| `to_node_id` | 目标节点 ID | `road_fight_1` | 必须存在于节点表 |
| `design_purpose` | 连接设计目的 | `起点到第一层战斗` | 说明路线意义 |
| `enabled` | 是否启用 | `true` | 暂时建议只用 `true` |
| `notes` | 备注 | 留空 | 可记录特殊路线 |

配置规则：

- `from_node_id` 和 `to_node_id` 都必须存在。
- 当前 MVP 建议只配置从低层到高层的连接。
- 必须保证从 `start_node_id` 可以走到 Boss。
- 不要配置循环路线，除非程序明确支持。

对应 JSON：

```json
{ "fromNodeId": "start", "toNodeId": "road_fight_1" }
```

常见错误：

- 配了孤立节点，没有任何连接。
- Boss 没有入口连接。
- 节点 ID 拼写和节点表不一致。

## 9. 怪物配置_敌人预设表

用途：定义一次战斗的敌人组合。地图节点通过 `enemy_preset_id` 引用这里的预设。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `enemy_preset_id` | 英文唯一 ID | `mvp_road_bandits` | 对应 JSON 的 `id` |
| `name` | 预设名称 | `Road Bandits` | 策划识别或 UI 显示 |
| `type` | 敌人类型 | `normal` / `boss` | 普通战或 Boss |
| `used_by_node_id` | 使用它的节点 | `road_fight_1` | 便于追踪引用 |
| `difficulty_role` | 难度作用 | `第一层基础阵容检查` | 数值设计说明 |
| `defeat_result` | 玩家失败结果 | `GameOver` | 当前固定为 GameOver |
| `victory_result` | 玩家胜利结果 | `清除节点并进入下一经营回合` | Boss 是 Victory |
| `notes` | 备注 | `不应过高阻断合理开局` | 设计注意事项 |

配置规则：

- `enemy_preset_id` 必须唯一。
- `type=normal` 通常用于 `battle` 节点。
- `type=boss` 通常用于 `boss` 节点。
- 每个预设必须至少在“怪物配置_敌方单位明细表”中有 1 行单位。

对应 JSON：

```json
{
  "id": "mvp_road_bandits",
  "name": "Road Bandits",
  "type": "normal",
  "units": []
}
```

## 10. 怪物配置_敌方单位明细表

用途：定义每个敌人预设里具体有哪些敌方单位。

字段说明：

| 字段 | 填写方式 | 示例 | 说明 |
| --- | --- | --- | --- |
| `enemy_preset_id` | 所属敌人预设 | `mvp_road_bandits` | 必须存在于敌人预设表 |
| `slot_id` | 敌方槽位 ID | `enemy_1` | 同一预设内唯一 |
| `unit_id` | 单位 ID | `bright_warrior` | 必须存在于单位数据 |
| `count` | 数量 | `12` | 必须大于 0 |
| `star` | 星级 | `1` | 建议 1 到 6，Boss 可更高压 |
| `role_in_encounter` | 战斗定位 | `前排压力` | 数值设计说明 |
| `notes` | 备注 | `低星基础单位` | 额外说明 |

配置规则：

- `enemy_preset_id` 必须存在于敌人预设表。
- 同一 `enemy_preset_id` 下 `slot_id` 不能重复。
- `unit_id` 必须存在于单位配置。
- `count` 必须大于 0。
- `star` 必须符合当前单位系统支持范围。

对应 JSON：

```json
{
  "slotId": "enemy_1",
  "unitId": "bright_warrior",
  "count": 12,
  "star": 1
}
```

难度建议：

- 第一层普通战：1 星，低数量，主要测试基础阵容。
- 第二层普通战：仍可 1 星，但组合更完整。
- Boss：使用高星核心单位和副单位组合。

## 11. 表格同步到 JSON 的建议规则

当前还没有自动同步工具。手工同步时建议遵循：

### 11.1 地图表同步

从这些表生成：

- 地图配置_地图定义表
- 地图配置_层级表
- 地图配置_节点表
- 地图配置_连接表

目标文件：

```text
Assets/Resources/Data/world_maps.json
```

字段映射：

| 表格字段 | JSON 字段 |
| --- | --- |
| `map_id` | `id` |
| `start_node_id` | `startNodeId` |
| `layer_index` | `layers[].index` |
| `layer_name` | `layers[].name` |
| `node_id` | `nodes[].id` |
| `node_name` | `nodes[].name` |
| `enemy_preset_id` | `nodes[].enemyPresetId` |
| `reward_gold` | `nodes[].reward.gold` |
| `reward_treasure_id` | `nodes[].reward.treasureId` |
| `from_node_id` | `connections[].fromNodeId` |
| `to_node_id` | `connections[].toNodeId` |

### 11.2 敌人表同步

从这些表生成：

- 怪物配置_敌人预设表
- 怪物配置_敌方单位明细表

目标文件：

```text
Assets/Resources/Data/boss_enemies.json
```

字段映射：

| 表格字段 | JSON 字段 |
| --- | --- |
| `enemy_preset_id` | `id` |
| `name` | `name` |
| `type` | `type` |
| `slot_id` | `units[].slotId` |
| `unit_id` | `units[].unitId` |
| `count` | `units[].count` |
| `star` | `units[].star` |

## 12. 配置发布前检查清单

地图检查：

- 地图定义表中每个 `map_id` 唯一。
- 起点 `start_node_id` 存在。
- 每个节点 ID 唯一。
- 每个节点的 `layer` 存在。
- 每个 `battle` / `boss` 节点都填了 `enemy_preset_id`。
- 每个连接的起点和终点都存在。
- Boss 从起点可达。
- `x/y` 在 0 到 1 范围。

怪物检查：

- 每个 `enemy_preset_id` 唯一。
- 每个预设至少有一行单位。
- 单位 ID 存在于单位数据。
- `count > 0`。
- `star` 合理。
- Boss 预设被 Boss 节点引用。

流程检查：

- 探索按钮仍先触发经营回合结束。
- 白天只选择 1 个节点。
- 普通节点完成后进入下一回合经营。
- 战斗失败 GameOver。
- Boss 胜利 Victory。

推荐运行：

```powershell
$env:PYTHONIOENCODING='utf-8'
python tools\qa_unit_data.py
python tools\qa_battle_consistency.py
powershell -ExecutionPolicy Bypass -File tools\validate_world_map_mvp.ps1
```

## 13. 未来工具化建议

建议后续增加一个导入脚本：

```text
Feishu Sheets -> 本地 CSV -> JSON -> QA -> Unity 验证
```

脚本应支持：

- 下载飞书表格为 CSV。
- 校验字段类型和引用关系。
- 生成 `world_maps.json`。
- 生成 `boss_enemies.json`。
- 输出配置差异报告。
- 自动运行 QA。

在工具完成前，飞书表格应被视为策划源，Unity JSON 应被视为运行源。
