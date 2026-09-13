# Casual PVP Local Server

Start the loopback-only development server:

```powershell
python tools/casual_pvp_server/server.py
```

Import the existing Unity battle log while starting:

```powershell
python tools/casual_pvp_server/server.py `
  --import-log "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\prophecy_century\player_state_log.jsonl" `
  --content-version 1.0
```

`--content-version` must match Unity's `Application.version` (currently `1.0`), otherwise those imported mirrors are intentionally excluded from matchmaking.

Default endpoint: `http://127.0.0.1:8765`. The SQLite database is created under `tools/casual_pvp_server/data/` and is intentionally ignored by Git.

Uploads and log imports are checked against `Assets/Resources/Data/unit_data.json`
and `unity_game_config.json`. Keep these files alongside the server in the same
repository layout. Missing or malformed catalog files prevent startup. Restart
the server after changing the catalog. Unknown units, nonexistent board slots,
overlapping footprints and footprints extending outside the board are rejected.
Board occupancy follows Unity's `size == 2` rule (anchor plus the cell to its
left), not `sizeTier`. All 72 currently shipped units have `size == 1`.

Matching also revalidates historical candidates. Invalid candidates are retained
with `status = 'invalid_lineup'` and excluded from subsequent matching; a valid
remaining candidate can still be selected. This is structural validation, not
proof that a client's submitted combat statistics were legitimately earned.

Endpoints:

- `GET /health`
- `GET /api/v1/pool/stats` — reports each version/round bucket's active pool size,
  duplicate upload rate, and empty-match rate (the rate that makes the client use its fallback).
- `POST /api/v1/snapshots`
- `POST /api/v1/matches`
- `POST /api/v1/matches/{matchId}/result`

The Unity client can continue with its local same-round fallback pool when this service is not running.

New Unity snapshots use `battle_v2`, including team gift totals, unit gem counters,
and battle progress counters. Imported historical logs retain `battle_v1` because
they lack these fields; they remain stored but cannot seed the v2 matching pool.
Use newly captured Unity snapshots to populate that pool. Content versions must
also match. Existing locked opponents are retained by the client's save recovery.

Run the database and loopback HTTP regression checks:

```powershell
python tools/casual_pvp_server/test_server.py
```

Identical lineups are deduplicated within their content/combat version and round.
Existing database hashes are upgraded on startup without deleting the mirror pool.
Result uploads must refer to an existing match and agree with its requester, snapshot,
and round. Retrying the same result is accepted without adding another record;
conflicting results are rejected. Local-only matches are not server match records.
The client persists pending results in the corresponding save slot with the original
server endpoint, and retries outside battle playback. A successful response must
include `success: true`, including for duplicate submissions.

For persistent deployment, backup/restore, HTTPS proxy placement, and the
networked-singleplayer acceptance procedure, see
`docs/markdown/CASUAL_ASYNC_PVP_DEPLOYMENT.md`.

For Unity recovery plus real HTTP integration tests, run from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate_casual_pvp.ps1
```

This compiles the runtime using the main project's existing compiler response file,
then runs an isolated Unity project and `integration_fixture.py` on a random
loopback port with a fresh test database. It covers service failures, lost receipts,
save switching, original-endpoint retries, unavailable-server fallback, and opponent
confirmation button behavior. Fixture-only fault endpoints are not part of `server.py`.
