# -*- coding: utf-8 -*-
"""将关卡设计文档逐章上传到飞书云文档"""
import subprocess
import json
import sys

DOC_ID = "S58XdBAq4okpGTxiRiicopIZnWe"

def run_lark(cmd_args):
    """运行 lark-cli 命令"""
    full_cmd = ["lark-cli"] + cmd_args
    print(f"  执行: lark-cli {' '.join(cmd_args[:6])}...")
    result = subprocess.run(full_cmd, capture_output=True, text=True, encoding='utf-8')
    if result.returncode != 0:
        print(f"  ⚠ 返回码: {result.returncode}")
        if result.stderr:
            print(f"  stderr: {result.stderr[:300]}")
    return result.stdout

def append_section(content_xml):
    """向文档末尾追加内容"""
    result = run_lark([
        "docs", "+update", "--api-version", "v2", "--as", "user",
        "--doc", DOC_ID, "--command", "append",
        "--content", content_xml
    ])
    return result

# 第一章：胜利与失败条件
print("Upload 1/6: 胜利与失败条件...")
append_section("""<h1>一、游戏胜利与失败条件</h1>
<callout emoji="✅" background-color="light-green" border-color="green"><p><b>胜利条件：</b>玩家抵达第15层Boss节点「深渊王座」并在战斗中获胜 → 直接触发 Victory。</p></callout>
<callout emoji="❌" background-color="light-red" border-color="red"><p><b>失败条件：</b>任意一场战斗失败 → 立即 GameOver（当前没有复活/续局机制）。</p></callout>
<table><colgroup><col width="200"/><col width="200"/><col width="250"/></colgroup>
<thead><tr><th background-color="light-gray">场景</th><th background-color="light-gray">结果</th><th background-color="light-gray">流程</th></tr></thead>
<tbody><tr><td>击败Boss</td><td><span text-color="green">Victory</span></td><td>通关结算，Boss奖励发放</td></tr>
<tr><td>任意战斗失败</td><td><span text-color="red">GameOver</span></td><td>立即结束，不进入下一回合</td></tr>
<tr><td>非Boss节点完成</td><td>进入经营回合</td><td>发放节点奖励→NextRound→经营阶段</td></tr>
<tr><td>资源/宝物节点</td><td>进入经营回合</td><td>立即发放→无战斗→经营阶段</td></tr></tbody></table>
<hr/>""")
print("  ✓ done")

