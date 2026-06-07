# -*- coding: utf-8 -*-
"""L1: 1 enemy only. L2: 2 enemies. L3: 3 enemies. All count halved."""
import json, csv, subprocess

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}

def gs(uid, cnt, star):
    u = ud[uid]
    return round(u["attack"]*1.85 + u.get("defense",0)*1.25 + u.get("power",1)*18
                 + cnt*u.get("hpPerUnit",u.get("hp",1))*0.58 + u.get("speed",0)*0.72
                 + u.get("morale",0)*9 + u.get("luck",0)*5)

fixes = {
    "wild_bandits": [("enemy_1", "bright_warrior", 4, 1)],  # 1 enemy only, score~73
    "ruin_sentry": [("enemy_1", "knight", 4, 2), ("enemy_2", "monk", 4, 2)],  # 2 enemies, score~170
    "shadow_scout": [("enemy_1", "assassin", 4, 4), ("enemy_2", "wanderer", 4, 3), ("enemy_3", "priest", 4, 3)],  # 3 enemies, score~320
}

role_map = {
    "bright_warrior": ("前排", "★1教学级 单挑胜利"),
    "knight": ("前排", "★2 骑士入队"),
    "monk": ("后排", "★2 僧侣治疗"),
    "assassin": ("前排", "★4 刺客登场"),
    "wanderer": ("副输出", "★3 流浪者"),
    "priest": ("后排", "★3 牧师护盾"),
}

# Read existing rows, skip the ones we're replacing
rows = []
with open("docs/markdown/config_tables/enemy_preset_units.csv", "r", encoding="utf-8-sig") as f:
    reader = csv.DictReader(f)
    fieldnames = reader.fieldnames
    for row in reader:
        if row["enemy_preset_id"].strip() in fixes:
            continue
        rows.append(row)

for pid, units in fixes.items():
    for slot_id, uid, cnt, star in units:
        r, n = role_map.get(uid, ("", ""))
        rows.append({
            "enemy_preset_id": pid, "slot_id": slot_id, "unit_id": uid,
            "count": str(cnt), "star": str(star), "role_in_encounter": r, "notes": n
        })

with open("docs/markdown/config_tables/enemy_preset_units.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.DictWriter(f, fieldnames=fieldnames)
    w.writeheader()
    w.writerows(rows)

subprocess.run(["python", "tools/regenerate_boss_json.py"], check=True)

with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = {e["id"]: e for e in json.load(f)}

print("L1-L3 heavily nerfed:")
for pid in ["wild_bandits", "ruin_sentry", "shadow_scout"]:
    ep = bp.get(pid)
    if ep:
        score = sum(gs(u["unitId"], u["count"], u["star"]) for u in ep["units"])
        desc = ", ".join(f"{u['unitId']}x{u['count']}" for u in ep["units"])
        bar = "█" * int(score/5)
        print(f"  {pid:22s} | {score:4d} | {bar} | {desc}")
print("Done! 现在L1只有1个敌人4只光明武士，随便赢。")