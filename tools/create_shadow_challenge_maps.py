# -*- coding: utf-8 -*-
"""Create independent shadow-player challenge campaigns from captured boards."""

from __future__ import annotations

import json
import os
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "Assets" / "Resources" / "Data"
LOG_PATH = Path(os.environ.get(
    "PROPHECY_PLAYER_STATE_LOG",
    Path.home() / "AppData" / "LocalLow" / "DefaultCompany" / "prophecy_century" / "player_state_log.jsonl",
))


LIGHT_ROWS = [
    [("2-1", "wanderer", 17), ("3-2", "elf", 12)],
    [("4-4", "elf", 21)],
    [("4-3", "bright_warrior", 23), ("4-4", "elf", 21)],
    [("1-1", "bright_warrior", 23), ("2-1", "monk", 14), ("2-2", "blacksmith", 15), ("3-2", "elf", 24), ("3-3", "wanderer", 10)],
    [("1-1", "bright_warrior", 28), ("2-1", "monk", 14), ("2-2", "blacksmith", 15), ("3-1", "knight", 14), ("3-2", "elf", 27), ("3-3", "wanderer", 10), ("4-4", "priest", 9)],
    [("1-1", "bright_warrior", 33), ("2-1", "monk", 20), ("2-2", "blacksmith", 18), ("3-1", "knight", 14), ("3-2", "priest", 12), ("3-3", "wanderer", 18), ("4-1", "elf", 35), ("4-2", "monk", 14), ("4-3", "elf", 30), ("4-4", "blacksmith", 18)],
    [("2-1", "knight", 19), ("2-2", "knight", 19), ("3-1", "monk", 55), ("3-2", "bright_warrior", 42), ("3-3", "blacksmith", 24), ("4-1", "assassin", 12), ("4-3", "elf", 70), ("4-4", "priest", 22)],
    [("1-1", "bright_warrior", 55), ("2-1", "knight", 22), ("2-2", "knight", 20), ("3-1", "monk", 65), ("3-2", "priest", 26), ("3-3", "blacksmith", 28), ("4-1", "assassin", 18), ("4-2", "wanderer", 24), ("4-3", "elf", 75), ("4-4", "priest", 28)],
    [("1-1", "bright_warrior", 65), ("2-1", "knight", 24), ("2-2", "knight", 22), ("3-1", "monk", 70), ("3-2", "assassin", 38), ("3-3", "blacksmith", 32), ("4-1", "martial_master", 12), ("4-2", "wanderer", 28), ("4-3", "elf", 80), ("4-4", "priest", 30)],
    [("1-1", "bright_warrior", 95), ("2-1", "monk", 90), ("2-2", "knight", 60), ("3-1", "wanderer", 42), ("3-2", "assassin", 65), ("3-3", "martial_master", 28), ("4-1", "blacksmith", 42), ("4-2", "priest", 48), ("4-3", "elf", 95), ("4-4", "light_mentor", 18)],
    [("1-1", "bright_warrior", 105), ("2-1", "monk", 95), ("2-2", "knight", 62), ("3-1", "wanderer", 50), ("3-2", "assassin", 72), ("3-3", "martial_master", 32), ("4-1", "priest", 55), ("4-2", "light_envoy", 18), ("4-3", "elf", 100), ("4-4", "light_mentor", 22)],
    [("1-1", "garrison_guard", 28), ("2-1", "bright_warrior", 95), ("2-2", "knight", 70), ("3-1", "assassin", 88), ("3-2", "monk", 120), ("3-3", "martial_master", 45), ("4-1", "priest", 70), ("4-2", "light_envoy", 28), ("4-3", "light_mentor", 35), ("4-4", "echo_of_light", 4)],
    [("1-1", "garrison_guard", 45), ("2-1", "bright_warrior", 130), ("2-2", "garrison_guard", 26), ("3-1", "assassin", 130), ("3-2", "monk", 135), ("3-3", "martial_master", 70), ("4-1", "priest", 75), ("4-2", "light_envoy", 35), ("4-3", "light_mentor", 60), ("4-4", "echo_of_light", 6)],
    [("1-1", "garrison_guard", 55), ("2-1", "bright_warrior", 145), ("2-2", "garrison_guard", 32), ("3-1", "assassin", 145), ("3-2", "monk", 145), ("3-3", "martial_master", 78), ("4-1", "priest", 85), ("4-2", "light_envoy", 40), ("4-3", "light_mentor", 70), ("4-4", "echo_of_light", 8)],
    [("1-1", "garrison_guard", 70), ("2-1", "bright_warrior", 160), ("2-2", "garrison_guard", 40), ("3-1", "assassin", 160), ("3-2", "monk", 155), ("3-3", "royal_swordsman", 38), ("4-1", "martial_master", 90), ("4-2", "light_envoy", 46), ("4-3", "light_mentor", 85), ("4-4", "echo_of_light", 10)],
    [("1-1", "garrison_guard", 105), ("2-1", "bright_warrior", 230), ("2-2", "echo_of_light", 18), ("3-1", "assassin", 190), ("3-2", "royal_swordsman", 35), ("3-3", "martial_master", 145), ("4-1", "light_mentor", 55), ("4-2", "light_envoy", 60), ("4-3", "monk", 170), ("4-4", "priest", 130)],
    [("1-1", "garrison_guard", 120), ("2-1", "bright_warrior", 245), ("2-2", "echo_of_light", 22), ("3-1", "assassin", 205), ("3-2", "royal_swordsman", 42), ("3-3", "martial_master", 155), ("4-1", "light_mentor", 70), ("4-2", "light_envoy", 68), ("4-3", "monk", 180), ("4-4", "priest", 140)],
    [("1-1", "garrison_guard", 170), ("2-1", "bright_warrior", 280), ("2-2", "echo_of_light", 38), ("3-1", "royal_swordsman", 90), ("3-2", "light_mentor", 105), ("3-3", "assassin", 260), ("4-1", "martial_master", 150), ("4-2", "light_envoy", 82), ("4-3", "monk", 205), ("4-4", "priest", 160)],
    [("1-1", "garrison_guard", 210), ("2-1", "bright_warrior", 300), ("2-2", "echo_of_light", 55), ("3-1", "royal_swordsman", 130), ("3-2", "light_mentor", 130), ("3-3", "assassin", 310), ("4-1", "martial_master", 175), ("4-2", "light_envoy", 95), ("4-3", "monk", 230), ("4-4", "priest", 180)],
    [("1-1", "garrison_guard", 250), ("2-1", "bright_warrior", 330), ("2-2", "echo_of_light", 70), ("3-1", "royal_swordsman", 165), ("3-2", "light_mentor", 160), ("3-3", "assassin", 360), ("4-1", "martial_master", 210), ("4-2", "light_envoy", 120), ("4-3", "monk", 260), ("4-4", "priest", 210)],
]


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: Any) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_unit_stars() -> dict[str, int]:
    units = load_json(DATA_DIR / "unit_data.json")
    return {unit["id"]: int(unit.get("star") or 1) for unit in units if unit.get("id")}


