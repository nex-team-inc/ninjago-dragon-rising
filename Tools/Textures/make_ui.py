"""Pixel-art UI kit: 9-slice frames, bars, HUD icons, cursor, chip, overlays and the title logo.

Run:  Tools/.venv/bin/python Tools/Textures/make_ui.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/UI/*.png + ui_slices.json (9-slice borders, suggested Image
      type, native size, pixel scale) and previews (sheet + mock-up at 3x) in DIR.

Scale: UI pixel art is drawn at 1x and shown at an integer 3x on the 1920x1080 canvas (the same texel size as
the 640x360 world RT). ImportSettingsBuilder therefore imports these sprites with PPU = 100/3, so
Image.SetNativeSize() gives 3x; the logo is usually placed at 2x (rect set explicitly).
9-slice rules: edges are uniform along their stretch axis and centres are flat or tileable, so Sliced (and
Tiled where noted) never distort the pixel art. Borders are in sprite pixels (left, bottom, right, top =
Unity's Sprite.border order x,y,z,w). Deterministic.
"""
import argparse
import math
import os
import sys

import numpy as np
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Fonts"))
import build_pixel_font as bpf  # noqa: E402
import make_icons as mi  # noqa: E402
import pixelkit as pk  # noqa: E402

OUT_DIR = ("Sprites", "BilliardRogue", "UI")
PIXEL_SCALE = 3

INK = "#0a0c18"
GOLD = {"hi": "#fff4b8", "lt": "#ffd65a", "md": "#e8a830", "dk": "#a8661a", "xd": "#5c360c"}
NAVY = {"a": "#34447a", "b": "#283867", "c": "#1f2c56", "d": "#172246", "e": "#111936", "f": "#0c1228"}
PARCH = {"hi": "#fbf1d2", "a": "#f2e2b4", "b": "#e6d29e", "c": "#d4b97e", "d": "#b8965a", "e": "#8a6a38"}
CRIMSON = {"hi": "#ff7a86", "a": "#e0485a", "b": "#b8283a", "c": "#8c1c2c", "d": "#6a1222", "e": "#4a0a16"}


# ----------------------------------------------------------------------------------------------
# frame machinery
# ----------------------------------------------------------------------------------------------

def rounded_mask(w, h, chamfer):
    x, y = np.meshgrid(np.arange(w), np.arange(h))
    cx = np.minimum(x, w - 1 - x)
    cy = np.minimum(y, h - 1 - y)
    return (cx + cy) >= chamfer


def rings(mask):
    """Distance (1 = outermost pixel) of every mask pixel to the outside, chessboard metric."""
    return ndimage.distance_transform_cdt(np.pad(mask, 1), metric="chessboard")[1:-1, 1:-1]


def lit_side(w, h):
    x, y = np.meshgrid(np.arange(w), np.arange(h))
    return np.minimum(x, y) <= np.minimum(w - 1 - x, h - 1 - y)


def frame(w, h, layers, center, chamfer=2, margin=0):
    """layers: [(lit_colour, shade_colour)] one per ring from the outside in; center: colour or callable(img, mask).
    The chamfer rounds only the outer `chamfer` rings; deeper rings keep square corners so nothing leaks into
    the 9-slice centre."""
    img = pk.canvas(w, h)
    inner_w, inner_h = w - 2 * margin, h - 2 * margin
    m = rounded_mask(inner_w, inner_h, chamfer)
    d_round = rings(m)
    x, y = np.meshgrid(np.arange(inner_w), np.arange(inner_h))
    d_rect = np.minimum(np.minimum(x, inner_w - 1 - x), np.minimum(y, inner_h - 1 - y)) + 1
    d = np.where(d_round <= chamfer, d_round, np.maximum(d_rect, chamfer + 1))
    lit = lit_side(inner_w, inner_h)
    sub = pk.canvas(inner_w, inner_h)
    for i, (lc, sc) in enumerate(layers):
        ring = m & (d == i + 1)
        pk.put(sub, ring & lit, lc)
        pk.put(sub, ring & ~lit, sc)
    inside = m & (d > len(layers))
    if callable(center):
        center(sub, inside)
    elif center is not None:
        pk.put(sub, inside, center)
    img[margin:margin + inner_h, margin:margin + inner_w] = sub
    return img


def stud(img, x, y, colors=(GOLD["hi"], GOLD["lt"], GOLD["dk"], INK)):
    """3x3 diamond rivet with a 1 px dark rim, centred on (x, y)."""
    hi, lt, dk, ink = colors
    for dx, dy in ((0, -2), (-1, -1), (1, -1), (-2, 0), (2, 0), (-1, 1), (1, 1), (0, 2)):
        img[y + dy, x + dx] = pk.rgba(ink)
    img[y, x] = pk.rgba(lt)
    img[y - 1, x] = pk.rgba(hi)
    img[y, x - 1] = pk.rgba(hi)
    img[y, x + 1] = pk.rgba(dk)
    img[y + 1, x] = pk.rgba(dk)


