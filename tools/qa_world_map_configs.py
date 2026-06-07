# -*- coding: utf-8 -*-
"""Validate world-map and enemy-preset runtime JSON generated from Excel tables."""

from __future__ import annotations

import json
from collections import deque
from pathlib import Path
from typing import Any


DATA_DIR = Path("Assets/Resources/Data")
BATTLE_NODE_TYPES = {
    "battle",
    "normal_battle",
    "pressure_battle",
    "hard_battle",
    "elite_battle",
    "guard_battle",
    "boss_guard",
    "boss",
}
NON_BATTLE_NODE_TYPES = {"resource", "event", "rest"}


def load_json(name: str) -> Any:
    with (DATA_DIR / name).open("r", encoding="utf-8-sig") as file:
        return json.load(file)


def require(condition: bool, message: str, issues: list[str]) -> None:
    if not condition:
        issues.append(message)


def validate() -> list[str]:
    campaigns = load_json("campaigns.json")
    maps = load_json("world_maps.json")
    enemy_presets = load_json("boss_enemies.json")
    units = load_json("unit_data.json")
    treasures = load_json("treasures.json")

    issues: list[str] = []
    maps_by_id = {item.get("id"): item for item in maps}
    presets_by_id = {item.get("id"): item for item in enemy_presets}
    unit_ids = {item.get("id") for item in units}
    treasure_ids = {item.get("id") for item in treasures}

    for campaign in campaigns:
        map_id = campaign.get("mapId")
        require(map_id in maps_by_id, f"Campaign {campaign.get('id')} references missing map {map_id}", issues)

    for preset in enemy_presets:
        preset_id = preset.get("id")
        require(bool(preset_id), "Enemy preset has empty id", issues)
        require(bool(preset.get("units")), f"Enemy preset {preset_id} has no units", issues)
        for unit in preset.get("units") or []:
            unit_id = unit.get("unitId")
            require(unit_id in unit_ids, f"Enemy preset {preset_id} references missing unit {unit_id}", issues)
            require(int(unit.get("count") or 0) > 0, f"Enemy preset {preset_id}/{unit_id} has non-positive count", issues)

    for world_map in maps:
        map_id = world_map.get("id")
        nodes = world_map.get("nodes") or []
        connections = world_map.get("connections") or []
        node_by_id = {node.get("id"): node for node in nodes}

        require(world_map.get("startNodeId") in node_by_id, f"Map {map_id} has invalid start node", issues)
        require(any(node.get("type") == "boss" for node in nodes), f"Map {map_id} has no boss node", issues)

        outgoing: dict[str, list[str]] = {}
        for node in nodes:
            node_id = node.get("id")
            require(bool(node_id), f"Map {map_id} contains empty node id", issues)
            if node.get("type") in BATTLE_NODE_TYPES:
                preset_id = node.get("enemyPresetId")
                require(preset_id in presets_by_id, f"Map {map_id} node {node_id} references missing enemy preset {preset_id}", issues)

            reward = node.get("reward") or {}
            treasure_id = reward.get("treasureId")
            if treasure_id:
                require(treasure_id in treasure_ids, f"Map {map_id} node {node_id} references missing treasure {treasure_id}", issues)

        for connection in connections:
            from_id = connection.get("fromNodeId")
            to_id = connection.get("toNodeId")
            require(from_id in node_by_id, f"Map {map_id} connection source missing: {from_id}", issues)
            require(to_id in node_by_id, f"Map {map_id} connection target missing: {to_id}", issues)
            if from_id in node_by_id and to_id in node_by_id:
                from_layer = int(node_by_id[from_id].get("layer") or 0)
                to_layer = int(node_by_id[to_id].get("layer") or 0)
                require(to_layer == from_layer + 1, f"Map {map_id} connection {from_id}->{to_id} is not next-layer", issues)
                from_type = node_by_id[from_id].get("type")
                to_type = node_by_id[to_id].get("type")
                require(
                    not (from_type in NON_BATTLE_NODE_TYPES and to_type in NON_BATTLE_NODE_TYPES),
                    f"Map {map_id} connection {from_id}->{to_id} creates consecutive non-battle nodes",
                    issues,
                )
                require(
                    not (from_type == "elite_battle" and to_type == "rest"),
                    f"Map {map_id} connection {from_id}->{to_id} connects elite directly to rest",
                    issues,
                )
                outgoing.setdefault(from_id, []).append(to_id)

        visited = set()
        start = world_map.get("startNodeId")
        queue = deque([start])
        while queue:
            node_id = queue.popleft()
            if node_id in visited:
                continue
            visited.add(node_id)
            queue.extend(outgoing.get(node_id, []))

        unreachable = sorted(node_id for node_id in node_by_id if node_id not in visited)
        require(not unreachable, f"Map {map_id} has unreachable nodes: {', '.join(unreachable[:8])}", issues)
        require(
            any(node_by_id[node_id].get("type") == "boss" for node_id in visited if node_id in node_by_id),
            f"Map {map_id} boss is not reachable from start",
            issues,
        )

        by_layer: dict[int, list[dict[str, Any]]] = {}
        for node in nodes:
            by_layer.setdefault(int(node.get("layer") or 0), []).append(node)

        require(set(by_layer) == set(range(21)), f"Map {map_id} must contain layers D0~D20", issues)
        require(by_layer.get(1, [{}])[0].get("type") == "normal_battle", f"Map {map_id} D1 must be normal_battle", issues)
        require(len(by_layer.get(19, [])) == 1 and by_layer[19][0].get("type") == "boss_guard", f"Map {map_id} D19 must be one boss_guard", issues)
        require(len(by_layer.get(20, [])) == 1 and by_layer[20][0].get("type") == "boss", f"Map {map_id} D20 must be one boss", issues)
        require(
            not any(node.get("type") == "elite_battle" and int(node.get("layer") or 0) <= 5 for node in nodes),
            f"Map {map_id} has elite nodes in D1~D5",
            issues,
        )
        for layer, layer_nodes in by_layer.items():
            require(1 <= len(layer_nodes) <= 4, f"Map {map_id} layer D{layer} has {len(layer_nodes)} nodes", issues)

    return issues


def main() -> None:
    issues = validate()
    if issues:
        print("World map QA failed:")
        for issue in issues:
            print(f"  - {issue}")
        raise SystemExit(1)

    print("World map QA OK")


if __name__ == "__main__":
    main()
