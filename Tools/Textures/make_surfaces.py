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
M_Surface_* `_Tiling` = 1 / tileMetres = 0.5 (surfaces.json "tiling", read by MaterialsBuilder): the environment's
*_Surface meshes use 1 UV = 1 m.
UV mapping in the game today (surfaces.json "uvMapping"): every kit piece carries its OWN box UVs from its corner
(Tools/Blender/environment/env_lib.Part.box_uv, offset 0.5) and ToonLit's _WORLD_UV is off, so each 1 m floor tile,
wall segment and coping shows the same bottom-left 32 x 32 quadrant (UV 0..0.5). The floors (RuinFloor, CryptFloor,
HollowFloor = the per-act arena floors Environment binds; StoneFloor, CrystalRock) are built for that: the quadrant
is one calm 1 m slab that tiles with itself, and the whole tile still tiles for continuous pieces or a world-space
mapping (see the "Floors" note above stone_floor). FLOOR_LIMITS checks it on every build (value, noise, normal tilt,
quadrant seam, saturation) and surfaces.json records the numbers ("floorMetrics").
Previews: surfaces_maps.png, surfaces_lit.png, surfaces_gamescale.png (+ _act1, _act2, _act3 and the _stonefloor /
_crystalfloor variants at 2x): the arena at 640x360 from the default camera (ArenaConfig: (0, 20.4, -6.15), pitch
58, fov 28) with each act's surface set and sun tint, the kit mapping (game today) and a world mapping side by side, point sampling, enemy
stand-ins (Enemy_* icon renders) and outlined HP numbers from the built fonts; surfaces_gamescale_tiled.png: every
surface as one ground plane from that camera (repetition check).
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

# Floors (StoneFloor, CryptFloor, CrystalRock, RuinFloor, HollowFloor) - read the kit mapping first:
# the environment's kit pieces (Env_FloorTile, Env_WallSegment, ...) carry their OWN box UVs (1 UV = 1 m, each piece
# starts at UV 0) and M_Surface_* use _Tiling 0.5, so every 1 m piece shows the SAME bottom-left 32 x 32 quadrant of
# the tile (UV 0..0.5; Unity's UV origin is the image's bottom-left = rows 32..63, cols 0..31; a floor tile's top even
# crops the outer texel ring: it spans UV 0.0175..0.4825). The floors are therefore built so that
#   * that quadrant alone is one whole, calm 1 m slab that tiles with itself (mortar on the quadrant borders, which
#     the floor tile's bevel crops), so every cell reads as one quiet slab and repeats without seams, and
#   * the full 64 px tile still tiles (the 4 quadrants share the mortar grid; only their interiors differ), so
#     continuous pieces (launch pad, paving / ground patches) or a world-space mapping show a 1 m slab grid on the
#     cells with gentle slab-to-slab variety.
# Calm = narrow value range, broad low-contrast mottling, no distinctive mark in the bottom-left slab (it would repeat
# in every cell), near-flat normals inside the slab (only the 2 px bevel tilts, so the 4-band toon light does not
# speckle), and darker than the actors (albedo luma ~0.22-0.30 vs ~0.45-0.8 for enemies, balls and the cat).
# FLOOR_LIMITS below turns this into a build check.
QUADS = [(0, 0, 32, 32), (32, 0, 32, 32), (0, 32, 32, 32), (32, 32, 32, 32)]  # TL, TR, BL (= every kit piece), BR
BL = 2


def slab_frame():
    """-> (ids, lx, ly, dist): the 4 quadrant slabs with 1 px mortar on the left/top of each (= every quadrant border),
    local slab coords (0 = mortar) and the taxicab distance to the mortar."""
    ids = rect_ids(QUADS, 0, 0, gap=1)
    x, y = np.meshgrid(np.arange(S), np.arange(S))
    return ids, x % 32, y % 32, pk.torus_distance(ids >= 0)


def slab_rim(lx, ly):
    """+1 on the lit top/left rim, -1 on the 2 px bottom/right rim (its outer pixel is cropped on floor tiles), 0 in
    the two corners where they meet."""
    rim = np.zeros(lx.shape, np.float32)
    lit_, dark = (lx == 1) | (ly == 1), (lx >= 30) | (ly >= 30)
    rim[lit_] = 1.0
    rim[dark] = -1.0
    rim[lit_ & dark] = 0.0
    return rim


def in_quad(q, mask=None):
    x0, y0, w, h = QUADS[q]
    m = np.zeros((S, S), bool)
    m[y0:y0 + h, x0:x0 + w] = True
    return m if mask is None else m & mask


