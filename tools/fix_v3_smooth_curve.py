# -*- coding: utf-8 -*-
"""v3 平滑难度曲线 + 经济调整
目标: 每层 +12-20% 平滑递增，Boss=5000 为顶点。
新增 7 个中间预设填补断层，共 15 个预设覆盖 24 个战斗节点。

商店等级里程碑:
  R3  Lv2 → ★3 入场 (shadow_scout 出现)
  R6  Lv3 → ★4 入场 (abyss_vanguard 升级)
  R9  Lv4 → ★5 入场 (shadow_elite 精英)
  R12 Lv5 → ★6 入场 (doom 系列登场)

经济调整:
  - 基础收入 2→2 (不变，但节点金币 L8+ 适当下调)
  - 商店升级费 5→7 (减缓Lv5冲到★6的节奏)
  - 商店升级改为 R3/7/11/14 (更均匀分布)
"""
import json, csv, sys, subprocess

sys.stdout.reconfigure(encoding='utf-8')

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}

def gs(uid, count, star):
    u = ud[uid]
    return round(u["attack"]*1.85 + u.get("defense",0)*1.25 + u.get("power",1)*18
                 + count*u.get("hpPerUnit",u.get("hp",1))*0.58 + u.get("speed",0)*0.72
                 + u.get("morale",0)*9 + u.get("luck",0)*5)

# ================================================================
# v3 PRESET DESIGN: 15 presets, smooth progression
# ================================================================
# Format: preset_id: [(slot_id, unit_id, count, star), ...]
# Key principle: count scales with unit quality to hit target score
v3 = {}

# L1: wild_bandits → 250
v3["wild_bandits"] = [
    ("enemy_1", "bright_warrior", 14, 1),
    ("enemy_2", "elf", 12, 1),
    ("enemy_3", "frost_spirit", 12, 1),
]
# L2: ruin_sentry → 480
v3["ruin_sentry"] = [
    ("enemy_1", "knight", 16, 2),
    ("enemy_2", "monk", 14, 2),
    ("enemy_3", "blacksmith", 14, 2),
    ("enemy_4", "frost_spirit", 20, 1),
]
# L3: shadow_scout (NEW) → 620 — ★3 unit showcase, bridge to shadow_raiders
v3["shadow_scout"] = [
    ("enemy_1", "assassin", 15, 4),
    ("enemy_2", "wanderer", 15, 3),
    ("enemy_3", "priest", 14, 3),
    ("enemy_4", "frost_spirit", 22, 1),
]
# L4: shadow_raiders → 800 — ★4 assassin peak
v3["shadow_raiders"] = [
    ("enemy_1", "assassin", 22, 5),
    ("enemy_2", "wanderer", 20, 4),
    ("enemy_3", "blacksmith", 20, 3),
    ("enemy_4", "light_mentor", 14, 5),
]
# L5: garrison_line → 1000 — ★5 garrison debut (R3 shop Lv2→★3, player has synergy starting)
v3["garrison_line"] = [
    ("enemy_1", "garrison_guard", 16, 6),
    ("enemy_2", "priest", 20, 5),
    ("enemy_3", "light_envoy", 14, 6),
    ("enemy_4", "wanderer", 24, 4),
]
# L6: abyss_vanguard → 1200 — ★4 AOE peak before defender
v3["abyss_vanguard"] = [
    ("enemy_1", "martial_master", 22, 6),
    ("enemy_2", "light_mentor", 20, 6),
    ("enemy_3", "assassin", 28, 5),
    ("enemy_4", "garrison_guard", 18, 6),
]
# L7: ruin_defender (NEW) → 1400 — ★5 heavy defense, player just hit Lv3 shop (★4)
v3["ruin_defender"] = [
    ("enemy_1", "garrison_guard", 28, 6),
    ("enemy_2", "royal_swordsman", 8, 6),
    ("enemy_3", "light_envoy", 20, 6),
    ("enemy_4", "priest", 25, 6),
]
# L8: fallen_sanctum → 1650 — ★6 all-star, player Lv3 shop
v3["fallen_sanctum"] = [
    ("enemy_1", "light_envoy", 25, 6),
    ("enemy_2", "garrison_guard", 28, 6),
    ("enemy_3", "light_mentor", 28, 6),
    ("enemy_4", "royal_swordsman", 18, 6),
]
# L9: shadow_elite (NEW) → 1900 — elite ★5-6, player just hit Lv4 shop (★5)
v3["shadow_elite"] = [
    ("enemy_1", "assassin", 40, 6),
    ("enemy_2", "martial_master", 30, 6),
    ("enemy_3", "wanderer", 40, 6),
    ("enemy_4", "garrison_guard", 25, 6),
]
# L10: garrison_fortress (NEW) → 2150 — ★6 fortress, player Lv4
v3["garrison_fortress"] = [
    ("enemy_1", "garrison_guard", 40, 6),
    ("enemy_2", "royal_swordsman", 25, 6),
    ("enemy_3", "echo_of_light", 20, 6),
    ("enemy_4", "light_envoy", 35, 6),
]
# L11: doom_herald → 2450 — ★6 endgame entry, player just hit Lv5 shop (★6)
v3["doom_herald"] = [
    ("enemy_1", "royal_swordsman", 45, 6),
    ("enemy_2", "echo_of_light", 25, 6),
    ("enemy_3", "martial_master", 40, 6),
    ("enemy_4", "garrison_guard", 40, 6),
]
# L12: abyss_elite (NEW) → 2800 — ★6 full power
v3["abyss_elite"] = [
    ("enemy_1", "echo_of_light", 40, 6),
    ("enemy_2", "royal_swordsman", 55, 6),
    ("enemy_3", "martial_master", 50, 6),
    ("enemy_4", "garrison_guard", 55, 6),
]
# L13: doom_commander (NEW) → 3200 — Boss前卫
v3["doom_commander"] = [
    ("enemy_1", "royal_swordsman", 65, 6),
    ("enemy_2", "echo_of_light", 50, 6),
    ("enemy_3", "martial_master", 60, 6),
    ("enemy_4", "garrison_guard", 65, 6),
]
# L14: doom_overlord (NEW) → 3600 — Boss序章
v3["doom_overlord"] = [
    ("enemy_1", "royal_swordsman", 75, 6),
    ("enemy_2", "echo_of_light", 60, 6),
    ("enemy_3", "martial_master", 70, 6),
    ("enemy_4", "garrison_guard", 75, 6),
]
# L15: abyss_lord → 5000 — Boss (从5820下调，曲线更自然)
v3["abyss_lord"] = [
    ("enemy_1", "echo_of_light", 75, 6),
    ("enemy_2", "royal_swordsman", 85, 6),
    ("enemy_3", "martial_master", 70, 6),
    ("enemy_4", "garrison_guard", 80, 6),
    ("enemy_5", "light_envoy", 70, 6),
    ("enemy_6", "assassin", 85, 6),
]

