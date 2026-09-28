"""Billiard Rogue hero / cue / ball / props / pickups (TDD 14.1) -> FBX in the staging mirror + review renders.

Run through build_heroprops.py (the one command, see that file). Direct use:
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python heroprops_models.py -- --staging <Tools/Staging/Assets> --preview-dir <dir> [--only Cat_Hero,Ball] [--no-render]

Conventions (research/asset-toolchain.md 5.2): model faces Blender -Y (= Unity +Z), +X is the character's LEFT, origin
= pivot, children have identity rotation/scale (rotations are baked into mesh data), 1 m = 1 cell.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import bl_common as bc  # noqa: E402
import hp_mesh as hm  # noqa: E402
import hp_render as hr  # noqa: E402
from hp_mesh import C, Mesh, rot, scl, tr  # noqa: E402

WHITE = C("gray", 15)


# =============================================================================================== cat
# fur shades (orange family only; Palette_CatP2 recolours this row for player 2)
FUR_HEAD, FUR_BODY, FUR_LIMB, FUR_DARK, FUR_STRIPE = 13, 12, 11, 10, 10


def _surface_strip(mesh, bvh, centre, radii, dirs, width, color, lift=0.004, taper=None):
    """Thin decal strip hugging a surface: dirs are ellipsoid-space directions from `centre`, ray-cast inward."""
    pts = []
    for d in dirs:
        d = Vector(d).normalized()
        origin = centre + Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2])) * 2.5
        hit, n, _, _ = bvh.ray_cast(origin, (centre - origin).normalized())
        pts.append((hit, n))
    taper = taper or [1.0] * len(pts)
    left, right = [], []
    for k, (p, n) in enumerate(pts):
        a, b = pts[max(k - 1, 0)][0], pts[min(k + 1, len(pts) - 1)][0]
        t = (b - a).normalized()
        side = n.cross(t).normalized()
        w = width * taper[k] / 2.0
        right.append(p + n * lift - side * w)
        left.append(p + n * lift + side * w)
    infos, _ = mesh.grid([right, left], color, smooth=True)  # winding: t x side = n (outward)
    return infos


def build_cat(mat):
    """Chibi cat knight, ~0.95 m. Hierarchy (one level, see the ear note): Body -> Head, EarL, EarR, PawL, PawR,
    Tail, Cape. Pivots: Body feet, Head neck, ears at their base, paws at the shoulders, tail at its root, cape at
    the neck-back.
    Fur uses ONLY the 'orange' family (Palette_CatP2 recolours it); white = gray 14/15, stays white for P2."""
    # ------------------------------------------------------------------ Body (torso, legs, belt, pauldron)
    m = Mesh()

    def torso_color(fi):
        c, n = fi.c, fi.n
        if 0.188 < c.z < 0.242:
            return C("brown", 4)  # leather belt
        if n.y < -0.5 and c.z < 0.40:
            return C("gray", 14)  # white bib / belly (tuxedo friendly)
        return C("orange", FUR_BODY if c.z > 0.2 else FUR_LIMB)

    m.lathe([(0.0, 0.085), (0.105, 0.09), (0.158, 0.14), (0.170, 0.188), (0.172, 0.242), (0.162, 0.30),
             (0.136, 0.375), (0.092, 0.435), (0.0, 0.47)], 8, torso_color, phase=math.radians(22.5), sy=0.86)

    def foot(co):
        x, y, z = co
        if z < 0.07:
            y -= 0.045 * (1.0 - z / 0.07)
        return (x, y, z)

    for s in (-1, 1):
        m.lathe([(0.0, 0.0), (0.062, 0.012), (0.056, 0.13), (0.0, 0.15)], 6,
                lambda fi: WHITE if fi.c.z < 0.05 else C("orange", FUR_LIMB), matrix=tr(s * 0.085, -0.005, 0.0),
                deform=foot, phase=math.radians(30))
    m.box((-0.032, -0.158, 0.192), (0.032, -0.128, 0.238), C("yellow", 12))  # belt buckle
    # small silver pauldron (domed plate, gold rim) on the RIGHT (cue-arm) shoulder, tilted outward
    m.lathe([(0.098, -0.035), (0.102, -0.008), (0.092, 0.03), (0.06, 0.068), (0.0, 0.082)], 8,
            lambda fi: {0: C("yellow", 11), 1: C("gray", 9)}.get(fi.i, C("gray", 13)),
            matrix=tr(-0.158, 0.0, 0.4) @ rot("Y", -36), closed=False, cap0=False)
    body = m.to_object("Body", mat, origin=(0, 0, 0), sharp_deg=55)

    # ------------------------------------------------------------------ Head
    h = Mesh()
    HC = Vector((0.0, 0.005, 0.645))
    HR = (0.25, 0.215, 0.205)

    def head_deform(co):
        x, y, z = co
        k = max(0.0, 1.0 - abs(z + 0.28) / 0.42)  # cheek fluff band around z = -0.28
        x *= 1.0 + 0.2 * k * abs(x) ** 3
        if y < 0:
            y *= 0.9  # flatter face
        if z < -0.55:
            z = -0.55 + (z + 0.55) * 0.55  # flatter chin
        return (x, y, z)

    def head_color(fi):
        c = fi.c - HC
        u = Vector((c.x / HR[0], c.y / HR[1], c.z / HR[2]))
        if fi.n.y < -0.25 and u.z < -0.2:
            return C("gray", 15)  # white lower face
        return C("orange", FUR_HEAD)

    h.sphere(HC, HR, 12, 8, head_color, deform=head_deform, phase=math.radians(15))
    muzzle_c, muzzle_r = Vector((0.0, -0.176, 0.566)), (0.098, 0.066, 0.062)
    h.sphere(muzzle_c, muzzle_r, 8, 3, WHITE)
    bvh = BVHTree.FromBMesh(h.bm)
    # tabby stripes: thin decals over forehead/crown (+ cheek marks); the head mesh itself stays plain.
    # 7 samples + 6 mm lift so the straight strip segments do not dip under the faceted head.
    stripe = C("orange", FUR_STRIPE)
    for x0, x1, a0, a1, w in ((0.0, 0.0, 50, 162, 0.036), (0.3, 0.46, 58, 128, 0.026),
                              (-0.3, -0.46, 58, 128, 0.026)):
        dirs = []
        for k in range(7):
            t = k / 6.0
            a = math.radians(a0 + (a1 - a0) * t)
            dirs.append((x0 + (x1 - x0) * t, -math.cos(a), math.sin(a)))
        _surface_strip(h, bvh, HC, HR, dirs, w, stripe, lift=0.006, taper=[0.4, 0.9, 1, 1, 1, 0.8, 0.4])
    for s in (-1, 1):
        for z0 in (0.02, -0.16):
            _surface_strip(h, bvh, HC, HR, [(s, -0.2, z0), (s, 0.05, z0 + 0.02), (s, 0.3, z0 + 0.05)], 0.02, stripe,
                           lift=0.006, taper=[0.5, 1.0, 0.5])
    h.sphere((0.0, -0.236, 0.603), (0.03, 0.02, 0.019), 6, 2, C("pink", 11))  # nose
    for s in (-1, 1):
        hit, normal, _, _ = bvh.ray_cast(Vector((s * 0.098, -1.0, 0.668)), Vector((0, 1, 0)))
        frame = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
        eye_c = hit + normal * 0.004
        h.sphere((0, 0, 0), (0.047, 0.062, 0.02), 8, 2, C("indigo", 1), matrix=tr(*eye_c) @ frame)
        up = (frame @ Vector((0, 1, 0, 0))).to_3d()
        side = (frame @ Vector((1, 0, 0, 0))).to_3d()
        g = eye_c + up * 0.023 + side * 0.015 + normal * 0.016
        h.sphere((0, 0, 0), (0.018, 0.018, 0.01), 6, 2, WHITE, matrix=tr(*g) @ frame)
    head = h.to_object("Head", mat, origin=(0, 0, 0.455), parent=body)

    # ------------------------------------------------------------------ Ears (pivot at the base)
    ears = []
    for s, name in ((1, "EarL"), (-1, "EarR")):
        e = Mesh()
        M = tr(s * 0.15, 0.014, 0.768) @ rot("Y", s * 21)
        base = [(-0.08, -0.034, 0.0), (0.08, -0.034, 0.0), (0.066, 0.044, 0.0), (-0.066, 0.044, 0.0)]
        apex = Vector((0.004 * s, 0.016, 0.2))
        e.loft([[M @ Vector(p) for p in base], [M @ apex]],
               lambda fi: C("orange", FUR_HEAD) if fi.n.y < -0.2 else C("orange", FUR_DARK), smooth=False,
               cap0=True)
        bl, br = Vector(base[0]), Vector(base[1])
        fn = (br - bl).cross(apex - bl).normalized()
        if fn.y > 0:
            fn = -fn
        cen = (bl + br + apex) / 3.0
        tri = [cen + (v - cen) * 0.64 + Vector((0, 0, -0.014)) + fn * 0.005 for v in (bl, br, apex)]
        f = e.bm.faces.new([e.bm.verts.new(M @ v) for v in tri])
        e.bm.normal_update()
        if f.normal.dot(M.to_3x3() @ fn) < 0:
            f.normal_flip()
        e.paint(f, C("pink", 12))
        # parent = Body, not Head: Blender's FBX exporter with bake_space_transform mis-bakes grandchildren
        # (level >= 2 gets a bogus 90 deg rotation + offset), so every part stays a direct child of Body.
        ears.append(e.to_object(name, mat, origin=M @ Vector((0, 0.005, 0)), parent=body, sharp_deg=30))

    # ------------------------------------------------------------------ Paws (arm + mitten, pivot at shoulder)
    paws = []
    for s, name in ((1, "PawL"), (-1, "PawR")):
        p = Mesh()
        sh = Vector((s * 0.138, -0.005, 0.385))
        pts = [sh, sh + Vector((s * 0.036, -0.02, -0.08)), Vector((s * 0.19, -0.055, 0.238))]
        p.tube(pts, [0.052, 0.048, 0.044], 6, C("orange", FUR_BODY), cap0=False, cap1=False)
        p.sphere((s * 0.194, -0.07, 0.216), (0.064, 0.066, 0.06), 8, 3, WHITE)
        paws.append(p.to_object(name, mat, origin=sh, parent=body))

    # ------------------------------------------------------------------ Tail (pivot at the base)
    t = Mesh()
    tail_pts = [(0.0, 0.10, 0.125), (0.02, 0.2, 0.088), (0.07, 0.295, 0.105), (0.112, 0.345, 0.2),
                (0.122, 0.352, 0.31), (0.102, 0.332, 0.41), (0.064, 0.295, 0.482), (0.03, 0.258, 0.51)]
    tail_r = [0.04, 0.042, 0.044, 0.046, 0.047, 0.048, 0.045, 0.0]

    def tail_color(fi):
        if fi.i >= 6:
            return C("gray", 15)  # small white tip
        return C("orange", FUR_BODY) if fi.i % 2 == 0 else C("orange", FUR_STRIPE - 1)

    t.tube(tail_pts, tail_r, 5, tail_color, cap0=True)
    tail = t.to_object("Tail", mat, origin=tail_pts[0], parent=body)

    # ------------------------------------------------------------------ Cape (pivot at the neck, top-back)
    c = Mesh()
    Z = [0.462, 0.40, 0.315, 0.228, 0.19, 0.162]
    R = [0.118, 0.17, 0.198, 0.214, 0.22, 0.225]
    T = [150.0, 104.0, 90.0, 82.0, 78.0, 76.0]  # top row wraps round to the front: collar under the chin
    cols = 6
    outer, inner = [], []
    for k in range(len(Z)):
        ro, ri = [], []
        for ci in range(cols):
            u = -1.0 + 2.0 * ci / (cols - 1)
            th = math.radians(T[k]) * u
            r = R[k] + (0.012 * math.cos(u * math.pi * 2.0) if k >= 2 else 0.0)  # soft folds
            z = Z[k] + (0.03 * u * u if k >= 3 else 0.0)  # hem rises at the sides
            if k == 0:
                z -= 0.035 * u * u  # collar dips towards the clasp at the front
            ro.append(Vector((r * math.sin(th), r * math.cos(th) + 0.012, z)))
            ri.append(Vector(((r - 0.02) * math.sin(th), (r - 0.02) * math.cos(th) + 0.012, z)))
        outer.append(ro)
        inner.append(ri)
    vo = [[c.bm.verts.new(p) for p in row] for row in outer]
    vi = [[c.bm.verts.new(p) for p in row] for row in inner]
    infos = []
    nk = len(Z)
    for k in range(nk - 1):
        for ci in range(cols - 1):
            infos.append(hm.FaceInfo(c.bm.faces.new([vo[k][ci], vo[k][ci + 1], vo[k + 1][ci + 1], vo[k + 1][ci]]), k, ci, "out"))
            infos.append(hm.FaceInfo(c.bm.faces.new([vi[k][ci + 1], vi[k][ci], vi[k + 1][ci], vi[k + 1][ci + 1]]), k, ci, "in"))
    for ci in range(cols - 1):  # top + bottom edge strips
        infos.append(hm.FaceInfo(c.bm.faces.new([vi[0][ci], vi[0][ci + 1], vo[0][ci + 1], vo[0][ci]]), -1, ci, "top"))
        infos.append(hm.FaceInfo(c.bm.faces.new([vo[nk - 1][ci], vo[nk - 1][ci + 1], vi[nk - 1][ci + 1], vi[nk - 1][ci]]), nk, ci, "hem"))
    for k in range(nk - 1):  # side edge strips
        infos.append(hm.FaceInfo(c.bm.faces.new([vo[k][0], vo[k + 1][0], vi[k + 1][0], vi[k][0]]), k, 0, "side"))
        infos.append(hm.FaceInfo(c.bm.faces.new([vi[k][cols - 1], vi[k + 1][cols - 1], vo[k + 1][cols - 1], vo[k][cols - 1]]), k, cols, "side"))

    def cape_color(fi):
        if fi.tag == "in":
            return C("yellow", 11) if fi.i == nk - 2 else C("indigo", 4)
        if fi.tag == "hem" or (fi.tag in ("out", "side") and fi.i == nk - 2):
            return C("yellow", 11)  # gold hem
        if fi.tag == "top" or (fi.tag == "out" and fi.i == 0):
            return C("blue", 11)  # collar
        return C("blue", 9)

    c._finish(infos, cape_color, smooth=True, closed=True)
    c.lathe([(0.0, -0.012), (0.032, -0.003), (0.0, 0.012)], 6, C("yellow", 12),
            matrix=tr(0.0, -0.118, 0.418) @ rot("X", 90), smooth=False)  # clasp
    cape = c.to_object("Cape", mat, origin=(0.0, 0.134, 0.462), parent=body, sharp_deg=60)

    parts = [body, head] + ears + paws + [tail, cape]
    return body, parts


# =============================================================================================== cue
def build_cue(mat):
    """1.2 m cue along Unity +Z (Blender -Y), pivot at the butt end. Tip = emissive cyan (child of Stick)."""
    M = rot("X", 90)  # lathe axis +Z -> -Y
    s = Mesh()
    prof = [(0.0, 0.0), (0.036, 0.0), (0.041, 0.012), (0.041, 0.034), (0.041, 0.05), (0.037, 0.40),
            (0.036, 0.425), (0.025, 1.10), (0.025, 1.158)]
    brass = C("yellow", 10)
    band_colors = {0: C("gray", 2), 1: C("gray", 2), 2: C("gray", 3), 3: brass, 4: C("red", 4), 5: brass,
                   6: C("yellow", 13), 7: brass}

    def cue_color(fi):
        if fi.tag == "cap1":
            return brass
        return band_colors.get(fi.i, C("yellow", 13))

    s.lathe(prof, 8, cue_color, matrix=M, phase=math.radians(22.5))
    stick = s.to_object("Stick", mat, origin=(0, 0, 0), sharp_deg=40)
    t = Mesh()
    t.lathe([(0.025, 1.156), (0.031, 1.17), (0.029, 1.19), (0.0, 1.2)], 8, C("cyan", 12, True), matrix=M,
            phase=math.radians(22.5))
    tip = t.to_object("Tip", mat, origin=M @ Vector((0, 0, 1.18)), parent=stick)
    return stick, [stick, tip]


# =============================================================================================== ball
def build_ball(mat):
    """80-tri icosphere, r = 0.5, pivot at the CENTRE (it spins); one neutral white texel in the EMISSIVE half so
    both 'albedo * tint' and 'emission map * glow colour' material setups work."""
    m = Mesh()
    m.icosphere((0, 0, 0), 0.5, 2, C("gray", 15, True), smooth=True)
    ball = m.to_object("Ball", mat, origin=(0, 0, 0))
    return ball, [ball]


# =============================================================================================== props
def build_pillar(mat):
    m = Mesh()
    stone = lambda s: C("gray", s)  # noqa: E731

    def plinth_color(fi):
        if fi.tag == "cap1":
            return stone(10)
        return stone(7 if fi.i == 0 else 9)

    m.block([(0.9, 0.9, 0.1, 0.0), (0.9, 0.9, 0.1, 0.1), (0.8, 0.8, 0.09, 0.165)], plinth_color)
    prof = [(0.315, 0.16), (0.315, 0.225), (0.272, 0.255), (0.272, 0.545), (0.248, 0.565), (0.248, 0.625),
            (0.272, 0.645), (0.272, 0.93), (0.315, 0.96), (0.315, 1.02)]
    shaft_c = {0: stone(10), 1: stone(8), 3: stone(6), 4: stone(4), 5: stone(6), 7: stone(8), 8: stone(10)}

    def shaft_color(fi):
        if fi.i == 4:  # recessed band: carved glyph blocks
            return stone(3) if fi.j % 2 == 0 else stone(7)
        if fi.i in shaft_c:
            return shaft_c[fi.i]
        return stone(9) if fi.j % 2 == 0 else stone(7)  # fluting

    m.lathe(prof, 8, shaft_color, phase=math.radians(22.5), cap0=False, cap1=False, smooth=False)

    def cap_color(fi):
        if fi.tag == "cap1":
            return stone(9)
        return {0: stone(7), 1: stone(10), 2: stone(12), 3: stone(7)}.get(fi.i, stone(10))

    m.block([(0.72, 0.72, 0.08, 1.0), (0.86, 0.86, 0.1, 1.075), (0.86, 0.86, 0.1, 1.2), (0.68, 0.68, 0.07, 1.2),
             (0.68, 0.68, 0.07, 1.18)], cap_color, cap0=False)
    obj = m.to_object("Pillar", mat, origin=(0, 0, 0))
    return obj, [obj]


def build_crate(mat):
    m = Mesh()
    wood = lambda s: C("brown", s)  # noqa: E731
    iron = lambda s: C("gray", s)  # noqa: E731
    layers = [(0.74, 0.74, 0.05, 0.0), (0.8, 0.8, 0.075, 0.03), (0.8, 0.8, 0.075, 0.11), (0.8, 0.8, 0.075, 0.345),
              (0.8, 0.8, 0.075, 0.455), (0.8, 0.8, 0.075, 0.69), (0.8, 0.8, 0.075, 0.77), (0.74, 0.74, 0.05, 0.8),
              (0.62, 0.62, 0.04, 0.8), (0.62, 0.62, 0.04, 0.78)]

    def crate_color(fi):
        if fi.tag == "cap1":
            return wood(6)
        if fi.tag == "cap0":
            return wood(4)
        corner = fi.j % 2 == 1
        if fi.i in (1, 5):
            return iron(7) if not corner else iron(9)  # iron bands
        if fi.i in (0, 6):
            return iron(6)  # bevel edges
        if fi.i == 7:
            return iron(10)  # top rim (flat ring, faces up)
        if fi.i == 8:
            return wood(4)  # recess wall
        if corner:
            return iron(8)  # corner posts
        return wood(9) if fi.i != 3 else wood(8)

    m.block(layers, crate_color)
    for k in range(4):  # diagonal brace plank on each side
        M = rot("Z", 90 * k) @ tr(0.0, -0.404, 0.4) @ rot("Y", 45)
        m.box((-0.36, -0.018, -0.045), (0.36, 0.012, 0.045), wood(11), matrix=M)
    for k in range(3):  # three top planks in the recess
        y0 = -0.29 + k * 0.197
        m.box((-0.29, y0 + 0.006, 0.78), (0.29, y0 + 0.19, 0.795), wood(10 if k != 1 else 9))
    obj = m.to_object("Crate", mat, origin=(0, 0, 0))
    return obj, [obj]


def build_portal(mat):
    """Ring = rune stone circle lying on the floor (runes emissive cyan); Swirl = flat emissive disc, own pivot so
    PortalView can spin it around Y."""
    ring = Mesh()
    segs = 12
    prof = [(0.335, 0.0), (0.46, 0.0), (0.475, 0.07), (0.45, 0.145), (0.425, 0.155), (0.375, 0.155), (0.35, 0.145),
            (0.322, 0.075)]
    rings = [[(r * math.cos(2 * math.pi * j / segs + math.pi / segs),
               r * math.sin(2 * math.pi * j / segs + math.pi / segs), z) for j in range(segs)] for r, z in prof]

    def ring_color(fi):
        if fi.i == 4:  # top centre strip: a rune on every other stone
            return C("cyan", 11, True) if fi.j % 2 == 0 else C("gray", 10)
        if fi.i == 7 and fi.j % 2 == 0:
            return C("cyan", 8, True)  # inner lip glows under each rune
        return {0: C("gray", 5), 1: C("gray", 7), 2: C("gray", 9), 3: C("gray", 10), 5: C("gray", 10),
                6: C("gray", 8), 7: C("gray", 6)}.get(fi.i, C("gray", 8))

    ring.loft(rings, ring_color, smooth=False, wrap=True)
    ring_obj = ring.to_object("Ring", mat, origin=(0, 0, 0))

    sw = Mesh()
    rows = [[(0.0, 0.0, 0.0)]]
    for k, r in enumerate((0.12, 0.235, 0.335)):
        twist = 0.55 * (k + 1)
        rows.append([(r * math.cos(2 * math.pi * j / 12 + twist), r * math.sin(2 * math.pi * j / 12 + twist), 0.0)
                     for j in range(12)])
    arms = [C("magenta", 11, True), C("magenta", 8, True), C("cyan", 11, True), C("cyan", 8, True)]

    def swirl_color(fi):
        if fi.i == 2:  # rows are reversed: i == 2 is the centre fan
            return C("pink", 14, True) if fi.j % 2 == 0 else C("magenta", 13, True)
        return arms[(fi.j + 2 * fi.i) % 4]

    sw.loft([[Vector(p) + Vector((0, 0, 0.035)) for p in row] for row in rows[::-1]], swirl_color,
            smooth=False, closed=False)
    swirl = sw.to_object("Swirl", mat, origin=(0, 0, 0.035))
    return ring_obj, [ring_obj, swirl]


def build_mud(mat):
    """Flat irregular puddle: dry lighter crust ridge around a dark wet centre, bubbles and sky glints."""
    m = Mesh()
    n = 20

    def rad(a):
        return 0.415 + 0.04 * math.sin(3 * a + 0.7) + 0.028 * math.sin(5 * a + 2.1) + 0.014 * math.sin(7 * a + 0.3)

    rows = []
    for f, z in ((1.0, 0.0), (0.9, 0.034), (0.78, 0.018), (0.45, 0.016)):
        rows.append([(rad(2 * math.pi * j / n) * f * math.cos(2 * math.pi * j / n),
                      rad(2 * math.pi * j / n) * f * math.sin(2 * math.pi * j / n), z) for j in range(n)])
    rows.append([(0.02, -0.01, 0.016)])

    def mud_color(fi):
        if fi.i == 0:
            return C("brown", 6)  # outer slope
        if fi.i == 1:
            return C("brown", 8)  # dry crust ridge (light -> readable on dark floors)
        if fi.i == 2:
            return C("brown", 3)
        return C("brown", 2)  # wet centre

    m.loft(rows, mud_color, smooth=False, closed=False)
    for (x, y, r) in ((-0.14, 0.09, 0.06), (0.15, -0.1, 0.05), (0.06, 0.19, 0.038), (-0.05, -0.2, 0.03)):
        m.sphere((x, y, 0.012), (r, r, r * 0.75), 6, 3,
                 lambda fi: C("brown", 9) if fi.n.z > 0.85 else C("brown", 5))
    for (x, y, a, l) in ((0.03, 0.02, 20, 0.07), (-0.2, -0.06, -15, 0.05), (0.2, 0.12, 40, 0.04)):
        m.sphere((0, 0, 0), (l, l * 0.35, 0.004), 4, 2, C("sky", 12), matrix=tr(x, y, 0.017) @ rot("Z", a))
    for (x, y, r, ph) in ((0.37, 0.31, 0.07, 0.3), (-0.41, -0.26, 0.055, 1.1)):
        sat = [[(x + r * (1 + 0.2 * math.sin(3 * j + ph)) * math.cos(2 * math.pi * j / 7),
                 y + r * (1 + 0.2 * math.sin(3 * j + ph)) * math.sin(2 * math.pi * j / 7), 0.0) for j in range(7)],
               [(x, y, 0.02)]]
        m.loft(sat, C("brown", 4), smooth=False, closed=False)
    obj = m.to_object("Mud", mat, origin=(0, 0, 0))
    return obj, [obj]


FLOAT_Z = 0.45  # pickups hover; the file root stays at the floor


PICKUP_TILT = 28.0  # icon pickups lean back so their face looks up at the 58-degree gameplay camera


def pickup_frame():
    """Local icon space (centre at origin, face along -Y/+Y) -> model space: hover height + backward lean."""
    return tr(0, 0, FLOAT_Z) @ rot("X", PICKUP_TILT)


def build_pickup_ball(mat):
    """+1 Ball: glowing mini ball (Orb) inside a gold ring with two gems (Ring); both pivot at the hover centre."""
    M = tr(0, 0, FLOAT_Z) @ rot("X", PICKUP_TILT)
    r = Mesh()
    r.torus(0.26, 0.05, 14, 5, lambda fi: C("yellow", 12) if fi.n.z > -0.3 else C("yellow", 10),
            matrix=rot("X", 90))
    for s in (-1, 1):  # two little gems on the ring sides (they catch the eye when it turns)
        r.sphere((s * 0.26, 0, 0), (0.048, 0.064, 0.048), 4, 2, C("cyan", 11, True), phase=math.pi / 4)
    r.transform(M)
    ring = r.to_object("Ring", mat, origin=M @ Vector((0, 0, 0)), sharp_deg=None)
    o = Mesh()
    o.icosphere((0, 0, FLOAT_Z), 0.16, 2, lambda fi: C("gray", 15, True) if fi.c.z > FLOAT_Z - 0.07
                else C("sky", 13, True))
    orb = o.to_object("Orb", mat, origin=(0, 0, FLOAT_Z))
    return ring, [ring, orb]


def build_pickup_heal(mat):
    """Puffy emissive heart (two lobes + a tapered point) with a small cork on top = heart potion."""
    h = Mesh()

    def heart_color(fi):
        return C("red", 9, True) if abs(fi.n.x) > 0.8 else C("red", 11, True)  # darker only on the flanks

    for s in (-1, 1):
        h.sphere((s * 0.118, 0.0, 0.07), (0.155, 0.1, 0.145), 10, 5, heart_color)
    # lower point: flattened cone from the lobes' equator down to the tip
    h.lathe([(0.0, -0.245), (0.135, -0.09), (0.225, 0.02), (0.0, 0.065)], 10, heart_color,
            matrix=scl(1.0, 0.46, 1.0), phase=math.radians(18))
    for s in (-1, 1):  # glossy glint on the upper-left lobe, both faces
        h.sphere((0, 0, 0), (0.042, 0.032, 0.012), 6, 2, C("gray", 15, True),
                 matrix=tr(0.135, s * 0.094, 0.13) @ rot("X", 90) @ rot("Z", 35))
    M = pickup_frame()
    h.transform(M)
    heart = h.to_object("Heart", mat, origin=M @ Vector((0, 0, 0)), sharp_deg=None)
    ck = Mesh()
    top = 0.175
    ck.lathe([(0.0, top - 0.03), (0.038, top - 0.03), (0.038, top + 0.02)], 6, C("sky", 12))  # glass neck
    ck.lathe([(0.05, top + 0.02), (0.055, top + 0.075), (0.0, top + 0.08)], 6,
             lambda fi: C("brown", 10) if fi.i == 1 else C("brown", 7))  # cork
    ck.transform(M)
    cork = ck.to_object("Cork", mat, origin=M @ Vector((0, 0, top)), parent=heart, sharp_deg=40)
    return heart, [heart, cork]


def bolt_outline(h=0.66, w=1.45):
    pts = [(-0.05, 0.5), (0.28, 0.5), (0.10, 0.10), (0.27, 0.10), (-0.15, -0.5), (-0.02, -0.04), (-0.21, -0.04)]
    return hm.ensure_ccw([(x * h * w, z * h) for x, z in pts])


def build_pickup_power(mat):
    """Emissive orange lightning crystal: bevelled bolt with a hot yellow face."""
    m = Mesh()
    layers = [(0.03, -0.085), (0.0, -0.048), (0.0, 0.048), (0.03, 0.085)]

    def bolt_color(fi):
        if fi.tag in ("cap0", "cap1"):
            return C("yellow", 13, True)
        if fi.i == 1:
            return C("orange", 9, True)  # side walls
        return C("orange", 12, True)  # bevels

    m.extrude(bolt_outline(), layers, bolt_color, smooth=False)
    M = pickup_frame()
    m.transform(M)
    obj = m.to_object("Crystal", mat, origin=M @ Vector((0, 0, 0)))
    return obj, [obj]


MODELS = {
    # name: (staging sub-folder, builder, in-game footprint note)
    "Cat_Hero": ("Player", build_cat),
    "Cue_Stick": ("Player", build_cue),
    "Ball": ("Balls", build_ball),
    "Prop_Pillar": ("Props", build_pillar),
    "Prop_Crate": ("Props", build_crate),
    "Prop_Portal": ("Props", build_portal),
    "Prop_Mud": ("Props", build_mud),
    "Pickup_ExtraBall": ("Props", build_pickup_ball),
    "Pickup_Heal": ("Props", build_pickup_heal),
    "Pickup_Power": ("Props", build_pickup_power),
}


# =============================================================================================== helpers
def hierarchy(obj, depth=0):
    out = ["  " * depth + obj.name]
    for ch in sorted(obj.children, key=lambda o: o.name):
        out += hierarchy(ch, depth + 1)
    return out


def model_stats(parts):
    lo, hi = hr.bounds(parts)
    return {
        "tris": bc.tri_count(parts),
        "parts": {p.name: bc.tri_count([p]) for p in parts},
        "size": [round(hi.x - lo.x, 3), round(hi.y - lo.y, 3), round(hi.z - lo.z, 3)],
        "min_z": round(lo.z, 3),
        "max_z": round(hi.z, 3),
    }


def move_to_collection(objs, col):
    for o in objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        col.objects.link(o)


def main():
    def extra(p):
        p.add_argument("--staging", required=True, help="Tools/Staging/Assets")
        p.add_argument("--preview-dir", required=True)
        p.add_argument("--only", default="")
        p.add_argument("--no-render", action="store_true")
        p.add_argument("--p2-palette", default=None)
        p.add_argument("--emission-png", default=None)
        p.add_argument("--context-staging", default=None, help="staged enemies for the mock (read-only)")

    args = bc.parse_args(extra)
    pal_dir = os.path.dirname(os.path.abspath(args.palette_png))
    emission_png = args.emission_png or os.path.join(pal_dir, "Palette_Emission.png")
    p2_png = args.p2_palette or os.path.join(pal_dir, "Palette_CatP2.png")
    bc.reset_scene()
    bc.load_palette(args.palette_json)
    mat = hr.toon_material("M_Palette", args.palette_png, emission_png)
    only = set(filter(None, args.only.split(",")))
    stats = {}
    built = {}
    for name, (folder, builder) in MODELS.items():
        if only and name not in only:
            continue
        col = bpy.data.collections.new("SRC_" + name)
        bpy.context.scene.collection.children.link(col)
        root, parts = builder(mat)
        move_to_collection(parts, col)
        bpy.context.view_layer.update()
        path = os.path.join(args.staging, "Models", "BilliardRogue", folder, name + ".fbx")
        for p in parts:
            if p.parent is not None and p.parent.parent is not None:
                raise RuntimeError("%s/%s: keep parts one level deep (FBX bake_space_transform bug)" % (name, p.name))
        secs, changed = bc.export_fbx_if_changed(path, parts)
        st = model_stats(parts)
        st.update(fbx=path, fbx_bytes=os.path.getsize(path), fbx_changed=changed, hierarchy=hierarchy(root))
        for p in parts:  # free the part names ("Ring" is used by two models) - object names are file-unique
            p.name = p.name + "@" + name
        stats[name] = st
        built[name] = (root, parts, col)
        print("ASSET_STATS", name, json.dumps({k: st[k] for k in ("tris", "size", "fbx_changed")}))

    os.makedirs(args.preview_dir, exist_ok=True)
    with open(os.path.join(args.preview_dir, "stats.json" if not only else "stats_partial.json"), "w") as f:
        json.dump(stats, f, indent=1)
    if args.no_render:
        return
    import hp_preview  # noqa: E402  (render-only code lives in its own module)
    hp_preview.render_all(args, built, mat, emission_png, p2_png, stats)


main()
