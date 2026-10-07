import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from tracker import observation


class ProtocolTests(unittest.TestCase):
    def test_missing_face(self):
        result = SimpleNamespace(face_landmarks=[])
        value = observation(result, 640, 480)
        self.assertFalse(value["found"])
        self.assertEqual((value["width"], value["height"]), (640, 480))

    def test_eye_centers(self):
        points = [SimpleNamespace(x=0.0, y=0.0) for _ in range(478)]
        points[33].x, points[133].x = 0.2, 0.4
        points[33].y = points[133].y = 0.5
        points[263].x, points[362].x = 0.6, 0.8
        points[263].y = points[362].y = 0.5
        value = observation(SimpleNamespace(face_landmarks=[points]), 640, 480)
        self.assertTrue(value["found"])
        self.assertAlmostEqual(value["leftX"], 0.3)
        self.assertAlmostEqual(value["rightX"], 0.7)

    def test_face_yaw_reports_eye_span_foreshortening(self):
        matrix = np.eye(4)
        matrix[0, 0] = 0.5
        matrix[2, 0] = np.sqrt(3) / 2
        points = [SimpleNamespace(x=0.5, y=0.5) for _ in range(478)]
        value = observation(SimpleNamespace(
            face_landmarks=[points], facial_transformation_matrixes=[matrix]), 640, 480)
        self.assertAlmostEqual(value["eyeSpanForeshortening"], 0.5)


if __name__ == "__main__":
    unittest.main()
