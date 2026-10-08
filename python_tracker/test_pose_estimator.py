import math
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from pose_estimator import PoseEstimator


class PoseTests(unittest.TestCase):
    def setUp(self):
        self.pose = PoseEstimator()
        self.pose.config = dict(imageWidth=640, imageHeight=480, focalXPixels=600,
                                focalYPixels=580, principalXPixels=315, principalYPixels=235,
                                eyeSeparationMeters=.063, k1=.025, k2=-.01, p1=.001, p2=-.001,
                                revision=7)

    def landmarks(self, eye, yaw=0, pitch=0, iris_offset=0):
        rotation = cv2.Rodrigues(np.array([math.radians(pitch), math.radians(yaw), 0.]))[0] @ np.diag([1., -1., -1.])
        model = self.pose.canonical * (.063 / self.pose.model_ipd)
        t = np.asarray(eye) - rotation @ (self.pose.eye_midpoint * .063 / self.pose.model_ipd)
        k, distortion, _ = self.pose.camera_parameters(640, 480)
        pixels = cv2.projectPoints(model, cv2.Rodrigues(rotation)[0], t, k, distortion)[0].reshape(-1, 2)
        pixels = np.vstack([pixels, np.zeros((10, 2))])
        for a, b, iris in [(33, 133, 468), (362, 263, 473)]:
            pixels[iris] = (pixels[a] + pixels[b]) * .5 + iris_offset * (pixels[b] - pixels[a])
        return [SimpleNamespace(x=p[0] / 640, y=p[1] / 480) for p in pixels]

    def test_rotating_around_eye_midpoint_preserves_eye_position(self):
        for yaw in (-45, -20, 0, 20, 45):
            for pitch in (-15, 0, 15):
                with self.subTest(yaw=yaw, pitch=pitch):
                    value = self.pose.estimate(self.landmarks([.03, -.04, .6], yaw, pitch), 640, 480)
                    self.assertTrue(value["poseValid"])
                    np.testing.assert_allclose([value["poseEyeX"], value["poseEyeY"], value["poseEyeZ"]],
                                               [.03, -.04, .6], atol=1e-5)

    def test_body_translation_and_distance_are_recovered(self):
        for eye in ([0, 0, .6], [.1, -.05, .4], [-.15, .07, .9]):
            value = self.pose.estimate(self.landmarks(eye, 25), 640, 480)
            self.assertTrue(value["poseValid"])
            np.testing.assert_allclose([value["poseEyeX"], value["poseEyeY"], value["poseEyeZ"]], eye, atol=1e-5)

    def test_iris_motion_does_not_translate_the_render_eye(self):
        a = self.pose.estimate(self.landmarks([0, 0, .6]), 640, 480)
        b = self.pose.estimate(self.landmarks([0, 0, .6], iris_offset=.15), 640, 480)
        self.assertTrue(a["gazeValid"] and b["gazeValid"])
        self.assertAlmostEqual(b["irisHorizontal"] - a["irisHorizontal"], .15, places=4)
        for key in ("poseEyeX", "poseEyeY", "poseEyeZ"):
            self.assertAlmostEqual(a[key], b[key], places=5)

    def test_eye_closure_invalidates_gaze_without_losing_head_pose(self):
        points = self.landmarks([0, 0, .6])
        points[159] = points[145]
        value = self.pose.estimate(points, 640, 480)
        self.assertTrue(value["poseValid"])
        self.assertFalse(value["gazeValid"])

    def test_large_yaw_is_rejected_instead_of_looking_confident(self):
        value = self.pose.estimate(self.landmarks([0, 0, .6], 80), 640, 480)
        self.assertFalse(value["poseValid"])


if __name__ == "__main__":
    unittest.main()
