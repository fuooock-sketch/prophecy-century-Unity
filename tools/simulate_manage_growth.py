# -*- coding: utf-8 -*-
"""经营阶段成长数值模拟 v1.0

模拟玩家从第1轮到第14轮的经营成长路径：
- 金币获取（基础收入 + 节点奖励）
- 商店购买（按星级和价格购买单位）
- 合成升级（三合一提星）
- 关键协同技能增长效果

输出每回合的预期总数量、总攻击、总防御，用于反推敌人难度曲线。
"""
import json
import math
import random
from collections import defaultdict

# ─── 加载单位数据 ───
with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8-sig") as f:
    all_units = json.load(f)

# 建立索引
units_by_id = {u["id"]: u for u in all_units}
units_by_star = defaultdict(list)
for u in all_units:
    if u.get("star", 0) >= 1 and not u.get("hidden", False):
        units_by_star[u["star"]].append(u)

# ─── 模拟参数 ───
TOTAL_ROUNDS = 14  # 总经营回合数
ROUND_INCOME_BASE = 2  # game_config 中的 roundIncomeBase
HAND_MAX = 9
BOARD_SLOTS = 6
SHOP_SLOTS = 4
UNIT_PRICE = 3  # 每张卡牌基础价格
REFRESH_COST = 1  # 刷新商店费用
SYNTHESIS_THRESHOLD = 3  # 三合一

# ─── 节点奖励配表 (战→战→战→战→战→宝→战→宝→战→战→战→战→宝→战) ───
# 策划：选择一条"偏战斗"路线来模拟上限难度
NODE_REWARDS = [
    {"gold": 2, "treasure": False},   # 回合1
    {"gold": 3, "treasure": False},   # 回合2
    {"gold": 3, "treasure": False},   # 回合3
    {"gold": 4, "treasure": False},   # 回合4
    {"gold": 4, "treasure": False},   # 回合5
    {"gold": 0, "treasure": True},    # 回合6 (宝物)
    {"gold": 5, "treasure": False},   # 回合7
    {"gold": 0, "treasure": True},    # 回合8 (宝物)
    {"gold": 5, "treasure": False},   # 回合9
    {"gold": 6, "treasure": False},   # 回合10
    {"gold": 6, "treasure": False},   # 回合11
    {"gold": 7, "treasure": False},   # 回合12
    {"gold": 0, "treasure": True},    # 回合13 (宝物)
    {"gold": 8, "treasure": False},   # 回合14
]

class UnitState:
    """运行时单位状态"""
    def __init__(self, unit_def, count=None, star=None):
        self.unit_id = unit_def["id"]
        self.name = unit_def["name"]
        self.star = star or unit_def["star"]
        self.race = unit_def.get("race", "")
        self.faith = unit_def.get("faith", "")
        self.type = unit_def.get("type", "melee")
        self.tags = unit_def.get("tags", [])
        self.count = count or unit_def.get("defaultCount", unit_def.get("startCount", 10))
        self.base_count = self.count
        self.hp_per_unit = unit_def.get("hpPerUnit", unit_def.get("hp", 1))
        self.attack = unit_def.get("attack", 1)
        self.defense = unit_def.get("defense", 0)
        self.speed = unit_def.get("speed", 0)
        self.luck = unit_def.get("luck", 0)
        self.morale = unit_def.get("morale", 0)
        self.power = unit_def.get("power", 0)
        self.talents = unit_def.get("talents", [])
        self.gold_talents = unit_def.get("goldTalents", [])
        self.battle_skills = unit_def.get("battleSkills", [])
        self.is_golden = False
        
    def total_hp(self):
        return self.count * self.hp_per_unit
    
    def estimated_combat_power(self):
        """简化战力估算"""
        return self.count * (self.hp_per_unit + self.attack + self.defense) / 3 * (1 + (self.star - 1) * 0.3)
    
    def clone(self):
        """深拷贝"""
        import copy
        return copy.deepcopy(self)

class PlayerState:
    """玩家经营状态"""
    def __init__(self):
        self.gold = 5  # 初始金币
        self.round = 0
        self.shop_level = 1
        self.board = []  # 棋盘上的单位 (最多6个)
        self.hand = []   # 手牌 (最多9张)
        self.shop = []   # 当前商店
    
    def max_shop_star(self):
        return min(self.shop_level + 1, 6)

