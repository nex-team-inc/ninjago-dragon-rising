"""Tileable 64x64 pixel-art surface sets for the environment (albedo + normal + cavity).

Run:  Tools/.venv/bin/python Tools/Textures/make_surfaces.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Textures/BilliardRogue/Surfaces/<Name>_{Albedo,Normal,Cavity}.png
      + surfaces.json (texel density / material hints), preview contact sheet in DIR.

Maps
- Albedo: RGB, colours quantised to ramps built from the shared model palette (cohesive with the models).
- Normal: tangent space, OpenGL convention (+G up) = Unity NormalMap import. Deliberately exaggerated
  (strong bevels) so the 4-band toon shader picks up crisp edge highlights.
- Cavity: grey, 0.5 = neutral, < 0.5 crevice (darkens), > 0.5 exposed edge (brightens) = ToonLit _CavityMap.
Texel density: 32 texels per metre = one 64 px tile per 2 x 2 m (surfaces.json tileMetres 2). At the default camera a
cell is ~28-30 px on the 640x360 world RT, so 32 texels/m is ~1:1 under point sampling without mips. That needs
M_Surface_* `_Tiling` = 1 / tileMetres = 0.5 (surfaces.json "tiling"): the environment's *_Surface meshes use
1 UV = 1 m, and `_Tiling` 1 would squeeze 64 texels into every metre (2.3:1 minification: mortar lines drop out,
the surface shimmers). ToonLit should sample *_Surface parts with a world-space box projection in the arena frame,
uv = (arenaLocal + (0.5, 0, 0.4)) * _Tiling (Tools/Blender/environment/render_common._world_box), so the UV origin
sits on a cell corner (x = -3.5 + k, z = launchZoneHeight 1.6 + k): only then do CryptFloor's 1 m slabs and the
32 px brick/slab modules line up with the gameplay cells.
Previews: surfaces_maps.png, surfaces_lit.png, surfaces_gamescale.png (+ _act1.._kit, 2x): the arena rendered at
640x360 from the default camera (ArenaConfig: (0, 20.4, -6.15), pitch 58, fov 28) with the act surface sets,
world-box UVs at _Tiling 0.5, point sampling, and outlined HP numbers from the built fonts;
surfaces_gamescale_tiled.png: every surface as one ground plane from that camera (repetition check).
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


def poisson_torus(rng, count, min_dist, tries=400):
    """`count` points on the 64 px torus at least `min_dist` apart (irregular, but never clumped or lattice-like)."""
    pts = []
    for _ in range(tries):
        if len(pts) == count:
            break
        p = rng.uniform(0, S, 2)
        if all(np.hypot(*((p - q + S / 2) % S - S / 2)) >= min_dist for q in pts):
            pts.append(p)
    return [(float(x), float(y)) for x, y in pts]


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
    # moss: 7 small irregular patches that creep along the brick ledges (a 2 m tile repeats ~9x along an arena wall,
    # so no large high-contrast blobs: those read as a polka-dot grid), 2 muted greens close to the brick value
    moss = np.zeros((S, S), bool)
    centres = poisson_torus(rng, 7, 15.0)
    edge_noise = pk.value_noise(S, 16, rng, order=1)
    mortar_row = ~solid & np.roll(solid, 1, axis=0)  # mortar pixels directly under a brick = ledge joints
    for k, (mx, my) in enumerate(centres):
        my = (round((my - oy) / 8) * 8 + oy + int(rng.integers(0, 2))) % S  # sit on a ledge (brick top)
        rx, ry = rng.uniform(3.5, 7.5), rng.uniform(1.3, 2.4)  # flat cushions that creep along the ledge
        blob = pk.torus_ellipse(S, mx + 0.5, my + 0.5, rx, ry, rng.uniform(-6, 6))
        blob &= edge_noise > rng.uniform(0.25, 0.38)  # ragged, never a clean ellipse
        moss |= blob
        if k % 2 == 0:  # moss running along the mortar joint next to the cushion
            run = int(rng.integers(5, 11))
            x0 = int(mx + rng.choice([-1, 1]) * (rx + 1))
            for dx in range(run):
                for dy in (-1, 0, 1):
                    yy, xx = (my + dy) % S, (x0 + dx) % S
                    if mortar_row[yy, xx]:
                        moss[yy, xx] = True
    drips = np.zeros_like(moss)
    ys, xs = np.nonzero(moss & ~np.roll(moss, -1, axis=0))  # bottom edge pixels
    order = rng.permutation(len(xs))[:7]
    for i in order:
        for k in range(1, int(rng.integers(2, 4))):
            drips[(ys[i] + k) % S, xs[i]] = True
    moss = pk.erode(pk.dilate(moss, False), False) | drips
    moss_bump = pk.value_noise(S, 32, rng, order=0)
    height = np.where(moss, np.maximum(height, 0.55) + 0.12 * moss_bump + 0.06, height)
    height = np.clip(height, 0, 1)
    light = bevel_light(height) * 1.2
    tone = np.where(solid, 0.55 + brick_tone + 0.2 * (noise - 0.5) + 0.08 * (fine - 0.5) + light, 0.08)
    stone = tone_to_albedo(tone, pal.mix("gray", "brown", 0.22, range(1, 11, 2)))
    lit_side = (moss_bump + light * 1.5) > 0.5
    lower_lip = moss & pk.shift(~moss, 0, -1)
    moss_dark = pal.mix("green", "gray", 0.45, [6])[0]   # value close to the mid brick shades
    moss_lite = pal.mix("lime", "gray", 0.4, [9])[0]
    albedo = stone.astype(np.float32)
    albedo[moss & ~lit_side] = moss_dark
    albedo[moss & lit_side] = moss_lite
    albedo[lower_lip] = moss_dark
    cav_extra = np.where(moss, 0.03, 0.0)
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
    # only 2 small, muted specks: the 3D crystal clusters carry the sparkle, and bright cyan would repeat as a lattice
    # along the Act 3 walls and compete with the cyan / ice gameplay colours
    for cx, cy, n_prisms, scale in ((19, 44, 1, 0.42), (48, 17, 1, 0.34)):
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
    crystal_ramp = np.concatenate([pal.mix("cyan", "indigo", 0.55, [5, 7, 9, 11]), pal.mix("cyan", "gray", 0.6, [12, 13])]).astype(np.float32)
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


# ----------------------------------------------------------------------------------------------
# game-scale preview: the arena at 640x360 from the default camera, point-sampled world-box UVs
# ----------------------------------------------------------------------------------------------

CAMERA = {"pos": (0.0, 20.4, -6.15), "pitch": 58.0, "fov": 28.0, "size": (640, 360)}  # ArenaConfig defaults
ARENA = {"half_w": 3.5, "rows_z0": 1.6, "rows": 10, "z1": 11.6, "wall_t": 0.4, "wall_h": 0.52, "ground_y": -0.2}
UV_OFFSET = (0.5, 0.0, 0.4)  # arena-local offset that puts the UV origin on a cell corner
TILING = 0.5                 # 1 / tileMetres
# (label, floor, wall side, wall top, ground) - Tools/Blender/environment/make_layouts.py act surface sets
# (ground None = the palette cave floor tile of Act 3, drawn flat)
ACT_SETS = [("act1", "StoneFloor", "MossyBrick", "StoneFloor", "Grass"),
            ("act2", "CryptFloor", "CryptBrick", "CryptFloor", "CryptBrick"),
            ("act3", "StoneFloor", "CrystalRock", "CrystalRock", None),
            ("kit", "WoodPlank", "MossyBrick", "Dirt", "Dirt")]
# (col, row from the top, text, rgb) HP numbers over enemy cells + one damage number
HP_LABELS = [(1, 2, "12", (255, 255, 255)), (3, 3, "8", (255, 255, 255)), (5, 1, "24", (255, 255, 255)),
             (6, 4, "5", (255, 255, 255)), (0, 5, "30", (255, 255, 255)), (2, 6, "-7", (255, 214, 90)),
             (4, 7, "CRIT!", (255, 122, 58))]


def camera_basis():
    p = np.deg2rad(CAMERA["pitch"])
    return (np.array(CAMERA["pos"], np.float64), np.array([0.0, -np.sin(p), np.cos(p)]),
            np.array([0.0, np.cos(p), np.sin(p)]), np.array([1.0, 0.0, 0.0]))


def project(point):
    c, f, u, r = camera_basis()
    w, h = CAMERA["size"]
    th = np.tan(np.deg2rad(CAMERA["fov"]) / 2)
    d = np.array(point, np.float64) - c
    z = d @ f
    return (w / 2 * (1 + (d @ r) / (z * th * w / h)), h / 2 * (1 - (d @ u) / (z * th)))


def raycast_arena():
    """-> (kind (0 floor, 1 wall side, 2 wall top, 3 ground, -1 sky), U, V, face shade) per 640x360 pixel."""
    c, f, u, r = camera_basis()
    w, h = CAMERA["size"]
    th = np.tan(np.deg2rad(CAMERA["fov"]) / 2)
    px, py = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    d = f + r * ((px / w * 2 - 1) * th * w / h)[..., None] + u * ((1 - py / h * 2) * th)[..., None]
    hw, z0, z1 = ARENA["half_w"], 0.0, ARENA["z1"]  # floor spans the launch zone (z 0) to the far wall
    wt, wh, gy = ARENA["wall_t"], ARENA["wall_h"], ARENA["ground_y"]
    best = np.full((h, w), np.inf)
    kind = np.full((h, w), -1, np.int8)
    U, V, shade = np.zeros((h, w)), np.zeros((h, w)), np.ones((h, w))
    ox, oy, oz = UV_OFFSET

    def hit(t, ok, k, uu, vv, sh):
        ok = ok & (t > 0) & (t < best)
        best[ok], kind[ok], U[ok], V[ok], shade[ok] = t[ok], k, uu[ok], vv[ok], sh

    def plane(axis, value):
        with np.errstate(divide="ignore", invalid="ignore"):
            t = (value - c[axis]) / d[..., axis]
        return t, c + d * t[..., None]

    t, p = plane(1, 0.0)  # arena floor
    hit(t, (np.abs(p[..., 0]) <= hw) & (p[..., 2] >= z0) & (p[..., 2] <= z1), 0, p[..., 0] + ox, p[..., 2] + oz, 1.0)
    t, p = plane(1, gy)   # ground outside the rim
    outside = (np.abs(p[..., 0]) > hw + wt) | (p[..., 2] > z1 + wt) | (p[..., 2] < z0 - wt)
    hit(t, outside, 3, p[..., 0] + ox, p[..., 2] + oz, 1.0)
    t, p = plane(1, wh)   # coping of the three rim walls
    ax, pz = np.abs(p[..., 0]), p[..., 2]
    top = ((ax >= hw) & (ax <= hw + wt) & (pz >= z0 - wt) & (pz <= z1 + wt)) | ((pz >= z1) & (pz <= z1 + wt) & (ax <= hw + wt))
    hit(t, top, 2, p[..., 0] + ox, p[..., 2] + oz, 1.0)
    for sign, sh in ((-1, 0.92), (1, 0.72)):  # inner faces: left wall faces +x (sunlit), right wall faces -x
        t, p = plane(0, sign * hw)
        ok = (p[..., 1] >= 0) & (p[..., 1] <= wh) & (p[..., 2] >= z0) & (p[..., 2] <= z1)
        hit(t, ok, 1, -sign * (p[..., 2] + oz), p[..., 1] + oy, sh)
    t, p = plane(2, z1)   # far wall, facing the camera
    hit(t, (np.abs(p[..., 0]) <= hw) & (p[..., 1] >= 0) & (p[..., 1] <= wh), 1, p[..., 0] + ox, p[..., 1] + oy, 0.85)
    t, p = plane(2, z0 - wt)  # south end faces of the side walls
    ok = (np.abs(p[..., 0]) >= hw) & (np.abs(p[..., 0]) <= hw + wt) & (p[..., 1] >= gy) & (p[..., 1] <= wh)
    hit(t, ok, 1, p[..., 0] + ox, p[..., 1] + oy, 0.8)
    t, p = plane(2, z0)       # front lip of the arena floor (0.2 m step down to the ground)
    ok = (np.abs(p[..., 0]) <= hw) & (p[..., 1] >= gy) & (p[..., 1] <= 0)
    hit(t, ok, 0, p[..., 0] + ox, p[..., 1] + oy, 0.8)
    return kind, U, V, shade


def draw_hp_labels(img):
    """Outlined HP numbers (BilliardPixel-Bold over BilliardPixel-BoldOutline at 16 px = 1x), if the fonts exist."""
    from PIL import Image, ImageDraw, ImageFont
    fonts = os.path.join(pk.STAGING_ASSETS, "Fonts", "BilliardRogue")
    main_path, halo_path = os.path.join(fonts, "BilliardPixel-Bold.ttf"), os.path.join(fonts, "BilliardPixel-BoldOutline.ttf")
    if not (os.path.exists(main_path) and os.path.exists(halo_path)):
        return img
    main, halo = ImageFont.truetype(main_path, 16), ImageFont.truetype(halo_path, 16)
    im = Image.fromarray(img[..., :3])
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    for col, row, text, rgb in HP_LABELS:
        x = -ARENA["half_w"] + col + 0.5
        z = ARENA["rows_z0"] + (ARENA["rows"] - 1 - row) + 0.5
        sx, sy = project((x, 1.0, z))
        tx, ty = int(round(sx - main.getlength(text) / 2)), int(round(sy)) - 14
        d.text((tx, ty), text, font=halo, fill=(10, 12, 24))
        d.text((tx, ty), text, font=main, fill=rgb)
    return np.dstack([np.asarray(im), np.full(img.shape[:2], 255, np.uint8)])


def gamescale(surfs):
    """One 640x360 render per act surface set (point sampling, _Tiling 0.5, world-box UVs)."""
    kind, U, V, shade = raycast_arena()
    tx = (np.floor(np.mod(U * TILING, 1.0) * S)).astype(int) % S
    ty = (S - 1 - np.floor(np.mod(V * TILING, 1.0) * S).astype(int)) % S
    lit_maps = {name: lit(s)[..., :3].astype(np.float32) for name, s in surfs.items()}
    cave = np.array(pk.palette()("indigo", 3), np.float32)
    panels, tiled = [], []
    for label, floor, side, top, ground in ACT_SETS:
        img = np.zeros(kind.shape + (3,), np.float32)
        img[:] = (18, 20, 34)
        for k, name in enumerate((floor, side, top, ground)):
            m = kind == k
            src = lit_maps[name][ty[m], tx[m]] if name else cave
            img[m] = src * shade[m][:, None] * (0.78 if k == 3 else 1.0)
        rgba = np.dstack([np.clip(img, 0, 255).astype(np.uint8), np.full(kind.shape, 255, np.uint8)])
        panels.append((label, draw_hp_labels(rgba)))
    # every surface as one continuous ground plane at game scale (~20 m across): repetition / wallpaper check
    ground_kind, gU, gV, _ = raycast_ground()
    gx = (np.floor(np.mod(gU * TILING, 1.0) * S)).astype(int) % S
    gy = (S - 1 - np.floor(np.mod(gV * TILING, 1.0) * S).astype(int)) % S
    for name, lm in lit_maps.items():
        img = np.zeros(ground_kind.shape + (3,), np.uint8)
        img[ground_kind] = np.clip(lm[gy[ground_kind], gx[ground_kind]], 0, 255).astype(np.uint8)
        tiled.append((name, np.dstack([img, np.full(ground_kind.shape, 255, np.uint8)])))
    return panels, tiled


def raycast_ground():
    c, f, u, r = camera_basis()
    w, h = CAMERA["size"]
    th = np.tan(np.deg2rad(CAMERA["fov"]) / 2)
    px, py = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    d = f + r * ((px / w * 2 - 1) * th * w / h)[..., None] + u * ((1 - py / h * 2) * th)[..., None]
    with np.errstate(divide="ignore", invalid="ignore"):
        t = -c[1] / d[..., 1]
    p = c + d * t[..., None]
    return t > 0, p[..., 0] + UV_OFFSET[0], p[..., 2] + UV_OFFSET[2], None


def rgb4(a):
    return np.dstack([a, np.full(a.shape[:2], 255, np.uint8)])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    os.makedirs(preview_dir, exist_ok=True)
    items_maps, items_lit, meta, surfs = [], [], {}, {}
    for gen in SURFACES:
        s = gen()
        surfs[s["name"]] = s
        base = pk.staging(*OUT_DIR, s["name"])
        pk.save_rgb(base + "_Albedo.png", s["albedo"])
        pk.save_rgb(base + "_Normal.png", s["normal"])
        pk.save_gray(base + "_Cavity.png", s["cavity"])
        print(" ", s["name"], "max normal tilt", s["maxTilt"])
        meta[s["name"]] = {"size": [S, S], "texelsPerMetre": 32, "tileMetres": 2, "tiling": TILING,
                           "maps": {k: f"{s['name']}_{k}.png" for k in ("Albedo", "Normal", "Cavity")}}
        items_maps += [(s["name"] + " albedo", rgb4(s["albedo"])), ("normal", rgb4(s["normal"])),
                       ("cavity", rgb4((np.repeat(s["cavity"][..., None], 3, -1) * 255).astype(np.uint8)))]
        items_lit.append((s["name"] + " lit 2x2", lit(s, tile=2)))
    pk.save_json(pk.staging(*OUT_DIR, "surfaces.json"), {
        "note": "Generated by Tools/Textures/make_surfaces.py. Normal = OpenGL (+G up), Cavity 0.5 = neutral. "
                "One 64 px tile = tileMetres x tileMetres metres (32 texels/m, ~1:1 with the 640x360 world RT). "
                "M_Surface_* must use _Tiling = tiling (= 1 / tileMetres) because *_Surface meshes carry 1 UV = 1 m; "
                "_Tiling 1 doubles the density to 64 texels/m (shimmer, lost mortar lines, CryptFloor slabs off-grid).",
        "uvMapping": {"space": "arena-local world box projection (top: x,z; x-facing: z,y; z-facing: x,y)",
                      "offset": list(UV_OFFSET), "tiling": TILING,
                      "note": "uv = (arenaLocal + offset) * tiling puts the UV origin on a cell corner (x = -3.5 + k, "
                              "z = launchZoneHeight + k); only then do CryptFloor's 1 m slabs line up with the cells."},
        "surfaces": meta})
    pk.save_rgb(os.path.join(preview_dir, "surfaces_maps.png"), pk.contact_sheet(items_maps, scale=3, cols=6, checker_bg=False))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_lit.png"), pk.contact_sheet(items_lit, scale=2, cols=4, checker_bg=False))
    panels, tiled = gamescale(surfs)
    for label, img in panels:
        pk.save_rgb(os.path.join(preview_dir, f"surfaces_gamescale_{label}.png"), pk.upscale(img, 2))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_gamescale.png"),
                pk.contact_sheet([(f"{lb} 1x (640x360, _Tiling 0.5)", im) for lb, im in panels], scale=1, cols=2, checker_bg=False))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_gamescale_tiled.png"),
                pk.contact_sheet([(f"{lb} as ground, 1x, 32 texels/m", im) for lb, im in tiled], scale=1, cols=2, checker_bg=False))
    print("SURFACES", {"count": len(SURFACES), "out": os.path.join(pk.STAGING_ASSETS, *OUT_DIR), "preview": preview_dir})


if __name__ == "__main__":
    main()
