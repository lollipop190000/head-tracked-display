from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from camera_control import CameraControl, read_json, select_camera, write_json


def device(name, index, backend="dshow"):
    return {"id": name + backend, "name": name, "path": "device/" + name, "index": index, "backend": backend}


class CameraControlTests(unittest.TestCase):
    def test_preference_follows_device_when_indices_change_but_explicit_index_wins(self):
        original = device("EShare", 1)
        current = [device("EShare", 0), device("Laptop", 1)]
        self.assertEqual(select_camera(current, preference=original)["name"], "EShare")
        self.assertEqual(select_camera(current, index=1, preference=original)["name"], "Laptop")
        with self.assertRaises(ValueError):
            select_camera(current, index=8)

    def test_backend_indices_are_not_interchangeable(self):
        devices = [device("Laptop", 0), device("EShare", 0, "msmf")]
        self.assertEqual(select_camera(devices, 0, "msmf")["name"], "EShare")

    def test_stale_session_and_duplicate_requests_cannot_switch_camera(self):
        with tempfile.TemporaryDirectory() as root:
            cameras = CameraControl(root, lambda: [device("EShare", 1)])
            path = Path(root) / "tracker_camera_request.json"
            request = {"session": "old-session", "revision": "one", "action": "select", "deviceId": "ESharedshow"}
            write_json(path, request)
            self.assertIsNone(cameras.request())
            request["session"] = cameras.session
            write_json(path, request)
            self.assertEqual(cameras.request()["device"]["name"], "EShare")
            self.assertIsNone(cameras.request())

    def test_refresh_and_disconnected_selection_do_not_invent_devices(self):
        with tempfile.TemporaryDirectory() as root:
            current = [device("EShare", 1)]
            cameras = CameraControl(root, lambda: list(current))
            current.clear()
            path = Path(root) / "tracker_camera_request.json"
            write_json(path, {"session": cameras.session, "revision": "one", "action": "refresh"})
            self.assertEqual(cameras.request(), {"action": "refresh"})
            self.assertEqual(cameras.devices, [])
            write_json(path, {"session": cameras.session, "revision": "two", "action": "select", "deviceId": "ESharedshow"})
            self.assertIsNone(cameras.request()["device"])

    def test_selection_and_status_are_separate_from_physical_calibration(self):
        with tempfile.TemporaryDirectory() as root:
            calibration = Path(root) / "physical-calibration.json"
            calibration.write_bytes(b'physical calibration must stay unchanged')
            cameras = CameraControl(root, lambda: [device("EShare", 1)])
            cameras.remember(cameras.devices[0])
            cameras.publish(cameras.devices[0], "Receiving frames", width=1280, height=720, blank=True)
            status = read_json(Path(root) / "tracker_camera_status.json")
            self.assertEqual(status["selected"]["name"], "EShare")
            self.assertTrue(status["blankFrames"])
            self.assertEqual(cameras.preference()["index"], 1)
            self.assertEqual(calibration.read_bytes(), b'physical calibration must stay unchanged')
            self.assertEqual(list(Path(root).glob("*.tmp")), [])

    def test_partial_or_malformed_control_file_does_not_crash_tracker(self):
        with tempfile.TemporaryDirectory() as root:
            cameras = CameraControl(root, lambda: [])
            path = Path(root) / "tracker_camera_request.json"
            path.write_text('{"session":', encoding="utf-8")
            self.assertIsNone(cameras.request())