def stone_floor():
    """Act 1 / Act 3 arena floor: worn cool-grey flagstones, one per 1 m cell."""
    rng = np.random.default_rng(11)
    pal = pk.palette()
    ids, lx, ly, dist = slab_frame()
    solid = ids >= 0
    q = np.maximum(ids, 0)
    noise = pk.fbm(S, rng, ((4, 0.6), (8, 0.4)))      # broad mottling only: no per-pixel grain
    fine = pk.value_noise(S, 16, rng, order=1)
    rim = slab_rim(lx, ly)
    interior = solid & (dist >= 3)
    pits = interior & (rng.random((S, S)) < 0.012)
    # a little slab-to-slab life for the continuous pieces; the bottom-left slab (every cell) stays the plainest
    chips = (in_quad(0, pk.torus_pixels(S, [(1, 1), (2, 1), (1, 2)])) | in_quad(1, pk.torus_pixels(S, [(63, 1), (62, 1), (63, 2)]))
             | in_quad(3, pk.torus_pixels(S, [(63, 63), (62, 63), (63, 62)])))  # chipped corners, never on the kit slab
    cracks = in_quad(1, crack(rng, (41, 9), 7, (0.9, 0.7))) | in_quad(3, crack(rng, (50, 44), 5, (-0.8, 0.9)))
    cracks &= interior
    solid &= ~chips
    tone_q = np.array([0.02, 0.0, 0.0, 0.03], np.float32)[q]
    height = np.where(solid, 0.35 + 0.27 * np.clip(dist / 2.0, 0, 1) + 0.015 * noise, 0.08)
    height[pits] -= 0.06
    height[cracks] -= 0.1
    height = np.clip(height, 0, 1)
    tone = 0.52 + tone_q + 0.1 * (noise - 0.5) + 0.04 * (fine - 0.5) + np.where(rim > 0, 0.26, np.where(rim < 0, -0.22, 0.0))
    tone = np.where(solid, tone, 0.04)
    tone[pits] -= 0.12
    tone[cracks] -= 0.14
    albedo = tone_to_albedo(tone, pal.mix("gray", "sky", 0.12, range(1, 7)), 0.03)  # cool grey: neutral under the warm Act 1 sun
    return finish("StoneFloor", albedo, height, strength=4.0)


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
    """Act 2 arena floor: dark desaturated slate grey-blue slabs (HSV sat <= 0.15, out of the purple enemy band),
    one per 1 m cell, with a faint engraved inset border; the top-right slab carries a faint rune."""
    rng = np.random.default_rng(41)
    pal = pk.palette()
    ids, lx, ly, dist = slab_frame()
    solid = ids >= 0
    q = np.maximum(ids, 0)
    noise = pk.fbm(S, rng, ((4, 0.6), (8, 0.4)))
    fine = pk.value_noise(S, 16, rng, order=1)
    rim = slab_rim(lx, ly)
    interior = solid & (dist >= 3)
    groove = solid & ((((lx == 5) | (lx == 26)) & (ly >= 5) & (ly <= 26)) | (((ly == 5) | (ly == 26)) & (lx >= 5) & (lx <= 26)))
    groove_lip = solid & ((((lx == 6) & (ly >= 6) & (ly <= 25)) | ((ly == 6) & (lx >= 6) & (lx <= 25))))  # lit inner lip
    cxy = np.abs(lx - 15.5) + np.abs(ly - 15.5)
    rune = in_quad(1, solid & (np.abs(cxy - 5) < 0.6)) | in_quad(1, solid & (cxy < 1.1))  # only the top-right slab
    pits = interior & ~groove & (rng.random((S, S)) < 0.01)
    cracks = in_quad(0, crack(rng, (10, 18), 6, (0.8, -0.6))) | in_quad(3, crack(rng, (40, 52), 5, (0.9, 0.5)))
    cracks &= interior & ~groove
    chips = in_quad(3, pk.torus_pixels(S, [(63, 63), (62, 63), (63, 62)])) | in_quad(0, pk.torus_pixels(S, [(31, 1), (30, 1), (31, 2)]))
    solid &= ~chips
    tone_q = np.array([0.0, 0.02, 0.0, -0.02], np.float32)[q]
    height = np.where(solid, 0.35 + 0.27 * np.clip(dist / 2.0, 0, 1) + 0.015 * noise, 0.05)
    height[groove | rune] -= 0.07
    height[pits] -= 0.05
    height[cracks] -= 0.09
    height = np.clip(height, 0, 1)
    tone = 0.43 + tone_q + 0.09 * (noise - 0.5) + 0.04 * (fine - 0.5) + np.where(rim > 0, 0.28, np.where(rim < 0, -0.2, 0.0))
    tone = np.where(solid, tone, 0.03)
    tone[groove | rune] -= 0.16
    tone[groove_lip & ~groove] += 0.1
    tone[pits] -= 0.12
    tone[cracks] -= 0.14
    albedo = tone_to_albedo(tone, pal.mix("gray", "sky", 0.16, range(1, 7)), 0.03)
    return finish("CryptFloor", albedo, height, strength=4.0)