# 第二章：关卡结构总览
print("Upload 2/6: 关卡结构总览...")
append_section("""<h1>二、关卡结构总览</h1>
<h2>2.1 地图规模</h2>
<table><colgroup><col width="150"/><col width="300"/></colgroup>
<thead><tr><th background-color="light-gray">维度</th><th background-color="light-gray">数值</th></tr></thead>
<tbody>
<tr><td>地图ID</td><td><code>abyss_wilds</code></td></tr>
<tr><td>地图名称</td><td>恶兆荒野</td></tr>
<tr><td>总层数</td><td>16层（0起点+14探索+1Boss）</td></tr>
<tr><td>总节点数</td><td>48个</td></tr>
<tr><td>连接数</td><td>108条</td></tr>
<tr><td>经营回合</td><td>14次（每节点→1次夜晚经营）</td></tr>
<tr><td>敌人预设</td><td>8种（7普通+1Boss）</td></tr>
<tr><td>宝物种类</td><td>5种</td></tr>
</tbody></table>
<h2>2.2 漏斗型分支结构</h2>
<table><colgroup><col width="60"/><col width="60"/><col width="90"/><col width="70"/><col width="200"/></colgroup>
<thead><tr><th background-color="light-gray">层</th><th background-color="light-gray">节点数</th><th background-color="light-gray">名称</th><th background-color="light-gray">阶段</th><th background-color="light-gray">设计意图</th></tr></thead>
<tbody>
<tr><td>0</td><td>1</td><td>营地篝火</td><td>起点</td><td>冒险开始</td></tr>
<tr><td>1</td><td>2</td><td>荒野边缘</td><td>教学</td><td>1战+1资源，引导熟悉</td></tr>
<tr><td>2</td><td>2</td><td>失路小径</td><td>教学</td><td>1战+1宝物，引入宝物概念</td></tr>
<tr><td>3</td><td>3</td><td>影棘丛林</td><td>展开</td><td>2战+1资源，增加选择</td></tr>
<tr><td>4</td><td>3</td><td>废墟岔口</td><td>展开</td><td>1战+1资源+1宝物</td></tr>
<tr><td>5</td><td>4</td><td>腐化原野</td><td>分支</td><td>2战+1资源+1宝物，路线分化</td></tr>
<tr><td>6</td><td>4</td><td>低语谷</td><td>分支</td><td>2战+1资源+1宝物</td></tr>
<tr><td>7</td><td>5</td><td>灰烬高地</td><td>最宽</td><td>2战+2资源+1宝物，最大选择</td></tr>
<tr><td>8</td><td>5</td><td>暮光盆地</td><td>最宽</td><td>2战+2资源+1宝物，最大决策</td></tr>
<tr><td>9</td><td>4</td><td>赤脊山</td><td>收束</td><td>2战+1资源+1宝物</td></tr>
<tr><td>10</td><td>4</td><td>破碎隘口</td><td>收束</td><td>2战+1资源+1宝物</td></tr>
<tr><td>11</td><td>3</td><td>遗忘堡垒</td><td>高压</td><td>2战+1宝物，强制战斗</td></tr>
<tr><td>12</td><td>3</td><td>哀嚎深渊</td><td>高压</td><td>2战+1宝物，终局检验</td></tr>
<tr><td>13</td><td>2</td><td>黑曜石门</td><td>逼近</td><td>1战+1宝物，最后补给</td></tr>
<tr><td>14</td><td>2</td><td>深渊近途</td><td>逼近</td><td>1战+1宝物，Boss前调整</td></tr>
<tr><td>15</td><td>1</td><td>绝望王座</td><td>终局</td><td>Boss战，胜利通关</td></tr>
</tbody></table>
<callout emoji="💡" background-color="light-yellow" border-color="yellow"><p><b>设计理念：</b>前窄（L1-2）→中宽（L5-8）→后窄（L13-14），仿杀戮尖塔的漏斗型节奏。L13-14无资源节点，只有battle+treasure，强制玩家靠已有经济做最终调整。</p></callout>
<hr/>""")
print("  ✓ done")

# 第三章：节点分布
print("Upload 3/6: 节点分布与路线设计...")
append_section("""<h1>三、节点分布与路线设计</h1>
<h2>3.1 节点类型统计</h2>
<table><colgroup><col width="120"/><col width="80"/><col width="250"/></colgroup>
<thead><tr><th background-color="light-gray">类型</th><th background-color="light-gray">数量</th><th background-color="light-gray">说明</th></tr></thead>
<tbody>
<tr><td><span text-color="blue">start</span></td><td>1</td><td>起点「营地篝火」Layer 0</td></tr>
<tr><td><span text-color="orange">battle</span></td><td>26</td><td>普通战斗，覆盖L1-14每层</td></tr>
<tr><td><span text-color="green">resource</span></td><td>13</td><td>资源节点，纯金币收益</td></tr>
<tr><td><span text-color="purple">treasure</span></td><td>9</td><td>宝物节点，全局被动道具</td></tr>
<tr><td><span text-color="red">boss</span></td><td>1</td><td>Boss「深渊王座」Layer 15</td></tr>
</tbody></table>
<h2>3.2 三条典型路线</h2>
<table><colgroup><col width="100"/><col width="80"/><col width="70"/><col width="70"/><col width="200"/></colgroup>
<thead><tr><th background-color="light-gray">路线</th><th background-color="light-gray">战斗</th><th background-color="light-gray">金币</th><th background-color="light-gray">宝物</th><th background-color="light-gray">体验</th></tr></thead>
<tbody>
<tr><td><b>全战斗</b></td><td>14</td><td>~75金</td><td>0</td><td>硬核，阵容检验充分</td></tr>
<tr><td><b>均衡</b></td><td>8-10</td><td>~60金</td><td>4-5</td><td>推荐，攻守兼备</td></tr>
<tr><td><b>纯资源</b></td><td>1(Boss)</td><td>~35金</td><td>5</td><td>高隐含风险：金币少→后期阵容弱</td></tr>
</tbody></table>
<callout emoji="⚠️" background-color="light-yellow" border-color="orange"><p><b>软强制设计：</b>纯资源路线看似安全，但战斗节点奖励高于资源节点，导致后期金币不足、阵容薄弱。鼓励玩家在路线选择上有策略深度而非简单避战。</p></callout>
<hr/>""")
print("  ✓ done")

