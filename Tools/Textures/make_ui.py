"""Pixel-art UI kit: 9-slice frames, bars, HUD icons, cursor, chip, overlays and the title logo.

Run:  Tools/.venv/bin/python Tools/Textures/make_ui.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/UI/*.png + ui_slices.json (9-slice borders, suggested Image
      type, native size, pixel scale) and previews in DIR: ui_kit.png, ui_logo.png, ui_mockup.png (reward overlay +
      HUD at 3x with real text from the built BilliardPixel fonts - run Tools/Fonts/build_pixel_font.py first).
Sprites: Frame_Panel/Card/Button/ButtonFocused/Banner/Slot/SlotActive, Bar_Bg, Bar_Fill_Hp/Boss, Chip, Icon_Heart/
      Ball/Skull/Turn, Arrow(+_Left), Cursor, Overlay_Dim (flat 72% navy for Pause/Reward/TrackingLost, TDD D3),
      Overlay_Vignette, Logo_BilliardRogue (640x200).

Scale: UI pixel art is drawn at 1x and shown at an integer 3x on the 1920x1080 canvas (the same texel size as
the 640x360 world RT). ImportSettingsBuilder therefore imports these sprites with PPU = 100/3, so
Image.SetNativeSize() gives 3x; the logo is usually placed at 2x (rect set explicitly).
9-slice rules: edges are uniform along their stretch axis and centres are flat or tileable, so Sliced (and
Tiled where noted) never distort the pixel art; validate_slices() enforces this and the build fails otherwise. Borders are in sprite pixels (left, bottom, right, top =
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
    """Dark navy wood for the Tiled panel centre: two 8 px planks per 16 px tile with 1 px seams (uniform along x),
    one staggered vertical butt joint per plank (dark line + lit edge), and 1 px grain streaks in two navy shades
    that wrap inside the tile, so the Tiled repeat stays seamless and reads as wood at 3x instead of a flat fill."""
    grain = [(2, 1, 6, NAVY["e"]), (4, 8, 3, NAVY["c"]), (5, 13, 5, NAVY["e"]),     # plank 1 (rows 0..6, joint lx 11)
             (10, 6, 7, NAVY["e"]), (12, 12, 4, NAVY["c"]), (13, 1, 3, NAVY["e"])]  # plank 2 (rows 8..14, joint lx 3)

    def fill(img, mask):
        ys, xs = np.nonzero(mask)
        x0, y0 = xs.min(), ys.min()
        x, y = np.meshgrid(np.arange(img.shape[1]), np.arange(img.shape[0]))
        lx, ly = (x - x0) % tile, (y - y0) % tile
        pk.put(img, mask, NAVY["d"])
        for gy, gx, n, c in grain:
            pk.put(img, mask & (ly == gy) & (((lx - gx) % tile) < n), c)
        pk.put(img, mask & ((ly == 7) | (ly == 15)), NAVY["e"])   # plank seams (uniform along x)
        pk.put(img, mask & ((ly == 8) | (ly == 0)), "#1b284e")    # lit lip under each seam
        for jx, r0, r1 in ((11, 0, 6), (3, 8, 14)):               # staggered butt joints
            pk.put(img, mask & (lx == jx) & (ly >= r0) & (ly <= r1), NAVY["f"])
            pk.put(img, mask & (lx == (jx + 1) % tile) & (ly >= r0 + 1) & (ly <= r1), NAVY["c"])
    return fill


def frame_panel():
    layers = [(INK, INK), (GOLD["hi"], GOLD["md"]), (GOLD["lt"], GOLD["dk"]), (GOLD["xd"], GOLD["xd"]),
              (NAVY["a"], NAVY["c"]), (NAVY["b"], NAVY["d"]), (NAVY["c"], NAVY["d"]), (NAVY["e"], NAVY["e"]),
              (GOLD["md"], GOLD["dk"]), ("#070912", "#070912")]
    img = frame(36, 36, layers, plank_center(16), chamfer=3)
    mirror_corners(img, lambda l: stud(l, 5, 5))
    return img, {"border": [10, 10, 10, 10], "imageType": "Tiled", "note": "tile the plank centre; edges are uniform"}


def parchment_center(tile=16, border=16):
    """Flat parchment in the 9-slice edge strips, speckled only inside the centre slice (tile-periodic), so the
    edges stay uniform along their stretch axis and the frame works as Sliced and as Tiled."""
    def fill(img, mask):
        h, w = img.shape[:2]
        rng = np.random.default_rng(5)
        speck = rng.random((tile, tile))
        x, y = np.meshgrid(np.arange(w), np.arange(h))
        s = speck[(y - border) % tile, (x - border) % tile]
        centre = (x >= border) & (x < w - border) & (y >= border) & (y < h - border)
        pk.put(img, mask, PARCH["a"])
        pk.put(img, mask & centre & (s < 0.10), PARCH["b"])
        pk.put(img, mask & centre & (s > 0.96), PARCH["hi"])
    return fill


def frame_card():
    layers = [("#1a0e06", "#1a0e06"), (GOLD["hi"], GOLD["md"]), (GOLD["lt"], GOLD["dk"]), (GOLD["xd"], GOLD["xd"]),
              (NAVY["b"], NAVY["d"]), (NAVY["c"], NAVY["e"]), (NAVY["e"], NAVY["e"]), (GOLD["lt"], GOLD["dk"]),
              ("#3a2208", "#3a2208"), (PARCH["d"], PARCH["e"]), (PARCH["c"], PARCH["d"]), (PARCH["b"], PARCH["c"])]
    img = frame(48, 48, layers, parchment_center(16, 16), chamfer=3)

    def corner(l):
        # gold filigree L in the corner of the parchment + rivet on the frame
        stud(l, 5, 5)
        for x, y, c in ((12, 12, GOLD["dk"]), (13, 12, GOLD["md"]), (14, 12, GOLD["lt"]), (12, 13, GOLD["md"]),
                        (12, 14, GOLD["lt"]), (13, 13, GOLD["hi"])):
            l[y, x] = pk.rgba(c)
    mirror_corners(img, corner)
    return img, {"border": [16, 16, 16, 16], "imageType": "Tiled", "note": "parchment centre tiles (16 px); corner filigree"}


BUTTON = {"w": 32, "h": 32, "margin": 3}  # native 32x32: 16 px caps fit with 3-4 px air


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
    # borders cover the glow margin + every ring (+ the bottom lip row) so edges/centre stay uniform when sliced
    ring = m + len(layers)
    return img, {"border": [ring, ring + 1, ring, ring], "imageType": "Sliced", "glowMargin": m,
                 "note": "same geometry as the other button state; visible frame is inset by glowMargin; "
                         "native 32 px (96 at 3x) holds one line of 16 px caps; stretch freely (edges and centre are uniform)"}


def frame_banner():
    """Crimson title ribbon with gold trim and notched tails. 16 crimson rows hold one line of 16 px caps
    (10 px) with 3 px air. Stretch horizontally only (the tails live in the left/right borders)."""
    w, h = 64, 32
    img = pk.canvas(w, h)
    x, y = pk.grid(w, h)
    fy = np.floor(y)
    band_top, band_bot, band_x0 = 2, 25, 12
    # tails (behind), notched, darker, with a folded corner where they tuck behind the band
    for flip in (False, True):
        layer = pk.canvas(w, h)
        tail = pk.poly(w, h, [(1, 9), (17, 9), (17, 30), (1, 30), (7, 19.5)])
        pk.put(layer, tail, CRIMSON["c"])
        pk.put(layer, tail & (fy == 10), CRIMSON["b"])
        pk.put(layer, tail & (fy >= 27), CRIMSON["d"])
        pk.put(layer, tail & (fy == 12) & (x > 4), GOLD["dk"])
        pk.put(layer, tail & (fy == 27) & (x > 4), GOLD["xd"])
        fold = pk.poly(w, h, [(13, band_bot + 1), (18, band_bot + 1), (18, 30)])
        pk.put(layer, fold, CRIMSON["e"])
        ring = pk.dilate(tail | fold, False) & ~(tail | fold)
        pk.put(layer, ring, "#1a0610")
        if flip:
            layer = np.ascontiguousarray(layer[:, ::-1])
        pk.blit(img, layer, 0, 0)
    # band (front): every row is uniform along x between the end caps
    band = np.zeros((h, w), bool)
    band[band_top:band_bot + 1, band_x0:w - band_x0] = True
    rows = {2: "#1a0610", 3: GOLD["hi"], 4: GOLD["md"], 5: GOLD["xd"], 6: CRIMSON["hi"], 7: CRIMSON["a"],
            19: CRIMSON["c"], 20: CRIMSON["c"], 21: CRIMSON["d"], 22: GOLD["xd"], 23: GOLD["lt"], 24: GOLD["dk"],
            25: "#1a0610"}
    for yy in range(band_top, band_bot + 1):
        pk.put(img, band & (fy == yy), rows.get(yy, CRIMSON["b"]))
    # end caps: dark outline + shaded crimson column + a gold rivet (all inside the 20 px side borders)
    fx = np.floor(x)
    pk.put(img, band & ((fx == band_x0) | (fx == w - 1 - band_x0)), "#1a0610")
    inner = band & (fy >= 6) & (fy <= 21)
    pk.put(img, inner & ((fx == band_x0 + 1) | (fx == w - 2 - band_x0)), CRIMSON["c"])
    stud(img, band_x0 + 4, 13)
    stud(img, w - 1 - band_x0 - 4, 13)
    return img, {"border": [20, 10, 20, 6], "imageType": "Sliced", "stretch": "horizontal",
                 "note": "stretch horizontally; keep native height (32 px -> 96 at 3x); text rows 6..21 "
                         "(centre a 16 px line on row 14)"}


def bar_bg():
    layers = [(INK, INK), (GOLD["md"], GOLD["xd"]), ("#05060c", "#05060c"), ("#0c0b18", "#221f3a")]
    img = frame(12, 12, layers, "#1a1830", chamfer=1)
    return img, {"border": [4, 4, 4, 4], "imageType": "Sliced", "fillInset": 3,
                 "note": "place Bar_Fill_* inset by fillInset px (3x = 9 canvas units) on each side"}


def bar_fill(color, highlight):
    """HP / boss bar fill: uniform along x (shading only down the rows: highlight, lit, body, shadow), so a Filled
    Horizontal image stretched to any Bar_Bg interior width keeps clean pixel columns. Segment ticks, if any, belong
    to Bar_Bg or code, not to the stretched fill."""
    w, h = 64, 8
    img = pk.canvas(w, h)
    base = np.array(pk.rgba(color)[:3], np.float32)
    hi = np.array(pk.rgba(highlight)[:3], np.float32)
    rows = [base + (255 - base) * 0.45, hi, hi * 0.5 + base * 0.5, base, base, base * 0.82, base * 0.68, base * 0.5]
    for yy, c in enumerate(rows):
        img[yy, :, :3] = np.clip(c, 0, 255).astype(np.uint8)
        img[yy, :, 3] = 255
    return img, {"border": [0, 0, 0, 0], "imageType": "Filled", "fillMethod": "Horizontal",
                 "note": "uniform along x: stretch to the Bar_Bg interior at any width; Filled Horizontal (origin Left) "
                         "-> fillAmount = hp/max"}


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
    img = frame(20, 16, layers, NAVY["d"], chamfer=3)
    # the rounded corner shapes rings up to chamfer + rings deep -> 6 px borders keep the edges uniform
    return img, {"border": [6, 6, 6, 6], "imageType": "Sliced",
                 "note": "small rounded tag (Fast-forward, P1/P2, BOSS, turn counter); interior 10 px tall at native size"}


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


def overlay_dim():
    """Flat night-navy dim for Pause / Reward / TrackingLost overlays (TDD D3: no background blur).
    72% alpha baked in so a plain white Image dims the world; fade it with Image.color.a (unscaled time)."""
    img = pk.canvas(16, 16, "#070a18")
    img[..., 3] = 184
    return img, {"border": [0, 0, 0, 0], "imageType": "Simple",
                 "note": "full-screen stretch (flat colour, point filter is fine); pair with Overlay_Vignette"}


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


def cue_stick(W, H, butt, tip):
    """Cue from butt to tip (tapered, ebony butt with gold rings, maple shaft, ivory ferrule, blue chalk tip) +
    its 2 px dark outline, as an RGBA layer."""
    a, b = np.array(butt, np.float64), np.array(tip, np.float64)
    length = np.linalg.norm(b - a)
    d = (b - a) / length
    n = np.array([-d[1], d[0]])
    x, y = pk.grid(W, H)
    rel = np.stack([x - a[0], y - a[1]], -1)
    along, across = rel @ d, rel @ n
    half = 2.6 + 2.8 * np.clip(1 - along / length, 0, 1)  # tapers toward the tip
    cue = (along >= 0) & (along <= length) & (np.abs(across) <= half)
    t = along / length
    layer = pk.canvas(W, H)
    zones = [(0.0, 0.30, ["#2a140c", "#4a2616", "#6a3a20"]), (0.30, 0.325, [GOLD["dk"], GOLD["lt"], GOLD["hi"]]),
             (0.325, 0.36, ["#2a140c", "#4a2616", "#6a3a20"]), (0.36, 0.375, [GOLD["dk"], GOLD["lt"], GOLD["hi"]]),
             (0.375, 0.93, ["#b07a3a", "#e0b070", "#f8dca0"]), (0.93, 0.965, ["#b8bcc8", "#e8eaf0", "#ffffff"]),
             (0.965, 1.001, ["#1c3c80", "#3a6ad0", "#7aa8ff"])]
    for z0, z1, (c_dk, c_md, c_lt) in zones:
        zone = cue & (t >= z0) & (t < z1)
        pk.put(layer, zone, c_md)
        pk.put(layer, zone & (across < -half * 0.35), c_lt)
        pk.put(layer, zone & (across > half * 0.45), c_dk)
    pk.put(layer, pk.dilate(cue, True, 2) & ~cue, "#0e1224")
    return layer


def logo():
    """Title logo: ivory BILLIARD over gold ROGUE whose O is the cue ball (paw emblem). The cue comes in from the
    lower left (behind the R, clear of BILLIARD) and its chalk tip just touches the ball, aimed at its centre, with
    an impact spark. The whole silhouette gets a 2 px dark stroke + a 2 px drop shadow so it holds on warm
    golden-hour, night and violet title backdrops."""
    W, H = 640, 200
    glyphs = bpf.parse(os.path.join(os.path.dirname(HERE), "Fonts", "glyphs_bold.txt"))
    img = pk.canvas(W, H)
    # --- words (placed first: the cue and ball are positioned from them)
    top_mask, _ = word_mask("BILLIARD", glyphs, spacing=2)
    bot_mask, slot = word_mask("ROGUE", glyphs, spacing=2, ball_slot=1)
    top_s = scale_mask(top_mask, 56)
    bot_s = scale_mask(bot_mask, 84)
    ivory = ["#ffffff", "#f4f6fb", "#e2e6f0", "#cdd2e2", "#b4bad0"]
    gold = ["#fff2b0", "#ffd65a", "#ffc23c", "#f09a24", "#d0741a"]
    top_img = style_letters(top_s, ivory, "#ffffff", "#8a92b0", "#0e1224", 5, ("#3a4470", "#232a4c"))
    bot_img = style_letters(bot_s, gold, "#fffbe6", "#8a4410", "#1a0c06", 7, ("#7a2e10", "#4a1a08"))
    tx, ty = (W - top_img.shape[1]) // 2 - 18, 2
    bx = (W - bot_img.shape[1]) // 2 + 14
    by = H - bot_img.shape[0] - 4
    pad = 4 + 7
    scale = bot_s.shape[0] / 10.0
    cx = bx + pad + (slot[0] + 5) * scale
    cy = by + pad + bot_s.shape[0] / 2
    r = bot_s.shape[0] / 2 + 2
    # --- cue: butt near the lower-left corner, axis through the ball centre, tip resting on the ball's rim
    butt = np.array([22.0, H - 16.0])
    axis = np.array([cx, cy]) - butt
    axis /= np.linalg.norm(axis)
    tip = np.array([cx, cy]) - axis * (r + 1.5)
    pk.blit(img, cue_stick(W, H, butt, tip), 0, 0)
    # --- letters over the cue
    pk.blit(img, top_img, tx, ty)
    pk.blit(img, bot_img, bx, by)
    # --- the "O" of ROGUE is a cue ball with the paw emblem
    ball_ramp = ["#6e7690", "#9aa2ba", "#c8cedc", "#e8ebf2", "#ffffff"]
    ball, inside = mi.sphere(W, cx, cy, r, ball_ramp, outline="#0e1224", height=H)
    shadow = pk.shift(inside, 3, 5) & ~inside  # extrusion toward bottom-right like the letters
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
    # --- impact spark where the tip meets the ball (4 px rays, white core, gold rays, dark rim)
    contact = np.array([cx, cy]) - axis * r
    sx, sy = int(round(contact[0])), int(round(contact[1]))
    spark = pk.canvas(W, H)
    rays = pk.pixels(W, H, [(sx + k * dx, sy + k * dy) for k in range(1, 5)
                            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))])
    diag = pk.pixels(W, H, [(sx + k * dx, sy + k * dy) for k in (1, 2, 3) for dx, dy in ((1, 1), (-1, -1), (1, -1), (-1, 1))])
    core = pk.pixels(W, H, [(sx, sy), (sx + 1, sy), (sx, sy + 1), (sx - 1, sy), (sx, sy - 1)])
    burst = rays | diag | core
    pk.put(spark, pk.dilate(burst, False) & ~burst, "#1a0c06")
    pk.put(spark, rays | diag, GOLD["lt"])
    pk.put(spark, core | pk.pixels(W, H, [(sx + 2 * dx, sy + 2 * dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]), "#ffffff")
    pk.blit(img, spark, 0, 0)
    # --- sparkles (kept off the cue and the letters)
    for spx, spy, sz in ((602, 34, 6), (588, 14, 3), (40, 30, 4), (566, 118, 3), (70, 112, 3)):
        star = pk.canvas(W, H)
        xx, yy = pk.grid(W, H)
        ax, ay = np.abs(xx - spx - 0.5), np.abs(yy - spy - 0.5)
        m = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(sz) + 0.2
        core = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(sz) * 0.55
        pk.put(star, pk.dilate(m, False) & ~m, "#0e1224")
        pk.put(star, m, GOLD["lt"])
        pk.put(star, core, "#ffffff")
        pk.blit(img, star, 0, 0)
    # --- whole-silhouette 2 px dark stroke + 2 px drop shadow (translucent, bottom-right)
    solid = pk.alpha(img)
    stroked = pk.dilate(solid, True, 2)
    out = pk.canvas(W, H)
    pk.put(out, pk.shift(stroked, 2, 2) & ~stroked, (4, 4, 10, 150))
    pk.put(out, stroked & ~solid, "#07080f")
    pk.blit(out, img, 0, 0)
    assert not (pk.alpha(out)[[0, -1]].any() or pk.alpha(out)[:, [0, -1]].any()), "logo touches the canvas border"
    return out, {"border": [0, 0, 0, 0], "imageType": "Simple",
                 "note": "title logo; place at 2x (1280x400) or 3x; 2 px stroke + translucent drop shadow baked in"}


# ----------------------------------------------------------------------------------------------
# main
# ----------------------------------------------------------------------------------------------

def validate_slices(name, img, info):
    """9-slice contract: edge strips are uniform along their stretch axis and a Sliced centre is flat, so Unity's
    Sliced/Tiled image types never smear or tear the pixel art. Returns a list of problems (empty = ok)."""
    L, B, R, T = info.get("border", [0, 0, 0, 0])
    if L + B + R + T == 0:
        return []
    a = img.astype(np.int32)
    H, W = a.shape[:2]
    horizontal_only = info.get("stretch") == "horizontal"
    problems = []
    top, bottom, centre = a[:T, L:W - R], a[H - B:, L:W - R], a[T:H - B, L:W - R]
    left, right = a[T:H - B, :L], a[T:H - B, W - R:]
    if T and (top != top[:, :1]).any():
        problems.append("top edge varies along x")
    if B and (bottom != bottom[:, :1]).any():
        problems.append("bottom edge varies along x")
    if horizontal_only:
        if (centre != centre[:, :1]).any():
            problems.append("centre varies along x")
        return problems
    if L and (left != left[:1]).any():
        problems.append("left edge varies along y")
    if R and (right != right[:1]).any():
        problems.append("right edge varies along y")
    if info.get("imageType") == "Sliced" and (centre != centre[:1, :1]).any():
        problems.append("Sliced centre is not flat")
    return problems


def nine_slice(spr, border, w, h, tiled=False):
    """Preview-side 9-slice (Sliced = nearest stretch of edges/centre, Tiled = repeat) at 1x."""
    L, B, R, T = border
    sh, sw = spr.shape[:2]

    def span(n_out, lead, src1, trail):
        mid = n_out - lead - trail
        width = src1 - lead
        if tiled:
            idx = [lead + (i % width) for i in range(mid)]
        else:
            idx = [lead + min(int(i * width / max(mid, 1)), width - 1) for i in range(mid)]
        return np.array(list(range(lead)) + idx + list(range(src1, src1 + trail)))
    return spr[span(h, T, sh - B, B)][:, span(w, L, sw - R, R)]


class TextStamp:
    """Draws 1-bit text with the built Billiard Pixel fonts at 16 px = one font pixel per UI art pixel (TMP size 48
    at 3x). Missing font files (fonts not built yet) turn text off instead of failing the sprite build."""

    def __init__(self):
        from PIL import ImageFont
        fonts = os.path.join(pk.STAGING_ASSETS, "Fonts", "BilliardRogue")
        self.fonts = {}
        for key, fname in (("regular", "BilliardPixel.ttf"), ("bold", "BilliardPixel-Bold.ttf")):
            path = os.path.join(fonts, fname)
            if os.path.exists(path):
                self.fonts[key] = ImageFont.truetype(path, 16)

    def width(self, text, style="bold"):
        return int(self.fonts[style].getlength(text)) if style in self.fonts else 0

    def draw(self, img, text, x, cap_top, color, style="bold", shadow=None, center=False):
        """cap_top: y of the first cap-height row (caps are 10 px tall, ascender 14)."""
        from PIL import Image, ImageDraw
        if style not in self.fonts:
            return
        font = self.fonts[style]
        w = int(font.getlength(text)) + 2
        if center:
            x -= (w - 2) // 2
        im = Image.new("L", (w, 20), 0)
        d = ImageDraw.Draw(im)
        d.fontmode = "1"
        d.text((0, 0), text, font=font, fill=255)
        m = np.asarray(im) > 0
        layer = pk.canvas(w + 1, 21)
        if shadow is not None:
            pk.put(layer, np.pad(m, ((1, 0), (1, 0)))[:21, :w + 1], shadow)
        pk.put(layer, np.pad(m, ((0, 1), (0, 1))), color)
        pk.blit(img, layer, int(x), int(cap_top) - 4)


def mockup(sprites):
    """3x mock-up of a reward overlay + HUD (1 font px = 1 art px) to judge kit + font together (preview only)."""
    W, H = 640, 360
    text = TextStamp()
    bg = pk.canvas(W, H, "#3a4a3a")
    x, y = pk.grid(W, H)
    bg[..., :3] = np.dstack([40 + 30 * (y / H), 52 + 36 * (y / H), 44 + 10 * (x / W)]).astype(np.uint8)

    def nine(name, w, h, tiled=None):
        spr, info = sprites[name]
        return nine_slice(spr, info["border"], w, h, info.get("imageType") == "Tiled" if tiled is None else tiled)

    img = bg.copy()
    dim = sprites["Overlay_Dim"][0]
    pk.blend(img, np.broadcast_to(dim[:1, :1], (H, W, 4)).copy())
    vig = sprites["Overlay_Vignette"][0]
    vy = (np.arange(H) * vig.shape[0] / H).astype(int)
    vx = (np.arange(W) * vig.shape[1] / W).astype(int)
    pk.blend(img, vig[vy][:, vx].copy())
    ink, cream, gold = "#1a0c06", "#fff4d6", "#ffd65a"
    # title banner
    pk.blit(img, nine("Frame_Banner", 300, 32), 170, 16)
    text.draw(img, "CHOOSE A REWARD", 320, 16 + 9, cream, "bold", shadow="#4a0a16", center=True)
    cards = [("FLAME BALL", ["Burns what", "it touches"]), ("HEAL", ["Restore 10 HP"]),
             ("MAX HP +5", ["Tougher knight,", "full heal"])]
    for i, (title, lines) in enumerate(cards):
        cx = 110 + i * 150
        pk.blit(img, nine("Frame_Card", 120, 176), cx, 60)
        pk.blit(img, pk.upscale(sprites["_icons"][i], 2), cx + 28, 80)
        text.draw(img, title, cx + 60, 152, ink, "bold", center=True)
        for k, ln in enumerate(lines):
            text.draw(img, ln, cx + 60, 172 + k * 16, "#5c3a1a", "regular", center=True)
    pk.blit(img, sprites["Arrow_Left"][0], 90, 140)   # "<" left of the cards, ">" right of them
    pk.blit(img, sprites["Arrow"][0], 534, 140)
    # button row on a wood panel
    pk.blit(img, nine("Frame_Panel", 236, 56), 202, 252)
    pk.blit(img, nine("Frame_ButtonFocused", 104, 32), 214, 264)
    pk.blit(img, nine("Frame_Button", 104, 32), 322, 264)
    text.draw(img, "CONTINUE", 266, 264 + 3 + 8, "#ffffff", "bold", shadow="#1a2650", center=True)
    text.draw(img, "REROLL", 374, 264 + 3 + 8, "#b8c4e8", "bold", shadow=NAVY["f"], center=True)
    cur = sprites["Cursor"][0]
    pk.blit(img, cur, 202 - cur.shape[1] - 2, 280 - cur.shape[0] // 2)  # outside the panel trim, on the focused row
    # HUD: hp bar + value, balls, turn chip, boss bar
    pk.blit(img, nine("Bar_Bg", 96, 14), 20, 10)
    fill = sprites["Bar_Fill_Hp"][0]
    fw = int((96 - 6) * 0.8)
    pk.blit(img, fill[(np.arange(8) * fill.shape[0] / 8).astype(int)][:, (np.arange(fw) * fill.shape[1] / fw).astype(int)], 23, 13)
    pk.blit(img, sprites["Icon_Heart"][0], 6, 9)
    text.draw(img, "24/30", 122, 12, cream, "bold", shadow=INK)
    pk.blit(img, sprites["Icon_Ball"][0], 6, 30)
    text.draw(img, "x6", 24, 33, cream, "bold", shadow=INK)
    pk.blit(img, sprites["Icon_Turn"][0], 540, 10)
    pk.blit(img, nine("Chip", 76, 16), 558, 10)
    text.draw(img, "TURN 3", 596, 13, cream, "regular", center=True)
    pk.blit(img, nine("Bar_Bg", 200, 14), 220, 332)
    boss = sprites["Bar_Fill_Boss"][0]
    bw = int(194 * 0.55)
    pk.blit(img, boss[(np.arange(8) * boss.shape[0] / 8).astype(int)][:, (np.arange(bw) * boss.shape[1] / bw).astype(int)], 223, 335)
    pk.blit(img, sprites["Icon_Skull"][0], 202, 331)
    text.draw(img, "KING SLIME", 320, 318, cream, "bold", shadow=INK, center=True)
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
        "Bar_Fill_Hp": bar_fill("#d8283a", "#ff7a3a"),
        "Bar_Fill_Boss": bar_fill("#6a2ab0", "#b060f0"),
        "Chip": chip(),
        "Icon_Heart": (icon_heart(), {}),
        "Icon_Ball": (icon_ball(), {}),
        "Icon_Skull": (icon_skull(), {}),
        "Icon_Turn": (icon_turn(), {}),
        "Arrow": (icon_arrow(), {"note": "right chevron; Arrow_Left is the mirrored copy"}),
        "Cursor": (icon_cursor(), {"note": "paw pointer, toes point right; bob it horizontally"}),
        "Overlay_Dim": overlay_dim(),
        "Overlay_Vignette": overlay_vignette(),
        "Logo_BilliardRogue": logo(),
    }
    sprites["Arrow_Left"] = (pk.flip_h(sprites["Arrow"][0]), {"note": "left chevron"})
    meta, failures = {}, []
    for name, (img, info) in sprites.items():
        h, w = img.shape[:2]
        assert w % 4 == 0 and h % 4 == 0, (name, w, h)
        failures += [f"{name}: {p}" for p in validate_slices(name, img, info)]
        pk.save_rgba(pk.staging(*OUT_DIR, name + ".png"), img)
        L, B, R, T = info.get("border", [0, 0, 0, 0])
        meta[name] = {"file": name + ".png", "size": [w, h],
                      "border": {"left": L, "bottom": B, "right": R, "top": T},
                      "imageType": info.get("imageType", "Simple"), "filter": info.get("filter", "Point"),
                      "note": info.get("note", "")}
        for k in ("stretch", "glowMargin", "fillInset", "fillMethod"):
            if k in info:
                meta[name][k] = info[k]
    if failures:
        sys.exit("9-slice contract broken: " + "; ".join(failures))
    pk.save_json(pk.staging(*OUT_DIR, "ui_slices.json"), {
        "note": "Generated by Tools/Textures/make_ui.py. Borders in sprite pixels (Unity Sprite.border order is "
                "left, bottom, right, top). Show UI pixel art at an integer 3x (PPU = 100/3 at CanvasScaler "
                "1920x1080 scale 1); text in the same grid = BilliardPixel at TMP size 48.",
        "pixelScale": PIXEL_SCALE, "pixelsPerUnit": round(100.0 / PIXEL_SCALE, 4),
        "sprites": [dict(name=k, **v) for k, v in meta.items()]})
    order = [k for k in sprites if k not in ("Logo_BilliardRogue", "Overlay_Vignette", "Overlay_Dim")]
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
