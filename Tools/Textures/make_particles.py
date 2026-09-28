"""Pixel-art particle flipbooks (one horizontal strip per effect, 8 frames), white/grey + hard alpha.

Run:  Tools/.venv/bin/python Tools/Textures/make_particles.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/Particles/<Name>.png + particles.json (frames, frameSize,
      loop, suggested fps/size); preview contact sheet (plain + tinted) in DIR.

Colour: values are white (255) / light grey (214) / mid grey (170) / shadow grey (128) so the particle colour
(vertex colour, HDR > 1 feeds bloom) tints them; alpha is strictly 0 or 255 (alpha-clip friendly).
Gutter: every frame keeps a 1 px empty border (checked, the build fails otherwise), so point sampling at
non-integer billboard sizes never picks up a column of the neighbouring frame and rays are never cut square.
Unity: Texture Sheet Animation, Mode Grid, Tiles (8, 1), Animation Whole Sheet, 12 fps (TDD D13).
Texel density: ~28 texels per metre at the default camera -> startSize = frameSize / 28 m (16 px = 0.57 m).
Deterministic: fixed seeds per sheet/frame.
"""
import argparse
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixelkit as pk  # noqa: E402

FRAMES = 8
OUT_DIR = ("Sprites", "BilliardRogue", "Particles")
W, L, M, D = 255, 214, 170, 128  # white, light, mid, dark grey


class Frame:
    def __init__(self, size):
        self.n = size
        self.v = np.zeros((size, size), np.float32)
        self.a = np.zeros((size, size), bool)

    def paint(self, mask, value):
        self.v[mask] = value
        self.a |= mask
        return self

    def under(self, mask, value):
        """Paint only where still empty."""
        m = mask & ~self.a
        return self.paint(m, value)

    def erase(self, mask):
        self.a &= ~mask
        self.v[mask] = 0
        return self

    def rgba(self):
        img = np.zeros((self.n, self.n, 4), np.uint8)
        g = np.clip(self.v, 0, 255).astype(np.uint8)
        img[..., 0] = img[..., 1] = img[..., 2] = np.where(self.a, g, 0)
        img[..., 3] = np.where(self.a, 255, 0)
        return img


def xy(n, cx, cy):
    x, y = pk.grid(n, n)
    return x - cx, y - cy


def seg_mask(n, x0, y0, x1, y1, width=1):
    return pk.line(n, n, [(x0, y0), (x1, y1)], width)


def dissolve(mask, amount, seed):
    """Pixel-art dissolve: removes a growing, clustered-noise fraction of the mask."""
    rng = np.random.default_rng(seed)
    n = mask.shape[0]
    noise = pk.bayer(n, n) + 0.5
    noise = 0.55 * noise + 0.45 * rng.random((n, n))
    return mask & (noise >= amount)


def rot(px_, py_, ang):
    c, s = math.cos(ang), math.sin(ang)
    return px_ * c - py_ * s, px_ * s + py_ * c


def shaded_poly(fr, pts_local, cx, cy, ang, light=(-0.7, -0.7), outline_value=None):
    """Polygon with per-edge-facet shading: each pixel takes the tone of the facet (triangle fan) it is in."""
    n = fr.n
    pts = [rot(px_, py_, ang) for px_, py_ in pts_local]
    world = [(cx + px_, cy + py_) for px_, py_ in pts]
    full = pk.poly(n, n, world)
    # facets: triangles (centre, p_i, p_i+1) shaded by the outward normal of edge i
    for i in range(len(pts)):
        ax, ay = pts[i]
        bx, by = pts[(i + 1) % len(pts)]
        ex, ey = bx - ax, by - ay
        nx, ny = ey, -ex
        ln = math.hypot(nx, ny) or 1.0
        nx, ny = nx / ln, ny / ln
        if nx * (ax + bx) + ny * (ay + by) < 0:
            nx, ny = -nx, -ny
        lit = nx * light[0] + ny * light[1]
        tone = W if lit > 0.45 else L if lit > -0.1 else M if lit > -0.6 else D
        tri = pk.poly(n, n, [(cx, cy), (cx + ax, cy + ay), (cx + bx, cy + by)]) & full
        fr.paint(tri & ~fr.a, tone)
    fr.paint(full & ~fr.a, L)
    if outline_value is not None:
        ring = pk.dilate(full, False) & ~full
        fr.paint(ring, outline_value)
    return full