# 第四章：怪物设计
print("Upload 4/6: 怪物分步设计...")
append_section("""<h1>四、怪物分步设计</h1>
<h2>4.1 敌人预设总览（8种）</h2>
<table><colgroup><col width="130"/><col width="130"/><col width="60"/><col width="60"/><col width="60"/><col width="150"/></colgroup>
<thead><tr><th background-color="light-gray">预设ID</th><th background-color="light-gray">名称</th><th background-color="light-gray">类型</th><th background-color="light-gray">适用层</th><th background-color="light-gray">单位</th><th background-color="light-gray">设计意图</th></tr></thead>
<tbody>
<tr><td>wild_bandits</td><td>荒野劫匪</td><td>normal</td><td>1-2</td><td>3</td><td>教学级，全★1基础单位</td></tr>
<tr><td>ruin_sentry</td><td>废墟哨卫</td><td>normal</td><td>2-4</td><td>4</td><td>引入治疗+后排，★1-2混合</td></tr>
<tr><td>shadow_raiders</td><td>暗影掠袭者</td><td>normal</td><td>4-6</td><td>4</td><td>刺客暴击+光明导师召唤</td></tr>
<tr><td>garrison_line</td><td>卫戍防线</td><td>normal</td><td>5-8</td><td>4</td><td>卫戍协兵群体增益联动</td></tr>
<tr><td>abyss_vanguard</td><td>深渊先锋</td><td>normal</td><td>7-9</td><td>4</td><td>武学大师AOE+导师召唤</td></tr>
<tr><td>fallen_sanctum</td><td>堕落圣殿</td><td>normal</td><td>9-12</td><td>4</td><td>莱特使者+卫戍协兵高协同</td></tr>
<tr><td>doom_herald</td><td>末日先驱</td><td>normal</td><td>11-14</td><td>4</td><td>皇家剑士+莱特回响终局检验</td></tr>
<tr><td>abyss_lord</td><td>深渊领主</td><td>boss</td><td>15</td><td>4</td><td>Boss四人全明星阵容</td></tr>
</tbody></table>
<h2>4.2 详细单位配置</h2>
<table><colgroup><col width="120"/><col width="110"/><col width="60"/><col width="60"/><col width="150"/></colgroup>
<thead><tr><th background-color="light-gray">预设</th><th background-color="light-gray">单位</th><th background-color="light-gray">数量</th><th background-color="light-gray">星</th><th background-color="light-gray">战斗定位</th></tr></thead>
<tbody>
<tr><td rowspan="3">荒野劫匪</td><td>光明武士</td><td>15</td><td>★1</td><td>前排近战检验</td></tr>
<tr><td>精灵</td><td>10</td><td>★1</td><td>魔灵协同</td></tr>
<tr><td>冰霜魔灵</td><td>12</td><td>★1</td><td>后排远程压力</td></tr>
<tr><td rowspan="4">废墟哨卫</td><td>骑士</td><td>12</td><td>★2</td><td>前排高防承伤</td></tr>
<tr><td>僧侣</td><td>10</td><td>★2</td><td>后排辅助</td></tr>
<tr><td>铁匠</td><td>10</td><td>★2</td><td>多面手辅助</td></tr>
<tr><td>冰霜魔灵</td><td>15</td><td>★1</td><td>后排填充</td></tr>
<tr><td rowspan="4">暗影掠袭者</td><td>刺客</td><td>10</td><td>★3</td><td>潜行暴击</td></tr>
<tr><td>流浪者</td><td>10</td><td>★3</td><td>中距输出</td></tr>
<tr><td>铁匠</td><td>14</td><td>★2</td><td>辅助输出</td></tr>
<tr><td>光明导师</td><td>6</td><td>★4</td><td>召唤幻影</td></tr>
<tr><td rowspan="4">卫戍防线</td><td>卫戍协兵</td><td>5</td><td>★5</td><td>全军增益核心</td></tr>
<tr><td>牧师</td><td>9</td><td>★3</td><td>护盾支持</td></tr>
<tr><td>莱特使者</td><td>4</td><td>★5</td><td>信仰协同</td></tr>
<tr><td>流浪者</td><td>10</td><td>★3</td><td>持续输出</td></tr>
<tr><td rowspan="4">深渊先锋</td><td>武学大师</td><td>6</td><td>★4</td><td>AOE范围伤害</td></tr>
<tr><td>光明导师</td><td>6</td><td>★4</td><td>召唤幻影</td></tr>
<tr><td>刺客</td><td>12</td><td>★3</td><td>暴击斩杀</td></tr>
<tr><td>卫戍协兵</td><td>4</td><td>★5</td><td>全军增益</td></tr>
<tr><td rowspan="4">堕落圣殿</td><td>莱特使者</td><td>5</td><td>★5</td><td>信仰协同核心</td></tr>
<tr><td>卫戍协兵</td><td>5</td><td>★5</td><td>高防联动</td></tr>
<tr><td>光明导师</td><td>7</td><td>★4</td><td>多单位召唤</td></tr>
<tr><td>武学大师</td><td>5</td><td>★4</td><td>AOE输出</td></tr>
<tr><td rowspan="4">末日先驱</td><td>皇家剑士</td><td>4</td><td>★6</td><td>战士标签增益</td></tr>
<tr><td>莱特回响</td><td>3</td><td>★6</td><td>开战全军增益</td></tr>
<tr><td>武学大师</td><td>7</td><td>★4</td><td>数量压制</td></tr>
<tr><td>卫戍协兵</td><td>5</td><td>★5</td><td>终局协同检查</td></tr>
<tr><td rowspan="4"><b>深渊领主</b></td><td>莱特回响</td><td>3</td><td>★6</td><td>Boss核心：开战爆发</td></tr>
<tr><td>皇家剑士</td><td>5</td><td>★6</td><td>副输出：战士增益</td></tr>
<tr><td>光明导师</td><td>7</td><td>★4</td><td>战场填充</td></tr>
<tr><td>刺客</td><td>14</td><td>★3</td><td>暗刺：潜行暴击</td></tr>
</tbody></table>
<h2>4.3 技能复杂度递进</h2>
<table><colgroup><col width="80"/><col width="60"/><col width="180"/><col width="150"/></colgroup>
<thead><tr><th background-color="light-gray">阶段</th><th background-color="light-gray">层</th><th background-color="light-gray">敌方技能特点</th><th background-color="light-gray">玩家应对</th></tr></thead>
<tbody>
<tr><td>教学</td><td>1-4</td><td>无战斗技能或简单被动</td><td>熟悉基础操作</td></tr>
<tr><td>入门</td><td>5-6</td><td>潜行、暴击（刺客）</td><td>需要后排承伤</td></tr>
<tr><td>进阶</td><td>7-8</td><td>协同技能（卫戍协兵buff）</td><td>阵容协同意识</td></tr>
<tr><td>考验</td><td>9-10</td><td>AOE+召唤</td><td>站位与AOE应对</td></tr>
<tr><td>高压</td><td>11-12</td><td>高协同+高生存</td><td>完整阵容配合</td></tr>
<tr><td>终局</td><td>13-14</td><td>群体buff+AOE</td><td>接近成型阵容</td></tr>
<tr><td>Boss</td><td>15</td><td>启动+增益+暗刺</td><td>最终检验</td></tr>
</tbody></table>
<hr/>""")
print("  ✓ done")

