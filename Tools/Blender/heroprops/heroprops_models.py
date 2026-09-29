"""Billiard Rogue hero / cue / ball / props / pickups (TDD 14.1) -> FBX in the staging mirror + review renders.

Run through build_heroprops.py (the one command, see that file). Direct use:
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python heroprops_models.py -- --staging <Tools/Staging/Assets> --preview-dir <dir> [--only Cat_Hero,Ball] [--no-render]

Conventions (research/asset-toolchain.md 5.2): model faces Blender -Y (= Unity +Z), +X is the character's LEFT, origin
= pivot, children have identity rotation/scale (rotations are baked into mesh data), 1 m = 1 cell. Hierarchies may be
nested (Cat: Body > Head > EarL/EarR); export goes through bl_common.export_fbx_nested_if_changed.

Integration contract (Presentation / WorldPrefabsBuilder):
  * Emission: every part uses M_Palette (M_Palette_CatP2 for the P2 cat) WITH Palette_Emission as _EmissionMap; the
    palette's right half is what glows, so no per-part flag is required. Parts that carry emissive faces and whose
    names match the TDD 14.1 rule: Runes_Emissive (Pillar, Portal), Plus_Emissive, Cross_Emissive, PommelGem.
    Parts that need a per-model override if the builder flags by name: Cue_Stick/Tip, Prop_Portal/Swirl.
  * Pickups (Pickup_*) are built to face the gameplay camera at IDENTITY rotation (icon face toward Unity -Z, top
    leaning 28 deg back toward +Z so it looks up at the 58 deg camera). Never rotate them 180 like enemies. Bob on Y;
    do not spin the whole model (the lean would wobble). The file root is at the floor; the icon part hovers at 0.45.
  * Ball.fbx: pivot at the sphere CENTRE (it rolls/spins), radius 0.5 -> BallView scales by 0.4 and lifts by 0.2.
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
# fur shades (orange family only; Palette_CatP2 recolours this row to charcoal for player 2)
FUR_HEAD, FUR_BODY, FUR_LIMB, FUR_DARK, FUR_STRIPE, EAR_BACK = 13, 12, 11, 10, 10, 8
# ears: base centre (y, z), height, forward tilt (deg). Tilting forward (-Y = north) makes the tips project ABOVE
# the head outline from the 58 deg camera behind the cat; about 0.06 m of the base is buried in the head.
EAR_Y, EAR_Z, EAR_H, EAR_FWD = -0.02, 0.75, 0.31, 20.0


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


def _project(bvh, p, n, lift):
    """Drop point p onto the surface along -n (ray from outside) and lift it by `lift` along the hit normal."""
    hit, hn, _, _ = bvh.ray_cast(Vector(p) + Vector(n) * 0.3, -Vector(n))
    return hit + hn * lift


def build_cat(mat):
    """Chibi cat knight, ~1.0 m to the ear tips. Hierarchy: Body -> Head -> EarL, EarR; Body -> PawL, PawR, Tail,
    Cape. Pivots: Body feet, Head neck, ears at their base, paws at the shoulders, tail at its root, cape at the
    neck-back. Designed for the BACK view (the camera sees the striking cat from behind at a 58 deg pitch): big
    dark-backed ears that break the head outline, horizontal tabby bars on the back of the head, a tail curling up
    beside the cape with a white tip, a gold crest on the cape.
    Fur uses ONLY the 'orange' family (Palette_CatP2 recolours it); white = gray 14/15, stays white for P2; the cape
    uses only the 'blue' family (P2 recolours it crimson)."""
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
    # small silver pauldron with a gold rim on the RIGHT (cue-arm) shoulder, tilted outward
    m.lathe([(0.064, -0.02), (0.066, -0.004), (0.058, 0.02), (0.036, 0.042), (0.0, 0.05)], 6,
            lambda fi: {0: C("yellow", 10), 1: C("yellow", 12)}.get(fi.i, C("gray", 13)),
            matrix=tr(-0.162, 0.0, 0.418) @ rot("Y", -40), closed=False, cap0=False)
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
    stripe = C("orange", FUR_STRIPE)
    # back of the head (what the gameplay camera sees): three short, chunky horizontal tabby bars
    for elev, half, w in ((64.0, 30.0, 0.05), (41.0, 40.0, 0.056), (17.0, 44.0, 0.05)):
        dirs = []
        for k in range(4):
            az = math.radians(-half + 2 * half * k / 3.0)
            e = math.radians(elev)
            dirs.append((math.sin(az) * math.cos(e), math.cos(az) * math.cos(e), math.sin(e)))
        _surface_strip(h, bvh, HC, HR, dirs, w, stripe, lift=0.007, taper=[0.35, 1.0, 1.0, 0.35])
    # forehead 'M' dashes (front / idle view + portrait) and cheek marks
    for x0, a0, a1, w in ((0.0, 44.0, 80.0, 0.034), (0.26, 50.0, 74.0, 0.026), (-0.26, 50.0, 74.0, 0.026)):
        dirs = [(x0, -math.cos(math.radians(a)), math.sin(math.radians(a))) for a in (a0, (a0 + a1) / 2, a1)]
        _surface_strip(h, bvh, HC, HR, dirs, w, stripe, lift=0.006, taper=[0.4, 1.0, 0.5])
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

    # ------------------------------------------------------------------ Ears (children of Head, pivot at the base)
    # big pyramids: 0.20 m base, 0.27 m tall, 10 deg outward tilt, tip slightly forward so it clears the head
    # outline from the 58 deg camera; the back faces are 5 shades darker than the head so they read against it.
    ears = []
    for s, name in ((1, "EarL"), (-1, "EarR")):
        e = Mesh()
        M = tr(s * 0.125, EAR_Y, EAR_Z) @ rot("Y", s * 10) @ rot("X", EAR_FWD)
        base = [Vector((s * x, y, 0.0)) for x, y in ((-0.105, -0.05), (0.105, -0.05), (0.09, 0.052), (-0.09, 0.052))]
        apex = Vector((s * 0.012, -0.03, EAR_H))
        e.loft([[M @ p for p in base], [M @ apex]],
               lambda fi: C("orange", FUR_HEAD) if (fi.n.y < -0.4 or fi.tag == "cap0") else C("orange", EAR_BACK),
               smooth=False, cap0=True)
        bl, br = base[0], base[1]
        fn = (br - bl).cross(apex - bl).normalized()
        if fn.y > 0:
            fn = -fn
        cen = (bl + br + apex) / 3.0
        tri = [M @ (cen + (v - cen) * 0.62 + Vector((0, 0, -0.012)) + fn * 0.005) for v in (bl, br, apex)]
        e.polygon(tri, C("pink", 12), normal=M.to_3x3() @ fn)
        ears.append(e.to_object(name, mat, origin=M @ Vector((0, 0.005, 0)), parent=head, sharp_deg=30))

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
    # curls out to the cat's left (screen-left from behind, away from the cue) and up beside the cape
    t = Mesh()
    tail_pts = [(0.0, 0.10, 0.13), (0.07, 0.19, 0.10), (0.17, 0.235, 0.125), (0.25, 0.22, 0.21), (0.285, 0.18, 0.32),
                (0.278, 0.13, 0.425), (0.245, 0.09, 0.51), (0.2, 0.06, 0.565)]
    tail_r = [0.04, 0.042, 0.044, 0.046, 0.048, 0.05, 0.048, 0.0]

    def tail_color(fi):
        if fi.i >= 5:
            return C("gray", 15)  # white tip (last ~0.15 m)
        return C("orange", FUR_BODY) if fi.i % 2 == 0 else C("orange", FUR_STRIPE - 1)

    t.tube(tail_pts, tail_r, 5, tail_color, cap0=True)
    tail = t.to_object("Tail", mat, origin=tail_pts[0], parent=body)

    # ------------------------------------------------------------------ Cape (pivot at the neck, top-back)
    c = Mesh()
    Z = [0.466, 0.40, 0.315, 0.20, 0.165]
    R = [0.13, 0.17, 0.198, 0.218, 0.224]
    T = [158.0, 104.0, 90.0, 80.0, 77.0]  # top row wraps round to the front: collar framing the neck
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
                z -= 0.028 * u * u  # collar dips towards the clasp at the front
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
            return C("yellow", 11) if fi.i == nk - 2 else C("blue", 5)
        if fi.tag == "hem" or (fi.tag in ("out", "side") and fi.i == nk - 2):
            return C("yellow", 11)  # gold hem
        if fi.tag == "top" or (fi.tag == "out" and fi.i == 0):
            return C("blue", 11)  # collar
        return C("blue", 9)

    c._finish(infos, cape_color, smooth=True, closed=True)
    cape_bvh = BVHTree.FromBMesh(c.bm)
    # gold crest on the cape back (knight identity from behind): a shield projected onto the folded cloth
    cz, cn = Vector((0.0, 0.2, 0.352)), Vector((0.0, 0.905, 0.426)).normalized()
    upv = Vector((0.0, -cn.z, cn.y))
    shield = [(-0.058, 0.05), (0.058, 0.05), (0.058, -0.004), (0.0, -0.066), (-0.058, -0.004)]
    pts = [_project(cape_bvh, cz + Vector((x, 0, 0)) + upv * y, cn, 0.007) for x, y in shield]
    c.polygon(pts, C("yellow", 12), normal=cn, centre=True)
    c.lathe([(0.0, -0.012), (0.032, -0.003), (0.0, 0.012)], 6, C("yellow", 12),
            matrix=tr(0.0, -0.13, 0.428) @ rot("X", 90), smooth=False)  # clasp
    cape = c.to_object("Cape", mat, origin=(0.0, 0.134, 0.466), parent=body, sharp_deg=60)

    parts = [body, head] + ears + paws + [tail, cape]
    return body, parts


# =============================================================================================== cue
def build_cue(mat):
    """1.2 m cue along Unity +Z (Blender -Y), pivot at the butt end. 0.12 m butt, 0.09-0.10 m shaft (>= 3 px at the
    game camera so it does not crawl when it re-aims). Tip (child of Stick) = glowing cyan gem + emissive ferrule
    band: the bloom point where the aim starts. Tip is emissive by palette; name-flag override: Cue_Stick/Tip."""
    M = rot("X", 90)  # lathe axis +Z -> -Y
    s = Mesh()
    prof = [(0.0, 0.0), (0.052, 0.0), (0.06, 0.014), (0.06, 0.05), (0.06, 0.07), (0.057, 0.40), (0.055, 0.425),
            (0.055, 0.445), (0.046, 1.06)]
    brass = C("yellow", 10)
    band_colors = {0: C("gray", 3), 1: C("gray", 3), 2: C("gray", 4), 3: brass, 4: C("red", 6), 5: brass,
                   6: C("yellow", 13), 7: C("yellow", 13)}

    def cue_color(fi):
        if fi.tag == "cap1":
            return C("yellow", 12)
        return band_colors.get(fi.i, C("yellow", 13))

    s.lathe(prof, 8, cue_color, matrix=M, phase=math.radians(22.5))
    stick = s.to_object("Stick", mat, origin=(0, 0, 0), sharp_deg=40)
    t = Mesh()
    t.lathe([(0.046, 1.058), (0.052, 1.066), (0.052, 1.094), (0.06, 1.13), (0.0, 1.2)], 8,
            lambda fi: C("cyan", 9, True) if fi.i <= 1 else C("cyan", 13, True), matrix=M, phase=math.radians(22.5))
    tip = t.to_object("Tip", mat, origin=M @ Vector((0, 0, 1.13)), parent=stick)
    return stick, [stick, tip]


# =============================================================================================== ball
def build_ball(mat):
    """80-tri icosphere, r = 0.5, pivot at the CENTRE (it spins; BallView lifts it by its scaled radius). One
    neutral white texel in the EMISSIVE half so both 'albedo * tint' and 'emission map * glow colour' setups work."""
    m = Mesh()
    m.icosphere((0, 0, 0), 0.5, 2, C("gray", 15, True), smooth=True)
    ball = m.to_object("Ball", mat, origin=(0, 0, 0))
    return ball, [ball]


# =============================================================================================== props
def build_pillar(mat):
    """Carved stone column, 1.415 m (Unity x1.05 -> ~1.49 m: well above the balls; slim, so the row behind still
    shows around it from the ~52 deg camera). Reads as a column at ~29 px per cell through its silhouette, not
    texture: a low square plinth, a torus base, a slim tapering shaft with 8 carved flutes (light ridges, dark grooves
    = vertical stripes), a dark neck under a flared echinus and a ROUND abacus (the old square cap read as a cube /
    crate from above). Tops stay mid-grey (lit tops bloomed to white on the old block); moss only in small side
    patches (the old camera-facing moss rings read as a green belt). Child part Runes_Emissive: a dim teal rune
    lozenge on each cardinal ridge (one faces the camera). <= 300 tris (TDD 14.1)."""
    m = Mesh()
    stone = lambda s: C("gray", s)  # noqa: E731
    moss = C("green", 5)

    def plinth_color(fi):
        if fi.tag == "cap1":
            return stone(6)
        if fi.i == 1 and fi.j in (0, 7):
            return moss  # moss creeping over the plinth's chamfered edge on the right-hand side
        return stone(5 if fi.i == 0 else 7)

    m.block([(0.74, 0.74, 0.08, 0.0), (0.74, 0.74, 0.08, 0.1), (0.6, 0.6, 0.07, 0.15)], plinth_color, cap0=False)
    # torus base: an outward band and a sun-facing band tucking into the shaft
    m.lathe([(0.25, 0.15), (0.265, 0.18), (0.205, 0.225)], 12, lambda fi: stone(7 if fi.i == 0 else 8),
            phase=math.radians(15.0), cap0=False, cap1=False, smooth=False)

    # fluted shaft: 8 flutes, each = a flat ridge (30 deg) + a V groove (15 deg), linear taper (one band)
    z0, z1 = 0.22, 1.235
    r_ridge, r_groove = (0.2, 0.178), (0.168, 0.15)
    half = math.radians(15.0)

    def flute_ring(z, rr, rg):
        pts = []
        for k in range(8):
            a = math.radians(45.0 * k)  # ridge centres at 0, 45, 90 ... (90 deg = Blender +Y = toward the camera)
            for ang, r in ((a - half, rr), (a + half, rr), (a + math.radians(22.5), rg)):
                pts.append((math.cos(ang) * r, math.sin(ang) * r, z))
        return pts

    m.loft([flute_ring(z0, r_ridge[0], r_groove[0]), flute_ring(z1, r_ridge[1], r_groove[1])],
           lambda fi: stone(9) if fi.j % 3 == 0 else stone(5), smooth=False)

    # capital: astragal ring, dark neck (shadow line under the flare), echinus, round abacus with a bevelled top
    def capital_color(fi):
        if fi.tag == "cap1":
            return stone(7)
        if fi.i == 4 and fi.j in (0, 11):
            return moss  # a moss patch on the abacus bevel (right-hand side)
        return {0: stone(9), 1: stone(4), 2: stone(9), 3: stone(6)}.get(fi.i, stone(8))

    m.lathe([(0.2, 1.22), (0.2, 1.255), (0.178, 1.27), (0.26, 1.345), (0.275, 1.395), (0.25, 1.415)], 12,
            capital_color, phase=math.radians(15.0), cap0=False, cap1=True, smooth=False)
    obj = m.to_object("Pillar", mat, origin=(0, 0, 0))

    # runes: a lozenge on each cardinal ridge face, lying in the (tapered) ridge plane, slightly lifted
    r = Mesh()
    zc, hh, hw = 0.72, 0.12, 0.042

    def on_ridge(n, side, s, z):
        t = (z - z0) / (z1 - z0)
        d = (r_ridge[0] + (r_ridge[1] - r_ridge[0]) * t) * math.cos(half) + 0.004
        return n * d + side * s + Vector((0.0, 0.0, z))

    for k in range(4):
        a = math.radians(90.0 * k)
        n = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-n.y, n.x, 0.0))
        glyph = [on_ridge(n, side, 0.0, zc + hh), on_ridge(n, side, -hw, zc), on_ridge(n, side, 0.0, zc - hh),
                 on_ridge(n, side, hw, zc)]
        r.polygon(glyph, C("teal", 10, True), normal=n)
    runes = r.to_object("Runes_Emissive", mat, origin=(0, 0, zc), parent=obj)
    return obj, [obj, runes]


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
    """Ring = lit rune-stone circle lying on the floor; its glowing runes are the child part Runes_Emissive.
    Swirl = flat emissive disc (shades 8-11 so live balls stay the brightest thing on the field), own pivot so
    PortalView can spin it around Y. Name-flag override needed only for Prop_Portal/Swirl."""
    ring = Mesh()
    segs = 12
    prof = [(0.335, 0.0), (0.46, 0.0), (0.475, 0.07), (0.45, 0.145), (0.425, 0.155), (0.375, 0.155), (0.35, 0.145),
            (0.322, 0.075)]
    rings = [[(r * math.cos(2 * math.pi * j / segs + math.pi / segs),
               r * math.sin(2 * math.pi * j / segs + math.pi / segs), z) for j in range(segs)] for r, z in prof]

    def ring_color(fi):
        if fi.i == 4:  # top centre strip: dark rune sockets under the glowing glyphs
            return C("gray", 5) if fi.j % 2 == 0 else C("gray", 10)
        return {0: C("gray", 5), 1: C("gray", 7), 2: C("gray", 9), 3: C("gray", 10), 5: C("gray", 10),
                6: C("gray", 8), 7: C("gray", 6)}.get(fi.i, C("gray", 8))

    ring.loft(rings, ring_color, smooth=False, wrap=True)
    ring_obj = ring.to_object("Ring", mat, origin=(0, 0, 0))

    rn = Mesh()  # one glyph plate over every other top stone, slightly raised
    for j in range(0, segs, 2):
        a0 = 2 * math.pi * j / segs + math.pi / segs
        a1 = a0 + 2 * math.pi / segs
        am = (a0 + a1) / 2
        pts = []
        for r_, a in ((0.442, am - 0.16), (0.442, am + 0.16), (0.358, am + 0.13), (0.358, am - 0.13)):
            pts.append((r_ * math.cos(a), r_ * math.sin(a), 0.159))
        rn.polygon(pts, C("cyan", 10, True), normal=(0, 0, 1))
    runes = rn.to_object("Runes_Emissive", mat, origin=(0, 0, 0.159), parent=ring_obj)

    sw = Mesh()
    rows = [[(0.0, 0.0, 0.0)]]
    for k, r in enumerate((0.12, 0.235, 0.335)):
        twist = 0.55 * (k + 1)
        rows.append([(r * math.cos(2 * math.pi * j / 12 + twist), r * math.sin(2 * math.pi * j / 12 + twist), 0.0)
                     for j in range(12)])
    arms = [C("magenta", 10, True), C("magenta", 8, True), C("cyan", 10, True), C("cyan", 8, True)]

    def swirl_color(fi):
        if fi.i == 2:  # rows are reversed: i == 2 is the centre fan
            return C("pink", 11, True) if fi.j % 2 == 0 else C("magenta", 11, True)
        return arms[(fi.j + 2 * fi.i) % 4]

    sw.loft([[Vector(p) + Vector((0, 0, 0.035)) for p in row] for row in rows[::-1]], swirl_color,
            smooth=False, closed=False)
    swirl = sw.to_object("Swirl", mat, origin=(0, 0, 0.035))
    return ring_obj, [ring_obj, runes, swirl]


def build_mud(mat):
    """Flat irregular puddle, everything at z >= 0: light dry crust ridge around a dark wet centre, bubble domes
    sitting on the wet surface, big glossy sky-blue highlight streaks, splash drops at the rim."""
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
            return C("brown", 7)  # outer slope
        if fi.i == 1:
            return C("brown", 10)  # dry crust ridge (light -> readable on dark and stone floors)
        if fi.i == 2:
            return C("brown", 4)
        return C("brown", 2)  # wet centre

    m.loft(rows, mud_color, smooth=False, closed=False)
    for (x, y, r) in ((-0.14, 0.09, 0.062), (0.15, -0.1, 0.05), (0.06, 0.2, 0.04), (-0.05, -0.2, 0.034)):
        m.lathe([(r, 0.0), (r * 0.8, r * 0.42), (0.0, r * 0.62)], 6,
                lambda fi: C("brown", 8) if fi.i == 1 else C("brown", 5), matrix=tr(x, y, 0.014), cap0=False)
    # glossy wet highlights: tapered curved streaks lying on the wet surface
    for (x, y, a0, span, rr, w) in ((0.02, 0.0, 195.0, 80.0, 0.21, 0.05), (0.0, 0.02, 15.0, 60.0, 0.17, 0.042),
                                    (0.0, 0.0, 292.0, 40.0, 0.27, 0.034)):
        inner_, outer_ = [], []
        for k in range(4):
            a = math.radians(a0 + span * k / 3.0)
            wk = w * (0.35 if k in (0, 3) else 1.0) / 2
            inner_.append((x + (rr - wk) * math.cos(a), y + (rr - wk) * math.sin(a), 0.019))
            outer_.append((x + (rr + wk) * math.cos(a), y + (rr + wk) * math.sin(a), 0.019))
        m.grid([outer_, inner_], C("sky", 14), smooth=False)  # outer first -> faces up
    for (x, y, r, ph) in ((0.37, 0.31, 0.07, 0.3), (-0.41, -0.26, 0.055, 1.1)):  # splats beyond the rim
        sat = [[(x + r * (1 + 0.2 * math.sin(3 * j + ph)) * math.cos(2 * math.pi * j / 7),
                 y + r * (1 + 0.2 * math.sin(3 * j + ph)) * math.sin(2 * math.pi * j / 7), 0.0) for j in range(7)],
               [(x, y, 0.02)]]
        m.loft(sat, C("brown", 5), smooth=False, closed=False)
    for (x, y, r) in ((0.46, -0.08, 0.03), (-0.2, 0.43, 0.026), (0.12, -0.45, 0.022)):  # splash drops
        m.lathe([(r, 0.0), (0.0, r * 0.8)], 5, C("brown", 6), matrix=tr(x, y, 0.0), cap0=False, smooth=False)
    obj = m.to_object("Mud", mat, origin=(0, 0, 0))
    return obj, [obj]


FLOAT_Z = 0.45  # pickups hover; the file root stays at the floor
PICKUP_TILT = 28.0  # icon pickups lean back so their face looks up at the 58-degree gameplay camera


def pickup_frame():
    """Local icon space (centre at origin, face toward +Y = the camera) -> model space: hover height + lean back.
    Pickups are placed at IDENTITY rotation (never turned 180 like enemies); bob on Y, no whole-model spin."""
    return tr(0, 0, FLOAT_Z) @ rot("X", PICKUP_TILT)


FACE = rot("X", -90)  # lathe/extrude helpers: local +Z -> +Y (towards the camera)


def plus_outline(span, bar):
    a, b = span / 2.0, bar / 2.0
    return hm.ensure_ccw([(b, a), (-b, a), (-b, b), (-a, b), (-a, -b), (-b, -b), (-b, -a), (b, -a), (b, -b), (a, -b),
                          (a, b), (b, b)])


def build_pickup_ball(mat):
    """+1 Ball: a chunky lit gold coin (Coin, 0.57 m) carrying a cel-shaded white mini ball with the blue paw print
    of Ball_Basic, and a big emissive teal '+' (Plus_Emissive, child). Only the '+' glows, so it never blooms like a
    live ball. Identity rotation faces the camera (see pickup_frame)."""
    M = pickup_frame()
    coin = Mesh()

    def coin_color(fi):
        if fi.i == 0:
            return C("yellow", 9)  # back face
        if fi.i in (1, 2):
            return C("yellow", 11)  # rim
        if fi.i == 3:
            return C("yellow", 14)  # front bevel ring (bright gold edge)
        return C("orange", 11)  # recessed centre: deeper amber gold so the white ball pops

    coin.lathe([(0.0, -0.035), (0.26, -0.035), (0.285, 0.0), (0.265, 0.036), (0.212, 0.04), (0.0, 0.032)], 12,
               coin_color, matrix=FACE, smooth=False)
    bc_ = Vector((0.05, 0.08, -0.045))  # lower-LEFT on screen (+X)
    coin.sphere(bc_, (0.135, 0.135, 0.135), 8, 5, lambda fi: C("gray", 15) if fi.n.z > -0.2 else C("gray", 12))
    # paw print on the mini ball's camera-facing side: pad + three beans, projected onto the sphere
    ball_bvh = BVHTree.FromBMesh(coin.bm)
    nrm = Vector((0.0, 1.0, 0.35)).normalized()  # towards the camera in icon space
    upv = Vector((0.0, -nrm.z, nrm.y))
    sidev = Vector((1.0, 0.0, 0.0))
    paw = C("blue", 9)
    beans = [((0.0, -0.022), 0.034, 0.028, 6)] + [((dx, dy), 0.017, 0.019, 5)
                                                   for dx, dy in ((-0.042, 0.022), (0.0, 0.045), (0.042, 0.022))]
    for (dx, dy), rx, ry, n_ in beans:
        c0 = bc_ + nrm * 0.135 + sidev * dx + upv * dy
        pts = [c0 + sidev * (rx * math.cos(2 * math.pi * k / n_)) + upv * (ry * math.sin(2 * math.pi * k / n_))
               for k in range(n_)]
        coin.polygon([_project(ball_bvh, p, nrm, 0.004) for p in pts], paw, normal=nrm)
    coin.transform(M)
    coin_obj = coin.to_object("Coin", mat, origin=M @ Vector((0, 0, 0)), sharp_deg=None)
    plus = Mesh()
    plus.extrude(plus_outline(0.27, 0.09), [(0.0, -0.035), (0.0, 0.035)],
                 lambda fi: C("teal", 13, True) if fi.tag == "cap1" else C("teal", 10, True),
                 matrix=tr(-0.17, 0.075, 0.17))  # upper-RIGHT on screen
    plus.transform(M)
    plus_obj = plus.to_object("Plus_Emissive", mat, origin=M @ Vector((-0.17, 0.075, 0.17)), parent=coin_obj)
    return coin_obj, [coin_obj, plus_obj]


def build_pickup_heal(mat):
    """Heal: a puffy LIT red heart (Heart, ~0.62 m wide, 4-band cel shading) with a glowing white medical cross
    (Cross_Emissive, child) as the only emissive part. Identity rotation faces the camera (see pickup_frame)."""
    h = Mesh()
    S = 1.16

    def heart_color(fi):
        if fi.n.y < -0.5:
            return C("red", 7)  # back
        return C("red", 9) if abs(fi.n.x) > 0.8 else C("red", 11)

    for s in (-1, 1):
        h.sphere((s * 0.118 * S, 0.0, 0.07 * S), (0.155 * S, 0.1 * S, 0.145 * S), 10, 5, heart_color)
    h.lathe([(0.0, -0.245 * S), (0.135 * S, -0.09 * S), (0.225 * S, 0.02 * S), (0.0, 0.065 * S)], 10, heart_color,
            matrix=scl(1.0, 0.46, 1.0), phase=math.radians(18))
    h.sphere((0, 0, 0), (0.045, 0.03, 0.012), 6, 2, C("gray", 15),
             matrix=tr(0.17, 0.112, 0.165) @ rot("X", 90) @ rot("Z", 35))  # lit glint, upper-left lobe on screen
    M = pickup_frame()
    h.transform(M)
    heart = h.to_object("Heart", mat, origin=M @ Vector((0, 0, 0)), sharp_deg=None)
    cr = Mesh()
    cc = Vector((0.0, 0.118, 0.035))
    cr.extrude(plus_outline(0.2, 0.068), [(0.0, -0.025), (0.0, 0.025)],
               lambda fi: C("gray", 15, True) if fi.tag == "cap1" else C("pink", 12, True),
               matrix=tr(*cc))
    cr.transform(M)
    cross = cr.to_object("Cross_Emissive", mat, origin=M @ cc, parent=heart)
    return heart, [heart, cross]


def build_pickup_power(mat):
    """Power ('next ball deals 2x on its first hit'): a short broad knight sword, tip up-right - silver lit blade
    with a bright ridge, gold guard, leather grip and a glowing red pommel gem (PommelGem, child). Deliberately
    not a bolt / not yellow (Ball_Thunder). Identity rotation faces the camera (see pickup_frame)."""
    m = Mesh()
    blade = hm.ensure_ccw([(-0.088, -0.02), (0.088, -0.02), (0.094, 0.29), (0.0, 0.43), (-0.094, 0.29)])

    def blade_color(fi):
        if fi.tag == "cap1":
            return C("gray", 14)  # front ridge flat (camera side)
        if fi.tag == "cap0":
            return C("gray", 10)
        return {0: C("gray", 9), 1: C("gray", 7), 2: C("gray", 12)}[fi.i]  # back bevel, edge, front bevel

    m.extrude(blade, [(0.048, -0.034), (0.0, -0.009), (0.0, 0.009), (0.048, 0.034)], blade_color)
    guard = hm.ensure_ccw([(-0.22, -0.03), (-0.175, -0.085), (0.175, -0.085), (0.22, -0.03), (0.17, -0.012),
                           (-0.17, -0.012)])
    m.extrude(guard, [(0.0, -0.046), (0.0, 0.046)], lambda fi: C("yellow", 11) if fi.tag else C("yellow", 9))
    m.tube([(0.0, 0.0, -0.085), (0.0, 0.0, -0.24)], [0.036, 0.036], 6, C("brown", 5), cap0=False, cap1=False,
           smooth=False)
    P = rot("Y", -35) @ tr(0, 0, -0.06)  # centred, tip up-RIGHT as seen from the camera (screen right = -X)
    M = pickup_frame()
    m.transform(M @ P)
    sword = m.to_object("Sword", mat, origin=M @ Vector((0, 0, 0)))
    g = Mesh()
    gc = Vector((0.0, 0.0, -0.292))
    g.sphere(gc, (0.066, 0.066, 0.066), 6, 3, lambda fi: C("red", 12, True) if fi.n.y > 0.2 else C("red", 9, True),
             phase=math.radians(30))
    g.transform(M @ P)
    gem = g.to_object("PommelGem", mat, origin=M @ P @ gc, parent=sword)
    return sword, [sword, gem]


MODELS = {
    # name: (staging sub-folder, builder)
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
    part_info = {}
    for p in parts:
        plo, phi = hr.bounds([p])
        part_info[p.name] = {"tris": bc.tri_count([p]), "parent": p.parent.name if p.parent else None,
                             "world_min": [round(c, 4) for c in plo], "world_max": [round(c, 4) for c in phi]}
    return {
        "tris": bc.tri_count(parts),
        "parts": {p.name: bc.tri_count([p]) for p in parts},
        "part_info": part_info,
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
        p.add_argument("--context-staging", default=None, help="staged enemies/surfaces for the mock (read-only)")

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
        secs, changed = bc.export_fbx_nested_if_changed(path, parts)
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