# Verify scores
print("=" * 90)
print("v3 SMOOTH CURVE — Enemy Power by Preset")
print("=" * 90)
for pid, units in v3.items():
    total = sum(gs(uid, cnt, star) for _, uid, cnt, star in units)
    bar = "█" * int(total / 40)
    print(f"{pid:22s} | {total:5d} | {bar}")

# ================================================================
# NODE → PRESET MAPPING (one preset per layer primary path)
# ================================================================
# node_id → new_preset_id
node_preset_map = {
    # L1
    "abyss_L1_battle_1": "wild_bandits",
    # L2
    "abyss_L2_battle_1": "ruin_sentry",
    # L3 — both nodes use new shadow_scout
    "abyss_L3_battle_1": "shadow_scout",
    "abyss_L3_battle_2": "shadow_scout",
    # L4
    "abyss_L4_battle_1": "shadow_raiders",
    # L5 — primary: garrison_line, secondary: shadow_raiders (easier branch)
    "abyss_L5_battle_1": "garrison_line",
    "abyss_L5_battle_2": "shadow_raiders",  # easier branch
    # L6 — primary: abyss_vanguard, secondary: garrison_line
    "abyss_L6_battle_1": "abyss_vanguard",
    "abyss_L6_battle_2": "garrison_line",  # easier branch
    # L7 — primary: ruin_defender, secondary: abyss_vanguard
    "abyss_L7_battle_1": "ruin_defender",
    "abyss_L7_battle_2": "abyss_vanguard",  # easier branch
    # L8 — primary: fallen_sanctum, secondary: ruin_defender
    "abyss_L8_battle_1": "fallen_sanctum",
    "abyss_L8_battle_2": "ruin_defender",  # easier branch
    # L9 — primary: shadow_elite, secondary: fallen_sanctum
    "abyss_L9_battle_1": "shadow_elite",
    "abyss_L9_battle_2": "fallen_sanctum",  # easier branch
    # L10 — primary: garrison_fortress, secondary: shadow_elite
    "abyss_L10_battle_1": "garrison_fortress",
    "abyss_L10_battle_2": "shadow_elite",  # easier branch
    # L11 — primary: doom_herald, secondary: garrison_fortress
    "abyss_L11_battle_1": "doom_herald",
    "abyss_L11_battle_2": "garrison_fortress",  # easier branch
    # L12 — primary: abyss_elite, secondary: doom_herald
    "abyss_L12_battle_1": "abyss_elite",
    "abyss_L12_battle_2": "doom_herald",  # easier branch
    # L13 — only one battle
    "abyss_L13_battle_1": "doom_commander",
    # L14 — only one battle
    "abyss_L14_battle_1": "doom_overlord",
    # L15 — Boss
    "abyss_throne": "abyss_lord",
}

