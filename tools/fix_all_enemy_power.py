# -*- coding: utf-8 -*-
"""Complete enemy power rebalance for abyss_wilds campaign.
Real game score formula: Attack*1.85 + Defense*1.25 + Power*18 + MaxHp*0.58 + Speed*0.72 + Morale*9 + Luck*5

Target curve (real scores):
  L1:  ~250 (教学级)
  L2-3: ~450 (舒适级)
  L4-6: ~700 (挑战级) 
  L7-9: ~1100 (压力级)
  L10-12: ~1600 (高压级)
  L13-14: ~2500 (终局级，末日先驱)
  L15 Boss: ~6000 (终极Boss，玩家~8000需要~75%比值)

Design philosophy: scale up counts aggressively for late-game enemies.
HP (count*hpPerUnit) contributes 0.58x to score, so counts need to be very high
to compensate for lack of synergy growth.
"""
import json
import csv
import sys
import subprocess

sys.stdout.reconfigure(encoding='utf-8')

# Load unit data
with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}


def game_score(uid, count, star):
    """Real in-game score for a single preset unit."""
    u = ud[uid]
    atk = u["attack"]
    df = u.get("defense", 0)
    pwr = u.get("power", 1)
    hp = count * u.get("hpPerUnit", u.get("hp", 1))
    spd = u.get("speed", 0)
    mor = u.get("morale", 0)
    luk = u.get("luck", 0)
    return round(atk * 1.85 + df * 1.25 + pwr * 18 + hp * 0.58 + spd * 0.72 + mor * 9 + luk * 5)


def preset_score(units):
    return sum(game_score(uid, cnt, star) for _, uid, cnt, star in units)


# ============================================================
# NEW PRESET DESIGNS
# ============================================================

new_presets = {
    # Early game: keep mostly as-is, slight buff
    "wild_bandits": [
        ("enemy_1", "bright_warrior", 12, 1),
        ("enemy_2", "elf", 10, 1),
        ("enemy_3", "frost_spirit", 10, 1),
    ],
    "ruin_sentry": [
        ("enemy_1", "knight", 15, 2),
        ("enemy_2", "monk", 12, 2),
        ("enemy_3", "blacksmith", 12, 2),
        ("enemy_4", "frost_spirit", 18, 1),
    ],
    # Mid game: push to ~700 real score range
    "shadow_raiders": [
        ("enemy_1", "assassin", 25, 4),
        ("enemy_2", "wanderer", 20, 4),
        ("enemy_3", "blacksmith", 20, 3),
        ("enemy_4", "light_mentor", 14, 5),
    ],
    # L5-8: push from ~660 to ~1000
    "garrison_line": [
        ("enemy_1", "garrison_guard", 14, 6),
        ("enemy_2", "priest", 18, 5),
        ("enemy_3", "light_envoy", 12, 6),
        ("enemy_4", "wanderer", 22, 5),
    ],
    # L7-9: push from ~730 to ~1300
    "abyss_vanguard": [
        ("enemy_1", "martial_master", 22, 6),
        ("enemy_2", "light_mentor", 20, 6),
        ("enemy_3", "assassin", 30, 5),
        ("enemy_4", "garrison_guard", 14, 6),
    ],
    # L9-12: push from ~750 to ~1800
    "fallen_sanctum": [
        ("enemy_1", "light_envoy", 20, 6),
        ("enemy_2", "garrison_guard", 22, 6),
        ("enemy_3", "light_mentor", 22, 6),
        ("enemy_4", "royal_swordsman", 15, 6),
    ],
    # L11-14: push from ~920 to ~3000
    "doom_herald": [
        ("enemy_1", "royal_swordsman", 35, 6),
        ("enemy_2", "echo_of_light", 18, 6),
        ("enemy_3", "martial_master", 30, 6),
        ("enemy_4", "garrison_guard", 30, 6),
    ],
    # L15 BOSS: push from ~1020 to ~6500 — 6 units, insane counts
    "abyss_lord": [
        ("enemy_1", "echo_of_light", 90, 6),
        ("enemy_2", "royal_swordsman", 100, 6),
        ("enemy_3", "martial_master", 80, 6),
        ("enemy_4", "garrison_guard", 90, 6),
        ("enemy_5", "light_envoy", 80, 6),
        ("enemy_6", "assassin", 100, 6),
    ],
}

