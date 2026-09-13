import shutil
import sqlite3
import unittest
import uuid
from pathlib import Path

from backup_database import create_backup, restore_backup
from server import MirrorDatabase
from test_server import TEST_TEMP_ROOT, snapshot


class BackupOperationsTests(unittest.TestCase):
    def test_verified_backup_can_restore_a_mirror_pool(self) -> None:
        root = TEST_TEMP_ROOT / f"casual_pvp_backup_{uuid.uuid4().hex}"
        database_path = root / "live" / "pool.sqlite3"
        restored_path = root / "restored" / "pool.sqlite3"
        try:
            database = MirrorDatabase(database_path)
            database.add_snapshot(snapshot("backup-source", "run_backup", 4, "2-1", 8))
            result = create_backup(database_path, root / "backups")
            self.assertEqual("ok", result["integrity"])
            backup_path = Path(result["backup"])
            self.assertTrue(backup_path.is_file())
            restored = restore_backup(backup_path, restored_path)
            self.assertEqual("ok", restored["integrity"])
            with sqlite3.connect(restored_path) as connection:
                self.assertEqual(1, connection.execute("SELECT COUNT(*) FROM snapshots").fetchone()[0])
                self.assertEqual("ok", connection.execute("PRAGMA integrity_check").fetchone()[0])
        finally:
            shutil.rmtree(root, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
