# -*- coding: utf-8 -*-
"""Approximate bot simulation for the current world-map run.

This is not a Unity-authoritative simulation. It is a fast balancing bot that
uses current JSON data, current economy config, current map node types, and a
Python approximation of the C# battle score rules.
"""

from __future__ import annotations

import argparse
import json
import math
import random
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Assets" / "Resources" / "Data"

BATTLE_TYPES = {
    "normal_battle",
    "pressure_battle",
    "hard_battle",
    "elite_battle",
    "guard_battle",
    "boss_guard",
    "boss",
}

NON_BATTLE_TYPES = {"resource", "event", "rest"}
UNIT_PRICE = 3
HAND_MAX = 9
BOARD_MAX = 10
SYNTHESIS_COUNT = 3
EXPECTED_PLAYER_SCORE_BY_DAY = [
    120,
    300,
    650,
    900,
    1000,
    1700,
    2500,
    3200,
    3800,
    5200,
    6000,
    8500,
    11000,
    13500,
    16000,
    18500,
    21000,
    23500,
    26000,
    29000,
]


def load_json(name: str) -> Any:
    return json.loads((DATA / name).read_text(encoding="utf-8"))


CONFIG = load_json("unity_game_config.json")
UNITS = load_json("unit_data.json")
ENEMY_PRESETS = load_json("boss_enemies.json")
WORLD_MAPS = load_json("world_maps.json")

UNIT_BY_ID = {u["id"]: u for u in UNITS if u and u.get("id")}
PRESET_BY_ID = {p["id"]: p for p in ENEMY_PRESETS if p and p.get("id")}
MAP = next(m for m in WORLD_MAPS if m["id"] == "abyss_wilds")
NODE_BY_ID = {n["id"]: n for n in MAP["nodes"]}

ADJ: dict[str, list[str]] = defaultdict(list)
for edge in MAP["connections"]:
    ADJ[edge["fromNodeId"]].append(edge["toNodeId"])


@dataclass
class Card:
    unit_id: str
    count: int
    star: int


@dataclass
class BattleRow:
    day: int
    node_id: str
    node_type: str
    preset_id: str
    player_score: int
    enemy_score: int
    ratio: float
    win: bool
    fate: int


@dataclass
class RunResult:
    seed: int
    strategy: str
    result: str
    day: int
    fate: int
    shop_level: int
    gold: int
    board_score: int
    battles: list[BattleRow] = field(default_factory=list)
    timeline: list[str] = field(default_factory=list)


def start_count(unit: dict[str, Any]) -> int:
    return max(
        1,
        int(
            unit.get("defaultCount")
            or unit.get("startCount")
            or unit.get("baseCount")
            or 1
        ),
    )


def unit_score(card: Card) -> int:
    unit = UNIT_BY_ID.get(card.unit_id)
    if not unit:
        return 0
    hp_per_unit = max(1, int(unit.get("hpPerUnit") or unit.get("hp") or 1))
    max_hp = max(1, card.count * hp_per_unit)
    attack = max(0, int(unit.get("attack") or 0))
    defense = max(0, int(unit.get("defense") or 0))
    power = max(0, int(unit.get("power") or 0))
    speed = max(0, int(unit.get("speed") or 0))
    morale = max(0, int(unit.get("morale") or 0))
    luck = max(0, int(unit.get("luck") or 0))
    return int(
        round(
            attack * 1.85
            + defense * 1.25
            + power * 18
            + max_hp * 0.58
            + speed * 0.72
            + morale * 9
            + luck * 5
        )
    )


def board_score(board: list[Card]) -> int:
    return sum(unit_score(card) for card in board)


def preset_units(preset_id: str) -> list[Card]:
    preset = PRESET_BY_ID.get(preset_id) or {}
    result: list[Card] = []
    for item in preset.get("units") or []:
        uid = item.get("unitId")
        if uid not in UNIT_BY_ID:
            continue
        unit = UNIT_BY_ID[uid]
        result.append(
            Card(
                unit_id=uid,
                count=max(1, int(item.get("count") or start_count(unit))),
                star=max(1, int(item.get("star") or unit.get("star") or 1)),
            )
        )
    return result


def min_enemy_units(day: int) -> int:
    curve = CONFIG.get("worldMapMinEnemyUnitsByDay") or [
        2,
        2,
        3,
        3,
        3,
        4,
        4,
        4,
        4,
        5,
        5,
        5,
        5,
        5,
        5,
        5,
        5,
        5,
        6,
        6,
    ]
    index = min(max(day, 1), len(curve)) - 1
    return max(1, min(10, int(curve[index])))


