"""Pixel-art gameplay icons: ball types, status effects, enemy telegraphs, reward cards.

Run:  Tools/.venv/bin/python Tools/Textures/make_icons.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/Icons/
        Ball_<Type>.png 32x32 (Basic Flame Frost Thunder Bomb Splitter Piercer Iron Venom Vampire Rubber Lucky)
        Status_<Burn|Poison|Freeze>.png 16x16, Telegraph_<Spawn|Cast|Heal|Quake>.png 16x16,
        Reward_<Heal|MaxHp>.png 32x32, icons.json (suggested colour/glowColour per ball for BallDefinition).
Style: cel-shaded pixel spheres (5 bands, light from the top-left, coloured outline, glossy highlight) with
a readable emblem; every icon has a 1 px dark outline so it reads on any background. Hard alpha only.
Deterministic.
"""
import argparse
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paintkit  # noqa: E402
import pixelkit as pk  # noqa: E402

OUT_DIR = ("Sprites", "BilliardRogue", "Icons")


def darker(c, k=0.5):
    r, g, b = pk.rgba(c)[:3]
    return (int(r * k), int(g * k), int(b * k))


# ----------------------------------------------------------------------------------------------
# sphere
# ----------------------------------------------------------------------------------------------

def sphere(size, cx, cy, r, ramp, outline=None, highlight=True, rim=True, height=None):
    """ramp: 5 colours dark -> light. Returns (image, inside-mask). Canvas is size x (height or size)."""
    height = height or size
    img = pk.canvas(size, height)
    ndl, inside, (dx, dy, nz) = pk.sphere_shading(size, height, cx, cy, r)
    bands = np.digitize(ndl, [0.22, 0.45, 0.7, 0.9])
    if rim:  # reflected light on the bottom-right rim lifts the darkest band
        edge = inside & ((dx * dx + dy * dy) > (1 - 1.6 / r) ** 2) & ((dx + dy) > 0.55)
        bands = np.where(edge & (bands == 0), 1, bands)
    for i, c in enumerate(ramp):
        pk.put(img, inside & (bands == i), c)
    if highlight:
        hx, hy = cx - r * 0.42, cy - r * 0.46
        pk.put(img, pk.ellipse(size, height, hx, hy, r * 0.2, r * 0.13, -40) & inside, lighten(ramp[-1]))
        pk.put(img, pk.disc(size, height, hx - r * 0.02, hy - r * 0.02, max(r * 0.07, 0.8)) & inside, "#ffffff")
    ring = pk.dilate(inside, False) & ~inside
    pk.put(img, ring, outline or darker(ramp[0], 0.45))
    return img, inside


def lighten(c, k=0.55):
    r, g, b = pk.rgba(c)[:3]
    return (int(r + (255 - r) * k), int(g + (255 - g) * k), int(b + (255 - b) * k))


def emblem(img, mask, fill, outline, x0, y0, shade=None, extra=None):
    """Stamp a bool mask (with auto 1px outline) at (x0, y0). extra: {colour: mask} painted on top."""
    h, w = mask.shape
    layer = pk.canvas(w + 2, h + 2)
    m = np.zeros((h + 2, w + 2), bool)
    m[1:-1, 1:-1] = mask
    pk.put(layer, m, fill)
    if shade is not None:  # bottom-right shade inside the emblem
        pk.put(layer, m & ~pk.shift(m, -1, -1), shade)
    if extra:
        for col, em in extra.items():
            mm = np.zeros_like(m)
            mm[1:-1, 1:-1] = em
            pk.put(layer, mm, col)
    ring = pk.dilate(m, False) & ~m
    pk.put(layer, ring, outline)
    pk.blit(img, layer, x0 - 1, y0 - 1)
    return img


def centered(img, mask, dx=0, dy=0):
    h, w = mask.shape
    H, W = img.shape[:2]
    return (W - w) // 2 + dx, (H - h) // 2 + dy


def art(rows, char="#"):
    return pk.ascii_mask(rows, char)


# ----------------------------------------------------------------------------------------------
# emblem bitmaps
# ----------------------------------------------------------------------------------------------

PAW = ["....##..##....",
       "...####.####..",
       "...####.####..",
       "....##...##...",
       ".##........##.",
       "####..##..####",
       "####.####.####",
       ".##.######.##.",
       "...########...",
       "..##########..",
       "..##########..",
       "...###..###..."]