def extract_latest_logged_states() -> list[list[dict[str, Any]]]:
    events: list[dict[str, Any]] = []
    for line in LOG_PATH.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get("type") in {"state", "battle"}:
            events.append(event)

    runs: list[list[dict[str, Any]]] = []
    current_run: list[dict[str, Any]] = []
    for event in events:
        if event.get("type") == "battle" and int(event.get("round") or 0) == 1 and current_run:
            runs.append(current_run)
            current_run = []
        current_run.append(event)
    if current_run:
        runs.append(current_run)

    for run in reversed(runs):
        states = [event for event in run if event.get("type") == "state" and event.get("board")]
        by_round: dict[int, list[dict[str, Any]]] = {}
        for state in states:
            round_number = int(state.get("round") or 0)
            if 1 <= round_number <= 20:
                by_round[round_number] = [
                    {
                        "slotId": card["slot"],
                        "unitId": card["id"],
                        "count": int(card["count"]),
                        "star": int(card.get("star") or 1),
                    }
                    for card in sorted(state.get("board") or [], key=lambda item: item.get("slot") or "")
                ]

        if 1 not in by_round:
            first_battle = next((event for event in run if event.get("type") == "battle" and int(event.get("round") or 0) == 1), None)
            first_units = first_battle.get("playerUnits") if first_battle else None
            if first_units:
                by_round[1] = [
                    {
                        "slotId": card["slot"],
                        "unitId": card["id"],
                        "count": int(card["count"]),
                        "star": int(card.get("star") or 1),
                    }
                    for card in sorted(first_units, key=lambda item: item.get("slot") or "")
                ]

        if all(round_number in by_round for round_number in range(1, 21)):
            return [by_round[round_number] for round_number in range(1, 21)]

    raise RuntimeError("No complete 20-round logged state run found")