def mirror_corners(img, fn):
    """Draws fn(img) in the top-left corner and mirrors the result into the other three corners."""
    h, w = img.shape[:2]
    layer = pk.canvas(w, h)
    fn(layer)
    for flip_x in (False, True):
        for flip_y in (False, True):
            l2 = layer
            if flip_x:
                l2 = l2[:, ::-1]
            if flip_y:
                l2 = l2[::-1]
            pk.blit(img, np.ascontiguousarray(l2), 0, 0)
    return img


# ----------------------------------------------------------------------------------------------
# frames
# ----------------------------------------------------------------------------------------------

def plank_center(tile=16):
    def fill(img, mask):
        ys, xs = np.nonzero(mask)
        x0, y0 = xs.min(), ys.min()
        x, y = np.meshgrid(np.arange(img.shape[1]), np.arange(img.shape[0]))
        lx, ly = (x - x0) % tile, (y - y0) % tile
        pk.put(img, mask, NAVY["d"])
        pk.put(img, mask & ((ly == 7) | (ly == 15)), NAVY["e"])   # plank seams (uniform along x)
        pk.put(img, mask & ((ly == 8) | (ly == 0)), "#1b284e")    # lit lip under each seam
        pk.put(img, mask & (ly == 7) & (lx == 11), NAVY["f"])      # one butt joint per plank row
        pk.put(img, mask & (ly == 15) & (lx == 3), NAVY["f"])
    return fill


def frame_panel():
    layers = [(INK, INK), (GOLD["hi"], GOLD["md"]), (GOLD["lt"], GOLD["dk"]), (GOLD["xd"], GOLD["xd"]),
              (NAVY["a"], NAVY["c"]), (NAVY["b"], NAVY["d"]), (NAVY["c"], NAVY["d"]), (NAVY["e"], NAVY["e"]),
              (GOLD["md"], GOLD["dk"]), ("#070912", "#070912")]
    img = frame(36, 36, layers, plank_center(16), chamfer=3)
    mirror_corners(img, lambda l: stud(l, 5, 5))
    return img, {"border": [10, 10, 10, 10], "imageType": "Tiled", "note": "tile the plank centre; edges are uniform"}


def parchment_center(tile=16):
    def fill(img, mask):
        ys, xs = np.nonzero(mask)
        x0, y0 = xs.min(), ys.min()
        rng = np.random.default_rng(5)
        speck = rng.random((tile, tile))
        x, y = np.meshgrid(np.arange(img.shape[1]), np.arange(img.shape[0]))
        s = speck[(y - y0) % tile, (x - x0) % tile]
        pk.put(img, mask, PARCH["a"])
        pk.put(img, mask & (s < 0.10), PARCH["b"])
        pk.put(img, mask & (s > 0.96), PARCH["hi"])
    return fill


def frame_card():
    layers = [("#1a0e06", "#1a0e06"), (GOLD["hi"], GOLD["md"]), (GOLD["lt"], GOLD["dk"]), (GOLD["xd"], GOLD["xd"]),
              (NAVY["b"], NAVY["d"]), (NAVY["c"], NAVY["e"]), (NAVY["e"], NAVY["e"]), (GOLD["lt"], GOLD["dk"]),
              ("#3a2208", "#3a2208"), (PARCH["d"], PARCH["e"]), (PARCH["c"], PARCH["d"]), (PARCH["b"], PARCH["c"])]
    img = frame(48, 48, layers, parchment_center(16), chamfer=3)

    def corner(l):
        # gold filigree L in the corner of the parchment + rivet on the frame
        stud(l, 5, 5)
        for x, y, c in ((12, 12, GOLD["dk"]), (13, 12, GOLD["md"]), (14, 12, GOLD["lt"]), (12, 13, GOLD["md"]),
                        (12, 14, GOLD["lt"]), (13, 13, GOLD["hi"])):
            l[y, x] = pk.rgba(c)
    mirror_corners(img, corner)
    return img, {"border": [16, 16, 16, 16], "imageType": "Tiled", "note": "parchment centre tiles (16 px); corner filigree"}


BUTTON = {"w": 24, "h": 24, "margin": 3}