# 核心协同单位池（模拟玩家会主动寻找这些单位）
SYNERGY_CORE_UNITS = {
    1: ["bright_warrior", "elf"],           # ★1: 光明武士, 精灵
    2: ["knight", "monk", "blacksmith"],     # ★2: 骑士, 僧侣, 铁匠
    3: ["assassin", "priest", "wanderer"],    # ★3: 刺客, 牧师, 流浪者
    4: ["martial_master", "light_mentor"],    # ★4: 武学大师, 光明导师
    5: ["garrison_guard", "light_envoy"],     # ★5: 卫戍协兵, 莱特使者
    6: ["royal_swordsman", "echo_of_light"],  # ★6: 皇家剑士, 莱特回响
}

# 非核心但可用的填充单位
FILLER_UNITS = {
    1: ["small_merchant", "frost_spirit"],
    2: [],
    3: [],
    4: [],
    5: [],
    6: [],
}

def pick_target_unit(star, prefer_synergy=True):
    """
    选择一个目标单位。模拟玩家行为：
    - 优先选核心协同单位（模拟玩家有策略地选择）
    - 如果已有该单位，倾向选同一个（模拟追三星）
    """
    core_ids = SYNERGY_CORE_UNITS.get(star, [])
    filler_ids = FILLER_UNITS.get(star, [])
    all_candidates = core_ids + filler_ids
    
    if not all_candidates:
        return None
    
    # 70%概率选核心，30%选填充
    if prefer_synergy and core_ids and random.random() < 0.7:
        chosen_id = core_ids[random.randint(0, len(core_ids) - 1)]
    else:
        chosen_id = all_candidates[random.randint(0, len(all_candidates) - 1)]
    
    return units_by_id.get(chosen_id)