FLAME = ["......#.....",
         ".....##.....",
         ".....###..#.",
         "....####..#.",
         ".#..#####.##",
         ".##.#####.##",
         ".##########.",
         "############",
         "#####ww#####",
         "####wwww####",
         "####wwwww###",
         ".###wwwww##.",
         "..##wwww##..",
         "....####...."]
SNOWFLAKE_L = ["......#......",
               "....#.#.#....",
               ".....###.....",
               "..#...#...#..",
               "...##.#.##...",
               "....#####....",
               "#############",
               "....#####....",
               "...##.#.##...",
               "..#...#...#..",
               ".....###.....",
               "....#.#.#....",
               "......#......"]
SNOWFLAKE = [".....#.....",
             "...#.#.#...",
             "....###....",
             ".#...#...#.",
             "..##.#.##..",
             "###########",
             "..##.#.##..",
             ".#...#...#.",
             "....###....",
             "...#.#.#...",
             ".....#....."]
BOLT = ["....######",
        "...######.",
        "..######..",
        ".######...",
        "#########.",
        "..######..",
        "...####...",
        "..####....",
        "..###.....",
        ".###......",
        ".##.......",
        "##........"]
DROP = ["....#....",
        "....#....",
        "...###...",
        "...###...",
        "..#####..",
        ".#######.",
        ".#######.",
        "#########",
        "#########",
        "#########",
        ".#######.",
        "..#####.."]
DROP_HL = [".........",
           ".........",
           ".........",
           ".........",
           ".........",
           ".........",
           "..#......",
           ".##......",
           ".#.......",
           ".........",
           ".........",
           "........."]
FANGS_MOUTH = ["#..............#",
               "##............##",
               ".##############.",
               "..############..",
               "....########....",
               "................",
               "................"]
FANGS_TEETH = ["................",
               "................",
               "................",
               "..##........##..",
               "..###......###..",
               "...##......##...",
               "...#........#..."]
PLUS = ["...##...",
        "...##...",
        "...##...",
        "########",
        "########",
        "...##...",
        "...##...",
        "...##..."]


def clover_mask(n=17):
    x, y = pk.grid(n, n)
    c = n / 2
    m = np.zeros((n, n), bool)
    for ang in (45, 135, 225, 315):
        a = math.radians(ang)
        for side in (-1, 1):  # heart-shaped leaf = two lobes + a point toward the centre
            b = a + side * 0.4
            lx, ly = c + math.cos(b) * 4.6, c + math.sin(b) * 4.6
            m |= (x - lx) ** 2 + (y - ly) ** 2 <= 2.25 ** 2
        tip = [(c + math.cos(a) * 1.0, c + math.sin(a) * 1.0),
               (c + math.cos(a + 0.75) * 4.4, c + math.sin(a + 0.75) * 4.4),
               (c + math.cos(a - 0.75) * 4.4, c + math.sin(a - 0.75) * 4.4)]
        m |= pk.poly(n, n, tip)
    gap = (np.abs(x - c) < 0.6) | (np.abs(y - c) < 0.6)
    m &= ~(gap & ((x - c) ** 2 + (y - c) ** 2 > 1.5 ** 2))
    return m


def clover_veins(n=17):
    x, y = pk.grid(n, n)
    c = n / 2
    d1 = np.abs((x - c) - (y - c)) < 0.55
    d2 = np.abs((x - c) + (y - c)) < 0.55
    r = np.hypot(x - c, y - c)
    return (d1 | d2) & (r > 1.6) & (r < 4.6)


BUBBLE_L = [".###.", "#w..#", "#...#", "#...#", ".###."]
BUBBLE_S = [".#.", "#.#", ".#."]


def stamp_bubble(img, rows, x0, y0, fill, outline):
    ring = art(rows, "#")
    hl = art(rows, "w")
    layer = pk.canvas(len(rows[0]) + 2, len(rows) + 2)
    m = np.zeros(layer.shape[:2], bool)
    m[1:-1, 1:-1] = ring
    pk.put(layer, m, fill)
    h = np.zeros_like(m)
    h[1:-1, 1:-1] = hl
    pk.put(layer, h, "#ffffff")
    solid = pk.dilate(m | h, False)
    pk.put(layer, solid & ~m & ~h & ~(np.pad(pk.erode(np.ones((len(rows), len(rows[0])), bool) & ~ring & ~hl, False), 1)), outline)
    pk.blit(img, layer, x0 - 1, y0 - 1)