def button(focused):
    w, h, m = BUTTON["w"], BUTTON["h"], BUTTON["margin"]
    if focused:
        layers = [("#3a2006", "#3a2006"), (GOLD["hi"], GOLD["lt"]), (GOLD["lt"], GOLD["md"]), ("#4a5ea0", "#1a2650")]
        fill = "#2c3c74"
    else:
        layers = [(INK, INK), (GOLD["md"], GOLD["dk"]), (GOLD["xd"], GOLD["xd"]), (NAVY["a"], NAVY["e"])]
        fill = NAVY["c"]
    img = frame(w, h, layers, fill, chamfer=2, margin=m)
    # bottom lip: 1 extra dark row above the bottom rim (pressable look); uniform along x
    inner = rounded_mask(w - 2 * m, h - 2 * m, 2)
    d = rings(inner)
    lip_row = h - m - 1 - len(layers)
    lip = np.zeros((h, w), bool)
    lip[lip_row, m + len(layers):w - m - len(layers)] = True
    pk.put(img, lip, "#1a2650" if focused else NAVY["f"])
    if focused:
        glow = pk.canvas(w, h)
        body = pk.alpha(img)
        for i, a in enumerate((200, 130, 70)):
            ring = pk.dilate(body, i > 0, i + 1) & ~pk.dilate(body, i > 1, i) if i else pk.dilate(body, False, 1) & ~body
            glow[ring & ~pk.alpha(glow)] = pk.rgba("#ffd84a", a)
        # corner sparkles on the rim
        img = pk.blend(glow, img)
        for x, y in ((m + 2, m + 1), (w - m - 3, h - m - 2)):
            img[y, x] = pk.rgba("#ffffff")
    top = m + 1 + len(layers) - 1
    return img, {"border": [m + 3, m + 4, m + 3, m + 3], "imageType": "Sliced", "glowMargin": m,
                 "note": "same geometry as the other button state; visible frame is inset by glowMargin"}


def frame_banner():
    w, h = 64, 24
    img = pk.canvas(w, h)
    x, y = pk.grid(w, h)
    # tails (behind), notched, darker
    for flip in (False, True):
        layer = pk.canvas(w, h)
        tail = pk.poly(w, h, [(0, 6), (14, 6), (14, 23), (0, 23), (5, 14.5)])
        pk.put(layer, tail, CRIMSON["c"])
        pk.put(layer, tail & (np.floor(y) == 7), CRIMSON["b"])
        pk.put(layer, tail & (np.floor(y) >= 20), CRIMSON["d"])
        pk.put(layer, tail & (np.floor(y) == 9) & (x > 3), GOLD["dk"])
        pk.put(layer, tail & (np.floor(y) == 19) & (x > 3), GOLD["xd"])
        fold = pk.poly(w, h, [(10, 18), (16, 18), (16, 23)])
        pk.put(layer, fold, CRIMSON["e"])
        ring = pk.dilate(tail | fold, False) & ~(tail | fold)
        pk.put(layer, ring, "#1a0610")
        if flip:
            layer = np.ascontiguousarray(layer[:, ::-1])
        pk.blit(img, layer, 0, 0)
    # band (front), uniform along x between x = 10 .. w-11
    band = np.zeros((h, w), bool)
    band[1:18, 10:w - 10] = True
    rows = {1: "#1a0610", 2: GOLD["hi"], 3: GOLD["md"], 4: GOLD["xd"], 5: CRIMSON["a"], 6: CRIMSON["b"],
            13: CRIMSON["c"], 14: GOLD["xd"], 15: GOLD["lt"], 16: GOLD["dk"], 17: "#1a0610"}
    for yy in range(1, 18):
        pk.put(img, band & (np.floor(y) == yy), rows.get(yy, CRIMSON["b"]))
    pk.put(img, band & ((np.floor(x) == 10) | (np.floor(x) == w - 11)), "#1a0610")
    pk.put(img, band & ((np.floor(x) == 11)) & (y > 4) & (y < 14), CRIMSON["c"])
    pk.put(img, band & ((np.floor(x) == w - 12)) & (y > 4) & (y < 14), CRIMSON["c"])
    return img, {"border": [17, 7, 17, 5], "imageType": "Sliced",
                 "note": "stretch horizontally; keep native height (24 px -> 72 at 3x); text rows 5..13"}


def bar_bg():
    layers = [(INK, INK), (GOLD["md"], GOLD["xd"]), ("#05060c", "#05060c"), ("#0c0b18", "#221f3a")]
    img = frame(12, 12, layers, "#1a1830", chamfer=1)
    return img, {"border": [4, 4, 4, 4], "imageType": "Sliced", "fillInset": 3,
                 "note": "place Bar_Fill_* inset by fillInset px (3x = 9 canvas units) on each side"}