def ruin_floor():
    """Act 1 arena floor (requested by Environment as RuinFloor): warm grey sandstone, one panel per 1 m cell, faint
    horizontal bedding, moss only in the joints (joints are cropped on the floor tiles, so the cells stay clean;
    the launch pad and continuous pieces show the mossy grid). HSV sat <= 0.15."""
    rng = np.random.default_rng(83)
    pal = pk.palette()
    ids, lx, ly, dist = slab_frame()
    solid = ids >= 0
    q = np.maximum(ids, 0)
    noise = pk.fbm(S, rng, ((4, 0.6), (8, 0.4)))
    bedding = pk.aniso_noise(S, 2, 16, rng)            # sandstone strata: broad, horizontal, very low contrast
    rim = slab_rim(lx, ly)
    interior = solid & (dist >= 3)
    pits = interior & (rng.random((S, S)) < 0.01)
    cracks = in_quad(1, crack(rng, (44, 10), 6, (0.9, 0.6))) & interior
    chips = in_quad(0, pk.torus_pixels(S, [(31, 1), (30, 1), (31, 2)])) | in_quad(3, pk.torus_pixels(S, [(63, 63), (62, 63), (63, 62)]))
    solid &= ~chips
    # moss: patches along the joints (mortar), creeping 1 px onto the slab rim outside the kit's quadrant
    joint = ~solid & ~chips
    moss_field = pk.value_noise(S, 8, rng, order=1)
    moss = joint & (moss_field > 0.55)
    creep = pk.dilate(moss, False) & solid & (dist <= 1) & ~in_quad(BL) & (moss_field > 0.62)
    moss |= creep | (chips & (moss_field > 0.4))
    tone_q = np.array([0.01, -0.01, 0.0, 0.02], np.float32)[q]
    height = np.where(solid, 0.35 + 0.27 * np.clip(dist / 2.0, 0, 1) + 0.015 * noise, 0.08)
    height[pits] -= 0.06
    height[cracks] -= 0.1
    height = np.clip(height, 0, 1)
    tone = 0.52 + tone_q + 0.08 * (noise - 0.5) + 0.06 * (bedding - 0.5) + np.where(rim > 0, 0.26, np.where(rim < 0, -0.22, 0.0))
    tone = np.where(solid, tone, 0.04)
    tone[pits] -= 0.12
    tone[cracks] -= 0.14
    albedo = tone_to_albedo(tone, pal.mix("gray", "brown", 0.35, range(1, 7)), 0.03).astype(np.float32)
    moss_lit = pk.dilate(moss, False) & ~pk.shift(moss, 0, 1)  # top edge of each moss run catches the light
    albedo[moss] = pal.mix("green", "gray", 0.55, [4])[0]
    albedo[moss & moss_lit & (moss_field > 0.7)] = pal.mix("lime", "gray", 0.55, [6])[0]
    return finish("RuinFloor", albedo, height, strength=4.0, cav_extra=np.where(moss, 0.03, 0.0))


