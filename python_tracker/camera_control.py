"""Named webcam discovery and session-scoped control, independent of Unity scenes."""
import hashlib
import json
import os
from pathlib import Path
import time
import uuid

import cv2
from cv2_enumerate_cameras import enumerate_cameras

BACKENDS = {"dshow": cv2.CAP_DSHOW, "msmf": cv2.CAP_MSMF}


def discover_cameras():
    devices = []
    for backend, api in BACKENDS.items():
        try:
            for camera in enumerate_cameras(api):
                identity = f"{backend}:{camera.path or camera.name}:{camera.index}"
                devices.append({"id": hashlib.sha256(identity.encode()).hexdigest()[:24],
                                "index": camera.index, "backend": backend,
                                "name": camera.name, "path": camera.path})
        except (OSError, RuntimeError):
            continue
    return devices


def select_camera(devices, index=None, backend="dshow", preference=None):
    if index is None and preference:
        # Resolve a stable device path/name again; a stored OpenCV index can change.
        for field in ("path", "name"):
            value = preference.get(field)
            matches = [d for d in devices if value and d[field] == value
                       and d["backend"] == preference.get("backend")]
            if len(matches) == 1:
                return matches[0]
    index = 0 if index is None else index
    for device in devices:
        if device["index"] == index and device["backend"] == backend:
            return device
    raise ValueError(f"Camera {index} ({backend}) is not listed. Use --list-cameras to see device names.")


def read_json(path):
    try:
        value = json.loads(Path(path).read_text(encoding="utf-8-sig"))
        return value if isinstance(value, dict) else {}
    except (OSError, ValueError, TypeError):
        return {}


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, allow_nan=False), encoding="utf-8")
    os.replace(temporary, path)


class CameraControl:
    """Old sessions cannot change a new session's camera; refresh never opens webcams."""
    def __init__(self, directory=None, discover=discover_cameras):
        self.directory = Path(directory) if directory else None
        self.discover = discover
        self.devices = discover()
        self.session = uuid.uuid4().hex
        self.last_request = None

    def preference(self):
        return read_json(self.directory / "tracker_camera_preference.json") if self.directory else {}

    def request(self):
        if not self.directory:
            return None
        request = read_json(self.directory / "tracker_camera_request.json")
        revision = request.get("revision")
        if request.get("session") != self.session or not revision or revision == self.last_request:
            return None
        self.last_request = revision
        if request.get("action") == "refresh":
            self.devices = self.discover()
            return {"action": "refresh"}
        if request.get("action") == "select":
            self.devices = self.discover()
            selected = next((d for d in self.devices if d["id"] == request.get("deviceId")), None)
            return {"action": "select", "device": selected}
        return None

    def publish(self, selected, status, error="", width=0, height=0, blank=False):
        if self.directory:
            write_json(self.directory / "tracker_camera_status.json", {
                "session": self.session, "updatedUnix": time.time(), "devices": self.devices,
                "selected": selected, "status": status, "error": error,
                "frameWidth": width, "frameHeight": height, "blankFrames": blank})

    def remember(self, device):
        if self.directory:
            write_json(self.directory / "tracker_camera_preference.json", device)
