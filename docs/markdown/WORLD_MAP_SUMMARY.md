# 预言世纪 · 世界地图系统重构 总结文档

> **日期**: 2026-06-05  
> **配套文档**:
> - 策划文档: [WORLD_MAP_SYSTEM_DESIGN.md](./WORLD_MAP_SYSTEM_DESIGN.md)
> - 实装清单: [WORLD_MAP_IMPLEMENTATION_TODO.md](./WORLD_MAP_IMPLEMENTATION_TODO.md)
> - MVP 开发清单: [WORLD_MAP_MVP_TODO.md](./WORLD_MAP_MVP_TODO.md)

---

## 一、改什么？

**现状**：线性回合制——经营 → 战斗 → 结算 → 下一轮经营，循环直到回合上限。

**目标**：

```
开局选英雄 → 白天·世界地图探索（消耗移动力）⇄ 入夜·营地经营（现有系统）
                                                    ↓
                                            阵容保存 → 下一天白天
                                                    ↓
                                            最终击败 Boss 通关
```

**一句话**：在现有经营自走棋之上，加一层 Slay the Spire 式的分支路径世界地图作为资源获取渠道。

---

## 二、核心设计

### 2.1 世界地图（Slay the Spire 分支路径风格）

```
         [起点]
        /  |  \
     [怪] [资源] [怪]     ← 每层 2-4 个节点，玩家选择推进
      |    |    / \
     [精] [店] [怪] [事]
       \   |   |   /
          [Boss]           ← 最终 Boss
```

| 节点类型 | 说明 |
|----------|------|
| 起点 | 开局位置 |
| 普通怪物 | 自动战斗，奖励金币+卡牌选择 |
| 精英怪物 | 更难战斗，奖励金币×2+宝物 |
| 资源点 | 直接获得金币/宝物 |
| 商店 | 金币购买卡牌/宝物 |
| 随机事件 | 触发事件，有选择分支 |
| Boss | 击败即通关 |

### 2.2 昼夜循环

| 阶段 | 操作 | 说明 |
|------|------|------|
| **白天探索** | 移动节点、触发战斗/事件 | 消耗移动力（每日 4 点） |
| **入夜经营** | 商店买卡、合成、部署 | 完全复用现有经营系统 |
| **新一天** | 移动力恢复、天数+1 | 自动切换回白天 |

移动力归零 → 只能入夜；也可提前入夜放弃剩余移动力。

### 2.3 资源流转

```
白天探索获得 → 金币/卡牌/宝物 → 入夜经营使用 → 提升阵容强度 → 挑战更难节点
```

---

## 三、工程技术路线

### 3.1 三大原则

1. **数据先行，逻辑次之，UI 最后**——最安全的开发顺序
2. **不破坏现有系统**——新建文件、增量修改、保留旧接口
3. **显式状态机**——用枚举替代字符串 `state` 做阶段切换

### 3.2 MVP Phase 概览

先做“3 层地图 + 昼夜切换 + 普通战斗 + Boss 通关”的最小可运行闭环，再扩展精英、事件、宝物和多地图。

| Phase | 内容 | 估时 | 关键产出 |
|:-----:|------|:----:|------|
| **A** | 规则收敛与文档修订 | 0.5天 | 明确 MVP 规则，消除实现冲突 |
| **B** | 运行时状态基础 | 1天 | RunState 扩展、节点状态、背包状态 |
| **C** | 地图配置与加载 | 1天 | 3 层测试地图、配置结构、加载逻辑 |
| **D** | WorldMapSystem 纯逻辑 | 1-1.5天 | 移动、揭示、节点结算、胜利判定 |
| **E** | 昼夜流程接入 | 1-1.5天 | 白天探索与夜晚经营切换 |
| **F** | 战斗节点接入 | 1天 | 普通战斗、Boss 战、战斗后回地图 |
| **G** | 最小 UI 闭环 | 2-3天 | 地图视图、HUD、入夜按钮、通关提示 |
| **H** | 存档与回归 | 1天 | 存档兼容、完整 MVP 跑通 |

**MVP 预计约 9-11 个工作日**。事件系统、精英节点、宝物背包、地图商店、英雄探索技能放入 MVP 后扩展池。

---

## 四、涉及的核心文件

### 新建

| 文件 | 说明 |
|------|------|
| `Model/WorldMapNodeState.cs` | 节点运行时状态 |
| `Model/InventoryItemState.cs` | 宝物运行时状态 |
| `Data/WorldMapDefinition.cs` | 地图静态配置结构 |
| `Data/TreasureDefinition.cs` | 宝物静态定义 |
| `Data/EnemyPresetDefinition.cs` | 敌人预设定义 |
| `Systems/WorldMapSystem.cs` | 地图移动/结算逻辑 |
| `Systems/DayNightCycleController.cs` | 昼夜切换协调 |
| `Systems/MapEventResolver.cs` | 随机事件处理（可选） |
| `UI/WorldMapView.cs` | 地图 UI 视图 |
| `UI/NodeDetailPopup.cs` | 节点详情弹窗 |
| `UI/EventPopup.cs` | 事件弹窗 |
| `UI/InventoryPanel.cs` | 宝物背包 |
| `UI/VictoryPanel.cs` | 通关结算 |
| `UI/GameOverPanel.cs` | 失败结算 |
| `Resources/Data/world_maps.json` | 地图数据 |
| `Resources/Data/treasures.json` | 宝物数据 |
| `Resources/Data/elite_enemies.json` | 精英敌人配置 |
| `Resources/Data/boss_enemies.json` | Boss 配置 |
| `Resources/Data/map_events.json` | 事件配置（可选） |

### 修改

| 文件 | 改动范围 |
|------|---------|
| `Model/RunState.cs` | 新增 GamePhase 枚举 + 5 个字段 + 2 个列表 |
| `Data/CampaignDefinition.cs` | 增加 mapId |
| `Data/GameDataRepository.cs` | 加载新配置文件 |
| `Core/ProphecyGameSession.cs` | StartNewRun 初始化地图 |
| `Systems/RunFlowController.cs` | 拆分 NextRound，增加 DayExplore 分支 |
| `Systems/BattleStubSystem.cs` | 新增 BuildEnemyUnitsFromPreset 重载 |
| `Systems/SaveGameSystem.cs` | 新字段序列化 + 存档迁移 |
| `UI/RunSceneController.cs` | HUD 扩展 + 视图切换 |

---

## 五、立即可开始的第一步

**Phase B 第一个任务**：在 `Model/RunState.cs` 中新增 `GamePhase` 枚举，不改任何现有逻辑。

```csharp
public enum GamePhase
{
    DayExplore,   // 白天探索（新增）
    NightManage,  // 夜间经营（原 manage）
    Battle,       // 自动战斗（原 battle）
    Settle,       // 战斗结算（原 settle）
    Victory,      // 通关
    GameOver      // 失败
}
```

---

> **下一步**: 开始 MVP Phase B 运行时状态基础。  
> **MVP 清单**: [WORLD_MAP_MVP_TODO.md](./WORLD_MAP_MVP_TODO.md)  
> **完整清单**: [WORLD_MAP_IMPLEMENTATION_TODO.md](./WORLD_MAP_IMPLEMENTATION_TODO.md)  
> **详细设计**: [WORLD_MAP_SYSTEM_DESIGN.md](./WORLD_MAP_SYSTEM_DESIGN.md)
