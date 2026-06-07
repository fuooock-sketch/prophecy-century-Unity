# 单局游戏流程设计文档

> 版本：2026-06-06  
> 状态：基于当前 World Map MVP 已验证流程编写  
> 目标读者：策划、客户端程序、战斗系统程序、内容配置维护者

## 1. 文档目标

本文档定义《Prophecy Century》当前单局游戏的完整玩家流程、状态机语义、关键触发时机和工程入口。它优先记录已经实现并验证的规则，未实现的扩展只放在“后续扩展”章节。

当前单局 MVP 的核心闭环是：

```text
选择英雄 -> 夜晚经营/部署 -> 点击探索 -> 结算经营回合结束效果 -> 白天地图 -> 选择 1 个节点 -> 结算节点 -> 下一回合夜晚经营 -> 重复 -> Boss 胜利通关
```

失败闭环是：

```text
任意战斗失败 -> GameOver -> 重新开始 -> 回到标题并打开英雄选择
```

## 2. 单局目标

玩家在一局中选择一个英雄，通过夜晚经营阶段购买和部署单位，再在白天地图阶段逐层探索节点。玩家需要在普通节点中获得资源、战胜敌人，并最终击败 Boss 节点完成通关。

MVP 不追求复杂分支事件，重点验证以下体验：

- 玩家先经营，再探索，而不是开局直接进地图。
- 每个白天只能选择 1 个地图节点。
- 节点完成后立即进入下一回合经营。
- 普通战斗胜利推进地图，失败立即结束本局。
- Boss 胜利直接通关。

## 3. 状态机定义

运行时主状态保存在 `RunState.phase`，兼容旧流程的字符串状态保存在 `RunState.state`。

| GamePhase | state | 含义 | 主要入口 |
| --- | --- | --- | --- |
| `NightManage` | `manage` | 夜晚经营。玩家购买、刷新、升级商店、部署和调整阵容。 | `RunFlowController.NextRound()` / `StartNextNightManageAfterDayNode()` |
| `DayExplore` | `day` | 白天探索。玩家在世界地图上选择 1 个可达节点。 | `RunFlowController.StartNewDay()` |
| `Battle` | `battle` | 战斗阶段。播放战斗准备和战斗过程。 | `RunFlowController.SetBattlePhase()` |
| `Settle` | `settle` | 战斗结算中间态。 | `RunFlowController.FinishBattlePhase()` |
| `Victory` | `victory` | 本局胜利。 | `WorldMapSystem.CheckVictoryCondition()` |
| `GameOver` | `gameover` | 本局失败。 | `RunFlowController.ResolveBattleOutcome()` |

## 4. 开局流程

### 4.1 选择英雄

玩家从标题界面进入英雄选择。

工程入口：

- `RunSceneController.OpenHeroSelection()`
- `RunSceneController.StartSelectedRunWithHero(string heroId)`
- `RunFlowController.PrepareNewRun(string campaignId, string heroId)`
- `ProphecyGameSession.StartNewRun(string campaignId, string heroId)`

### 4.2 新局默认状态

新局初始化后的关键字段：

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `phase` | `GamePhase.NightManage` | 开局进入夜晚经营 |
| `state` | `manage` | 兼容旧 UI 流程 |
| `round` | `1` | 第一经营回合 |
| `dayCount` | `0` | 尚未开始白天探索 |
| `maxMovePoints` | `1` | MVP 每天只能走 1 个节点 |
| `remainingMovePoints` | `0` | 夜晚阶段不能移动 |
| `currentNodeId` | 地图起点 | 默认来自 `WorldMapDefinition.startNodeId` |

## 5. 夜晚经营阶段

夜晚经营阶段是玩家主要决策阶段。

玩家可执行：

- 购买商店单位。
- 刷新商店。
- 升级商店。
- 部署手牌到棋盘。
- 调整阵容。
- 处理金色上阵奖励或指定目标祝福。
- 点击“探索”结束经营并进入白天地图。

### 5.1 点击探索前的拦截

如果存在未完成选择，不能进入探索：

- 仍有指定祝福目标未选择。
- 金色上阵奖励弹窗未完成。
- 当前已经在战斗播放或白天过渡中。
- 当前不是 `NightManage/manage`。

工程入口：

- `RunSceneController.StartDayExploreFromManage()`

### 5.2 探索按钮语义

“探索”不是单纯切换地图，而是“结束当前夜晚经营回合并开始白天探索”。

执行顺序：

1. 捕获金币和单位数量快照，用于 UI 反馈。
2. `RunFlowController.ResolveRoundEndBeforeBattle()`。
3. 消费并播放经营反馈事件。
4. 播放能力触发音效和合成音效。
5. `RunFlowController.StartNewDay()`。
6. 进入 `DayExplore/day`，行动力恢复为 1。

