# 世界地图 + 昼夜双阶段 · 实装 Todo List

> **创建日期**: 2026-06-05  
> **状态**: 未开始  
> **关联文档**: [WORLD_MAP_SYSTEM_DESIGN.md](./WORLD_MAP_SYSTEM_DESIGN.md)

---

## Phase 0：基础设施 —— 不改变行为，只建立地基

- [ ] **0.1** 在 `Model/RunState.cs` 中新增 `GamePhase` 枚举，保留现有 `state` 字符串做兼容
- [ ] **0.2** `RunState` 新增字段：`dayCount`、`remainingMovePoints`、`maxMovePoints`、`currentNodeId`
- [ ] **0.3** 新建 `Model/WorldMapNodeState.cs`，定义节点运行时数据结构
- [ ] **0.4** 新建 `Model/InventoryItemState.cs`，定义宝物/道具运行时数据结构
- [ ] **0.5** `RunState` 新增列表字段：`List<WorldMapNodeState>`、`List<InventoryItemState>`
- [ ] **0.6** 给 `RunState` 加 `saveVersion` 字段，准备存档迁移逻辑
- [ ] **0.7** `ProphecyGameSession.StartNewRun()` 中初始化新字段默认值

---

## Phase 1：地图数据层 —— 静态配置定义与加载

- [ ] **1.1** 新建 `Data/WorldMapDefinition.cs`，定义地图/层/节点的静态数据结构（`WorldMapConfig`、`MapLayerConfig`、`MapNodeConfig`）
- [ ] **1.2** 新建 `Data/TreasureDefinition.cs`，定义宝物静态数据结构
- [ ] **1.3** 新建 `Data/EnemyPresetDefinition.cs`，定义预设敌人阵容结构
- [ ] **1.4** 创建第一张测试地图 `Resources/Data/world_maps.json`（3 层 × 3-5 节点的小地图）
- [ ] **1.5** 创建测试宝物配置 `Resources/Data/treasures.json`
- [ ] **1.6** `GameDataRepository` 新增 `LoadWorldMaps()`、`LoadTreasures()`、`LoadEnemyPresets()` 方法
- [ ] **1.7** `ProphecyGameSession.StartNewRun()` 增加地图节点展开逻辑（从静态配置生成 Runtime NodeState 列表）
- [ ] **1.8** 扩展 `CampaignDefinition`，增加 `mapId` 字段

---

## Phase 2：世界地图系统 —— 纯逻辑层

- [ ] **2.1** 新建 `Systems/WorldMapSystem.cs`，实现核心接口
- [ ] **2.2** 实现 `GetAvailableDestinations()` —— 从当前节点获取可移动的目标节点列表
- [ ] **2.3** 实现 `CanMoveToNode()` —— 检查移动力是否足够、节点是否可见、是否沿连接前进到下一层
- [ ] **2.4** 实现 `MoveToNode()` —— 消耗移动力，更新 `currentNodeId`，标记节点可见
- [ ] **2.5** 实现 `ResolveNode()` —— 根据节点类型返回 `NodeEventResult`
- [ ] **2.6** 定义 `NodeEventResult` 数据结构 —— 描述节点结算后的事件类型与参数
- [ ] **2.7** 实现 `NodeEventResult` 分发逻辑：区分战斗节点 / 资源节点 / 商店 / 事件 / Boss
- [ ] **2.8** 实现节点可见性逻辑 —— 到达某节点后揭示其下层相邻节点的类型图标
- [ ] **2.9** 实现 `CheckVictoryCondition()` —— Boss 节点被清除后返回胜利

---

## Phase 3：昼夜循环控制器 —— 阶段切换协调

- [ ] **3.1** 新建 `Systems/DayNightCycleController.cs`
- [ ] **3.2** 实现 `CurrentPhase` 属性 —— 根据 `RunState` 字段推断当前阶段
- [ ] **3.3** 实现 `StartNewDay()` —— 恢复移动力、设置阶段为 DayExplore；不在此处增加天数
- [ ] **3.4** 实现 `EnterNight()` —— 保存白天进度、切换到 NightManage 阶段
- [ ] **3.5** 实现 `EndNight()` —— 保存阵容、天数+1、调用 `StartNewDay()`
- [ ] **3.6** 实现 `CanEnterNight()` —— 检查是否允许入夜（战斗中不可入夜）
- [ ] **3.7** 实现阶段切换时的边界逻辑 —— 清除临时 buff、触发天数推进事件

---

## Phase 4：RunFlowController 适配重构

