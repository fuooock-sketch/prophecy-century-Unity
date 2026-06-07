# -*- coding: utf-8 -*-
"""Calculate real in-game combat scores using the actual formula from BattleStubSystem."""
import json

with open("Assets/Resources/Data/unit_data.json", "r", encoding="utf-8") as f:
    ud = {u["id"]: u for u in json.load(f)}
with open("Assets/Resources/Data/boss_enemies.json", "r", encoding="utf-8") as f:
    bp = {e["id"]: e for e in json.load(f)}


def game_score(uid, count, star):
    """Real game score: Attack*1.85 + Defense*1.25 + Power*18 + MaxHp*0.58 + Speed*0.72 + Morale*9 + Luck*5"""
    u = ud[uid]
    atk = u["attack"]
    df = u.get("defense", 0)
    pwr = u.get("power", 1)
    hp = count * u.get("hpPerUnit", u.get("hp", 1))
    spd = u.get("speed", 0)
    mor = u.get("morale", 0)
    luk = u.get("luck", 0)
    return round(atk * 1.85 + df * 1.25 + pwr * 18 + hp * 0.58 + spd * 0.72 + mor * 9 + luk * 5)


print("=" * 90)
print("Real In-Game Combat Scores (Attack*1.85+Def*1.25+Power*18+MaxHp*0.58+Speed*0.72+Morale*9+Luck*5)")
print("=" * 90)

for eid, ep in bp.items():
    total = sum(game_score(u["unitId"], u["count"], u["star"]) for u in ep["units"])
    print(f"\n{eid:20s} | {ep['name']:10s} | type={ep['type']:7s} | SCORE={total}")
    for u in ep["units"]:
        s = game_score(u["unitId"], u["count"], u["star"])
        unit = ud[u["unitId"]]
        print(f"  {u['slotId']:10s} {u['unitId']:20s} x{u['count']:2d} *{u['star']} | atk={unit['attack']} def={unit.get('defense',0)} pwr={unit.get('power',1)} hp={u['count']}x{unit.get('hpPerUnit',unit.get('hp',1))} spd={unit.get('speed',0)} mor={unit.get('morale',0)} luk={unit.get('luck',0)} => {s}")

# Summary sorted
print("\n" + "=" * 90)
print("Sorted by Score (low to high)")
print("=" * 90)
scored = [(eid, ep["name"], ep["type"], sum(game_score(u["unitId"], u["count"], u["star"]) for u in ep["units"])) for eid, ep in bp.items()]
for eid, name, typ, score in sorted(scored, key=lambda x: x[3]):
    print(f"  {eid:20s} | {name:10s} | {typ:7s} | {score}")