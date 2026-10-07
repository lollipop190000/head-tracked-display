"""Capture checkerboard views and write OpenCV intrinsics for precision mode."""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "Demo" / "Assets" / "StreamingAssets" / "camera_intrinsics.json"


def main(args: argparse.Namespace) -> None:
    camera = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
    if not camera.isOpened():
        raise RuntimeError(f"Cannot open webcam index {args.camera}")
    camera.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    pattern = (args.columns, args.rows)
    object_points = np.zeros((args.columns * args.rows, 3), np.float32)
    object_points[:, :2] = np.mgrid[0:args.columns, 0:args.rows].T.reshape(-1, 2)
    object_points *= args.square_mm / 1000
    world_samples, image_samples = [], []
    image_size = None
    print("Show a 9x6 inner-corner checkerboard from varied positions and angles.")
    print("SPACE: capture valid view; Q: finish (at least 12 samples).")
    try:
        while True:
            ok, frame = camera.read()
            if not ok:
                continue
            gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
            image_size = gray.shape[::-1]
            found, corners = cv2.findChessboardCorners(gray, pattern)
            shown = frame.copy()
            if found:
                corners = cv2.cornerSubPix(gray, corners, (11, 11), (-1, -1),
                                           (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_MAX_ITER, 30, 0.001))
                cv2.drawChessboardCorners(shown, pattern, corners, found)
            cv2.putText(shown, f"Samples: {len(world_samples)}  SPACE capture  Q finish", (12, 28),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2)
            cv2.imshow("Webcam calibration", shown)
            key = cv2.waitKey(1) & 0xFF
            if key == ord(" ") and found:
                world_samples.append(object_points.copy())
                image_samples.append(corners)
                print(f"Captured {len(world_samples)}")
            elif key == ord("q"):
                break
    finally:
        camera.release()
        cv2.destroyAllWindows()
    if len(world_samples) < 12:
        raise RuntimeError("At least 12 varied checkerboard views are required.")
    rms, matrix, distortion, _, _ = cv2.calibrateCamera(
        world_samples, image_samples, image_size, None, None)
    values = distortion.ravel().tolist() + [0] * 5
    output = {"imageWidth": image_size[0], "imageHeight": image_size[1],
              "focalXPixels": float(matrix[0, 0]), "focalYPixels": float(matrix[1, 1]),
              "principalXPixels": float(matrix[0, 2]), "principalYPixels": float(matrix[1, 2]),
              "k1": values[0], "k2": values[1], "p1": values[2], "p2": values[3], "k3": values[4],
              "rmsReprojectionError": float(rms)}
    path = Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(f"Saved {path}; RMS reprojection error: {rms:.3f} px")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--height", type=int, default=480)
    parser.add_argument("--columns", type=int, default=9, help="Inner corners across")
    parser.add_argument("--rows", type=int, default=6, help="Inner corners down")
    parser.add_argument("--square-mm", type=float, default=24)
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT))
    main(parser.parse_args())
