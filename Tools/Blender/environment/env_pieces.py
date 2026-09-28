"""Billiard Rogue environment kit: arena pieces + three act diorama sets (Blender 5.2, bmesh, Unity-space authoring).

Conventions (see env_lib): 1 unit = 1 m, pivot at the bottom centre unless noted, pieces face Unity +Z.
Arena kit exceptions (documented in layouts.json "kit"):
  * Floor pieces (Env_FloorTile*, Env_LaunchPad, Env_GroundTile): pivot at the centre of the TOP face (top = y 0).
  * Env_WallSegment: pivot at the bottom of the inner (arena-facing) face, centred along its 1 m length; inner face
    looks along local +Z; body extends to local -Z (0.4 m); y 0 = arena floor, body continues down to y -0.3.
  * Env_WallTorch / Env_Banner: pivot at the wall mounting point; the prop sticks out along local +Z.
  * Env_LightShaft: pivot at the TOP of the god-ray volume (hangs along -Y); unit UVs for BilliardRogue/LightShaft.
  * Env_CaveFloorTile / Env_GroundTile: 4 x 4 m ground tiles, pivot at the tile centre on the ground plane.
Part names ending in `_Surface` carry metre-scale box UVs (M_Surface_* materials); everything else palette UVs.
Emissive parts carry an emissive keyword in their name (Flame, Crystal, *_Emissive) and only emissive-half UVs.
Beyond the brief's list the kit adds dressing for density / height layering: water pools (Act 1/2, glowing Act 3),
stairs, paving patches, log, stump, toadstools, urn, crypt pillar and the cave floor tile.
"""
import math

import bmesh
from mathutils import Vector

from env_lib import Part, Rng, hash01, xform as X

WALL_T = 0.40       # rim wall thickness
WALL_BODY_TOP = 0.42
WALL_TOP = 0.52     # top of the coping
WALL_BOTTOM = -0.30  # walls sink below the ground (ground is at y -0.2)


# ================================================================ small shared helpers
def rect(hx, hz, y, cx=0.0, cz=0.0):
    return [(cx - hx, y, cz - hz), (cx + hx, y, cz - hz), (cx + hx, y, cz + hz), (cx - hx, y, cz + hz)]


def quad_up(part, x0, x1, z0, z1, y=0.0):
    return part.raw([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], [(0, 1, 2, 3)], outward=(0, 1, 0))


def sweep_x(part, prof, x0, x1, closed=False):
    """Extrude an (z, y) polyline along x from x0 to x1 (quads only, ends open). Faces point away from the
    profile centroid."""
    cz = sum(p[0] for p in prof) / len(prof)
    cy = sum(p[1] for p in prof) / len(prof)
    verts, faces = [], []
    for z, y in prof:
        verts += [(x0, y, z), (x1, y, z)]
    n = len(prof)
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        faces.append((2 * i, 2 * i + 1, 2 * j + 1, 2 * j))
    return part.raw(verts, faces, outward=lambda c: (0.0, c[1] - cy, c[2] - cz))


def drop_flat(part, faces, up=True, down=True):
    """Delete faces of `faces` that point straight up / down (hidden caps)."""
    ids = set(faces)
    part.delete_faces(lambda f: f in ids and ((up and f.normal.z > 0.99) or (down and f.normal.z < -0.99)))


def clamp_ground(part, faces, ymin=0.0, keep=0.12):
    """Push vertices below ymin up to the ground plane (flat bottoms for blobs)."""
    for v in part._verts_of([f for f in faces if f.is_valid]):
        if v.co.z < ymin:
            v.co.z = ymin + (v.co.z - ymin) * keep


def by_height(bands):
    """paint_fn helper: bands = [(y_max, family, shade), ...] ascending; last entry catches the rest."""
    def fn(f, n, c):
        for y_max, fam, s in bands:
            if c[1] <= y_max:
                return fam, s
        return bands[-1][1], bands[-1][2]
    return fn


def patch01(c, scale, seed):
    """Spatially coherent hash: faces within the same ~1/scale m cell share a value (patches, not confetti)."""
    return hash01(math.floor(c[0] * scale), math.floor(c[1] * scale), math.floor(c[2] * scale), seed)


def stone_fn(fam, base, seed=0.0, moss=None, moss_thresh=0.55, jitter=True, low=None, alt=None, moss_cover=0.6,
             patch=2.2):
    """Chunky stone faces: top +1, bottom -2, per-face jitter +-1; moss=(family, shade) on a `moss_cover` share of
    the up-facing faces (coherent patches); low=(y, delta) darkens faces below y; alt=(family, shade, share)
    variegates the stone in coherent patches of ~1/patch metres."""
    def fn(f, n, c):
        r = hash01(c[0], c[1], c[2], seed)
        r2 = patch01(c, patch, seed + 17.0)
        fm, s = fam, base
        if alt is not None and r2 < alt[2]:
            fm, s = alt[0], alt[1]
        if n[1] > 0.7:
            s += 1
        elif n[1] < -0.5:
            s -= 2
        if jitter:
            s += -1 if r < 0.25 else (1 if r > 0.8 else 0)
        if low is not None and c[1] < low[0]:
            s += low[1]
        if moss is not None and n[1] > moss_thresh and r2 > 1.0 - moss_cover:
            return moss[0], moss[1] + (1 if r > 0.7 else 0) + (1 if n[1] > 0.9 else 0)
        return fm, s
    return fn


def moss_clump(part, pos, r, seed, squash=0.38, drape=0.0):
    """Lumpy moss cushion sitting on a ledge (optionally draping over one side)."""
    f = part.ico(1, r, X(pos, yaw=seed * 47, scale=(1.0, squash, 1.0)), jitter=0.25, seed=900 + seed)
    part.paint_fn(f, lambda f_, n, c: ("lime", 8 if n[1] > 0.6 else 6) if hash01(*c) > 0.3 else ("green", 7 if n[1] > 0.5 else 5))
    return f


def foliage_fn(top="lime", side="lime", under="green", base=8, seed=0.0, accents=None):
    """Leafy blob shading: bright warm tops, mid sides, dark cool undersides, random leaf-cluster facets.
    accents = (family, shade, probability) on upper faces (flowers / berries)."""
    def fn(f, n, c):
        r = hash01(c[0], c[1], c[2], seed)
        if accents is not None and n[1] > 0.2 and r > 1.0 - accents[2]:
            return accents[0], accents[1]
        if n[1] > 0.6:
            return top, base + 3 + (1 if r > 0.8 else 0) - (1 if r < 0.18 else 0)
        if n[1] > 0.15:
            return top, base + 1 + (1 if r > 0.82 else 0) - (1 if r < 0.2 else 0)
        if n[1] > -0.35:
            return side, base - 1 + (1 if r > 0.8 else 0) - (1 if r < 0.15 else 0)
        return under, base - 4
    return fn


def flame_fn(fams=(("yellow", 15), ("yellow", 14), ("orange", 15), ("orange", 13), ("orange", 11)), h=0.4, y0=0.0):
    """Emissive flame gradient by height: hot core at the base, deeper colour at the tip."""
    def fn(f, n, c):
        t = (c[1] - y0) / max(h, 1e-6)
        i = min(len(fams) - 1, max(0, int(t * len(fams))))
        return fams[i]
    return fn


# ================================================================ arena kit
def floor_tile(danger=False):
    """1 x 1 x 0.2 m arena cell (pivot = centre of the top face). The cell seam is modelled: a 3.5 cm palette bevel on
    every edge, so neighbouring cells always read as separate tiles whatever surface the act binds to Top_Surface.
    Top_Surface box UVs start on the cell corner (u, v 0..1 per cell): with M_Surface_* at _Tiling 0.5 every cell
    samples texels 1..31 of the 64 px tile, i.e. exactly one panel of a one-panel-per-cell surface (CryptFloor).
    danger=True: the danger-row cell. A carved groove (DangerFrame, dark red palette, never emissive) frames the cell
    and a 3.5 cm line inside it (DangerInlay_Emissive, M_DangerTile) is the only part that glows when the row is
    occupied, so the idle row reads as part of the floor instead of a lit UI frame."""
    base, top = Part("Base"), Part("Top_Surface")
    c, h = 0.035, 0.2
    f = base.quads([rect(0.5 - c, 0.5 - c, 0.0), rect(0.5, 0.5, -c), rect(0.5, 0.5, -h)], close_ends=False)
    base.paint_fn(f, lambda f_, n, c_: ("gray", 4 if n[1] > 0.3 else 3))
    parts = [base, top]
    if not danger:
        quad_up(top, -0.5 + c, 0.5 - c, -0.5 + c, 0.5 - c)
        return parts
    inner, outer, gd = 0.395, 0.5 - c, 0.014
    quad_up(top, -inner, inner, -inner, inner)
    frame = Part("DangerFrame")
    vi0, vi1, vo0, vo1 = rect(inner, inner, 0.0), rect(inner, inner, -gd), rect(outer, outer, 0.0), rect(outer, outer, -gd)
    ring = [(i, (i + 1) % 4, 4 + (i + 1) % 4, 4 + i) for i in range(4)]
    fw = frame.raw(vi0 + vi1, ring, outward=lambda c_: (c_[0], 0.0, c_[2]))            # inner wall faces the groove
    fw += frame.raw(vo1 + vo0, ring, outward=lambda c_: (-c_[0], 0.0, -c_[2]))         # outer wall faces the groove
    frame.paint(fw, "red", 3, top=0, bottom=0)
    li, lo = 0.41, 0.445
    fg = frame.raw(rect(inner, inner, -gd) + rect(li, li, -gd), ring, outward=(0, 1, 0))
    fg += frame.raw(rect(lo, lo, -gd) + rect(outer, outer, -gd), ring, outward=(0, 1, 0))
    frame.paint(fg, "red", 4, top=0, bottom=0)
    inlay = Part("DangerInlay_Emissive")
    fs = inlay.raw(rect(li, li, -gd) + rect(lo, lo, -gd), ring, outward=(0, 1, 0))
    inlay.paint(fs, "red", 8, emissive=True, top=0)
    parts += [frame, inlay]
    return parts


