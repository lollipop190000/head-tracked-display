"""Download the official MediaPipe Face Landmarker task model for both providers."""
from pathlib import Path
from urllib.request import urlopen
import hashlib
import shutil

URL = "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task"
SHA256 = "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff"
ROOT = Path(__file__).resolve().parents[1]
SIDE_CAR_MODEL = ROOT / "python_tracker" / "face_landmarker.task"
UNITY_MODEL = ROOT / "Demo" / "Assets" / "StreamingAssets" / "face_landmarker.task"


def main() -> None:
    if not SIDE_CAR_MODEL.exists():
        print(f"Downloading {URL}")
        with urlopen(URL, timeout=60) as response, SIDE_CAR_MODEL.open("wb") as output:
            shutil.copyfileobj(response, output)
    if SIDE_CAR_MODEL.stat().st_size < 100_000:
        raise RuntimeError("The downloaded model is unexpectedly small; remove it and retry.")
    if hashlib.sha256(SIDE_CAR_MODEL.read_bytes()).hexdigest() != SHA256:
        raise RuntimeError("The model SHA-256 differs from the pinned task model; remove it and retry.")
    UNITY_MODEL.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(SIDE_CAR_MODEL, UNITY_MODEL)
    print(f"Model ready: {SIDE_CAR_MODEL} ({SIDE_CAR_MODEL.stat().st_size} bytes)")
    print(f"Unity copy: {UNITY_MODEL}")


if __name__ == "__main__":
    main()
