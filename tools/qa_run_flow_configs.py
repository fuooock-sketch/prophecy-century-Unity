# -*- coding: utf-8 -*-
"""Validate the table-driven run flow against generated runtime JSON."""

from __future__ import annotations

import json
from collections import deque
from pathlib import Path
from typing import Any


DATA_DIR = Path("Assets/Resources/Data")
DEFAULT_CAMPAIGN_ID = "south_town_adventure"

EXPECTED_PHASES = {
    ("NightManage", "manage"),
    ("DayExplore", "day"),
    ("Battle", "battle"),
    ("Settle", "settle"),
    ("Victory", "victory"),
    ("GameOver", "gameover"),
}

EXPECTED_TRIGGERS = {
    "run_start",
    "round_start",
    "round_end",
    "day_start",
    "node_select",
    "node_complete",
    "battle_start",
    "battle_defeat",
    "boss_victory",
}


def load_json(name: str) -> Any:
    with (DATA_DIR / name).open("r", encoding="utf-8-sig") as file:
        return json.load(file)


def require(condition: bool, message: str, issues: list[str]) -> None:
    if not condition:
        issues.append(message)


def outgoing_by_source(connections: list[dict[str, Any]]) -> dict[str, list[str]]:
    outgoing: dict[str, list[str]] = {}
    for connection in connections:
        outgoing.setdefault(connection.get("fromNodeId"), []).append(connection.get("toNodeId"))
    return outgoing


def shortest_path_to_type(
    start_node_id: str,
    target_type: str,
    nodes_by_id: dict[str, dict[str, Any]],
    outgoing: dict[str, list[str]],
) -> list[str]:
    queue = deque([[start_node_id]])
    visited: set[str] = set()
    while queue:
        path = queue.popleft()
        node_id = path[-1]
        if node_id in visited:
            continue
        visited.add(node_id)

        node = nodes_by_id.get(node_id) or {}
        if node.get("type") == target_type:
            return path

        for next_node_id in outgoing.get(node_id, []):
            if next_node_id not in visited:
                queue.append([*path, next_node_id])

    return []


def phase_exists(flow: dict[str, Any], phase: str, state: str) -> bool:
    return any(item.get("phase") == phase and item.get("state") == state for item in flow.get("phases") or [])


def trigger_exists(flow: dict[str, Any], trigger_id: str) -> bool:
    return any(item.get("id") == trigger_id for item in flow.get("triggers") or [])