def launch_pad():
    """7 x 1.6 m launch platform (pivot = centre of the top face; place at world (0, 0, 0.8)). A carved groove with
    a brass rail marks the launch line (world z 0.55); ivory diamond sights mark the 7 column centres."""
    W, D, c, h = 7.0, 1.6, 0.035, 0.2
    hx, hz, ix, iz = W / 2, D / 2, W / 2 - c, D / 2 - c
    lz = 0.55 - D / 2          # launch line in local z
    gz0, gz1, gx, gd = lz - 0.075, lz + 0.075, 3.28, 0.07
    top = Part("Top_Surface", uv_offset=(0.5, 0.0, 0.2))   # UV integers on world x -3.5 + k, z 1.6 - k: cell corners
    for x0, x1, z0, z1 in ((-ix, ix, -iz, gz0), (-ix, ix, gz1, iz), (-ix, -gx, gz0, gz1), (gx, ix, gz0, gz1)):
        quad_up(top, x0, x1, z0, z1)

    base = Part("Base")
    f = base.quads([rect(ix, iz, 0.0), rect(hx, hz, -c), rect(hx, hz, -h)], close_ends=False)
    base.paint_fn(f, lambda f_, n, c_: ("gray", 5 if n[1] > 0.3 else 4))
    # groove: inner walls face the groove centre, floor faces up
    vt, vb = rect(gx, (gz1 - gz0) / 2, 0.0, 0.0, lz), rect(gx, (gz1 - gz0) / 2, -gd, 0.0, lz)
    g = base.raw(vt + vb, [(i, (i + 1) % 4, 4 + (i + 1) % 4, 4 + i) for i in range(4)],
                 outward=lambda c_: (-c_[0] if abs(c_[0]) > gx - 0.01 else 0.0, 0.0, lz - c_[2]))
    g += quad_up(base, -gx, gx, gz0, gz1, -gd)
    base.paint(g, "gray", 2, top=0)
    # front face stone band: slightly lighter lip at the exit line
    lip = base.box((W, 0.05, 0.03), X((0, -0.06, -hz - 0.012)))
    base.paint(lip, "gray", 6, top=1, bottom=-2)

    rail = Part("Rail")
    fr = rail.box((2 * gx - 0.06, 0.032, 0.05), X((0, -gd + 0.016, lz)))
    for sx in (-1, 1):   # end blocks where the cat's track stops
        fr += rail.box((0.07, 0.05, 0.11), X((sx * (gx - 0.035), -gd + 0.025, lz)))
    rail.paint(fr, "yellow", 11, top=2, bottom=-3)

    trim = Part("Trim")
    ft = []
    for col in range(7):
        ft += trim.lathe([(0.075, 0.0), (0.05, 0.014)], 4, X((col - 3.0, 0.0, -hz + 0.2)), squash=(1.0, 1.35),
                         cap_bot=False)
    trim.paint(ft, "gray", 13, top=1)
    return [base, top, rail, trim]


def wall_segment():
    side, top, trim = Part("Side_Surface"), Part("Top_Surface"), Part("Trim")
    T, o, ch = WALL_T, 0.04, 0.035
    body = side.box((1.0, WALL_BODY_TOP - WALL_BOTTOM, T), X((0, (WALL_BODY_TOP + WALL_BOTTOM) / 2, -T / 2)))
    drop_flat(side, body)
    # coping: inner/outer faces + chamfers are palette trim, the flat top is the tiling surface
    y0, y1 = WALL_BODY_TOP, WALL_TOP
    zf, zb = o, -T - o
    fc = sweep_x(trim, [(zf, y0), (zf, y1 - ch), (zf - ch, y1)], -0.5, 0.5)
    fc += sweep_x(trim, [(zb + ch, y1), (zb, y1 - ch), (zb, y0)], -0.5, 0.5)
    fc += trim.raw([(-0.5, y0, zb), (0.5, y0, zb), (0.5, y0, zf), (-0.5, y0, zf)], [(0, 1, 2, 3)], outward=(0, -1, 0))
    trim.paint_fn(fc, lambda f, n, c: ("gray", 9 if n[1] > 0.3 else (4 if n[1] < -0.5 else 7)))
    quad_up(top, -0.5, 0.5, zb + ch, zf - ch, y1)
    # skirting at the inner foot (reads as the floor/wall contact line)
    sk = sweep_x(trim, [(0.0, 0.0), (0.03, 0.0), (0.03, 0.07), (0.0, 0.1)], -0.5, 0.5)
    trim.paint(sk, "gray", 5, top=1)
    # billiard-rail "diamond" sight on top of every 1 m segment (brass)
    dm = trim.lathe([(0.065, 0.0), (0.045, 0.016)], 4, X((0, y1, -T / 2)), squash=(1.5, 1.0), cap_bot=False)
    trim.paint(dm, "yellow", 12, top=2)
    return [side, top, trim]


def wall_corner():
    """Corner / end post (0.44 m square, pivot bottom centre at arena floor level, body continues to y -0.3)."""
    side, trim = Part("Side_Surface"), Part("Trim")
    b = side.box((0.44, 0.62 - WALL_BOTTOM, 0.44), X((0, (0.62 + WALL_BOTTOM) / 2, 0)))
    drop_flat(side, b)
    f = trim.box((0.54, 0.1, 0.54), X((0, 0.67, 0)), bevel=0.025)
    f += trim.box((0.48, 0.05, 0.48), X((0, 0.595, 0)))
    trim.paint(f, "gray", 8, top=1, bottom=-3)
    p = trim.lathe([(0.2, 0.72), (0.0, 0.9)], 4, rot_offset=45.0, cap_bot=False)
    trim.paint(p, "gray", 9, top=1, jitter=0)
    ball = trim.ico(1, 0.075, X((0, 0.96, 0)))
    ball += trim.lathe([(0.05, 0.88), (0.035, 0.91)], 6)
    trim.paint(ball, "yellow", 12, top=2, bottom=-3)
    return [side, trim]


def _torch(flame_cols, rim_family="yellow"):
    sconce, flame = Part("Sconce"), Part("Flame", pivot=(0.0, 0.25, 0.25))
    f = sconce.box((0.17, 0.3, 0.035), X((0, 0.0, 0.0175)), bevel=0.012)
    sconce.paint(f, "gray", 4, top=1)
    f = sconce.box((0.055, 0.055, 0.24), X((0, -0.07, 0.14)))
    f += sconce.box((0.05, 0.2, 0.05), X((0, 0.03, 0.25)))
    f += sconce.box((0.04, 0.04, 0.2), X((0, -0.005, 0.12), lean_f=-40))
    sconce.paint(f, "gray", 3, top=1)
    cup = sconce.lathe([(0.05, 0.12), (0.12, 0.2), (0.13, 0.25), (0.105, 0.25)], 7, X((0, 0, 0.25)))
    sconce.paint_fn(cup, lambda f_, n, c: (rim_family, 10) if c[1] > 0.235 else ("gray", 3 if n[1] < 0 else 4))
    fl = flame.lathe([(0.075, 0.0), (0.11, 0.07), (0.1, 0.15), (0.06, 0.26), (0.0, 0.42)], 6,
                     X((0, 0.25, 0.25), lean_f=4), jitter=0.12, seed=3, rot_offset=15)
    fl += flame.lathe([(0.04, 0.0), (0.05, 0.05), (0.0, 0.22)], 5, X((0.045, 0.3, 0.2), lean_r=18, lean_f=-10))
    flame.paint_fn(fl, flame_fn(flame_cols, h=0.45, y0=0.25))
    return [sconce, flame]


def wall_torch():
    return _torch((("yellow", 15), ("yellow", 14), ("orange", 15), ("orange", 14), ("orange", 12)))


def wall_torch_arcane():
    """Act 3 variant: cold magic flame (cyan core, violet tip)."""
    return _torch((("cyan", 15), ("cyan", 14), ("cyan", 13), ("sky", 12), ("purple", 12)), rim_family="purple")


# ================================================================ act 1: mossy ruins
MOSS = ("green", 6)


def tree_a():
    """Round oak, ~4 m: faceted trunk with root flares and two limbs carrying three separate canopy clumps (main
    crown, a low left clump, a high right-back clump) so the silhouette has notches instead of one lollipop blob.
    Clump bottoms are flattened: bright lime tops, mid sides, a dark green underside band for the 4-band shading.
    Canopy pivot at the crown base (sway)."""
    trunk, canopy = Part("Trunk"), Part("Canopy", pivot=(0.0, 1.9, 0.0))
    t = trunk.lathe([(0.44, 0.0), (0.3, 0.22), (0.24, 0.9), (0.21, 1.6), (0.16, 2.4)], 6, X(lean_f=-3, lean_r=4),
                    jitter=0.1, seed=11, cap_bot=False)
    for i, a in enumerate((20, 140, 260)):
        t += trunk.box((0.16, 0.2, 0.42), X((0.24 * math.sin(math.radians(a)), 0.07, 0.24 * math.cos(math.radians(a))),
                                            yaw=a, lean_f=0), taper=(0.6, 0.45), skip_bottom=True)
    for pos, yaw, lean, L in (((-0.42, 1.95, 0.1), -80, -48, 1.0), ((0.36, 2.2, -0.12), 115, -42, 0.95)):
        t += trunk.box((0.12, 0.12, L), X(pos, yaw=yaw, lean_f=lean), taper=(0.55, 0.55))
    trunk.paint_fn(t, lambda f, n, c: ("brown", 4 + min(3, int(c[1] * 1.4)) + (1 if hash01(*c) > 0.75 else 0)))
    clumps = [((0.05, 3.0, 0.0), 1.08, (1.12, 0.78, 1.05)), ((-1.2, 2.3, 0.25), 0.74, (1.05, 0.74, 1.0)),
              ((1.05, 2.72, -0.3), 0.7, (1.05, 0.76, 1.0))]
    c = []
    for i, (p, r, sc) in enumerate(clumps):
        c += canopy.ico(2, r, X(p, yaw=i * 37, scale=sc), jitter=0.12, seed=100 + i, flatten_below=-r * 0.45)
    canopy.paint_fn(c, foliage_fn("lime", "lime", "green", base=8, seed=1.0))
    return [trunk, canopy]


def tree_b():
    """Tall pine, ~4.4 m: three separated tiers with drooping skirts (dark undersides, lit upper faces) stacked on a
    visible trunk, so it reads as a layered conifer rather than one cone (Canopy pivot at the first tier)."""
    trunk, canopy = Part("Trunk"), Part("Canopy", pivot=(0.0, 0.9, 0.0))
    t = trunk.lathe([(0.34, 0.0), (0.22, 0.25), (0.17, 1.2), (0.1, 3.6)], 6, jitter=0.08, seed=5, cap_bot=False)
    trunk.paint_fn(t, lambda f, n, c: ("brown", 4 + (1 if c[1] > 0.4 else 0) + (1 if hash01(*c) > 0.7 else 0)))
    tiers = ((0.95, 1.5, 1.2), (2.0, 1.12, 1.05), (2.9, 0.76, 1.4))
    c = []
    for i, (y0, r, h) in enumerate(tiers):
        c += canopy.lathe([(0.0, y0 + 0.34), (r, y0 - 0.08), (r * 0.72, y0 + h * 0.22), (r * 0.42, y0 + h * 0.5),
                           (0.0, y0 + h)], 9, X(lean_r=(-2, 2, -3)[i]), rot_offset=i * 17, jitter=0.1, seed=20 + i)

    def fn(f, n, cc):
        # seen from the high game camera only the upper faces show: a dark skirt band on every tier's rim outlines
        # the tiers, lighter needles above it
        r = hash01(cc[0], cc[1], cc[2], 7.0)
        if n[1] < -0.2:
            return "green", 2
        tier = max(i for i, t in enumerate(tiers) if cc[1] >= t[0] - 0.1 or i == 0)
        y0, _, h = tiers[tier]
        if (cc[1] - y0) / h < 0.2:
            return "green", 4 + (1 if r > 0.6 else 0)
        s = 7 + (2 if n[1] > 0.55 else 0) + (1 if r > 0.78 else 0) - (1 if r < 0.2 else 0)
        return ("teal" if r < 0.12 else "green"), s
    canopy.paint_fn(c, fn)
    return [trunk, canopy]


