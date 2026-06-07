# -*- coding: utf-8 -*-
"""恶兆荒野 专用QA检查脚本 — L1静态数据+L2拓扑+L3引用完整性"""
import json
import os
import sys

# Fix Windows console encoding
sys.stdout.reconfigure(encoding='utf-8')

print("=" * 70)
print("  恶兆荒野 (abyss_wilds) QA 完整性检查")
print("=" * 70)

# Load data
with open("Assets/Resources/Data/world_maps.json", "r", encoding="utf-8") as f:
    maps = json.load(f)
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    enemies = json.load(f)
with open("Assets/Resources/Data/treasures.json", "r", encoding="utf-8") as f:
    treasures = json.load(f)
with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    units = json.load(f)

unit_ids = {u["id"] for u in units}
enemy_preset_ids = {e["id"] for e in enemies}
treasure_ids = {t["id"] for t in treasures}

map_data = next((m for m in maps if m["id"] == "abyss_wilds"), None)
if not map_data:
    print("ERROR: abyss_wilds map not found!")
    sys.exit(1)

nodes = map_data.get("nodes", [])
connections = map_data.get("connections", [])
layers = map_data.get("layers", [])
start_id = map_data.get("startNodeId", "")

node_ids = {n["id"] for n in nodes}
node_by_id = {n["id"]: n for n in nodes}

errors = []
warnings = []

# ===== L1: Static Data Integrity =====
print("\n--- L1: Static Data Integrity ---")

# 1.1 Duplicate node IDs
if len(node_ids) != len(nodes):
    errors.append(f"L1.1: Duplicate node IDs! {len(nodes)} nodes but {len(node_ids)} unique IDs")

# 1.2 Node ID format
for n in nodes:
    if not n["id"].startswith("abyss_"):
        warnings.append(f"L1.2: Node '{n['id']}' does not start with 'abyss_'")

# 1.3 x/y in [0,1]
for n in nodes:
    x, y = n.get("x", 0), n.get("y", 0)
    if x < 0 or x > 1 or y < 0 or y > 1:
        errors.append(f"L1.3: Node '{n['id']}' has out-of-range x={x}, y={y}")

# 1.4 All battle/boss nodes have enemyPresetId
for n in nodes:
    if n["type"] in ("battle", "boss"):
        eid = n.get("enemyPresetId", "")
        if not eid:
            errors.append(f"L1.4: {n['type']} node '{n['id']}' has no enemyPresetId")
        elif eid not in enemy_preset_ids:
            errors.append(f"L1.4: enemyPresetId '{eid}' (node '{n['id']}') not found in boss_enemies.json")

# 1.5 All treasureId in rewards exist
for n in nodes:
    tid = n.get("reward", {}).get("treasureId", "")
    if tid and tid not in treasure_ids:
        errors.append(f"L1.5: treasureId '{tid}' (node '{n['id']}') not found in treasures.json")

# 1.6 All unitId in enemy presets exist
for e in enemies:
    for u in e.get("units", []):
        uid = u.get("unitId", "")
        if uid and uid not in unit_ids:
            errors.append(f"L1.6: unitId '{uid}' (preset '{e['id']}') not found in unit_data.json")

# 1.7 Layer indices are consecutive
layer_indices = sorted([l["index"] for l in layers])
expected = list(range(len(layers)))
if layer_indices != expected:
    errors.append(f"L1.7: Layer indices not consecutive: {layer_indices} vs expected {expected}")

# 1.8 reward_gold >= 0 (treasure nodes may not have gold key)
for n in nodes:
    reward = n.get("reward", {})
    if "gold" in reward:
        gold = reward["gold"]
        if gold < 0:
            errors.append(f"L1.8: Node '{n['id']}' has negative gold={gold}")

# ===== L2: Topology =====
print("\n--- L2: Topology ---")

# 2.1 startNodeId exists
if start_id not in node_ids:
    errors.append(f"L2.1: startNodeId '{start_id}' not in nodes")
else:
    print(f"  Start node: {start_id} ({node_by_id[start_id]['name']})")

