import unittest
import uuid
import copy
import json
import threading
from contextlib import closing
from http.client import HTTPConnection
from http.server import ThreadingHTTPServer
from pathlib import Path

from server import ApiHandler, MirrorDatabase, normalize_snapshot

TEST_TEMP_ROOT = Path(__file__).resolve().parents[2] / "Temp"
TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)


def snapshot(snapshot_id: str, run_id: str, round_number: int, slot: str, attack: int) -> dict:
    return {
        "snapshotId": snapshot_id,
        "sourceRunId": run_id,
        "sourceType": "player",
        "displayName": run_id,
        "round": round_number,
        "contentVersion": "1.0",
        "combatVersion": "battle_v1",
        "powerScore": attack,
        "units": [
            {
                "unitId": "small_merchant",
                "name": "Test Unit",
                "slotId": slot,
                "star": 1,
                "count": 1,
                "maxHp": 10,
                "attack": attack,
                "defense": 2,
                "power": 1,
                "speed": 3,
            }
        ],
    }


class MirrorDatabaseTests(unittest.TestCase):
    def test_same_round_match_excludes_requester_and_recent_snapshot(self) -> None:
        database_path = TEST_TEMP_ROOT / f"casual_pvp_test_{uuid.uuid4().hex}.sqlite3"
        try:
            database = MirrorDatabase(database_path)
            database.add_snapshot(snapshot("own", "run_a", 6, "2-1", 4))
            database.add_snapshot(snapshot("candidate", "run_b", 6, "3-1", 5))
            database.add_snapshot(snapshot("wrong_round", "run_c", 7, "4-1", 6))

            matched = database.match(
                {
                    "round": 6,
                    "requesterRunId": "run_a",
                    "contentVersion": "1.0",
                    "combatVersion": "battle_v1",
                    "excludeSnapshotIds": [],
                }
            )
            self.assertIsNotNone(matched)
            self.assertEqual("run_b", matched["snapshot"]["sourceRunId"])
            self.assertEqual(6, matched["snapshot"]["round"])

            excluded = database.match(
                {
                    "round": 6,
                    "requesterRunId": "run_a",
                    "contentVersion": "1.0",
                    "combatVersion": "battle_v1",
                    "excludeSnapshotIds": ["candidate"],
                }
            )
            self.assertIsNone(excluded)
        finally:
            self.remove_database(database_path)

    def test_duplicate_lineup_is_not_inserted_twice(self) -> None:
        database_path = TEST_TEMP_ROOT / f"casual_pvp_test_{uuid.uuid4().hex}.sqlite3"
        try:
            database = MirrorDatabase(database_path)
            _, first_created = database.add_snapshot(snapshot("one", "run_a", 3, "2-1", 4))
            _, second_created = database.add_snapshot(snapshot("two", "run_b", 3, "2-1", 4))
            self.assertTrue(first_created)
            self.assertFalse(second_created)
        finally:
            self.remove_database(database_path)

    def test_debug_assisted_snapshot_is_rejected(self) -> None:
        database_path = TEST_TEMP_ROOT / f"casual_pvp_test_{uuid.uuid4().hex}.sqlite3"
        try:
            database = MirrorDatabase(database_path)
            with self.assertRaisesRegex(ValueError, "debug-assisted"):
                database.add_snapshot(dict(snapshot("debug", "run_debug", 3, "2-1", 4), isCheatRun=True))
        finally:
            self.remove_database(database_path)

    def test_pool_stats_are_partitioned_and_track_duplicate_and_fallback_rates(self) -> None:
        database_path = TEST_TEMP_ROOT / f"casual_pvp_test_{uuid.uuid4().hex}.sqlite3"
        try:
            database = MirrorDatabase(database_path)
            database.add_snapshot(snapshot("one", "run_a", 3, "2-1", 4))
            database.add_snapshot(snapshot("duplicate", "run_b", 3, "2-1", 4))
            database.match({"round": 3, "requesterRunId": "run_a", "contentVersion": "1.0",
                            "combatVersion": "battle_v1", "excludeSnapshotIds": ["one"]})
            bucket = database.stats()["buckets"][0]
            self.assertEqual(3, bucket["round"])
            self.assertEqual(1, bucket["poolSize"])
            self.assertEqual(2, bucket["uploads"])
            self.assertEqual(1, bucket["duplicateUploads"])
            self.assertEqual(0.5, bucket["duplicateRate"])
            self.assertEqual(1, bucket["matchRequests"])
            self.assertEqual(1, bucket["emptyMatchRequests"])
            self.assertEqual(1, bucket["fallbackRate"])
        finally:
            self.remove_database(database_path)

    @staticmethod
    def remove_database(database_path: Path) -> None:
        for path in (database_path, Path(str(database_path) + "-wal"), Path(str(database_path) + "-shm")):
            if path.exists():
                path.unlink()


class MirrorRegressionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.path = TEST_TEMP_ROOT / f"casual_pvp_regression_{uuid.uuid4().hex}.sqlite3"
        self.addCleanup(MirrorDatabaseTests.remove_database, self.path)
        self.database = MirrorDatabase(self.path)
        self.candidate = snapshot("candidate", "opponent", 6, "2-1", 10)
        self.request = {"round": 6, "requesterRunId": "requester", "contentVersion": "1.0",
                        "combatVersion": "battle_v1", "excludeSnapshotIds": []}

    def make_report(self) -> dict:
        self.database.add_snapshot(self.candidate)
        matched = self.database.match(self.request)
        return {"matchId": matched["matchId"], "snapshotId": "candidate", "requesterRunId": "requester",
                "round": 6, "victory": True, "hpDelta": 0, "playerScore": 20, "enemyScore": 10}

    def test_catalog_and_full_footprint_validation(self) -> None:
        # Current catalog has only size=1. Exercise the supported size=2 rule
        # with a test-only catalog entry, without changing production balance.
        large_id = "test_large_unit"
        self.database.unit_sizes[large_id] = 2
        large = copy.deepcopy(self.candidate)
        large["units"][0].update(unitId=large_id, slotId="2-2")
        self.assertTrue(self.database.add_snapshot(large)[1])
        for unit_id, slot in (("missing_unit", "2-1"), ("small_merchant", "5-1"), (large_id, "1-1")):
            bad = copy.deepcopy(self.candidate)
            bad["units"][0].update(unitId=unit_id, slotId=slot)
            with self.subTest(unit=unit_id, slot=slot), self.assertRaises(ValueError):
                self.database.add_snapshot(bad)
        overlap = copy.deepcopy(large)
        overlap["units"].append(self.candidate["units"][0])
        with self.assertRaises(ValueError):
            self.database.add_snapshot(overlap)

    def test_invalid_legacy_pool_is_quarantined_without_deletion(self) -> None:
        self.database.add_snapshot(self.candidate)
        bad = copy.deepcopy(self.candidate)
        bad["units"][0]["unitId"] = "missing_unit"
        with closing(self.database.connect()) as connection:
            connection.execute("UPDATE snapshots SET payload = ?", (json.dumps(bad),))
            connection.commit()
        self.assertIsNone(self.database.match(self.request))
        with closing(self.database.connect()) as connection:
            row = connection.execute("SELECT status, payload FROM snapshots").fetchone()
            self.assertEqual("invalid_lineup", row["status"])
            self.assertEqual(bad, json.loads(row["payload"]))
        valid = dict(self.candidate, snapshotId="valid")
        valid = copy.deepcopy(valid)
        valid["units"][0]["attack"] += 1
        self.database.add_snapshot(valid)
        self.assertEqual("valid", self.database.match(self.request)["snapshot"]["snapshotId"])

    def test_content_versions_keep_separate_identical_lineups(self) -> None:
        self.database.add_snapshot(self.candidate)
        updated = dict(self.candidate, snapshotId="new_version", contentVersion="2.0")
        self.assertTrue(self.database.add_snapshot(updated)[1])
        for version, expected in (("1.0", "candidate"), ("2.0", "new_version")):
            matched = self.database.match(dict(self.request, contentVersion=version))
            self.assertEqual(expected, matched["snapshot"]["snapshotId"])
        self.assertIsNone(self.database.match(dict(self.request, combatVersion="battle_v2")))

    def test_hash_upgrade_preserves_existing_pool_and_is_repeatable(self) -> None:
        self.database.add_snapshot(self.candidate)
        with closing(self.database.connect()) as connection:
            connection.execute("UPDATE snapshots SET dedupe_hash = 'old_hash'")
            connection.commit()
        for _ in range(2):
            reopened = MirrorDatabase(self.path)
            self.assertFalse(reopened.add_snapshot(self.candidate)[1])
            self.assertEqual("candidate", reopened.match(self.request)["snapshot"]["snapshotId"])

    def test_reused_snapshot_id_cannot_replace_lineup(self) -> None:
        self.database.add_snapshot(self.candidate)
        changed = copy.deepcopy(self.candidate)
        changed["units"][0]["attack"] += 1
        with self.assertRaises(ValueError):
            self.database.add_snapshot(changed)
        self.assertEqual(10, self.database.match(self.request)["snapshot"]["units"][0]["attack"])

    def test_report_retry_records_only_once(self) -> None:
        report = self.make_report()
        self.assertTrue(self.database.report(report))
        self.assertFalse(self.database.report(dict(report, reportedAtUtc="retry time")))
        with closing(self.database.connect()) as connection:
            self.assertEqual(1, connection.execute("SELECT COUNT(*) FROM battle_results").fetchone()[0])

    def test_conflicting_retry_is_rejected(self) -> None:
        report = self.make_report()
        self.database.report(report)
        with self.assertRaises(ValueError):
            self.database.report(dict(report, victory=False))

    def test_report_must_belong_to_existing_match(self) -> None:
        report = self.make_report()
        for field, value in (("matchId", "missing"), ("snapshotId", "other"),
                             ("requesterRunId", "other"), ("round", 7)):
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.database.report(dict(report, **{field: value}))

    def test_bad_units_rejected_without_silently_dropping_them(self) -> None:
        bad_cases = [None, [], {"units": [None]}, dict(self.candidate, schemaVersion=2),
                     dict(self.candidate, units=self.candidate["units"] * 2)]
        for field, value in (("unitId", ""), ("slotId", ""), ("attack", -1),
                             ("count", "many"), ("isGolden", "false")):
            changed = copy.deepcopy(self.candidate)
            changed["units"][0][field] = value
            bad_cases.append(changed)
        for bad in bad_cases:
            with self.subTest(value=bad), self.assertRaises(ValueError):
                normalize_snapshot(bad)

    def test_malformed_match_requests_rejected(self) -> None:
        for bad in ([], dict(self.request, round=0), dict(self.request, excludeSnapshotIds="candidate")):
            with self.subTest(value=bad), self.assertRaises(ValueError):
                self.database.match(bad)

    def test_bad_log_rows_do_not_abort_following_valid_rows(self) -> None:
        log_path = self.path.with_suffix(".jsonl")
        self.addCleanup(log_path.unlink)
        valid = {"type": "battle", "round": 6,
                 "playerUnits": [{"id": "small_merchant", "slot": "2-1", "count": 1}]}
        log_path.write_text("null\n[]\n" + json.dumps(valid) + "\n", encoding="utf-8")
        result = self.database.import_jsonl(log_path, "1.0")
        self.assertEqual(2, result["rejected"])
        self.assertEqual(1, result["imported"])

    def test_http_validation_and_result_route(self) -> None:
        report = self.make_report()

        class TestHandler(ApiHandler):
            database = self.database

            def log_message(self, *args) -> None:
                pass

        server = ThreadingHTTPServer(("127.0.0.1", 0), TestHandler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            cases = [("/api/v1/snapshots", [], 400),
                     ("/api/v1/snapshots", {"units": [None]}, 400),
                     ("/api/v1/matches/wrong/result", report, 400),
                     (f"/api/v1/matches/{report['matchId']}/result", report, 200)]
            for path, body, expected in cases:
                with self.subTest(path=path), closing(HTTPConnection(*server.server_address, timeout=5)) as client:
                    client.request("POST", path, json.dumps(body), {"Content-Type": "application/json"})
                    response = client.getresponse()
                    self.assertEqual(expected, response.status)
                    self.assertIn("success", json.loads(response.read()))
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)

    def test_v2_combat_counters_roundtrip_and_team_dedupe(self) -> None:
        value = copy.deepcopy(self.candidate)
        value.update(combatVersion="battle_v2", teamForestGiftTotal=23)
        value["units"][0].update(forestGemsReceived=11, forestGemsAttached=9,
                                 battleProgressCounters=[{"key": "kill_progress", "value": 3}])
        saved, created = self.database.add_snapshot(value)
        self.assertTrue(created)
        self.assertEqual(23, saved["teamForestGiftTotal"])
        matched = self.database.match(dict(self.request, combatVersion="battle_v2"))["snapshot"]
        self.assertEqual(11, matched["units"][0]["forestGemsReceived"])
        self.assertEqual(9, matched["units"][0]["forestGemsAttached"])
        self.assertEqual([{"key": "kill_progress", "value": 3}], matched["units"][0]["battleProgressCounters"])
        self.assertTrue(self.database.add_snapshot(dict(value, snapshotId="different_team", teamForestGiftTotal=24))[1])
        self.assertIsNone(self.database.match(self.request))

    def test_v2_rejects_missing_or_invalid_counters(self) -> None:
        value = copy.deepcopy(self.candidate)
        value["combatVersion"] = "battle_v2"
        with self.assertRaises(ValueError):
            normalize_snapshot(value)
        value["teamForestGiftTotal"] = 0
        with self.assertRaises(ValueError):
            normalize_snapshot(value)
        value["units"][0].update(forestGemsReceived=0, forestGemsAttached=0,
                                 battleProgressCounters=[{"key": "test", "value": -1}])
        with self.assertRaises(ValueError):
            normalize_snapshot(value)


if __name__ == "__main__":
    unittest.main()