def hollow_floor():
    """Act 3 arena floor (requested by Environment as HollowFloor): cyan-grey slate, one panel per 1 m cell, dark
    cyan grout, faint cleavage lines; the violet stays in the scenery (CrystalRock walls, 3D crystals). Lighter than
    the other floors but still well under the actors (luma ~0.33 vs >= 0.45). HSV sat <= 0.15."""
    rng = np.random.default_rng(97)
    pal = pk.palette()
    ids, lx, ly, dist = slab_frame()
    solid = ids >= 0
    q = np.maximum(ids, 0)
    noise = pk.fbm(S, rng, ((4, 0.6), (8, 0.4)))
    layers = pk.aniso_noise(S, 3, 12, rng)
    rim = slab_rim(lx, ly)
    interior = solid & (dist >= 3)
    x, y = np.meshgrid(np.arange(S), np.arange(S))
    # slate cleavage: a few short, faint diagonal hairlines, never in the kit's quadrant (it would repeat per cell)
    cleave = np.zeros((S, S), bool)
    for qq, (sx, sy, n) in ((0, (6, 22, 7)), (1, (40, 26, 5)), (3, (38, 58, 6))):
        cleave |= in_quad(qq, crack(rng, (sx, sy), n, (1.0, -0.75)))
    cleave &= interior
    pits = interior & (rng.random((S, S)) < 0.008)
    chips = in_quad(1, pk.torus_pixels(S, [(63, 1), (62, 1), (63, 2)])) | in_quad(3, pk.torus_pixels(S, [(33, 63), (34, 63), (33, 62)]))
    solid &= ~chips
    tone_q = np.array([0.02, 0.0, 0.0, -0.02], np.float32)[q]
    height = np.where(solid, 0.35 + 0.27 * np.clip(dist / 2.0, 0, 1) + 0.015 * noise, 0.08)
    height[cleave] -= 0.05
    height[pits] -= 0.05
    height = np.clip(height, 0, 1)
    tone = 0.56 + tone_q + 0.08 * (noise - 0.5) + 0.05 * (layers - 0.5) + np.where(rim > 0, 0.24, np.where(rim < 0, -0.22, 0.0))
    tone[cleave] -= 0.12
    tone[pits] -= 0.12
    albedo = tone_to_albedo(tone, pal.mix("gray", "cyan", 0.1, range(1, 7)), 0.03).astype(np.float32)
    grout = ~solid
    albedo[grout] = pal.mix("cyan", "gray", 0.5, [3])[0]
    return finish("HollowFloor", albedo, height, strength=4.0)


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
    """Act 3 rock (walls, wall tops, ground patches, arena floor candidate): dark desaturated violet facets.
    The facet pattern repeats every 32 px (1 m) so a kit piece's bottom-left quadrant tiles with itself; the painted
    crystals are two tiny muted specks outside that quadrant (the 3D crystal clusters carry the sparkle)."""
    rng = np.random.default_rng(67)
    pal = pk.palette()
    Q = 32
    pts = []
    while len(pts) < 4:  # poisson-ish sites on the 32 px torus: 4 broad facets per metre, never clumped
        p_ = rng.uniform(0, Q, 2)
        if all(np.hypot(*((p_ - np.array(o) + Q / 2) % Q - Q / 2)) >= 13.0 for o in pts):
            pts.append((float(p_[0]), float(p_[1])))
    labels, border = pk.torus_voronoi(Q, pts)
    x, y = np.meshgrid(np.arange(Q) + 0.5, np.arange(Q) + 0.5)
    h = np.zeros((Q, Q), np.float32)
    shade = np.zeros((Q, Q), np.float32)
    grads = rng.uniform(-1, 1, (len(pts), 2))
    grads /= np.linalg.norm(grads, axis=1, keepdims=True)
    for i, (px_, py_) in enumerate(pts):
        m = labels == i
        dx = (x - px_ + Q / 2) % Q - Q / 2
        dy = (y - py_ + Q / 2) % Q - Q / 2
        plane = 0.62 + rng.uniform(0.008, 0.016) * (grads[i, 0] * dx + grads[i, 1] * dy)
        h[m] = plane[m]
        shade[m] = -0.5 * grads[i, 0] - 0.5 * grads[i, 1]  # facets tilted toward the top-left read a touch lighter
    crevice = border < 0.8
    lip = (border >= 0.8) & (border < 1.8)
    h = np.where(crevice, 0.45, np.where(lip, h - 0.04, h))
    noise = pk.fbm(Q, rng, ((4, 0.6), (8, 0.4)))
    h += 0.015 * (noise - 0.5)
    tone = 0.46 + 0.07 * shade + 0.08 * (noise - 0.5)
    tone = np.where(lip, tone - 0.05, tone)
    tone = np.where(crevice, 0.26, tone)  # low-contrast seams, not black cracks
    tone, h = np.tile(tone, (2, 2)), np.tile(h, (2, 2))
    # two tiny muted crystal specks, top-right and bottom-right quadrants only (never in the kit's quadrant)
    X, Y = np.meshgrid(np.arange(S), np.arange(S))
    crystal = np.zeros((S, S), bool)
    c_tone = np.zeros((S, S), np.float32)
    for bx, by, hw, length in ((47.5, 20.0, 1.6, 6.0), (54.5, 55.0, 1.3, 4.5)):
        pts_poly = [(bx - hw, by), (bx - hw, by - length * 0.7), (bx, by - length), (bx + hw, by - length * 0.7), (bx + hw, by)]
        m = pk.torus_poly(S, pts_poly)
        c_tone = np.where(m, np.where(X < bx, 0.8, 0.45), c_tone)
        crystal |= m
    rim = pk.dilate(crystal, False) & ~crystal
    height = np.where(crystal, 0.85, np.where(rim, 0.3, h))
    height = np.clip(height, 0, 1)
    rock_ramp = (pal.ramp("indigo", range(1, 8)) * 0.5 + pal.ramp("purple", range(1, 8)) * 0.12
                 + pal.ramp("gray", range(1, 8)) * 0.38).round()
    albedo = tone_to_albedo(tone, rock_ramp, 0.03)
    speck_ramp = pal.mix("cyan", "gray", 0.65, [6, 8]).astype(np.float32)  # muted glints, not pickups
    albedo = np.where(crystal[..., None], tone_to_albedo(c_tone, speck_ramp, 0.0), albedo)
    albedo[rim] = rock_ramp[0]
    return finish("CrystalRock", albedo, height, strength=4.0, cav_gain=1.2)


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


SURFACES = [stone_floor, mossy_brick, crypt_brick, crypt_floor, wood_plank, crystal_rock, dirt, grass,
            ruin_floor, hollow_floor]

# Arena floors must stay calm and darker than the actors (enemy icon renders average luma ~0.45-0.67, balls and the
# cat are lighter still). Checked on every build; the numbers go to surfaces.json "floorMetrics".
FLOOR_LIMITS = {"albedoLuma": (0.20, 0.34),   # mean Rec.601 luma of the albedo
                "kitSlabStd": (0.0, 0.07),    # luma std of the bottom-left quadrant = what every 1 m cell shows
                "kitSlabHf": (0.0, 0.12),     # mean |4-neighbour laplacian| of that quadrant (pixel-level noise)
                "tiltedPct": (0.0, 25.0),     # % of normals tilted > 0.3 (the toon bands speckle on those)
                "quadrantSeam": (0.0, 0.02),  # height step the quadrant's self-tiling adds vs the full tile
                "hsvSat": (0.0, 0.15)}        # mean HSV saturation: out of the enemy / gameplay colour bands
