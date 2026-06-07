# -*- coding: utf-8 -*-
"""直接用 os.system 调用 lark-cli 批量填写飞书表格"""
import os
import json

SHEETS = {
    "world_map_layers": ("https://ifosuw0aw4.feishu.cn/sheets/EkuIsoXkBhdUZwtls8FcXGFdnvO", "ad55f2"),
    "world_map_nodes": ("https://ifosuw0aw4.feishu.cn/sheets/Xq3wsxwVrhA3Z3tPdTrcoOOpn9b", None),
    "world_map_connections": ("https://ifosuw0aw4.feishu.cn/sheets/THE3szt6HhHgIrtQy7acEUJRnEL", None),
    "enemy_presets": ("https://ifosuw0aw4.feishu.cn/sheets/Ac6es3SdthlNJ1twpidcXVZGnOc", None),
    "enemy_preset_units": ("https://ifosuw0aw4.feishu.cn/sheets/Hv3ysSdjkhRKsLt0zecc1MA9nnS", None),
    "run_phase_states": ("https://ifosuw0aw4.feishu.cn/sheets/PpG3sDuBmh2b8ZtvS93cCgWsnCe", None),
    "run_trigger_timing": ("https://ifosuw0aw4.feishu.cn/sheets/BApfsuaSfhO5fbtHqRUc7LQmnug", None),
}

CSV_DIR = "docs/markdown/config_tables"

def fill_sheet(name, url, sheet_id):
    csv_path = os.path.join(CSV_DIR, f"{name}.csv")
    if not os.path.exists(csv_path):
        print(f"  [{name}] CSV not found: {csv_path}")
        return False
    
    with open(csv_path, "r", encoding="utf-8-sig") as f:
        lines = [line.strip() for line in f.readlines() if line.strip()]
    
    if not lines:
        print(f"  [{name}] CSV is empty")
        return False
    
    n = len(lines)
    print(f"  [{name}] {n} rows → filling...")
    
    # Write header + data rows using +cells-set for large datasets
    # For simplicity, use csv-put with file redirect since direct --csv is limited
    # Strategy: write CSV to temp file, use lark-cli with it
    os.system(f'lark-cli sheets +csv-put --as user --url "{url}" --sheet-id {sheet_id} --start-cell A1 --csv "{chr(10).join(lines)}"')
    return True

# First get sheet_ids for sheets where we don't know them
import subprocess

for name, (url, sheet_id) in SHEETS.items():
    if sheet_id is None:
        result = subprocess.run(
            f'lark-cli sheets +workbook-info --as user --url "{url}" --format json',
            shell=True, capture_output=True, text=True
        )
        if result.returncode == 0:
            data = json.loads(result.stdout)
            sheets = data.get("data", {}).get("sheets", [])
            if sheets:
                sheet_id = sheets[0]["sheet_id"]
                SHEETS[name] = (url, sheet_id)
                print(f"  [{name}] sheet_id={sheet_id}")
            else:
                print(f"  [{name}] no sheets found")
                continue
        else:
            print(f"  [{name}] workbook-info failed: {result.stderr[:200]}")
            continue
    
    fill_sheet(name, url, sheet_id)

print("\nDone!")