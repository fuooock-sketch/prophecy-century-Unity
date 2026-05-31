# Unit Skill Migration Heatmap

Source: `unit_202605250127_数量修订版02_攻防血压缩版.xlsx`

术语约定：玩家可见文本统一使用“获得数量”。

## Legend

| 状态 | 含义 |
| --- | --- |
| ⬜ | 未开始 |
| 🟨 | 文本/数据已导入 |
| 🟧 | 逻辑部分接入，未全量验收 |
| 🟩 | 已验证 |
| 🟥 | 阻塞或需要设计确认 |

## Snapshot

- Excel units: 71
- Visible runtime units: 59
- Units with executable skill arrays: 59
- Mechanism families detected: 17

## Mechanism Heatmap

| 机制 | 涉及单位数 | 文本 | 逻辑 | 测试 |
| --- | ---: | --- | --- | --- |
| 入场触发 | 20 | 🟨 | 🟧 | ⬜ |
| 回合开始 | 8 | 🟨 | 🟧 | ⬜ |
| 回合结束 | 12 | 🟨 | 🟧 | ⬜ |
| 获得数量 | 56 | 🟨 | 🟧 | 🟩 |
| 密林宝钻 | 19 | 🟨 | 🟧 | ⬜ |
| 商店/手牌/发现 | 14 | 🟨 | 🟧 | ⬜ |
| 吞噬/复制 | 6 | 🟨 | 🟧 | ⬜ |
| 进阶/变身 | 8 | 🟨 | ⬜ | ⬜ |
| 开战触发 | 14 | 🟨 | 🟧 | ⬜ |
| 攻击触发 | 19 | 🟨 | 🟧 | ⬜ |
| 受伤触发 | 1 | 🟨 | ⬜ | ⬜ |
| 死亡触发 | 8 | 🟨 | ⬜ | ⬜ |
| 召唤 | 7 | 🟨 | ⬜ | ⬜ |
| 护盾 | 3 | 🟨 | 🟧 | ⬜ |
| 控制/位移 | 2 | 🟨 | ⬜ | ⬜ |
| 范围伤害 | 5 | 🟨 | 🟧 | ⬜ |
| 士气/运气/暴击 | 6 | 🟨 | 🟩 | ⬜ |

## Unit Heatmap

| 单位 | 机制标签 | 新字段 | 可执行技能 | 逻辑 | 测试 |
| --- | --- | --- | --- | --- | --- |
| 小商人 | 回合开始、商店/手牌/发现、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 光明武士 | 回合结束、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 精灵 | 入场触发、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 铁匠 | 获得数量、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 僧侣 | 获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 骑士 | 回合开始、获得数量、开战触发、控制/位移、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 刺客 | 回合结束、获得数量、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 牧师 | 入场触发、开战触发、护盾 | 🟩 | 🟩 | 🟧 | ⬜ |
| 流浪者 | 获得数量、商店/手牌/发现 | 🟩 | 🟩 | 🟧 | ⬜ |
| 武学大师 | 获得数量、商店/手牌/发现、攻击触发、范围伤害 | 🟩 | 🟩 | 🟧 | ⬜ |
| 光明导师 | 获得数量、开战触发、攻击触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 卫戍协兵 | 获得数量、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 莱特使者 | 回合开始、商店/手牌/发现 | 🟩 | 🟩 | 🟧 | ⬜ |
| 皇家剑士 | 回合结束、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 莱特的回响 | 获得数量、开战触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 冰霜魔灵 | 获得数量、商店/手牌/发现 | 🟩 | 🟩 | 🟧 | ⬜ |
| 犟嘴学徒 | 入场触发、获得数量、受伤触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 火元素 | 获得数量、攻击触发、死亡触发、范围伤害 | 🟩 | 🟩 | 🟧 | ⬜ |
| 低级元素使 | 入场触发、获得数量、开战触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 水元素 | 攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 无魔者 | 获得数量、开战触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 飞毯法师 | 入场触发、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 傀儡魔灵 | 获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 风元素 | 开战触发、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 土元素 | 获得数量、开战触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 学院园丁 | 获得数量、死亡触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 魔导师 | 获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 元素大师 | 获得数量、攻击触发、死亡触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 魔尊 | 获得数量、护盾 | 🟩 | 🟩 | 🟧 | ⬜ |
| 大魔灵 | 获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 魔法龙 | 获得数量、开战触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 林地卫兵 | 入场触发、密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 游骑兵 | 回合结束、获得数量、密林宝钻、进阶/变身 | 🟩 | 🟩 | 🟧 | ⬜ |
| 精锐游骑兵 | 回合结束、获得数量、开战触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 林地密探 | 密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 游侠 | 入场触发、获得数量、密林宝钻、进阶/变身 | 🟩 | 🟩 | 🟧 | ⬜ |
| 神剑游侠 | 回合开始、获得数量、密林宝钻、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 机警后援 | 回合结束、密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 河边队长 | 入场触发、密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 高翎守望者 | 密林宝钻、攻击触发、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 弓箭手 | 获得数量、密林宝钻、进阶/变身 | 🟩 | 🟩 | 🟧 | ⬜ |
| 幻影射手 | 获得数量、密林宝钻、攻击触发、士气/运气/暴击 | 🟩 | 🟩 | 🟧 | ⬜ |
| 掘地鼠 | 密林宝钻、进阶/变身、开战触发、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 佣兵队长 | 获得数量、密林宝钻、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 羽卫 | 回合开始、回合结束、获得数量、密林宝钻、攻击触发、死亡触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 林地将军 | 获得数量、密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 猎豹 | 回合开始、获得数量、密林宝钻、死亡触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 双塔术士 | 回合结束、获得数量、密林宝钻、攻击触发、范围伤害 | 🟩 | 🟩 | 🟧 | ⬜ |
| 席林迪翁 | 获得数量、密林宝钻 | 🟩 | 🟩 | 🟧 | ⬜ |
| 淤魔 | 回合结束、密林宝钻、进阶/变身 | 🟩 | 🟩 | 🟧 | ⬜ |
| 血淤魔 | 获得数量、密林宝钻、商店/手牌/发现、开战触发、攻击触发、控制/位移 | 🟩 | 🟩 | 🟧 | ⬜ |
| 格尔兽 | 入场触发、获得数量、商店/手牌/发现、吞噬/复制 | 🟩 | 🟩 | 🟧 | ⬜ |
| 格尔步兵 | 入场触发、商店/手牌/发现 | 🟩 | 🟩 | 🟧 | ⬜ |
| 鱼人奴仆 | 入场触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 苦工 | 回合结束、获得数量、商店/手牌/发现、进阶/变身 | 🟩 | 🟩 | 🟧 | ⬜ |
| 叫唤者 | 入场触发、获得数量、商店/手牌/发现 | 🟩 | 🟩 | 🟧 | ⬜ |
| 打手 | 入场触发、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 蘑菇夸库 | 回合结束、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 雪狮 | 入场触发、获得数量、攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 驯兽师 | 入场触发、获得数量、进阶/变身、开战触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 劣徒 | 入场触发、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 兽骑兵 | 入场触发、获得数量、死亡触发、召唤 | 🟩 | 🟩 | 🟧 | ⬜ |
| 驱魔师坐骑 | 回合结束、获得数量、商店/手牌/发现、吞噬/复制、攻击触发、召唤、范围伤害 | 🟩 | 🟩 | 🟧 | ⬜ |
| 痛苦火苗 | 回合开始、获得数量、死亡触发、范围伤害 | 🟩 | 🟩 | 🟧 | ⬜ |
| 阴暗屠夫 | 入场触发、获得数量、商店/手牌/发现、吞噬/复制、攻击触发、护盾 | 🟩 | 🟩 | 🟧 | ⬜ |
| 苦嚎叫兽 | 入场触发、获得数量、商店/手牌/发现、吞噬/复制 | 🟩 | 🟩 | 🟧 | ⬜ |
| 酒鬼 | 回合开始、获得数量、商店/手牌/发现、死亡触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 格尔军官 | 入场触发、获得数量 | 🟩 | 🟩 | 🟧 | ⬜ |
| 格尔巨兽 | 获得数量、吞噬/复制 | 🟩 | 🟩 | 🟧 | ⬜ |
| 邪恶女巫 | 获得数量、吞噬/复制、进阶/变身、开战触发 | 🟩 | 🟩 | 🟧 | ⬜ |
| 巫兽师 | 攻击触发 | 🟩 | 🟩 | 🟧 | ⬜ |