def bush():
    p = Part("Bush")
    f = p.ico(2, 0.58, X((0.0, 0.55, 0.0), scale=(1.1, 0.95, 1.05)), jitter=0.13, seed=31)
    f += p.ico(2, 0.42, X((-0.55, 0.38, 0.14), scale=(1.0, 0.95, 1.0)), jitter=0.14, seed=32)
    f += p.ico(2, 0.44, X((0.52, 0.36, -0.14), scale=(1.0, 0.92, 1.0)), jitter=0.14, seed=33)
    f += p.ico(2, 0.3, X((0.12, 0.92, 0.1), scale=(1.0, 0.9, 1.0)), jitter=0.14, seed=34)
    clamp_ground(p, f, 0.02)
    p.paint_fn(f, foliage_fn("lime", "green", "green", base=7, seed=2.0))
    return [p]


def _blade(part, base, h, lean, yaw, w=0.075, seed=0):
    """Chunky 3-sided grass blade: base ring, mid ring, tip (9 tris)."""
    a = math.radians(yaw)
    ox, oz = math.sin(a), math.cos(a)            # lean direction
    tx, tz = oz, -ox                             # tangent (blade width)
    lx = math.sin(math.radians(lean))
    rings = []
    for t, width in ((0.0, w), (0.55, w * 0.62)):
        cx = base[0] + ox * h * t * lx * 1.2
        cz = base[2] + oz * h * t * lx * 1.2
        cy = base[1] + h * t * math.cos(math.radians(lean * 0.6))
        rings.append([(cx + tx * width, cy, cz + tz * width), (cx - tx * width, cy, cz - tz * width),
                      (cx + ox * width * 0.55, cy, cz + oz * width * 0.55)])
    tip = (base[0] + ox * h * lx * 1.5, base[1] + h * math.cos(math.radians(lean)), base[2] + oz * h * lx * 1.5)
    rings.append([tip])
    return part.quads(rings, close_ends=False)


def grass_tuft():
    p = Part("Grass")
    rng = Rng(41)
    f = []
    for i in range(13):
        a = i * 137.5 + rng.uniform(-15, 15)
        d = 0.04 + 0.17 * math.sqrt((i + 0.5) / 13)
        base = (math.sin(math.radians(a)) * d, 0.0, math.cos(math.radians(a)) * d)
        f += _blade(p, base, rng.uniform(0.28, 0.5), rng.uniform(12, 34), a + rng.uniform(-20, 20))
    p.paint_fn(f, lambda f_, n, c: ("lime", 6 if c[1] < 0.12 else (9 if c[1] < 0.26 else 11))
               if hash01(*c) > 0.2 else ("green", 7 if c[1] < 0.2 else 10))
    return [p]


def flowers():
    stems, heads = Part("Stems"), Part("Petals")
    rng = Rng(51)
    cols = (("pink", 13), ("skin", 15), ("yellow", 14), ("pink", 12), ("skin", 15), ("yellow", 13), ("sky", 13))
    fs, fh = [], []
    for i, (fam, sh) in enumerate(cols):
        a = i * 137.5 + 20
        d = 0.06 + 0.26 * math.sqrt((i + 0.5) / len(cols))
        x, z = math.sin(math.radians(a)) * d, math.cos(math.radians(a)) * d
        h = rng.uniform(0.22, 0.42)
        lean = rng.uniform(4, 14)
        fs += _blade(stems, (x, 0.0, z), h + 0.02, lean, a, w=0.028)
        lx = math.sin(math.radians(lean)) * 1.5 * h
        hx, hz, hy = x + math.sin(math.radians(a)) * lx, z + math.cos(math.radians(a)) * lx, h * 0.98
        m = X((hx, hy, hz), yaw=a, lean_f=-18)
        petals = heads.lathe([(0.035, 0.0), (0.115, 0.03), (0.0, 0.045)], 5, m, rot_offset=i * 23)
        heads.paint(petals, fam, sh, top=0, bottom=-3)
        centre = heads.lathe([(0.04, 0.038), (0.0, 0.07)], 5, m, cap_bot=False)
        heads.paint(centre, "orange", 13 if fam != "yellow" else 11, top=0)
    for i in range(4):
        a = 45 + i * 90 + rng.uniform(-20, 20)
        fs += stems.box((0.09, 0.02, 0.26), X((math.sin(math.radians(a)) * 0.12, 0.03, math.cos(math.radians(a)) * 0.12),
                                              yaw=a, lean_f=-70), taper=(0.3, 1.0))
    stems.paint_fn(fs, lambda f, n, c: ("green", 7 if c[1] < 0.15 else 9))
    return [stems, heads]


def _drum(part, y0, y1, r, seed, flutes=16, jag=None):
    """Fluted column drum (alternating radii = flutes). jag=depth -> broken top."""
    rng = Rng(seed)
    rot = rng.uniform(0, 360)
    off = (rng.uniform(-0.025, 0.025), 0.0, rng.uniform(-0.025, 0.025))

    def ring(y, rr):
        pts = []
        for i in range(flutes):
            a = math.radians(rot) + 2 * math.pi * i / flutes
            k = rr * (1.0 if i % 2 == 0 else 0.88)
            pts.append((off[0] + math.cos(a) * k, y, off[2] + math.sin(a) * k))
        return pts
    bot = ring(y0, r)
    if jag is None:
        return part.quads([bot, ring(y1, r * 0.97)], close_ends=True)
    top = [(x, y1 - jag * rng.next() ** 0.7, z) for x, _, z in ring(y1, r * 0.97)]
    # the broken top is a concave fan: force it to face up (the centroid heuristic would flip it)
    return part.quads([bot, top, [(off[0], y1 - jag * 1.1, off[2])]], close_ends=True,
                      expect=lambda band, c: Vector((0, 0, 1)) if band == 1 else None)


def paint_blocks(part, blocks, seed, moss_cover=0.4, bare=()):
    """Masonry: every block gets its own tone (warm sandstone or cool gray), moss patches on top faces
    (except blocks listed in `bare`, e.g. column drums whose caps would read as green stripes)."""
    for i, faces in enumerate(blocks):
        r = hash01(i * 1.7, seed, 3.3)
        fam, base = (("skin", 9) if r < 0.22 else ("gray", 10 if r < 0.65 else 9))
        part.paint_fn(faces, stone_fn(fam, base, seed + i, moss=None if i in bare else MOSS, moss_thresh=0.5,
                                      low=(0.35, -1), moss_cover=moss_cover, patch=3.0))


def ruin_column(broken=False):
    p = Part("Column")
    blocks = [p.box((0.84, 0.24, 0.84), X((0, 0.12, 0)), bevel=0.03)]
    blocks.append(p.lathe([(0.41, 0.24), (0.41, 0.31), (0.33, 0.41)], 8, rot_offset=22.5, cap_bot=False, cap_top=False))
    if not broken:
        blocks.append(_drum(p, 0.41, 1.06, 0.29, 1))
        blocks.append(_drum(p, 1.08, 1.73, 0.285, 2))
        blocks.append(_drum(p, 1.75, 2.38, 0.275, 3))
        blocks.append(p.lathe([(0.27, 2.38), (0.37, 2.5)], 8, rot_offset=22.5, cap_bot=False, cap_top=False))
        blocks.append(p.box((0.86, 0.22, 0.86), X((0, 2.61, 0), yaw=4), bevel=0.03))
        paint_blocks(p, blocks, 3.0, moss_cover=0.3, bare=(2, 3, 4, 5))
        moss_clump(p, (0.12, 2.74, 0.1), 0.34, 1)
        moss_clump(p, (-0.3, 2.7, -0.22), 0.2, 2)
        moss_clump(p, (0.36, 0.25, -0.3), 0.22, 3)
        return [p]
    blocks.append(_drum(p, 0.41, 1.06, 0.29, 4))
    blocks.append(_drum(p, 1.08, 1.62, 0.285, 5, jag=0.32))
    # fallen drum + rubble
    fallen = _drum(p, -0.3, 0.32, 0.28, 6)
    bmesh.ops.transform(p.bm, matrix=X((0.95, 0.25, -0.35), yaw=62, lean_f=90), verts=p._verts_of(fallen))
    blocks.append(fallen)
    for i, (pos, r) in enumerate((((-0.6, 0.08, -0.35), 0.16), ((0.45, 0.06, 0.55), 0.12), ((-0.2, 0.05, 0.6), 0.1))):
        blocks.append(p.ico(1, r, X(pos, yaw=i * 50, scale=(1.2, 0.8, 1.0)), jitter=0.2, seed=60 + i))
    paint_blocks(p, blocks, 4.0, moss_cover=0.45, bare=(2,))
    moss_clump(p, (0.05, 1.42, 0.0), 0.24, 4)
    moss_clump(p, (-0.35, 0.25, 0.32), 0.2, 5)
    return [p]


