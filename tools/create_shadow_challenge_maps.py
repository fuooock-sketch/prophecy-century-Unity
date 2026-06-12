# -*- coding: utf-8 -*-
"""Create independent shadow-player challenge campaigns from captured boards."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "Assets" / "Resources" / "Data"
LOG_PATH = Path(r"C:\Users\huawe\AppData\LocalLow\DefaultCompany\prophecy_century\player_state_log.jsonl")


LIGHT_NAME_TO_ID = {
    "流浪者": "wanderer",
    "精灵": "elf",
    "卫戍协兵": "garrison_guard",
    "骑士": "knight",
    "牧师": "priest",
    "光明武士": "bright_warrior",
    "铁匠": "blacksmith",
    "刺客": "assassin",
    "武学大师": "martial_master",
    "莱特使者": "light_envoy",
    "光明导师": "light_mentor",
    "皇家剑士": "royal_swordsman",
    "僧侣": "monk",
    "莱特的回响": "echo_of_light",
}


LIGHT_ROWS = [
    "2-1 流浪者x17；3-2 精灵x12",
    "2-1 流浪者x20；3-2 流浪者x18；4-2 精灵x16",
    "1-1 卫戍协兵x30；2-1 骑士x22；3-2 流浪者x20；4-2 牧师x14",
    "1-1 卫戍协兵x38；2-1 骑士x30；2-2 光明武士x25；3-2 流浪者x28；4-2 牧师x17",
    "1-1 卫戍协兵x44；2-1 光明武士x36；2-2 骑士x30；3-1 铁匠x25；3-2 流浪者x24；4-2 牧师x15",
    "1-1 卫戍协兵x60；2-1 骑士x50；2-2 光明武士x35；3-1 刺客x48；3-2 铁匠x35；4-2 流浪者x40；4-3 牧师x20",
    "1-1 卫戍协兵x60；2-1 骑士x48；2-2 光明武士x30；3-1 刺客x50；3-2 武学大师x38；3-3 铁匠x36；4-1 流浪者x44；4-2 牧师x30",
    "1-1 卫戍协兵x80；2-1 骑士x65；2-2 光明武士x50；3-1 刺客x45；3-2 武学大师x40；3-3 铁匠x40；4-1 流浪者x42；4-2 牧师x35；4-3 莱特使者x35",
    "1-1 卫戍协兵x85；2-1 骑士x70；2-2 光明武士x60；3-1 铁匠x45；3-2 武学大师x45；3-3 流浪者x50；4-1 莱特使者x40；4-2 牧师x45；4-3 光明导师x28",
    "1-1 卫戍协兵x130；2-1 骑士x90；2-2 光明武士x80；3-1 皇家剑士x60；3-2 武学大师x60；3-3 铁匠x55；4-1 流浪者x60；4-2 莱特使者x50；4-3 牧师x55；4-4 光明导师x32",
    "1-1 卫戍协兵x140；2-1 骑士x95；2-2 光明武士x85；3-1 皇家剑士x75；3-2 武学大师x70；3-3 刺客x60；4-1 莱特使者x65；4-2 牧师x70；4-3 光明导师x44；4-4 铁匠x40",
    "1-1 卫戍协兵x145；2-1 骑士x105；2-2 皇家剑士x95；3-1 刺客x105；3-2 武学大师x85；3-3 刺客x90；4-1 流浪者x80；4-2 牧师x70；4-3 莱特使者x65；4-4 光明导师x72",
    "1-1 卫戍协兵x210；2-1 皇家剑士x145；2-2 光明武士x130；3-1 骑士x110；3-2 武学大师x105；3-3 刺客x95；4-1 莱特使者x90；4-2 牧师x95；4-3 光明导师x50；4-4 铁匠x26",
    "1-1 卫戍协兵x200；2-1 骑士x135；2-2 皇家剑士x125；3-1 光明武士x115；3-2 武学大师x110；3-3 刺客x100；4-1 莱特使者x105；4-2 牧师x110；4-3 光明导师x85；4-4 僧侣x91",
    "1-1 卫戍协兵x310；2-1 皇家剑士x220；2-2 光明武士x190；3-1 骑士x170；3-2 武学大师x150；3-3 刺客x140；4-1 莱特使者x140；4-2 牧师x150；4-3 光明导师x130；4-4 僧侣x20",
    "1-1 卫戍协兵x330；2-1 皇家剑士x240；2-2 骑士x210；3-1 刺客x190；3-2 武学大师x170；3-3 刺客x160；4-1 莱特使者x150；4-2 牧师x150；4-3 光明导师x150；4-4 莱特的回响x50",
    "1-1 卫戍协兵x360；2-1 皇家剑士x280；2-2 骑士x230；3-1 刺客x220；3-2 武学大师x200；3-3 刺客x200；4-1 莱特使者x170；4-2 牧师x180；4-3 光明导师x210；4-4 莱特的回响x230",
    "1-1 卫戍协兵x420；2-1 皇家剑士x310；2-2 骑士x270；3-1 刺客x240；3-2 武学大师x230；3-3 刺客x220；4-1 莱特使者x190；4-2 牧师x210；4-3 光明导师x240；4-4 莱特的回响x190",
    "1-1 卫戍协兵x460；2-1 皇家剑士x350；2-2 骑士x310；3-1 刺客x280；3-2 武学大师x260；3-3 刺客x250；4-1 莱特使者x230；4-2 牧师x250；4-3 光明导师x290；4-4 莱特的回响x320",
    "1-1 卫戍协兵x560；2-1 卫戍协兵x400；2-2 皇家剑士x380；3-1 刺客x300；3-2 武学大师x330；3-3 刺客x300；4-1 莱特使者x300；4-2 牧师x310；4-3 光明导师x360；4-4 莱特的回响x600",
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

    start_index = 0
    for index in range(1, len(events)):
        previous_round = int(events[index - 1].get("round") or 0)
        current_round = int(events[index].get("round") or 0)
        if current_round == 1 and previous_round > 1:
            start_index = index

    states = [event for event in events[start_index:] if event.get("type") == "state" and event.get("board")]
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
        by_round[1] = [
            {
                "slotId": "4-4",
                "unitId": "frost_spirit",
                "count": 21,
                "star": 1,
            }
        ]

    missing = [round_number for round_number in range(1, 21) if round_number not in by_round]
    if missing:
        raise RuntimeError(f"Latest logged run is missing state rounds: {missing}")

    return [by_round[round_number] for round_number in range(1, 21)]


def parse_light_states(unit_stars: dict[str, int]) -> list[list[dict[str, Any]]]:
    states: list[list[dict[str, Any]]] = []
    token_pattern = re.compile(r"(?P<slot>\d-\d)\s+(?P<name>[^x；]+)x(?P<count>\d+)")
    for line in LIGHT_ROWS:
        units: list[dict[str, Any]] = []
        for match in token_pattern.finditer(line):
            name = match.group("name").strip()
            unit_id = LIGHT_NAME_TO_ID.get(name)
            if not unit_id:
                raise RuntimeError(f"Unknown unit name in light challenge: {name}")
            units.append(
                {
                    "slotId": match.group("slot"),
                    "unitId": unit_id,
                    "count": int(match.group("count")),
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
    ]
    challenge_maps = [
        build_map("shadow_elemental_map", "Shadow Challenge: Elemental", "shadow_elemental"),
        build_map("shadow_light_map", "Shadow Challenge: Light", "shadow_light"),
    ]
    challenge_presets = [
        *(preset("shadow_elemental", "Elemental Shadow", index, units) for index, units in enumerate(elemental_states, start=1)),
        *(preset("shadow_light", "Light Shadow", index, units) for index, units in enumerate(light_states, start=1)),
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
    print(f"Generated presets: {len(challenge_presets)}")


if __name__ == "__main__":
    main()