# ----------------------------------------------------------------------------------------------
# effects: each returns a list of 8 Frames
# ----------------------------------------------------------------------------------------------

def spark(n=16):
    frames = []
    arm = [3, 6, 5, 4, 3, 2, 2, 1]  # gap + arm <= 6: rays stop 1 px inside the 16 px frame (centre pixel 7)
    gap = [0, 0, 1, 2, 3, 4, 4, 5]
    diag = [0, 3, 4, 3, 2, 1, 0, 0]
    core = [2, 1, 1, 1, 0, 0, 0, 0]
    c = 7
    for f in range(FRAMES):
        fr = Frame(n)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            r0, r1 = gap[f] + 1, gap[f] + arm[f]
            for r in range(r0, r1 + 1):
                val = W if r < r0 + max(1, (r1 - r0) // 2) or f < 2 else L
                fr.paint(pk.pixels(n, n, [(c + dx * r, c + dy * r)]), val)
            if f in (1, 2) and arm[f] >= 5:  # thicker base of the rays
                fr.paint(pk.pixels(n, n, [(c + dx * 1 + dy, c + dy * 1 + dx), (c + dx * 1 - dy, c + dy * 1 - dx)]), L)
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            for r in range(1, diag[f] + 1):
                rr = r + max(0, gap[f] - 1)
                fr.paint(pk.pixels(n, n, [(c + dx * rr, c + dy * rr)]), M if r > 1 else L)
        if core[f] > 0:
            fr.paint(pk.rect(n, n, c - core[f] + 1, c - core[f] + 1, c + core[f], c + core[f]), W)
            if core[f] == 2:
                fr.paint(pk.pixels(n, n, [(c, c)]), W)
        elif f < 5:
            fr.paint(pk.pixels(n, n, [(c, c)]), W)
        frames.append(fr)
    return frames


def puff(n, t, seed, rise=0.0, grow=(2.0, 6.5), dissolve_from=0.45, lobes=4):
    rng = np.random.default_rng(seed)
    fr = Frame(n)
    r = grow[0] + (grow[1] - grow[0]) * math.sqrt(t)
    cx, cy = n / 2, n / 2 + 1 - rise * t
    offs = [(0, 0)] + [(math.cos(a) * r * 0.55, math.sin(a) * r * 0.45) for a in
                       np.linspace(0, 2 * math.pi, lobes, endpoint=False) + rng.uniform(0, 1.5)]
    radii = [r * 0.8] + [r * rng.uniform(0.5, 0.65) for _ in range(lobes)]
    x, y = pk.grid(n, n)
    body = np.zeros((n, n), bool)
    light = np.zeros((n, n), np.float32)
    for (ox, oy), rr in zip(offs, radii):
        dx, dy = x - (cx + ox), y - (cy + oy)
        m = dx * dx + dy * dy <= rr * rr
        body |= m
        # per-lobe shading: lit from top-left
        lam = (-dx - dy) / (rr * 1.4 + 1e-6)
        light = np.where(m, np.maximum(light, lam + 0.25), light)
    if t > dissolve_from:
        body = dissolve(body, (t - dissolve_from) / (1 - dissolve_from) * 1.05, seed + 99)
    tone = np.where(light > 0.55, W, np.where(light > 0.0, L, np.where(light > -0.45, M, D)))
    fr.paint(body, 0)
    fr.v[body] = tone[body]
    return fr


def dust(n=16):
    return [puff(n, f / (FRAMES - 1), 100 + f * 0 + 7, rise=1.5, grow=(2.4, 6.0), dissolve_from=0.4, lobes=4) for f in range(FRAMES)]


def smoke(n=16):
    return [puff(n, f / (FRAMES - 1), 200 + 3, rise=3.0, grow=(2.2, 7.0), dissolve_from=0.5, lobes=5) for f in range(FRAMES)]


def leaf(n=16):
    frames = []
    outline_pts = [(-4.9, 0), (-3.1, -2.2), (0, -2.8), (2.8, -2.0), (4.9, 0), (2.8, 2.0), (0, 2.8), (-3.1, 2.2)]
    for f in range(FRAMES):
        fr = Frame(n)
        ang = f / FRAMES * 2 * math.pi
        flutter = 0.35 + 0.65 * abs(math.cos(ang * 1.5 + 0.4))
        pts = [(px_, py_ * flutter) for px_, py_ in outline_pts]
        world = [(7.5 + rot(px_, py_, ang)[0], 7.5 + rot(px_, py_, ang)[1]) for px_, py_ in pts]
        body = pk.poly(n, n, world)
        # upper (lit) half vs lower half relative to the midrib
        x, y = pk.grid(n, n)
        ca, sa = math.cos(ang), math.sin(ang)
        side = -(x - 7.5) * sa + (y - 7.5) * ca
        facing = math.cos(ang * 1.5 + 0.4) >= 0  # which face shows (front brighter)
        top_val, bot_val = (W, L) if facing else (L, M)
        fr.paint(body & (side < 0), top_val)
        fr.paint(body & (side >= 0), bot_val)
        rib = pk.line(n, n, [(7.5 + rot(-4.0, 0, ang)[0], 7.5 + rot(-4.0, 0, ang)[1]),
                             (7.5 + rot(4.0, 0, ang)[0], 7.5 + rot(4.0, 0, ang)[1])], 1) & body
        if flutter > 0.5:
            fr.paint(rib, M if facing else D)
        stem = pk.line(n, n, [(7.5 + rot(4.9, 0, ang)[0], 7.5 + rot(4.9, 0, ang)[1]),
                              (7.5 + rot(6.2, 0, ang)[0], 7.5 + rot(6.2, 0, ang)[1])], 1)
        fr.paint(stem & ~body, M)
        frames.append(fr)
    return frames


EMBER_SHAPES = [
    [".#.", "###", ".#."],
    ["##", "##"],
    ["..#..", ".###.", "#####", ".###.", "..#.."],
    [".#.", "###", "###", ".#."],
    ["..#..", ".###.", "#####", ".###.", "..#.."],
    [".##.", "####", ".##."],
    [".#.", "###", ".#."],
    ["#"],
]


def ember(n=16):
    frames = []
    wob = [0, 1, 1, 0, -1, -1, 0, 0]
    for f, art in enumerate(EMBER_SHAPES):
        fr = Frame(n)
        m = pk.ascii_mask(art)
        h, w = m.shape
        x0, y0 = 7 + wob[f] - w // 2, 6 - h // 2
        big = np.zeros((n, n), bool)
        big[y0:y0 + h, x0:x0 + w] = m
        core = pk.erode(big, False) if h >= 3 else big
        fr.paint(big, L)
        fr.paint(core if core.any() else big, W)
        # hot trail left behind by a rising ember
        tx = 7 + wob[f]
        trail = [(tx - wob[f], y0 + h), (tx, y0 + h + 1), (tx + wob[(f + 3) % 8], y0 + h + 3)]
        for k, (px_, py_) in enumerate(trail[: 1 + (f % 3)]):
            fr.under(pk.pixels(n, n, [(px_, py_)]), M if k == 0 else D)
        frames.append(fr)
    return frames


SNOW_A = ["...#...",
          ".#.#.#.",
          "..###..",
          "###.###",
          "..###..",
          ".#.#.#.",
          "...#..."]
SNOW_B = ["#.....#",
          ".#.#.#.",
          "..###..",
          ".##.##.",
          "..###..",
          ".#.#.#.",
          "#.....#"]


def snow(n=16):
    frames = []
    seq = [(SNOW_A, 0), (SNOW_A, 1), (SNOW_B, 0), (SNOW_B, 1), (SNOW_A, 0), (SNOW_A, 2), (SNOW_B, 0), (SNOW_B, 2)]
    for f, (art, glint) in enumerate(seq):
        fr = Frame(n)
        m = pk.ascii_mask(art)
        big = np.zeros((n, n), bool)
        big[4:11, 4:11] = m
        fr.paint(big, L)
        fr.paint(pk.pixels(n, n, [(7, 7)]), W)
        tips = [(7, 4), (4, 7), (10, 7), (7, 10)] if art is SNOW_A else [(4, 4), (10, 4), (4, 10), (10, 10)]
        if glint:
            fr.paint(pk.pixels(n, n, tips), W)
        if glint == 2:
            fr.paint(pk.pixels(n, n, [(7, 2), (7, 12), (2, 7), (12, 7)]), M)
        frames.append(fr)
    return frames


def mote(n=16):
    frames = []
    for f in range(FRAMES):
        fr = Frame(n)
        k = 0.5 + 0.5 * math.cos(f / FRAMES * 2 * math.pi)  # 1 -> 0 -> 1 (loop)
        r = 2.2 + 1.3 * k
        x, y = xy(n, 7.5, 7.5)
        d2 = x * x + y * y
        fr.paint(d2 <= (r + 1.2) ** 2, D)
        fr.paint(d2 <= r * r, M)
        fr.paint(d2 <= (r * 0.62) ** 2, L)
        fr.paint(d2 <= max(r * 0.35, 0.8) ** 2, W)
        arm = int(round(1 + 3 * k))
        cross = ((np.abs(x) < 0.6) & (np.abs(y) < r + arm)) | ((np.abs(y) < 0.6) & (np.abs(x) < r + arm))
        cross = ((np.abs(x + 0.0) <= 0.5) | (np.abs(y) <= 0.5)) & (np.maximum(np.abs(x), np.abs(y)) < min(r + arm, 7.0))
        fr.under(cross, L)
        frames.append(fr)
    return frames


def star(n=16):
    frames = []
    size = [1, 3, 4.5, 5.4, 5.0, 3.6, 2, 1]  # astroid radius (sqrt(s) + 0.3)^2 < 7: tips stay 1 px inside
    for f in range(FRAMES):
        fr = Frame(n)
        s = size[f]
        c = 7
        x, y = xy(n, c + 0.5, c + 0.5)
        ax, ay = np.abs(x), np.abs(y)
        if s <= 1:
            fr.paint(pk.pixels(n, n, [(c, c)]), W)
        else:
            astroid = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(s) + 0.3
            fr.paint(astroid, L)
            fr.paint(np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(s) * 0.62, W)
            if s >= 5:
                diag = (np.abs(ax - ay) < 0.6) & (ax < s * 0.42)
                fr.under(diag, M)
        frames.append(fr)
    return frames


def ring(n=32):
    frames = []
    for f in range(FRAMES):
        t = f / (FRAMES - 1)
        fr = Frame(n)
        x, y = xy(n, n / 2, n / 2)
        r = np.hypot(x, y)
        rad = 3.0 + 11.5 * (1 - (1 - t) ** 1.6)
        thick = 3.6 - 2.6 * t
        band = np.abs(r - rad) <= thick / 2 + 0.3
        inner = band & (r < rad - thick * 0.15)
        if t > 0.55:
            ang = np.arctan2(y, x)
            segs = 10
            keep = ((ang / (2 * math.pi) * segs + 0.25 * f) % 1.0) < (1.25 - t)
            band &= keep
            inner &= keep
        fr.paint(band, L if t < 0.6 else M)
        fr.paint(inner, W)
        frames.append(fr)
    return frames


def bolt(n=32):
    frames = []
    for f in range(FRAMES):
        rng = np.random.default_rng(500 + f)
        fr = Frame(n)
        fade = f >= 6
        pts = [(n / 2 + rng.uniform(-2, 2), 3.0)]  # 2 px core + 1 px glow ring stay inside the 1 px gutter
        y = 3.0
        while y < n - 4.5:
            y = min(n - 4.0, y + rng.uniform(3.5, 6))  # 2 px lines paint below their centre line
            pts.append((n / 2 + rng.uniform(-6, 6) * (1 - abs(y / n - 0.5)), y))
        core = np.zeros((n, n), bool)
        for a, b in zip(pts[:-1], pts[1:]):
            core |= pk.line(n, n, [a, b], 1 if fade else 2)
        branches = np.zeros((n, n), bool)
        for _ in range(0 if fade else 2):
            i = int(rng.integers(1, len(pts) - 1))
            bx, by = pts[i]
            ex, ey = bx + rng.choice([-1, 1]) * rng.uniform(4, 8), min(by + rng.uniform(3, 7), n - 4.0)
            mx, my = (bx + ex) / 2 + rng.uniform(-2, 2), (by + ey) / 2
            branches |= pk.line(n, n, [(bx, by), (mx, my), (ex, ey)], 1)
        if fade:
            core = dissolve(core, 0.35 if f == 6 else 0.6, 900 + f)
        glow = pk.dilate(core | branches, False) & ~(core | branches)
        fr.paint(glow, M)
        fr.paint(branches, L)
        fr.paint(core, W)
        frames.append(fr)
    return frames


def ice_shard(n=16):
    frames = []
    shard = [(0, -6.5), (2.3, -2.5), (2.0, 3.5), (0, 6.0), (-2.0, 3.5), (-2.3, -2.5)]
    for f in range(FRAMES):
        fr = Frame(n)
        ang = f / FRAMES * 2 * math.pi
        shaded_poly(fr, shard, 7.5, 7.5, ang)
        # bright edge highlight along the long facet line
        a = rot(0, -6.0, ang)
        b = rot(0, 5.5, ang)
        fr.paint(pk.line(n, n, [(7.5 + a[0], 7.5 + a[1]), (7.5 + b[0], 7.5 + b[1])], 1) & fr.a, W)
        frames.append(fr)
    return frames


def bubble(n=16):
    frames = []
    radii = [2.0, 3.0, 4.0, 4.6, 5.0]
    squash = [1.0, 1.1, 0.92, 1.06, 0.97]
    for f in range(FRAMES):
        fr = Frame(n)
        x, y = xy(n, 7.5, 8.5 - min(f, 4) * 0.5)
        if f < 5:  # (5.0 * 1.06 radius at centre y 6.5 keeps the 1 px gutter)
            r = radii[f]
            sx, sy = r * squash[f], r / squash[f]
            d = (x / sx) ** 2 + (y / sy) ** 2
            shell = (d <= 1.0) & (d > ((min(sx, sy) - 1.1) / min(sx, sy)) ** 2)
            fr.paint(shell, L)
            fr.paint(shell & (x + y > 0.6 * r), M)
            hx, hy = int(round(7.5 - r * 0.45)), int(round(8.5 - min(f, 4) * 0.5 - r * 0.45))
            fr.paint(pk.pixels(n, n, [(hx, hy), (hx + 1, hy), (hx, hy + 1)] if r >= 3 else [(hx, hy)]), W)
        else:
            k = f - 5
            rr = (4.0, 5.0, 5.9)[k]  # pop droplets fly out but stay inside the 1 px gutter
            for i in range(6):
                ang = i / 6 * 2 * math.pi + 0.3
                px_, py_ = 7.5 + math.cos(ang) * rr, 7.0 + math.sin(ang) * rr
                size = 2 if k == 0 else 1
                fr.paint(pk.rect(n, n, int(px_), int(py_), int(px_) + size, int(py_) + size), L if k < 2 else M)
            if k == 0:
                fr.paint(pk.pixels(n, n, [(7, 6), (8, 6), (7, 7), (8, 7)]), W)
        frames.append(fr)
    return frames


HEART_S = [".#.#.",
           "#####",
           "#####",
           ".###.",
           "..#.."]
HEART_M = [".##.##.",
           "#######",
           "#######",
           "#######",
           ".#####.",
           "..###..",
           "...#..."]
HEART_L = [".###.###.",
           "#########",
           "#########",
           "#########",
           "#########",
           ".#######.",
           "..#####..",
           "...###...",
           "....#...."]
HEART_XL = [".####.####.",
            "###########",
            "###########",
            "###########",
            "###########",
            "###########",
            ".#########.",
            "..#######..",
            "...#####...",
            "....###....",
            ".....#....."]


def heart_frame(n, art, dissolve_amt=0.0, seed=0, sparkles=()):
    fr = Frame(n)
    m = pk.ascii_mask(art)
    h, w = m.shape
    big = np.zeros((n, n), bool)
    y0, x0 = (n - h) // 2 + (1 if h % 2 else 0), (n - w) // 2 + (1 if w % 2 == 0 else 0)
    big[y0:y0 + h, x0:x0 + w] = m
    if dissolve_amt > 0:
        big = dissolve(big, dissolve_amt, seed)
    fr.paint(big, L)
    # shadow on the bottom-right edge, highlight top-left lobe
    edge_br = big & ~pk.shift(big, -1, -1)
    fr.paint(edge_br, M)
    if h >= 7:
        hl = [(x0 + 1, y0 + 1), (x0 + 2, y0 + 1), (x0 + 1, y0 + 2)]
        fr.paint(pk.pixels(n, n, hl) & big, W)
    for sx, sy in sparkles:
        fr.paint(pk.pixels(n, n, [(sx, sy)]), W)
        fr.under(pk.pixels(n, n, [(sx - 1, sy), (sx + 1, sy), (sx, sy - 1), (sx, sy + 1)]), L)
    return fr


def heart(n=16):
    return [heart_frame(n, HEART_S), heart_frame(n, HEART_XL), heart_frame(n, HEART_L), heart_frame(n, HEART_M),
            heart_frame(n, HEART_L), heart_frame(n, HEART_L, 0.35, 11), heart_frame(n, HEART_M, 0.6, 12, [(3, 3), (12, 5)]),
            heart_frame(n, HEART_S, 1.1, 13, [(2, 5), (13, 3), (9, 13)])]


def debris(n=16):
    frames = []
    rock = [(-4.2, -1.5), (-1.5, -4.2), (2.5, -3.6), (4.4, -0.2), (3.0, 3.4), (-0.8, 4.2), (-3.8, 2.4)]
    for f in range(FRAMES):
        fr = Frame(n)
        ang = f / FRAMES * 2 * math.pi
        shaded_poly(fr, rock, 7.5, 7.5, ang, outline_value=None)
        frames.append(fr)
    return frames


def flash(n=32):
    frames = []
    c = n / 2
    for f in range(FRAMES):
        fr = Frame(n)
        x, y = xy(n, c, c)
        r = np.hypot(x, y)
        ang = np.arctan2(y, x)
        rays8 = np.abs(np.cos(ang * 4))  # 8 lobes
        if f == 0:
            fr.paint(r <= 5.5 + 5.0 * rays8 ** 6, L)
            fr.paint(r <= 5.0, W)
        elif f == 1:
            fr.paint(r <= 7.5 + 7.0 * rays8 ** 8, M)
            fr.paint(r <= 7.0 + 5.0 * rays8 ** 8, L)
            fr.paint(r <= 6.5, W)
        elif f == 2:
            fr.paint((r <= 11.0) & (r > 4.5), L)
            fr.paint((r <= 9.0) & (r > 6.0), W)
            fr.paint((r > 11.0) & (r <= 11.0 + 3.0 * rays8 ** 10), M)
        elif f == 3:
            fr.paint((r <= 12.5) & (r > 9.0), L)
            fr.paint((r <= 11.5) & (r > 10.0), W)
        elif f in (4, 5):
            k = f - 4
            band = (r <= 13.5 + k) & (r > 11.5 + k)
            keep = ((ang / (2 * math.pi) * 12 + 0.5 * k) % 1.0) < (0.7 - 0.25 * k)
            fr.paint(band & keep, L if k == 0 else M)
        else:
            rng = np.random.default_rng(700 + f)
            for _ in range(7 - (f - 6) * 3):
                a = rng.uniform(0, 2 * math.pi)
                rr = rng.uniform(10, 13.5)
                px_, py_ = int(c + math.cos(a) * rr), int(c + math.sin(a) * rr)
                fr.paint(pk.pixels(n, n, [(px_, py_)]), W if f == 6 else L)
                if f == 6:
                    fr.under(pk.pixels(n, n, [(px_ + 1, py_), (px_ - 1, py_), (px_, py_ + 1), (px_, py_ - 1)]), M)
        frames.append(fr)
    return frames


# name: (generator, frame size, loop, fps, suggested tint for previews, usage)
SHEETS = {
    "Spark": (spark, 16, False, 12, "#ffe070", "HitSpark / WallSpark / CritSpark burst"),
    "Dust": (dust, 16, False, 12, "#d8c8a8", "DustPuff on enemy hop landing, ball scuffs"),
    "Leaf": (leaf, 16, True, 12, "#8ccf5a", "Act 1 falling leaves (ambient), tumble loop"),
    "Ember": (ember, 16, True, 12, "#ff9040", "Act 2 drifting embers, BurnBurst, torch sparks"),
    "Snow": (snow, 16, True, 12, "#c8f0ff", "FreezeBurst flakes, frost ball trail"),
    "Mote": (mote, 16, True, 12, "#b890ff", "Act 3 floating magic motes (GlowParticle, pulsing)"),
    "Smoke": (smoke, 16, False, 12, "#a0a0b0", "EnemyPoof / Explosion smoke / BossPoof"),
    "Star": (star, 16, False, 12, "#fff0a0", "PickupSparkle / LevelUpBurst / HealSparkle twinkles"),
    "Ring": (ring, 32, False, 12, "#80e0ff", "Shockwave: Explosion, PortalFlash, SplitPop, LevelUp"),
    "Bolt": (bolt, 32, True, 12, "#fff080", "LightningHit / Thunder chain (random start frame)"),
    "IceShard": (ice_shard, 16, True, 12, "#a0e8ff", "FreezeBurst shards (tumble loop)"),
    "Bubble": (bubble, 16, False, 12, "#b060e0", "PoisonBurst bubbles rising then popping"),
    "Heart": (heart, 16, False, 12, "#ff5a78", "HealSparkle / Vampire heal / Heal pickup"),
    "Debris": (debris, 16, True, 12, "#b08a60", "CratePieces / rubble / bone-wall chunks (tumble loop)"),
    "Flash": (flash, 32, False, 12, "#ffffff", "Impact flash: CritSpark, BossHit, Explosion core"),
}


def border_ink(alpha_mask):
    """True when any opaque pixel sits on the outer 1 px ring of the frame."""
    return bool(alpha_mask[0].any() or alpha_mask[-1].any() or alpha_mask[:, 0].any() or alpha_mask[:, -1].any())


def tint(img, color):
    c = np.array(pk.rgba(color)[:3], np.float32) / 255
    out = img.copy()
    out[..., :3] = (img[..., :3].astype(np.float32) * c).round().astype(np.uint8)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    meta, items, touching = {}, [], []
    for name, (gen, size, loop, fps, color, usage) in SHEETS.items():
        frames = gen(size)
        assert len(frames) == FRAMES
        touching += [f"{name}[{i}]" for i, fr in enumerate(frames) if border_ink(fr.a)]
        strip = np.concatenate([fr.rgba() for fr in frames], axis=1)
        a_vals = set(np.unique(strip[..., 3]).tolist())
        assert a_vals <= {0, 255}, (name, a_vals)
        pk.save_rgba(pk.staging(*OUT_DIR, name + ".png"), strip)
        meta[name] = {"file": name + ".png", "frames": FRAMES, "frameSize": [size, size], "columns": FRAMES, "rows": 1,
                      "loop": loop, "fps": fps, "startSizeMetres": round(size / 28.0, 3), "previewTint": color,
                      "usage": usage}
        items.append((f"{name} {size}px {'loop' if loop else 'once'}", strip))
        items.append((name + " tinted", tint(strip, color)))
    if touching:
        sys.exit("particle ink touches the frame border (keep a 1 px gutter): " + ", ".join(touching))
    pk.save_json(pk.staging(*OUT_DIR, "particles.json"), {
        "note": "Generated by Tools/Textures/make_particles.py. Horizontal strips, white/grey values, alpha 0/255. "
                "Texture Sheet Animation: Grid, tiles (columns, rows), Whole Sheet, frame rate = fps.",
        "texelsPerMetre": 28, "sheets": meta})
    sheet = pk.contact_sheet(items, scale=4, cols=2, checker_bg=False, bg=(24, 24, 32))
    pk.save_rgb(os.path.join(preview_dir, "particles.png"), sheet)
    print("PARTICLES", {"count": len(SHEETS), "preview": os.path.join(preview_dir, "particles.png")})


if __name__ == "__main__":
    main()
