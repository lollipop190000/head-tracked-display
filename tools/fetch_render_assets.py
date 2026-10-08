"""Fetch the CC0 Poly Haven comparison assets and pack URP metallic/smoothness maps."""
import hashlib
import json
from pathlib import Path
import urllib.request

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[1] / 'Demo/Assets/Resources/Realism'
HEADERS = {'User-Agent': 'HeadTrackedDisplay/0.3 (https://github.com/lollipop190000/head-tracked-display)'}
records = []


def api(asset):
    request = urllib.request.Request('https://api.polyhaven.com/files/' + asset, headers=HEADERS)
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def fetch(asset, entry, filename):
    ROOT.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(entry['url'], headers=HEADERS)
    with urllib.request.urlopen(request, timeout=60) as response:
        data = response.read()
    if len(data) != entry['size'] or hashlib.md5(data).hexdigest() != entry['md5']:
        raise ValueError('Upstream size/hash mismatch: ' + filename)
    (ROOT / filename).write_bytes(data)
    records.append(dict(file=filename, asset=asset, source=entry['url'], sha256=hashlib.sha256(data).hexdigest(), license='CC0-1.0'))
    print(filename, len(data))


def pack(prefix, metal=False):
    rough = cv2.imread(str(ROOT / (prefix + '_rough.png')), cv2.IMREAD_GRAYSCALE)
    metallic = cv2.imread(str(ROOT / (prefix + '_metal.png')), cv2.IMREAD_GRAYSCALE) if metal else np.zeros_like(rough)
    # OpenCV BGRA: Unity reads metallic from R and smoothness from A.
    packed = np.zeros((*rough.shape, 4), dtype=np.uint8)
    packed[:, :, 2] = metallic
    packed[:, :, 3] = 255 - rough
    cv2.imwrite(str(ROOT / (prefix + '_metallic_smoothness.png')), packed)


if __name__ == '__main__':
    vase = api('ceramic_vase_01')
    fetch('ceramic_vase_01', vase['fbx']['1k']['fbx'], 'ceramic_vase.fbx')
    for key, name in [('Diffuse', 'albedo'), ('nor_gl', 'normal'), ('Rough', 'rough'), ('Metal', 'metal'), ('AO', 'ao')]:
        fetch('ceramic_vase_01', vase[key]['2k']['png'], 'vase_' + name + '.png')
    pack('vase', metal=True)
    wood = api('wood_floor_deck')
    for key, name in [('Diffuse', 'albedo'), ('nor_gl', 'normal'), ('Rough', 'rough'), ('AO', 'ao')]:
        fetch('wood_floor_deck', wood[key]['1k']['png'], 'wood_' + name + '.png')
    pack('wood')
    studio = api('studio_small_09')
    fetch('studio_small_09', studio['hdri']['1k']['hdr'], 'studio.hdr')
    (ROOT / 'sources.json').write_text(json.dumps(records, indent=2) + '\n', encoding='utf-8')
