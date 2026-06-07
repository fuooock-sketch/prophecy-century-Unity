# -*- coding: utf-8 -*-
"""Generate Unity runtime JSON from local Excel config tables."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import openpyxl


EXCEL_DIR = Path("docs/excel")
OUT_DIR = Path("Assets/Resources/Data")

WORLD_MAPS_XLSX = "地图配置_地图定义表.xlsx"
WORLD_MAP_LAYERS_XLSX = "地图配置_层级表.xlsx"
WORLD_MAP_NODES_XLSX = "地图配置_节点表.xlsx"
WORLD_MAP_CONNECTIONS_XLSX = "地图配置_连接表.xlsx"
ENEMY_PRESETS_XLSX = "怪物配置_敌人预设表.xlsx"
ENEMY_UNITS_XLSX = "怪物配置_敌方单位明细表.xlsx"
RUN_FLOW_PHASES_XLSX = "单局流程_阶段状态配置表.xlsx"
RUN_FLOW_TRIGGERS_XLSX = "单局流程_触发时机配置表.xlsx"
UNIT_DATA_JSON = "unit_data.json"


def read_xlsx(filename: str) -> list[dict[str, Any]]:
    path = EXCEL_DIR / filename
    if not path.exists():
        raise FileNotFoundError(f"Missing Excel table: {path}")

    workbook = openpyxl.load_workbook(path, data_only=True, read_only=True)
    sheet = workbook.active
    rows = list(sheet.iter_rows(values_only=True))
    if not rows:
        return []

    headers = [safe_str(value) for value in rows[0]]
    result: list[dict[str, Any]] = []
    for values in rows[1:]:
        row = {
            headers[index]: value
            for index, value in enumerate(values)
            if index < len(headers) and headers[index]
        }
        if any(not is_blank(value) for value in row.values()):
            result.append(row)
    return result


def load_json_array(filename: str) -> list[dict[str, Any]]:
    path = OUT_DIR / filename
    if not path.exists():
        return []

    with path.open("r", encoding="utf-8-sig") as file:
        data = json.load(file)
    if not isinstance(data, list):
        raise ValueError(f"Expected a JSON array in {path}")
    return data


def write_json(filename: str, data: Any) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUT_DIR / filename
    with path.open("w", encoding="utf-8", newline="\n") as file:
        json.dump(data, file, ensure_ascii=False, indent=2)
        file.write("\n")


def is_blank(value: Any) -> bool:
    return value is None or str(value).strip() == ""


def safe_str(value: Any, default: str = "") -> str:
    if is_blank(value):
        return default
    return str(value).strip()


def safe_int(value: Any, default: int = 0) -> int:
    if is_blank(value):
        return default
    try:
        return int(float(str(value).strip()))
    except (TypeError, ValueError):
        return default


def safe_float(value: Any, default: float = 0.0) -> float:
    if is_blank(value):
        return default
    try:
        return float(str(value).strip())
    except (TypeError, ValueError):
        return default


def safe_bool(value: Any, default: bool = True) -> bool:
    if is_blank(value):
        return default
    text = str(value).strip().lower()
    return text in {"true", "1", "yes", "y", "是"}


def generate_world_maps() -> list[dict[str, Any]]:
    maps = read_xlsx(WORLD_MAPS_XLSX)
    layers = read_xlsx(WORLD_MAP_LAYERS_XLSX)
    nodes = read_xlsx(WORLD_MAP_NODES_XLSX)
    connections = read_xlsx(WORLD_MAP_CONNECTIONS_XLSX)

    result: list[dict[str, Any]] = []
    for map_row in maps:
        map_id = safe_str(map_row.get("map_id"))
        if not map_id or safe_str(map_row.get("current_status")).lower() == "deprecated":
            continue

        map_layers = [row for row in layers if safe_str(row.get("map_id")) == map_id]
        map_nodes = [row for row in nodes if safe_str(row.get("map_id")) == map_id]
        map_connections = [
            row
            for row in connections
            if safe_str(row.get("map_id")) == map_id and safe_bool(row.get("enabled"), True)
        ]

        world_map = {
            "id": map_id,
            "name": safe_str(map_row.get("name")),
            "startNodeId": safe_str(map_row.get("start_node_id")),
            "layers": [
                {
                    "index": safe_int(row.get("layer_index")),
                    "name": safe_str(row.get("layer_name")),
                }
                for row in sorted(map_layers, key=lambda item: safe_int(item.get("layer_index")))
            ],
            "nodes": [],
            "connections": [],
        }

        for node_row in sorted(map_nodes, key=lambda item: safe_int(item.get("layer"))):
            node = {
                "id": safe_str(node_row.get("node_id")),
                "name": safe_str(node_row.get("node_name")),
                "layer": safe_int(node_row.get("layer")),
                "type": safe_str(node_row.get("type"), "empty"),
                "x": safe_float(node_row.get("x"), 0.5),
                "y": safe_float(node_row.get("y"), 0.5),
            }

            enemy_preset_id = safe_str(node_row.get("enemy_preset_id"))
            if enemy_preset_id:
                node["enemyPresetId"] = enemy_preset_id

            reward_gold = safe_int(node_row.get("reward_gold"))
            reward_treasure_id = safe_str(node_row.get("reward_treasure_id"))
            if reward_gold > 0 or reward_treasure_id:
                reward: dict[str, Any] = {}
                if reward_gold > 0:
                    reward["gold"] = reward_gold
                if reward_treasure_id:
                    reward["treasureId"] = reward_treasure_id
                node["reward"] = reward

            world_map["nodes"].append(node)

        for connection_row in map_connections:
            world_map["connections"].append(
                {
                    "fromNodeId": safe_str(connection_row.get("from_node_id")),
                    "toNodeId": safe_str(connection_row.get("to_node_id")),
                }
            )

        result.append(world_map)

    write_json("world_maps.json", result)
    return result


def generate_enemy_presets() -> list[dict[str, Any]]:
    presets = read_xlsx(ENEMY_PRESETS_XLSX)
    units = read_xlsx(ENEMY_UNITS_XLSX)

    result: list[dict[str, Any]] = []
    for preset_row in presets:
        preset_id = safe_str(preset_row.get("enemy_preset_id"))
        if not preset_id:
            continue

        preset_units = [row for row in units if safe_str(row.get("enemy_preset_id")) == preset_id]
        result.append(
            {
                "id": preset_id,
                "name": safe_str(preset_row.get("name")),
                "type": safe_str(preset_row.get("type"), "normal"),
                "units": [
                    {
                        "slotId": safe_str(row.get("slot_id")),
                        "unitId": safe_str(row.get("unit_id")),
                        "count": safe_int(row.get("count"), 1),
                        "star": safe_int(row.get("star"), 1),
                    }
                    for row in sorted(preset_units, key=lambda item: safe_str(item.get("slot_id")))
                ],
            }
        )

    write_json("boss_enemies.json", result)
    return result


def generate_run_flow_config() -> dict[str, Any]:
    phase_rows = read_xlsx(RUN_FLOW_PHASES_XLSX)
    trigger_rows = read_xlsx(RUN_FLOW_TRIGGERS_XLSX)

    result = {
        "phases": [
            {
                "phase": safe_str(row.get("phase")),
                "state": safe_str(row.get("state")),
                "displayName": safe_str(row.get("display_name")),
                "playerAction": safe_str(row.get("player_action")),
                "enterCondition": safe_str(row.get("enter_condition")),
                "exitCondition": safe_str(row.get("exit_condition")),
                "engineeringEntry": safe_str(row.get("engineering_entry")),
                "notes": safe_str(row.get("notes")),
            }
            for row in phase_rows
            if safe_str(row.get("phase"))
        ],
        "triggers": [
            {
                "id": safe_str(row.get("trigger_id")),
                "name": safe_str(row.get("trigger_name")),
                "phase": safe_str(row.get("phase")),
                "timing": safe_str(row.get("timing")),
                "condition": safe_str(row.get("condition")),
                "effectSummary": safe_str(row.get("effect_summary")),
                "engineeringEntry": safe_str(row.get("engineering_entry")),
                "configurableNow": safe_bool(row.get("configurable_now"), False),
                "notes": safe_str(row.get("notes")),
            }
            for row in trigger_rows
            if safe_str(row.get("trigger_id"))
        ],
    }

    write_json("run_flow_config.json", result)
    return result


def validate(world_maps: list[dict[str, Any]], enemy_presets: list[dict[str, Any]]) -> list[str]:
    issues: list[str] = []
    preset_ids = {preset["id"] for preset in enemy_presets if preset.get("id")}
    unit_ids = {unit.get("id") for unit in load_json_array(UNIT_DATA_JSON) if unit.get("id")}
    treasure_ids = {treasure.get("id") for treasure in load_json_array("treasures.json") if treasure.get("id")}

    for preset in enemy_presets:
        if not preset.get("units"):
            issues.append(f"Enemy preset has no units: {preset.get('id')}")
        for unit in preset.get("units", []):
            unit_id = unit.get("unitId")
            if unit_id and unit_id not in unit_ids:
                issues.append(f"Enemy preset {preset.get('id')} references missing unit: {unit_id}")

    for world_map in world_maps:
        map_id = world_map.get("id")
        nodes = world_map.get("nodes", [])
        node_ids = {node.get("id") for node in nodes if node.get("id")}
        if world_map.get("startNodeId") not in node_ids:
            issues.append(f"Map {map_id} startNodeId is missing: {world_map.get('startNodeId')}")

        for node in nodes:
            enemy_preset_id = node.get("enemyPresetId")
            if enemy_preset_id and enemy_preset_id not in preset_ids:
                issues.append(f"Map {map_id} node {node.get('id')} references missing enemy preset: {enemy_preset_id}")
            treasure_id = (node.get("reward") or {}).get("treasureId")
            if treasure_id and treasure_ids and treasure_id not in treasure_ids:
                issues.append(f"Map {map_id} node {node.get('id')} references missing treasure: {treasure_id}")

        for connection in world_map.get("connections", []):
            from_id = connection.get("fromNodeId")
            to_id = connection.get("toNodeId")
            if from_id not in node_ids:
                issues.append(f"Map {map_id} connection source is missing: {from_id}")
            if to_id not in node_ids:
                issues.append(f"Map {map_id} connection target is missing: {to_id}")

    return issues


def main() -> None:
    world_maps = generate_world_maps()
    enemy_presets = generate_enemy_presets()
    run_flow_config = generate_run_flow_config()
    issues = validate(world_maps, enemy_presets)

    total_nodes = sum(len(world_map.get("nodes", [])) for world_map in world_maps)
    total_connections = sum(len(world_map.get("connections", [])) for world_map in world_maps)
    total_enemy_units = sum(len(preset.get("units", [])) for preset in enemy_presets)

    print(f"Generated world_maps.json: {len(world_maps)} map(s), {total_nodes} nodes, {total_connections} connections")
    print(f"Generated boss_enemies.json: {len(enemy_presets)} preset(s), {total_enemy_units} unit entries")
    print(
        "Generated run_flow_config.json: "
        f"{len(run_flow_config['phases'])} phase(s), {len(run_flow_config['triggers'])} trigger(s)"
    )

    if issues:
        print("Validation failed:")
        for issue in issues:
            print(f"  - {issue}")
        raise SystemExit(1)

    print("Validation OK")


if __name__ == "__main__":
    main()