# ----------------------------------------------------------------------------------------------
# balls
# ----------------------------------------------------------------------------------------------

S = 32
CX = CY = 16.0
R = 14.0

BALLS = {
    #          ramp (dark -> light)                                            colour     glow
    "Basic": (["#7a8298", "#a8aec0", "#d2d6e0", "#eceef3", "#ffffff"], "#f2f4f8", "#e8f0ff"),
    "Flame": (["#7a1a10", "#b8321a", "#e8601c", "#ff9a2e", "#ffd060"], "#ff6a1c", "#ffa030"),
    "Frost": (["#23508e", "#3f82c8", "#72b8ee", "#a8e0ff", "#e4f8ff"], "#78c4f4", "#a8ecff"),
    "Thunder": (["#94640a", "#d49c06", "#f4cc1c", "#fff06a", "#fffcc4"], "#ffd81c", "#fff27a"),
    "Bomb": (["#0c0c16", "#1c1e2e", "#30344c", "#4c5276", "#8088b4"], "#2c3048", "#ff7a30"),
    "Splitter": (["#0c544a", "#168674", "#2cbc9c", "#6ce6c4", "#c0fff0"], "#2cc8a4", "#70ffd8"),
    "Piercer": (["#20346a", "#3656a4", "#5682d4", "#88b2f0", "#d2e6ff"], "#5a8ae0", "#a0c8ff"),
    "Iron": (["#26262c", "#44444e", "#696b78", "#9698a6", "#cfd1da"], "#7a7c88", "#c8ccd8"),
    "Venom": (["#34104a", "#5a1c7c", "#8632ae", "#ae5cd8", "#dca4ff"], "#9a40c8", "#b8ff50"),
    "Vampire": (["#34040e", "#660a1a", "#9c1226", "#cc2a3a", "#ff6e7e"], "#b01a2e", "#ff4060"),
    "Rubber": (["#741248", "#b42674", "#e44a9c", "#ff86c6", "#ffd0ec"], "#f050a8", "#ff90d0"),
    "Lucky": (["#0c461a", "#18762a", "#2ca43e", "#5ccc5a", "#b0f08c"], "#34b448", "#ffe060"),
}