# ============================================================
# Print analysis
# ============================================================
print("=" * 100)
print("NEW ENEMY POWER DESIGN (Real Game Scores)")
print("=" * 100)

preset_names = {
    "wild_bandits": "荒野劫匪",
    "ruin_sentry": "废墟哨卫",
    "shadow_raiders": "暗影掠袭者",
    "garrison_line": "卫戍防线",
    "abyss_vanguard": "深渊先锋",
    "fallen_sanctum": "堕落圣殿",
    "doom_herald": "末日先驱",
    "abyss_lord": "深渊领主",
}
preset_types = {
    "wild_bandits": "normal",
    "ruin_sentry": "normal",
    "shadow_raiders": "normal",
    "garrison_line": "normal",
    "abyss_vanguard": "normal",
    "fallen_sanctum": "normal",
    "doom_herald": "normal",
    "abyss_lord": "boss",
}
notes_map = {
    "wild_bandits": "Layer 1 教学级基础阵容检查",
    "ruin_sentry": "Layer 2-3 引入治疗和后排压力",
    "shadow_raiders": "Layer 4-5 爆发伤害和暴击检验",
    "garrison_line": "Layer 5-7 协同压力+群体增益",
    "abyss_vanguard": "Layer 7-9 AOE+召唤考验",
    "fallen_sanctum": "Layer 9-11 高协同高生存检验",
    "doom_herald": "Layer 11-13 终局强度检验",
    "abyss_lord": "终局Boss 6单位满编 ★6极限强度",
}

print("\n--- UNIT DETAIL ---")
for pid, units in new_presets.items():
    total = preset_score(units)
    slots = len(units)
    ptype = preset_types.get(pid, "normal")
    pname = preset_names.get(pid, pid)
    print(f"\n{pid:20s} | {pname:10s} | {ptype:7s} | {slots} slots | SCORE={total}")
    for slot_id, uid, cnt, star in units:
        s = game_score(uid, cnt, star)
        u = ud[uid]
        print(f"  {slot_id:10s} {uid:20s} x{cnt:3d} *{star} | atk={u['attack']} def={u.get('defense',0)} pwr={u.get('power',1)} hp={cnt}x{u.get('hpPerUnit',u.get('hp',1))} spd={u.get('speed',0)} → {s}")

print("\n" + "=" * 100)
print("PROGRESSION CURVE")
print("=" * 100)
layers = [
    ("L1", ["wild_bandits"]),
    ("L2-3", ["ruin_sentry"]),
    ("L4-6", ["shadow_raiders"]),
    ("L5-8", ["garrison_line"]),
    ("L7-9", ["abyss_vanguard"]),
    ("L9-12", ["fallen_sanctum"]),
    ("L11-14", ["doom_herald"]),
    ("L15 Boss", ["abyss_lord"]),
]
for layer, pids in layers:
    for pid in pids:
        score = preset_score(new_presets[pid])
        bar = "█" * int(score / 50)
        print(f"  {layer:8s} {preset_names[pid]:10s} SCORE={score:5d}  {bar}")

print("\n" + "=" * 100)
print("UPDATING CSV FILES...")
print("=" * 100)

# ============================================================
# Update enemy_preset_units.csv
# ============================================================
csv_path = "docs/markdown/config_tables/enemy_preset_units.csv"

# Read existing to get header
with open(csv_path, "r", encoding="utf-8-sig") as f:
    reader = csv.reader(f)
    header = next(reader)

