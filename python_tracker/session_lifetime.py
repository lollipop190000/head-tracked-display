"""Bound a launcher's webcam session, including when its console is forcibly closed."""
from pathlib import Path
import time


class SessionLifetime:
    def __init__(self, path=None, timeout_seconds=8.0):
        self.path = Path(path) if path else None
        self.timeout = timeout_seconds
        self.next_check = 0.0
        self.active = True

    def is_active(self):
        if self.path is None:
            return True
        now = time.monotonic()
        if now >= self.next_check:
            self.next_check = now + .25
            try:
                self.active = time.time() - self.path.stat().st_mtime <= self.timeout
            except OSError:
                self.active = False
        return self.active
