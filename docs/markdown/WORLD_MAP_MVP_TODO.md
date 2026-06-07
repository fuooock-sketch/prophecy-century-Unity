# 世界地图 MVP 开发 Todo

> 创建日期：2026-06-05  
> 目标：先做出“3 层世界地图 + 昼夜切换 + 普通战斗 + Boss 通关”的最小可运行闭环。  
> 原则：数据先行、逻辑其次、UI 最后；不破坏现有经营和战斗流程。

---

## 当前状态

- 当前阶段：Phase I
- 总进度：60 / 60
- MVP 范围：3 层测试地图、移动力、节点清除、白天探索、入夜经营、普通战斗、Boss 战、基础存档
- 暂不进入 MVP：同层横移、复杂事件系统、完整宝物背包、复杂英雄主动技能、多地图、天气、周目

---

## Phase A：规则收敛与文档修订

- [x] A1. 明确 MVP 第一版不做同层横向移动，只允许沿连接前进到下一层节点。
- [x] A2. 明确所有节点移动统一消耗 1 点移动力，精英和 Boss 不额外消耗移动力。
- [x] A3. 明确节点清除后不可重复获得奖励，避免无限刷资源。
- [x] A4. 明确天数只在 `EndNight` 时 +1，`StartNewDay` 只恢复移动力并进入白天。
- [x] A5. 明确第一版奖励规则：金币可直接到账，卡牌/宝物奖励保留统一奖励流程。
- [x] A6. 更新 `WORLD_MAP_SYSTEM_DESIGN.md`、`WORLD_MAP_IMPLEMENTATION_TODO.md`、`WORLD_MAP_SUMMARY.md` 中与上述规则冲突的内容。

---

## Phase B：运行时状态基础

- [x] B1. 在 `RunState.cs` 中新增 `GamePhase` 枚举，保留现有 `state` 字符串兼容旧流程。
- [x] B2. 在 `RunState` 中新增 `dayCount`、`remainingMovePoints`、`maxMovePoints`、`currentNodeId`。
- [x] B3. 新建 `WorldMapNodeState`，保存节点 ID、可见状态、已访问状态、已清除状态。
- [x] B4. 新建 `InventoryItemState`，为后续宝物/道具运行时状态预留结构。
- [x] B5. 在 `RunState` 中新增地图节点列表和背包列表。
- [x] B6. 给 `RunState` 增加 `saveVersion`，为旧存档迁移做准备。
- [x] B7. 在 `ProphecyGameSession.StartNewRun()` 中初始化新增字段默认值，确保现有流程行为不变。

---

## Phase C：地图配置与加载

- [x] C1. 新建 `WorldMapDefinition.cs`，定义地图、层、节点、连接和奖励配置。
- [x] C2. 新建 `TreasureDefinition.cs`，先定义最小字段，不急着实现完整宝物效果。
- [x] C3. 新建 `EnemyPresetDefinition.cs`，定义普通战斗和 Boss 战敌人预设。
- [x] C4. 新建 `Assets/Resources/Data/world_maps.json`，包含 3 层测试地图。
- [x] C5. 新建 `Assets/Resources/Data/treasures.json`，包含少量占位宝物。
- [x] C6. 新建 `Assets/Resources/Data/boss_enemies.json` 和必要的普通敌人预设配置。
- [x] C7. 扩展 `CampaignDefinition`，增加 `mapId`。
- [x] C8. 扩展 `GameDataRepository`，加载世界地图、宝物和敌人预设。
- [x] C9. 在开始新局时从 `mapId` 生成运行时节点状态。

---

## Phase D：WorldMapSystem 纯逻辑

