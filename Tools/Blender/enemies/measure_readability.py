"""Readability numbers for the enemy previews (review only): per model and act, in the emulated in-game frame.

Tools/.venv/bin/python Tools/Blender/enemies/measure_readability.py <work_dir> <out_dir> [baseline.json]
  (also run by compose_previews.py). Needs work_dir/preview_raw/arena_<act>_{id,albedo,...}.png from bl_preview.py.
Per placement of the roster layout (averaged per model):
  px      visible silhouette pixels at 640x360 (true game scale)
  w       silhouette bounding-box width in pixels
  dL      |mean luma(model) - mean luma(floor ring 2-5 px around it)|  (Rec.709 luma of the graded sRGB frame)
  dE      mean CIE76 Lab distance of the model's pixels to the floor ring mean (colour + value separation)
  pop     share of the model's pixels whose Lab distance to the floor ring mean is > 40 (clearly separated)
Writes out_dir/readability.json; with a baseline json prints before -> after per model.
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def _lab(rgb):
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def measure(work):
    from compose_previews import game_frame
    raw = os.path.join(work, "preview_raw")
    info = json.load(open(os.path.join(raw, "arena_info.json")))
    layout = info["layout"]
    result = {}
    for act in ("act1", "act2", "act3"):
        rgb = np.asarray(Image.open(os.path.join(raw, f"arena_{act}_id.png")).convert("RGB")).astype(int)
        ids = np.where((rgb[..., 1] == 0) & (rgb[..., 2] == 0), rgb[..., 0], -1)  # -1: not an id colour
        frame = np.asarray(game_frame(raw, f"arena_{act}", act), np.float32) / 255.0
        luma = frame @ np.array([0.2126, 0.7152, 0.0722])
        lab = _lab(frame)
        per = {}
        for i, (model, _, _) in enumerate(layout):
            mask = ids == i + 1
            if mask.sum() < 4:
                continue
            ring = ndimage.binary_dilation(mask, iterations=5) & ~ndimage.binary_dilation(mask, iterations=2)
            ring &= ids == 0
            if ring.sum() < 4:
                continue
            ring_lab = lab[ring].mean(axis=0)
            de = np.linalg.norm(lab[mask] - ring_lab, axis=-1)
            cols = np.where(mask.any(axis=0))[0]
            per.setdefault(model, []).append(dict(
                px=int(mask.sum()), w=int(cols.max() - cols.min() + 1),
                dL=float(abs(luma[mask].mean() - luma[ring].mean())), dE=float(de.mean()),
                pop=float((de > 40).mean())))
        result[act] = {m: {k: round(float(np.mean([v[k] for v in vals])), 3) for k in vals[0]} for m, vals in per.items()}
    return result


def table(res, base=None):
    lines = []
    for act, models in res.items():
        lines.append(f"[{act}] model                 px     w     dL      dE     pop" + ("   (baseline -> now)" if base else ""))
        for m, v in sorted(models.items()):
            if base and m in base.get(act, {}):
                b = base[act][m]
                lines.append(f"  {m:<20} {b['px']:>4}->{v['px']:<4} {b['w']:>2}->{v['w']:<3} "
                             f"{b['dL']:.2f}->{v['dL']:.2f}  {b['dE']:>4.1f}->{v['dE']:<5.1f} {b['pop']:.2f}->{v['pop']:.2f}")
            else:
                lines.append(f"  {m:<20} {v['px']:>5} {v['w']:>5} {v['dL']:>6.2f} {v['dE']:>7.1f} {v['pop']:>7.2f}")
    return "\n".join(lines)


def main():
    work, out = sys.argv[1:3]
    base = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else None
    res = measure(work)
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "readability.json"), "w") as f:
        json.dump(res, f, indent=1, sort_keys=True)
    print(table(res, base))


if __name__ == "__main__":
    main()
