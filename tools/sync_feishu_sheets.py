# -*- coding: utf-8 -*-
"""同步本地 CSV 到飞书在线表格"""
import os

SHEETS = {
    "world_map_connections": ("https://ifosuw0aw4.feishu.cn/sheets/THE3szt6HhHgIrtQy7acEUJRnEL", "1df93d"),
    "enemy_preset_units": ("https://ifosuw0aw4.feishu.cn/sheets/Hv3ysSdjkhRKsLt0zecc1MA9nnS", "3a52e9"),
    "enemy_presets": ("https://ifosuw0aw4.feishu.cn/sheets/Ac6es3SdthlNJ1twpidcXVZGnOc", "abb227"),
}

CSV_DIR = "docs/markdown/config_tables"

def sync_sheet(name, url, sheet_id, chunk_size=45):
    csv_path = os.path.join(CSV_DIR, f"{name}.csv")
    if not os.path.exists(csv_path):
        print(f"  SKIP: {csv_path} not found")
        return
    
    with open(csv_path, "r", encoding="utf-8-sig") as f:
        lines = [l.strip() for l in f.readlines() if l.strip()]
    
    n = len(lines)
    print(f"  [{name}] {n} rows, uploading in chunks of {chunk_size}...")
    
    chunks = [lines[i:i+chunk_size] for i in range(0, n, chunk_size)]
    
    for ci, chunk in enumerate(chunks):
        csv_text = "\n".join(chunk)
        start_cell = "A1" if ci == 0 else f"A{ci * chunk_size + 1}"
        
        cmd = f'lark-cli sheets +csv-put --as user --url "{url}" --sheet-id {sheet_id} --start-cell {start_cell} --csv "{csv_text}"'
        rc = os.system(cmd)
        if rc != 0:
            print(f"    Chunk {ci+1}/{len(chunks)} FAILED (rc={rc})")
            return
        print(f"    Chunk {ci+1}/{len(chunks)} OK ({start_cell})")
    
    print(f"  ✅ Done")

print("=" * 60)
print("  同步本地 CSV → 飞书在线表格")
print("=" * 60)

for name, (url, sid) in SHEETS.items():
    print()
    sync_sheet(name, url, sid)

print()
print("=" * 60)
print("  同步完成")
print("=" * 60)