def bar_fill(stops, sheen=False, ticks=0):
    w, h = 64, 8
    img = pk.canvas(w, h)
    x = np.arange(w)
    t = x / (w - 1)
    n = len(stops) - 1
    base = np.zeros((w, 3), np.float32)
    for i in range(w):
        k = min(int(t[i] * n), n - 1)
        f = t[i] * n - k
        a, b = np.array(pk.rgba(stops[k])[:3], np.float32), np.array(pk.rgba(stops[k + 1])[:3], np.float32)
        base[i] = a + (b - a) * f
    # posterise the gradient into 4 clean steps (pixel look, no dither noise)
    steps = 4
    q = np.minimum(np.floor(t * steps), steps - 1) / (steps - 1)
    cols = np.array([base[int(round(v * (w - 1)))] for v in q])
    row_k = [1.55, 1.25, 1.0, 1.0, 1.0, 0.82, 0.68, 0.5]
    for yy in range(h):
        k = row_k[yy]
        c = np.clip(cols * k if k <= 1 else cols + (255 - cols) * (k - 1), 0, 255)
        img[yy, :, :3] = c.astype(np.uint8)
        img[yy, :, 3] = 255
    if sheen:
        img[1, ::4, :3] = np.clip(img[1, ::4, :3].astype(int) + 40, 0, 255).astype(np.uint8)
    if ticks:
        for tx in range(ticks, w, ticks):
            img[1:h - 1, tx, :3] = (img[1:h - 1, tx, :3] * 0.6).astype(np.uint8)
    return img, {"border": [0, 0, 0, 0], "imageType": "Filled", "fillMethod": "Horizontal",
                 "note": "stretch to the Bar_Bg interior; Filled Horizontal (origin Left) -> fillAmount = hp/max"}


def frame_slot(active):
    if active:
        layers = [("#3a2006", "#3a2006"), (GOLD["hi"], GOLD["lt"]), (GOLD["lt"], GOLD["md"]), ("#05060c", "#05060c")]
        center = "#2a3668"
    else:
        layers = [(INK, INK), (GOLD["md"], GOLD["xd"]), ("#05060c", "#05060c"), ("#0c1024", "#1c2446")]
        center = "#161e3c"
    img = frame(40, 40, layers, center, chamfer=2)
    return img, {"border": [4, 4, 4, 4], "imageType": "Sliced", "note": "holds a 32x32 ball icon at the centre"}


def chip():
    layers = [(INK, INK), (GOLD["lt"], GOLD["dk"]), ("#0a0e20", "#0a0e20")]
    img = frame(16, 12, layers, NAVY["d"], chamfer=3)
    return img, {"border": [5, 4, 5, 4], "imageType": "Sliced", "note": "small rounded tag (Fast-forward, P1/P2, BOSS)"}


# ----------------------------------------------------------------------------------------------
# HUD icons (16x16)
# ----------------------------------------------------------------------------------------------

SKULL = ["...#######...",
         "..#########..",
         ".###########.",
         "#############",
         "##eee###eee##",
         "##eee###eee##",
         "##eee###eee##",
         ".#####n#####.",
         "..###nnn###..",
         "...#######...",
         "...#d#d#d#...",
         "...#######..."]
HOURGLASS = ["fffffffffff",
             ".fgggggggf.",
             ".fsssssssf.",
             ".fgsssssgf.",
             "..fgsssgf..",
             "...fgsgf...",
             "....fsf....",
             "....fsf....",
             "...fgsgf...",
             "..fggsggf..",
             ".fggsssggf.",
             ".fgsssssgf.",
             ".fsssssssf.",
             "fffffffffff"]
CHEVRON = ["###......",
           "####.....",
           ".####....",
           "..####...",
           "...####..",
           "....####.",
           ".....####",
           "....####.",
           "...####..",
           "..####...",
           ".####....",
           "####.....",
           "###......"]


def icon_heart():
    img = pk.canvas(16, 16)
    hm = mi.heart_mask(16, 8, 7.6, 7.0)
    mi.shade_mask(img, hm, ["#6a0a18", "#a81628", "#dc2c3c", "#ff6070", "#ffb0b6"])
    pk.put(img, pk.dilate(hm, False) & ~hm, "#1a0408")
    img[4, 4] = pk.rgba("#ffffff")
    return img


def icon_ball():
    img, _ = mi.sphere(16, 8.0, 8.0, 6.5, ["#7a8298", "#a8aec0", "#d2d6e0", "#eceef3", "#ffffff"], outline="#1a2030")
    return img


def icon_skull():
    img = pk.canvas(16, 16)
    bone = mi.art(SKULL, "#") | mi.art(SKULL, "e") | mi.art(SKULL, "n") | mi.art(SKULL, "d")
    x0, y0 = mi.centered(img, bone, 0, 1)
    mi.emblem(img, bone, "#f4ecd8", "#1a1014", x0, y0, shade="#c8b89a",
              extra={"#2a1418": mi.art(SKULL, "e") | mi.art(SKULL, "n") | mi.art(SKULL, "d")})
    return img


