# 表格数据实装状态

更新时间：2026-06-06

## 已落地内容

本地 Excel 已从飞书表格重建到 `docs/excel`：

- `地图配置_地图定义表.xlsx`
- `地图配置_层级表.xlsx`
- `地图配置_节点表.xlsx`
- `地图配置_连接表.xlsx`
- `怪物配置_敌人预设表.xlsx`
- `怪物配置_敌方单位明细表.xlsx`
- `单局流程_阶段状态配置表.xlsx`
- `单局流程_触发时机配置表.xlsx`

生成脚本：

- `tools/generate_config_json.py`

运行时 JSON：

- `Assets/Resources/Data/world_maps.json`
- `Assets/Resources/Data/boss_enemies.json`
- `Assets/Resources/Data/run_flow_config.json`

QA 脚本：

- `tools/qa_world_map_configs.py`
- `tools/qa_run_flow_configs.py`
- `tools/validate_world_map_mvp.ps1`

Unity 侧已接入：

- `GameDataRepository` 加载地图、宝物、敌人预设、运行流程配置。
- `ProphecyGameSession` 新局初始化世界地图节点状态。
- `RunFlowController` 支持夜晚经营、白天探索、地图节点移动、地图战斗、节点奖励、Boss 胜利。
- `BattleStubSystem` 地图战斗优先使用表格敌人预设。
- `RunSceneController` 接入白天探索入口、世界地图视图、地图节点点击、战斗结果回流。
- `WorldMapView` 显示地图节点、连接线、当前位置、移动力和入夜按钮。

## 当前验证结果

已通过：

```powershell
python tools\generate_config_json.py
python tools\qa_world_map_configs.py
python tools\qa_run_flow_configs.py
powershell -ExecutionPolicy Bypass -File tools\validate_world_map_mvp.ps1 -MapId abyss_wilds -MaxMovePoints 20
```

验证覆盖：

- 1 张地图、48 个节点、108 条连接。
- 8 个敌人预设、31 条敌方单位明细。
- 6 个运行阶段、9 个触发时机。
- 起点到 Boss 可达，最短路径 15 步。
- 第一天从起点可到达 2 个节点。
- 非战斗节点会回到 `NightManage/manage`。
- 战斗节点会进入 `Battle/battle`。

## 未完成或需要编辑器确认

- Unity batchmode 这次启动后没有产生日志，已手动结束进程；尚未完成编辑器内脚本编译检查。
- 需要在 Unity Editor 内手动或自动验证完整交互：
  - 开新局。
  - 点击探索进入白天地图。
  - 点击资源节点并返回夜晚经营。
  - 点击战斗节点并进入战斗。
  - 战斗胜利后节点清除并回到经营。
  - Boss 胜利后进入胜利结算。

## 后续实装步骤

1. 表格同步：飞书表格变更后，重建 `docs/excel`。
2. 数据生成：运行 `python tools\generate_config_json.py` 生成 Unity 运行时 JSON。
3. 数据 QA：运行地图 QA、流程 QA 和 Boss 路径校验。
4. Unity 导入：打开 Unity Editor，确认新增 JSON、C#、meta 文件均正常导入。
5. 运行 smoke：按“开新局 -> 探索 -> 节点 -> 战斗/奖励 -> 回夜晚 -> Boss”走一遍。
6. 细化配置：把当前写死的奖励、移动力、节点特殊事件继续拆成表格字段。
7. 回归测试：每次表格或流程改动后复跑生成脚本和 QA 脚本。

## 操作建议

如果 Unity Editor 或 batchmode 长时间无响应，先结束对应 Unity 进程，不要等待工具无限挂起。当前项目层面的可复跑检查入口以脚本 QA 为准，编辑器编译结果需要下次在 Unity 内确认。
