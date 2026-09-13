"""Loopback-only fault injection around the real PVP database/API for Unity tests."""
import argparse
import copy
import json
import socket
import time
from contextlib import closing
from http.server import ThreadingHTTPServer
from pathlib import Path

from server import ApiHandler, MirrorDatabase


class SeedDatabase(MirrorDatabase):
    def add_snapshot(self, raw, source_type=None):
        result = super().add_snapshot(raw, source_type)
        other = copy.deepcopy(raw)
        other["snapshotId"] = "fixture_" + raw["snapshotId"]
        other["sourceRunId"] = "fixture_opponent"
        other["units"][0]["attack"] += 1
        super().add_snapshot(other, "player")
        return result


class FixtureHandler(ApiHandler):
    mode = "normal"

    def do_GET(self):
        if self.path == "/test/state":
            with closing(self.database.connect()) as connection:
                count = connection.execute("SELECT COUNT(*) FROM battle_results").fetchone()[0]
            self.send_json(200, {"count": count})
        else:
            super().do_GET()

    def do_POST(self):
        if self.path == "/test/control":
            FixtureHandler.mode = self.read_json()["mode"]
            self.send_json(200, {"success": True})
            return
        mode = FixtureHandler.mode
        if self.path.endswith("/result"):
            if mode == "unavailable":
                self.read_json()
                self.send_json(503, {"success": False})
                return
            if mode == "drop_ack":
                FixtureHandler.mode = "normal"
                self.database.report(self.read_json())
                self.connection.shutdown(socket.SHUT_RDWR)
                self.connection.close()
                return
            if mode == "delay_result":
                time.sleep(0.4)
        if self.path == "/api/v1/snapshots" and mode == "delay_upload":
            time.sleep(0.4)
        super().do_POST()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--ready-file", type=Path, required=True)
    parser.add_argument("--database", type=Path, required=True)
    args = parser.parse_args()
    FixtureHandler.database = SeedDatabase(args.database)
    with ThreadingHTTPServer(("127.0.0.1", 0), FixtureHandler) as server:
        args.ready_file.write_text(json.dumps({"endpoint": f"http://127.0.0.1:{server.server_port}"}), encoding="utf-8")
        server.serve_forever()