def icon_turn():
    img = pk.canvas(16, 16)
    shape = mi.art(HOURGLASS, "fgs")
    x0, y0 = mi.centered(img, shape, 0, 0)
    mi.emblem(img, shape, GOLD["md"], "#1a1008", x0, y0,
              extra={"#cfe8f4": mi.art(HOURGLASS, "g"), "#ffb030": mi.art(HOURGLASS, "s"),
                     GOLD["lt"]: mi.art(HOURGLASS, "f") & np.array([[r == 0] * 11 for r in range(14)])})
    return img


def icon_arrow():
    img = pk.canvas(16, 16)
    m = mi.art(CHEVRON)
    x0, y0 = mi.centered(img, m, 1, 0)
    upper = m.copy()
    upper[7:] = False
    mi.emblem(img, m, GOLD["dk"], "#1a1008", x0, y0, extra={GOLD["lt"]: upper, GOLD["hi"]: upper & ~pk.shift(upper, 0, 1)})
    return img


def icon_cursor():
    img = pk.canvas(16, 16)
    paw = np.rot90(mi.art(mi.PAW), -1)  # toes point right = direction of the pointer
    x0, y0 = mi.centered(img, paw, 0, 0)
    pads = paw & ~pk.erode(paw, False)
    mi.emblem(img, paw, "#ff9ab4", "#1a0c14", x0, y0, shade="#e06a8c",
              extra={"#ffd8e4": paw & pk.inner_edge(paw, "top")})
    return img


def overlay_vignette():
    w, h = 256, 144
    x, y = pk.grid(w, h)
    u, v = (x / w - 0.5) * 2, (y / h - 0.5) * 2
    r = np.sqrt((u * 0.92) ** 2 + (v * 1.0) ** 2)
    a = np.clip((r - 0.55) / 0.85, 0, 1) ** 1.6 * 215
    img = pk.canvas(w, h)
    img[..., :3] = (6, 6, 16)
    img[..., 3] = a.round().astype(np.uint8)
    return img, {"border": [0, 0, 0, 0], "imageType": "Simple", "filter": "Bilinear",
                 "note": "full-screen stretch, smooth alpha (not pixel art); tint via Image.color"}


# ----------------------------------------------------------------------------------------------
# logo
# ----------------------------------------------------------------------------------------------

def epx(mask):
    """Scale2x/EPX on a bool mask: 2x upscale that smooths 45-degree stairs, keeps square corners."""
    h, w = mask.shape
    p = np.pad(mask, 1)
    A, B, C, D = p[:-2, 1:-1], p[1:-1, 2:], p[1:-1, :-2], p[2:, 1:-1]  # up, right, left, down
    P = mask
    e0 = np.where((C == A) & (C != D) & (A != B), A, P)
    e1 = np.where((A == B) & (A != C) & (B != D), B, P)
    e2 = np.where((D == C) & (D != B) & (C != A), C, P)
    e3 = np.where((B == D) & (B != A) & (D != C), D, P)
    out = np.zeros((h * 2, w * 2), bool)
    out[0::2, 0::2], out[0::2, 1::2], out[1::2, 0::2], out[1::2, 1::2] = e0, e1, e2, e3
    return out


def word_mask(text, glyphs, spacing=1, space=4, ball_slot=None):
    """Caps-only word -> bool mask (10 rows). ball_slot: index of a letter replaced by a round gap (returns x range)."""
    cols, slot = [], None
    for i, ch in enumerate(text):
        if ch == " ":
            cols.append(np.zeros((10, space), bool))
            continue
        px, w = glyphs[ord(ch)]
        m = np.zeros((10, w), bool)
        for x, yy in px:
            m[9 - yy, x] = True
        if ball_slot == i:
            start = sum(c.shape[1] for c in cols)
            slot = (start, start + 10)
            m = np.zeros((10, 10), bool)
        cols.append(m)
        if i < len(text) - 1:
            cols.append(np.zeros((10, spacing), bool))
    return np.concatenate(cols, axis=1), slot


def scale_mask(mask, target_h):
    big = mask
    for _ in range(3):
        big = epx(big)  # 8x
    k = target_h / big.shape[0]
    out = ndimage.zoom(big.astype(np.float32), k, order=1) > 0.5
    return out