FLOORS = ("StoneFloor", "CryptFloor", "CrystalRock", "RuinFloor", "HollowFloor")
SAT_LIMIT = {"CrystalRock": 0.38}  # violet scenery rock (walls, patches) may keep its hue; the arena floors may not


def floor_metrics(s):
    lum = (s["albedo"].astype(np.float32) / 255) @ np.array([0.299, 0.587, 0.114], np.float32)
    q = lum[32:64, 0:32]
    lap = np.abs(4 * q - np.roll(q, 1, 0) - np.roll(q, -1, 0) - np.roll(q, 1, 1) - np.roll(q, -1, 1))
    n = s["normal_f"]
    h = s["height"]
    # self-tiling the quadrant puts col 0 right of col 31 (the tile has col 32 there) and row 63 above row 32 (row 31)
    seam = float(np.abs(h[32:64, 0] - h[32:64, 32]).mean() + np.abs(h[63, 0:32] - h[31, 0:32]).mean())
    rgb = s["albedo"].astype(np.float32)
    hsv_sat = (rgb.max(-1) - rgb.min(-1)) / np.maximum(rgb.max(-1), 1)
    return {"albedoLuma": round(float(lum.mean()), 3), "kitSlabStd": round(float(q.std()), 3),
            "kitSlabHf": round(float(lap.mean()), 3),
            "tiltedPct": round(float((np.hypot(n[..., 0], n[..., 1]) > 0.3).mean() * 100), 1),
            "quadrantSeam": round(seam, 4), "hsvSat": round(float(hsv_sat.mean()), 3)}


def check_floor(s):
    m = floor_metrics(s)
    limits = dict(FLOOR_LIMITS, hsvSat=(0.0, SAT_LIMIT.get(s["name"], FLOOR_LIMITS["hsvSat"][1])))
    bad = [f"{k} {v} not in {limits[k]}" for k, v in m.items() if not limits[k][0] <= v <= limits[k][1]]
    if bad:
        sys.exit(f"{s['name']}: floor is not calm enough: " + "; ".join(bad))
    return m


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
# (label, floor, wall side, wall top, ground, light tint, enemies) - Tools/Blender/environment/make_layouts.py act
# surface sets (ground None = the palette cave floor tile of Act 3, drawn flat); the tint is the act's sun colour
# (layouts.json lighting.sun) normalised to luma 1, applied to the surfaces AND the enemy stand-ins alike.
ACT1 = (["Slime", "Bat", "Slime", "Bomber", "KingSlime"], (1.0, 0.78, 0.5))
ACT2 = (["Skeleton", "Mage", "ShieldKnight", "Skeleton", "BoneWall"], (0.55, 0.66, 1.0))
ACT3 = (["CrystalGolem", "Totem", "Healer", "Mage", "CrystalGolem"], (0.72, 0.55, 1.0))
# arena floors per act as Environment requests them (make_layouts.ARENA_FLOOR_REQUESTS: RuinFloor / CryptFloor /
# HollowFloor) plus the floors bound today (StoneFloor in Acts 1 and 3) and CrystalRock as a floor candidate
ACT_SETS = [("act1", "RuinFloor", "MossyBrick", "StoneFloor", "Grass", ACT1[1], ACT1[0]),
            ("act1_stonefloor", "StoneFloor", "MossyBrick", "StoneFloor", "Grass", ACT1[1], ACT1[0]),
            ("act2", "CryptFloor", "CryptBrick", "CryptFloor", "Dirt", ACT2[1], ACT2[0]),
            ("act3", "HollowFloor", "CrystalRock", "CrystalRock", None, ACT3[1], ACT3[0]),
            ("act3_stonefloor", "StoneFloor", "CrystalRock", "CrystalRock", None, ACT3[1], ACT3[0]),
            ("act3_crystalfloor", "CrystalRock", "CrystalRock", "CrystalRock", None, ACT3[1], ACT3[0])]
# (col, row from the top, text, rgb) HP numbers over the enemy cells (the first 5 carry an enemy) + damage numbers
HP_LABELS = [(1, 2, "12", (255, 255, 255)), (3, 3, "8", (255, 255, 255)), (5, 1, "24", (255, 255, 255)),
             (6, 4, "5", (255, 255, 255)), (0, 5, "30", (255, 255, 255)), (2, 6, "-7", (255, 214, 90)),
             (4, 7, "CRIT!", (255, 122, 58))]
