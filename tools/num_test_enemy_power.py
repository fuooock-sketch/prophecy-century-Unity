# -*- coding: utf-8 -*-
"""Extract actual enemy combat power for numerical testing."""
import json

with open("Assets/Resources/Data/world_maps.json", "r", encoding="utf-8") as f:
    maps = json.load(f)
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = json.load(f)
with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = json.load(f)

uid_map = {u["id"]: u for u in ud}
ep_map = {e["id"]: e for e in bp}


def unit_power(uid, count, star):
    u = uid_map.get(uid, {})
    hp = u.get("hpPerUnit", u.get("hp", 1))
    atk = u.get("attack", 1)
    df = u.get("defense", 0)
    return count * (hp + atk + df) / 3 * (1 + (star - 1) * 0.3)


def enemy_power(preset):
    total = 0
    for u in preset.get("units", []):
        total += unit_power(u["unitId"], u["count"], u["star"])
    return total


m = next(m for m in maps if m["id"] == "abyss_wilds")
nodes = sorted(m["nodes"], key=lambda n: (n["layer"], n["id"]))

print("Layer|NodeID|NodeName|Type|EnemyPresetID|PresetName|Gold|EnemyPower|UnitSlots")
print("-" * 100)
for n in nodes:
    if n["type"] not in ("battle", "boss"):
        continue
    eid = n.get("enemyPresetId", "")
    preset = ep_map.get(eid, {})
    pwr = enemy_power(preset)
    pname = preset.get("name", "")
    units_count = len(preset.get("units", []))
    gold = n.get("reward", {}).get("gold", 0)
    print(f"{n['layer']:2d} | {n['id']:30s} | {n['name']:12s} | {n['type']:7s} | {eid:20s} | {pname:10s} | {gold:4d} | {pwr:10.0f} | {units_count}")

# Summary: power by layer (unique presets)
print()
print("=" * 80)
print("Actual Enemy Power by Preset (may repeat across layers)")
print("=" * 80)
seen = set()
for e in bp:
    eid = e["id"]
    if eid in seen:
        continue
    seen.add(eid)
    pwr = enemy_power(e)
    max_unit = max(e["units"], key=lambda u: u["count"])
    max_star = max(u["star"] for u in e["units"])
    print(f"  {eid:20s} | {e['name']:10s} | type={e['type']:7s} | power={pwr:8.0f} | max*={max_star} | max_count={max_unit['count']} ({uid_map.get(max_unit['unitId'],{}).get('name','')})")

# Check: are any presets used at multiple layers with same power?
print()
print("=" * 80)
print("Layer-by-Layer Enemy Power Progression")
print("=" * 80)
for n in nodes:
    if n["type"] not in ("battle", "boss"):
        continue
    eid = n.get("enemyPresetId", "")
    preset = ep_map.get(eid, {})
    pwr = enemy_power(preset)
    print(f"  L{n['layer']:2d} {n['name']:15s} → {preset.get('name',''):12s} Power={pwr:.0f}")