## Risk Heatmap

| 风险项 | 状态 | 处理方式 |
| --- | --- | --- |
| 旧数量成长术语 | 🟩 | 必须统一改成“获得数量”。 |
| 百分比获得数量文本 | 🟩 | 发现后需转成固定公式或设计确认。 |
| 默认数量除法公式 | 🟧 | 属于固定公式，已由导入脚本同步 ratio 参数，仍需专项验收。 |
| 旧 power 技能数组 | 🟥 | power 可兼容保留，但不能驱动新版伤害。 |
| 技能数组丢失 | 🟩 | 经营效果依赖可执行技能数组。 |
| Unity batchmode 验证 | 🟥 | 当前本机 batchmode 启动曾卡住，需要手动确认 Unity 无弹窗。 |

## Fixed Count Acceptance

| Check | Status | Detail |
| --- | --- | --- |
| No visible count cap | 🟩 | `maxCount` must stay zero/unused for visible runtime units. |
| No percentage quantity text | 🟩 | Visible unit data must not contain percentage-based quantity gain. |
| GainCount writes permanent count | 🟩 | `GainCount`/`ReinforceUnit` must increase `baseCount` and clear compatibility cap. |
| GainCount dispatches count event | 🟩 | Dependent effects must listen to `on_gain_count`. |
| Shop cards start with default count | 🟩 | Shop-generated cards must receive default quantity. |
| Board deploy fills missing count | 🟩 | Legacy hand cards must not enter battle with zero quantity. |
| Reward/discovery cards start with default count | 🟩 | Reward and discovery cards must receive default quantity. |
| Forest gem count rule | 🟩 | Forest gem must count +1 and permanent quantity +1 per gem. |
| Shop quantity feedback uses count | 🟩 | Shop-card quantity growth must use count feedback and card quantity. |
| Bright Warrior fixed values | 🟩 | Round-end adjacent faith gain should be fixed +10/+20 quantity. |
| Elf fixed values | 🟩 | Entry-trigger self gain should be fixed +10/+20 quantity. |
| Fire Elemental count chain | 🟩 | Legacy skill kind should now route through `on_gain_count` with fixed +4/+8. |
| Earth Elemental fixed temporary count | 🟩 | Temporary battle count gain should be fixed +3/+6. |
| Ger Beast default divisor formula | 🟩 | Default-count divisor formula should import to ratio 0.1/0.2. |
| Magic Dragon fixed summon count | 🟩 | Summoned Fire Elemental count should be fixed 22/26. |

## Next Mechanism Sprint

目标机制：固定获得数量。

验收点：

- 经营阶段单位获得数量时，永久数量 `baseCount` 增加固定值。
- 战斗阶段临时获得数量时，`currentCount` 与 `currentTotalHp` 同步增加。
- 密林宝钻赐予时，密林宝钻计数 +1，永久获得数量 +1。
- 玩家可见文本统一使用“获得数量”。
- 不出现按百分比获得数量。