# 第五章：数值设计
print("Upload 5/6: 数值设计...")
append_section("""<h1>五、数值设计</h1>
<h2>5.1 玩家战力增长曲线（模拟数据）</h2>
<table><colgroup><col width="60"/><col width="60"/><col width="60"/><col width="60"/><col width="60"/><col width="80"/></colgroup>
<thead><tr><th background-color="light-gray">回合</th><th background-color="light-gray">金币</th><th background-color="light-gray">棋盘</th><th background-color="light-gray">数量</th><th background-color="light-gray">均星</th><th background-color="light-gray">战力</th></tr></thead>
<tbody>
<tr><td>1</td><td>4</td><td>2</td><td>45</td><td>1.3</td><td>283</td></tr>
<tr><td>2</td><td>5</td><td>3</td><td>84</td><td>1.4</td><td>534</td></tr>
<tr><td>3</td><td>2</td><td>3</td><td>89</td><td>1.7</td><td>583</td></tr>
<tr><td>4</td><td>0</td><td>5</td><td>111</td><td>2.0</td><td>785</td></tr>
<tr><td>5</td><td>2</td><td>6</td><td>153</td><td>1.9</td><td>1045</td></tr>
<tr><td>6</td><td>1</td><td>6</td><td>166</td><td>2.0</td><td>1212</td></tr>
<tr><td>7</td><td>3</td><td>6</td><td>182</td><td>2.2</td><td>1352</td></tr>
<tr><td>8</td><td>1</td><td>6</td><td>196</td><td>2.3</td><td>1476</td></tr>
<tr><td>9</td><td>2</td><td>6</td><td>157</td><td>2.8</td><td>1379</td></tr>
<tr><td>10</td><td>8</td><td>6</td><td>171</td><td>2.8</td><td>1483</td></tr>
<tr><td>11</td><td>18</td><td>6</td><td>162</td><td>2.9</td><td>1420</td></tr>
<tr><td>12</td><td>26</td><td>6</td><td>176</td><td>2.9</td><td>1525</td></tr>
<tr><td>13</td><td>39</td><td>6</td><td>190</td><td>2.9</td><td>1629</td></tr>
<tr><td>14</td><td>61</td><td>6</td><td>204</td><td>2.9</td><td>1742</td></tr>
</tbody></table>
<callout emoji="📊" background-color="light-yellow" border-color="yellow"><p><b>关键观察：</b>第5-8轮快速拉升（合成+武学大师），第9轮因合成消耗短暂回落→V型回升至Boss前巅峰。Boss层难度=玩家战力95%。数据来源：simulate_manage_growth.py 3次平均。</p></callout>
<h2>5.2 敌人难度反推</h2>
<table><colgroup><col width="60"/><col width="80"/><col width="60"/><col width="80"/><col width="60"/><col width="80"/><col width="60"/></colgroup>
<thead><tr><th background-color="light-gray">层</th><th background-color="light-gray">玩家</th><th background-color="light-gray">%</th><th background-color="light-gray">敌方</th><th background-color="light-gray">层</th><th background-color="light-gray">玩家</th><th background-color="light-gray">%</th><th background-color="light-gray">敌方</th></tr></thead>
<tbody>
<tr><td>1</td><td>283</td><td>45%</td><td>127</td><td>8</td><td>1476</td><td>65%</td><td>959</td></tr>
<tr><td>2</td><td>534</td><td>45%</td><td>240</td><td>9</td><td>1379</td><td>65%</td><td>896</td></tr>
<tr><td>3</td><td>583</td><td>45%</td><td>262</td><td>10</td><td>1483</td><td>75%</td><td>1112</td></tr>
<tr><td>4</td><td>785</td><td>55%</td><td>431</td><td>11</td><td>1420</td><td>75%</td><td>1065</td></tr>
<tr><td>5</td><td>1045</td><td>55%</td><td>574</td><td>12</td><td>1525</td><td>75%</td><td>1143</td></tr>
<tr><td>6</td><td>1212</td><td>55%</td><td>666</td><td>13</td><td>1629</td><td>95%</td><td>1547</td></tr>
<tr><td>7</td><td>1352</td><td>65%</td><td>878</td><td>14</td><td>1742</td><td>95%</td><td>1654</td></tr>
</tbody></table>
<h2>5.3 奖励经济</h2>
<table><colgroup><col width="80"/><col width="80"/><col width="80"/><col width="80"/><col width="100"/></colgroup>
<thead><tr><th background-color="light-gray">层段</th><th background-color="light-gray">基础收入</th><th background-color="light-gray">battle</th><th background-color="light-gray">resource</th><th background-color="light-gray">日金币</th></tr></thead>
<tbody>
<tr><td>1-2</td><td>3-4</td><td>2-3</td><td>3</td><td>5-7</td></tr>
<tr><td>3-4</td><td>5-6</td><td>3-4</td><td>4</td><td>8-10</td></tr>
<tr><td>5-6</td><td>7-8</td><td>4-5</td><td>4-5</td><td>11-13</td></tr>
<tr><td>7-8</td><td>9-10</td><td>5</td><td>5-6</td><td>14-16</td></tr>
<tr><td>9-10</td><td>11-12</td><td>5-6</td><td>6</td><td>16-18</td></tr>
<tr><td>11-12</td><td>13-14</td><td>6-7</td><td>—</td><td>19-21</td></tr>
<tr><td>13-14</td><td>15-16</td><td>7-8</td><td>—</td><td>22-24</td></tr>
</tbody></table>
<h2>5.4 宝物体系</h2>
<table><colgroup><col width="120"/><col width="80"/><col width="60"/><col width="220"/></colgroup>
<thead><tr><th background-color="light-gray">宝物</th><th background-color="light-gray">首现层</th><th background-color="light-gray">出现</th><th background-color="light-gray">效果描述</th></tr></thead>
<tbody>
<tr><td>古符咒</td><td>2</td><td>1</td><td>微弱幸运加成（占位）</td></tr>
<tr><td>暗影斗篷</td><td>4</td><td>2</td><td>首次致命伤害抵消（占位）</td></tr>
<tr><td>圣香炉</td><td>5</td><td>1</td><td>入夜恢复3HP（占位）</td></tr>
<tr><td>废墟王冠</td><td>7</td><td>3</td><td>全军攻击+1（占位）</td></tr>
<tr><td>恶兆之心</td><td>10</td><td>5</td><td>开战1层护盾（占位）</td></tr>
</tbody></table>
<callout emoji="⚠️" background-color="light-orange" border-color="orange"><p>当前宝物效果均为占位符，需后续接入宝物系统实现。恶兆之心在L10-14共出现5次，确保几乎所有路线能获取至少1个。</p></callout>
<hr/>""")
print("  ✓ done")

