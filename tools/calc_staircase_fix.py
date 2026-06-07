# -*- coding: utf-8 -*-
"""Calculate power boost needed for each preset to form a difficulty staircase."""
import json

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = json.load(f)
um = {u["id"]: u for u in ud}
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = json.load(f)


def unit_power(uid, count, star):
    u = um.get(uid, {})
    hp = u.get("hpPerUnit", u.get("hp", 1))
    atk = u.get("attack", 1)
    df = u.get("defense", 0)
    return count * (hp + atk + df) / 3 * (1 + (star - 1) * 0.3)


def preset_power(p):
    return sum(unit_power(u["unitId"], u["count"], u["star"]) for u in p.get("units", []))


# Target powers (staircase design)
targets = {
    "wild_bandits": 132,     # keep teaching
    "ruin_sentry": 364,      # keep - already reasonable for L2-3
    "shadow_raiders": 750,   # was 635, +18%
    "garrison_line": 880,    # was 660, +33%
    "abyss_vanguard": 1000,  # was 742, +35%
    "fallen_sanctum": 1100,  # was 721, +53%
    "doom_herald": 1250,     # was 946, +32%
    "abyss_lord": 1486,      # keep - already fixed
}

print("=== Preset Power Targets ===")
for p in bp:
    pid = p["id"]
    cur = preset_power(p)
    tgt = targets.get(pid, cur)
    print(f"  {pid:20s}: {cur:5.0f} → {tgt:5.0f}  (+{tgt-cur:4.0f}, {tgt/cur*100-100:+.0f}%)")

# For each preset that needs boosting, show detail and suggest new counts/stars
print("\n=== Boost Suggestions ===")
for p in bp:
    pid = p["id"]
    cur = preset_power(p)
    tgt = targets.get(pid, cur)
    if cur >= tgt:
        continue
    ratio = tgt / cur
    print(f"\n{pid} ({p['name']}): {cur:.0f} → {tgt:.0f} (×{ratio:.2f})")
    for u in p["units"]:
        uid = u["unitId"]
        cnt = u["count"]
        star = u["star"]
        pwr = unit_power(uid, cnt, star)
        # suggest: multiply count by ratio
        new_cnt = max(1, round(cnt * ratio))
        # or boost star
        new_star = min(6, max(star, round(star + (ratio - 1) * 2)))
        pwr_new = unit_power(uid, new_cnt, star)
        pwr_new_s = unit_power(uid, cnt, new_star)
        print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:.0f}")
        print(f"    → count ×{ratio:.1f}: ×{new_cnt} ★{star} → {pwr_new:.0f}")
        print(f"    → star boost: ×{cnt} ★{new_star} → {pwr_new_s:.0f}")