注意：地图战斗开始时不能再次结算经营回合结束效果。

## 6. 白天地图阶段

白天地图阶段只允许玩家选择 1 个可达节点。

工程入口：

- `WorldMapView`
- `RunSceneController.SelectWorldMapNode(string nodeId)`
- `RunFlowController.MoveToMapNode(string nodeId)`
- `WorldMapSystem.MoveToNode()`
- `WorldMapSystem.ResolveNode()`

### 6.1 可选节点规则

节点可选需要满足：

- 节点已可见。
- 节点与当前位置有连接。
- 节点未清除或允许访问。
- 当前剩余行动力大于 0。

MVP 中行动力固定为 1，因此每个白天只能完成 1 个节点。

### 6.2 节点结果

节点结果由 `NodeEventResult` 表示。

| 节点结果 | 当前行为 |
| --- | --- |
| `Battle` | 设置探索战斗上下文，进入战斗 |
| `Boss` | 设置探索战斗上下文，进入 Boss 战 |
| `Resource` | 发放资源，清除节点，进入下一回合经营 |
| `Treasure` | 发放宝物，清除节点，进入下一回合经营 |
| `AlreadyCleared` | 不重复领奖 |
| `None` | 无特殊结算 |

## 7. 战斗阶段

当前权威战斗结算使用 `BattleStubSystem`。

战斗阶段流程：

1. 如果不是地图探索战斗，先结算经营回合结束效果。
2. 如果是地图探索战斗，跳过经营回合结束效果。
3. 创建战斗预览。
4. 展示战斗准备。
5. 玩家确认开始行动。
6. 播放战斗表现。
7. 使用权威结果结算。
8. 根据胜负进入 Victory、GameOver 或下一回合经营。

工程入口：

- `RunSceneController.StartBattle()`
- `RunSceneController.PlayBattleStage()`
- `BattleStubSystem.CreatePreview()`
- `BattleStubSystem.Resolve()`
- `RunFlowController.ResolveBattleOutcome(BattleStubResult result)`

## 8. 胜负规则

### 8.1 任意战斗失败

任意战斗失败立即结束本局：

```text
Battle result victory=false -> GamePhase.GameOver -> GameOver modal -> Restart -> Hero Selection
```

工程入口：

- `RunFlowController.ResolveBattleOutcome()`
- `RunSceneController.ShowGameResultModal("gameover")`

### 8.2 普通地图战斗胜利

普通地图战斗胜利后：

1. 标记节点清除。
2. 开启下一回合夜晚经营。
3. 发放节点奖励。
4. 清理探索战斗上下文。

工程入口：

- `RunFlowController.ResolveExplorationBattleOutcome()`
- `RunFlowController.StartNextNightManageAfterDayNode()`

### 8.3 Boss 战胜利

Boss 节点胜利后：

1. 标记 Boss 节点清除。
2. 检查胜利条件。
3. 进入 `Victory`。
4. 展示通关结算弹窗。

Boss 胜利不进入下一回合经营。

## 9. 下一回合经营

节点完成后进入下一回合经营时，必须执行完整回合开始逻辑。

当前统一入口：

- `RunFlowController.StartNextNightManageAfterDayNode()`

它会调用：

- `NextRound()`
- 回合数 `round + 1`
- 设置新回合金币收入
- 触发回合开始效果
- 应用 pending battle rewards
- 尝试合成
- 刷新商店
- 清空白天行动力
- 回到 `NightManage/manage`

注意：地图奖励金币必须在 `NextRound()` 之后发放，否则会被新回合收入覆盖。

## 10. 结算界面

Victory/GameOver 结算弹窗显示：

- 结局。
- 英雄。
- 回合和天数。
- 生命、金币、商店等级。
- 胜负记录。
- 地图清除节点数和可见节点数。
- 当前节点。
- 上阵、手牌、宝物数量。
- 最后一场战斗战力。

工程入口：

- `RunSceneController.ShowGameResultModal()`
- `RunSceneController.FormatGameResultContent()`

## 11. 已实现约束

- 新局从夜晚经营开始。
- 玩家点击探索时才进入白天地图。
- 白天只选 1 个节点。
- 节点完成后立即进入下一回合夜晚经营。
- 地图战斗不重复触发经营回合结束效果。
- 任意战斗失败直接 GameOver。
- Boss 胜利直接 Victory。

## 12. 后续扩展建议

以下内容未进入当前 MVP：

- 精英节点。
- 随机事件节点。
- 地图商店。
- 完整宝物效果。
- 多地图和章节推进。
- 天气或世界状态。
- 更复杂的英雄主动技能。
- 战斗难度随天数或层数动态缩放。
