"""Artefact report for every Billiard Rogue 2D sprite (paintkit.lint_image metrics), with an optional before/after.

Run:  Tools/.venv/bin/python Tools/Textures/art_report.py [--root Starter/Assets] [--save FILE.json]
                                                          [--compare BEFORE.json] [--sheet-dir DIR --before-root DIR]
Metrics per sprite (lower is cleaner; see paintkit.lint_image):
  colors            distinct opaque colours
  near_dupes        colour pairs closer than dE 5 (gradient / anti-alias maths, not a hand-picked ramp)
  stray             lone pixels that match none of their 8 neighbours inside a flat area (speckle, dither noise)
  noise             share of pixels with 3+ differing 4-neighbours (checkerboard dither, noise textures)
  soft_alpha        partially transparent pixels
  outline_doubles   outline pixels that touch the outside only diagonally (thick, doubled outline corners)
  painted           whether a paint/<folder>/<name>.paint source exists
--sheet-dir writes before/after contact sheets per folder (needs --before-root, e.g. a checkout of the old assets).
"""
import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paintkit  # noqa: E402
import pixelkit as pk  # noqa: E402

FOLDERS = [("Sprites/BilliardRogue/Icons", "Icons"), ("Sprites/BilliardRogue/UI", "UI"),
           ("Sprites/BilliardRogue/Particles", "Particles"), ("Textures/BilliardRogue/Surfaces", "Surfaces")]
METRICS = ("colors", "near_dupes", "stray", "noise", "soft_alpha", "outline_doubles")


def frames_of(folder, name, img):
    if folder == "Particles":
        return img.shape[1] // img.shape[0]
    return 1


def collect(root):
    out = {}
    for rel, folder in FOLDERS:
        d = os.path.join(root, rel)
        if not os.path.isdir(d):
            continue
        for n in sorted(os.listdir(d)):
            if not n.endswith(".png") or (folder == "Surfaces" and not n.endswith("_Albedo.png")):
                continue
            name = n[:-4]
            with Image.open(os.path.join(d, n)) as im:
                img = np.asarray(im.convert("RGBA"))
            m = paintkit.lint_image(img, tile=folder == "Surfaces", frames=frames_of(folder, name, img))
            m.pop("opaque", None)
            paint_name = name[:-7] if folder == "Surfaces" else name
            m["painted"] = os.path.exists(paintkit.paint_path(folder, paint_name))
            out[f"{folder}/{name}"] = m
    return out


def sheets(root, before_root, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for rel, folder in FOLDERS:
        items = []
        d = os.path.join(root, rel)
        for n in sorted(os.listdir(d)):
            if not n.endswith(".png") or (folder == "Surfaces" and not n.endswith("_Albedo.png")):
                continue
            after = np.asarray(Image.open(os.path.join(d, n)).convert("RGBA"))
            bp = os.path.join(before_root, rel, n)
            before = np.asarray(Image.open(bp).convert("RGBA")) if os.path.exists(bp) else None
            if before is not None and before.tobytes() == after.tobytes():
                continue
            if after.shape[1] > 256:
                continue  # logos / overlays: shown separately
            items.append((n[:-4] + " before", before if before is not None else np.zeros_like(after)))
            items.append((n[:-4] + " after", after))
        if items:
            scale = 4 if folder != "Particles" else 3
            cols = 2 if folder == "Particles" else 8
            pk.save_rgb(os.path.join(out_dir, f"before_after_{folder}.png"), pk.contact_sheet(items, scale=scale, cols=cols))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=os.path.join(pk.REPO, "Starter", "Assets"))
    ap.add_argument("--save")
    ap.add_argument("--compare")
    ap.add_argument("--sheet-dir")
    ap.add_argument("--before-root")
    a = ap.parse_args()
    now = collect(a.root)
    before = json.load(open(a.compare)) if a.compare else {}
    print(f"{'sprite':40} " + " ".join(f"{m:>15}" for m in METRICS) + "  painted")
    tot_b = {m: 0.0 for m in METRICS}
    tot_a = {m: 0.0 for m in METRICS}
    for k, m in now.items():
        cells = []
        for key in METRICS:
            v = m[key]
            tot_a[key] += v
            if k in before:
                tot_b[key] += before[k][key]
                cells.append(f"{before[k][key]:>7}->{v:<7}")
            else:
                cells.append(f"{v:>15}")
        print(f"{k:40} " + " ".join(cells) + f"  {'yes' if m['painted'] else '-'}")
    print("TOTAL", {m: round(v, 3) for m, v in tot_a.items()}, "painted", sum(m["painted"] for m in now.values()),
          "/", len(now))
    if before:
        print("BEFORE", {m: round(v, 3) for m, v in tot_b.items()})
    if a.save:
        with open(a.save, "w") as f:
            json.dump(now, f, indent=1)
    if a.sheet_dir and a.before_root:
        sheets(a.root, a.before_root, a.sheet_dir)


if __name__ == "__main__":
    main()
