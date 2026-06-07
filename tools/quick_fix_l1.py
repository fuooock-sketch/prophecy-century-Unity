# -*- coding: utf-8 -*-
import json, csv, subprocess

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}

def gs(uid, cnt, star):
    u = ud[uid]
    return round(u["attack"]*1.85 + u.get("defense",0)*1.25 + u.get("power",1)*18
                 + cnt*u.get("hpPerUnit",u.get("hp",1))*0.58 + u.get("speed",0)*0.72
                 + u.get("morale",0)*9 + u.get("luck",0)*5)

# L1: bright_warrior×6, elf×4 → ~115 (even 1 weak unit should win)
# L2: knight×6, monk×6, blacksmith×6 → ~270
fixes = {
    "wild_bandits": [("enemy_1","bright_warrior",6,1), ("enemy_2","elf",4,1)],
    "ruin_sentry": [("enemy_1","knight",6,2), ("enemy_2","monk",6,2), ("enemy_3","blacksmith",6,2)],
    "shadow_scout": [("enemy_1","assassin",6,4), ("enemy_2","wanderer",6,3), ("enemy_3","priest",6,3)],
    "shadow_raiders": [("enemy_1","assassin",8,5), ("enemy_2","wanderer",8,4), ("enemy_3","blacksmith",8,3), ("enemy_4","light_mentor",5,5)],
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

print("L1-L4 lowered:")
for pid in ["wild_bandits", "ruin_sentry", "shadow_scout", "shadow_raiders"]:
    ep = bp.get(pid)
    if ep:
        score = sum(gs(u["unitId"], u["count"], u["star"]) for u in ep["units"])
        desc = ", ".join(f"{u['unitId']}x{u['count']}*{u['star']}" for u in ep["units"])
        print(f"  {pid:22s} | {score:5d} | {desc}")