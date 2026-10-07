"""Install the optional pinned MediaPipe Unity Plugin release in the demo project."""
from pathlib import Path
import hashlib
import json
import shutil
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
VERSION = "0.16.3"
FILE_NAME = f"com.github.homuler.mediapipe-{VERSION}.tgz"
URL = f"https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v{VERSION}/{FILE_NAME}"
SHA256 = "cc3e77a219e0b99618ae3be64c31a566197deedc69c1e136acf52d65d7cf2e79"
PACKAGE_PATH = ROOT / "Demo" / "Packages" / FILE_NAME
MANIFEST_PATH = ROOT / "Demo" / "Packages" / "manifest.json"


def main() -> None:
    if not PACKAGE_PATH.exists():
        print(f"Downloading {URL} (about 290 MB)")
        temporary = PACKAGE_PATH.with_suffix(".download")
        with urlopen(URL, timeout=120) as response, temporary.open("wb") as output:
            shutil.copyfileobj(response, output)
        temporary.rename(PACKAGE_PATH)
    if PACKAGE_PATH.stat().st_size < 250_000_000:
        raise RuntimeError("The plugin archive is incomplete. Remove it and retry.")
    with PACKAGE_PATH.open("rb") as archive:
        digest = hashlib.file_digest(archive, "sha256").hexdigest()
    if digest != SHA256:
        raise RuntimeError("The plugin SHA-256 differs from the official release; remove it and retry.")
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    # The upstream package uses AssetBundleResourceManager but omits this built-in module dependency.
    manifest["dependencies"]["com.unity.modules.assetbundle"] = "1.0.0"
    manifest["dependencies"]["com.github.homuler.mediapipe"] = "file:" + FILE_NAME
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"MediaPipe Unity Plugin {VERSION} is ready in {MANIFEST_PATH}")


if __name__ == "__main__":
    main()
