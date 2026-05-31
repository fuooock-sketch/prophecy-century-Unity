# 标题星盘入口设计

## 背景

当前启动入口界面由 `Assets/Scripts/UI/RuntimeUiBootstrap.cs` 在运行时生成，对应节点是 `TitlePanel`。界面包含游戏标题、战役下拉框、英雄下拉框、两个预览文本区、`StartSelectedRunButton` 和 `ChaseTestButton`。目前这个界面使用 `Assets/Art/bg/loading_image.png` 作为背景，整体仍是第一版布局和视觉样式。

## 目标

在不新增位图资产的前提下，把标题/登录入口重做成“神秘预言仪式”风格。界面应该像玩家正在通过星盘开启一段预言：深色仪式空间、天体圆环、符文细节，以及从选择战役和英雄到开始游戏的清晰路径。

## 范围

本设计只覆盖标题/登录入口界面。不改动战斗、商店、棋盘、单位卡、存档/读档行为、战役数据、英雄数据或任何游戏规则。

## 视觉方向

界面使用深墨黑和蓝黑色作为背景主色，搭配克制的金色和冷青白色强调色。主视觉是一套由 Unity UI 基础元素生成的星盘：半透明圆环、径向刻度、小星点、细连接线和少量符文式标签。不再把 `loading_image.png` 用作标题背景。

游戏标题需要成为更强的第一视觉重点：字号更大，颜色偏暖金，并通过叠层文字或阴影制造轻微发光感。整体不做普通奇幻面板，而是让界面像一件用来选择命运的仪式装置。

## 布局

标题界面继续是全屏 Unity Canvas，并沿用现有的 2560x1280 参考分辨率。

星盘位于画面中心附近，作为主要构图锚点。游戏标题放在星盘上方。战役选择和英雄选择放在星盘周围的两个“仪式铭牌”面板中，各自包含标签、下拉框和预览说明，让玩家可以在同一屏内清楚比较当前选择。

`StartSelectedRunButton` 是主操作按钮，需要最醒目。`ChaseTestButton` 继续保留，但视觉上降级为更小、更次要的控制项，避免和主流程竞争注意力。

## 组件

- `TitlePanel`：入口界面的全屏根节点。
- `AstrolabeRoot`：装饰性 UI 组，包含圆环、刻度、星点、连接线和符文标签。
- `TitleText`：更大的仪式感游戏标题。
- `CampaignSelectionPanel`：战役标签、战役下拉框和战役预览文本。
- `HeroSelectionPanel`：英雄标签、英雄下拉框和英雄预览文本。
- `StartSelectedRunButton`：主要的开始游戏操作。
- `ChaseTestButton`：次要的测试入口，视觉权重低于开始按钮。

## 交互

战役和英雄下拉框的行为保持不变。现有的 `RunSceneController.InitializeTitleSelectors()` 和预览刷新逻辑仍然负责选项内容与说明文本。

`StartSelectedRunButton` 继续调用 `StartSelectedRun()`。`ChaseTestButton` 继续调用 `StartSmallMerchantChaseTest()`。本次改动不引入新的游戏状态，也不加入真实账号登录或认证流程。

## 实现说明

优先在运行时生成 UI 的路径中实现，因为当前项目已经在 `RuntimeUiBootstrap.CreateGeneratedUi()` 中构建标题界面。为标题样式新增的辅助方法应尽量留在运行时 UI 生成逻辑附近，除非它们明显能被多个 UI 界面复用。

使用项目中已经存在的 Unity UI 元素：`Image`、`Text`、`Dropdown`、`Button`、`RectTransform`，以及必要时生成的简单 Sprite 或 Texture。布局应使用稳定的锚点和明确尺寸，避免文本、下拉框和按钮在运行时发生位移或挤压。

## 验证

实现后需要确认：

- 进入游戏运行后，标题界面先于正式 run 界面出现。
- 战役和英雄下拉框仍可正常使用。
- 切换战役或英雄后，预览文本仍会更新。
- `StartSelectedRunButton` 能启动所选 run。
- `ChaseTestButton` 仍可调用。
- 在参考分辨率下没有明显文本重叠或控件挤压。
- 如果本地 Unity 环境允许，Unity 编译或项目现有验证命令能够完成。
