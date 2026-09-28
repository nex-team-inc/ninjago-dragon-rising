"""Stand-in 64x64 tiling surface textures for the environment PREVIEW renders only (not game assets).

The real M_Surface_* textures are owned by the 2D-art module (Assets/Textures/BilliardRogue/Surfaces/<Name>_Albedo.png);
render_diorama.py prefers those when they exist in the staging mirror and falls back to these.

    Tools/.venv/bin/python Tools/Blender/environment/make_preview_surfaces.py --out-dir <dir>

Deterministic (fixed seeds), palette-quantized, 1 texture tile = 1 m (64 px / m).
"""
import argparse
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
PALETTE = os.path.join(REPO, "Starter", "Assets", "Textures", "BilliardRogue", "Palette", "palette.json")
S = 64


def ramp(pal, fam):
    return np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for h in pal["families"][fam]["hex"]], np.float32)


def noise(rng, cells, octaves=3):
    out = np.zeros((S, S), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        c = cells * 2 ** o
        g = rng.random((c, c)).astype(np.float32)
        z = ndimage.zoom(np.tile(g, (3, 3)), S / c, order=3, mode="grid-wrap")
        out += amp * z[S:2 * S, S:2 * S]
        tot += amp
        amp *= 0.5
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def quant(v, rmp, lo, hi):
    idx = np.clip(np.round(lo + v * (hi - lo)), 0, 15).astype(int)
    return rmp[idx]


def blocks(rows, cols, offset_rows=True, mortar=1, rng=None, jitter=0.0):
    """Per-pixel (block id, distance to mortar) for a running-bond grid that wraps."""
    bh, bw = S // rows, S // cols
    y, x = np.mgrid[0:S, 0:S]
    r = y // bh
    xs = (x + (bw // 2) * (r % 2) * offset_rows) % S
    c = xs // bw
    bid = r * cols + c
    dy = np.minimum(y % bh, bh - 1 - y % bh)
    dx = np.minimum(xs % bw, bw - 1 - xs % bw)
    d = np.minimum(dx, dy)
    return bid, d


def bevel_light(h):
    gy, gx = np.gradient(h)
    return np.clip(0.5 - (gx + gy) * 1.5, 0, 1)


def make(name, pal, rng):
    gray, lime, green, brown = ramp(pal, "gray"), ramp(pal, "lime"), ramp(pal, "green"), ramp(pal, "brown")
    indigo, cyan, purple = ramp(pal, "indigo"), ramp(pal, "cyan"), ramp(pal, "purple")
    n1, n2 = noise(rng, 4), noise(rng, 8)
    if name in ("StoneFloor", "CryptFloor"):
        rows = 2
        bid, d = blocks(rows, rows, offset_rows=(name == "CryptFloor"))
        tone = rng.random(rows * rows)[bid]
        h = np.clip(d / 3.0, 0, 1) * (0.7 + 0.3 * n2)
        v = 0.45 * tone + 0.35 * n2 + 0.2 * bevel_light(h)
        v = np.where(d == 0, 0.0, v)
        cav = np.where(d == 0, 0.2, np.where(d == 1, 0.42, 0.5 + (n2 - 0.5) * 0.2))
        img = quant(v, gray, 6, 10) if name == "StoneFloor" else quant(v, indigo, 3, 6) * 0.5 + quant(v, gray, 4, 7) * 0.5
    elif name in ("MossyBrick", "CryptBrick"):
        rows, cols = (8, 4) if name == "MossyBrick" else (4, 2)
        bid, d = blocks(rows, cols)
        tone = rng.random(rows * cols)[bid]
        h = np.clip(d / 2.0, 0, 1)
        v = 0.5 * tone + 0.3 * n2 + 0.2 * bevel_light(h)
        v = np.where(d == 0, 0.0, v)
        cav = np.where(d == 0, 0.2, 0.5 + (n2 - 0.5) * 0.25)
        if name == "MossyBrick":
            img = quant(v, gray, 5, 9)
            moss = (n1 > 0.58) & (d > 0) | ((n1 > 0.5) & (d == 0))
            img = np.where(moss[..., None], quant(n2, lime, 6, 9), img)
        else:
            img = quant(v, gray, 3, 6) * 0.6 + quant(v, indigo, 3, 6) * 0.4
    elif name == "CrystalRock":
        h = noise(rng, 6, 4)
        v = 0.6 * h + 0.4 * bevel_light(ndimage.gaussian_filter(h, 1, mode="wrap"))
        img = quant(v, indigo, 2, 6)
        specks = rng.random((S, S)) > 0.992
        img = np.where(specks[..., None], cyan[13], img)
        cav = 0.5 + (h - ndimage.gaussian_filter(h, 2, mode="wrap")) * 1.5
    elif name == "Dirt":
        v = 0.6 * n1 + 0.4 * n2
        img = quant(v, brown, 4, 7)
        peb = rng.random((S, S)) > 0.985
        img = np.where(peb[..., None], gray[8], img)
        cav = 0.5 + (n2 - 0.5) * 0.3
    elif name == "Grass":
        v = 0.55 * n1 + 0.45 * n2
        img = quant(v, lime, 5, 8) * 0.6 + quant(v, green, 5, 8) * 0.4
        blades = (rng.random((S, S)) > 0.9) & (n2 > 0.4)
        img = np.where(blades[..., None], lime[10], img)
        dark = rng.random((S, S)) > 0.93
        img = np.where(dark[..., None], green[6], img)
        cav = 0.5 + (n2 - 0.5) * 0.3
    else:  # WoodPlank
        y = np.mgrid[0:S, 0:S][0]
        plank = y // 16
        d = np.minimum(y % 16, 15 - y % 16)
        v = 0.5 * rng.random(4)[plank] + 0.3 * noise(rng, 16) + 0.2
        v = np.where(d == 0, 0.0, v)
        img = quant(v, brown, 5, 9)
        cav = np.where(d == 0, 0.2, 0.5)
    return np.clip(img, 0, 255).astype(np.uint8), (np.clip(cav, 0, 1) * 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)
    with open(PALETTE) as f:
        pal = json.load(f)
    for i, name in enumerate(("StoneFloor", "MossyBrick", "CryptBrick", "CryptFloor", "WoodPlank", "CrystalRock",
                              "Dirt", "Grass")):
        rng = np.random.default_rng(1000 + i)
        alb, cav = make(name, pal, rng)
        Image.fromarray(alb, "RGB").save(os.path.join(a.out_dir, f"{name}_Albedo.png"), optimize=True)
        Image.fromarray(cav, "L").save(os.path.join(a.out_dir, f"{name}_Cavity.png"), optimize=True)
    print("preview surfaces ->", a.out_dir)


if __name__ == "__main__":
    main()