BEVEL = 0.035   # Env_FloorTile top inset: the Base bevel (palette gray 4) frames every cell
DANGER = (0.105, 0.055, 0.09)  # Env_FloorTile_Danger (bottom row), distance from the cell edge: groove to 0.105 m
                               # (dark red palette), idle inlay line 0.055-0.09 m (glows only when occupied)


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
    """-> (kind (0 floor, 1 wall side, 2 wall top, 3 ground, -1 sky), U, V, face shade, world z) per 640x360 pixel.
    U, V are arena-local world coordinates + UV_OFFSET (1 unit = 1 m, integers on the cell corners)."""
    c, f, u, r = camera_basis()
    w, h = CAMERA["size"]
    th = np.tan(np.deg2rad(CAMERA["fov"]) / 2)
    px, py = np.meshgrid(np.arange(w) + 0.5, np.arange(h) + 0.5)
    d = f + r * ((px / w * 2 - 1) * th * w / h)[..., None] + u * ((1 - py / h * 2) * th)[..., None]
    hw, z0, z1 = ARENA["half_w"], 0.0, ARENA["z1"]  # floor spans the launch zone (z 0) to the far wall
    wt, wh, gy = ARENA["wall_t"], ARENA["wall_h"], ARENA["ground_y"]
    best = np.full((h, w), np.inf)
    kind = np.full((h, w), -1, np.int8)
    U, V, shade, Z = np.zeros((h, w)), np.zeros((h, w)), np.ones((h, w)), np.zeros((h, w))
    ox, oy, oz = UV_OFFSET

    def hit(t, ok, k, uu, vv, sh, pz):
        ok = ok & (t > 0) & (t < best)
        best[ok], kind[ok], U[ok], V[ok], shade[ok], Z[ok] = t[ok], k, uu[ok], vv[ok], sh, pz[ok]

    def plane(axis, value):
        with np.errstate(divide="ignore", invalid="ignore"):
            t = (value - c[axis]) / d[..., axis]
        return t, c + d * t[..., None]

    t, p = plane(1, 0.0)  # arena floor
    hit(t, (np.abs(p[..., 0]) <= hw) & (p[..., 2] >= z0) & (p[..., 2] <= z1), 0, p[..., 0] + ox, p[..., 2] + oz, 1.0, p[..., 2])
    t, p = plane(1, gy)   # ground outside the rim
    outside = (np.abs(p[..., 0]) > hw + wt) | (p[..., 2] > z1 + wt) | (p[..., 2] < z0 - wt)
    hit(t, outside, 3, p[..., 0] + ox, p[..., 2] + oz, 1.0, p[..., 2])
    t, p = plane(1, wh)   # coping of the three rim walls
    ax, pz = np.abs(p[..., 0]), p[..., 2]
    top = ((ax >= hw) & (ax <= hw + wt) & (pz >= z0 - wt) & (pz <= z1 + wt)) | ((pz >= z1) & (pz <= z1 + wt) & (ax <= hw + wt))
    hit(t, top, 2, np.where(ax >= hw, pz + oz, p[..., 0] + ox), np.where(ax >= hw, ax - hw, pz - z1), 1.0, pz)
    for sign, sh in ((-1, 0.92), (1, 0.72)):  # inner faces: left wall faces +x (sunlit), right wall faces -x
        t, p = plane(0, sign * hw)
        ok = (p[..., 1] >= 0) & (p[..., 1] <= wh) & (p[..., 2] >= z0) & (p[..., 2] <= z1)
        hit(t, ok, 1, -sign * (p[..., 2] + oz), p[..., 1] + oy, sh, p[..., 2])
    t, p = plane(2, z1)   # far wall, facing the camera
    hit(t, (np.abs(p[..., 0]) <= hw) & (p[..., 1] >= 0) & (p[..., 1] <= wh), 1, p[..., 0] + ox, p[..., 1] + oy, 0.85, p[..., 2])
    t, p = plane(2, z0 - wt)  # south end faces of the side walls
    ok = (np.abs(p[..., 0]) >= hw) & (np.abs(p[..., 0]) <= hw + wt) & (p[..., 1] >= gy) & (p[..., 1] <= wh)
    hit(t, ok, 1, p[..., 0] + ox, p[..., 1] + oy, 0.8, p[..., 2])
    t, p = plane(2, z0)       # front lip of the arena floor (0.2 m step down to the ground)
    ok = (np.abs(p[..., 0]) <= hw) & (p[..., 1] >= gy) & (p[..., 1] <= 0)
    hit(t, ok, 0, p[..., 0] + ox, p[..., 1] + oy, 0.8, np.full(t.shape, -1.0))
    return kind, U, V, shade, Z


def kit_uv(kind, U, V, Z):
    """The mapping the game uses today: every kit piece has its own box UVs (1 UV = 1 m from the piece's corner), so
    the 1 m floor tiles, wall segments and copings all restart at UV 0 (u, v mod 1); the launch pad is one
    7 x 1.6 m piece whose UV integers also sit on the cell corners (uv_offset z 0.2) and the ground is
    world-mapped. -> (u, v, cell mask)."""
    cells = (kind == 0) & (Z >= ARENA["rows_z0"])
    pad = (kind == 0) & (Z < ARENA["rows_z0"])
    kitp = cells | (kind == 1) | (kind == 2)
    u = np.where(kitp, np.mod(U, 1.0), U)
    v = np.where(cells | (kind == 2), np.mod(V, 1.0), np.where(pad, V - 1.0, V))
    return u, v, cells


