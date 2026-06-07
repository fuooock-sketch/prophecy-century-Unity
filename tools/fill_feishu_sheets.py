# -*- coding: utf-8 -*-
"""将本地 CSV 数据批量写入飞书在线表格"""
import subprocess
import os
import sys

# 8张表格的URL映射
SHEET_URLS = {
    "world_maps": "https://ifosuw0aw4.feishu.cn/sheets/ESdNsWHuDhXpKEt4UOJcMBrKnHe",
    "world_map_layers": "https://ifosuw0aw4.feishu.cn/sheets/EkuIsoXkBhdUZwtls8FcXGFdnvO",
    "world_map_nodes": "https://ifosuw0aw4.feishu.cn/sheets/Xq3wsxwVrhA3Z3tPdTrcoOOpn9b",
    "world_map_connections": "https://ifosuw0aw4.feishu.cn/sheets/THE3szt6HhHgIrtQy7acEUJRnEL",
    "enemy_presets": "https://ifosuw0aw4.feishu.cn/sheets/Ac6es3SdthlNJ1twpidcXVZGnOc",
    "enemy_preset_units": "https://ifosuw0aw4.feishu.cn/sheets/Hv3ysSdjkhRKsLt0zecc1MA9nnS",
    "run_phase_states": "https://ifosuw0aw4.feishu.cn/sheets/PpG3sDuBmh2b8ZtvS93cCgWsnCe",
    "run_trigger_timing": "https://ifosuw0aw4.feishu.cn/sheets/BApfsuaSfhO5fbtHqRUc7LQmnug",
}

CSV_DIR = "docs/markdown/config_tables"

def run_cmd(cmd):
    """运行命令"""
    result = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8')
    return result.stdout, result.stderr, result.returncode

def get_sheet_id(sheet_url):
    """获取表格的sheet_id"""
    stdout, stderr, rc = run_cmd([
        "lark-cli", "sheets", "+workbook-info", "--as", "user",
        "--url", sheet_url, "--format", "json"
    ])
    if rc != 0:
        print(f"  ⚠ 获取sheet_id失败: {stderr[:200]}")
        return None
    import json
    try:
        data = json.loads(stdout)
        sheets = data.get("data", {}).get("sheets", [])
        if sheets:
            return sheets[0].get("sheet_id")
    except:
        pass
    return None

def put_csv(sheet_url, sheet_id, csv_filename):
    """将CSV文件内容写入飞书表格"""
    csv_path = os.path.join(CSV_DIR, csv_filename)
    if not os.path.exists(csv_path):
        print(f"  ⚠ CSV文件不存在: {csv_path}")
        return False
    
    with open(csv_path, "r", encoding="utf-8-sig") as f:
        csv_content = f.read()
    
    cmd = [
        "lark-cli", "sheets", "+csv-put", "--as", "user",
        "--url", sheet_url,
        "--sheet-id", sheet_id,
        "--start-cell", "A1",
        "--csv", csv_content
    ]
    
    stdout, stderr, rc = run_cmd(cmd)
    if rc != 0:
        print(f"  ⚠ 写入失败: {stderr[:300]}")
        # 尝试用写入权限
        if "missing_scope" in stderr or "authorization" in stderr:
            print(f"  → 可能需要写入授权")
        return False
    
    print(f"  ✓ 写入成功")
    return True

def main():
    print("=" * 60)
    print("  飞书在线表格批量填充")
    print("=" * 60)
    
    for sheet_name, sheet_url in SHEET_URLS.items():
        print(f"\n[{sheet_name}]")
        sheet_id = get_sheet_id(sheet_url)
        if not sheet_id:
            print(f"  ✗ 跳过")
            continue
        
        csv_file = f"{sheet_name}.csv"
        put_csv(sheet_url, sheet_id, csv_file)

if __name__ == "__main__":
    main()