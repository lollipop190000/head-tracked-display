"""Webcam Face Landmarker -> loopback TCP JSON lines consumed by PythonBridgeSource."""
import argparse
import json
import math
from pathlib import Path
import socket
import time

import cv2
import mediapipe as mp

ROOT = Path(__file__).resolve().parent


def open_camera(index: int, width: int, height: int) -> cv2.VideoCapture:
    backend = cv2.CAP_DSHOW if hasattr(cv2, "CAP_DSHOW") else cv2.CAP_ANY
    camera = cv2.VideoCapture(index, backend)
    if not camera.isOpened():
        raise RuntimeError(f"Cannot open webcam index {index}")
    camera.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    return camera


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


def observation(result, width: int, height: int) -> dict:
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
    print(f"Webcam {args.camera} opened. Waiting for Unity at 127.0.0.1:{args.port}. Ctrl+C to stop.")
    try:
        with mp.tasks.vision.FaceLandmarker.create_from_options(options) as landmarker:
            while True:
                ok, frame = camera.read()
                if not ok:
                    print("Webcam read failed; retrying")
                    time.sleep(0.05)
                    continue
                height, width = frame.shape[:2]
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
                timestamp = max(int(time.monotonic() * 1000), last_timestamp + 1)
                last_timestamp = timestamp
                result = landmarker.detect_for_video(image, timestamp)
                line = (json.dumps(observation(result, width, height), separators=(",", ":")) + "\n").encode()
                if connection is None and time.monotonic() - last_connect_attempt >= 1.0:
                    last_connect_attempt = time.monotonic()
                    connection = connect(args.port)
                    if connection:
                        print("Connected to Unity")
                if connection:
                    try:
                        connection.sendall(line)
                    except OSError:
                        connection.close()
                        connection = None
                        print("Unity disconnected; reconnecting")
    finally:
        camera.release()
        if connection:
            connection.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--list-cameras", action="store_true", help="Probe camera indices 0-5 and exit")
    parser.add_argument("--camera", type=int, default=0, help="OpenCV camera index")
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--height", type=int, default=480)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--model", default=str(ROOT / "face_landmarker.task"))
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
