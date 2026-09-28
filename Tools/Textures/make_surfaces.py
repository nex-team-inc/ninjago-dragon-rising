"""Tileable 64x64 pixel-art surface sets for the environment (albedo + normal + cavity).

Run:  Tools/.venv/bin/python Tools/Textures/make_surfaces.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Textures/BilliardRogue/Surfaces/<Name>_{Albedo,Normal,Cavity}.png
      + surfaces.json (texel density / material hints), preview contact sheet in DIR.

Maps
- Albedo: RGB, colours quantised to ramps built from the shared model palette (cohesive with the models).
- Normal: tangent space, OpenGL convention (+G up) = Unity NormalMap import. Deliberately exaggerated
  (strong bevels) so the 4-band toon shader picks up crisp edge highlights.
- Cavity: grey, 0.5 = neutral, < 0.5 crevice (darkens), > 0.5 exposed edge (brightens) = ToonLit _CavityMap.
Texel density: designed for 32 texels per metre (one 64 px tile covers 2 x 2 arena cells, slabs/tiles are
~32 px so they line up with 1 m cells when the UVs are cell-aligned).
Deterministic: fixed seeds; re-runs are byte-identical.
"""
import argparse
import os
import sys

import numpy as np
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixelkit as pk  # noqa: E402

S = 64
OUT_DIR = ("Textures", "BilliardRogue", "Surfaces")


# ----------------------------------------------------------------------------------------------
# helpers
# ----------------------------------------------------------------------------------------------

