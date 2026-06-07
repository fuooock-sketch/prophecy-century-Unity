# -*- coding: utf-8 -*-
"""Recalculate staircase values - moderate boost (~half of previous)."""
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


def battle_threshold(enemy_power):
    """Minimum player power needed to win."""
    return enemy_power * (1.0 + max(0, (enemy_power - 500) / 2000))


print("=== Verification of V2 values ===")

v2 = {
    "shadow_raiders": [
        ("assassin", 16, 3),
        ("wanderer", 13, 3),
        ("blacksmith", 15, 3),
        ("light_mentor", 7, 4),
    ],
    "garrison_line": [
        ("garrison_guard", 6, 5),
        ("priest", 11, 3),
        ("light_envoy", 6, 5),
        ("wanderer", 14, 3),
    ],
    "abyss_vanguard": [
        ("martial_master", 9, 5),
        ("light_mentor", 8, 5),
        ("assassin", 16, 4),
        ("garrison_guard", 6, 6),
    ],
    "fallen_sanctum": [
        ("light_envoy", 6, 5),
        ("garrison_guard", 7, 6),
        ("light_mentor", 9, 5),
        ("royal_swordsman", 4, 6),
    ],
    "doom_herald": [
        ("royal_swordsman", 7, 6),
        ("echo_of_light", 4, 6),
        ("martial_master", 9, 5),
        ("garrison_guard", 7, 6),
    ],
}

for pid, units in v2.items():
    total = sum(unit_power(uid, cnt, star) for uid, cnt, star in units)
    thr = battle_threshold(total)
    print(f"\n{pid}: Power={total:.0f}  Threshold={thr:.0f}")
    for uid, cnt, star in units:
        p = unit_power(uid, cnt, star)
        print(f"  {uid:20s} ×{cnt:2d} ★{star} → {p:.0f}")

# Show player power at key layers for context
print("\n=== Player power reference ===")
player_at_layer = {
    4: 1000, 5: 1100, 6: 1200, 7: 1350, 8: 1500, 9: 1400,
    10: 1480, 11: 1420, 12: 1525, 13: 1630, 14: 1740,
}
for l, pp in player_at_layer.items():
    print(f"  L{l}: ~{pp}")

# Check which presets are used at which layers
print("\n=== Enemy layer assignment check ===")
preset_layers = {
    "shadow_raiders": [4, 5, 6],
    "garrison_line": [5, 6, 7, 8],
    "abyss_vanguard": [7, 8, 9],
    "fallen_sanctum": [9, 10, 11, 12],
    "doom_herald": [11, 12, 13, 14],
}
for pid, layers in preset_layers.items():
    pwr = sum(unit_power(uid, cnt, star) for uid, cnt, star in v2[pid])
    thr = battle_threshold(pwr)
    min_layer = min(layers)
    pp_min = player_at_layer.get(min_layer, 0)
    max_layer = max(layers)
    pp_max = player_at_layer.get(max_layer, 0)
    margin_min = pp_min - thr
    margin_max = pp_max - thr
    print(f"  {pid:20s}: Power={pwr:.0f} Thr={thr:.0f}  L{min_layer} margin={margin_min:+.0f}  L{max_layer} margin={margin_max:+.0f}")
