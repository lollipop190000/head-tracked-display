from pathlib import Path
import os
import sys
import tempfile
import time
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from session_lifetime import SessionLifetime


class SessionLifetimeTests(unittest.TestCase):
    def test_manual_tracker_has_no_launcher_deadline(self):
        self.assertTrue(SessionLifetime().is_active())

    def test_missing_deleted_or_stale_launcher_ends_tracking(self):
        with tempfile.TemporaryDirectory() as directory:
            marker = Path(directory) / 'session'
            self.assertFalse(SessionLifetime(marker).is_active())
            marker.touch()
            self.assertTrue(SessionLifetime(marker).is_active())
            stale = time.time() - 30
            os.utime(marker, (stale, stale))
            self.assertFalse(SessionLifetime(marker).is_active())
            marker.unlink()
            self.assertFalse(SessionLifetime(marker).is_active())