def guillotine(rng, x, y, w, h, min_side=12, max_side=32, out=None):
    out = [] if out is None else out
    can_w, can_h = w >= 2 * min_side, h >= 2 * min_side
    must = w > max_side or h > max_side
    if (must or (rng.random() < 0.45 and (can_w or can_h))) and (can_w or can_h):
        vertical = can_w and (not can_h or w > h or (w == h and rng.random() < 0.5))
        if vertical:
            cut = int(rng.integers(min_side // 4, (w - min_side) // 4 + 1)) * 4
            guillotine(rng, x, y, cut, h, min_side, max_side, out)
            guillotine(rng, x + cut, y, w - cut, h, min_side, max_side, out)
        else:
            cut = int(rng.integers(min_side // 4, (h - min_side) // 4 + 1)) * 4
            guillotine(rng, x, y, w, cut, min_side, max_side, out)
            guillotine(rng, x, y + cut, w, h - cut, min_side, max_side, out)
    else:
        out.append((x, y, w, h))
    return out


def rect_ids(rects, ox, oy, gap=1):
    """Paint rectangles (wrapping) into an id map; `gap` px on the left/top edge of each rect = mortar (-1)."""
    ids = np.full((S, S), -1, np.int32)
    for i, (x, y, w, h) in enumerate(rects):
        for yy in range(y + gap, y + h):
            for xx in range(x + gap, x + w):
                ids[(yy + oy) % S, (xx + ox) % S] = i
    return ids


def row_layout(rows):
    """rows: [(height, [widths...], x_offset)] -> list of rects covering the 64x64 torus."""
    rects, y = [], 0
    for h, widths, off in rows:
        x = off
        for w in widths:
            rects.append((x, y, w, h))
            x += w
        y += h
    return rects


def chip_corners(ids, rects, ox, oy, rng, chance=0.35, max_cut=3):
    for i, (x, y, w, h) in enumerate(rects):
        for cx, cy, sx, sy in ((x + 1, y + 1, 1, 1), (x + w - 1, y + 1, -1, 1),
                               (x + 1, y + h - 1, 1, -1), (x + w - 1, y + h - 1, -1, -1)):
            if rng.random() > chance:
                continue
            cut = int(rng.integers(1, max_cut + 1))
            for a in range(cut):
                for b in range(cut - a):
                    px_, py_ = (cx + sx * a + ox) % S, (cy + sy * b + oy) % S
                    if sy < 0:
                        py_ = (cy - 1 - b + oy) % S
                    if sx < 0:
                        px_ = (cx - 1 - a + ox) % S
                    if ids[py_, px_] == i:
                        ids[py_, px_] = -1
    return ids


def crack(rng, start, steps, drift):
    x, y = start
    pts = [(int(x), int(y))]
    dx, dy = drift
    for _ in range(steps):
        x += dx + rng.uniform(-0.8, 0.8)
        y += dy + rng.uniform(-0.8, 0.8)
        pts.append((int(round(x)), int(round(y))))
    return pk.torus_line(S, pts, 1)


def bevel_light(height):
    """Baked top-left light from the height gradient (pixel-art edge highlight / shadow), wraps."""
    gy, gx = np.gradient(np.pad(height, 1, mode="wrap"))
    return (-gx - gy)[1:-1, 1:-1]


def finish(name, albedo, height, strength, cav_gain=1.4, cav_radius=1.0, cav_extra=None):
    n = pk.normal_from_height(height, strength)
    cav = pk.cavity_from_height(height, cav_radius, cav_gain)
    if cav_extra is not None:
        cav = np.clip(cav + cav_extra, 0, 1)
    cav = np.round(cav * 16) / 16  # posterise: crisp pixel steps
    tilt = float(np.sqrt(n[..., 0] ** 2 + n[..., 1] ** 2).max())
    return {"name": name, "albedo": albedo.astype(np.uint8), "normal": pk.encode_normal(n), "normal_f": n,
            "cavity": cav, "height": height, "maxTilt": round(tilt, 3)}


def tone_to_albedo(tone, ramp, dither=0.035):
    t = np.clip(tone + pk.bayer(S, S) * dither, 0, 1)
    return pk.quantize_to_ramp(t, ramp)


# ----------------------------------------------------------------------------------------------
# surfaces
# ----------------------------------------------------------------------------------------------

def stone_floor():
    rng = np.random.default_rng(11)
    pal = pk.palette()
    rects = guillotine(rng, 0, 0, S, S, 12, 32)
    ox, oy = 5, 3
    ids = rect_ids(rects, ox, oy, gap=1)
    ids = chip_corners(ids, rects, ox, oy, rng, 0.4, 3)
    solid = ids >= 0
    dist = pk.torus_distance(solid)
    noise = pk.fbm(S, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    tone_of = rng.uniform(-0.12, 0.12, len(rects))
    tilt = rng.uniform(-0.035, 0.035, (len(rects), 2))
    x, y = np.meshgrid(np.arange(S), np.arange(S))
    slab_tone = np.where(solid, tone_of[np.maximum(ids, 0)], 0)
    tilt_h = np.where(solid, tilt[np.maximum(ids, 0), 0] * ((x - ox) % 16 - 8) + tilt[np.maximum(ids, 0), 1] * ((y - oy) % 16 - 8), 0)
    cracks = np.zeros((S, S), bool)
    for _ in range(5):
        cracks |= crack(rng, (rng.uniform(0, S), rng.uniform(0, S)), int(rng.integers(4, 9)),
                        (rng.choice([-1, 1]) * rng.uniform(0.4, 1), rng.uniform(0.4, 1)))
    cracks &= solid & (dist >= 2)
    bevel = np.clip(dist / 3.0, 0, 1) ** 0.8
    height = np.where(solid, 0.45 + 0.4 * bevel + 0.08 * noise + tilt_h + 0.03 * fine, 0.08)
    height[cracks] -= 0.22
    pits = (rng.random((S, S)) < 0.025) & solid & (dist >= 2)
    height[pits] -= 0.12
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.3
    tone = np.where(solid, 0.56 + slab_tone + 0.22 * (noise - 0.5) + 0.08 * (fine - 0.5) + light, 0.06)
    tone[cracks] = 0.2
    tone[pits] -= 0.12
    warm = rng.random(len(rects)) < 0.4
    cool_rgb = tone_to_albedo(tone, pal.mix("gray", "brown", 0.22, range(1, 12, 2)))
    warm_rgb = tone_to_albedo(tone, pal.mix("gray", "brown", 0.5, range(1, 12, 2)))
    albedo = np.where((solid & warm[np.maximum(ids, 0)])[..., None], warm_rgb, cool_rgb)
    return finish("StoneFloor", albedo, height, strength=4.5)


def mossy_brick():
    rng = np.random.default_rng(23)
    pal = pk.palette()
    rows = [(8, [16, 16, 16, 16], (i % 2) * 8 + (3 if i % 4 == 3 else 0)) for i in range(8)]
    rects = row_layout(rows)
    ox, oy = 2, 1
    ids = rect_ids(rects, ox, oy, gap=1)
    ids = chip_corners(ids, rects, ox, oy, rng, 0.3, 2)
    solid = ids >= 0
    dist = pk.torus_distance(solid)
    noise = pk.fbm(S, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    tone_of = rng.uniform(-0.13, 0.13, len(rects))
    brick_tone = np.where(solid, tone_of[np.maximum(ids, 0)], 0)
    bevel = np.clip(dist / 2.0, 0, 1)
    height = np.where(solid, 0.5 + 0.35 * bevel + 0.08 * noise + 0.04 * fine, 0.1)
    # moss: patchy noise, heavier on brick tops and in mortar, drips downward
    moss_field = pk.fbm(S, rng, ((4, 0.6), (8, 0.3), (16, 0.1)))
    y_in_brick = (np.arange(S)[:, None] - oy) % 8
    top_bias = np.where(y_in_brick <= 2, 0.14, 0.0) + np.where(~solid, 0.1, 0.0)
    moss = (moss_field + top_bias) > 0.6
    drips = np.zeros_like(moss)
    for _ in range(26):
        mx, my = int(rng.integers(0, S)), int(rng.integers(0, S))
        if moss[my, mx]:
            for k in range(1, int(rng.integers(2, 5))):
                drips[(my + k) % S, mx] = True
    moss = pk.erode(pk.dilate(moss, False), False) | drips | (moss & (rng.random((S, S)) < 0.97))
    moss_bump = pk.value_noise(S, 32, rng, order=0)
    height = np.where(moss, np.maximum(height, 0.55) + 0.18 * moss_bump + 0.1, height)
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.2
    tone = np.where(solid, 0.55 + brick_tone + 0.2 * (noise - 0.5) + 0.08 * (fine - 0.5) + light, 0.08)
    stone = tone_to_albedo(tone, pal.mix("gray", "brown", 0.22, range(1, 11, 2)))
    moss_edge = moss & ~pk.erode(moss, False)
    moss_tone = 0.5 + 0.35 * (moss_bump - 0.5) + light * 0.8
    moss_tone = np.where(moss_edge & pk.shift(~moss, 0, -1), moss_tone - 0.25, moss_tone)  # darker bottom lip
    moss_ramp = np.concatenate([pal.ramp("green", [2, 4]), pal.mix("green", "lime", 0.5, [6, 8, 10, 12])])
    moss_rgb = tone_to_albedo(moss_tone, moss_ramp, 0.08)
    albedo = np.where(moss[..., None], moss_rgb, stone)
    cav_extra = np.where(moss, 0.04, 0.0)
    return finish("MossyBrick", albedo, height, strength=4.5, cav_extra=cav_extra)


def crypt_brick():
    rng = np.random.default_rng(37)
    pal = pk.palette()
    rows = [(16, [24, 20, 20], 0), (12, [16, 28, 20], 10), (20, [20, 24, 20], 4), (16, [28, 16, 20], 14)]
    rects = row_layout(rows)
    ox, oy = 3, 2
    ids = rect_ids(rects, ox, oy, gap=2)
    ids = chip_corners(ids, rects, ox, oy, rng, 0.45, 3)
    solid = ids >= 0
    dist = pk.torus_distance(solid)
    noise = pk.fbm(S, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    tone_of = rng.uniform(-0.12, 0.12, len(rects))
    soot = rng.random(len(rects)) < 0.25
    block_tone = np.where(solid, tone_of[np.maximum(ids, 0)] - np.where(soot[np.maximum(ids, 0)], 0.12, 0), 0)
    bevel = np.clip(dist / 3.0, 0, 1) ** 0.7
    height = np.where(solid, 0.4 + 0.45 * bevel + 0.1 * noise + 0.03 * fine, 0.02)
    cracks = np.zeros((S, S), bool)
    for _ in range(4):
        cracks |= crack(rng, (rng.uniform(0, S), rng.uniform(0, S)), int(rng.integers(5, 10)),
                        (rng.uniform(-0.6, 0.6), 1.0))
    cracks &= solid & (dist >= 2)
    height[cracks] -= 0.25
    height = np.clip(height, 0, 1)
    # vertical damp streaks under the mortar lines
    streak = np.zeros((S, S), np.float32)
    for _ in range(9):
        sx, sy = int(rng.integers(0, S)), int(rng.integers(0, S))
        length = int(rng.integers(4, 11))
        for k in range(length):
            streak[(sy + k) % S, sx] = max(streak[(sy + k) % S, sx], 1 - k / length)
    light = bevel_light(height) * 1.2
    tone = np.where(solid, 0.55 + block_tone + 0.2 * (noise - 0.5) + 0.07 * (fine - 0.5) + light - 0.14 * streak, 0.03)
    tone[cracks] = 0.12
    ramp = pal.mix("gray", "blue", 0.3, range(1, 11, 2))
    albedo = tone_to_albedo(tone, ramp)
    return finish("CryptBrick", albedo, height, strength=5.0, cav_gain=1.5)


def crypt_floor():
    rng = np.random.default_rng(41)
    pal = pk.palette()
    rects = [(0, 0, 32, 32), (32, 0, 32, 32), (0, 32, 32, 32), (32, 32, 32, 32)]
    ox, oy = 0, 0
    ids = rect_ids(rects, ox, oy, gap=2)
    ids = chip_corners(ids, rects, ox, oy, rng, 0.5, 3)
    solid = ids >= 0
    dist = pk.torus_distance(solid)
    noise = pk.fbm(S, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    x, y = np.meshgrid(np.arange(S), np.arange(S))
    lx, ly = x % 32, y % 32  # local coords inside a tile (gap occupies 0..1)
    checker = ((x // 32 + y // 32) % 2).astype(np.float32)
    # engraved inset groove + a diamond rune on the darker tiles
    groove = solid & (((lx == 6) | (lx == 27)) & (ly >= 6) & (ly <= 27) | ((ly == 6) | (ly == 27)) & (lx >= 6) & (lx <= 27))
    cxy = np.abs(lx - 16.5) + np.abs(ly - 16.5)
    diamond = solid & (checker > 0) & (np.abs(cxy - 5) < 0.6)
    dot = solid & (checker > 0) & (cxy < 1.2)
    corner_studs = solid & (checker < 1) & (((lx - 16.5) ** 2 + (ly - 16.5) ** 2) < 3.0)
    bevel = np.clip(dist / 2.5, 0, 1)
    height = np.where(solid, 0.45 + 0.4 * bevel + 0.07 * noise + 0.03 * fine, 0.03)
    height[groove | diamond] -= 0.28
    height[dot | corner_studs] += 0.12
    cracks = np.zeros((S, S), bool)
    for _ in range(4):
        cracks |= crack(rng, (rng.uniform(0, S), rng.uniform(0, S)), int(rng.integers(4, 8)),
                        (rng.choice([-1, 1]) * rng.uniform(0.5, 1), rng.choice([-1, 1]) * rng.uniform(0.5, 1)))
    cracks &= solid & (dist >= 2) & ~groove
    height[cracks] -= 0.22
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.2
    tone = np.where(solid, 0.52 + 0.08 * (checker - 0.5) + 0.2 * (noise - 0.5) + 0.07 * (fine - 0.5) + light, 0.04)
    tone[groove | diamond | cracks] -= 0.2
    ramp = pal.mix("gray", "indigo", 0.35, range(1, 10, 2))
    albedo = tone_to_albedo(tone, ramp)
    return finish("CryptFloor", albedo, height, strength=5.0)


def wood_plank():
    rng = np.random.default_rng(53)
    pal = pk.palette()
    plank_h = 8
    grain = pk.aniso_noise(S, 4, 32, rng)
    grain2 = pk.aniso_noise(S, 8, 64, rng)
    x, y = np.meshgrid(np.arange(S), np.arange(S))
    row = y // plank_h
    ly = y % plank_h
    joints = [int(v) for v in rng.permutation(np.arange(4, 60, 7))[:8]]
    plank_id = row * 2 + ((x - np.array(joints)[row]) % S > S // 2).astype(int)
    gap = (ly == plank_h - 1)
    for r, j in enumerate(joints):
        gap |= (row == r) & (x == j)
    solid = ~gap
    dist = pk.torus_distance(solid)
    tone_of = rng.uniform(-0.12, 0.12, 16)
    plank_tone = tone_of[plank_id % 16]
    rings = np.sin((grain * 9 + grain2 * 3 + ly * 0.15) * np.pi)
    streaks = (rings > 0.55).astype(np.float32) - (rings < -0.7).astype(np.float32)
    knots = np.zeros((S, S), bool)
    knot_ring = np.zeros((S, S), bool)
    for _ in range(3):
        kx, ky = rng.uniform(0, S), (int(rng.integers(0, 8)) * plank_h + 3.5)
        knots |= pk.torus_ellipse(S, kx, ky, 1.6, 1.1)
        knot_ring |= pk.torus_ellipse(S, kx, ky, 3.2, 2.0) & ~pk.torus_ellipse(S, kx, ky, 2.2, 1.3)
    nails = np.zeros((S, S), bool)
    for r, j in enumerate(joints):
        for dxn in (-2, 2):
            for yy in (2, 5):
                nails[(r * plank_h + yy) % S, (j + dxn) % S] = True
    bevel = np.clip(dist / 1.5, 0, 1)
    height = np.where(solid, 0.5 + 0.3 * bevel + 0.06 * streaks + 0.04 * grain2, 0.05)
    height[knots] -= 0.1
    height[nails] = 0.95
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.0
    tone = np.where(solid, 0.55 + plank_tone + 0.12 * streaks + 0.1 * (grain2 - 0.5) + light, 0.05)
    tone[knot_ring & solid] -= 0.12
    tone[knots] = 0.2
    tone[ly == 0] += 0.08  # sunlit top lip of every plank
    albedo = tone_to_albedo(tone, pal.mix("brown", "orange", 0.2, range(1, 12, 2)))
    nail_rgb = np.array(pal("gray", 9), np.float32)
    albedo[nails] = nail_rgb
    return finish("WoodPlank", albedo, height, strength=4.0, cav_gain=1.3)


def crystal_rock():
    rng = np.random.default_rng(67)
    pal = pk.palette()
    pts = [(rng.uniform(0, S), rng.uniform(0, S)) for _ in range(14)]
    labels, border = pk.torus_voronoi(S, pts)
    x, y = np.meshgrid(np.arange(S) + 0.5, np.arange(S) + 0.5)
    h = np.zeros((S, S), np.float32)
    shade = np.zeros((S, S), np.float32)
    grads = rng.uniform(-1, 1, (len(pts), 2))
    grads /= np.linalg.norm(grads, axis=1, keepdims=True)
    for i, (px_, py_) in enumerate(pts):
        m = labels == i
        dx = (x - px_ + S / 2) % S - S / 2
        dy = (y - py_ + S / 2) % S - S / 2
        slope = rng.uniform(0.018, 0.035)
        plane = 0.62 + slope * (grads[i, 0] * dx + grads[i, 1] * dy)
        h[m] = plane[m]
        shade[m] = -0.5 * grads[i, 0] - 0.5 * grads[i, 1]  # facets tilted toward the top-left read lighter
    crevice = border < 1.0
    h = np.where(crevice, 0.12, h)
    h = np.where((border >= 1.0) & (border < 2.0), h - 0.12, h)
    noise = pk.fbm(S, rng)
    h += 0.05 * (noise - 0.5)
    # crystals: hexagonal prisms (3 visible faces) growing out of the rock, drawn back to front
    crystal = np.zeros((S, S), bool)
    c_tone = np.zeros((S, S), np.float32)
    c_height = np.zeros((S, S), np.float32)
    prisms = []
    for cx, cy, n_prisms, scale in ((17, 44, 4, 1.0), (47, 15, 3, 0.85), (50, 50, 1, 0.55), (28, 16, 1, 0.5)):
        for k in range(n_prisms):
            spread = (k - (n_prisms - 1) / 2)
            ang = np.deg2rad(-90 + spread * 26 + rng.uniform(-8, 8))
            length = (15 if k == n_prisms // 2 else rng.uniform(9, 12)) * scale
            half_w = (3.2 if k == n_prisms // 2 else 2.6) * scale
            prisms.append((cx + spread * 3.0 * scale, cy + abs(spread) * 1.5, ang, length, max(half_w, 1.6)))
    prisms.sort(key=lambda p: -p[3])  # longest (back) first
    light2d = np.array([-0.62, -0.78])
    for bx, by, ang, length, hw in prisms:
        ax, ay = np.cos(ang), np.sin(ang)
        nx, ny = -ay, ax
        body = length * 0.72
        pts_poly = [(bx + nx * hw, by + ny * hw), (bx + ax * body + nx * hw, by + ay * body + ny * hw),
                    (bx + ax * length, by + ay * length), (bx + ax * body - nx * hw, by + ay * body - ny * hw),
                    (bx - nx * hw, by - ny * hw)]
        m = pk.torus_poly(S, pts_poly)
        dxl = (x - bx + S / 2) % S - S / 2
        dyl = (y - by + S / 2) % S - S / 2
        v = dxl * nx + dyl * ny
        u = dxl * ax + dyl * ay
        third = hw / 3.0
        face = np.where(v < -third, 0, np.where(v > third, 2, 1))
        # face normals: centre faces the viewer, sides tilt +-60deg around the prism axis
        fn = [np.array([-nx, -ny]) * 0.87, np.array([0.0, 0.0]), np.array([nx, ny]) * 0.87]
        tones = np.array([0.55 + 0.45 * float(np.dot(f, -light2d)) for f in fn])
        t = tones[face] + np.where(u > body, 0.12, 0.0)
        c_tone = np.where(m, np.clip(t, 0.1, 1.0), c_tone)
        ridge = np.clip((hw - np.abs(v)) / (hw * 2 / 3), 0, 1)
        c_height = np.where(m, 0.7 + 0.28 * ridge, c_height)
        crystal |= m
    rim = pk.dilate(crystal, False) & ~crystal
    height = np.where(crystal, c_height, h)
    height = np.where(rim, 0.15, height)
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.1
    tone = 0.48 + 0.18 * shade + 0.12 * (noise - 0.5) + light * 0.8
    tone = np.where(crevice, 0.05, tone)
    rock_ramp = pal.mix("indigo", "purple", 0.45, range(1, 10, 2))
    rock = tone_to_albedo(tone, rock_ramp, 0.05)
    crystal_ramp = np.concatenate([pal.ramp("teal", [6]), pal.ramp("cyan", [8, 10, 12, 14]), [[226, 255, 252]]]).astype(np.float32)
    c_rgb = tone_to_albedo(c_tone, crystal_ramp, 0.0)
    albedo = np.where(crystal[..., None], c_rgb, rock)
    albedo[rim] = pal("indigo", 1)
    return finish("CrystalRock", albedo, height, strength=4.5, cav_gain=1.3)


def dirt():
    rng = np.random.default_rng(71)
    pal = pk.palette()
    noise = pk.fbm(S, rng)
    clods = pk.value_noise(S, 8, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    clod = clods > 0.62
    clod_d = np.clip(pk.torus_distance(clod) / 2.0, 0, 1)
    damp = pk.value_noise(S, 4, rng)
    height = 0.35 + 0.18 * noise + 0.08 * fine + 0.18 * clod_d
    # low-contrast macro variation so 2x2-cell tiling does not read as repeating blotches
    tone = 0.5 + 0.16 * (noise - 0.5) + 0.14 * (fine - 0.5) + 0.07 * clod_d - 0.06 * (damp < 0.35)
    pebbles = np.zeros((S, S), bool)
    p_tone = np.zeros((S, S), np.float32)
    x, y = np.meshgrid(np.arange(S) + 0.5, np.arange(S) + 0.5)
    for _ in range(22):
        cx, cy = rng.uniform(0, S), rng.uniform(0, S)
        rx, ry = rng.uniform(1.1, 3.0), rng.uniform(1.0, 2.2)
        m = pk.torus_ellipse(S, cx, cy, rx, ry, rng.uniform(0, 180))
        dx = (x - cx + S / 2) % S - S / 2
        dy = (y - cy + S / 2) % S - S / 2
        dome = np.clip(1 - np.sqrt((dx / rx) ** 2 + (dy / ry) ** 2), 0, 1)
        height = np.where(m, 0.6 + 0.35 * dome, height)
        p_tone = np.where(m, 0.55 + 0.35 * np.clip(-(dx + dy) / (rx + ry), -1, 1) + rng.uniform(-0.1, 0.1), p_tone)
        pebbles |= m
    twigs = np.zeros((S, S), bool)
    for _ in range(4):
        sx, sy = rng.uniform(0, S), rng.uniform(0, S)
        ang = rng.uniform(0, np.pi)
        ln = rng.uniform(4, 8)
        twigs |= pk.torus_line(S, [(sx, sy), (sx + np.cos(ang) * ln, sy + np.sin(ang) * ln)], 1)
    twigs &= ~pebbles
    height = np.where(twigs, height + 0.15, height)
    cracks = np.zeros((S, S), bool)
    for _ in range(3):
        cracks |= crack(rng, (rng.uniform(0, S), rng.uniform(0, S)), int(rng.integers(5, 9)), (rng.uniform(-1, 1), rng.uniform(-1, 1)))
    cracks &= ~pebbles & ~twigs
    height[cracks] -= 0.2
    shadow = pk.shift(pebbles, 1, 1) & ~pebbles  # pebble contact shadow toward bottom-right
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 0.9
    tone = tone + light
    tone[shadow] -= 0.18
    tone[cracks] = 0.12
    soil = tone_to_albedo(tone, pal.mix("brown", "orange", 0.22, range(1, 10, 2)), 0.08)
    stone = tone_to_albedo(p_tone, pal.mix("gray", "brown", 0.5, range(4, 13, 2)), 0.0)
    twig = tone_to_albedo(0.3 + light, pal.ramp("brown", [2, 3, 5, 7]), 0.0)
    albedo = np.where(pebbles[..., None], stone, np.where(twigs[..., None], twig, soil))
    return finish("Dirt", albedo, height, strength=6.5, cav_gain=1.3)


def grass():
    rng = np.random.default_rng(83)
    pal = pk.palette()
    noise = pk.fbm(S, rng)
    fine = pk.value_noise(S, 32, rng, order=1)
    patches = pk.value_noise(S, 4, rng)
    height = 0.2 + 0.15 * noise + 0.05 * fine
    tone = 0.25 + 0.2 * (noise - 0.5) + 0.08 * (fine - 0.5) + 0.1 * (patches - 0.5)
    kind = np.zeros((S, S), np.int8)  # 0 ground, 1 blade, 2 flower
    blades = []
    for _ in range(300):
        blades.append((rng.uniform(0, S), rng.uniform(0, S), rng.uniform(-0.55, 0.55), int(rng.integers(3, 7)), rng.uniform(-0.08, 0.1)))
    blades.sort(key=lambda b: b[1])  # painter's order: lower (nearer) blades drawn last
    for bx, by, lean, ln, tint in blades:
        for k in range(ln):
            t = k / max(ln - 1, 1)
            px_ = int(round(bx + lean * k)) % S
            py_ = int(round(by - k)) % S
            height[py_, px_] = 0.45 + 0.5 * t
            tone[py_, px_] = 0.4 + 0.5 * t + tint + 0.16 * (patches[py_, px_] - 0.5)
            kind[py_, px_] = 1
    flowers = []
    for _ in range(5):
        fx, fy = int(rng.integers(0, S)), int(rng.integers(0, S))
        flowers.append((fx, fy, int(rng.integers(0, 2))))
        for ddx, ddy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            kind[(fy + ddy) % S, (fx + ddx) % S] = 2
            height[(fy + ddy) % S, (fx + ddx) % S] = 0.8
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 0.6
    tone = np.clip(tone + light, 0, 1)
    ramp = np.concatenate([pal.ramp("green", [1, 2, 3]), pal.mix("green", "lime", 0.45, [5, 7, 9, 11, 13])])
    albedo = tone_to_albedo(tone, ramp, 0.05)
    for fx, fy, c in flowers:
        petal = pal("yellow", 14) if c == 0 else pal("gray", 14)
        for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            albedo[(fy + ddy) % S, (fx + ddx) % S] = petal
        albedo[fy % S, fx % S] = pal("orange", 11)
    return finish("Grass", albedo, height, strength=4.5, cav_gain=1.1)


SURFACES = [stone_floor, mossy_brick, crypt_brick, crypt_floor, wood_plank, crystal_rock, dirt, grass]


# ----------------------------------------------------------------------------------------------
# preview
# ----------------------------------------------------------------------------------------------

def lit(s, light=(-0.55, 0.55, 0.63), bands=4, cavity_strength=1.0, tile=1):
    n = s["normal_f"]
    L = np.array(light, np.float32)
    L /= np.linalg.norm(L)
    ndl = np.clip((n * L).sum(-1), 0, 1)
    band = np.ceil(ndl * bands) / bands
    shade = 0.3 + 0.75 * band
    col = s["albedo"].astype(np.float32) * shade[..., None] * (1 + (s["cavity"][..., None] - 0.5) * cavity_strength)
    img = np.clip(col, 0, 255).astype(np.uint8)
    img = np.tile(img, (tile, tile, 1))
    return np.dstack([img, np.full(img.shape[:2], 255, np.uint8)])


def rgb4(a):
    return np.dstack([a, np.full(a.shape[:2], 255, np.uint8)])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    os.makedirs(preview_dir, exist_ok=True)
    items_maps, items_lit, meta = [], [], {}
    for gen in SURFACES:
        s = gen()
        base = pk.staging(*OUT_DIR, s["name"])
        pk.save_rgb(base + "_Albedo.png", s["albedo"])
        pk.save_rgb(base + "_Normal.png", s["normal"])
        pk.save_gray(base + "_Cavity.png", s["cavity"])
        print(" ", s["name"], "max normal tilt", s["maxTilt"])
        meta[s["name"]] = {"size": [S, S], "texelsPerMetre": 32, "tileMetres": 2,
                           "maps": {k: f"{s['name']}_{k}.png" for k in ("Albedo", "Normal", "Cavity")}}
        items_maps += [(s["name"] + " albedo", rgb4(s["albedo"])), ("normal", rgb4(s["normal"])),
                       ("cavity", rgb4((np.repeat(s["cavity"][..., None], 3, -1) * 255).astype(np.uint8)))]
        items_lit.append((s["name"] + " lit 2x2", lit(s, tile=2)))
    pk.save_json(pk.staging(*OUT_DIR, "surfaces.json"), {
        "note": "Generated by Tools/Textures/make_surfaces.py. Normal = OpenGL (+G up), Cavity 0.5 = neutral.",
        "surfaces": meta})
    pk.save_rgb(os.path.join(preview_dir, "surfaces_maps.png"), pk.contact_sheet(items_maps, scale=3, cols=6, checker_bg=False))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_lit.png"), pk.contact_sheet(items_lit, scale=2, cols=4, checker_bg=False))
    print("SURFACES", {"count": len(SURFACES), "out": os.path.join(pk.STAGING_ASSETS, *OUT_DIR), "preview": preview_dir})


if __name__ == "__main__":
    main()