def paste_sprite(img, sprite, cx, bottom, width):
    """Nearest-neighbour scaled RGBA sprite, bottom-centred at (cx, bottom) (enemy stand-ins)."""
    from PIL import Image
    im = Image.fromarray(sprite)
    k = width / im.width
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.NEAREST)
    a = np.asarray(im)
    x0, y0 = int(round(cx - a.shape[1] / 2)), int(round(bottom - a.shape[0]))
    pk.blit(img, a, x0, y0)


def draw_hp_labels(img, enemies=None, tint=(1.0, 1.0, 1.0)):
    """Enemy stand-ins (the models pipeline's Enemy_* icon renders at ~1 cell, lit by the act tint) + outlined HP
    numbers (BilliardPixel-Bold over BilliardPixel-BoldOutline at 16 px = 1x), each when available."""
    from PIL import Image, ImageDraw, ImageFont
    img = img.copy()
    icons = os.path.join(pk.STAGING_ASSETS, "Sprites", "BilliardRogue", "Icons")
    for (col, row, _, _), name in zip(HP_LABELS, enemies or []):
        path = os.path.join(icons, f"Enemy_{name}.png")
        if not os.path.exists(path):
            continue
        spr = np.array(Image.open(path).convert("RGBA"))
        spr[..., :3] = np.clip(spr[..., :3] * np.array(tint), 0, 255).astype(np.uint8)
        x = -ARENA["half_w"] + col + 0.5
        z = ARENA["rows_z0"] + (ARENA["rows"] - 1 - row) + 0.5
        sx, sy = project((x, 0.0, z))
        paste_sprite(img, spr, sx, sy + 6, 26)
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
        tx, ty = int(round(sx - main.getlength(text) / 2)), int(round(sy)) - 16
        d.text((tx, ty), text, font=halo, fill=(10, 12, 24))
        d.text((tx, ty), text, font=main, fill=rgb)
    return np.dstack([np.asarray(im), np.full(img.shape[:2], 255, np.uint8)])


def luma(rgb):
    return (rgb[..., :3].astype(np.float32) / 255) @ np.array([0.299, 0.587, 0.114], np.float32)


def norm_tint(rgb):
    t = 0.5 + 0.5 * np.array(rgb, np.float32)  # sun + a neutral share for the ambient / fill light
    return t / float(t @ np.array([0.299, 0.587, 0.114], np.float32))


def render_arena(surfs, lit_maps, floor, side, top, ground, tint, mapping):
    kind, U, V, shade, Z = raycast_arena()
    u, v, cells = kit_uv(kind, U, V, Z) if mapping == "kit" else (U, V, np.zeros(kind.shape, bool))
    tx = (np.floor(np.mod(u * TILING, 1.0) * S)).astype(int) % S
    ty = (S - 1 - np.floor(np.mod(v * TILING, 1.0) * S).astype(int)) % S
    pal = pk.palette()
    img = np.zeros(kind.shape + (3,), np.float32)
    img[:] = (18, 20, 34)
    for k, name in enumerate((floor, side, top, ground)):
        m = kind == k
        src = lit_maps[name][ty[m], tx[m]] if name else np.array(pal("indigo", 3), np.float32)
        img[m] = src * shade[m][:, None] * (0.78 if k == 3 else 1.0)
    if mapping == "kit":  # Env_FloorTile bevel ring + the danger row's red inlay (bottom row of cells)
        fu, fv = np.mod(U, 1.0), np.mod(V, 1.0)
        edge = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))
        lit_edge = (np.minimum(fu, 1 - fv) <= np.minimum(1 - fu, fv))  # left / far chamfers face the light
        img[cells & (edge < BEVEL) & lit_edge] = pal("gray", 4)
        img[cells & (edge < BEVEL) & ~lit_edge] = pal("gray", 2)
        danger = cells & (Z < ARENA["rows_z0"] + 1) & (edge >= BEVEL)
        img[danger & (edge < DANGER[0])] = pal("red", 3)
        img[danger & (edge >= DANGER[1]) & (edge < DANGER[2])] = pal("red", 4)
    img = img * norm_tint(tint)
    return np.dstack([np.clip(img, 0, 255).astype(np.uint8), np.full(kind.shape, 255, np.uint8)]), kind == 0


