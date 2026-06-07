# -*- coding: utf-8 -*-
"""Calculate power of abyss_lord units and test alternatives."""
import json

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = json.load(f)
um = {u["id"]: u for u in ud}


def unit_power(uid, count, star):
    u = um.get(uid, {})
    hp = u.get("hpPerUnit", u.get("hp", 1))
    atk = u.get("attack", 1)
    df = u.get("defense", 0)
    return count * (hp + atk + df) / 3 * (1 + (star - 1) * 0.3)


# Current abyss_lord
current = [
    ("echo_of_light", 4, 6),
    ("royal_swordsman", 6, 6),
    ("light_mentor", 8, 4),
    ("assassin", 16, 3),
]
total = 0
print("=== Current abyss_lord (901 total) ===")
for uid, cnt, star in current:
    pwr = unit_power(uid, cnt, star)
    total += pwr
    u = um[uid]
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}  (hp={u.get('hpPerUnit',u.get('hp',1))} atk={u['attack']} def={u.get('defense',0)})")
print(f"  TOTAL: {total:.0f}")

# Check doom_herald for reference
doom = [
    ("royal_swordsman", 6, 6),
    ("echo_of_light", 3, 6),
    ("martial_master", 8, 5),
    ("garrison_guard", 6, 5),
]
print("\n=== doom_herald (946 total) for reference ===")
for uid, cnt, star in doom:
    pwr = unit_power(uid, cnt, star)
    u = um[uid]
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}")

# --- OPTION A: Boost existing units ---
print("\n=== OPTION A: Boost counts/stars on existing 4 slots ===")
# Replace assassin×16★3 with martial_master×10★5 or garrison_guard×8★5
# Keep echo_of_light×4★6, royal_swordsman×6★6, light_mentor×8★4
option_a = [
    ("echo_of_light", 5, 6),      # +1 count
    ("royal_swordsman", 8, 6),    # +2 count
    ("martial_master", 10, 5),    # replace assassin
    ("garrison_guard", 8, 5),     # replace light_mentor, or keep
]
# No, need to keep light_mentor for flavor. Let me think...
# Keep echo_of_light + royal_swordsman + light_mentor + replace assassin with stronger unit
option_a2 = [
    ("echo_of_light", 5, 6),
    ("royal_swordsman", 8, 6),
    ("light_mentor", 10, 5),      # ★4→★5, 8→10
    ("martial_master", 10, 5),    # replace assassin
]
total_a = sum(unit_power(uid, cnt, star) for uid, cnt, star in option_a2)
for uid, cnt, star in option_a2:
    pwr = unit_power(uid, cnt, star)
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}")
print(f"  TOTAL: {total_a:.0f}")

# --- OPTION B: 5-slot Boss ---
print("\n=== OPTION B: 5-slot Boss (keep assassin for flavor) ===")
option_b = [
    ("echo_of_light", 5, 6),
    ("royal_swordsman", 7, 6),
    ("light_mentor", 9, 5),
    ("garrison_guard", 6, 5),
    ("assassin", 14, 4),           # ★3→★4 for some boost
]
total_b = sum(unit_power(uid, cnt, star) for uid, cnt, star in option_b)
for uid, cnt, star in option_b:
    pwr = unit_power(uid, cnt, star)
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}")
print(f"  TOTAL: {total_b:.0f}")

# --- OPTION C: Aggressive 4-slot redesign ---
print("\n=== OPTION C: 4-slot aggressive Boss ===")
option_c = [
    ("echo_of_light", 6, 6),
    ("royal_swordsman", 9, 6),
    ("martial_master", 12, 5),
    ("garrison_guard", 9, 5),
]
total_c = sum(unit_power(uid, cnt, star) for uid, cnt, star in option_c)
for uid, cnt, star in option_c:
    pwr = unit_power(uid, cnt, star)
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}")
print(f"  TOTAL: {total_c:.0f}")

# --- OPTION D: Keep flavor, sensible boost ---
print("\n=== OPTION D: Keep flavor units, boost stars/counts ===")
option_d = [
    ("echo_of_light", 5, 6),       # Boss核心 +1
    ("royal_swordsman", 8, 6),     # 副输出 +3
    ("light_mentor", 10, 5),       # 战场填充 ★4→★5, +2
    ("assassin", 14, 4),           # 暗刺 ★3→★4, -2 count (star boost compensates)
]
total_d = sum(unit_power(uid, cnt, star) for uid, cnt, star in option_d)
for uid, cnt, star in option_d:
    pwr = unit_power(uid, cnt, star)
    print(f"  {uid:20s} ×{cnt:2d} ★{star} → {pwr:6.0f}")
print(f"  TOTAL: {total_d:.0f}")
print(f"  Ratio vs original: {total_d/901:.2f}x")

# Also check: what power does doom_herald have at L14 vs player?
print(f"\n  doom_herald power: {sum(unit_power(uid,cnt,star) for uid,cnt,star in doom):.0f}")
print(f"  Target for Boss: ~{sum(unit_power(uid,cnt,star) for uid,cnt,star in doom) * 1.6:.0f} (doom_herald × 1.6)")