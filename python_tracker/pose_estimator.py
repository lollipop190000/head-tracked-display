"""Small calibrated PnP fit; no additional neural network is run.

Camera coordinates follow OpenCV: +X right, +Y down, +Z away from lens.
The canonical face is scaled by the entered eye separation, not assumed metric output
from MediaPipe. Iris features are observations for calibrated screen gaze, not gaze rays.
"""
import json
import math
from pathlib import Path
import time

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parent
LANDMARK_IDS = np.array([1, 4, 6, 8, 10, 33, 133, 263, 362, 168,
                         54, 284, 67, 297, 103, 332, 127, 356, 93, 323])


def load_canonical():
    vertices = []
    for line in (ROOT / "models/canonical_face_model.obj").read_text().splitlines():
        if line.startswith("v "):
            vertices.append([float(v) for v in line.split()[1:4]])
    return np.asarray(vertices, dtype=np.float64)


def eye_features(points):
    """Mean iris offset in an eye-local basis, measured in eye-width units."""
    if len(points) < 478:
        return None
    features = []
    for a, b, iris, upper, lower in [(33, 133, 468, 159, 145), (362, 263, 473, 386, 374)]:
        start, end = points[a], points[b]
        axis = end - start
        span = np.linalg.norm(axis)
        if span < 2:
            return None
        horizontal = axis / span
        vertical = np.array([-horizontal[1], horizontal[0]])
        openness = abs(np.dot(points[lower] - points[upper], vertical)) / span
        if openness < .10:  # eye closure / insufficient iris visibility
            return None
        delta = points[iris] - (start + end) * .5
        features.append([np.dot(delta, horizontal) / span, np.dot(delta, vertical) / span])
    features = np.mean(features, axis=0)
    return features if np.all(np.isfinite(features)) and np.max(np.abs(features)) < .6 else None


class PoseEstimator:
    def __init__(self, calibration_file=None):
        self.canonical = load_canonical()
        self.left_eye = (self.canonical[33] + self.canonical[133]) * .5
        self.right_eye = (self.canonical[263] + self.canonical[362]) * .5
        self.eye_midpoint = (self.left_eye + self.right_eye) * .5
        self.model_ipd = np.linalg.norm(self.right_eye - self.left_eye)
        self.calibration_file = Path(calibration_file) if calibration_file else None
        self.config = {}
        self.last_check = 0.0

    def reload_calibration(self):
        now = time.monotonic()
        if self.calibration_file and now - self.last_check > .25:
            self.last_check = now
            try:
                config = json.loads(self.calibration_file.read_text(encoding="utf-8-sig"))
                if config.get("focalXPixels", 0) > 1 and config.get("eyeSeparationMeters", 0) > 0:
                    self.config = config
            except (OSError, ValueError):
                pass  # Unity can be rewriting the tiny runtime configuration.

    def camera_parameters(self, width, height):
        c = self.config
        sx, sy = width / c.get("imageWidth", width), height / c.get("imageHeight", height)
        focal = width / (2 * math.tan(math.radians(60) / 2))
        k = np.array([[c.get("focalXPixels", focal) * sx, 0, c.get("principalXPixels", width / 2) * sx],
                      [0, c.get("focalYPixels", focal) * sy, c.get("principalYPixels", height / 2) * sy],
                      [0, 0, 1]], dtype=np.float64)
        distortion = np.asarray([c.get(name, 0.0) for name in ("k1", "k2", "p1", "p2", "k3")], dtype=np.float64)
        return k, distortion, c.get("eyeSeparationMeters", .063)

    def estimate(self, landmarks, width, height):
        started = time.perf_counter()
        self.reload_calibration()
        output = {"poseSupported": True, "poseValid": False, "gazeValid": False,
                  "calibrationRevision": self.config.get("revision", 0)}
        if len(landmarks) < 468:
            return output
        pixels = np.asarray([(p.x * width, p.y * height) for p in landmarks], dtype=np.float64)
        if not np.all(np.isfinite(pixels)):
            return output
        k, distortion, ipd = self.camera_parameters(width, height)
        model = self.canonical * (ipd / self.model_ipd)
        objects, images = model[LANDMARK_IDS], pixels[LANDMARK_IDS]
        try:
            success, rvec, tvec = cv2.solvePnP(objects, images, k, distortion, flags=cv2.SOLVEPNP_SQPNP)
            if not success:
                return output
            rvec, tvec = cv2.solvePnPRefineLM(objects, images, k, distortion, rvec, tvec)
            rotation = cv2.Rodrigues(rvec)[0]
            projected = cv2.projectPoints(objects, rvec, tvec, k, distortion)[0].reshape(-1, 2)
            error = float(np.sqrt(np.mean(np.sum((projected - images) ** 2, axis=1))))
            eye = rotation @ (self.eye_midpoint * ipd / self.model_ipd) + tvec.reshape(3)
            if not np.all(np.isfinite(eye)) or not np.all(np.isfinite(rotation)) or not math.isfinite(error):
                return output
            pitch, yaw, roll = cv2.RQDecomp3x3(rotation @ np.diag([1., -1., -1.]))[0]
            limit = max(3., width * .015)
            valid = bool(np.all(np.isfinite(eye)) and .15 < eye[2] < 2.5 and
                         error < limit and abs(yaw) < 65 and abs(pitch) < 55 and rotation[2, 2] < -.25)
            output.update(poseValid=valid, poseEyeX=float(eye[0]), poseEyeY=float(eye[1]),
                          poseEyeZ=float(eye[2]), headPitchDegrees=float(pitch), headYawDegrees=float(yaw),
                          headRollDegrees=float(roll), reprojectionErrorPixels=error,
                          poseConfidence=max(0., 1. - error / limit))
            gaze = eye_features(pixels)
            if valid and gaze is not None:
                output.update(gazeValid=True, irisHorizontal=float(gaze[0]), irisVertical=float(gaze[1]))
        except (cv2.error, ValueError, FloatingPointError):
            pass
        output["poseMs"] = (time.perf_counter() - started) * 1000
        return output
