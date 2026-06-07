# -*- coding: utf-8 -*-
"""探索 unit_data.json 结构"""
import json

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8-sig") as f:
    data = json.load(f)

print(f"Total units: {len(data)}")
print()

# 第一个单位的完整结构
u0 = data[0]
print("=== First unit keys ===")
for k, v in u0.items():
    print(f"  {k}: {type(v).__name__} = {v}")
print()

# 抽查几个单位
for i in [0, 1, 2, 5, 10, 20, 30, 40, 50, 60]:
    u = data[i]
    name = u.get("name", "?")
    sid = u.get("id", "?")
    star = u.get("star", "?")
    talents = u.get("talents", [])
    gold_talents = u.get("goldTalents", [])
    battle_skills = u.get("battleSkills", [])
    gold_battle_skills = u.get("goldBattleSkills", [])
    print(f"[{i}] {name} (id={sid}, star={star})")
    print(f"    talentText:     {u.get('talentText','')[:80]}")
    print(f"    goldTalentText: {u.get('goldTalentText','')[:80]}")
    print(f"    battleText:     {u.get('battleText','')[:80]}")
    print(f"    goldBattleText: {u.get('goldBattleText','')[:80]}")
    print(f"    talents: {len(talents)} skills")
    for t in talents:
        print(f"      kind={t.get('kind')}, value={t.get('value')}, threshold={t.get('threshold')}, race={t.get('race')}, faith={t.get('faith')}, tag={t.get('tag')}")
    if gold_talents:
        print(f"    goldTalents: {len(gold_talents)} skills")
        for t in gold_talents:
            print(f"      kind={t.get('kind')}, value={t.get('value')}, threshold={t.get('threshold')}")
    if battle_skills:
        print(f"    battleSkills: {len(battle_skills)} skills")
        for t in battle_skills:
            print(f"      kind={t.get('kind')}, value={t.get('value')}")
    if gold_battle_skills:
        print(f"    goldBattleSkills: {len(gold_battle_skills)} skills")
        for t in gold_battle_skills:
            print(f"      kind={t.get('kind')}, value={t.get('value')}")
    print()

# 统计所有 kind 类型
all_kinds = set()
for u in data:
    for sk in u.get("talents", []) + u.get("goldTalents", []) + u.get("battleSkills", []) + u.get("goldBattleSkills", []):
        k = sk.get("kind", "")
        if k:
            all_kinds.add(k)

print(f"=== All unique skill kinds ({len(all_kinds)}) ===")
for k in sorted(all_kinds):
    print(f"  {k}")