def simulate_shop_buy(state, strategy="synergy"):
    """
    模拟一次完整的商店阶段。
    改进：多轮刷新+购买，模拟玩家真实决策。
    商店等级越高，刷新次数越多。
    """
    max_star = state.max_shop_star()
    refresh_count = min(3, 1 + state.shop_level // 2)  # 1-3次刷新
    bought_this_round = 0
    
    for _ in range(refresh_count):
        if state.gold < UNIT_PRICE:
            break
        
        # 生成商店
        state.shop = []
        for __ in range(SHOP_SLOTS):
            star = min(max_star, random.randint(1, max_star))
            unit_def = pick_target_unit(star, prefer_synergy=(strategy == "synergy"))
            if unit_def:
                state.shop.append(unit_def)
        
        # 购买：买1-2个单位（取决于金币和手牌空间）
        buy_count = min(2, (state.gold // UNIT_PRICE), HAND_MAX - len(state.hand))
        for __ in range(buy_count):
            if not state.shop or state.gold < UNIT_PRICE or len(state.hand) >= HAND_MAX:
                break
            
            candidates = [u for u in state.shop if u is not None]
            if not candidates:
                break
            
            # 策略：优先已有的单位（追三星合成）、其次是核心单位
            existing_ids = set()
            for u in state.board + state.hand:
                existing_ids.add(u.unit_id)
            
            duplicate = [u for u in candidates if u.get("id") in existing_ids]
            if duplicate:
                target = duplicate[0]
            elif strategy == "synergy":
                preferred = [u for u in candidates if u.get("race") == "甘地" or u.get("faith") == "莱特"]
                target = preferred[0] if preferred else candidates[0]
            else:
                target = candidates[0]
            
            state.gold -= UNIT_PRICE
            unit = UnitState(target)
            state.hand.append(unit)
            state.shop.remove(target)
            bought_this_round += 1
    
    return bought_this_round

def try_synthesize(state):
    """三合一合成"""
    synthesized = False
    # 按单位ID分组，寻找3个同类同星
    groups = defaultdict(list)
    for i, unit in enumerate(state.board + state.hand):
        key = (unit.unit_id, unit.star)
        groups[key].append((i, unit))
    
    for key, items in groups.items():
        while len(items) >= SYNTHESIS_THRESHOLD:
            synthesized = True
            # 取前3个
            triple = items[:3]
            for idx, unit in triple:
                if unit in state.board:
                    state.board.remove(unit)
                if unit in state.hand:
                    state.hand.remove(unit)
            
            # 生成升级单位
            unit_id, star = key
            new_star = min(6, star + 1)
            base_def = units_by_id.get(unit_id)
            if base_def:
                new_unit = UnitState(base_def, star=new_star)
                if len(state.board) < BOARD_SLOTS:
                    state.board.append(new_unit)
                else:
                    state.hand.append(new_unit)
            
            items = items[3:]

def resolve_synergy_growth(board):
    """
    解析棋盘协同技能的数量增长。
    只模拟关键协同技能，不完整实现所有技能。
    
    核心协同链：
    1. 光明武士: 相邻莱特信仰者 → +3数量
    2. 精灵: 入场甘地 → +2数量 (上限3次/回合)
    3. 武学大师: 重触发所有甘地回合结束技能 (翻倍)
    4. 卫戍协兵: 任意友军获得数量 → 全军+1
    5. 皇家剑士: 回合结束全体战士+1数量
    """
    total_gain = 0
    board = [u for u in board if u is not None]
    
    # 统计种族和信仰
    race_counts = defaultdict(int)
    faith_counts = defaultdict(int)
    tag_counts = defaultdict(int)
    for unit in board:
        race_counts[unit.race] += 1
        faith_counts[unit.faith] += 1
        for tag in unit.tags:
            tag_counts[tag] += 1
    
    # 1. 光明武士: 相邻莱特信仰者增益
    for unit in board:
        for talent in unit.talents:
            if talent.get("kind") == "round_end_if_adjacent_faith_self_gain_attack":
                faith = talent.get("faith", "")
                value = talent.get("value", 0)
                # 简化：假设场上莱特信仰者个数的一半为邻接数
                adjacent_count = min(2, faith_counts.get(faith, 0))
                gain = adjacent_count * value
                unit.count += gain
                total_gain += gain
    
    # 2. 精灵: 入场甘地增益
    for unit in board:
        for talent in unit.talents:
            if talent.get("kind") == "while_on_board_on_entry_race_self_gain_attack":
                race = talent.get("race", "")
                value = talent.get("value", 0)
                max_triggers = talent.get("count", 3)
                entries = min(max_triggers, race_counts.get(race, 0))
                gain = entries * value
                unit.count += gain
                total_gain += gain
    
    # 3. 武学大师: 重触发甘地回合结束技能 (核心放大器)
    wm_count = 0
    for unit in board:
        for talent in unit.talents:
            if talent.get("kind") == "round_start_retrigger_race_round_end_talents":
                times = talent.get("times", 1)
                wm_count += times
    
    if wm_count > 0 and race_counts.get("甘地", 0) >= 3:
        # 武学大师再次触发所有甘地单位的回合结束效果
        # 简化：将甘地单位的总增长再应用 wm_count 次
        gandhi_gain = 0
        for unit in board:
            if unit.race == "甘地":
                for talent in unit.talents:
                    if "round_end" in talent.get("kind", ""):
                        gandhi_gain += talent.get("value", 0)
        bonus = gandhi_gain * wm_count
        # 分散到甘地单位
        gandhi_units = [u for u in board if u.race == "甘地"]
        if gandhi_units:
            per_unit = bonus // len(gandhi_units)
            for u in gandhi_units:
                u.count += per_unit
                total_gain += per_unit
    
    # 4. 卫戍协兵: 全军增益联动
    for unit in board:
        for talent in unit.talents:
            if talent.get("kind") == "on_gain_defense_team_gain_attack":
                value = talent.get("value", 0)
                if total_gain > 0:
                    for u in board:
                        u.count += value
                        total_gain += value
    
    # 5. 皇家剑士: 战士标签增益
    for unit in board:
        for talent in unit.talents:
            if talent.get("kind") == "round_end_tagged_units_gain_attack_and_defense":
                tag = talent.get("targetTag", "")
                value = talent.get("value", 0)
                tag_count = tag_counts.get(tag, 0)
                if tag_count > 0:
                    gain = tag_count * value
                    unit.count += gain
                    total_gain += gain
    
    return total_gain

def deploy_to_board(state):
    """将手牌部署到棋盘"""
    while len(state.board) < BOARD_SLOTS and len(state.hand) > 0:
        state.board.append(state.hand.pop(0))

def simulate_one_run(seed=42, strategy="synergy"):
    """模拟一局游戏"""
    random.seed(seed)
    state = PlayerState()
    history = []
    
    for round_idx in range(TOTAL_ROUNDS):
        state.round = round_idx + 1
        
        # 每日收入
        daily_income = ROUND_INCOME_BASE + state.round
        state.gold += daily_income
        
        # 节点奖励
        reward = NODE_REWARDS[min(round_idx, len(NODE_REWARDS) - 1)]
        state.gold += reward["gold"]
        
        # 经营操作
        deploy_to_board(state)
        
        # 商店购买
        units_bought = simulate_shop_buy(state, strategy)
        
        # 部署新单位
        deploy_to_board(state)
        
        # 合成
        try_synthesize(state)
        
        # ⭐ 协同增长（只计算一次，简化模拟）
        synergy_gain = resolve_synergy_growth(state.board)
        
        # 部署合成后的单位
        deploy_to_board(state)
        
        # 如果商店等级可升级（每3回合）
        if state.round % 3 == 0 and state.gold >= 5:
            state.shop_level = min(6, state.shop_level + 1)
            state.gold -= 5
        
        # 记录回合数据
        total_count = sum(u.count for u in state.board)
        total_attack = sum(u.attack * u.count for u in state.board)
        total_defense = sum(u.defense * u.count for u in state.board)
        total_hp = sum(u.total_hp() for u in state.board)
        avg_star = sum(u.star for u in state.board) / max(1, len(state.board))
        combat_power = sum(u.estimated_combat_power() for u in state.board)
        
        history.append({
            "round": state.round,
            "gold": state.gold,
            "board_size": len(state.board),
            "hand_size": len(state.hand),
            "total_count": total_count,
            "total_attack": total_attack,
            "total_defense": total_defense,
            "total_hp": total_hp,
            "avg_star": round(avg_star, 1),
            "synergy_gain": synergy_gain,
            "combat_power": round(combat_power),
        })
    
    return history

def estimate_enemy_difficulty(player_history, player_power_key="combat_power"):
    """
    基于玩家战力曲线反推敌人难度。
    Boss层(target_pct)设为玩家战力的 85%-110%
    普通战斗层设为玩家战力的 45%-75%
    """
    enemy_config = []
    for i, entry in enumerate(player_history):
        player_power = entry[player_power_key]
        round_num = entry["round"]
        
        # 根据层级确定难度系数
        if round_num <= 3:
            pct = 0.45  # 教学阶段，敌人为玩家的45%
        elif round_num <= 6:
            pct = 0.55  # 热身
        elif round_num <= 9:
            pct = 0.65  # 检验
        elif round_num <= 12:
            pct = 0.75  # 高压
        else:
            pct = 0.95  # Boss层
        
        target_enemy_power = int(player_power * pct)
        
        enemy_config.append({
            "layer": round_num,
            "player_power": player_power,
            "difficulty_pct": pct,
            "target_enemy_power": target_enemy_power,
        })
    
    return enemy_config

# ─── 主模拟 ───
print("=" * 90)
print("  预言世纪 · 经营阶段成长数值模拟")
print("=" * 90)
print()

# 运行3次模拟取平均值
all_histories = []
seeds = [42, 99, 777]
for seed in seeds:
    history = simulate_one_run(seed=seed, strategy="synergy")
    all_histories.append(history)

# 计算平均值
print(f"{'回合':>4} | {'金币':>5} | {'棋盘数':>5} | {'总数量':>7} | {'总攻击':>7} | {'总HP':>7} | {'均星':>5} | {'协同增量':>8} | {'战力':>7}")
print("-" * 90)

avg_history = []
for i in range(TOTAL_ROUNDS):
    golds = [h[i]["gold"] for h in all_histories]
    counts = [h[i]["total_count"] for h in all_histories]
    attacks = [h[i]["total_attack"] for h in all_histories]
    hps = [h[i]["total_hp"] for h in all_histories]
    stars = [h[i]["avg_star"] for h in all_histories]
    synergies = [h[i]["synergy_gain"] for h in all_histories]
    powers = [h[i]["combat_power"] for h in all_histories]
    
    avg = {
        "round": i + 1,
        "gold": round(sum(golds) / len(golds)),
        "board_size": round(sum(h[i]["board_size"] for h in all_histories) / len(all_histories)),
        "total_count": round(sum(counts) / len(counts)),
        "total_attack": round(sum(attacks) / len(attacks)),
        "total_hp": round(sum(hps) / len(hps)),
        "avg_star": round(sum(stars) / len(stars), 1),
        "synergy_gain": round(sum(synergies) / len(synergies)),
        "combat_power": round(sum(powers) / len(powers)),
    }
    avg_history.append(avg)
    
    print(f"  {i+1:2} | {avg['gold']:5} | {avg['board_size']:5} | {avg['total_count']:7} | {avg['total_attack']:7} | {avg['total_hp']:7} | {avg['avg_star']:5.1f} | {avg['synergy_gain']:8} | {avg['combat_power']:7}")

print()
print("-" * 90)
print("  敌人难度反推建议")
print("-" * 90)

enemy_suggestions = estimate_enemy_difficulty(avg_history)
print(f"{'层':>4} | {'玩家战力':>8} | {'难度%':>6} | {'建议敌方战力':>10}")
print("-" * 50)
for e in enemy_suggestions:
    print(f"  {e['layer']:2} | {e['player_power']:8} | {e['difficulty_pct']*100:5.0f}% | {e['target_enemy_power']:10}")

print()
print("=" * 90)
print("  模拟完成")
print(f"  策略: synergy (优先甘地+莱特协同)")
print(f"  模拟次数: {len(seeds)}")
print(f"  总经营回合: {TOTAL_ROUNDS}")
print("=" * 90)