- [ ] **4.1** 将 `NextRound()` 逻辑拆分为两个方法：`EnterNightFlow()` 和 `StartNewDayFlow()`
- [ ] **4.2** `EnterNightFlow()` —— 调用现有的经营阶段初始化（保留现有逻辑，不删除）
- [ ] **4.3** `StartNewDayFlow()` —— 恢复移动力、商店刷新；天数推进只由 `EndNight()` 负责
- [ ] **4.4** `EnterBattlePhase()` 增加重载 —— 支持传入外部敌人预设配置
- [ ] **4.5** `ResolveBattleOutcome()` 增加对 DayExplore 阶段的返回处理 —— 战斗后回到探索而非直接 NextRound
- [ ] **4.6** `FinishBattlePhase()` 增加上下文感知 —— 区分探索战斗 vs 经营战斗的后续流程
- [ ] **4.7** 保留所有现有公开方法签名不变，确保向后兼容

---

## Phase 5：战斗系统适配

- [ ] **5.1** `BattleStubSystem` 新增 `BuildEnemyUnitsFromPreset()` 静态方法
- [ ] **5.2** 定义 `EnemyUnitPreset` 结构 —— 单位ID、数量、星级、Buff 列表
- [ ] **5.3** 实现从节点配置加载预设敌人列表的桥接逻辑
- [ ] **5.4** 普通怪物节点 —— 基于 `dayCount` 而非 `round` 做难度缩放
- [ ] **5.5** Boss 节点 —— 支持为单位添加特殊被动技能（如开场护盾、AOE）
- [ ] **5.6** 战斗奖励适配 —— 金币可直接到账；卡牌/宝物保留统一奖励流程，避免探索奖励和经营奖励分叉

---

## Phase 6：事件系统（可选，视进度决定）

- [ ] **6.1** 新建 `Systems/MapEventResolver.cs`
- [ ] **6.2** 定义事件配置数据结构 —— 事件文本、选项、效果
- [ ] **6.3** 实现事件选项的效果执行 —— 获得金币、失去HP、触发战斗、获得宝物等
- [ ] **6.4** 创建事件配置 `Resources/Data/map_events.json`，内置 5-10 个基础事件
- [ ] **6.5** 事件结果结构 —— 反馈给 UI 的数据

---

## Phase 7：UI 层集成

- [ ] **7.1** 修改 HUD 栏 —— 显示天数、移动力、入夜按钮（RunSceneController.cs）
- [ ] **7.2** 入夜按钮逻辑 —— 移动力归零时高亮闪烁；点击调用 DayNightCycle.EnterNight()
- [ ] **7.3** 新建 `UI/WorldMapView.cs` —— 程序化生成节点 Button + 连接线 Image
- [ ] **7.4** WorldMapView —— 节点颜色区分（已清除/当前/可用/未解锁/已探索未清除）
- [ ] **7.5** WorldMapView —— 英雄头像标记当前位置
- [ ] **7.6** WorldMapView —— 点击可用节点触发移动确认
- [ ] **7.7** 新建 `UI/NodeDetailPopup.cs` —— 移动确认弹窗（节点类型、难度、预估奖励）
- [ ] **7.8** 新建 `UI/EventPopup.cs` —— 随机事件弹窗（文本 + 选项按钮）
- [ ] **7.9** 新建 `UI/InventoryPanel.cs` —— 宝物背包界面
- [ ] **7.10** `RunSceneController` 增加视图切换 —— 地图视图 ↔ 经营视图
- [ ] **7.11** 新建 `UI/VictoryPanel.cs` —— 通关结算界面
- [ ] **7.12** 新建 `UI/GameOverPanel.cs` —— 失败结算界面

---

## Phase 8：存储与存档

- [ ] **8.1** `SaveGameSystem` 支持新 RunState 字段的序列化/反序列化
- [ ] **8.2** 实现存档迁移逻辑 —— 旧存档自动补充默认值
- [ ] **8.3** 存档版本号校验 —— 不兼容版本弹出提示

---

## Phase 9：编辑器工具与调试

- [ ] **9.1** 创建编辑器工具脚本 —— 可视化查看当前地图状态
- [ ] **9.2** 创建调试快捷入口 —— 跳转到任意节点、修改移动力、修改天数
- [ ] **9.3** 创建快速模拟工具 —— 自动运行一局直到 Boss 战

---

## Phase 10：打磨与测试

- [ ] **10.1** 完整跑通一局测试地图（选英雄 → 探索 → 战斗 → 入夜 → 经营 → 第二天 → Boss → 通关）
- [ ] **10.2** 边界条件测试 —— 移动力耗尽、HP 归零、手牌满时获得卡牌
- [ ] **10.3** 存档加载测试 —— 经营阶段/探索阶段/战斗阶段分别存读档
- [ ] **10.4** 旧存档兼容测试 —— 加载一个只有旧字段的 JSON 存档
- [ ] **10.5** 性能检查 —— 大图（20+ 节点）下 UI 生成和刷新性能

---

> **进度统计**: 0 / 52 项完成  
> **当前 Phase**: Phase 0 — 待开始
