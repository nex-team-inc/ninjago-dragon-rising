"""Pixel particle flipbook atlas: ROWS effects x FRAMES frames of CELL px, white/grey + hard alpha.

Tint in Unity via ParticleSystem startColor / colorOverLifetime (HDR colour > 1 feeds bloom).
Texture Sheet Animation: Mode=Grid, Tiles=(FRAMES, ROWS), Animation=Single Row, Row Index=<effect row>.

python make_particle_sheet.py --out T_PixelParticles.png [--cell 16] [--frames 8]
"""
import argparse
import math
import os
import time

import numpy as np

import pixeltex as px

ROWS = ["spark", "dust", "leaf", "snowflake", "orb", "smoke", "ring", "twinkle"]


def grid(cell):
    c = (cell - 1) / 2.0
    y, x = np.mgrid[0:cell, 0:cell].astype(np.float32)
    return x - c, c - y  # +y up


def put(v, a, mask, value):
    v[mask] = np.maximum(v[mask], value)
    a[mask] = 1.0


def spark(t, cell):
    x, y = grid(cell)
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    arm = (1 - t) * (cell / 2 - 2) + 1.0
    width = 0.6 if t < 0.5 else 0.45
    cross = ((np.abs(x) < width) & (np.abs(y) < arm)) | ((np.abs(y) < width) & (np.abs(x) < arm))
    diag = (np.abs(np.abs(x) - np.abs(y)) < 0.6) & (np.abs(x) < arm * 0.45)
    put(v, a, cross, 0.75)
    put(v, a, diag & (t < 0.6), 0.6)
    put(v, a, (np.abs(x) < 1.1) & (np.abs(y) < 1.1), 1.0 - 0.3 * t)
    return v, a


def dust(t, cell):
    x, y = grid(cell)
    r2 = x * x + y * y
    pulse = 1.6 + 0.6 * math.sin(t * 2 * math.pi)  # cell centre sits between pixels: keep >= 1
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    put(v, a, r2 < pulse ** 2, 0.7)
    put(v, a, r2 < max(pulse * 0.6, 0.8) ** 2, 1.0)
    return v, a


def leaf(t, cell):
    x, y = grid(cell)
    ang = t * 2 * math.pi
    ca, sa = math.cos(ang), math.sin(ang)
    lx, ly = ca * x + sa * y, -sa * x + ca * y
    tumble = 0.45 + 0.55 * abs(math.cos(ang * 1.5))  # fake 3D flutter: squash the width
    length = 5.5
    half_w = 2.7 * tumble * np.clip(1 - (lx / length) ** 2, 0, 1)
    body = (np.abs(lx) < length) & (np.abs(ly) < half_w + 0.2)
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    put(v, a, body, 0.85)
    v[body & (ly < -0.3)] = 0.62  # shaded half
    v[body & (np.abs(ly) < 0.45) & (np.abs(lx) < length - 1)] = 0.5  # midrib
    put(v, a, (np.abs(ly) < 0.5) & (lx >= length) & (lx < length + 1.8), 0.5)  # stem
    return v, a


def snowflake(t, cell):
    x, y = grid(cell)
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    base = t * math.pi / 3  # 60 deg over the loop -> seamless (6-fold symmetry)
    r = np.hypot(x, y)
    for k in range(6):
        ang = base + k * math.pi / 3
        dx, dy = math.cos(ang), math.sin(ang)
        along = x * dx + y * dy
        perp = np.abs(-x * dy + y * dx)
        put(v, a, (perp < 0.55) & (along > 0) & (along < 6.2), 0.85)
        # side barbs
        bx, by = x - dx * 3.5, y - dy * 3.5
        for s in (-1, 1):
            ba = ang + s * math.pi / 3
            ex, ey = math.cos(ba), math.sin(ba)
            al = bx * ex + by * ey
            pp = np.abs(-bx * ey + by * ex)
            put(v, a, (pp < 0.55) & (al > 0) & (al < 2.2), 0.7)
    put(v, a, r < 1.2, 1.0)
    return v, a


def orb(t, cell):
    x, y = grid(cell)
    r = np.hypot(x, y)
    rad = 4.2 + 0.5 * math.sin(t * 2 * math.pi)
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    put(v, a, r < rad, 0.55)
    put(v, a, (r < rad) & (r > rad - 1.1), 0.8)  # rim
    put(v, a, np.hypot(x + 1.2, y - 1.2) < 1.6, 1.0)  # highlight
    ang = t * 2 * math.pi
    sx, sy = math.cos(ang) * 6.3, math.sin(ang) * 6.3
    put(v, a, (np.abs(x - sx) < 0.8) & (np.abs(y - sy) < 0.8), 1.0)  # orbiting mote
    return v, a


def smoke(t, cell):
    x, y = grid(cell)
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    rad = 2.0 + t * 5.2
    density = 1.0 - t * 0.85
    blobs = ((0, 0), (-0.35, 0.25), (0.35, 0.3), (0, 0.45))
    field = np.zeros((cell, cell))
    for bx, by in blobs:
        field = np.maximum(field, 1 - np.hypot(x - bx * rad, y - by * rad - t * 1.5) / (rad * 0.75))
    dither = px.ordered_dither(cell, cell) + 0.5
    mask = (field > 0) & (dither < density * np.clip(field * 3, 0, 1))
    put(v, a, mask, 0.9 - 0.3 * t)
    return v, a


def ring(t, cell):
    x, y = grid(cell)
    r = np.hypot(x, y)
    rad = 1.0 + t * 6.2
    thick = 2.2 - t * 1.4
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    put(v, a, np.abs(r - rad) < thick / 2 + 0.25, 1.0 - 0.3 * t)
    return v, a


def twinkle(t, cell):
    x, y = grid(cell)
    k = math.sin(t * math.pi)  # 0 -> 1 -> 0 over the loop
    arm = 1.0 + k * 6.0
    v, a = np.zeros((cell, cell)), np.zeros((cell, cell))
    star = np.abs(x) ** 0.5 + np.abs(y) ** 0.5 < arm ** 0.5 + 0.35  # astroid-ish 4-point star
    put(v, a, star, 0.8)
    put(v, a, (np.abs(x) < 1) & (np.abs(y) < 1), 1.0)
    return v, a


GENERATORS = [spark, dust, leaf, snowflake, orb, smoke, ring, twinkle]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--cell", type=int, default=16)
    ap.add_argument("--frames", type=int, default=8)
    a = ap.parse_args()
    t0 = time.time()
    cell, frames = a.cell, a.frames
    sheet = np.zeros((cell * len(ROWS), cell * frames, 4), np.uint8)
    for row, gen in enumerate(GENERATORS):
        for f in range(frames):
            loops = gen in (dust, leaf, snowflake, orb)  # looping effects use f/frames, one-shots f/(frames-1)
            t = f / frames if loops else f / (frames - 1)
            v, al = gen(t, cell)
            g = (np.clip(v, 0, 1) * 255).round().astype(np.uint8)
            y0, x0 = row * cell, f * cell
            sheet[y0:y0 + cell, x0:x0 + cell, 0:3] = g[..., None] * (al[..., None] > 0)
            sheet[y0:y0 + cell, x0:x0 + cell, 3] = (al > 0) * 255
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    px.save_rgba(a.out, sheet)
    print("SHEET_STATS", {"s": round(time.time() - t0, 3), "bytes": os.path.getsize(a.out),
                          "size": sheet.shape[1::-1], "rows": ROWS})


if __name__ == "__main__":
    main()
