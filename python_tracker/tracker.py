"""Webcam Face Landmarker -> loopback TCP JSON lines consumed by PythonBridgeSource."""
import argparse
import json
import math
from pathlib import Path
import socket
import time
import threading

import cv2
import mediapipe as mp
from pose_estimator import PoseEstimator

ROOT = Path(__file__).resolve().parent


def open_camera(index: int, width: int, height: int) -> cv2.VideoCapture:
    backend = cv2.CAP_DSHOW if hasattr(cv2, "CAP_DSHOW") else cv2.CAP_ANY
    camera = cv2.VideoCapture(index, backend)
    if not camera.isOpened():
        raise RuntimeError(f"Cannot open webcam index {index}")
    camera.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    camera.set(cv2.CAP_PROP_FPS, 60)
    return camera


class LatestFrameCamera:
    """Continuously drain the webcam; keep a single frame rather than a work queue."""
    def __init__(self, camera):
        self.camera = camera
        self.lock = threading.Lock()
        self.latest = None
        self.running = True
        self.sequence = 0
        self.thread = threading.Thread(target=self._capture, daemon=True)
        self.thread.start()

    def _capture(self):
        while self.running:
            ok, frame = self.camera.read()
            captured = time.monotonic()
            if ok:
                with self.lock:
                    self.sequence += 1
                    self.latest = (self.sequence, captured, frame)
            else:
                time.sleep(.01)

    def get_after(self, sequence):
        with self.lock:
            return self.latest if self.latest and self.latest[0] > sequence else None

    def close(self):
        self.running = False
        self.thread.join(2)
        self.camera.release()


def eye_center(points, a: int, b: int) -> tuple[float, float]:
    return ((points[a].x + points[b].x) / 2, (points[a].y + points[b].y) / 2)


def eye_span_foreshortening(result) -> float:
    matrices = getattr(result, "facial_transformation_matrixes", None)
    if matrices is None or len(matrices) == 0:
        return 0.0
    matrix = matrices[0]
    x, y, z = float(matrix[0, 0]), float(matrix[1, 0]), float(matrix[2, 0])
    length = math.sqrt(x * x + y * y + z * z)
    if not math.isfinite(length) or length < 1e-5:
        return 0.0
    return min(1.0, math.hypot(x, y) / length)


def observation(result, width: int, height: int, pose=None) -> dict:
    message = {"found": False, "leftX": 0.0, "leftY": 0.0,
               "rightX": 0.0, "rightY": 0.0,
               "width": width, "height": height, "confidence": 0.0,
               "eyeSpanForeshortening": 0.0}
    if result.face_landmarks:
        points = result.face_landmarks[0]
        left = eye_center(points, 33, 133)
        right = eye_center(points, 263, 362)
        message.update(found=True, leftX=left[0], leftY=left[1],
                       rightX=right[0], rightY=right[1], confidence=1.0,
                       eyeSpanForeshortening=eye_span_foreshortening(result))
        if pose is not None:
            message.update(pose.estimate(points, width, height))
    elif pose is not None:
        message.update(poseSupported=True, poseValid=False, gazeValid=False)
    return message


def connect(port: int) -> socket.socket | None:
    try:
        connection = socket.create_connection(("127.0.0.1", port), timeout=0.5)
        connection.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        return connection
    except OSError:
        return None


def run(args: argparse.Namespace) -> None:
    model = Path(args.model)
    if not model.exists():
        raise FileNotFoundError(f"Missing {model}; run download_model.py first")
    camera = open_camera(args.camera, args.width, args.height)
    options = mp.tasks.vision.FaceLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(model)),
        running_mode=mp.tasks.vision.RunningMode.VIDEO,
        num_faces=1,
        output_facial_transformation_matrixes=True)
    connection = None
    last_connect_attempt = 0.0
    last_timestamp = -1
    pose = PoseEstimator(args.calibration_file)
    capture = LatestFrameCamera(camera)
    sequence = 0
    last_result = time.monotonic()
    fps = 0.0
    deadline = time.monotonic() + args.benchmark_seconds if args.benchmark_seconds > 0 else float("inf")
    stats = []
    print(f"Webcam {args.camera} opened. Waiting for Unity at 127.0.0.1:{args.port}. Ctrl+C to stop.")
    try:
        with mp.tasks.vision.FaceLandmarker.create_from_options(options) as landmarker:
            while time.monotonic() < deadline:
                newest = capture.get_after(sequence)
                if newest is None:
                    time.sleep(.002)
                    continue
                sequence, captured_at, frame = newest
                height, width = frame.shape[:2]
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
                timestamp = max(int(captured_at * 1000), last_timestamp + 1)
                last_timestamp = timestamp
                started = time.perf_counter()
                result = landmarker.detect_for_video(image, timestamp)
                inference_ms = (time.perf_counter() - started) * 1000
                message = observation(result, width, height, pose)
                completed = time.monotonic()
                instantaneous_fps = 1 / max(.001, completed - last_result)
                fps = instantaneous_fps if not fps else .9 * fps + .1 * instantaneous_fps
                last_result = completed
                message.update(sequence=sequence, inferenceMs=inference_ms, trackerFps=fps,
                               frameAgeMs=(completed - captured_at) * 1000)
                if args.benchmark_seconds > 0:
                    stats.append((inference_ms, message.get("poseMs", 0.), message["frameAgeMs"],
                                  message["found"], message.get("poseValid", False)))
                if connection is None and time.monotonic() - last_connect_attempt >= 1.0:
                    last_connect_attempt = time.monotonic()
                    connection = connect(args.port)
                    if connection:
                        print("Connected to Unity")
                if connection:
                    try:
                        message["frameAgeMs"] = (time.monotonic() - captured_at) * 1000
                        line = (json.dumps(message, separators=(",", ":"), allow_nan=False) + "\n").encode()
                        connection.sendall(line)
                    except OSError:
                        connection.close()
                        connection = None
                        print("Unity disconnected; reconnecting")
    finally:
        capture.close()
        if connection:
            connection.close()
        if stats:
            import numpy as np
            values = np.asarray(stats)
            print(json.dumps({"frames": len(stats), "faceFrames": int(values[:, 3].sum()),
                              "validPoseFrames": int(values[:, 4].sum()),
                              "medianInferenceMs": float(np.median(values[:, 0])),
                              "medianPoseMs": float(np.median(values[:, 1])),
                              "medianFrameAgeMs": float(np.median(values[:, 2])),
                              "p95FrameAgeMs": float(np.percentile(values[:, 2], 95))}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list-cameras", action="store_true", help="Probe camera indices 0-5 and exit")
    parser.add_argument("--camera", type=int, default=0, help="OpenCV camera index")
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--height", type=int, default=480)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--model", default=str(ROOT / "face_landmarker.task"))
    parser.add_argument("--calibration-file", help="Unity runtime camera parameters JSON (automatically reloaded)")
    parser.add_argument("--benchmark-seconds", type=float, default=0, help="Capture briefly, report timings, and exit")
    arguments = parser.parse_args()
    if arguments.list_cameras:
        for index in range(6):
            probe = cv2.VideoCapture(index, cv2.CAP_DSHOW)
            if probe.isOpened():
                print(f"Camera {index}: available")
            probe.release()
        raise SystemExit(0)
    try:
        run(arguments)
    except KeyboardInterrupt:
        print("Stopped")