def ball_basic(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = art(PAW)
    x0, y0 = centered(img, m, 0, 1)
    emblem(img, m, "#6f86b6", "#3a4a78", x0, y0, shade="#5a6ea0")
    return img


def ball_flame(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = art(FLAME, "#w")
    core = art(FLAME, "w")
    x0, y0 = centered(img, m, 0, 0)
    emblem(img, m, "#ffd84a", "#5a0c08", x0, y0, extra={"#fff8d0": core})
    return img


def ball_frost(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = art(SNOWFLAKE_L)
    x0, y0 = centered(img, m, 0, 0)
    emblem(img, m, "#ffffff", "#163c78", x0, y0, extra={"#bfe8ff": pk.pixels(13, 13, [(6, 6)])})
    return img


def ball_thunder(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = art(BOLT)
    x0, y0 = centered(img, m, 0, 0)
    emblem(img, m, "#ffffff", "#6a3c00", x0, y0, shade="#fff4b0")
    return img


def ball_bomb(ramp):
    r = 12.0
    cx, cy = 15.0, 18.0
    img, _ = sphere(S, cx, cy, r, ramp, outline="#05050a")
    # fuse cap
    cap = pk.poly(S, S, [(17.5, 7.5), (21.5, 3.5), (26.5, 8.5), (22.5, 12.5)])
    pk.put(img, cap, "#4c5276")
    pk.put(img, cap & pk.shift(cap, 1, 1) & ~pk.shift(cap, -1, -1), "#8088b4")
    pk.put(img, pk.dilate(cap, False) & ~cap & ~pk.alpha(img), "#05050a")
    # fuse cord
    cord = pk.line(S, S, [(24, 5), (26, 3), (28, 3)], 1)
    pk.put(img, cord, "#c8a060")
    # spark
    for px_, py_, col in ((29, 2, "#ffffff"), (30, 2, "#ffd040"), (28, 2, "#ffd040"), (29, 1, "#ffd040"), (29, 3, "#ffd040"),
                          (31, 1, "#ff7a20"), (27, 1, "#ff7a20"), (31, 3, "#ff7a20"), (30, 0, "#ff7a20")):
        img[py_, px_] = pk.rgba(col)
    return img


def ball_splitter(ramp):
    base, inside = sphere(S, CX, CY, R, ramp, highlight=True)
    # cut the ball along a zig-zag and push the halves apart (reads as "splits in two")
    x, y = pk.grid(S, S)
    zig = CX + 1.6 * (np.abs(((y - 2) / 8.0) % 1.0 - 0.5) * 4 - 1)
    left = x < zig
    img = pk.canvas(S, S)
    lhalf, rhalf = base.copy(), base.copy()
    lhalf[~left] = 0
    rhalf[left] = 0
    lmask, rmask = inside & left, inside & ~left
    pk.blit(img, lhalf, -2, 1)
    pk.blit(img, rhalf, 2, -1)
    # clean seam: outline both halves along the cut
    lm, rm = pk.shift(lmask, -2, 1), pk.shift(rmask, 2, -1)
    seam = (pk.dilate(lm, False) & ~lm & ~rm) | (pk.dilate(rm, False) & ~rm & ~lm)
    pk.put(img, seam & ~(lm | rm), darker(ramp[0], 0.45))
    img[~(lm | rm | seam)] = 0
    # two white arrows pointing apart
    for mm, x0, y0 in ((art(["..#", ".##", "###", ".##", "..#"]), 6, 14), (art(["#..", "##.", "###", "##.", "#.."]), 23, 12)):
        emblem(img, mm, "#ffffff", "#064038", x0, y0)
    return img


def ball_piercer(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    # spear shaft from bottom-left through the ball, head leaving the top-right edge
    shaft = pk.line(S, S, [(4, 27), (21, 10)], 2)
    head = pk.poly(S, S, [(18.5, 8.5), (29.5, 1.5), (23.5, 13.5)])
    tail = pk.poly(S, S, [(1.5, 26.5), (5.5, 26.5), (5.5, 30.5)]) | pk.poly(S, S, [(3.5, 24.5), (7.5, 24.5), (7.5, 28.5)])
    spear = shaft | head | tail
    layer = pk.canvas(S, S)
    pk.put(layer, spear, "#ffffff")
    pk.put(layer, head & ~pk.shift(head, 1, 1), "#d2e6ff")
    pk.put(layer, tail, "#ffd060")
    ring = pk.dilate(spear, False) & ~spear
    pk.put(layer, ring, "#101c40")
    pk.blit(img, layer, 0, 0)
    return img


def ball_iron(ramp):
    img, inside = sphere(S, CX, CY, R, ramp)
    x, y = pk.grid(S, S)
    # riveted equator band following the sphere curvature
    band = inside & (np.abs(y - (CY + 1)) <= 2.5)
    pk.put(img, band, "#4a4c58")
    pk.put(img, band & (np.abs(y - (CY - 1.5)) < 0.5), "#8a8c9a")
    pk.put(img, band & (np.abs(y - (CY + 3.5)) < 0.5), "#2a2a32")
    for rx in (5, 10, 16, 22, 27):
        yy = int(CY + 1)
        if inside[yy, rx]:
            img[yy, rx] = pk.rgba("#dfe1ea")
            img[yy, rx + 1] = pk.rgba("#9698a6") if inside[yy, rx + 1] else img[yy, rx + 1]
            img[yy + 1, rx] = pk.rgba("#2a2a32")
    # vertical plate seam + rivets on the upper cap
    seam = inside & (np.abs(x - (CX + 5.5)) < 0.5) & (y < CY - 1.5) & (y > 4)
    pk.put(img, seam, "#44444e")
    for ry in (7, 11):
        img[ry, int(CX + 3)] = pk.rgba("#dfe1ea")
        img[ry, int(CX + 8)] = pk.rgba("#dfe1ea")
    # re-apply the glossy highlight on top (metal)
    hx, hy = CX - R * 0.42, CY - R * 0.46
    pk.put(img, pk.ellipse(S, S, hx, hy, R * 0.22, R * 0.12, -40) & inside, "#f4f6fb")
    return img


def ball_venom(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = art(DROP)
    x0, y0 = centered(img, m, -1, 1)
    emblem(img, m, "#a8f040", "#1c3208", x0, y0, shade="#6cc020", extra={"#eaffb0": art(DROP_HL)})
    stamp_bubble(img, BUBBLE_L, 19, 6, "#d0ff80", "#1c3208")
    stamp_bubble(img, BUBBLE_S, 23, 13, "#d0ff80", "#1c3208")
    return img


def ball_vampire(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    mouth = art(FANGS_MOUTH)
    teeth = art(FANGS_TEETH)
    x0, y0 = centered(img, mouth, 0, 3)
    layer = pk.canvas(S, S)
    mm = np.zeros((S, S), bool)
    tm = np.zeros((S, S), bool)
    mm[y0:y0 + mouth.shape[0], x0:x0 + mouth.shape[1]] = mouth
    tm[y0:y0 + teeth.shape[0], x0:x0 + teeth.shape[1]] = teeth
    pk.put(layer, mm, "#1a0206")
    pk.put(layer, tm, "#ffffff")
    pk.put(layer, tm & ~pk.shift(tm, 1, 0) & ~pk.shift(tm, 0, -1) & pk.shift(tm, 0, 1), "#ffe4e8")
    ring = pk.dilate(tm, False) & ~tm & ~mm
    pk.put(layer, ring, "#1a0206")
    # eyes: two glowing slits above the grin
    for ex in (10, 20):
        pk.put(layer, pk.rect(S, S, ex, 12, ex + 3, 14), "#ffe060")
        pk.put(layer, pk.rect(S, S, ex - 1, 11, ex + 4, 15) & ~pk.rect(S, S, ex, 12, ex + 3, 14), "#1a0206")
    pk.blit(img, layer, 0, 0)
    return img


def ball_rubber(ramp):
    img, inside = sphere(S, CX, CY, R, ramp)
    x, y = pk.grid(S, S)
    tri = np.abs(((x - 1) / 6.0) % 1.0 - 0.5) * 2  # triangle wave 0..1, period 6 px
    zig = CY + 0.5 + (tri - 0.5) * 4.0
    band = inside & (np.abs(y - zig) <= 1.6)
    pk.put(img, band, "#ffffff")
    pk.put(img, band & (y > zig + 0.6), "#ffd0ec")
    edge = inside & ~band & (pk.dilate(band, False))
    pk.put(img, edge, darker(ramp[1], 0.7))
    # second thin stripe above
    zig2 = CY - 6.0 + (tri - 0.5) * 3.0
    band2 = inside & (np.abs(y - zig2) <= 0.6) & (y > 4)
    pk.put(img, band2, "#ffe070")
    hx, hy = CX - R * 0.42, CY - R * 0.46
    pk.put(img, pk.disc(S, S, hx, hy, 1.4) & inside, "#ffffff")
    return img


def ball_lucky(ramp):
    img, _ = sphere(S, CX, CY, R, ramp)
    m = clover_mask(17)
    stem = pk.line(17, 17, [(9, 9), (13, 14), (14, 16)], 1) & ~m
    x0, y0 = centered(img, m, 0, 0)
    emblem(img, m | stem, "#f4fff0", "#0a3814", x0, y0, shade="#b8ecb0", extra={"#6cc070": clover_veins(17) | stem})
    # sparkle
    for px_, py_, col in ((23, 6, "#ffffff"), (22, 6, "#fff4b0"), (24, 6, "#fff4b0"), (23, 5, "#fff4b0"), (23, 7, "#fff4b0")):
        img[py_, px_] = pk.rgba(col)
    return img


BALL_FUNCS = {"Basic": ball_basic, "Flame": ball_flame, "Frost": ball_frost, "Thunder": ball_thunder,
              "Bomb": ball_bomb, "Splitter": ball_splitter, "Piercer": ball_piercer, "Iron": ball_iron,
              "Venom": ball_venom, "Vampire": ball_vampire, "Rubber": ball_rubber, "Lucky": ball_lucky}


# ----------------------------------------------------------------------------------------------
# small icons (16x16)
# ----------------------------------------------------------------------------------------------

FLAME_S = ["....#.....",
           "....##....",
           "...###.#..",
           "..####.#..",
           ".#######..",
           ".########.",
           "###yy#####",
           "###yyy####",
           "##yyyyy###",
           "##yyyyy###",
           ".##yyy###.",
           "..######.."]
SLIME = ["....####....",
         "..########..",
         ".##########.",
         ".#e#####e##.",
         "##e#####e###",
         "############",
         "############",
         ".##########."]
QUAKE = ["#.........#",
         "##.......##",
         ".##.....##.",
         "..##...##..",
         "...##.##...",
         "....###....",
         "#....#....#",
         "##.......##",
         ".##.....##.",
         "..##...##..",
         "...##.##...",
         "....###....",
         ".....#....."]


def small_canvas():
    return pk.canvas(16, 16)


def status_burn():
    img = small_canvas()
    m = art(FLAME_S, "#y")
    core = art(FLAME_S, "y")
    x0, y0 = centered(img, m, 0, 1)
    emblem(img, m, "#ff7a1c", "#3a0a04", x0, y0, extra={"#ffe060": core, "#ffb030": core & ~pk.shift(core, 0, 1)})
    return img


def status_poison():
    img = small_canvas()
    m = art(DROP)
    x0, y0 = centered(img, m, -1, 1)
    emblem(img, m, "#a44ae0", "#1e0632", x0, y0, shade="#7a2cb0", extra={"#f0c8ff": art(DROP_HL)})
    stamp_bubble(img, BUBBLE_L, 10, 1, "#d8a0ff", "#1e0632")
    return img


def status_freeze():
    img = small_canvas()
    m = art(SNOWFLAKE)
    x0, y0 = centered(img, m, 0, 0)
    emblem(img, m, "#d8f6ff", "#10306a", x0, y0, extra={"#ffffff": pk.pixels(11, 11, [(5, 5), (5, 0), (0, 5), (10, 5), (5, 10)])})
    return img


# Telegraph hues are unique per threat at 16 px over an enemy: Spawn white + gold, Cast magenta, Heal green (the
# only green one), Quake orange. Status hues: Burn red-orange, Poison violet, Freeze ice blue.
def telegraph_spawn():
    """A new enemy is coming: pale ghost-white slime silhouette + gold up-arrow (no '+', no green: that is Heal)."""
    img = small_canvas()
    m = art(SLIME, "#e")
    eyes = art(SLIME, "e")
    x0, y0 = centered(img, m, -1, 3)
    body = m & ~eyes
    emblem(img, m, "#eeeaf6", "#1a1a2e", x0, y0, shade="#b4aecc",
           extra={"#ffffff": body & pk.inner_edge(body, "top"), "#1a1a2e": eyes})
    arrow = art(["..#..", ".###.", "#####", ".###.", ".###."])
    emblem(img, arrow, "#ffd23a", "#3a2406", 10, 1, extra={"#fff4b0": art(["..#..", ".#...", ".....", ".....", "....."])})
    return img


def telegraph_cast():
    img = small_canvas()
    x, y = pk.grid(16, 16)
    ax, ay = np.abs(x - 8), np.abs(y - 8)
    star = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(7.0) + 0.2
    core = np.sqrt(ax) + np.sqrt(ay) <= math.sqrt(3.2)
    layer = pk.canvas(16, 16)
    pk.put(layer, star, "#ff4ec4")  # hot magenta: clearly apart from Poison's violet drop
    pk.put(layer, core, "#ffffff")
    pk.put(layer, star & ~core & (x + y < 16) & (np.minimum(ax, ay) < 1.1), "#ffc8ec")
    pk.put(layer, star & ~core & (x + y > 17) & (np.minimum(ax, ay) >= 1.1), "#d0289a")
    ring = pk.dilate(star, False) & ~star
    pk.put(layer, ring, "#3a0426")
    pk.blit(img, layer, 0, 0)
    for px_, py_ in ((2, 3), (13, 12)):
        pk.put(img, pk.pixels(16, 16, [(px_, py_)]), "#ffffff")
        pk.put(img, pk.pixels(16, 16, [(px_ - 1, py_), (px_ + 1, py_), (px_, py_ - 1), (px_, py_ + 1)]) & ~pk.alpha(img), "#ff4ec4")
    return img


def telegraph_heal():
    img = small_canvas()
    m = art(["...####...",
             "...####...",
             "...####...",
             "##########",
             "##########",
             "##########",
             "##########",
             "...####...",
             "...####...",
             "...####..."])
    x0, y0 = centered(img, m, 0, 0)
    hl = art(["...##.....", "...#......", "..........", "##........", "#.........", "..........", "..........", "..........", "..........", ".........."])
    emblem(img, m, "#48d860", "#0a3414", x0, y0, shade="#28a040", extra={"#c8ffc8": hl})
    img[1, 14] = pk.rgba("#ffffff")
    img[0, 14] = img[2, 14] = img[1, 13] = img[1, 15] = pk.rgba("#48d860")
    return img


def telegraph_quake():
    img = small_canvas()
    m = art(QUAKE)
    x0, y0 = centered(img, m, 0, -1)
    emblem(img, m, "#ff9a3a", "#3a1604", x0, y0, shade="#d0601a")
    # ground line with a crack at the bottom
    ground = pk.rect(16, 16, 1, 15, 15, 16)
    pk.put(img, ground & ~pk.alpha(img), "#8a5a30")
    img[15, 7] = img[15, 8] = pk.rgba("#3a1604")
    return img


# ----------------------------------------------------------------------------------------------
# rewards (32x32)
# ----------------------------------------------------------------------------------------------

def heart_mask(n, cx, cy, s):
    x, y = pk.grid(n, n)
    u, v = (x - cx) / s, (y - cy) / s
    lobes = ((u + 0.5) ** 2 + (v + 0.25) ** 2 <= 0.30) | ((u - 0.5) ** 2 + (v + 0.25) ** 2 <= 0.30)
    tri = (v >= -0.25) & (np.abs(u) <= 1.02 - (v + 0.25) * 1.02) & (v <= 0.78)
    return lobes | tri


def shade_mask(img, m, ramp):
    """5-band top-left shading of an arbitrary blob."""
    dist = pk.torus_edt(np.pad(m, 1))[1:-1, 1:-1]
    x, y = pk.grid(m.shape[1], m.shape[0])
    ys, xs = np.nonzero(m)
    cx, cy = xs.mean() + 0.5, ys.mean() + 0.5
    rr = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2 + 0.5
    lam = (-(x - cx) - (y - cy)) / (rr * 1.6) + np.clip(dist / 4.0, 0, 1) * 0.55
    bands = np.digitize(lam, [0.05, 0.3, 0.6, 0.85])
    for i, c in enumerate(ramp):
        pk.put(img, m & (bands == i), c)


def reward_heal():
    img = pk.canvas(32, 32)
    x, y = pk.grid(32, 32)
    bulb = pk.disc(32, 32, 16, 20, 10.2)
    neck = pk.rect(32, 32, 13, 6, 19, 12)
    lip = pk.rect(32, 32, 12, 6, 20, 8)
    cork = pk.rect(32, 32, 13, 2, 19, 6)
    glass = bulb | neck | lip
    liquid = bulb & (y > 15.5 + 0.6 * np.sin((x - 8) / 3.0))
    pk.put(img, glass, "#cfe8f4")
    pk.put(img, glass & ~liquid & (x > 17), "#9ec4dc")
    shade_mask(img, liquid, ["#6a0a18", "#a01428", "#d42a3a", "#ff5a64", "#ff9a9e"])
    surface = liquid & ~pk.shift(liquid, 0, 1)
    pk.put(img, surface, "#ff8a92")
    pk.put(img, lip, "#e8f6ff")
    shade_mask(img, cork, ["#4a2a12", "#6e4220", "#94602e", "#b8844a", "#d8a868"])
    # heart label on the liquid
    hm = heart_mask(32, 16, 21.5, 4.6)
    pk.put(img, pk.dilate(hm, False) & ~hm, "#5a0612")
    pk.put(img, hm, "#ffffff")
    pk.put(img, hm & ~pk.shift(hm, -1, -1), "#ffd0d6")
    # glass highlight arc
    arc = pk.disc(32, 32, 16, 20, 8.2) & ~pk.disc(32, 32, 17.2, 21.2, 8.2) & (x < 14) & (y < 22)
    pk.put(img, arc, "#ffffff")
    shape = glass | cork
    pk.put(img, pk.dilate(shape, False) & ~shape, "#1a0c14")
    return img


def reward_maxhp():
    img = pk.canvas(32, 32)
    hm = heart_mask(32, 16, 15, 13.0)
    rim_inner = pk.erode(hm, False, 2)
    inner = pk.erode(hm, False, 3)
    shade_mask(img, hm, ["#8a4a08", "#c07a10", "#eab02a", "#ffd860", "#fff2b0"])  # gold rim
    pk.put(img, rim_inner & ~inner, "#5a1a08")
    shade_mask(img, inner, ["#6a0a18", "#a81628", "#dc2c3c", "#ff6070", "#ffa4aa"])
    pk.put(img, pk.dilate(hm, False) & ~hm, "#1a0a06")
    # white plus in the centre
    p = art(PLUS)
    x0, y0 = centered(img, p, 0, -1)
    emblem(img, p, "#ffffff", "#4a0610", x0, y0, shade="#ffd8dc")
    # up arrow badge top-right
    arrow = art(["..#..", ".###.", "#####", ".###.", ".###."])
    emblem(img, arrow, "#80ff80", "#0a3010", 25, 1)
    return img


SMALL = {"Status_Burn": status_burn, "Status_Poison": status_poison, "Status_Freeze": status_freeze,
         "Telegraph_Spawn": telegraph_spawn, "Telegraph_Cast": telegraph_cast, "Telegraph_Heal": telegraph_heal,
         "Telegraph_Quake": telegraph_quake}
REWARDS = {"Reward_Heal": reward_heal, "Reward_MaxHp": reward_maxhp}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    balls, small, rewards, meta = [], [], [], {"balls": {}}
    for name, (ramp, color, glow) in BALLS.items():
        img = paintkit.override(OUT_DIR, f"Ball_{name}", BALL_FUNCS[name](ramp))
        assert img.shape == (32, 32, 4)
        pk.save_rgba(pk.staging(*OUT_DIR, f"Ball_{name}.png"), img)
        meta["balls"][name] = {"file": f"Ball_{name}.png", "color": color, "glowColor": glow}
        balls.append((name, img))
    for name, fn in SMALL.items():
        img = paintkit.override(OUT_DIR, name, fn())
        assert img.shape == (16, 16, 4)
        pk.save_rgba(pk.staging(*OUT_DIR, name + ".png"), img)
        small.append((name.split("_")[1], img))
    for name, fn in REWARDS.items():
        img = paintkit.override(OUT_DIR, name, fn())
        pk.save_rgba(pk.staging(*OUT_DIR, name + ".png"), img)
        rewards.append((name, img))
    meta["note"] = "Generated by Tools/Textures/make_icons.py. colour/glowColour are suggestions for BallDefinition."
    pk.save_json(pk.staging(*OUT_DIR, "icons.json"), meta)
    pk.save_rgb(os.path.join(preview_dir, "icons_balls.png"), pk.contact_sheet(balls, scale=4, cols=6))
    pk.save_rgb(os.path.join(preview_dir, "icons_small.png"), pk.contact_sheet(small + rewards, scale=4, cols=9))
    # in-context check: native 1x and 3x on dark and light UI backgrounds
    strip = np.concatenate([b for _, b in balls], axis=1)
    ctx = [("1x on navy", pk.on_background(strip, bg=(26, 32, 56))), ("1x on parchment", pk.on_background(strip, bg=(236, 220, 176)))]
    small_strip = np.concatenate([np.pad(i, ((2, 2), (2, 2), (0, 0))) for _, i in small], axis=1)
    for label, bg in (("stone", (150, 142, 128)), ("grass", (86, 150, 60)), ("crypt", (58, 56, 92)), ("crystal", (84, 60, 140))):
        ctx.append((f"status + telegraph 1x on {label}", pk.on_background(small_strip, bg=bg)))
    pk.save_rgb(os.path.join(preview_dir, "icons_context.png"), pk.contact_sheet(ctx, scale=3, cols=1, checker_bg=False))
    print("ICONS", {"balls": len(balls), "small": len(small), "rewards": len(rewards)})


if __name__ == "__main__":
    main()
