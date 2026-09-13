#!/usr/bin/env python3
"""Create and restore verified SQLite backups for the Casual PVP mirror pool."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sqlite3
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path


def integrity_check(path: Path) -> str:
    with closing(sqlite3.connect(path)) as connection:
        return str(connection.execute("PRAGMA integrity_check").fetchone()[0])


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def create_backup(database: Path, output_dir: Path) -> dict[str, object]:
    """Use SQLite's backup API so a live service can be backed up safely."""
    database, output_dir = Path(database), Path(output_dir)
    if not database.is_file():
        raise FileNotFoundError(f"database not found: {database}")
    if integrity_check(database) != "ok":
        raise ValueError("source database integrity check failed")
    output_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    destination = output_dir / f"casual_pvp_{stamp}.sqlite3"
    suffix = 1
    while destination.exists():
        destination = output_dir / f"casual_pvp_{stamp}_{suffix}.sqlite3"
        suffix += 1
    with closing(sqlite3.connect(database)) as source, closing(sqlite3.connect(destination)) as target:
        source.backup(target)
        target.commit()
    check = integrity_check(destination)
    if check != "ok":
        destination.unlink(missing_ok=True)
        raise ValueError(f"backup integrity check failed: {check}")
    return {"backup": str(destination.resolve()), "sha256": sha256(destination), "integrity": check,
            "createdAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")}


def restore_backup(backup: Path, database: Path) -> dict[str, object]:
    """Restore through SQLite, leaving the original untouched until validation succeeds."""
    backup, database = Path(backup), Path(database)
    if not backup.is_file():
        raise FileNotFoundError(f"backup not found: {backup}")
    if integrity_check(backup) != "ok":
        raise ValueError("backup integrity check failed")
    database.parent.mkdir(parents=True, exist_ok=True)
    temporary = database.with_suffix(database.suffix + ".restore")
    temporary.unlink(missing_ok=True)
    with closing(sqlite3.connect(backup)) as source, closing(sqlite3.connect(temporary)) as target:
        source.backup(target)
        target.commit()
    if integrity_check(temporary) != "ok":
        temporary.unlink(missing_ok=True)
        raise ValueError("restored database integrity check failed")
    shutil.move(str(temporary), str(database))
    return {"database": str(database.resolve()), "sha256": sha256(database), "integrity": "ok"}


def main() -> None:
    parser = argparse.ArgumentParser(description="Back up or restore the Casual PVP SQLite pool")
    parser.add_argument("--database", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--restore", type=Path, help="restore this backup into --database")
    parser.add_argument("--confirm-restore", action="store_true", help="required because restore replaces --database")
    args = parser.parse_args()
    if args.restore:
        if not args.confirm_restore:
            parser.error("--restore requires --confirm-restore")
        result = restore_backup(args.restore, args.database)
    else:
        if args.output_dir is None:
            parser.error("--output-dir is required when creating a backup")
        result = create_backup(args.database, args.output_dir)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    main()