def fill_enemy_lineup(enemies: list[Card], day: int) -> list[Card]:
    target = min_enemy_units(day)
    if len(enemies) >= target:
        return enemies
    max_star = min(6, max(1, 1 + (day - 1) // 3))
    existing = {card.unit_id for card in enemies}
    pool = sorted(
        [
            unit
            for unit in UNITS
            if unit
            and not unit.get("hidden")
            and unit.get("id") != "light_illusion"
            and int(unit.get("star") or 1) <= max_star
        ],
        key=lambda unit: (-int(unit.get("star") or 1), 1 if unit.get("type") == "range" else 0, unit.get("id", "")),
    )
    while len(enemies) < target and pool:
        prefer_range = sum(1 for card in enemies if UNIT_BY_ID.get(card.unit_id, {}).get("type") == "range") < max(1, target // 3)
        picked = next(
            (
                unit
                for unit in pool
                if unit.get("id") not in existing
                and ((prefer_range and unit.get("type") == "range") or (not prefer_range and unit.get("type") != "range"))
            ),
            None,
        )
        if picked is None:
            picked = next((unit for unit in pool if unit.get("id") not in existing), pool[0])
        uid = picked["id"]
        enemies.append(Card(uid, max(1, min(4, start_count(picked))), int(picked.get("star") or 1)))
        existing.add(uid)
    return enemies


def enemy_target_ratio(node_type: str, day: int) -> float:
    if node_type == "boss":
        return 1.20
    if node_type == "boss_guard":
        return 1.10
    if node_type == "elite_battle":
        return 1.05
    if node_type in {"hard_battle", "guard_battle"}:
        return 0.95
    if node_type == "pressure_battle":
        return 0.62 if day <= 4 else 0.85 if day <= 8 else 1.05
    if node_type == "normal_battle":
        return 0.50 if day <= 2 else min(0.68, 0.42 + day * 0.018)
    return 0.55


def expected_player_score(day: int) -> int:
    curve = CONFIG.get("worldMapExpectedPlayerScoreByDay") or EXPECTED_PLAYER_SCORE_BY_DAY
    index = min(max(day, 1), len(curve)) - 1
    return int(curve[index])


def scaled_enemy_score(
    preset_id: str,
    node_type: str,
    day: int,
    player_score: int,
    enemy_mode: str,
) -> int:
    enemies = fill_enemy_lineup(preset_units(preset_id), day)
    base = board_score(enemies)
    if base <= 0:
        return base

    if enemy_mode == "static":
        return base

    score_basis = player_score if enemy_mode == "dynamic" else expected_player_score(day)
    if score_basis <= 0:
        return base

    target = int(round(score_basis * enemy_target_ratio(node_type, day)))
    if target <= base:
        return base
    multiplier = min(600.0, target / float(base))
    scaled = [Card(e.unit_id, max(1, int(round(e.count * multiplier))), e.star) for e in enemies]
    return board_score(scaled)


def round_income(day: int) -> int:
    curve = CONFIG.get("roundIncomeByRound") or []
    if curve:
        return max(0, int(curve[min(max(day, 1), len(curve)) - 1]))
    return int(CONFIG.get("roundIncomeBase", 2)) + day


def shop_slots(level: int) -> int:
    slots = CONFIG.get("shopSlots") or [3, 4, 5, 5, 6, 6]
    return int(slots[min(max(level, 1), len(slots)) - 1])


def shop_max_star(level: int) -> int:
    stars = CONFIG.get("shopMaxStar") or [1, 2, 3, 4, 5, 6]
    return int(stars[min(max(level, 1), len(stars)) - 1])


def upgrade_cost(level: int, day: int, anchor_day: int) -> int:
    costs = CONFIG.get("shopUpgradeCost") or [0, 5, 6, 10, 16, 22]
    if level >= len(costs):
        return 0
    base = int(costs[min(max(level, 0), len(costs) - 1)])
    return max(0, base - max(0, day - anchor_day))


def unit_pick_weight(unit: dict[str, Any], strategy: str) -> float:
    star = int(unit.get("star") or 1)
    base = star * 10 + start_count(unit) * 0.35 + int(unit.get("attack") or 0) * 2
    tags = set(unit.get("tags") or [])
    if strategy == "elemental" and ("元素" in str(unit.get("race")) or "elemental" in unit.get("id", "")):
        base += 35
    if strategy == "light" and ("light" in unit.get("id", "") or "莱特" in str(unit.get("faith"))):
        base += 30
    if "hidden" in unit and unit.get("hidden"):
        base -= 1000
    if tags:
        base += min(10, len(tags) * 2)
    return base


def roll_shop(level: int, rng: random.Random, strategy: str) -> list[Card]:
    max_star = shop_max_star(level)
    pool = [u for u in UNITS if u and not u.get("hidden") and int(u.get("star") or 1) <= max_star]
    if not pool:
        return []
    weighted: list[dict[str, Any]] = []
    for unit in pool:
        weight = max(1, int(unit_pick_weight(unit, strategy) / 8))
        weighted.extend([unit] * weight)
    cards: list[Card] = []
    for _ in range(shop_slots(level)):
        unit = rng.choice(weighted)
        cards.append(Card(unit["id"], start_count(unit), int(unit.get("star") or 1)))
    return cards


def card_sort_key(card: Card, strategy: str) -> tuple[float, int]:
    unit = UNIT_BY_ID.get(card.unit_id) or {}
    return (unit_pick_weight(unit, strategy) + unit_score(card) * 0.05, card.star)


def synthesize(cards: list[Card]) -> list[Card]:
    changed = True
    while changed:
        changed = False
        groups: dict[tuple[str, int], list[Card]] = defaultdict(list)
        for card in cards:
            groups[(card.unit_id, card.star)].append(card)
        for (uid, star), group in list(groups.items()):
            if len(group) < SYNTHESIS_COUNT:
                continue
            picked = group[:SYNTHESIS_COUNT]
            for card in picked:
                cards.remove(card)
            unit = UNIT_BY_ID.get(uid) or {}
            cards.append(Card(uid, max(start_count(unit), sum(c.count for c in picked)), min(6, star + 1)))
            changed = True
            break
    return cards


def apply_growth(board: list[Card], day: int) -> int:
    """Loose approximation of common count-growth talents."""
    if not board:
        return 0
    gain_total = 0
    by_race = defaultdict(int)
    by_faith = defaultdict(int)
    by_id = defaultdict(int)
    for card in board:
        unit = UNIT_BY_ID.get(card.unit_id) or {}
        by_race[str(unit.get("race") or "")] += 1
        by_faith[str(unit.get("faith") or "")] += 1
        by_id[card.unit_id] += 1

    for card in list(board):
        unit = UNIT_BY_ID.get(card.unit_id) or {}
        for skill in unit.get("talents") or []:
            kind = str(skill.get("kind") or "")
            value = int(skill.get("value") or skill.get("attack") or skill.get("count") or 0)
            if value <= 0:
                continue
            gain = 0
            if "round_end" in kind or "round_start" in kind:
                gain = value
                if "per_faith" in kind:
                    gain *= max(1, by_faith.get(str(skill.get("faith") or unit.get("faith") or ""), 1))
                elif "per_race" in kind:
                    gain *= max(1, by_race.get(str(skill.get("race") or unit.get("race") or ""), 1))
            elif "on_entry" in kind and ("self_gain" in kind or "same_id" in kind):
                gain = min(3, by_race.get(str(unit.get("race") or ""), 1)) * value
            elif "on_gain" in kind and "self" in kind:
                gain = value
            if gain > 0:
                card.count += gain
                gain_total += gain

    # Mild global growth to account for unresolved chained effects.
    if day >= 8:
        for card in board:
            bonus = max(0, int(card.count * 0.04))
            card.count += bonus
            gain_total += bonus
    return gain_total


def manage_phase(
    board: list[Card],
    hand: list[Card],
    gold: int,
    shop_level: int,
    anchor_day: int,
    day: int,
    rng: random.Random,
    strategy: str,
) -> tuple[list[Card], list[Card], int, int, int]:
    carry = min(max(0, gold), int(CONFIG.get("goldCarryLimit", 0)))
    gold = round_income(day) + carry

    # Upgrade timing differs by strategy.
    desired_by_day = 1 + (1 if day >= 2 else 0) + (1 if day >= 4 else 0) + (1 if day >= 7 else 0) + (1 if day >= 11 else 0) + (1 if day >= 16 else 0)
    if strategy == "econ":
        desired_by_day += 1
    while shop_level < min(6, desired_by_day):
        cost = upgrade_cost(shop_level, day, anchor_day)
        reserve = 0 if strategy == "econ" else UNIT_PRICE
        if not board and not hand:
            reserve = UNIT_PRICE
        if gold < cost + reserve:
            break
        gold -= cost
        shop_level += 1
        anchor_day = day

    shop = roll_shop(shop_level, rng, strategy)
    buys_this_day = 0
    while gold >= UNIT_PRICE and len(hand) < HAND_MAX:
        if not shop:
            if gold <= UNIT_PRICE or strategy == "econ":
                break
            gold -= 1
            shop = roll_shop(shop_level, rng, strategy)
            continue
        best = max(shop, key=lambda c: card_sort_key(c, strategy))
        if strategy == "econ" and buys_this_day >= 1 and day < 8:
            break
        if strategy == "conservative" and buys_this_day >= 2:
            break
        gold -= UNIT_PRICE
        hand.append(best)
        shop.remove(best)
        buys_this_day += 1

    all_cards = synthesize(board + hand)
    all_cards.sort(key=lambda c: card_sort_key(c, strategy), reverse=True)
    board = all_cards[:BOARD_MAX]
    hand = all_cards[BOARD_MAX : BOARD_MAX + HAND_MAX]
    apply_growth(board, day)
    board = synthesize(board)
    board.sort(key=lambda c: card_sort_key(c, strategy), reverse=True)
    return board[:BOARD_MAX], hand[:HAND_MAX], gold, shop_level, anchor_day


def node_score(node: dict[str, Any], player_score: int, day: int, strategy: str, enemy_mode: str) -> float:
    node_type = node.get("type", "")
    if node_type in BATTLE_TYPES:
        enemy = scaled_enemy_score(node.get("enemyPresetId", ""), node_type, day, player_score, enemy_mode)
        ratio = enemy / max(1, player_score)
        reward = {
            "normal_battle": 10,
            "pressure_battle": 18,
            "hard_battle": 25,
            "guard_battle": 28,
            "elite_battle": 35,
            "boss_guard": 45,
            "boss": 100,
        }.get(node_type, 10)
        risk_penalty = max(0.0, ratio - 1.05) * (80 if strategy != "risk" else 35)
        if strategy == "risk":
            reward += 10
        return reward - risk_penalty
    if node_type == "resource":
        return 16 if strategy != "risk" else 8
    if node_type == "event":
        return 13
    if node_type == "rest":
        return 10 if strategy == "conservative" else 6
    return 0


def choose_next(
    current: str,
    board: list[Card],
    day: int,
    strategy: str,
    enemy_mode: str,
    rng: random.Random,
) -> str | None:
    candidates = ADJ.get(current, [])
    if not candidates:
        return None
    pscore = board_score(board)
    best = max(
        candidates,
        key=lambda node_id: node_score(NODE_BY_ID[node_id], pscore, day, strategy, enemy_mode)
        + rng.random() * 0.01,
    )
    return best


def battle_win_threshold(node_type: str) -> float:
    if node_type == "boss":
        return 0.70
    if node_type == "boss_guard":
        return 0.62
    if node_type in {"elite_battle", "hard_battle", "guard_battle"}:
        return 0.55
    if node_type == "pressure_battle":
        return 0.50
    return 0.45


def run_one(seed: int, strategy: str, enemy_mode: str) -> RunResult:
    rng = random.Random(seed)
    board: list[Card] = []
    hand: list[Card] = []
    gold = int(CONFIG.get("startGold", 3))
    shop_level = 1
    anchor_day = 1
    current = MAP["startNodeId"]
    fate = int(CONFIG.get("playerStartHp", 100))
    battles: list[BattleRow] = []
    timeline: list[str] = []

    for day in range(1, 26):
        board, hand, gold, shop_level, anchor_day = manage_phase(
            board, hand, gold, shop_level, anchor_day, day, rng, strategy
        )
        next_node = choose_next(current, board, day, strategy, enemy_mode, rng)
        if next_node is None:
            return RunResult(seed, strategy, "stuck", day, fate, shop_level, gold, board_score(board), battles, timeline)
        node = NODE_BY_ID[next_node]
        node_type = node.get("type", "")

        if node_type in BATTLE_TYPES:
            pscore = board_score(board)
            escore = scaled_enemy_score(node.get("enemyPresetId", ""), node_type, day, pscore, enemy_mode)
            ratio = escore / max(1, pscore)
            win = pscore / max(1, escore) >= battle_win_threshold(node_type)
            if win:
                # Approximate battle reward: choose one card at shop-level star.
                reward_star = min(6, shop_level + 1)
                pool = [u for u in UNITS if u and not u.get("hidden") and int(u.get("star") or 1) <= reward_star]
                choices = rng.sample(pool, k=min(3, len(pool))) if pool else []
                if choices and len(hand) < HAND_MAX:
                    unit = max(choices, key=lambda u: unit_pick_weight(u, strategy))
                    hand.append(Card(unit["id"], start_count(unit), int(unit.get("star") or 1)))
                apply_growth(board, day)
            else:
                if node_type == "boss":
                    fate = 0
                else:
                    fate -= max(5, int(10 + ratio * 8))
            battles.append(
                BattleRow(
                    day=day,
                    node_id=next_node,
                    node_type=node_type,
                    preset_id=node.get("enemyPresetId", ""),
                    player_score=pscore,
                    enemy_score=escore,
                    ratio=ratio,
                    win=win,
                    fate=fate,
                )
            )
            timeline.append(
                f"D{day:02d} {next_node:6s} {node_type:15s} "
                f"P={pscore:6d} E={escore:6d} ratio={ratio:4.2f} "
                f"{'WIN' if win else 'LOSE'} fate={fate}"
            )
            if fate <= 0:
                return RunResult(seed, strategy, "gameover", day, fate, shop_level, gold, board_score(board), battles, timeline)
            if node_type == "boss" and win:
                return RunResult(seed, strategy, "victory", day, fate, shop_level, gold, board_score(board), battles, timeline)
        elif node_type == "resource":
            gold += rng.randint(3, 10)
            timeline.append(f"D{day:02d} {next_node:6s} resource        gold={gold} shop={shop_level}")
        elif node_type == "event":
            if rng.random() < 0.5 and len(hand) < HAND_MAX:
                max_star = min(6, shop_level + 1)
                pool = [u for u in UNITS if u and not u.get("hidden") and int(u.get("star") or 1) <= max_star]
                unit = max(rng.sample(pool, min(3, len(pool))), key=lambda u: unit_pick_weight(u, strategy))
                hand.append(Card(unit["id"], start_count(unit), int(unit.get("star") or 1)))
            else:
                gold += 6
            timeline.append(f"D{day:02d} {next_node:6s} event           gold={gold} shop={shop_level}")
        elif node_type == "rest":
            apply_growth(board, day)
            timeline.append(f"D{day:02d} {next_node:6s} rest            score={board_score(board)}")

        current = next_node

    return RunResult(seed, strategy, "timeout", 25, fate, shop_level, gold, board_score(board), battles, timeline)


def summarize(results: list[RunResult]) -> None:
    wins = [r for r in results if r.result == "victory"]
    print(f"runs={len(results)} victories={len(wins)} win_rate={len(wins)/max(1,len(results)):.0%}")
    avg_day = sum(r.day for r in results) / max(1, len(results))
    avg_score = sum(r.board_score for r in results) / max(1, len(results))
    avg_shop = sum(r.shop_level for r in results) / max(1, len(results))
    print(f"avg_end_day={avg_day:.1f} avg_board_score={avg_score:.0f} avg_shop_level={avg_shop:.1f}")
    all_battles = [b for r in results for b in r.battles]
    if all_battles:
        print("\nBattle pressure by type:")
        for node_type in sorted({b.node_type for b in all_battles}):
            group = [b for b in all_battles if b.node_type == node_type]
            avg_ratio = sum(b.ratio for b in group) / len(group)
            win_rate = sum(1 for b in group if b.win) / len(group)
            print(f"  {node_type:15s} count={len(group):3d} avg_ratio={avg_ratio:4.2f} win={win_rate:4.0%}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seeds", type=int, default=20)
    parser.add_argument("--strategy", choices=["balanced", "risk", "econ", "conservative", "elemental", "light"], default="balanced")
    parser.add_argument(
        "--enemy-mode",
        choices=["design", "static", "dynamic"],
        default="design",
        help="design=fixed expected-player curve, static=raw presets, dynamic=player-following comparison only",
    )
    parser.add_argument("--show", type=int, default=1, help="number of run timelines to print")
    args = parser.parse_args()

    results = [run_one(seed, args.strategy, args.enemy_mode) for seed in range(args.seeds)]
    summarize(results)
    print()
    for result in results[: max(0, args.show)]:
        print("=" * 88)
        print(
            f"seed={result.seed} strategy={result.strategy} result={result.result} "
            f"enemy_mode={args.enemy_mode} "
            f"day={result.day} fate={result.fate} shop={result.shop_level} "
            f"gold={result.gold} score={result.board_score}"
        )
        for line in result.timeline:
            print("  " + line)


if __name__ == "__main__":
    main()
