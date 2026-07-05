# 地图与节点内容配置文档

> 版本：2026-06-06  
> 状态：基于 `Assets/Resources/Data/world_maps.json` 与当前 World Map MVP 实现编写  
> 目标读者：关卡策划、系统策划、客户端程序、QA

## 1. 文档目标

本文档说明世界地图与节点内容如何配置、字段含义、节点类型行为、连接规则和 QA 检查方式。当前配置使用 JSON 文件：

```text
Assets/Resources/Data/world_maps.json
```

加载入口：

- `GameDataRepository`
- `WorldMapDefinition`
- `WorldMapSystem`

## 2. 当前 MVP 地图概览

当前地图 ID：

```text
mvp_3_layer_map
```

地图结构：

```text
Camp -> Outskirts -> Ruins -> Throne
```

当前层级：

| layer index | name | 作用 |
| --- | --- | --- |
| 0 | Camp | 起点 |
| 1 | Outskirts | 第一组选择：战斗或资源 |
| 2 | Ruins | 第二组选择：战斗或宝物 |
| 3 | Throne | Boss |

MVP 最短路径示例：

```text
start -> road_fight_1 -> ruin_guard_1 -> boss_throne
```

## 3. 地图顶层字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 是 | 地图唯一 ID |
| `name` | string | 是 | 地图显示名或策划识别名 |
| `startNodeId` | string | 是 | 起始节点 ID |
| `layers` | array | 是 | 地图层级定义 |
| `nodes` | array | 是 | 节点列表 |
| `connections` | array | 是 | 节点连接列表 |

示例：

```json
{
  "id": "mvp_3_layer_map",
  "name": "MVP Three Layer Map",
  "startNodeId": "start",
  "layers": [],
  "nodes": [],
  "connections": []
}
```

## 4. layer 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `index` | int | 层级序号，越大越接近终点 |
| `name` | string | 层级名称 |

当前 MVP 只使用层级表达地图推进，不支持同层横向移动。

## 5. node 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 是 | 节点唯一 ID |
| `name` | string | 是 | 节点显示名 |
| `layer` | int | 是 | 节点所在层级 |
| `type` | string | 是 | 节点类型 |
| `enemyPresetId` | string | battle/boss 必填 | 敌人预设 ID |
| `reward` | object | 否 | 节点奖励 |
| `x` | float | 是 | UI 横向位置，0 到 1 |
| `y` | float | 是 | UI 纵向位置，0 到 1 |

## 6. 节点类型

| type | 行为 | 是否需要敌人预设 | 完成后 |
| --- | --- | --- | --- |
| `start` | 起点，无奖励 | 否 | 不作为普通可领奖节点 |
| `battle` | 普通地图战斗 | 是 | 胜利清除节点并进入下一回合经营 |
| `resource` | 资源点 | 否 | 发放资源并进入下一回合经营 |
| `treasure` | 宝物点 | 否 | 发放宝物并进入下一回合经营 |
| `boss` | Boss 战 | 是 | 胜利后 Victory |

### 6.1 battle 节点示例

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

### 6.2 resource 节点示例

```json
{
  "id": "supply_cache_1",
  "name": "Supply Cache",
  "layer": 1,
  "type": "resource",
  "reward": { "gold": 3 },
  "x": 0.75,
  "y": 0.35
}
```

### 6.3 treasure 节点示例

```json
{
  "id": "old_relic_1",
  "name": "Old Relic",
  "layer": 2,
  "type": "treasure",
  "reward": { "treasureId": "mvp_old_relic" },
  "x": 0.65,
  "y": 0.62
}
```

### 6.4 boss 节点示例

```json
{
  "id": "boss_throne",
  "name": "Boss Throne",
  "layer": 3,
  "type": "boss",
  "enemyPresetId": "mvp_gate_boss",
  "reward": { "gold": 8, "treasureId": "mvp_boss_trophy" },
  "x": 0.5,
  "y": 0.88
}
```

## 7. reward 字段

当前支持：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `gold` | int | 直接加到玩家金币 |
| `treasureId` | string | 加入 `RunState.inventoryItems` |

重要规则：

