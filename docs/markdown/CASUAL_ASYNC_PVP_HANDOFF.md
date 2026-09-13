# 异步 PVP 开发上下文摘要

更新时间：2026-09-12，第五批交付后。用于重启应用或新对话接续。

## 目标与协作方式

用户已授权分阶段完成异步 PVP，每批汇报完成内容与下一步；“继续”表示接着实施。保留工作区原有修改，不擅自回退、提交或推送。当前工作区有大量未提交及未跟踪文件，不能假定全是本轮改动。

项目根目录：`C:\projectZhongxu\prophecy_century\prophecy-century-Unity\prophecy_century`。
完整阶段记录见 [CASUAL_ASYNC_PVP_DEVELOPMENT_STATUS.md](CASUAL_ASYNC_PVP_DEVELOPMENT_STATUS.md)。

## 已确定的玩法

- 独立休闲异步 PVP，不走地图；初始生命 100，匹配同回合历史玩家镜像。
- 先锁定自己的阵容，再查看对手，点击“开始战斗”。
- 断网或空池使用同回合本地/系统保底；三个独立存档槽。
- 第 15 回合存活可结束或进入无尽；生命归零结束。
- 新镜像协议为 `battle_v2`；旧日志标记 `battle_v1`，不混入新协议匹配池。

## 已完成五批

1. 服务端镜像去重、版本隔离、结果归属检查和幂等重试；非法输入及损坏日志处理。
2. 锁阵检查点、读档恢复、战斗副本隔离、结算防重入、第 15 回合选择及无尽恢复。锁阵后禁止经营修改。
3. 镜像保存光环攻击、队伍累计赋予、单位宝石和战斗进度计数；战绩先写入持久化待发送队列，失败重试，收到明确成功才移除。
4. 真实 UnityWebRequest 联调：HTTP 503、丢失确认、延迟响应期间换档、服务地址变化、连接失败保底。旧请求不修改新存档。对手页增加主动开战按钮与金色标识。
5. 服务端按真实单位目录及棋盘配置校验，客户端共用完整占位校验；历史非法镜像保留并标记不可匹配。保底阵容按空位生成，星级上限每三回合提高一档，最高六星。

当前 72 个单位均为 `size = 1`。占位规则看 `size`，不看 `sizeTier`；支持的两格规则用测试专用数据覆盖，没有修改正式单位体型。

## 最近验证结果

- Python 服务端 16 项通过。
- Unity 运行时代码编译、镜像属性、存档恢复、按钮组件、真实 HTTP 联调通过。
- 保底合法性及星级上限覆盖第 1–200 回合与第 100000 回合；不代表后期平衡和战斗耗时已验收。
- 最新日志：`tools/obj/PvpRecoveryValidation/test-b855164d59c44b53a867e39e1637a9fd.log`。
- `git diff --check` 通过，仅原有换行符提示。

验证命令（在项目根目录运行）：

```powershell
python -m unittest discover -s tools/casual_pvp_server -p test_server.py
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate_casual_pvp.ps1
```

Unity 验证脚本使用 `tools/obj/PvpRecoveryValidation` 隔离项目及临时本机服务，不占用主项目。此前沙箱内 Unity 曾停滞，成功运行使用了工具权限提升。不要把隔离项目改放 Unity 的 `Temp` 目录，主编辑器退出会清理它。

## 当前阻碍与用户下一步

电脑控制连接在初始化阶段返回：

```text
codex/sandbox-state-meta: missing field sandboxPolicy
```

这不是自动审批拒绝，也没有证据说明是 Unity 项目错误。图形操作尚未开始；完整画面、整局试玩、多分辨率验收未完成。用户已被建议完全退出并重新打开 Codex，再在本对话发送“重试电脑控制”；重启仅是排查，不能保证修复。Unity 可以保持打开。

恢复后使用 computer-use 技能规定的连接流程重试，不用其他键鼠自动化绕过。若仍报错，记录错误并准备反馈，同时继续不依赖图形操作的开发。

## 下一批优先级

1. 重试电脑控制，完成对手展示、锁阵查看、结算、第 15 回合及无尽的画面验收。
2. 统一保底战力估算：目前保底 `powerScore` 仍是简单属性求和，未与玩家引擎估算一致。验证后期数值、强度和耗时。
3. 增加按版本/回合的池量、重复率、保底率统计。
4. 正式服务重启、长期数据保留、备份与远端部署准备，之后实际多人试玩。

服务端仍是本地开发服务；未完成身份认证、请求限流、服务端权威战斗或远端验收。目录校验不等于证明客户端成长数值来源合法。

## 主要代码入口

- `Assets/Scripts/Systems/CasualPvpSystem.cs`：镜像、锁阵、保底、队列状态、占位校验。
- `Assets/Scripts/Systems/CasualPvpNetworkClient.cs`：上传、匹配、失败回退、持久化队列重试及取消代次。
- `Assets/Scripts/Systems/RunFlowController.cs`、`BattleStubSystem.cs`：阶段推进、战斗副本与镜像还原。
- `Assets/Scripts/UI/RunSceneController.cs`、`CasualPvpUiController.cs`：读档恢复、战斗播放与对手确认。
- `Assets/Scripts/Model/RunState.cs`、`CasualPvpModels.cs`：持久化数据。
- `Assets/Editor/CasualPvpSelfTest.cs`、`CasualPvpNetworkSelfTest.cs`：Unity 回归与真实 HTTP 测试。
- `tools/casual_pvp_server/server.py`、`test_server.py`、`integration_fixture.py`：正式本地服务、测试与故障注入。

## 相关文档

- [阶段进度与验收记录](CASUAL_ASYNC_PVP_DEVELOPMENT_STATUS.md)
- [玩法与数据设计](CASUAL_ASYNC_PVP_MODE_DESIGN.md)
- [界面流程设计](CASUAL_ASYNC_PVP_UI_FLOW_DESIGN.md)
- [本地服务器使用说明](../../tools/casual_pvp_server/README.md)

接续提示词：读取 `docs/markdown/CASUAL_ASYNC_PVP_HANDOFF.md`，先重试电脑控制，再按下一批优先级继续异步 PVP，分阶段汇报。
