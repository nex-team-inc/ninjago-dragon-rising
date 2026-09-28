"""32x32 tileable stone-brick set: albedo (on-palette), normal (Sobel on height), cavity mask.

python make_stone_brick.py --palette-json palette.json --out-dir OUT --name StoneBrick [--size 32] [--seed 3]
Writes OUT/T_<name>_Albedo.png, T_<name>_Normal.png, T_<name>_Cavity.png, T_<name>_Height.png
"""
import argparse
import os
import time

import numpy as np
from scipy import ndimage

import pixeltex as px


def brick_layout(size, brick_w, brick_h, mortar, rng):
    """Returns (brick_id map, distance-to-mortar map) for a running-bond pattern that wraps."""
    ids = np.zeros((size, size), np.int32)
    solid = np.zeros((size, size), bool)
    for y in range(size):
        row = y // brick_h
        offset = (brick_w // 2) * (row % 2)
        for x in range(size):
            bx = ((x + offset) % size) // brick_w
            ids[y, x] = row * 100 + bx
            in_x = (x + offset) % brick_w >= mortar
            in_y = y % brick_h >= mortar
            solid[y, x] = in_x and in_y
    dist = ndimage.distance_transform_cdt(np.pad(solid, 4, mode="wrap"), metric="taxicab")[4:-4, 4:-4]
    return ids, solid, dist


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--palette-json", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--name", default="StoneBrick")
    ap.add_argument("--size", type=int, default=32)
    ap.add_argument("--seed", type=int, default=3)
    ap.add_argument("--family", default="gray")
    a = ap.parse_args()
    t0 = time.time()
    rng = np.random.default_rng(a.seed)
    s = a.size
    ramp = px.load_ramp(a.palette_json, a.family)

    ids, solid, dist = brick_layout(s, brick_w=s // 2, brick_h=s // 4, mortar=1, rng=rng)
    noise = px.value_noise(s, 8, rng)
    # height: mortar low, brick high with a 1-2 px bevel, plus chips
    bevel = np.clip(dist / 2.0, 0, 1)
    height = np.where(solid, 0.55 + 0.35 * bevel + 0.10 * noise, 0.1)
    chips = (rng.random((s, s)) < 0.04) & solid & (dist <= 2)
    height[chips] -= 0.3
    height = np.clip(height, 0, 1)

    # albedo: per-brick tone + noise, lit from the top-left, then quantized to palette shades 3..10
    brick_tone = {i: rng.uniform(-0.12, 0.12) for i in np.unique(ids)}
    tone = np.vectorize(brick_tone.get)(ids)
    gy, gx = np.gradient(np.pad(height, 1, mode="wrap"))
    light = (-gx - gy)[1:-1, 1:-1] * 1.2  # bright on top-left edges
    v = np.where(solid, 0.55 + tone + 0.18 * (noise - 0.5) + light, 0.18)
    v += px.ordered_dither(s, s) * 0.08
    shade_lo, shade_hi = 3, 10
    albedo = px.quantize_to_ramp(np.clip(v, 0, 1), ramp[shade_lo:shade_hi + 1])

    normal = px.normal_from_height(height, strength=3.0)
    cavity = px.cavity_from_height(height)

    os.makedirs(a.out_dir, exist_ok=True)
    base = os.path.join(a.out_dir, f"T_{a.name}")
    px.save_rgb(base + "_Albedo.png", albedo)
    px.save_rgb(base + "_Normal.png", normal)
    px.save_gray(base + "_Cavity.png", cavity)
    px.save_gray(base + "_Height.png", height)
    print("TEX_STATS", {"s": round(time.time() - t0, 3),
                        **{k: os.path.getsize(base + k + ".png") for k in ("_Albedo", "_Normal", "_Cavity", "_Height")}})


if __name__ == "__main__":
    main()