def gamescale(surfs):
    """640x360 renders per act surface set (point sampling, _Tiling 0.5) with the kit mapping the game uses today
    and with a world-space mapping, enemy stand-ins + HP numbers; -> panels, tiled planes, contrast metrics."""
    lit_maps = {name: lit(s)[..., :3].astype(np.float32) for name, s in surfs.items()}
    panels, metrics = [], {}
    for label, floor, side, top, ground, tint, enemies in ACT_SETS:
        for mapping in ("kit", "world"):
            base, floor_mask = render_arena(surfs, lit_maps, floor, side, top, ground, tint, mapping)
            shown = draw_hp_labels(base, enemies, norm_tint(tint))
            panels.append((f"{label}_{mapping}", shown))
            if mapping == "kit":
                actor = np.any(shown != base, axis=-1) & floor_mask
                fl = luma(base)[floor_mask & ~actor]
                sprites = draw_hp_labels(np.zeros_like(base), enemies, norm_tint(tint))  # enemies only, on black
                sm = np.any(sprites[..., :3] > 0, axis=-1) & ~np.any(draw_hp_labels(np.zeros_like(base), [], (1, 1, 1))[..., :3] > 0, axis=-1)
                metrics[label] = {"floorLuma": round(float(fl.mean()), 3), "floorLumaP90": round(float(np.percentile(fl, 90)), 3),
                                  "enemyLuma": round(float(luma(sprites)[sm].mean()), 3),
                                  "floorHf": round(float(np.abs(np.diff(luma(base), axis=1))[floor_mask[:, 1:] & floor_mask[:, :-1]].mean()), 3)}
    # every surface as one continuous ground plane at game scale (~20 m across): repetition / wallpaper check
    ground_kind, gU, gV, _ = raycast_ground()
    gx = (np.floor(np.mod(gU * TILING, 1.0) * S)).astype(int) % S
    gy = (S - 1 - np.floor(np.mod(gV * TILING, 1.0) * S).astype(int)) % S
    tiled = []
    for name, lm in lit_maps.items():
        img = np.zeros(ground_kind.shape + (3,), np.uint8)
        img[ground_kind] = np.clip(lm[gy[ground_kind], gx[ground_kind]], 0, 255).astype(np.uint8)
        tiled.append((name, np.dstack([img, np.full(ground_kind.shape, 255, np.uint8)])))
    return panels, tiled, metrics


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
        # "tiling" must stay before any nested object: MaterialsBuilder reads it with a flat regex
        meta[s["name"]] = {"size": [S, S], "texelsPerMetre": 32, "tileMetres": 2, "tiling": TILING,
                           "maps": {k: f"{s['name']}_{k}.png" for k in ("Albedo", "Normal", "Cavity")}}
        if s["name"] in FLOORS:
            meta[s["name"]]["floorMetrics"] = check_floor(s)
            print("   floor", meta[s["name"]]["floorMetrics"])
        items_maps += [(s["name"] + " albedo", rgb4(s["albedo"])), ("normal", rgb4(s["normal"])),
                       ("cavity", rgb4((np.repeat(s["cavity"][..., None], 3, -1) * 255).astype(np.uint8)))]
        items_lit.append((s["name"] + " lit 2x2", lit(s, tile=2)))
    pk.save_json(pk.staging(*OUT_DIR, "surfaces.json"), {
        "note": "Generated by Tools/Textures/make_surfaces.py. Normal = OpenGL (+G up), Cavity 0.5 = neutral. "
                "One 64 px tile = tileMetres x tileMetres metres (32 texels/m, ~1:1 with the 640x360 world RT). "
                "M_Surface_* must use _Tiling = tiling (= 1 / tileMetres) because *_Surface meshes carry 1 UV = 1 m; "
                "_Tiling 1 doubles the density to 64 texels/m (shimmer, lost mortar lines, CryptFloor slabs off-grid).",
        "uvMapping": {"kit": "each kit piece's own box UVs, 1 UV = 1 m from the piece corner (_WORLD_UV off): every 1 m "
                              "floor tile / wall segment shows the bottom-left 32x32 quadrant, which the floors keep as "
                              "one self-tiling slab",
                      "world": "optional ToonLit _WORLD_UV: uv = (arenaLocal + offset) * tiling puts the UV origin on a "
                               "cell corner (x = -3.5 + k, z = launchZoneHeight + k), so the 1 m slab grid lines up "
                               "with the cells; without the offset the slab joints cross the cell centres",
                      "offset": list(UV_OFFSET), "tiling": TILING},
        "surfaces": meta})
    pk.save_rgb(os.path.join(preview_dir, "surfaces_maps.png"), pk.contact_sheet(items_maps, scale=3, cols=6, checker_bg=False))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_lit.png"), pk.contact_sheet(items_lit, scale=2, cols=4, checker_bg=False))
    panels, tiled, metrics = gamescale(surfs)
    for label, img in panels:
        if label.endswith("_kit"):
            pk.save_rgb(os.path.join(preview_dir, f"surfaces_gamescale_{label[:-4]}.png"), pk.upscale(img, 2))
    pk.save_rgb(os.path.join(preview_dir, "surfaces_gamescale.png"), pk.contact_sheet(
        [(f"{lb[:lb.rindex('_')]} 1x, {'kit UVs (game today)' if lb.endswith('_kit') else 'world UVs'}", im) for lb, im in panels],
        scale=1, cols=2, checker_bg=False))
    print("  game-scale floor luma (kit mapping, act tint):", metrics)
    pk.save_rgb(os.path.join(preview_dir, "surfaces_gamescale_tiled.png"),
                pk.contact_sheet([(f"{lb} as ground, 1x, 32 texels/m", im) for lb, im in tiled], scale=1, cols=2, checker_bg=False))
    print("SURFACES", {"count": len(SURFACES), "out": os.path.join(pk.STAGING_ASSETS, *OUT_DIR), "preview": preview_dir})


if __name__ == "__main__":
    main()
