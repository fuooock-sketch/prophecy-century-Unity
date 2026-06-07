# -*- coding: utf-8 -*-
"""Sync CSV config tables from Excel source of truth."""
import openpyxl
import csv
import os

EXCEL_DIR = "docs/excel"
CSV_DIR = "docs/markdown/config_tables"

def safe_str(v, default=""):
    if v is None:
        return default
    return str(v).strip()

def read_xlsx(filename):
    wb = openpyxl.load_workbook(os.path.join(EXCEL_DIR, filename), data_only=True, read_only=True)
    ws = wb.active
    rows = list(ws.iter_rows(values_only=True))
    return rows

def write_csv(filename, headers, rows):
    path = os.path.join(CSV_DIR, filename)
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(headers)
        for r in rows:
            writer.writerow([safe_str(c) for c in r])
    print(f"  Wrote {len(rows)} rows → {filename}")

# ===== 1. Enemy Presets =====
print("=== Syncing enemy_presets.csv ===")
rows = read_xlsx("怪物配置_敌人预设表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    pid = safe_str(r[0])
    if not pid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("enemy_presets.csv", headers, data)

# ===== 2. Enemy Preset Units =====
print("=== Syncing enemy_preset_units.csv ===")
rows = read_xlsx("怪物配置_敌方单位明细表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    pid = safe_str(r[0])
    if not pid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("enemy_preset_units.csv", headers, data)

# ===== 3. World Maps =====
print("=== Syncing world_maps.csv ===")
rows = read_xlsx("地图配置_地图定义表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    mid = safe_str(r[0])
    if not mid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("world_maps.csv", headers, data)

# ===== 4. World Map Layers =====
print("=== Syncing world_map_layers.csv ===")
rows = read_xlsx("地图配置_层级表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    mid = safe_str(r[0])
    if not mid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("world_map_layers.csv", headers, data)

# ===== 5. World Map Nodes =====
print("=== Syncing world_map_nodes.csv ===")
rows = read_xlsx("地图配置_节点表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    mid = safe_str(r[0])
    if not mid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("world_map_nodes.csv", headers, data)

# ===== 6. World Map Connections =====
print("=== Syncing world_map_connections.csv ===")
rows = read_xlsx("地图配置_连接表.xlsx")
headers = [safe_str(h) for h in rows[0]]
data = []
for r in rows[1:]:
    mid = safe_str(r[0])
    if not mid:
        continue
    data.append([safe_str(r[i]) if i < len(r) else "" for i in range(len(headers))])
write_csv("world_map_connections.csv", headers, data)

print("\n✅ All CSV config tables synced from Excel!")