# Write new content
role_notes = {
    "wild_bandits": {
        "bright_warrior": ("前排压力", "基础近战 检验基础阵容"),
        "elf": ("后排输出", "魔灵协同 补充近战"),
        "frost_spirit": ("后排远程", "冰霜魔灵 提供远程压力"),
    },
    "ruin_sentry": {
        "knight": ("前排承伤", "骑士高防 引入防御压力"),
        "monk": ("后排辅助", "僧侣治疗 检验续航"),
        "blacksmith": ("副输出", "铁匠多面手 辅助输出"),
        "frost_spirit": ("后排远程", "低星填充 增加目标数"),
    },
    "shadow_raiders": {
        "assassin": ("前排爆发", "刺客暴击 检验后排承伤"),
        "wanderer": ("副输出", "流浪者 中距输出"),
        "blacksmith": ("辅助输出", "铁匠 补充近战"),
        "light_mentor": ("后排召唤", "光明导师 召唤幻影"),
    },
    "garrison_line": {
        "garrison_guard": ("前排核心", "卫戍协兵 全军增益联动"),
        "priest": ("后排治疗", "牧师 护盾支持"),
        "light_envoy": ("后排支援", "莱特使者 信仰协同"),
        "wanderer": ("副输出", "流浪者 中距持续输出"),
    },
    "abyss_vanguard": {
        "martial_master": ("前排AOE", "武学大师 AOE范围伤害"),
        "light_mentor": ("后排召唤", "光明导师 召唤幻影部队"),
        "assassin": ("刺客爆发", "刺客 潜行暴击"),
        "garrison_guard": ("辅助增益", "卫戍协兵 提供全军buff"),
    },
    "fallen_sanctum": {
        "light_envoy": ("核心支援", "莱特使者 信仰协同核心"),
        "garrison_guard": ("前排承伤", "卫戍协兵 高防联动"),
        "light_mentor": ("后排召唤", "光明导师 多单位召唤"),
        "royal_swordsman": ("AOE输出", "皇家剑士 战士AOE"),
    },
    "doom_herald": {
        "royal_swordsman": ("前排核心", "皇家剑士 战士增益"),
        "echo_of_light": ("后排输出", "莱特回响 开战全军增益"),
        "martial_master": ("前排AOE", "武学大师 数量压制"),
        "garrison_guard": ("辅助增益", "卫戍协兵 终局协同检查"),
    },
    "abyss_lord": {
        "echo_of_light": ("Boss核心", "莱特回响 终局全军爆发增益"),
        "royal_swordsman": ("Boss副输出", "皇家剑士 战士标签增益"),
        "martial_master": ("Boss AOE", "武学大师 范围伤害压场"),
        "garrison_guard": ("Boss增益", "卫戍协兵 全军协同联动"),
        "light_envoy": ("Boss支援", "莱特使者 信仰体系加持"),
        "assassin": ("Boss暗刺", "刺客 潜行暴击 终局暗杀者"),
    },
}

with open(csv_path, "w", encoding="utf-8-sig", newline="") as f:
    writer = csv.writer(f)
    writer.writerow(header)
    for pid, units in new_presets.items():
        for slot_id, uid, cnt, star in units:
            roles = role_notes.get(pid, {})
            role_info = roles.get(uid, ("", ""))
            writer.writerow([pid, slot_id, uid, cnt, star, role_info[0], role_info[1]])

print(f"Updated {csv_path}")

# ============================================================
# Update enemy_presets.csv notes field
# ============================================================
preset_csv_path = "docs/markdown/config_tables/enemy_presets.csv"
rows = []
with open(preset_csv_path, "r", encoding="utf-8-sig") as f:
    reader = csv.DictReader(f)
    fieldnames = reader.fieldnames
    for row in reader:
        pid = row.get("enemy_preset_id", "").strip()
        if pid in notes_map:
            row["notes"] = notes_map[pid]
        rows.append(row)

with open(preset_csv_path, "w", encoding="utf-8-sig", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=fieldnames)
    writer.writeheader()
    writer.writerows(rows)

print(f"Updated {preset_csv_path}")

# ============================================================
# Regenerate boss_enemies.json
# ============================================================
print("\nRegenerating boss_enemies.json...")
result = subprocess.run(["python", "tools/regenerate_boss_json.py"], capture_output=True, text=True)
print(result.stdout)
if result.returncode != 0:
    print("ERROR:", result.stderr)

# ============================================================
# Final verification
# ============================================================
print("\n" + "=" * 100)
print("VERIFICATION: New Enemy Scores")
print("=" * 100)
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = json.load(f)

for ep in bp:
    eid = ep["id"]
    if eid in new_presets:
        units_list = new_presets[eid]
        total = preset_score(units_list)
        ptype = preset_types.get(eid, "normal")
        pname = preset_names.get(eid, eid)
        print(f"  {eid:20s} | {pname:10s} | {ptype:7s} | {len(units_list)} slots | SCORE={total}")

print("\n" + "=" * 100)
print("DONE! Run bot_simulate_full_run.py to verify.")
print("=" * 100)