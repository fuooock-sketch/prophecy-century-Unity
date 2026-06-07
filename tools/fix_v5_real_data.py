# -*- coding: utf-8 -*-
"""v5: 基于真实玩家数据重新配平 L1-L4，L5+ 保持 v3 斜率。
真实数据: R1 玩家~228分(2单位), R2 玩家~186分(1单位)
"""
import json, csv, subprocess, sys

sys.stdout.reconfigure(encoding='utf-8')

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}

def gs(uid, cnt, star):
    u = ud[uid]
    return round(u["attack"]*1.85 + u.get("defense",0)*1.25 + u.get("power",1)*18
                 + cnt*u.get("hpPerUnit",u.get("hp",1))*0.58 + u.get("speed",0)*0.72
                 + u.get("morale",0)*9 + u.get("luck",0)*5)

# ================================================================
# v5: L1-L4 大幅下调，L5+ 保持 v3
# ================================================================
fixes = {
    # L1: 2 enemy units, score ~155 → player 228 稳过
    "wild_bandits": [
        ("enemy_1", "bright_warrior", 8, 1),
        ("enemy_2", "elf", 6, 1),
    ],
    # L2: 3 enemy units, score ~280 → player 2单位~400 可过
    "ruin_sentry": [
        ("enemy_1", "knight", 8, 2),
        ("enemy_2", "monk", 8, 2),
        ("enemy_3", "blacksmith", 8, 2),
    ],
    # L3: 3 enemy units, score ~380 → player ~500-600
    "shadow_scout": [
        ("enemy_1", "assassin", 8, 4),
        ("enemy_2", "wanderer", 8, 3),
        ("enemy_3", "priest", 8, 3),
    ],
    # L4: 4 enemy units, score ~500 → player ~600-700
    "shadow_raiders": [
        ("enemy_1", "assassin", 10, 5),
        ("enemy_2", "wanderer", 10, 4),
        ("enemy_3", "blacksmith", 10, 3),
        ("enemy_4", "light_mentor", 6, 5),
    ],
}

role_map = {
    "bright_warrior": ("前排压力", "基础近战"),
    "elf": ("后排输出", "魔灵协同"),
    "knight": ("前排承伤", "骑士高防"),
    "monk": ("后排辅助", "僧侣治疗"),
    "blacksmith": ("副输出", "铁匠多面手"),
    "assassin": ("前排爆发", "刺客暴击"),
    "wanderer": ("副输出", "流浪者中距输出"),
    "priest": ("后排治疗", "牧师护盾"),
    "light_mentor": ("后排召唤", "光明导师召唤幻影"),
}

# Read existing CSV
rows = []
with open("docs/markdown/config_tables/enemy_preset_units.csv", "r", encoding="utf-8-sig") as f:
    reader = csv.DictReader(f)
    fieldnames = reader.fieldnames
    for row in reader:
        pid = row["enemy_preset_id"].strip()
        if pid in fixes:
            continue  # will replace
        rows.append(row)

# Add fixed rows
for pid, units in fixes.items():
    for slot_id, uid, cnt, star in units:
        r, n = role_map.get(uid, ("", ""))
        rows.append({
            "enemy_preset_id": pid,
            "slot_id": slot_id,
            "unit_id": uid,
            "count": str(cnt),
            "star": str(star),
            "role_in_encounter": r,
            "notes": n
        })

with open("docs/markdown/config_tables/enemy_preset_units.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=fieldnames)
    w.writeheader()
    w.writerows(rows)

print(f"Updated enemy_preset_units.csv ({len(rows)} total rows)")

# Regenerate JSON
subprocess.run(["python", "tools/regenerate_boss_json.py"], check=True)

# Verify
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = {e["id"]: e for e in json.load(f)}

print("\n" + "=" * 70)
print("v5 VERIFICATION (L1-L4 fixed, L5+ unchanged)")
print("=" * 70)
for pid in ["wild_bandits", "ruin_sentry", "shadow_scout", "shadow_raiders",
            "garrison_line", "abyss_vanguard", "ruin_defender", "shadow_elite",
            "fallen_sanctum", "garrison_fortress", "doom_herald", "abyss_elite",
            "doom_commander", "doom_overlord", "abyss_lord"]:
    ep = bp.get(pid)
    if ep:
        score = sum(gs(u["unitId"], u["count"], u["star"]) for u in ep["units"])
        bar = "█" * int(score / 30)
        units_desc = ", ".join(f"{u['unitId']}×{u['count']}★{u['star']}" for u in ep["units"])
        print(f"  {pid:22s} | {score:5d} | {bar}")
        print(f"    {units_desc}")
    else:
        print(f"  {pid:22s} | MISSING!")

print("\nDone! 请在 Unity 中重新加载并战斗，然后查看日志。")