def extract_latest_logged_battle_units() -> list[list[dict[str, Any]]]:
    events: list[dict[str, Any]] = []
    for line in LOG_PATH.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get("type") == "battle":
            events.append(event)

    runs: list[list[dict[str, Any]]] = []
    current_run: list[dict[str, Any]] = []
    for event in events:
        if int(event.get("round") or 0) == 1 and current_run:
            runs.append(current_run)
            current_run = []
        current_run.append(event)
    if current_run:
        runs.append(current_run)

    for run in reversed(runs):
        by_round: dict[int, list[dict[str, Any]]] = {}
        for battle in run:
            round_number = int(battle.get("round") or 0)
            if not 1 <= round_number <= 20:
                continue
            units = battle.get("playerUnits") or []
            by_round[round_number] = [
                {
                    "slotId": card["slot"],
                    "unitId": card["id"],
                    "count": int(card["count"]),
                    "star": int(card.get("star") or 1),
                }
                for card in sorted(units, key=lambda item: item.get("slot") or "")
            ]

        if all(round_number in by_round for round_number in range(1, 21)):
            return [by_round[round_number] for round_number in range(1, 21)]

    raise RuntimeError("No complete 20-round logged battle run found")


def parse_light_states(unit_stars: dict[str, int]) -> list[list[dict[str, Any]]]:
    states: list[list[dict[str, Any]]] = []
    for line in LIGHT_ROWS:
        units: list[dict[str, Any]] = []
        for slot_id, unit_id, count in line:
            if unit_id not in unit_stars:
                raise RuntimeError(f"Unknown unit id in light challenge: {unit_id}")
            units.append(
                {
                    "slotId": slot_id,
                    "unitId": unit_id,
                    "count": int(count),
                    "star": unit_stars[unit_id],
                }
            )
        states.append(units)
    if len(states) != 20:
        raise RuntimeError(f"Expected 20 light states, got {len(states)}")
    return states


def preset(prefix: str, name_prefix: str, round_number: int, units: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "id": f"{prefix}_r{round_number:02d}",
        "name": f"{name_prefix} R{round_number:02d}",
        "type": "boss" if round_number == 20 else "normal",
        "units": units,
    }


def build_map(map_id: str, map_name: str, preset_prefix: str) -> dict[str, Any]:
    start_id = f"{map_id}_start"
    layers = [{"index": 0, "name": "Start"}]
    layers.extend({"index": round_number, "name": f"R{round_number:02d}"} for round_number in range(1, 21))

    nodes = [
        {
            "id": start_id,
            "name": "Start",
            "layer": 0,
            "type": "start",
            "x": 0.5,
            "y": 0.03,
        }
    ]
    for round_number in range(1, 21):
        node_type = "boss" if round_number == 20 else "boss_guard" if round_number == 19 else "normal_battle"
        nodes.append(
            {
                "id": f"{map_id}_r{round_number:02d}",
                "name": f"Round {round_number:02d}",
                "layer": round_number,
                "type": node_type,
                "x": 0.5,
                "y": round(0.03 + round_number * 0.046, 3),
                "enemyPresetId": f"{preset_prefix}_r{round_number:02d}",
            }
        )

    connections = [{"fromNodeId": start_id, "toNodeId": f"{map_id}_r01"}]
    connections.extend(
        {
            "fromNodeId": f"{map_id}_r{round_number:02d}",
            "toNodeId": f"{map_id}_r{round_number + 1:02d}",
        }
        for round_number in range(1, 20)
    )

    return {
        "id": map_id,
        "name": map_name,
        "startNodeId": start_id,
        "layers": layers,
        "nodes": nodes,
        "connections": connections,
    }