# ================================================================
# PRESET METADATA
# ================================================================
preset_names = {
    "wild_bandits": "荒野劫匪", "ruin_sentry": "废墟哨卫",
    "shadow_scout": "暗影斥候", "shadow_raiders": "暗影掠袭者",
    "garrison_line": "卫戍防线", "abyss_vanguard": "深渊先锋",
    "ruin_defender": "废墟守卫", "fallen_sanctum": "堕落圣殿",
    "shadow_elite": "暗影精锐", "garrison_fortress": "卫戍要塞",
    "doom_herald": "末日先驱", "abyss_elite": "深渊精锐",
    "doom_commander": "末日统领", "doom_overlord": "末日霸主",
    "abyss_lord": "深渊领主",
}
preset_types = {p: "boss" if p == "abyss_lord" else "normal" for p in v3}
preset_notes = {
    "wild_bandits": "L1 教学级 ★1基础阵容",
    "ruin_sentry": "L2 引入★2治疗和后排压力",
    "shadow_scout": "L3 ★3刺客首次亮相 检验暴击承伤",
    "shadow_raiders": "L4 ★4精英刺客 暴击压力升级",
    "garrison_line": "L5 ★5卫戍协兵 协同增益入门",
    "abyss_vanguard": "L6 ★4武学大师 AOE范围压力",
    "ruin_defender": "L7 ★5重装防线 高防联动检验",
    "fallen_sanctum": "L8 ★6满星高协同 阵容成型检验",
    "shadow_elite": "L9 ★6暗影精英 商店Lv4后检验",
    "garrison_fortress": "L10 ★6卫戍要塞 全员★6压制",
    "doom_herald": "L11 终局入门 ★6四核检验",
    "abyss_elite": "L12 深渊精锐 ★6全明星阵容",
    "doom_commander": "L13 Boss前卫战 ★6极限count",
    "doom_overlord": "L14 Boss序章 ★6超限count",
    "abyss_lord": "终局Boss 6槽满编 ★6极限强度",
}

# ================================================================
# ROLE NOTES for CSV
# ================================================================
def role_for(pid, uid, i):
    """Generate role/notes for CSV"""
    roles = {
        "bright_warrior": ("前排压力", "基础近战"),
        "elf": ("后排输出", "魔灵协同"),
        "frost_spirit": ("后排远程", "冰霜魔灵填充"),
        "knight": ("前排承伤", "骑士高防"),
        "monk": ("后排辅助", "僧侣治疗"),
        "blacksmith": ("副输出", "铁匠多面手"),
        "assassin": ("前排爆发", "刺客暴击"),
        "wanderer": ("副输出", "流浪者中距输出"),
        "priest": ("后排治疗", "牧师护盾"),
        "light_mentor": ("后排召唤", "光明导师召唤幻影"),
        "martial_master": ("前排AOE", "武学大师范围伤害"),
        "garrison_guard": ("前排核心/增益", "卫戍协兵全军联动"),
        "light_envoy": ("后排支援", "莱特使者信仰协同"),
        "royal_swordsman": ("核心输出", "皇家剑士战士增益"),
        "echo_of_light": ("Boss核心/爆发", "莱特回响开战全军增益"),
    }
    role = roles.get(uid, ("", ""))
    # Tweak for Boss
    if pid == "abyss_lord":
        if uid == "echo_of_light": return ("Boss核心", "莱特回响终局全军爆发")
        if uid == "royal_swordsman": return ("Boss副输出", "皇家剑士战士标签增益")
        if uid == "martial_master": return ("Boss AOE", "武学大师范围压场")
        if uid == "garrison_guard": return ("Boss增益", "卫戍协兵全军协同")
        if uid == "light_envoy": return ("Boss支援", "莱特使者信仰加持")
        if uid == "assassin": return ("Boss暗刺", "刺客潜行暴击暗杀")
    return role