- 节点奖励只发放一次。
- 已清除节点不能重复领奖。
- 非 Boss 节点奖励在进入下一回合经营后发放，避免被新回合收入覆盖。
- Boss 节点胜利后直接 Victory，奖励只作为结算数据保留，不进入下一回合经营。

## 8. connections 字段

连接定义玩家可从哪个节点移动到哪个节点。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `fromNodeId` | string | 起点节点 ID |
| `toNodeId` | string | 目标节点 ID |

示例：

```json
{ "fromNodeId": "start", "toNodeId": "road_fight_1" }
```

MVP 规则：

- 只配置向下一层推进。
- 不配置同层横向移动。
- 需要保证 Boss 可达。
- 每个白天玩家只能消耗 1 点行动力选择 1 个节点。

## 9. 运行时节点状态

运行时状态保存在 `RunState.worldMapNodes`，元素类型为 `WorldMapNodeState`。

| 字段 | 说明 |
| --- | --- |
| `nodeId` | 节点 ID |
| `isVisible` | 是否可见 |
| `isVisited` | 是否访问过 |
| `isCleared` | 是否已清除 |

配置文件只定义静态地图，运行中是否可见、是否清除由系统维护。

## 10. 工程实现参考

| 需求 | 代码入口 |
| --- | --- |
| 加载地图配置 | `GameDataRepository` |
| 定义地图数据结构 | `WorldMapDefinition` |
| 判断可移动节点 | `WorldMapSystem.GetAvailableDestinations()` |
| 移动到节点 | `WorldMapSystem.MoveToNode()` |
| 解析节点结果 | `WorldMapSystem.ResolveNode()` |
| 清除节点 | `WorldMapSystem.MarkNodeCleared()` |
| 检查 Boss 胜利 | `WorldMapSystem.CheckVictoryCondition()` |
| 地图 UI | `WorldMapView` |

## 11. 配置 QA 清单

新增或修改地图时至少检查：

- `startNodeId` 是否存在于 `nodes`。
- 所有 `connection.fromNodeId` 和 `connection.toNodeId` 是否存在。
- 所有 `battle` 和 `boss` 节点是否配置 `enemyPresetId`。
- 所有 `enemyPresetId` 是否能在敌人预设中找到。
- Boss 是否可从起点抵达。
- 节点 `x/y` 是否在 0 到 1 范围。
- 奖励字段是否使用当前支持的 `gold` 和 `treasureId`。
- 是否存在重复节点 ID。

当前验证脚本：

```powershell
powershell -ExecutionPolicy Bypass -File tools\validate_world_map_mvp.ps1
```

## 12. 后续扩展池

以下类型尚未实现完整玩法：

- `elite` 精英战斗节点。
- `event` 随机事件节点。
- `shop` 地图商店节点。
- `heal` 回复节点。
- `choice` 多选事件节点。
- 天气、地区词缀、章节推进。

扩展原则：

- 先扩 `WorldMapDefinition` 和 `WorldMapSystem`。
- 再扩 `RunFlowController` 节点结算。
- 最后扩 `WorldMapView` 表现。
- 每新增一种节点类型，必须补 QA 检查。

## 13. 2026-07-06 update: `testmap`

`testmap` is a default test campaign/map entry shown in the campaign selection list after login.

Configured files:
- `Assets/Resources/Data/campaigns.json`: campaign id `testmap`, display name `testmap`, `mapId` `testmap`.
- `Assets/Resources/Data/world_maps.json`: world map id `testmap`, start node `testmap_start`.
- `Assets/Resources/Data/boss_enemies.json`: enemy preset `testmap_paladin`.

Map shape:
- Linear 20-round route: `testmap_start -> testmap_r01 -> ... -> testmap_r20`.
- `testmap_r01` through `testmap_r19` use `normal_battle`.
- `testmap_r20` uses `boss`, so clearing it completes the map through the existing boss victory path.
- Every battle node uses the same `enemyPresetId`: `testmap_paladin`.

Enemy expectation:
- `testmap_paladin` contains exactly one `bright_warrior`.
- Count is fixed at `1`.
- Star is `1`.

Runtime note:
- `BattleStubSystem.IsFixedCapturedPreset()` treats `testmap_` presets as fixed, so exploration battle auto-fill and day-based scaling do not add extra enemies or increase the count.