def style_letters(mask, ramp, hi, dk, ink, extrude, ext_colors):
    """Gradient fill + bevel + extrusion + outline. Returns RGBA at mask size + padding."""
    pad = 4 + extrude
    m = np.pad(mask, pad)
    h, w = m.shape
    img = pk.canvas(w, h)
    # extrusion toward bottom-right
    ext = np.zeros_like(m)
    for k in range(1, extrude + 1):
        ext |= pk.shift(m, k // 2, k)
    ext &= ~m
    pk.put(img, ext, ext_colors[0])
    pk.put(img, ext & ~pk.shift(ext, 0, -1), ext_colors[1])
    # gradient fill in bands by row within the letter height
    ys = np.nonzero(m.any(axis=1))[0]
    top, bot = ys.min(), ys.max()
    yy = (np.arange(h)[:, None] - top) / max(bot - top, 1)
    band = np.clip((yy * len(ramp)).astype(int), 0, len(ramp) - 1)
    band = np.broadcast_to(band, m.shape)
    for i, c in enumerate(ramp):
        pk.put(img, m & (band == i), c)
    # shine line at 30% height, bevel highlight on top edges, shade on bottom edges
    shine = m & (np.abs(yy - 0.3) < 0.5 / max(bot - top, 1) * 2)
    pk.put(img, shine, hi)
    top_edge = m & ~pk.shift(m, 0, 1)
    top_edge2 = m & ~pk.shift(m, 0, 2)
    pk.put(img, top_edge2, hi)
    left_edge = m & ~pk.shift(m, 1, 0)
    pk.put(img, left_edge & ~top_edge, lighten_hex(ramp[1]))
    bottom_edge = m & ~pk.shift(m, 0, -2)
    right_edge = m & ~pk.shift(m, -1, 0)
    pk.put(img, bottom_edge | right_edge, dk)
    solid = m | ext
    ring1 = pk.dilate(solid, True, 2) & ~solid
    pk.put(img, ring1, ink)
    return img


def lighten_hex(c, k=0.35):
    r, g, b = pk.rgba(c)[:3]
    return (int(r + (255 - r) * k), int(g + (255 - g) * k), int(b + (255 - b) * k))


def logo():
    W, H = 640, 200
    glyphs = bpf.parse(os.path.join(os.path.dirname(HERE), "Fonts", "glyphs_bold.txt"))
    img = pk.canvas(W, H)
    # --- cue stick (behind everything), lower-left -> upper-right
    a, b = np.array([28.0, 186.0]), np.array([612.0, 22.0])
    d = (b - a) / np.linalg.norm(b - a)
    n = np.array([-d[1], d[0]])
    x, y = pk.grid(W, H)
    rel = np.stack([x - a[0], y - a[1]], -1)
    along = rel @ d
    across = rel @ n
    length = np.linalg.norm(b - a)
    half = 3.2 + 2.4 * np.clip(1 - along / length, 0, 1)  # tapers toward the tip
    cue = (along >= 0) & (along <= length) & (np.abs(across) <= half)
    t = along / length
    cue_img = pk.canvas(W, H)
    # butt (ebony with gold rings), shaft (maple), ferrule (ivory), tip (blue chalk)
    zones = [(0.0, 0.30, ["#2a140c", "#4a2616", "#6a3a20"]), (0.30, 0.315, [GOLD["dk"], GOLD["lt"], GOLD["hi"]]),
             (0.315, 0.34, ["#2a140c", "#4a2616", "#6a3a20"]), (0.34, 0.35, [GOLD["dk"], GOLD["lt"], GOLD["hi"]]),
             (0.35, 0.955, ["#b07a3a", "#e0b070", "#f8dca0"]), (0.955, 0.985, ["#b8bcc8", "#e8eaf0", "#ffffff"]),
             (0.985, 1.001, ["#1c3c80", "#3a6ad0", "#7aa8ff"])]
    for z0, z1, (c_dk, c_md, c_lt) in zones:
        zone = cue & (t >= z0) & (t < z1)
        pk.put(cue_img, zone, c_md)
        pk.put(cue_img, zone & (across < -half * 0.35), c_lt)
        pk.put(cue_img, zone & (across > half * 0.45), c_dk)
    ring = pk.dilate(cue, True, 2) & ~cue
    pk.put(cue_img, ring, "#0e1224")
    pk.blit(img, cue_img, 0, 0)
    # --- words
    top_mask, _ = word_mask("BILLIARD", glyphs, spacing=2)
    bot_mask, slot = word_mask("ROGUE", glyphs, spacing=2, ball_slot=1)
    top_s = scale_mask(top_mask, 56)
    bot_s = scale_mask(bot_mask, 84)
    ivory = ["#ffffff", "#f4f6fb", "#e2e6f0", "#cdd2e2", "#b4bad0"]
    gold = ["#fff2b0", "#ffd65a", "#ffc23c", "#f09a24", "#d0741a"]
    top_img = style_letters(top_s, ivory, "#ffffff", "#8a92b0", "#0e1224", 5, ("#3a4470", "#232a4c"))
    bot_img = style_letters(bot_s, gold, "#fffbe6", "#8a4410", "#1a0c06", 7, ("#7a2e10", "#4a1a08"))
    tx = (W - top_img.shape[1]) // 2 - 18
    pk.blit(img, top_img, tx, 2)
    bx = (W - bot_img.shape[1]) // 2 + 14
    by = H - bot_img.shape[0] - 2
    pk.blit(img, bot_img, bx, by)
    # --- the "O" of ROGUE is a cue ball with the paw emblem
    pad = 4 + 7
    scale = bot_s.shape[0] / 10.0
    cx = bx + pad + (slot[0] + 5) * scale
    cy = by + pad + bot_s.shape[0] / 2
    r = bot_s.shape[0] / 2 + 2
    ball_ramp = ["#6e7690", "#9aa2ba", "#c8cedc", "#e8ebf2", "#ffffff"]
    ball, inside = mi.sphere(W, cx, cy, r, ball_ramp, outline="#0e1224", height=H)
    # thicker outline + drop shadow toward bottom-right like the letters
    shadow = pk.shift(inside, 3, 5) & ~inside
    pk.put(img, pk.dilate(shadow | inside, True, 2) & ~(shadow | inside), "#1a0c06")
    pk.put(img, shadow, "#4a1a08")
    pk.blit(img, ball, 0, 0)
    paw_big = scale_mask(np.pad(mi.art(mi.PAW), 1), 42)
    ph, pw = paw_big.shape
    layer = pk.canvas(W, H)
    pm = np.zeros((H, W), bool)
    px0, py0 = int(cx - pw / 2), int(cy - ph / 2 + 3)
    pm[py0:py0 + ph, px0:px0 + pw] = paw_big
    pk.put(layer, pk.dilate(pm, True, 2) & ~pm & inside, "#2a3a6a")
    pk.put(layer, pm, "#6f86b6")
    pk.put(layer, pm & ~pk.shift(pm, -2, -2), "#4e64a0")
    pk.put(layer, pm & ~pk.shift(pm, 2, 2), "#98aede")
    pk.blit(img, layer, 0, 0)
    # --- sparkles
    for sx, sy, s in ((602, 34, 6), (590, 14, 3), (40, 30, 4), (560, 120, 3), (128, 176, 3)):
        star = pk.canvas(W, H)
        xx, yy = pk.grid(W, H)
        ax, ay = np.abs(xx - sx - 0.5), np.abs(yy - sy - 0.5)
        m = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(s) + 0.2
        core = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(s) * 0.55
        pk.put(star, pk.dilate(m, False) & ~m, "#0e1224")
        pk.put(star, m, GOLD["lt"])
        pk.put(star, core, "#ffffff")
        pk.blit(img, star, 0, 0)
    return img, {"border": [0, 0, 0, 0], "imageType": "Simple", "note": "title logo; place at 2x (1280x400) or 3x"}


# ----------------------------------------------------------------------------------------------
# main
# ----------------------------------------------------------------------------------------------

def mockup(sprites):
    """3x mock-up of a reward overlay + HUD to judge the kit together (preview only)."""
    W, H = 640, 360
    bg = pk.canvas(W, H, "#3a4a3a")
    x, y = pk.grid(W, H)
    bg[..., :3] = np.dstack([40 + 30 * (y / H), 52 + 36 * (y / H), 44 + 10 * (x / W)]).astype(np.uint8)

    def nine(name, w, h, tiled=False):
        spr, meta = sprites[name]
        L, B, R, T = meta["border"]
        sh, sw = spr.shape[:2]
        out = pk.canvas(w, h)
        cx0, cx1, cy0, cy1 = L, sw - R, T, sh - B
        cw, ch = cx1 - cx0, cy1 - cy0

        def span(n_out, src0, src1, lead, trail):
            mid = n_out - lead - trail
            idx = list(range(lead))
            if tiled:
                idx += [src0 + (i % (src1 - src0)) for i in range(mid)]
            else:
                idx += [src0 + min(int(i * (src1 - src0) / max(mid, 1)), src1 - src0 - 1) for i in range(mid)]
            idx += list(range(src1, src1 + trail))
            return np.array(idx)
        xi = span(w, cx0, cx1, L, R)
        yi = span(h, cy0, cy1, T, B)
        return spr[yi][:, xi]

    img = bg.copy()
    pk.blend(img, np.dstack([np.zeros((H, W, 3), np.uint8), np.full((H, W), 110, np.uint8)]))
    pk.blit(img, nine("Frame_Banner", 300, 24), 170, 22)
    for i in range(3):
        card = nine("Frame_Card", 120, 170, tiled=True)
        cx = 110 + i * 150
        pk.blit(img, card, cx, 70)
        icon = sprites["_icons"][i]
        pk.blit(img, pk.upscale(icon, 2), cx + 28, 100)
    pk.blit(img, nine("Frame_Panel", 220, 60, tiled=True), 210, 262)
    pk.blit(img, nine("Frame_ButtonFocused", 90, 24), 222, 280)
    pk.blit(img, nine("Frame_Button", 90, 24), 318, 280)
    # HUD: hp bar + balls + chip
    pk.blit(img, nine("Bar_Bg", 80, 14), 8, 8)
    fill = sprites["Bar_Fill_Hp"][0]
    fw = 74 - 0
    xi = (np.arange(int(fw * 0.7)) * fill.shape[1] / fw).astype(int)
    yi = (np.arange(8) * fill.shape[0] / 8).astype(int)
    pk.blit(img, fill[yi][:, xi], 11, 11)
    pk.blit(img, sprites["Icon_Heart"][0], 4, 7)
    pk.blit(img, nine("Chip", 60, 12), 570, 8)
    pk.blit(img, sprites["Arrow"][0], 88, 140)
    pk.blit(img, sprites["Cursor"][0], 200, 284)
    return pk.upscale(img, 3)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    sprites = {
        "Frame_Panel": frame_panel(),
        "Frame_Card": frame_card(),
        "Frame_Button": button(False),
        "Frame_ButtonFocused": button(True),
        "Frame_Banner": frame_banner(),
        "Frame_Slot": frame_slot(False),
        "Frame_SlotActive": frame_slot(True),
        "Bar_Bg": bar_bg(),
        "Bar_Fill_Hp": bar_fill(["#b01830", "#d8283a", "#f04a3a", "#ff7a3a"]),
        "Bar_Fill_Boss": bar_fill(["#4a1e88", "#6a2ab0", "#8a3ad0", "#b060f0"], sheen=True, ticks=16),
        "Chip": chip(),
        "Icon_Heart": (icon_heart(), {}),
        "Icon_Ball": (icon_ball(), {}),
        "Icon_Skull": (icon_skull(), {}),
        "Icon_Turn": (icon_turn(), {}),
        "Arrow": (icon_arrow(), {"note": "right chevron; Arrow_Left is the mirrored copy"}),
        "Cursor": (icon_cursor(), {"note": "paw pointer, toes point right; bob it horizontally"}),
        "Overlay_Vignette": overlay_vignette(),
        "Logo_BilliardRogue": logo(),
    }
    sprites["Arrow_Left"] = (pk.flip_h(sprites["Arrow"][0]), {"note": "left chevron"})
    meta = {}
    for name, (img, info) in sprites.items():
        h, w = img.shape[:2]
        assert w % 4 == 0 and h % 4 == 0, (name, w, h)
        pk.save_rgba(pk.staging(*OUT_DIR, name + ".png"), img)
        L, B, R, T = info.get("border", [0, 0, 0, 0])
        meta[name] = {"file": name + ".png", "size": [w, h],
                      "border": {"left": L, "bottom": B, "right": R, "top": T},
                      "imageType": info.get("imageType", "Simple"), "filter": info.get("filter", "Point"),
                      "note": info.get("note", "")}
        for k in ("glowMargin", "fillInset", "fillMethod"):
            if k in info:
                meta[name][k] = info[k]
    pk.save_json(pk.staging(*OUT_DIR, "ui_slices.json"), {
        "note": "Generated by Tools/Textures/make_ui.py. Borders in sprite pixels. Show UI pixel art at an integer "
                "3x (PPU = 100/3 at CanvasScaler 1920x1080 scale 1).",
        "pixelScale": PIXEL_SCALE, "pixelsPerUnit": round(100.0 / PIXEL_SCALE, 4),
        "sprites": [dict(name=k, **v) for k, v in meta.items()]})
    order = [k for k in sprites if k not in ("Logo_BilliardRogue", "Overlay_Vignette")]
    items = [(k, sprites[k][0]) for k in order]
    pk.save_rgb(os.path.join(preview_dir, "ui_kit.png"), pk.contact_sheet(items, scale=4, cols=7))
    pk.save_rgb(os.path.join(preview_dir, "ui_logo.png"),
                pk.contact_sheet([("Logo_BilliardRogue 1x", sprites["Logo_BilliardRogue"][0])], scale=2, cols=1, checker_bg=False, bg=(24, 30, 56)))
    icons = [mi.BALL_FUNCS["Flame"](mi.BALLS["Flame"][0]), mi.reward_heal(), mi.reward_maxhp()]
    sprites["_icons"] = icons
    pk.save_rgb(os.path.join(preview_dir, "ui_mockup.png"), mockup(sprites))
    print("UI", {"sprites": len(meta), "preview": preview_dir})


if __name__ == "__main__":
    main()