def validate() -> tuple[list[str], list[str]]:
    campaigns = load_json("campaigns.json")
    maps = load_json("world_maps.json")
    enemy_presets = load_json("boss_enemies.json")
    units = load_json("unit_data.json")
    treasures = load_json("treasures.json")
    flow = load_json("run_flow_config.json")

    issues: list[str] = []
    report: list[str] = []

    campaign_by_id = {item.get("id"): item for item in campaigns}
    map_by_id = {item.get("id"): item for item in maps}
    preset_by_id = {item.get("id"): item for item in enemy_presets}
    unit_ids = {item.get("id") for item in units}
    treasure_ids = {item.get("id") for item in treasures}

    for phase, state in sorted(EXPECTED_PHASES):
        require(phase_exists(flow, phase, state), f"Missing run-flow phase/state: {phase}/{state}", issues)

    for trigger_id in sorted(EXPECTED_TRIGGERS):
        require(trigger_exists(flow, trigger_id), f"Missing run-flow trigger: {trigger_id}", issues)

    campaign = campaign_by_id.get(DEFAULT_CAMPAIGN_ID) or next(iter(campaigns), None)
    require(campaign is not None, "No campaign config found", issues)
    world_map = map_by_id.get((campaign or {}).get("mapId"))
    require(world_map is not None, f"Campaign {(campaign or {}).get('id')} references missing map", issues)
    if world_map is None:
        return issues, report

    nodes = world_map.get("nodes") or []
    nodes_by_id = {node.get("id"): node for node in nodes}
    outgoing = outgoing_by_source(world_map.get("connections") or [])
    start_node_id = world_map.get("startNodeId")
    start_node = nodes_by_id.get(start_node_id)
    require(start_node is not None, f"Map {world_map.get('id')} has missing start node {start_node_id}", issues)

    # Mirrors ProphecyGameSession.InitializeWorldMapProgress.
    visible_nodes = {
        node.get("id")
        for node in nodes
        if node.get("id") == start_node_id or int(node.get("layer") or 0) <= 1
    }
    cleared_nodes = {start_node_id}
    current_node_id = start_node_id
    remaining_move_points = 1

    first_destinations = [
        nodes_by_id[node_id]
        for node_id in outgoing.get(current_node_id, [])
        if node_id in nodes_by_id
        and node_id in visible_nodes
        and remaining_move_points >= 1
        and int(nodes_by_id[node_id].get("layer") or 0) == int((start_node or {}).get("layer") or 0) + 1
    ]
    require(first_destinations, "DayExplore has no reachable first-layer destinations from start", issues)
    report.append(f"First day reachable nodes: {len(first_destinations)}")

    for node in first_destinations:
        node_type = node.get("type")
        if node_type in {"battle", "boss"}:
            preset = preset_by_id.get(node.get("enemyPresetId"))
            require(preset is not None, f"Node {node.get('id')} has missing enemy preset {node.get('enemyPresetId')}", issues)
            require(bool((preset or {}).get("units")), f"Enemy preset {node.get('enemyPresetId')} has no units", issues)
        reward = node.get("reward") or {}
        if reward.get("treasureId"):
            require(reward.get("treasureId") in treasure_ids, f"Node {node.get('id')} has missing treasure reward", issues)
        if reward.get("unitId"):
            require(reward.get("unitId") in unit_ids, f"Node {node.get('id')} has missing unit reward", issues)

    non_battle = next((node for node in first_destinations if node.get("type") not in {"battle", "boss"}), None)
    if non_battle is not None:
        current_node_id = non_battle.get("id")
        remaining_move_points = 0
        visible_nodes.update(outgoing.get(current_node_id, []))
        cleared_nodes.add(current_node_id)
        phase, state = "NightManage", "manage"
        require(phase_exists(flow, phase, state), "Non-battle node completion cannot return to NightManage/manage", issues)
        report.append(f"Non-battle node sample: {current_node_id} -> {phase}/{state}")

    battle_node = next((node for node in first_destinations if node.get("type") in {"battle", "boss"}), None)
    if battle_node is not None:
        preset = preset_by_id.get(battle_node.get("enemyPresetId"))
        phase, state = "Battle", "battle"
        require(phase_exists(flow, phase, state), "Battle node cannot enter Battle/battle", issues)
        require(preset is not None and bool(preset.get("units")), f"Battle node {battle_node.get('id')} has unusable preset", issues)
        report.append(f"Battle node sample: {battle_node.get('id')} -> {phase}/{state}")
    else:
        report.append("Battle node sample: none on first layer")

    boss_path = shortest_path_to_type(start_node_id, "boss", nodes_by_id, outgoing)
    require(boss_path, "No path from start to boss", issues)
    if boss_path:
        report.append(f"Shortest boss path length: {len(boss_path) - 1}")
        for node_id in boss_path:
            node = nodes_by_id.get(node_id) or {}
            if node.get("type") in {"battle", "boss"}:
                preset = preset_by_id.get(node.get("enemyPresetId"))
                require(preset is not None, f"Boss path node {node_id} has missing preset", issues)
                require(bool((preset or {}).get("units")), f"Boss path node {node_id} preset has no units", issues)

    for preset in enemy_presets:
        for unit in preset.get("units") or []:
            require(unit.get("unitId") in unit_ids, f"Preset {preset.get('id')} references missing unit {unit.get('unitId')}", issues)

    require(start_node_id in cleared_nodes, "Run start does not mark start node as cleared", issues)
    return issues, report


def main() -> None:
    issues, report = validate()
    if issues:
        print("Run flow QA failed:")
        for issue in issues:
            print(f"  - {issue}")
        raise SystemExit(1)

    print("Run flow QA OK")
    for line in report:
        print(f"  - {line}")


if __name__ == "__main__":
    main()