# ================================================================
# WRITE CSV FILES
# ================================================================
print("\n" + "=" * 90)
print("WRITING CSV FILES...")
print("=" * 90)

# 1. enemy_preset_units.csv
units_csv = "docs/markdown/config_tables/enemy_preset_units.csv"
with open(units_csv, "r", encoding="utf-8-sig") as f:
    header = next(csv.reader(f))
with open(units_csv, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f)
    w.writerow(header)
    for pid, units in v3.items():
        for i, (slot_id, uid, cnt, star) in enumerate(units):
            r, n = role_for(pid, uid, i)
            w.writerow([pid, slot_id, uid, cnt, star, r, n])
print(f"  Updated {units_csv} ({len(v3)} presets, {sum(len(u) for u in v3.values())} units)")

# 2. enemy_presets.csv
preset_csv = "docs/markdown/config_tables/enemy_presets.csv"
# Read existing to get fieldnames and non-unit rows
existing_rows = []
with open(preset_csv, "r", encoding="utf-8-sig") as f:
    reader = csv.DictReader(f)
    fieldnames = reader.fieldnames
    for row in reader:
        existing_rows.append(row)

# Build new rows: keep only presets in v3, add new ones
existing_pids = set(r["enemy_preset_id"].strip() for r in existing_rows)
new_preset_order = list(v3.keys())

new_preset_rows = []
for pid in new_preset_order:
    # Find existing row or create new
    row = None
    for r in existing_rows:
        if r["enemy_preset_id"].strip() == pid:
            row = r
            break
    if row is None:
        row = {k: "" for k in fieldnames}
        row["enemy_preset_id"] = pid
    row["name"] = preset_names.get(pid, pid)
    row["type"] = preset_types.get(pid, "normal")
    row["notes"] = preset_notes.get(pid, "")
    # Keep existing used_by_node_id if present
    if not row.get("used_by_node_id"):
        row["used_by_node_id"] = pid
    row["difficulty_role"] = preset_notes.get(pid, "")
    new_preset_rows.append(row)

with open(preset_csv, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=fieldnames)
    w.writeheader()
    w.writerows(new_preset_rows)
print(f"  Updated {preset_csv} ({len(new_preset_rows)} presets)")

# 3. world_map_nodes.csv — update enemy_preset_id references
nodes_csv = "docs/markdown/config_tables/world_map_nodes.csv"
node_rows = []
with open(nodes_csv, "r", encoding="utf-8-sig") as f:
    reader = csv.DictReader(f)
    node_fieldnames = reader.fieldnames
    for row in reader:
        nid = row.get("node_id", "").strip()
        if nid in node_preset_map:
            row["enemy_preset_id"] = node_preset_map[nid]
        node_rows.append(row)

with open(nodes_csv, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=node_fieldnames)
    w.writeheader()
    w.writerows(node_rows)
changed_nodes = [nid for nid in node_preset_map if nid in {r.get("node_id","").strip() for r in node_rows}]
print(f"  Updated {nodes_csv} ({len(changed_nodes)} nodes remapped)")

# ================================================================
# REGENERATE JSON
# ================================================================
print("\nRegenerating boss_enemies.json...")
result = subprocess.run(["python", "tools/regenerate_boss_json.py"], capture_output=True, text=True)
print(result.stdout.strip())
if result.returncode != 0:
    print("ERROR:", result.stderr)
    sys.exit(1)

# ================================================================
# FINAL VERIFICATION
# ================================================================
print("\n" + "=" * 90)
print("v3 VERIFICATION: New Enemy Scores")
print("=" * 90)
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = {e["id"]: e for e in json.load(f)}

prev = 0
for pid in v3:
    total = sum(gs(uid, cnt, star) for _, uid, cnt, star in v3[pid])
    delta = total - prev if prev > 0 else 0
    pct = f"+{delta/prev*100:.0f}%" if prev > 0 else ""
    bar = "█" * int(total / 40)
    print(f"  {pid:22s} | {total:5d} ({pct:>5s}) | {bar}")
    prev = total

print("\n" + "=" * 90)
print("v3 DONE! Ready for simulation.")
print("=" * 90)