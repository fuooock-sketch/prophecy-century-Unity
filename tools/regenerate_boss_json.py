import json, csv

units = {}
with open('docs/markdown/config_tables/enemy_preset_units.csv', 'r', encoding='utf-8-sig') as f:
    for row in csv.DictReader(f):
        pid = row['enemy_preset_id'].strip()
        if pid not in units: units[pid] = []
        units[pid].append({'slotId': row['slot_id'].strip(), 'unitId': row['unit_id'].strip(), 'count': int(row['count']), 'star': int(row['star'])})

presets = {}
with open('docs/markdown/config_tables/enemy_presets.csv', 'r', encoding='utf-8-sig') as f:
    for row in csv.DictReader(f):
        pid = row['enemy_preset_id'].strip()
        presets[pid] = {'id': pid, 'name': row['name'].strip(), 'type': row['type'].strip(), 'units': units.get(pid, [])}

result = list(presets.values())
with open('Assets/Resources/Data/boss_enemies.json', 'w', encoding='utf-8') as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

for p in result:
    maxc = max(u['count'] for u in p['units'])
    total = sum(u['count'] for u in p['units'])
    print(f"{p['id']}: max_count={maxc}, total_units={total}")
print("Done!")