- [x] D1. 新建 `WorldMapSystem.cs`，不依赖 UI。
- [x] D2. 实现 `GetAvailableDestinations()`，根据当前节点和连接关系返回可移动节点。
- [x] D3. 实现 `CanMoveToNode()`，检查连接关系、节点可见性和移动力。
- [x] D4. 实现 `MoveToNode()`，消耗移动力、更新当前位置、标记访问状态。
- [x] D5. 实现节点揭示逻辑，到达节点后揭示下一层相邻节点。
- [x] D6. 定义 `NodeEventResult`，表达战斗、资源、Boss、空节点等结果。
- [x] D7. 实现 `ResolveNode()`，根据节点类型返回结果并避免重复结算。
- [x] D8. 实现 `MarkNodeCleared()`，战斗胜利或资源领取后清除节点。
- [x] D9. 实现 `CheckVictoryCondition()`，Boss 节点清除后胜利。
- [x] D10. 增加最小调试验证入口或脚本，确认 3 层地图可移动到 Boss。

---

## Phase E：昼夜流程接入

- [x] E1. 新建 `DayNightCycleController.cs`。
- [x] E2. 实现 `EnterNight()`，从白天探索切到夜晚经营。
- [x] E3. 实现 `EndNight()`，保存阵容、天数 +1、调用新一天初始化。
- [x] E4. 实现 `StartNewDay()`，恢复移动力并切回白天探索，不重复增加天数。
- [x] E5. 改造 `RunFlowController`，让现有经营结束流程可以进入下一天白天。
- [x] E6. 保留现有 `NextRound()` 等公开入口，避免旧 UI 或调试工具失效。
- [x] E7. 验证旧的经营 → 战斗 → 结算流程没有被破坏。

---

## Phase F：战斗节点接入

- [x] F1. 为 `RunFlowController` 增加探索战斗上下文，区分经营战斗和地图战斗。
- [x] F2. 为 `BattleStubSystem` 增加从敌人预设生成敌方阵容的方法。
- [x] F3. 普通怪物节点进入战斗，胜利后清除节点并发放基础奖励。
- [x] F4. Boss 节点进入战斗，胜利后清除节点并触发通关状态。
- [x] F5. 失败后不清除节点，允许后续再次挑战。
- [x] F6. 验证战斗结束后能回到世界地图，而不是直接进入下一轮经营。

---

## Phase G：最小 UI 闭环

- [x] G1. 新建 `WorldMapView.cs`，程序化生成节点按钮和连接线。
- [x] G2. 节点显示当前、可移动、已清除、未解锁状态。
- [x] G3. 点击可移动节点弹出最小确认框或直接移动。
- [x] G4. `RunSceneController` 增加地图视图和经营视图切换。
- [x] G5. HUD 显示天数、移动力、当前位置和入夜按钮。
- [x] G6. 入夜按钮调用 `DayNightCycleController.EnterNight()`。
- [x] G7. 战斗结束后刷新地图 UI。
- [x] G8. Boss 胜利后显示最小通关提示。

---

## Phase H：存档与回归

- [x] H1. `SaveGameSystem` 支持新增 RunState 字段序列化和反序列化。
- [x] H2. 旧存档缺字段时自动补默认值。
- [x] H3. 测试白天探索阶段存读档。
- [x] H4. 测试夜晚经营阶段存读档。
- [x] H5. 测试战斗结束回地图后的存读档。
- [x] H6. 运行数据 QA 和基础战斗一致性检查。
- [x] H7. 完整跑通：选英雄 → 夜晚经营/部署 → 探索触发回合结束效果 → 白天地图 → 普通战斗 → 第二回合经营 → Boss → 通关；战斗失败 → GameOver → 重新选择英雄。

---

## Phase I：MVP 后扩展池

- [ ] I1. 精英节点和更高奖励。
- [ ] I2. 随机事件节点。
- [ ] I3. 宝物效果系统和背包 UI。
- [ ] I4. 地图商店。
- [ ] I5. 英雄探索被动和主动技能。
- [x] I6. 更完整的 Victory / GameOver 结算界面。
- [ ] I7. 多地图和章节推进。

---

## 更新规则

每完成一项工作后：

1. 更新本文件对应任务状态。
2. 更新“当前状态”中的阶段和总进度。
3. 在对话中展示本轮完成项、下一项建议和剩余风险。