# 2.2 All connection endpoints exist
for c in connections:
    for key, direction in [("fromNodeId", "from"), ("toNodeId", "to")]:
        nid = c.get(key, "")
        if nid not in node_ids:
            errors.append(f"L2.2: Connection {direction} node '{nid}' not found")
        elif c.get("fromNodeId", "") in node_ids and c.get("toNodeId", "") in node_ids:
            fn = node_by_id[c["fromNodeId"]]
            tn = node_by_id[c["toNodeId"]]
            if tn["layer"] != fn["layer"] + 1:
                errors.append(f"L2.3: Layer jump! {c['fromNodeId']}(L{fn['layer']}) -> {c['toNodeId']}(L{tn['layer']})")

# 2.4 Boss reachable from start (BFS)
from collections import deque
reachable = set()
q = deque([start_id])
while q:
    cur = q.popleft()
    if cur in reachable:
        continue
    reachable.add(cur)
    for c in connections:
        if c["fromNodeId"] == cur and c["toNodeId"] not in reachable:
            q.append(c["toNodeId"])

boss_nodes = [n for n in nodes if n["type"] == "boss"]
if boss_nodes and boss_nodes[0]["id"] not in reachable:
    errors.append(f"L2.4: Boss '{boss_nodes[0]['id']}' NOT reachable from start '{start_id}'")
else:
    # Find shortest path
    def find_path(start, target):
        visited = {start: [start]}
        q2 = deque([start])
        while q2:
            cur = q2.popleft()
            if cur == target:
                return visited[cur]
            for c in connections:
                if c["fromNodeId"] == cur and c["toNodeId"] not in visited:
                    visited[c["toNodeId"]] = visited[cur] + [c["toNodeId"]]
                    q2.append(c["toNodeId"])
        return None
    
    path = find_path(start_id, boss_nodes[0]["id"]) if boss_nodes else None
    if path:
        print(f"  Boss reachable: {len(path)-1} moves, {len(path)} nodes")
    else:
        errors.append("L2.4: No path found to boss")

# 2.5 No isolated nodes (except start may have no inbound)
has_inbound = set()
for c in connections:
    has_inbound.add(c["toNodeId"])
isolated = [n["id"] for n in nodes if n["id"] not in has_inbound and n["id"] != start_id]
if isolated:
    errors.append(f"L2.5: {len(isolated)} isolated nodes (no inbound): {isolated[:5]}...")

# ===== L3: Reference Integrity =====
print("\n--- L3: Reference Integrity ---")

# 3.1 node count by type
type_counts = {}
for n in nodes:
    t = n["type"]
    type_counts[t] = type_counts.get(t, 0) + 1
print(f"  Node types: {dict(sorted(type_counts.items()))}")

# 3.2 connection count
print(f"  Connections: {len(connections)}")
print(f"  Layers: {len(layers)}")

# 3.3 Verify all 8 enemy presets are used
used_presets = set()
for n in nodes:
    eid = n.get("enemyPresetId", "")
    if eid:
        used_presets.add(eid)
unused_presets = enemy_preset_ids - used_presets
if unused_presets:
    warnings.append(f"L3.3: {len(unused_presets)} unused enemy presets: {unused_presets}")
print(f"  Enemy presets used: {len(used_presets)}/{len(enemy_preset_ids)}")

# 3.4 Verify treasure usage
used_treasures = set()
for n in nodes:
    tid = n.get("reward", {}).get("treasureId", "")
    if tid:
        used_treasures.add(tid)
print(f"  Treasure IDs used: {len(used_treasures)}/{len(treasure_ids)}")

# 3.5 Verify gold distribution
gold_by_layer = {}
for n in nodes:
    l = n["layer"]
    g = n.get("reward", {}).get("gold", 0)
    if l not in gold_by_layer:
        gold_by_layer[l] = []
    gold_by_layer[l].append(g)
print(f"  Gold by layer (avg):")
for l in sorted(gold_by_layer):
    vals = gold_by_layer[l]
    print(f"    L{l}: min={min(vals)} max={max(vals)} avg={sum(vals)/len(vals):.1f}")

# ===== Summary =====
print("\n" + "=" * 70)
print("  QA SUMMARY")
print("=" * 70)

if errors:
    print(f"\n  ERRORS ({len(errors)}):")
    for e in errors:
        print(f"    [ERROR] {e}")
else:
    print("\n  [PASS] All checks passed - 0 errors")

if warnings:
    print(f"\n  WARNINGS ({len(warnings)}):")
    for w in warnings:
        print(f"    [WARN] {w}")

status = "PASS" if not errors else f"FAIL ({len(errors)} errors)"
print(f"\n  Final Status: {status}")
print("=" * 70)