def ruin_arch():
    """Freestanding ruined arch, 3.2 m wide x 3.35 m tall x 0.7 m deep (walk-through along Z)."""
    p = Part("Arch")
    blocks = []
    rng = Rng(71)
    for sx in (-1, 1):
        y = 0.0
        for i, h in enumerate((0.78, 0.7, 0.7)):
            blocks.append(p.box((0.72, h - 0.02, 0.72), X((sx * 1.2 + rng.uniform(-0.03, 0.03), y + h / 2,
                                                             rng.uniform(-0.03, 0.03)), yaw=rng.uniform(-4, 4)),
                                bevel=0.035))
            y += h
        blocks.append(p.box((0.9, 0.2, 0.84), X((sx * 1.22, y + 0.1, 0)), bevel=0.03))
    y0 = 2.18 + 0.2
    rin, rout, n = 0.86, 1.56, 7
    for i in range(n):
        a0 = math.pi - math.pi * i / n - 0.012
        a1 = math.pi - math.pi * (i + 1) / n + 0.012
        ro = rout + (0.14 if i == n // 2 else 0.0)
        pts = [(math.cos(a0) * rin, y0 + math.sin(a0) * rin), (math.cos(a0) * ro, y0 + math.sin(a0) * ro),
               (math.cos(a1) * ro, y0 + math.sin(a1) * ro), (math.cos(a1) * rin, y0 + math.sin(a1) * rin)]
        depth = 0.78 if i == n // 2 else 0.68
        m = X((0.0, -0.05, 0.0), lean_r=-2.5) if i == n - 1 else X()
        blocks.append(p.poly_prism(pts, depth, m))
    paint_blocks(p, blocks, 5.0)
    moss_clump(p, (-0.55, 3.62, 0.05), 0.3, 6)
    moss_clump(p, (0.2, 4.1, -0.05), 0.26, 7)
    moss_clump(p, (1.25, 2.52, 0.12), 0.26, 8)
    moss_clump(p, (-1.2, 0.2, 0.3), 0.3, 9, squash=0.5)
    return [p]


def mossy_rock(variant):
    p = Part("Rock")
    f = []
    if variant == "A":
        f += p.ico(2, 0.72, X((0.0, 0.3, 0.0), yaw=20, scale=(1.15, 0.72, 0.92)), jitter=0.16, seed=81)
        f += p.ico(2, 0.42, X((0.7, 0.14, -0.35), yaw=70, scale=(1.0, 0.8, 1.0)), jitter=0.18, seed=82)
    else:
        f += p.ico(2, 0.44, X((0.0, 0.16, 0.0), yaw=10, scale=(1.2, 0.7, 1.0)), jitter=0.16, seed=83)
        f += p.ico(2, 0.3, X((0.48, 0.1, 0.2), yaw=50, scale=(1.0, 0.75, 1.1)), jitter=0.18, seed=84)
        f += p.ico(2, 0.22, X((-0.35, 0.07, 0.36), yaw=80, scale=(1.1, 0.7, 1.0)), jitter=0.2, seed=85)
    clamp_ground(p, f, -0.04)
    p.paint_fn(f, stone_fn("gray", 8, 6.0, moss=MOSS, moss_thresh=0.5, alt=("skin", 8, 0.2), moss_cover=0.55))
    return [p]


def stone_lantern():
    """Japanese-style toro, 1.45 m; 'Flame' glows inside the open fire box."""
    p, flame = Part("Lantern"), Part("Flame", pivot=(0.0, 0.93, 0.0))
    f = p.lathe([(0.36, 0.0), (0.33, 0.1), (0.24, 0.16)], 6, cap_bot=False)
    f += p.lathe([(0.13, 0.16), (0.11, 0.66)], 6, cap_bot=False, cap_top=False)
    f += p.lathe([(0.2, 0.62), (0.32, 0.7), (0.32, 0.76)], 6, cap_bot=True)
    for sx in (-1, 1):
        for sz in (-1, 1):
            f += p.box((0.06, 0.34, 0.06), X((sx * 0.15, 0.93, sz * 0.15)))
    f += p.lathe([(0.24, 1.09), (0.42, 1.13), (0.41, 1.18), (0.15, 1.31), (0.1, 1.33)], 6, cap_bot=True)
    f += p.lathe([(0.07, 1.33), (0.1, 1.4), (0.0, 1.53)], 6, cap_bot=False)
    p.paint_fn(f, stone_fn("gray", 8, 7.0, moss=MOSS, moss_thresh=0.8, low=(0.2, -1), alt=("gray", 7, 0.3),
                           moss_cover=0.35))
    moss_clump(p, (0.22, 1.17, 0.1), 0.16, 10, squash=0.4)
    fl = flame.ico(1, 0.16, X((0, 0.93, 0), scale=(1.0, 1.3, 1.0)), jitter=0.05, seed=3)
    fl += flame.box((0.26, 0.02, 0.26), X((0, 0.77, 0)))           # glowing floor of the fire box
    flame.paint_fn(fl, lambda f_, n, c: ("yellow", 15) if n[1] > -0.4 else ("orange", 15))
    return [p, flame]


def fence():
    """2 m wooden fence run along local X."""
    p = Part("Fence")
    rng = Rng(91)
    f = []
    for i, x in enumerate((-0.95, 0.0, 0.95)):
        h = rng.uniform(0.82, 0.98)
        f += p.lathe([(0.085, -0.05), (0.08, h - 0.12), (0.0, h)], 4, X((x, 0.0, 0.0), yaw=rng.uniform(-8, 8),
                                                                      lean_r=rng.uniform(-5, 5), lean_f=rng.uniform(-3, 3)),
                     rot_offset=45, cap_bot=False)
    f += p.box((2.12, 0.075, 0.05), X((0.0, 0.38, 0.075), lean_r=1.5))
    f += p.box((2.08, 0.075, 0.05), X((0.02, 0.7, 0.075), lean_r=-2.5))
    p.paint(f, "brown", 8, top=1, bottom=-2, jitter=1, seed=9.0, grad=(0.0, 0.9, 1))
    return [p]


# ================================================================ act 2: sunken crypt
CRYPT = "gray"


def crypt_wall():
    """2 m x 2.8 m crypt wall block (pivot bottom centre, 0.56 m thick body centred on z 0). Brick faces are
    'Side_Surface' (M_Surface_CryptBrick); plinth, pilaster and cornice are palette stone."""
    side, trim = Part("Side_Surface"), Part("Trim")
    b = side.box((2.0, 2.1, 0.56), X((0, 1.4, 0)))
    drop_flat(side, b)
    f = sweep_x(trim, [(0.36, 0.0), (0.36, 0.28), (0.33, 0.35), (0.28, 0.35)], -1.0, 1.0)
    f += sweep_x(trim, [(-0.28, 0.35), (-0.33, 0.35), (-0.36, 0.28), (-0.36, 0.0)], -1.0, 1.0)
    f += quad_up(trim, -1.0, 1.0, -0.33, 0.33, 0.35)
    prof = [(0.28, 2.45), (0.34, 2.52), (0.37, 2.52), (0.37, 2.7), (0.33, 2.8)]
    f += sweep_x(trim, prof, -1.0, 1.0)
    f += sweep_x(trim, [(-z, y) for z, y in reversed(prof)], -1.0, 1.0)
    f += quad_up(trim, -1.0, 1.0, -0.33, 0.33, 2.8)
    f += trim.box((0.3, 2.1, 0.1), X((0.0, 1.4, 0.3)))
    f += trim.box((0.4, 0.14, 0.14), X((0.0, 2.38, 0.32)), bevel=0.02)
    f += trim.box((0.38, 0.16, 0.14), X((0.0, 0.43, 0.32)), bevel=0.02)
    trim.paint(f, CRYPT, 6, top=1, bottom=-2, jitter=0)
    return [side, trim]


def tombstone(variant):
    stone = Part("Tombstone")
    f = []
    if variant == "A":
        pts = [(-0.31, 0.0), (0.31, 0.0), (0.31, 0.62)]
        pts += [(0.31 * math.cos(math.radians(a)), 0.62 + 0.31 * math.sin(math.radians(a))) for a in range(30, 180, 30)]
        pts += [(-0.31, 0.62)]
        m = X((0, 0.08, 0.0), lean_f=-5, lean_r=3)
        f += stone.poly_prism(pts, 0.15, m)
        f += stone.box((0.07, 0.34, 0.02), X((0, 0.08, 0), lean_f=-5, lean_r=3) @ X((0, 0.6, 0.08)))
        f += stone.box((0.24, 0.07, 0.02), X((0, 0.08, 0), lean_f=-5, lean_r=3) @ X((0, 0.68, 0.08)))
        eng = f[-12:]
        f += stone.box((0.76, 0.1, 0.3), X((0, 0.05, 0.0)), bevel=0.02)
        stone.paint_fn(f, stone_fn(CRYPT, 9, 8.0, moss=("green", 6), moss_thresh=0.8, low=(0.2, -1)))
        stone.paint(eng, CRYPT, 4, top=0, bottom=0)
    else:
        lean = X((0, 0.0, 0.0), lean_r=-7, lean_f=-3)
        f += stone.box((0.62, 0.14, 0.42), X((0, 0.07, 0)), bevel=0.02)
        f += stone.box((0.44, 0.12, 0.3), X((0, 0.2, 0)), bevel=0.015)
        f += stone.box((0.17, 0.95, 0.13), lean @ X((0, 0.72, 0)))
        f += stone.box((0.6, 0.16, 0.13), lean @ X((0, 0.92, 0)))
        for i in range(8):
            a = i * 45 + 22.5
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            f += stone.box((0.14, 0.04, 0.09), lean @ X((ca * 0.19, 0.92 + sa * 0.19, 0.0), lean_r=a + 90))
        stone.paint_fn(f, stone_fn(CRYPT, 9, 9.0, moss=("green", 6), moss_thresh=0.85, low=(0.25, -1)))
    mound = Part("Mound")
    fm = mound.ico(2, 0.46, X((0.0, 0.0, 0.66), scale=(0.72, 0.2, 1.12)), jitter=0.12, seed=101 if variant == "A" else 102)
    clamp_ground(mound, fm, 0.0, keep=0.0)
    mound.delete_faces(lambda f_: f_.normal.z < -0.99)
    fm = [x for x in fm if x.is_valid]

    def earth(f_, n, c):   # dark, cool grave soil with a little moss: a low mound, not a bright dirt bed
        r = patch01(c, 5.0, 3.0 if variant == "A" else 4.0)
        if n[1] > 0.8 and r > 0.62:
            return "green", 3 + (1 if hash01(*c) > 0.5 else 0)       # sparse moss on the crown
        return "brown", 2 + (1 if n[1] > 0.5 else 0)
    mound.paint_fn(fm, earth)
    return [stone, mound]


def candles():
    wax, flame = Part("Wax"), Part("Flame")
    fw = wax.lathe([(0.3, 0.0), (0.26, 0.025), (0.0, 0.035)], 8, cap_bot=False, jitter=0.12, seed=4)
    wax.paint(fw, "gray", 13, top=0)
    spec = ((0.0, 0.0, 0.09, 0.62), (0.17, 0.07, 0.07, 0.4), (-0.15, 0.1, 0.075, 0.32), (0.06, -0.16, 0.06, 0.24),
            (-0.1, -0.12, 0.055, 0.48))
    ff, fc, fk = [], [], []
    for i, (x, z, r, h) in enumerate(spec):
        m = X((x, 0.0, z), lean_r=(i - 2) * 1.5)
        fc += wax.lathe([(r * 1.1, 0.0), (r, 0.06), (r, h), (r * 0.7, h + 0.02)], 6, m, rot_offset=i * 13, cap_bot=False)
        fk += wax.box((0.014, 0.035, 0.014), m @ X((0, h + 0.035, 0)))
        ff += flame.lathe([(0.025, 0.0), (0.045, 0.04), (0.0, 0.15)], 5, m @ X((0, h + 0.035, 0)), rot_offset=i * 20)
    wax.paint_fn(fc, lambda f, n, c: ("gray", 15 if n[1] > 0.5 else (14 if hash01(*c) > 0.3 else 13)))
    wax.paint(fk, "gray", 1, top=0, bottom=0)
    flame.paint_fn(ff, lambda f, n, c: ("yellow", 15) if n[1] < 0.35 else ("orange", 14))
    return [wax, flame]


def brazier():
    iron, flame = Part("Brazier"), Part("Flame", pivot=(0.0, 1.1, 0.0))
    f = []
    for i in range(3):
        a = math.radians(i * 120 + 30)
        f += iron.box((0.065, 0.95, 0.065), X((math.sin(a) * 0.25, 0.45, math.cos(a) * 0.25), yaw=math.degrees(a), lean_f=-17))
        f += iron.box((0.12, 0.05, 0.16), X((math.sin(a) * 0.38, 0.025, math.cos(a) * 0.38), yaw=math.degrees(a)))
    f += iron.lathe([(0.09, 0.5), (0.09, 0.78)], 6, cap_bot=True)
    f += iron.lathe([(0.12, 0.62), (0.2, 0.66), (0.12, 0.7)], 6)
    iron.paint(f, "gray", 3, top=1, bottom=-1)
    bowl = iron.lathe([(0.14, 0.76), (0.4, 0.94), (0.47, 1.08), (0.41, 1.1)], 8, rot_offset=22.5)
    iron.paint_fn(bowl, lambda f_, n, c: ("yellow", 9) if c[1] > 1.04 and n[1] > -0.2 else ("red", 3) if n[1] > 0.95
                  else ("gray", 4 if n[1] < 0 else 5))
    fl = flame.ico(1, 0.34, X((0, 1.08, 0), scale=(1.0, 0.28, 1.0)), jitter=0.15, seed=12)
    flame.paint_fn(fl, lambda f_, n, c: ("orange", 11 if hash01(*c) > 0.5 else 9))
    tongues = (((0.0, 1.1, 0.0), 0.62, 0.17, 0, 0), ((0.16, 1.1, 0.08), 0.42, 0.12, 110, 14),
               ((-0.14, 1.1, 0.1), 0.38, 0.11, 250, 16), ((0.02, 1.1, -0.16), 0.34, 0.1, 0, -15))
    for i, (pos, h, r, yaw, lean) in enumerate(tongues):
        ft = flame.lathe([(r, 0.0), (r * 1.1, h * 0.2), (r * 0.6, h * 0.55), (0.0, h)], 6,
                         X(pos, yaw=yaw, lean_f=lean), jitter=0.12, seed=40 + i, rot_offset=i * 11)
        flame.paint_fn(ft, flame_fn(h=h + 0.02, y0=1.1))
    return [iron, flame]


def coffin():
    """Wooden coffin on a stone bier (2.3 x 1.0 m footprint, head toward +Z). 'Lid' pivot at the lid centre."""
    bier, box_, lid = Part("Bier"), Part("Coffin"), Part("Lid", pivot=(0.0, 0.68, 0.0))
    fb = bier.box((1.05, 0.3, 2.3), X((0, 0.15, 0)), bevel=0.03)
    fb += bier.box((0.9, 0.06, 2.1), X((0, 0.33, 0)))
    bier.paint_fn(fb, stone_fn(CRYPT, 7, 10.0, jitter=False))
    outline = [(-0.24, -0.95), (0.24, -0.95), (0.36, 0.48), (0.28, 0.95), (-0.28, 0.95), (-0.36, 0.48)]
    fc = box_.extrude_y(outline, 0.36, 0.7, cap_bottom=False)
    box_.paint(fc, "brown", 5, top=1, bottom=-1)
    lid_out = [(x * 1.07, z * 1.04) for x, z in outline]
    m = X((0.05, 0.0, -0.08), yaw=5)
    fl = lid.extrude_y(lid_out, 0.7, 0.8, m, cap_bottom=True)
    lid.paint(fl, "brown", 6, top=1, bottom=-2)
    fs = lid.box((0.08, 0.025, 1.1), m @ X((0, 0.81, 0.05)))
    fs += lid.box((0.42, 0.025, 0.08), m @ X((0, 0.81, 0.34)))
    lid.paint(fs, "yellow", 10, top=2)
    fi = []
    for z in (-0.55, 0.62):
        fi += lid.box((0.62 if z > 0 else 0.5, 0.03, 0.07), m @ X((0, 0.805, z)))
    lid.paint(fi, "gray", 4, top=1)
    return [bier, box_, lid]


def _bone(part, m, L):
    """Dog-bone: shaft + a double knob at each end."""
    f = part.box((L, 0.06, 0.06), m)
    for sx in (-1, 1):
        for sz in (-1, 1):
            f += part.box((0.075, 0.075, 0.07), m @ X((sx * L / 2, 0.0, sz * 0.035)))
    return f


def bone_pile():
    """Heap of bones with a big skull on top (skull faces +Z)."""
    p, skull = Part("Bones"), Part("Skull")
    fh = p.ico(2, 0.52, X((0, 0.0, 0), scale=(1.15, 0.3, 1.0)), jitter=0.14, seed=13)
    clamp_ground(p, fh, 0.0, keep=0.0)
    p.delete_faces(lambda f_: f_.normal.z < -0.99)
    fh = [f for f in fh if f.is_valid]
    p.paint_fn(fh, lambda f_, n, c: ("brown", 4 + (1 if n[1] > 0.6 else 0)) if hash01(*c) > 0.3 else ("gray", 9))
    rng = Rng(111)
    fb = []
    for i in range(7):
        a = i * 137.5 + 15
        d = 0.1 + 0.3 * math.sqrt((i + 0.5) / 7)
        x, z = math.sin(math.radians(a)) * d, math.cos(math.radians(a)) * d
        y = 0.16 * max(0.0, 1.0 - (d / 0.55) ** 2) + 0.05
        m = X((x, y, z), yaw=rng.uniform(0, 180), lean_r=rng.uniform(-14, 14), lean_f=rng.uniform(-10, 10))
        fb += _bone(p, m, rng.uniform(0.36, 0.52))
    p.paint(fb, "gray", 13, top=2, bottom=-3, jitter=1, seed=3.0)
    sm = X((0.02, 0.2, 0.02), yaw=-12, lean_r=5, lean_f=-6)
    fs = skull.box((0.34, 0.29, 0.34), sm @ X((0, 0.14, -0.02)), bevel=0.08)
    fs += skull.box((0.22, 0.09, 0.14), sm @ X((0, 0.0, 0.1)), bevel=0.02)
    skull.paint(fs, "gray", 14, top=1, bottom=-3)
    fe = []
    for sx in (-1, 1):
        fe += skull.box((0.095, 0.085, 0.03), sm @ X((sx * 0.078, 0.15, 0.152)))
    fe += skull.lathe([(0.03, 0.0), (0.0, 0.05)], 3, sm @ X((0, 0.07, 0.15), lean_f=90), cap_bot=False)
    skull.paint(fe, "gray", 0, top=0, bottom=0)
    return [p, skull]


def iron_gate():
    """Stone gateway with an iron portcullis ('Gate' pivot bottom centre, raise it along +Y to open)."""
    frame, gate = Part("Frame"), Part("Gate")
    f = []
    for sx in (-1, 1):
        f += frame.box((0.46, 3.0, 0.56), X((sx * 1.36, 1.5, 0)), bevel=0.03)
        f += frame.box((0.56, 0.18, 0.64), X((sx * 1.36, 0.09, 0)), bevel=0.02)
    f += frame.box((3.3, 0.46, 0.62), X((0, 3.23, 0)), bevel=0.03)
    f += frame.box((3.44, 0.1, 0.7), X((0, 3.51, 0)), bevel=0.02)
    f += frame.lathe([(0.2, 3.46), (0.0, 3.85)], 4, rot_offset=45, squash=(1.0, 0.35))
    frame.paint_fn(f, stone_fn(CRYPT, 6, 11.0, low=(0.3, -1)))
    void = frame.raw([(-1.14, 0.0, -0.2), (1.14, 0.0, -0.2), (1.14, 3.0, -0.2), (-1.14, 3.0, -0.2)], [(0, 1, 2, 3)],
                     outward=(0, 0, 1))
    frame.paint(void, "gray", 0, top=0)
    g = []
    for i in range(7):
        x = -0.9 + i * 0.3
        g += gate.box((0.075, 2.55, 0.075), X((x, 1.275, 0.02)))
        g += gate.lathe([(0.065, 2.55), (0.0, 2.78)], 4, X((x, 0.0, 0.02)), rot_offset=45)
    for y in (0.35, 1.3, 2.25):
        g += gate.box((2.08, 0.085, 0.06), X((0, y, 0.08)))
    gate.paint_fn(g, lambda f_, n, c: ("brown", 3) if c[1] < 0.45 and hash01(*c) > 0.5
                  else ("gray", 4 if n[1] > 0.5 else 3))
    return [frame, gate]


def banner():
    """Hanging banner (pivot at the wall mount; hangs along -Y, faces +Z). 'Cloth' pivot at its top edge."""
    pole, cloth = Part("Pole"), Part("Cloth", pivot=(0.0, -0.03, 0.1))
    f = pole.box((1.02, 0.05, 0.05), X((0, 0.0, 0.1)))
    for sx in (-1, 1):
        f += pole.box((0.04, 0.04, 0.1), X((sx * 0.36, 0.0, 0.05)))
    pole.paint(f, "gray", 4, top=1)
    fk = []
    for sx in (-1, 1):
        fk += pole.ico(1, 0.05, X((sx * 0.55, 0.0, 0.1)))
    pole.paint(fk, "yellow", 11, top=2, bottom=-2)
    cols, rows, W, H, Hv = 4, 6, 0.8, 1.55, 1.28
    verts, faces = [], []
    for r in range(rows + 1):
        for c in range(cols + 1):
            u = c / cols
            x = -W / 2 + W * u
            ybot = -(Hv + (H - Hv) * abs(2 * u - 1))
            y = -0.03 + ybot * (r / rows)
            z = 0.1 + 0.03 * math.sin(u * 6.0 + 0.8) * (0.3 + r / rows)
            verts.append((x, y, z))
    n = len(verts)
    verts += [(x, y, z - 0.025) for x, y, z in verts]
    for r in range(rows):
        for c in range(cols):
            a = r * (cols + 1) + c
            faces.append((a, a + 1, a + cols + 2, a + cols + 1))
            faces.append((n + a, n + a + cols + 1, n + a + cols + 2, n + a + 1))
    fc = cloth.raw(verts, faces)
    cloth.orient(fc[0::2], (0, 0, 1))
    cloth.orient(fc[1::2], (0, 0, -1))

    def cloth_fn(f_, nn, c):
        edge = abs(c[0]) > W / 2 - 0.09
        if nn[2] <= 0:
            return "red", 4
        return ("yellow", 10) if edge or c[1] > -0.1 else ("red", 8 if hash01(*c) > 0.2 else 7)
    cloth.paint_fn(fc, cloth_fn)
    # emblem: gold diamond with a crimson core and a gold bar hem (raised, facing +Z)
    fe = cloth.lathe([(0.19, 0.0), (0.15, 0.025)], 4, X((0, -0.68, 0.1), lean_f=90), squash=(0.75, 1.25), cap_bot=False)
    cloth.paint(fe, "yellow", 12, top=0)
    fe = cloth.lathe([(0.09, 0.0), (0.06, 0.03)], 4, X((0, -0.68, 0.115), lean_f=90), squash=(0.75, 1.25), cap_bot=False)
    cloth.paint(fe, "red", 5, top=0)
    fe = cloth.box((W + 0.06, 0.08, 0.05), X((0, -0.06, 0.1)))
    cloth.paint(fe, "yellow", 11, top=1, bottom=-3)
    return [pole, cloth]


# ================================================================ act 3: crystal hollow
CAVE = "indigo"
CAVE_ROCK = dict(alt=("gray", 3, 0.45))


def _shard(part, pos, length, r, yaw, lean, seed):
    return part.lathe([(r * 0.8, -0.1), (r, length * 0.14), (r * 0.94, length * 0.7), (0.0, length)], 6,
                      X(pos, yaw=yaw, lean_f=lean), rot_offset=seed * 17 % 60, cap_bot=False)


def crystal_fn(fam, seed=0.0, lo=10):
    def fn(f, n, c):
        r = hash01(n[0] * 3.1, n[1] * 2.3, n[2] * 1.7, seed)
        if n[1] > 0.45:
            return fam, 15 if r > 0.4 else 14
        return fam, lo + (3 if r > 0.66 else (1 if r > 0.33 else 0))
    return fn


def crystal(variant):
    base, cr = Part("Base"), Part("Crystal")
    if variant == "A":
        fb = base.ico(2, 0.62, X((0, 0.05, 0), scale=(1.3, 0.5, 1.1)), jitter=0.16, seed=201)
        spec = (((0, 0.05, 0), 2.0, 0.27, 20, 6), ((0.3, 0.02, 0.12), 1.35, 0.2, 110, 24),
                ((-0.3, 0.02, 0.08), 1.15, 0.19, 235, 30), ((0.05, 0.0, -0.32), 0.85, 0.15, 170, 40),
                ((0.42, 0.0, -0.22), 0.6, 0.12, 140, 50), ((-0.35, 0.0, -0.25), 0.55, 0.11, 200, 48))
        fams = ("cyan",) * 6
    elif variant == "B":
        fb = base.ico(2, 0.48, X((0, 0.04, 0), scale=(1.25, 0.5, 1.1)), jitter=0.16, seed=202)
        spec = (((0, 0.04, 0), 1.35, 0.21, 40, 10), ((0.25, 0.02, 0.1), 0.95, 0.16, 120, 30),
                ((-0.22, 0.02, 0.12), 0.8, 0.15, 250, 34), ((0.0, 0.0, -0.25), 0.6, 0.12, 180, 42))
        fams = ("purple", "purple", "magenta", "purple")
    else:
        fb = base.ico(2, 0.3, X((0, 0.02, 0), scale=(1.3, 0.55, 1.1)), jitter=0.18, seed=203)
        spec = (((0, 0.02, 0), 0.75, 0.13, 30, 12), ((0.14, 0.0, 0.06), 0.5, 0.1, 100, 32),
                ((-0.13, 0.0, 0.05), 0.45, 0.09, 260, 36), ((0.02, 0.0, -0.15), 0.38, 0.08, 180, 44))
        fams = ("cyan", "cyan", "purple", "cyan")
    clamp_ground(base, fb, 0.0)
    base.paint_fn(fb, stone_fn(CAVE, 3, 12.0, moss=("purple", 5), moss_thresh=0.6, **CAVE_ROCK))
    for i, ((pos, L, r, yaw, lean), fam) in enumerate(zip(spec, fams)):
        fs = _shard(cr, pos, L, r, yaw, lean, i + 3)
        cr.paint_fn(fs, crystal_fn(fam, seed=i * 1.3, lo=9 if fam != "magenta" else 10))
    return [base, cr]


def stalagmite():
    """Cluster of three spires on a rubble skirt. Upper faces and the tips are pale lavender-grey so the spires read
    against the dark cave floor (the base stays in the cave indigo)."""
    p = Part("Rock")
    f = p.lathe([(0.78, 0.0), (0.5, 0.14), (0.0, 0.2)], 8, jitter=0.2, seed=301, cap_bot=False)
    for i, (pos, h, r, lf, lr) in enumerate((((0, 0.0, 0), 2.5, 0.46, -3, 4), ((0.48, 0.0, 0.2), 1.55, 0.32, 6, 8),
                                             ((-0.38, 0.0, -0.26), 0.95, 0.25, -7, -6))):
        f += p.lathe([(r, 0.0), (r * 0.76, h * 0.28), (r * 0.46, h * 0.6), (r * 0.2, h * 0.84), (0.0, h)], 7,
                     X(pos, lean_f=lf, lean_r=lr), jitter=0.14, seed=310 + i, rot_offset=i * 21, cap_bot=False)

    def fn(f_, n, c):
        y, r = c[1], hash01(*c)
        if y > 1.25 or (y > 0.7 and n[1] > 0.25):
            return "gray", 8 + (1 if y > 1.8 else 0) + (1 if n[1] > 0.3 else 0)       # pale tips / lit shoulders
        band = int(y * 4.0) % 2
        s = 3 + min(3, int(y * 2.0)) + band + (1 if n[1] > 0.5 else 0)
        return (CAVE if r > 0.35 else "gray"), s
    p.paint_fn(f, fn)
    return [p]


def cave_rock(variant):
    p = Part("Rock")
    parts = [p]
    if variant == "A":
        f = p.ico(2, 0.95, X((0, 0.45, 0), yaw=15, scale=(1.2, 0.78, 1.0)), jitter=0.16, seed=321)
        f += p.ico(2, 0.52, X((0.9, 0.2, -0.4), yaw=40, scale=(1.0, 0.8, 1.0)), jitter=0.18, seed=322)
        clamp_ground(p, f, -0.04)
        cr = Part("Crystal")
        fs = _shard(cr, (-0.55, 0.45, -0.55), 0.55, 0.1, 225, 40, 1)
        fs += _shard(cr, (-0.35, 0.35, -0.72), 0.36, 0.075, 190, 55, 2)
        cr.paint_fn(fs, crystal_fn("cyan", seed=9.0))
        parts.append(cr)
    else:
        f = p.ico(2, 0.6, X((0, 0.12, 0), yaw=5, scale=(1.3, 0.45, 1.0)), jitter=0.16, seed=323)
        f += p.ico(2, 0.42, X((0.55, 0.1, 0.35), yaw=60, scale=(1.1, 0.55, 1.0)), jitter=0.18, seed=324)
        f += p.ico(2, 0.3, X((-0.5, 0.08, 0.4), yaw=30, scale=(1.0, 0.6, 1.2)), jitter=0.2, seed=325)
        clamp_ground(p, f, -0.04)
    p.paint_fn(f, stone_fn(CAVE, 3, 13.0, moss=("purple", 5), moss_thresh=0.62, moss_cover=0.5, **CAVE_ROCK))
    return parts


def glow_mushroom():
    stem, cap = Part("Stem"), Part("Cap_Emissive")
    spec = ((0.0, 0.0, 0.58, 0.34, "cyan", -4, 3), (0.34, 0.14, 0.36, 0.23, "purple", 9, 12),
            (-0.27, 0.2, 0.26, 0.18, "cyan", -6, -12), (0.1, -0.3, 0.2, 0.13, "teal", -14, 5))
    fs, fc = [], []
    for i, (x, z, h, r, fam, lf, lr) in enumerate(spec):
        k = r / 0.34
        m = X((x, 0.0, z), lean_f=lf, lean_r=lr)
        s = stem.lathe([(0.085 * k + 0.02, 0.0), (0.065 * k + 0.015, h * 0.55), (0.08 * k + 0.015, h)], 6, m,
                       rot_offset=i * 10, cap_bot=False)
        fs += s
        c = cap.lathe([(r * 0.28, -0.01), (r, 0.1 * r), (r * 0.84, 0.5 * r), (r * 0.4, 0.8 * r), (0.0, 0.88 * r)], 8,
                      m @ X((0, h, 0)), rot_offset=i * 7, jitter=0.06, seed=400 + i)

        def cap_fn(f_, n, cc, fam=fam):
            if n[1] < -0.3:
                return fam, 6
            return fam, 14 if hash01(*cc) > 0.8 else (11 if n[1] > 0.5 else 9)
        cap.paint_fn(c, cap_fn)
        fc += c
    stem.paint_fn(fs, lambda f_, n, c: ("skin", 12 if c[1] > 0.12 else 10))
    return [stem, cap]


def rune_stone():
    stone, runes = Part("Stone"), Part("Runes_Emissive")
    tilt = X(lean_f=-4, lean_r=2)
    f = stone.box((0.74, 1.72, 0.42), tilt @ X((0, 0.86, 0)), taper=(0.72, 0.8), bevel=0.05)
    f += stone.lathe([(0.66, -0.02), (0.6, 0.07), (0.5, 0.1)], 7, jitter=0.12, seed=7, cap_bot=False)
    stone.paint_fn(f, stone_fn("gray", 5, 14.0, moss=(CAVE, 6), moss_thresh=0.7, alt=(CAVE, 4, 0.35)))
    # front face plane: z = 0.21 at y 0 -> 0.168 at y 1.72 (taper 0.8) => lean back atan(0.042 / 1.72)
    face_lean = -math.degrees(math.atan2(0.21 - 0.21 * 0.8, 1.72))

    def bar(x, y, length, ang):
        zf = 0.21 - 0.042 * (y / 1.72) + 0.012
        return runes.box((0.05, length, 0.018), tilt @ X((x, y, zf), lean_f=face_lean) @ X(lean_r=ang))
    g = []
    g += bar(-0.02, 1.28, 0.3, 0) + bar(0.06, 1.34, 0.17, -50) + bar(0.06, 1.25, 0.15, -50)
    d = 0.1
    for sx, sy, a in ((-1, 1, -45), (1, 1, 45), (1, -1, -45), (-1, -1, 45)):
        g += bar(sx * d / 2, 0.88 + sy * d / 2, 0.15, a)
    g += bar(0.0, 0.46, 0.3, 0) + bar(-0.06, 0.55, 0.15, 45) + bar(0.06, 0.55, 0.15, -45)
    runes.paint(g, "cyan", 14, top=1, bottom=0)
    return [stone, runes]


# ================================================================ extra dressing (density + height layering)
def water_pool(glow=False):
    """Irregular shallow pool, ~2.7 x 2.0 m, water surface 3 cm above the pivot (ground) plane, ringed by stones.
    Water (mid-value, so it reads as water and not a hole): pale teal shallows at the shore, sky-blue body, a lighter
    ripple ring, three floating lily pads (they bob with the Water part); Rim = stones, a pale wet-sand shoreline and a
    reed clump. Glints_Emissive = four tiny sparkles.
    glow=True (Act 3): the whole water is emissive cyan (a light pool, no separate glints, no pads / reeds)."""
    rim, water = Part("Rim"), Part("Water_Emissive" if glow else "Water", pivot=(0.0, 0.03, 0.0))
    n, y = 18, 0.03
    outline = []
    for i in range(n):
        a = 2 * math.pi * i / n
        r = 1.25 * (0.8 + 0.3 * hash01(i * 1.7, 4.0, 9.0 if glow else 7.0))
        outline.append((math.cos(a) * r * 1.1, math.sin(a) * r * 0.82))
    rings = [[(x * k, y, z * k) for x, z in outline] for k in (1.0, 0.8, 0.62, 0.5)]
    fw = water.quads(rings + [[(0.0, y, 0.0)]], close_ends=False)
    water.orient(fw, (0, 1, 0))

    def wfn(f_, nn, c):
        rr = math.hypot(c[0] / 1.1, c[2] / 0.82) / 1.25
        if glow:
            return "cyan", 6 if rr > 0.78 else (5 if 0.56 < rr < 0.66 else 3)
        if rr > 0.78:
            return "teal", 7                                # shallows over sand
        return "sky", 7 if 0.56 < rr < 0.66 else 5          # ripple ring / open water (luma ~0.25-0.3 lit)
    water.paint_fn(fw, wfn)
    parts = [rim, water]
    rng = Rng(611 if glow else 601)
    if not glow:
        pads = []
        for i, (px, pz, pr) in enumerate(((-0.55, -0.12, 0.2), (-0.3, 0.3, 0.15), (0.62, -0.28, 0.17))):
            pads += water.cyl(7, pr, pr, 0.012, X((px, y, pz), yaw=i * 50), cap_bot=False, rot_offset=i * 13)
        water.paint_fn(pads, lambda f_, nn, c: ("green", 7 if nn[1] > 0.5 else 4))
        gl = Part("Glints_Emissive", pivot=(0.0, 0.03, 0.0))
        fg = []
        for i, (gx, gz, gw) in enumerate(((-0.1, 0.12, 0.2), (0.3, -0.15, 0.14), (0.05, 0.42, 0.1), (0.55, 0.25, 0.08))):
            fg += gl.box((gw, 0.006, 0.035), X((gx, y + 0.004, gz), yaw=12 + i * 7))
        gl.paint(fg, "sky", 14, top=0, bottom=0)
        parts.append(gl)
        # pale wet-sand shoreline between the water edge and the stones
        shore = rim.quads([[(x * 1.1, y - 0.004, z * 1.1) for x, z in outline],
                           [(x * 0.99, y - 0.004, z * 0.99) for x, z in outline]], close_ends=False)
        rim.orient(shore, (0, 1, 0))
        rim.paint(shore, "skin", 9, top=0, bottom=0)
        reeds = []
        for i in range(6):
            a = math.radians(200 + i * 11)
            rx, rz = math.cos(a) * 1.15, math.sin(a) * 0.9
            reeds += _blade(rim, (rx, 0.0, rz), rng.uniform(0.45, 0.7), rng.uniform(4, 14), rng.uniform(0, 360),
                            w=0.035, seed=i)
        rim.paint_fn(reeds, lambda f_, nn, c: ("green", 5 + min(3, int(c[1] * 6))))
    fr = []
    for i, (x, z) in enumerate(outline):
        if i % 3 == 2 and not glow:
            continue
        r = rng.uniform(0.14, 0.26)
        fr += rim.ico(1, r, X((x * 1.02, 0.0, z * 1.02), yaw=i * 41, scale=(1.25, 0.62, 1.0)), jitter=0.2, seed=620 + i)
    clamp_ground(rim, fr, 0.0)
    if glow:
        rim.paint_fn(fr, stone_fn(CAVE, 3, 16.0, moss=("purple", 5), moss_thresh=0.6, alt=("gray", 3, 0.4)))
    else:
        rim.paint_fn(fr, stone_fn("gray", 7, 16.0, moss=("green", 3), moss_thresh=0.6, moss_cover=0.25))
    return parts


def stairs():
    """Ruined temple steps: two risers up to a 0.42 m landing (3.6 m wide, 2.1 m deep). Risers face local +Z (use
    rotY 180 so they face the camera); the landing (local z -1.05 .. 0.45, top y 0.42) carries e.g. Env_RuinArch."""
    p = Part("Stairs")
    rng = Rng(701)
    blocks = []
    W = 3.6
    for z0, z1, h in ((0.75, 1.05, 0.14), (0.45, 0.75, 0.28), (-1.05, 0.45, 0.42)):
        cols = 4 if z1 - z0 < 0.5 else 3
        for c in range(cols):
            x0 = -W / 2 + W * c / cols
            w = W / cols - 0.03
            hh = h - (rng.uniform(0.0, 0.05) if h < 0.4 else rng.uniform(0.0, 0.03))
            blocks.append(p.box((w, hh, z1 - z0 - 0.02), X((x0 + W / cols / 2 + rng.uniform(-0.02, 0.02), hh / 2,
                                                            (z0 + z1) / 2), yaw=rng.uniform(-2, 2)), skip_bottom=True))
    for sx in (-1, 1):   # cheek walls
        blocks.append(p.box((0.28, 0.56, 1.95), X((sx * (W / 2 + 0.12), 0.28, -0.07)), bevel=0.02, skip_bottom=True))
    paint_blocks(p, blocks, 6.0, moss_cover=0.12)
    moss_clump(p, (-1.9, 0.56, -0.6), 0.22, 11)
    moss_clump(p, (1.2, 0.14, 0.95), 0.16, 12, squash=0.3)
    return [p]


def log():
    """Fallen mossy log, 1.9 m along local X, broken end at +X, moss on top."""
    p = Part("Log")
    f = p.lathe([(0.2, -0.95), (0.23, -0.9), (0.22, 0.6), (0.2, 0.95)], 7, rot_offset=10, jitter=0.08, seed=8)
    bmesh.ops.transform(p.bm, matrix=X((0.0, 0.2, 0.0), lean_r=90, yaw=0), verts=p._verts_of(f))

    def bark(f_, n, c):
        if abs(n[0]) > 0.9:                       # end grain
            return ("brown", 9) if hash01(*c) > 0.3 else ("brown", 7)
        if n[1] > 0.55 and patch01(c, 2.5, 5.0) > 0.35:
            return "lime", 7 + (1 if n[1] > 0.85 else 0)
        return "brown", 4 + (1 if n[1] > 0.2 else 0) + (1 if hash01(*c) > 0.8 else 0)
    p.paint_fn(f, bark)
    br = p.box((0.18, 0.1, 0.1), X((-0.35, 0.32, 0.12), yaw=35, lean_f=-20), taper=(0.5, 0.5))
    p.paint(br, "brown", 5, top=1)
    return [p]


def stump():
    p = Part("Stump")
    f = p.lathe([(0.34, 0.0), (0.26, 0.08), (0.24, 0.3), (0.23, 0.36)], 7, jitter=0.08, seed=9, cap_bot=False)
    for i, a in enumerate((30, 150, 270)):
        f += p.box((0.12, 0.14, 0.3), X((0.22 * math.sin(math.radians(a)), 0.05, 0.22 * math.cos(math.radians(a))),
                                        yaw=a), taper=(0.5, 0.4), skip_bottom=True)

    def fn(f_, n, c):
        if n[1] > 0.9 and c[1] > 0.3:
            rr = math.hypot(c[0], c[2])
            return ("brown", 9) if rr < 0.1 else ("brown", 7) if rr < 0.17 else ("brown", 8)
        return "brown", 4 + (1 if n[1] > 0.3 else 0) + (1 if hash01(*c) > 0.8 else 0)
    p.paint_fn(f, fn)
    moss_clump(p, (-0.14, 0.34, 0.1), 0.12, 13, squash=0.35)
    return [p]


def mushrooms():
    """Three cute red toadstools with ivory spots (Act 1 forest floor)."""
    stem, cap = Part("Stems"), Part("Caps")
    fs, fc, fd = [], [], []
    for i, (x, z, h, r) in enumerate(((0.0, 0.0, 0.2, 0.13), (0.14, 0.08, 0.13, 0.085), (-0.1, 0.12, 0.09, 0.065))):
        m = X((x, 0.0, z), lean_r=(i - 1) * 7, lean_f=-5)
        fs += stem.lathe([(0.04 * r / 0.13 + 0.012, 0.0), (0.032 * r / 0.13 + 0.01, h)], 5, m, cap_bot=False)
        fc += cap.lathe([(r * 0.3, h - 0.01), (r, h + r * 0.15), (r * 0.8, h + r * 0.6), (0.0, h + r * 0.85)], 7, m,
                        rot_offset=i * 13)
        for k in range(3):
            a = math.radians(k * 120 + i * 40)
            fd += cap.box((r * 0.28, 0.012, r * 0.28), m @ X((math.cos(a) * r * 0.55, h + r * 0.52,
                                                               math.sin(a) * r * 0.55), lean_r=math.degrees(a) * 0.1))
    stem.paint(fs, "skin", 14, top=0)
    cap.paint_fn(fc, lambda f_, n, c: ("red", 11 if n[1] > 0.5 else 9) if n[1] > -0.3 else ("skin", 12))
    cap.paint(fd, "skin", 15, top=0, bottom=0)
    return [stem, cap]


def urn():
    """Crypt funerary urn, 0.62 m."""
    p = Part("Urn")
    f = p.lathe([(0.14, 0.0), (0.16, 0.05), (0.26, 0.24), (0.24, 0.4), (0.13, 0.5), (0.12, 0.55), (0.17, 0.6),
                 (0.15, 0.62)], 8, rot_offset=22.5, cap_bot=False)
    p.paint_fn(f, lambda f_, n, c: ("yellow", 9) if 0.54 < c[1] < 0.6 else
               ("brown", 6 + (1 if n[1] > 0.3 else 0) + (1 if c[1] > 0.22 and c[1] < 0.3 else 0)))
    for sx in (-1, 1):
        h = p.box((0.05, 0.14, 0.04), X((sx * 0.27, 0.36, 0.0), lean_r=-sx * 25))
        p.paint(h, "brown", 5, top=1)
    return [p]


# ================================================================ ground (all acts)
def ground_tile():
    """4 x 4 m ground slab top (pivot = centre of the top face; ground plane sits at world y -0.2)."""
    top = Part("Top_Surface")
    for i in range(2):
        for j in range(2):
            quad_up(top, -2.0 + 2.0 * i, 2.0 * i, -2.0 + 2.0 * j, 2.0 * j)
    return [top]


def ground_mound():
    """Low 5 x 3.6 m hill, 0.75 m high, edges dip just below the ground plane. Surface only."""
    top = Part("Top_Surface")
    nx, nz, W, D, H = 9, 7, 5.0, 3.6, 0.75
    verts, faces = [], []
    for j in range(nz + 1):
        for i in range(nx + 1):
            x, z = -W / 2 + W * i / nx, -D / 2 + D * j / nz
            rr = (x / (W / 2)) ** 2 + (z / (D / 2)) ** 2
            y = H * max(0.0, 1.0 - rr) ** 1.6 - 0.03
            if 0 < i < nx and 0 < j < nz:
                y += 0.06 * (hash01(x, z, 3.0) - 0.5)
                x += 0.12 * (hash01(x, z, 5.0) - 0.5)
                z += 0.12 * (hash01(x, z, 7.0) - 0.5)
            verts.append((x, y, z))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    top.raw(verts, faces, outward=(0, 1, 0))
    return [top]


def ground_patch():
    """Irregular flat ground decal, ~2.6 m across, 1.5 cm above the ground plane (paths, sand, rubble). Surface only;
    chain scaled / rotated copies to draw paths."""
    top = Part("Top_Surface")
    n, verts = 14, [(0.0, 0.015, 0.0)]
    for i in range(n):
        a = 2 * math.pi * i / n
        r = 1.3 * (0.72 + 0.4 * hash01(i * 1.3, 2.0, 5.0))
        verts.append((math.cos(a) * r * 1.15, 0.015, math.sin(a) * r * 0.85))
    top.raw(verts, [(0, 1 + (i + 1) % n, 1 + i) for i in range(n)], outward=(0, 1, 0))
    return [top]


def paving_patch():
    """Irregular patch of old paving, ~3.4 x 2.6 m, 2.5 cm above the ground: 'Top_Surface' takes the act's paving
    material (brick / flagstone), 'Rubble' = loose slabs along the broken edge. Chain / rotate copies for terraces
    and walks; cheaper and calmer than scattering kit floor tiles."""
    top, rub = Part("Top_Surface"), Part("Rubble")
    n, y = 16, 0.025
    verts = [(0.0, y, 0.0)]
    outline = []
    for i in range(n):
        a = 2 * math.pi * i / n
        r = 1.0 * (0.78 + 0.34 * hash01(i * 2.3, 1.0, 11.0))
        outline.append((math.cos(a) * r * 1.7, math.sin(a) * r * 1.3))
    verts += [(x, y, z) for x, z in outline]
    top.raw(verts, [(0, 1 + (i + 1) % n, 1 + i) for i in range(n)], outward=(0, 1, 0))
    rng = Rng(801)
    fr = []
    for i, (x, z) in enumerate(outline):
        if i % 4:                      # a few loose slabs only (the review read dense rubble as grey specks)
            continue
        k = 1.08 + rng.uniform(0.0, 0.12)
        fr += rub.box((rng.uniform(0.26, 0.36), 0.05, rng.uniform(0.18, 0.28)), X((x * k, 0.02, z * k),
                                                                                  yaw=rng.uniform(0, 90)))
    rub.paint_fn(fr, stone_fn("gray", 6, 17.0, patch=4.0))
    return [top, rub]


def cave_floor_tile():
    """4 x 4 m faceted cave floor (Act 3 ground; palette, no surface). Pivot = centre of the tile at ground level.
    Border vertices stay flat at y 0 so tiles butt seamlessly; interior vertices are jittered for a crisp
    low-poly rock floor in dark indigo, with a few violet / slate patches. Rotate copies by 90 deg to hide repeats."""
    p = Part("Floor")
    n, S = 6, 4.0
    verts, faces = [], []
    for j in range(n + 1):
        for i in range(n + 1):
            x, z = -S / 2 + S * i / n, -S / 2 + S * j / n
            y = 0.0
            if 0 < i < n and 0 < j < n:
                x += 0.22 * (hash01(i, j, 1.0) - 0.5)
                z += 0.22 * (hash01(i, j, 2.0) - 0.5)
                y = 0.07 * (hash01(i, j, 3.0) - 0.35)
            verts.append((x, y, z))
    for j in range(n):
        for i in range(n):
            a = j * (n + 1) + i
            if (i + j) % 2:
                faces += [(a, a + 1, a + n + 2), (a, a + n + 2, a + n + 1)]
            else:
                faces += [(a, a + 1, a + n + 1), (a + 1, a + n + 2, a + n + 1)]
    f = p.raw(verts, faces, outward=(0, 1, 0))

    def fn(f_, nn, c):
        r, q = hash01(*c), patch01(c, 0.9, 21.0)
        fam = "purple" if q > 0.9 else ("gray" if q < 0.3 else CAVE)
        return fam, 1 + (1 if r > 0.75 else 0)
    p.paint_fn(f, fn)
    return [p]


def moss_border():
    """Shade skirt for the outside of the arena walls (Act 1): a flat, 4 m long strip of dark moss and soil, 2.1-2.8 m
    wide with a ragged outer edge, 1.2 cm above the ground, plus a few low, dark moss cushions near the straight inner
    edge. It darkens the ground band next to the rim so the sunlit arena reads as the stage. Pivot = centre of the
    straight inner edge on the ground plane; the strip extends toward local -X (rotY 0 for the left wall, 180 for the
    right, 90 for the top wall)."""
    p = Part("Moss")
    L, n = 4.0, 8
    rng = Rng(1311)
    inner = [(0.0, -L / 2 + L * i / n) for i in range(n + 1)]
    mid = [(-1.1 - 0.15 * rng.uniform(-1, 1), z) for _, z in inner]
    outer = [(-2.1 - 0.7 * hash01(i * 1.9, 3.0, 5.0), z + (0.0 if i in (0, n) else rng.uniform(-0.15, 0.15)))
             for i, (_, z) in enumerate(inner)]
    rows = [[(x, 0.012 + (0.0 if k in (0, 2) else 0.012 * hash01(x, z, 2.0)), z) for x, z in row]
            for k, row in enumerate((inner, mid, outer))]
    f = p.quads(rows, close_ends=False)
    p.orient(f, (0, 1, 0))

    def fn(f_, nn, c):
        q = patch01(c, 1.1, 5.0)
        if q < 0.3:
            return "brown", 1
        return "green", 1 + (1 if q > 0.6 else 0)
    p.paint_fn(f, fn)
    cushions = []
    for i in range(4):
        z = -L / 2 + 0.5 + i * (L - 1.0) / 3 + rng.uniform(-0.2, 0.2)
        cushions += p.ico(1, rng.uniform(0.16, 0.24), X((-0.3 - rng.uniform(0.0, 0.3), 0.0, z), yaw=i * 47,
                                                        scale=(1.3, 0.4, 1.1)), jitter=0.18, seed=1320 + i)
    clamp_ground(p, cushions, 0.0)
    p.paint_fn(cushions, lambda f_, nn, c: ("green", 3 if nn[1] > 0.6 else 2))
    return [p]


def crypt_ground_tile():
    """4 x 4 m crypt flagstone ground (Act 2; palette, no surface): four courses of large, dark slate slabs with
    bevelled edges over a near-black joint bed. Low contrast on purpose, so the lit arena is the brightest thing in
    the crypt. Pivot = centre of the tile at ground level; rotate copies by 90 deg to hide repeats."""
    p = Part("Floor")
    S, c, jy = 4.0, 0.04, -0.018
    bed = quad_up(p, -S / 2, S / 2, -S / 2, S / 2, jy)
    p.paint(bed, "gray", 0, top=0, bottom=0)
    rng = Rng(1401)
    rows = ((1.3, 1.5, 1.2), (0.9, 1.6, 1.5), (1.6, 1.1, 1.3), (1.2, 1.4, 1.4))
    for j, widths in enumerate(rows):
        z0, z1 = -S / 2 + j, -S / 2 + j + 1
        x = -S / 2
        for k, w in enumerate(widths):
            x0, x1 = x, min(S / 2, x + w * S / sum(widths))
            x = x1
            g = 0.012
            top = rect((x1 - x0) / 2 - g - c, (z1 - z0) / 2 - g - c, 0.0, (x0 + x1) / 2, (z0 + z1) / 2)
            low = rect((x1 - x0) / 2 - g, (z1 - z0) / 2 - g, jy, (x0 + x1) / 2, (z0 + z1) / 2)
            f = p.quads([top, low], close_ends=False)
            f += p.raw(top, [(0, 1, 2, 3)], outward=(0, 1, 0))
            p.orient(f, lambda cc, cx=(x0 + x1) / 2, cz=(z0 + z1) / 2: (cc[0] - cx, 0.4, cc[2] - cz))
            shade = 2 if rng.uniform(0, 1) > 0.8 else 1      # darker than the (tinted) arena floor
            p.paint_fn(f, lambda f_, nn, cc, shade=shade: ("gray", shade))
    return [p]


def crypt_pillar():
    """Square crypt pillar, 2.9 m: plinth, shaft with a sunken panel, capital. Dark crypt stone, no moss."""
    p = Part("Pillar")
    f = p.box((0.74, 0.3, 0.74), X((0, 0.15, 0)), bevel=0.03)
    f += p.box((0.62, 0.08, 0.62), X((0, 0.34, 0)))
    f += p.box((0.46, 2.1, 0.46), X((0, 1.43, 0)))
    f += p.box((0.6, 0.1, 0.6), X((0, 2.53, 0)))
    f += p.box((0.76, 0.24, 0.76), X((0, 2.7, 0)), bevel=0.03)
    p.paint_fn(f, stone_fn(CRYPT, 6, 18.0, low=(0.35, -1), jitter=False))
    pan = []
    for yaw in (0, 90, 180, 270):
        pan += p.box((0.26, 1.5, 0.02), X((0, 1.45, 0), yaw=yaw) @ X((0, 0, 0.235)))
    p.paint(pan, CRYPT, 4, top=0, bottom=0)
    return [p]


def pebbles():
    p = Part("Pebbles")
    rng = Rng(501)
    f = []
    for i in range(6):
        a = i * 137.5
        d = 0.08 + 0.4 * math.sqrt((i + 0.5) / 6)
        r = rng.uniform(0.07, 0.14)
        f += p.ico(1, r, X((math.sin(math.radians(a)) * d, r * 0.3, math.cos(math.radians(a)) * d), yaw=a,
                           scale=(1.2, 0.65, 1.0)), jitter=0.22, seed=510 + i)
    clamp_ground(p, f, 0.0)
    p.paint_fn(f, stone_fn("gray", 8, 15.0))
    return [p]


# ================================================================ effects
def light_shaft(sides=8, r_top=0.5, r_bot=0.85):
    """Fake god-ray volume for BilliardRogue/LightShaft: open frustum, pivot at the TOP centre, 1 m long along
    local -Y (scale Y = length, X/Z = top diameter), flares to 1.7x at the bottom. UV0: u around (0..1, seam at
    local +X), v = 1 at the top -> 0 at the bottom (length fade). Normals point outward (N.V edge softness);
    the shader should render it Cull Off so the back half thickens the core."""
    p = Part("Shaft", unit_uv=True)
    bm, uv = p.bm, p.uv
    rows = []
    for t in (0.0, 0.5, 1.0):
        r = r_top + (r_bot - r_top) * t
        ring = []
        for i in range(sides + 1):          # duplicated seam column so u runs 0..1 without wrapping
            a = 2 * math.pi * (i % sides) / sides
            ring.append(bm.verts.new(Vector((-math.cos(a) * r, -math.sin(a) * r, -t))))
        rows.append((ring, 1.0 - t))
    for (ra, va), (rb, vb) in zip(rows, rows[1:]):
        for i in range(sides):
            f = bm.faces.new((ra[i], rb[i], rb[i + 1], ra[i + 1]))
            f.normal_update()
            c = f.calc_center_median()
            if f.normal.dot(Vector((c.x, c.y, 0.0))) < 0:
                f.normal_flip()
            for loop in f.loops:
                ring, v = (ra, va) if loop.vert in ra else (rb, vb)
                loop[uv].uv = (ring.index(loop.vert) / sides, v)
    return [p]


# ================================================================ registry
PIECES = {
    # arena kit
    "Env_FloorTile": floor_tile,
    "Env_FloorTile_Danger": lambda: floor_tile(True),
    "Env_LaunchPad": launch_pad,
    "Env_WallSegment": wall_segment,
    "Env_WallCorner": wall_corner,
    "Env_WallTorch": wall_torch,
    "Env_WallTorch_Arcane": wall_torch_arcane,
    # act 1
    "Env_Tree_A": tree_a,
    "Env_Tree_B": tree_b,
    "Env_Bush": bush,
    "Env_GrassTuft": grass_tuft,
    "Env_Flowers": flowers,
    "Env_RuinColumn": ruin_column,
    "Env_RuinColumn_Broken": lambda: ruin_column(True),
    "Env_RuinArch": ruin_arch,
    "Env_MossyRock_A": lambda: mossy_rock("A"),
    "Env_MossyRock_B": lambda: mossy_rock("B"),
    "Env_StoneLantern": stone_lantern,
    "Env_Fence": fence,
    # act 2
    "Env_CryptWall": crypt_wall,
    "Env_Tombstone_A": lambda: tombstone("A"),
    "Env_Tombstone_B": lambda: tombstone("B"),
    "Env_Candles": candles,
    "Env_Brazier": brazier,
    "Env_Coffin": coffin,
    "Env_BonePile": bone_pile,
    "Env_IronGate": iron_gate,
    "Env_Banner": banner,
    "Env_CryptPillar": crypt_pillar,
    # act 3
    "Env_Crystal_A": lambda: crystal("A"),
    "Env_Crystal_B": lambda: crystal("B"),
    "Env_Crystal_C": lambda: crystal("C"),
    "Env_Stalagmite": stalagmite,
    "Env_CaveRock_A": lambda: cave_rock("A"),
    "Env_CaveRock_B": lambda: cave_rock("B"),
    "Env_GlowMushroom": glow_mushroom,
    "Env_RuneStone": rune_stone,
    # extra dressing
    "Env_WaterPool": water_pool,
    "Env_WaterPool_Glow": lambda: water_pool(True),
    "Env_Stairs": stairs,
    "Env_Log": log,
    "Env_Stump": stump,
    "Env_Mushrooms": mushrooms,
    "Env_Urn": urn,
    # shared ground dressing
    "Env_GroundTile": ground_tile,
    "Env_GroundMound": ground_mound,
    "Env_GroundPatch": ground_patch,
    "Env_PavingPatch": paving_patch,
    "Env_CaveFloorTile": cave_floor_tile,
    "Env_CryptGroundTile": crypt_ground_tile,
    "Env_MossBorder": moss_border,
    "Env_Pebbles": pebbles,
    # effects (not palette: unit UVs for BilliardRogue/LightShaft)
    "Env_LightShaft": light_shaft,
}

TRI_BUDGET = 800
