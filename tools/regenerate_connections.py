# -*- coding: utf-8 -*-
"""重新生成连接表 — 仿杀戮尖塔汇聚模式

核心规则：
1. L1-L2: 宽松教学，全互联
2. L3-L4: 汇聚，不管走哪条都必须遇到 battle
3. L5-L7: 展开期，多路径交叉
4. L8-L9: 第一汇聚点（中部检验）
5. L10-L12: 收束 + 第二汇聚点
6. L13-L14: 最终汇聚，只留战→战 或 战→宝 两条路到Boss
"""

# 从 world_map_nodes.csv 读取每层的节点
import csv

nodes_by_layer = {}
with open("docs/markdown/config_tables/world_map_nodes.csv", "r", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row["map_id"].strip() != "abyss_wilds":
            continue
        layer = int(row["layer"])
        if layer not in nodes_by_layer:
            nodes_by_layer[layer] = []
        nodes_by_layer[layer].append(row["node_id"].strip())

# 验证
for l in range(16):
    if l not in nodes_by_layer:
        print(f"⚠ Layer {l} has no nodes!")
    else:
        print(f"Layer {l}: {len(nodes_by_layer[l])} nodes")

def make_conn(from_list, to_list):
    """生成所有 from→to 的连接"""
    lines = []
    for fn in from_list:
        for tn in to_list:
            lines.append(f"abyss_wilds,{fn},{tn},Connection L{NODE_LAYER[fn]}->L{NODE_LAYER[tn]},true,")
    return lines

# Build node→layer lookup
NODE_LAYER = {}
for l, nodes in nodes_by_layer.items():
    for n in nodes:
        NODE_LAYER[n] = l

# ─── Build node type lookup ───
node_type = {}
with open("docs/markdown/config_tables/world_map_nodes.csv", "r", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row["map_id"].strip() == "abyss_wilds":
            node_type[row["node_id"].strip()] = row["type"].strip()

def make_safe_conn(from_list, to_list, same_type_ok=True):
    """生成连接。same_type_ok=False时，非战斗节点不能连到非战斗节点"""
    lines = []
    for fn in from_list:
        for tn in to_list:
            if not same_type_ok:
                # 如果from和to都是非战斗节点，跳过
                ft = node_type.get(fn, "")
                tt = node_type.get(tn, "")
                if ft not in ("battle", "boss", "start") and tt not in ("battle", "boss"):
                    continue
            lines.append(f"abyss_wilds,{fn},{tn},Connection,true,")
    return lines

# ─── 仿尖塔汇聚连接设计 ───
conns = []

# L0→L1: 起点发散 (1→2, safe_full)
conns += make_safe_conn(nodes_by_layer[0], nodes_by_layer[1])

# L1→L2: 教学期，全互联 (2→2)
conns += make_safe_conn(nodes_by_layer[1], nodes_by_layer[2])

# L2→L3: 汇聚，3个出口 (2→3)
conns += make_safe_conn(nodes_by_layer[2], nodes_by_layer[3])

# L3→L4: ★汇聚点★ (3→3)，非战斗不能连非战斗
conns += make_safe_conn(nodes_by_layer[3], nodes_by_layer[4], same_type_ok=False)

# L4→L5: 展开 (3→4)，非战斗不能连非战斗
conns += make_safe_conn(nodes_by_layer[4], nodes_by_layer[5], same_type_ok=False)

# L5→L6: 交叉路径 (4→4)
conns += make_safe_conn(nodes_by_layer[5], nodes_by_layer[6])

# L6→L7: 最宽处 (4→5)
conns += make_safe_conn(nodes_by_layer[6], nodes_by_layer[7])

# L7→L8: ★第二汇聚点★ (5→5)，非战斗不能连非战斗
conns += make_safe_conn(nodes_by_layer[7], nodes_by_layer[8], same_type_ok=False)

# L8→L9: 开始收束 (5→4)，非战斗只能连战斗
conns += make_safe_conn(nodes_by_layer[8], nodes_by_layer[9], same_type_ok=False)

# L9→L10: 继续收束 (4→4)，非战斗只能连战斗
conns += make_safe_conn(nodes_by_layer[9], nodes_by_layer[10], same_type_ok=False)

# L10→L11: ★第三汇聚点★ (4→3)，非战斗强制连战斗
conns += make_safe_conn(nodes_by_layer[10], nodes_by_layer[11], same_type_ok=False)

# L11→L12: 终局检验 (3→3)，全互联（因为L11没有resource）
conns += make_safe_conn(nodes_by_layer[11], nodes_by_layer[12])

# L12→L13: ★最终汇聚★ (3→2)，非战斗强制连战斗
conns += make_safe_conn(nodes_by_layer[12], nodes_by_layer[13], same_type_ok=False)

# L13→L14: Boss前 (2→2)，全互联
conns += make_safe_conn(nodes_by_layer[13], nodes_by_layer[14])

# L14→L15: 最终汇聚到Boss (2→1)
conns += make_safe_conn(nodes_by_layer[14], nodes_by_layer[15])

# ─── 输出 ───
header = "map_id,from_node_id,to_node_id,design_purpose,enabled,notes\n"
with open("docs/markdown/config_tables/world_map_connections.csv", "w", encoding="utf-8-sig") as f:
    f.write(header)
    for c in conns:
        f.write(c + "\n")

print(f"\nTotal connections: {len(conns)}")

# ─── 验证：是否存在纯非战斗路径？──
from collections import deque

# Build adjacency
adj = {}
for c in conns:
    parts = c.split(",")
    fn, tn = parts[1], parts[2]
    if fn not in adj:
        adj[fn] = []
    adj[fn].append(tn)

# Check if any path from start to boss is 100% non-battle
start = nodes_by_layer[0][0]
boss = nodes_by_layer[15][0]
battle_nodes = set()
for l, nodes in nodes_by_layer.items():
    # Read back from CSV to get type
    with open("docs/markdown/config_tables/world_map_nodes.csv", "r", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            if row["node_id"].strip() in nodes and row["type"].strip() in ("battle", "boss"):
                battle_nodes.add(row["node_id"].strip())

print(f"Battle nodes: {len(battle_nodes)}")

# BFS find shortest non-battle path
def find_safest_path(start, target, adj, battle_nodes):
    visited = set()
    q = deque([(start, [start], 0)])  # (node, path, battle_count)
    min_battles = 999
    min_path = None
    
    while q:
        node, path, bcount = q.popleft()
        if node == target:
            if bcount < min_battles:
                min_battles = bcount
                min_path = path
            continue
        if node in visited and bcount >= min_battles:
            continue
        visited.add(node)
        for nb in adj.get(node, []):
            nb_battle = 1 if nb in battle_nodes else 0
            q.append((nb, path + [nb], bcount + nb_battle))
    
    return min_path, min_battles

safest_path, min_battles = find_safest_path(start, boss, adj, battle_nodes)
if safest_path:
    print(f"Minimum battles to boss: {min_battles}")
    print(f"Safest path: {' -> '.join(safest_path[:5])}... -> {' -> '.join(safest_path[-3:])}")
    if min_battles <= 2:
        print("⚠ WARNING: Player can reach boss with <= 2 battles!")
    else:
        print("✅ Minimum battles >= 3 — acceptable")
else:
    print("⚠ No path found to boss!")