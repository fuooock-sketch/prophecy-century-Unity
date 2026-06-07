# -*- coding: utf-8 -*-
import json

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8-sig") as f:
    data = json.load(f)

targets = ["卫戍协兵", "莱特使者", "莱特的回响", "猎豹", "血淤魔"]
for u in data:
    n = u.get("name", "")
    if n in targets:
        print(f"=== {n} (id={u.get('id')}) ===")
        for k in ["talentText", "goldTalentText", "battleText", "goldBattleText"]:
            t = u.get(k, "")
            if t and t != "\u2014":
                print(f"  {k}: {t[:150]}")
        for k in ["talents", "goldTalents", "battleSkills", "goldBattleSkills"]:
            arr = u.get(k, [])
            print(f"  {k}: {len(arr)} skills")
            for s in arr:
                print(f"    kind={s.get('kind')}, value={s.get('value')}, threshold={s.get('threshold')}")
        print()

# Also dump the 11 "only json no csharp" kinds with their users
print("=" * 60)
print("11 kinds only in JSON, check C# implementation:")
kinds = [
    "battle_aura_sync_unit_id_attack_to_highest",
    "battle_periodic_temp_power",
    "first_hits_counterattack",
    "on_ally_death_tagged_units_temp_power",
    "on_attack_chance_force_crit",
    "on_damaged_count_temp_morale",
    "on_damaged_survive_next_round_forest_gem",
    "on_extra_attack_once_next_round_gold",
    "on_sell_price_if_attack_threshold",
    "passive_every_nth_attack_force_crit",
    "same_row_units_count_as_race",
]
for k in kinds:
    users = []
    for u in data:
        for arr_name in ["talents", "goldTalents", "battleSkills", "goldBattleSkills"]:
            arr = u.get(arr_name, []) or []
            if any(s.get("kind") == k for s in arr):
                users.append(u.get("name", "?"))
                break
    print(f"  {k} -> {users}")