def main() -> None:
    unit_stars = load_unit_stars()
    elemental_states = extract_latest_logged_states()
    elemental_battle_states = extract_latest_logged_battle_units()
    light_states = parse_light_states(unit_stars)

    campaigns = load_json(DATA_DIR / "campaigns.json")
    world_maps = load_json(DATA_DIR / "world_maps.json")
    enemy_presets = load_json(DATA_DIR / "boss_enemies.json")

    challenge_campaigns = [
        {
            "id": "shadow_elemental_challenge",
            "name": "Shadow Challenge: Elemental",
            "desc": "A 20-round challenge built from the latest captured elemental player board.",
            "mapId": "shadow_elemental_map",
        },
        {
            "id": "shadow_light_challenge",
            "name": "Shadow Challenge: Light",
            "desc": "A 20-round challenge built from the submitted Light and warrior board curve.",
            "mapId": "shadow_light_map",
        },
        {
            "id": "shadow_elemental_battle_challenge",
            "name": "Shadow Challenge: Elemental Battle",
            "desc": "A 20-round challenge built from the latest captured elemental battle snapshots.",
            "mapId": "shadow_elemental_battle_map",
        },
    ]
    challenge_maps = [
        build_map("shadow_elemental_map", "Shadow Challenge: Elemental", "shadow_elemental"),
        build_map("shadow_light_map", "Shadow Challenge: Light", "shadow_light"),
        build_map("shadow_elemental_battle_map", "Shadow Challenge: Elemental Battle", "shadow_elemental_battle"),
    ]
    challenge_presets = [
        *(preset("shadow_elemental", "Elemental Shadow", index, units) for index, units in enumerate(elemental_states, start=1)),
        *(preset("shadow_light", "Light Shadow", index, units) for index, units in enumerate(light_states, start=1)),
        *(preset("shadow_elemental_battle", "Elemental Battle Shadow", index, units) for index, units in enumerate(elemental_battle_states, start=1)),
    ]

    shadow_campaign_ids = {item["id"] for item in challenge_campaigns}
    shadow_map_ids = {item["id"] for item in challenge_maps}
    shadow_preset_prefixes = ("shadow_elemental_", "shadow_light_")

    campaigns = [item for item in campaigns if item.get("id") not in shadow_campaign_ids]
    campaigns.extend(challenge_campaigns)

    world_maps = [item for item in world_maps if item.get("id") not in shadow_map_ids]
    world_maps.extend(challenge_maps)

    enemy_presets = [
        item
        for item in enemy_presets
        if not any(str(item.get("id", "")).startswith(prefix) for prefix in shadow_preset_prefixes)
    ]
    enemy_presets.extend(challenge_presets)

    write_json(DATA_DIR / "campaigns.json", campaigns)
    write_json(DATA_DIR / "world_maps.json", world_maps)
    write_json(DATA_DIR / "boss_enemies.json", enemy_presets)

    print("Created shadow challenge campaigns:")
    print("  - shadow_elemental_challenge -> shadow_elemental_map")
    print("  - shadow_light_challenge -> shadow_light_map")
    print("  - shadow_elemental_battle_challenge -> shadow_elemental_battle_map")
    print(f"Generated presets: {len(challenge_presets)}")


if __name__ == "__main__":
    main()