# 第六章：兴奋曲线
print("Upload 6/6: 玩家兴奋曲线...")
append_section("""<h1>六、玩家兴奋曲线</h1>
<h2>6.1 14回合情绪节奏</h2>
<table><colgroup><col width="50"/><col width="70"/><col width="70"/><col width="200"/><col width="200"/></colgroup>
<thead><tr><th background-color="light-gray">回合</th><th background-color="light-gray">阶段</th><th background-color="light-gray">情绪</th><th background-color="light-gray">体验</th><th background-color="light-gray">设计支撑</th></tr></thead>
<tbody>
<tr><td>1</td><td>教学</td><td>轻松</td><td>首战轻松获胜，感受地图移动</td><td>★1敌人，战力仅45%</td></tr>
<tr><td>2</td><td>教学</td><td>好奇</td><td>发现宝物概念</td><td>古符咒引导宝物系统</td></tr>
<tr><td>3</td><td>展开</td><td>决策</td><td>二选一→三选一，路线有意义</td><td>3节点分支</td></tr>
<tr><td>4</td><td>展开</td><td>思考</td><td>刺客暴击，阵容感到压力</td><td>shadow_raiders首次出现</td></tr>
<tr><td>5</td><td>分支</td><td>期待</td><td>4分支，武学大师可能入场</td><td>最宽处开始成型</td></tr>
<tr><td>6</td><td>分支</td><td>兴奋</td><td>协同爆发，强力组合乐趣</td><td>卫戍协兵+莱特使者联动</td></tr>
<tr><td>7</td><td>最宽</td><td>自信</td><td>5选择，AOE+召唤敌人</td><td>深渊先锋AOE检验</td></tr>
<tr><td>8</td><td>最宽</td><td>掌控</td><td>最大决策空间，阵容稳定</td><td>5节点最大补给</td></tr>
<tr><td>9</td><td>收束</td><td>紧张</td><td>高协同敌人，阵容检验</td><td>难度65%，★4-5混合</td></tr>
<tr><td>10</td><td>收束</td><td>压力</td><td>恶兆之心首次出现</td><td>第一个大宝物</td></tr>
<tr><td>11</td><td>高压</td><td>恐惧</td><td>末日先驱，★6核心出场</td><td>难度75%</td></tr>
<tr><td>12</td><td>高压</td><td>巅峰对决</td><td>终局检验</td><td>doom_herald全套</td></tr>
<tr><td>13</td><td>逼近</td><td>决意</td><td>逼近Boss，最后补给</td><td>最后宝物+最后战斗</td></tr>
<tr><td>14</td><td>逼近</td><td>蓄力</td><td>Boss前最后一战</td><td>doom_herald最强化</td></tr>
<tr><td>15</td><td>Boss</td><td>释放</td><td>击败深渊领主→Victory！</td><td>难度95%，四人全明星</td></tr>
</tbody></table>
<h2>6.2 叙事弧设计</h2>
<callout emoji="🎢" background-color="light-purple" border-color="purple"><p><b>叙事弧：</b>教学（R1-2）低开→展开-最宽（R3-7）持续上升→合成低谷（R8-9）受挫回调→V型回升（R10-12）重建巅峰→Boss（R13-15）高潮释放。</p></callout>
<h2>6.3 关键转折点</h2>
<table><colgroup><col width="80"/><col width="80"/><col width="350"/></colgroup>
<thead><tr><th background-color="light-gray">转折</th><th background-color="light-gray">位置</th><th background-color="light-gray">设计意图</th></tr></thead>
<tbody>
<tr><td>首杀快感</td><td>R1-2</td><td>轻松获胜→建立信心→引入宝物</td></tr>
<tr><td>战略觉醒</td><td>R3-4</td><td>分支有意义→新敌人类型→需调整策略</td></tr>
<tr><td>阵容成型</td><td>R5-7</td><td>协同爆发→武学大师入场→强力组合</td></tr>
<tr><td><b>合成低谷</b></td><td>R8-9</td><td>合成消耗→暂时减员→焦虑→刺激重建动机</td></tr>
<tr><td>V型回升</td><td>R10-12</td><td>高星补位→恶兆之心获取→超之前峰值</td></tr>
<tr><td>终局释放</td><td>R13-15</td><td>最后补给→Boss前战→Victory释放</td></tr>
</tbody></table>
<callout emoji="💡" background-color="light-blue" border-color="blue"><p><b>「合成低谷」是刻意设计的情绪锚点：</b>玩家在R5-8快速拉升后，合成三★1→★2会暂时减少棋盘单位数，导致短期战力下降。这个「暂时变弱」让后续V型回升更有满足感。如果测试发现低谷太深，可通过增加R8-10宝物密度软性补偿。</p></callout>
<hr/>
<h1>附录</h1>
<h2>A. 配置文件位置</h2>
<table><colgroup><col width="200"/><col width="280"/></colgroup>
<thead><tr><th background-color="light-gray">配置</th><th background-color="light-gray">路径</th></tr></thead>
<tbody>
<tr><td>运行时地图</td><td>Assets/Resources/Data/world_maps.json</td></tr>
<tr><td>运行时敌人</td><td>Assets/Resources/Data/boss_enemies.json</td></tr>
<tr><td>运行时宝物</td><td>Assets/Resources/Data/treasures.json</td></tr>
<tr><td>CSV配置表</td><td>docs/markdown/config_tables/</td></tr>
<tr><td>数值模拟</td><td>tools/simulate_manage_growth.py</td></tr>
<tr><td>JSON生成</td><td>tools/generate_config_json.py</td></tr>
</tbody></table>
<h2>B. QA验证命令</h2>
<pre lang="powershell" caption="QA验证"><code># 单位数据验证
python tools\\qa_unit_data.py
# 战斗一致验证
python tools\\qa_battle_consistency.py
# 地图结构验证
powershell -ExecutionPolicy Bypass -File tools\\validate_world_map_mvp.ps1 -MapId abyss_wilds -MaxMovePoints 15</code></pre>
<hr/>
<p align="center"><span text-color="gray">预言世纪 · 恶兆荒野关卡设计文档 · v1.0 · 2026-06-06</span></p>""")
print("  ✓ done")

print()
print("=" * 60)
print("  文档上传完成！")
print(f"  📄 https://ifosuw0aw4.feishu.cn/docx/{DOC_ID}")
print("=" * 60)