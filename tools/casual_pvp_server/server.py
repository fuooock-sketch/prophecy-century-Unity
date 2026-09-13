#!/usr/bin/env python3
"""Local asynchronous mirror-PVP server for Prophecy Century.

Uses only Python's standard library. The service binds to loopback by default and
stores normalized mirror snapshots in SQLite.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import sqlite3
import threading
import uuid
from contextlib import closing
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any


SCHEMA = """
CREATE TABLE IF NOT EXISTS snapshots (
    snapshot_id TEXT PRIMARY KEY,
    source_run_id TEXT NOT NULL,
    source_type TEXT NOT NULL,
    round INTEGER NOT NULL,
    content_version TEXT NOT NULL,
    combat_version TEXT NOT NULL,
    power_score INTEGER NOT NULL DEFAULT 0,
    display_name TEXT NOT NULL,
    captured_at_utc TEXT NOT NULL,
    dedupe_hash TEXT NOT NULL UNIQUE,
    payload TEXT NOT NULL,
    matched_count INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'active'
);
CREATE INDEX IF NOT EXISTS idx_snapshot_pool
ON snapshots(round, content_version, combat_version, status);

CREATE TABLE IF NOT EXISTS matches (
    match_id TEXT PRIMARY KEY,
    requester_run_id TEXT NOT NULL,
    snapshot_id TEXT NOT NULL,
    round INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS battle_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    match_id TEXT,
    snapshot_id TEXT,
    requester_run_id TEXT,
    round INTEGER NOT NULL,
    victory INTEGER NOT NULL,
    hp_delta INTEGER NOT NULL,
    player_score INTEGER NOT NULL,
    enemy_score INTEGER NOT NULL,
    reported_at_utc TEXT NOT NULL,
    payload TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS pool_metrics (
    round INTEGER NOT NULL,
    content_version TEXT NOT NULL,
    combat_version TEXT NOT NULL,
    snapshot_uploads INTEGER NOT NULL DEFAULT 0,
    duplicate_uploads INTEGER NOT NULL DEFAULT 0,
    match_requests INTEGER NOT NULL DEFAULT 0,
    empty_match_requests INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (round, content_version, combat_version)
);
"""


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_hash(snapshot: dict[str, Any]) -> str:
    canonical = {
        "contentVersion": snapshot.get("contentVersion"),
        "round": snapshot.get("round"),
        "combatVersion": snapshot.get("combatVersion"),
        "teamForestGiftTotal": snapshot.get("teamForestGiftTotal", 0),
        "units": sorted([{"forestGemsReceived": 0, "forestGemsAttached": 0, "battleProgressCounters": [], **unit}
                         for unit in snapshot.get("units") or []], key=lambda unit: str(unit.get("slotId", ""))),
    }
    raw = json.dumps(canonical, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def normalize_snapshot(value: dict[str, Any], source_type: str | None = None) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ValueError("snapshot must be an object")
    if value.get("schemaVersion", 1) != 1:
        raise ValueError("unsupported snapshot schema")
    if value.get("isCheatRun") is True:
        raise ValueError("debug-assisted snapshots cannot enter the mirror pool")
    is_v2 = value.get("combatVersion") == "battle_v2"
    team_gifts = value.get("teamForestGiftTotal", 0)
    if type(team_gifts) is not int or not 0 <= team_gifts <= 2_147_483_647:
        raise ValueError("invalid teamForestGiftTotal")
    if is_v2 and "teamForestGiftTotal" not in value:
        raise ValueError("battle_v2 requires teamForestGiftTotal")
    raw_units = value.get("units")
    if not isinstance(raw_units, list) or not 1 <= len(raw_units) <= 16:
        raise ValueError("snapshot must contain 1-16 units")
    units = []
    slots = set()
    for raw in raw_units:
        if not isinstance(raw, dict):
            raise ValueError("unit must be an object")
        unit_id = str(raw.get("unitId") or "").strip()
        slot_id = str(raw.get("slotId") or "").strip()
        if not unit_id or not slot_id:
            raise ValueError("unitId and slotId are required")
        if slot_id in slots:
            raise ValueError("duplicate occupied slot")
        slots.add(slot_id)
        if is_v2 and not all(field in raw for field in ("forestGemsReceived", "forestGemsAttached", "battleProgressCounters")):
            raise ValueError("battle_v2 requires unit combat counters")
        counters = raw.get("battleProgressCounters", [])
        if not isinstance(counters, list) or len(counters) > 64:
            raise ValueError("invalid battleProgressCounters")
        for counter in counters:
            if (not isinstance(counter, dict) or not isinstance(counter.get("key"), str)
                    or not 1 <= len(counter["key"]) <= 200 or type(counter.get("value")) is not int
                    or not 0 <= counter["value"] <= 2_147_483_647):
                raise ValueError("invalid battle progress counter")
        for field in ("star", "count", "maxHp", "attack", "defense", "power", "speed", "luck", "morale", "forestGemsReceived", "forestGemsAttached"):
            if field in raw:
                number = raw[field]
                if type(number) is not int or number < 0 or number > 2_147_483_647:
                    raise ValueError(f"invalid unit {field}")
        if "isGolden" in raw and type(raw["isGolden"]) is not bool:
            raise ValueError("isGolden must be boolean")
        units.append(
            {
                "unitId": unit_id,
                "name": str(raw.get("name") or unit_id),
                "slotId": slot_id,
                "star": max(1, min(99, int(raw.get("star") or 1))),
                "isGolden": bool(raw.get("isGolden", False)),
                "count": max(1, min(10_000_000, int(raw.get("count") or 1))),
                "maxHp": max(0, int(raw.get("maxHp") or 0)),
                "attack": max(0, int(raw.get("attack") or 0)),
                "defense": max(0, int(raw.get("defense") or 0)),
                "power": max(0, int(raw.get("power") or 0)),
                "speed": max(0, int(raw.get("speed") or 0)),
                "luck": max(0, int(raw.get("luck") or 0)),
                "morale": max(0, int(raw.get("morale") or 0)),
                "forestGemsReceived": raw.get("forestGemsReceived", 0),
                "forestGemsAttached": raw.get("forestGemsAttached", 0),
                "battleProgressCounters": [{"key": item["key"], "value": item["value"]} for item in counters],
            }
        )
    if not units or len(units) > 16:
        raise ValueError("snapshot must contain 1-16 valid units")
    round_number = int(value.get("round") or 0)
    if round_number < 1 or round_number > 100_000:
        raise ValueError("round is out of range")
    return {
        "schemaVersion": 1,
        "teamForestGiftTotal": team_gifts,
        "snapshotId": str(value.get("snapshotId") or f"snap_{uuid.uuid4().hex}"),
        "sourceRunId": str(value.get("sourceRunId") or "anonymous"),
        "sourceType": source_type or str(value.get("sourceType") or "player"),
        "displayName": str(value.get("displayName") or "训练镜像"),
        "round": round_number,
        "contentVersion": str(value.get("contentVersion") or "unknown"),
        "combatVersion": str(value.get("combatVersion") or "battle_v1"),
        "isCheatRun": False,
        "powerScore": max(0, int(value.get("powerScore") or 0)),
        "capturedAtUtc": str(value.get("capturedAtUtc") or utc_now()),
        "units": units,
    }


class MirrorDatabase:
    def __init__(self, path: Path):
        data_path = Path(__file__).resolve().parents[2] / "Assets/Resources/Data"
        catalog = json.loads((data_path / "unit_data.json").read_text(encoding="utf-8-sig"))
        config = json.loads((data_path / "unity_game_config.json").read_text(encoding="utf-8-sig"))
        self.unit_sizes = {unit["id"]: unit.get("size", 1) for unit in catalog}
        self.board_slots = {slot for row in config["boardLayout"] for slot in row["slots"]}
        self.path = path
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()
        with closing(self.connect()) as connection:
            connection.executescript(SCHEMA)
            # Upgrade old hashes without discarding the existing mirror pool.
            rows = connection.execute("SELECT snapshot_id, payload, dedupe_hash FROM snapshots").fetchall()
            upgrades = [(canonical_hash(json.loads(row["payload"])), row["snapshot_id"])
                        for row in rows if canonical_hash(json.loads(row["payload"])) != row["dedupe_hash"]]
            for _, snapshot_id in upgrades:
                connection.execute("UPDATE snapshots SET dedupe_hash = ? WHERE snapshot_id = ?",
                                   ("migration_" + snapshot_id, snapshot_id))
            connection.executemany("UPDATE snapshots SET dedupe_hash = ? WHERE snapshot_id = ?", upgrades)
            connection.commit()

    def connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(self.path, timeout=15)
        connection.row_factory = sqlite3.Row
        return connection

    @staticmethod
    def record_metric(connection: sqlite3.Connection, round_number: int, content_version: str,
                      combat_version: str, column: str) -> None:
        if column not in {"snapshot_uploads", "duplicate_uploads", "match_requests", "empty_match_requests"}:
            raise ValueError("unknown pool metric")
        connection.execute(
            f"""INSERT INTO pool_metrics(round, content_version, combat_version, {column}) VALUES (?, ?, ?, 1)
                ON CONFLICT(round, content_version, combat_version) DO UPDATE SET {column} = {column} + 1""",
            (round_number, content_version, combat_version),
        )

    def add_snapshot(self, raw: dict[str, Any], source_type: str | None = None) -> tuple[dict[str, Any], bool]:
        snapshot = normalize_snapshot(raw, source_type)
        self.validate_lineup(snapshot)
        dedupe = canonical_hash(snapshot)
        payload = json.dumps(snapshot, ensure_ascii=False, separators=(",", ":"))
        with self.lock, closing(self.connect()) as connection:
            same_id = connection.execute("SELECT dedupe_hash FROM snapshots WHERE snapshot_id = ?",
                                         (snapshot["snapshotId"],)).fetchone()
            if same_id and same_id["dedupe_hash"] != dedupe:
                raise ValueError("snapshotId already belongs to a different snapshot")
            existing = connection.execute("SELECT payload FROM snapshots WHERE dedupe_hash = ?", (dedupe,)).fetchone()
            if existing:
                self.record_metric(connection, snapshot["round"], snapshot["contentVersion"],
                                   snapshot["combatVersion"], "duplicate_uploads")
                connection.commit()
                return json.loads(existing["payload"]), False
            connection.execute(
                """INSERT INTO snapshots
                (snapshot_id, source_run_id, source_type, round, content_version, combat_version,
                 power_score, display_name, captured_at_utc, dedupe_hash, payload)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    snapshot["snapshotId"], snapshot["sourceRunId"], snapshot["sourceType"], snapshot["round"],
                    snapshot["contentVersion"], snapshot["combatVersion"], snapshot["powerScore"],
                    snapshot["displayName"], snapshot["capturedAtUtc"], dedupe, payload,
                ),
            )
            self.record_metric(connection, snapshot["round"], snapshot["contentVersion"],
                               snapshot["combatVersion"], "snapshot_uploads")
            connection.commit()
        return snapshot, True

    def match(self, request: dict[str, Any]) -> dict[str, Any] | None:
        if not isinstance(request, dict):
            raise ValueError("match request must be an object")
        round_number = int(request.get("round") or 0)
        if round_number < 1 or round_number > 100_000:
            raise ValueError("round is out of range")
        requester = str(request.get("requesterRunId") or "anonymous")
        content_version = str(request.get("contentVersion") or "unknown")
        combat_version = str(request.get("combatVersion") or "battle_v1")
        raw_excluded = request.get("excludeSnapshotIds") or []
        if not isinstance(raw_excluded, list) or len(raw_excluded) > 100:
            raise ValueError("excludeSnapshotIds must be an array of at most 100 IDs")
        excluded = [str(value) for value in raw_excluded if value]
        clauses = [
            "round = ?", "content_version = ?", "combat_version = ?", "status = 'active'", "source_run_id <> ?",
        ]
        parameters: list[Any] = [round_number, content_version, combat_version, requester]
        if excluded:
            clauses.append("snapshot_id NOT IN (%s)" % ",".join("?" for _ in excluded))
            parameters.extend(excluded)
        sql = "SELECT snapshot_id, payload FROM snapshots WHERE " + " AND ".join(clauses) + " ORDER BY RANDOM() LIMIT 1"
        with self.lock, closing(self.connect()) as connection:
            self.record_metric(connection, round_number, content_version, combat_version, "match_requests")
            while True:
                row = connection.execute(sql, parameters).fetchone()
                if not row:
                    self.record_metric(connection, round_number, content_version, combat_version, "empty_match_requests")
                    connection.commit()
                    return None
                try:
                    self.validate_lineup(normalize_snapshot(json.loads(row["payload"])))
                    break
                except (ValueError, TypeError, KeyError):
                    # Preserve old data for inspection, but never serve an illegal lineup.
                    connection.execute("UPDATE snapshots SET status = 'invalid_lineup' WHERE snapshot_id = ?",
                                       (row["snapshot_id"],))
            match_id = f"match_{uuid.uuid4().hex}"
            connection.execute("UPDATE snapshots SET matched_count = matched_count + 1 WHERE snapshot_id = ?", (row["snapshot_id"],))
            connection.execute(
                "INSERT INTO matches(match_id, requester_run_id, snapshot_id, round, created_at_utc) VALUES (?, ?, ?, ?, ?)",
                (match_id, requester, row["snapshot_id"], round_number, utc_now()),
            )
            connection.commit()
        return {"matchId": match_id, "snapshot": json.loads(row["payload"])}

    def validate_lineup(self, snapshot: dict[str, Any]) -> None:
        occupied = set()
        for unit in snapshot["units"]:
            unit_id, anchor = unit["unitId"], unit["slotId"]
            if unit_id not in self.unit_sizes:
                raise ValueError("unknown unitId")
            if anchor not in self.board_slots:
                raise ValueError("invalid board slot")
            footprint = [anchor]
            if self.unit_sizes[unit_id] == 2:
                row, column = map(int, anchor.split("-"))
                footprint.append(f"{row}-{column - 1}")
            for slot in footprint:
                if slot not in self.board_slots or slot in occupied:
                    raise ValueError("unit footprint overlaps or leaves the board")
                occupied.add(slot)

    def report(self, report: dict[str, Any]) -> bool:
        if not isinstance(report, dict):
            raise ValueError("result must be an object")
        for field in ("round", "hpDelta", "playerScore", "enemyScore"):
            if type(report.get(field)) is not int:
                raise ValueError(f"{field} must be an integer")
        if type(report.get("victory")) is not bool:
            raise ValueError("victory must be boolean")
        with self.lock, closing(self.connect()) as connection:
            match = connection.execute("SELECT * FROM matches WHERE match_id = ?",
                                       (report.get("matchId"),)).fetchone()
            if match is None:
                raise ValueError("unknown matchId")
            if (report.get("snapshotId") != match["snapshot_id"]
                    or report.get("requesterRunId") != match["requester_run_id"]
                    or report["round"] != match["round"]):
                raise ValueError("result does not belong to this match")
            existing = connection.execute("SELECT payload FROM battle_results WHERE match_id = ? ORDER BY id LIMIT 1",
                                          (report["matchId"],)).fetchone()
            if existing:
                previous = json.loads(existing["payload"])
                fields = ("snapshotId", "requesterRunId", "round", "victory", "hpDelta", "playerScore", "enemyScore")
                if any(previous.get(field) != report.get(field) for field in fields):
                    raise ValueError("match result has already been recorded with different values")
                return False
            connection.execute(
                """INSERT INTO battle_results
                (match_id, snapshot_id, requester_run_id, round, victory, hp_delta,
                 player_score, enemy_score, reported_at_utc, payload)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    report.get("matchId"), report.get("snapshotId"), report.get("requesterRunId"),
                    int(report.get("round") or 0), 1 if report.get("victory") else 0,
                    int(report.get("hpDelta") or 0), int(report.get("playerScore") or 0),
                    int(report.get("enemyScore") or 0), report.get("reportedAtUtc") or utc_now(),
                    json.dumps(report, ensure_ascii=False, separators=(",", ":")),
                ),
            )
            connection.commit()
        return True

    def stats(self) -> dict[str, Any]:
        with closing(self.connect()) as connection:
            rows = connection.execute(
                """WITH pool AS (
                       SELECT round, content_version, combat_version, COUNT(*) AS pool_size
                       FROM snapshots WHERE status = 'active'
                       GROUP BY round, content_version, combat_version
                   ), buckets AS (
                       SELECT round, content_version, combat_version FROM pool_metrics
                       UNION
                       SELECT round, content_version, combat_version FROM pool
                   )
                   SELECT buckets.round, buckets.content_version, buckets.combat_version,
                          COALESCE(pool.pool_size, 0) AS pool_size,
                          COALESCE(metrics.snapshot_uploads, 0) AS snapshot_uploads,
                          COALESCE(metrics.duplicate_uploads, 0) AS duplicate_uploads,
                          COALESCE(metrics.match_requests, 0) AS match_requests,
                          COALESCE(metrics.empty_match_requests, 0) AS empty_match_requests
                   FROM buckets
                   LEFT JOIN pool_metrics AS metrics ON metrics.round = buckets.round
                       AND metrics.content_version = buckets.content_version
                       AND metrics.combat_version = buckets.combat_version
                   LEFT JOIN pool ON pool.round = buckets.round
                       AND pool.content_version = buckets.content_version
                       AND pool.combat_version = buckets.combat_version
                   ORDER BY buckets.combat_version, buckets.content_version, buckets.round"""
            ).fetchall()
        buckets = []
        for row in rows:
            uploads = row["snapshot_uploads"] + row["duplicate_uploads"]
            requests = row["match_requests"]
            buckets.append({
                "round": row["round"],
                "contentVersion": row["content_version"],
                "combatVersion": row["combat_version"],
                "poolSize": row["pool_size"],
                "uploads": uploads,
                "duplicateUploads": row["duplicate_uploads"],
                "duplicateRate": row["duplicate_uploads"] / uploads if uploads else 0,
                "matchRequests": requests,
                "emptyMatchRequests": row["empty_match_requests"],
                "fallbackRate": row["empty_match_requests"] / requests if requests else 0,
            })
        return {"buckets": buckets}

    def import_jsonl(self, path: Path, content_version: str) -> dict[str, int]:
        result = {"lines": 0, "imported": 0, "duplicate": 0, "rejected": 0}
        if not path.exists():
            return result
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                result["lines"] += 1
                try:
                    raw = json.loads(line)
                    if not isinstance(raw, dict):
                        raise ValueError("log row must be an object")
                    if raw.get("type") != "battle" or not raw.get("playerUnits"):
                        continue
                    converted = {
                        "sourceRunId": f"legacy_{hashlib.sha1(line.encode('utf-8')).hexdigest()[:12]}",
                        "sourceType": "legacy_player",
                        "displayName": "训练镜像",
                        "round": raw.get("round"),
                        "contentVersion": content_version,
                        "combatVersion": "battle_v1",
                        "powerScore": raw.get("playerScore", 0),
                        "units": [
                            {
                                "unitId": unit.get("id"), "name": unit.get("name"), "slotId": unit.get("slot"),
                                "star": unit.get("star", 1), "count": unit.get("count", 1),
                                "maxHp": unit.get("maxHp", 0), "attack": unit.get("attack", 0),
                                "defense": unit.get("defense", 0), "power": unit.get("power", 0),
                                "speed": unit.get("speed", 0),
                            }
                            for unit in raw.get("playerUnits") or []
                        ],
                    }
                    _, created = self.add_snapshot(converted, "legacy_player")
                    result["imported" if created else "duplicate"] += 1
                except (ValueError, TypeError, AttributeError, json.JSONDecodeError, sqlite3.Error):
                    result["rejected"] += 1
        return result


class ApiHandler(BaseHTTPRequestHandler):
    database: MirrorDatabase

    def log_message(self, fmt: str, *args: Any) -> None:
        print(f"[{self.log_date_time_string()}] {fmt % args}")

    def do_GET(self) -> None:  # noqa: N802
        if self.path == "/health":
            self.send_json(200, {"ok": True, "service": "prophecy-century-casual-pvp", "timeUtc": utc_now()})
        elif self.path == "/api/v1/pool/stats":
            self.send_json(200, self.database.stats())
        else:
            self.send_json(404, {"error": "not found"})

    def do_POST(self) -> None:  # noqa: N802
        try:
            body = self.read_json()
            if self.path == "/api/v1/snapshots":
                snapshot, created = self.database.add_snapshot(body)
                self.send_json(201 if created else 200, {"success": True, "created": created, "snapshot": snapshot})
            elif self.path == "/api/v1/matches":
                matched = self.database.match(body)
                if matched is None:
                    self.send_json(404, {"success": False, "error": "same-round pool is empty"})
                else:
                    self.send_json(200, {"success": True, "fallback": False, **matched})
            elif self.path.startswith("/api/v1/matches/") and self.path.endswith("/result"):
                route_match_id = self.path[len("/api/v1/matches/"):-len("/result")]
                if not route_match_id or "/" in route_match_id or body.get("matchId") != route_match_id:
                    raise ValueError("URL matchId must match result matchId")
                created = self.database.report(body)
                self.send_json(200, {"success": True, "created": created})
            else:
                self.send_json(404, {"error": "not found"})
        except (ValueError, TypeError, json.JSONDecodeError) as exc:
            self.send_json(400, {"success": False, "error": str(exc)})
        except sqlite3.Error as exc:
            self.send_json(500, {"success": False, "error": f"database error: {exc}"})

    def read_json(self) -> dict[str, Any]:
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0 or length > 1_000_000:
            raise ValueError("invalid request size")
        value = json.loads(self.rfile.read(length).decode("utf-8"))
        if not isinstance(value, dict):
            raise ValueError("request body must be an object")
        return value

    def send_json(self, status: int, value: dict[str, Any]) -> None:
        payload = json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)


def main() -> None:
    parser = argparse.ArgumentParser(description="Prophecy Century local Casual PVP mirror server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--database", type=Path, default=Path(__file__).with_name("data") / "casual_pvp.db")
    parser.add_argument("--import-log", type=Path)
    parser.add_argument("--content-version", default="unknown")
    args = parser.parse_args()
    database = MirrorDatabase(args.database)
    if args.import_log:
        print("Import:", json.dumps(database.import_jsonl(args.import_log, args.content_version), ensure_ascii=False))
    ApiHandler.database = database
    server = ThreadingHTTPServer((args.host, args.port), ApiHandler)
    print(f"Casual PVP server listening on http://{args.host}:{args.port}")
    print(f"Database: {args.database.resolve()}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
