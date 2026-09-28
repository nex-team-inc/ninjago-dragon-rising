"""The 3 bosses (TDD 14.1): 2x2-cell footprint (pivot = centre of the footprint, bottom), <= 1500 tris.
Blender axes: front = -Y (Unity +Z), up = +Z, character's right = -X.
"""
import math

from mathutils import Vector

import ekit as k
from ekit import C, R, S, T, Geo
from models_enemies import BONE, X, Y, Z, mirror_x, on_surface, shine, smile, top_lit

BOSS_BUDGET = 1500


def rock(color_rule, seed, subdiv=0, amount=0.08):
    """Unit-radius faceted boulder (jittered icosphere)."""
    g = k.ico(1.0, C("gray", 8), subdiv)
    k.jitter(g, amount, seed)
    return g.paint(color_rule)


def rock_rule(c, n):
    return C("gray", 12) if n.z > 0.55 else C("indigo", 5) if n.z < -0.45 else C("gray", 9)


# ================================================================== King Slime
def king_slime():
    m = k.Model("Boss_KingSlime", BOSS_BUDGET, footprint=(2, 2))
    body = m.part("Body", (0, 0, 0))
    prof = [(0.66, 0.0), (0.84, 0.05), (0.92, 0.17), (0.925, 0.35), (0.87, 0.54), (0.77, 0.73), (0.63, 0.9),
            (0.46, 1.03), (0.28, 1.12), (0.11, 1.175), (0.0, 1.19)]
    segs = 22
    g = k.lathe(prof, segs, C("green", 9), phase=math.pi / segs)

    def layered(c, n):
        a = math.atan2(c.y, c.x)
        edge = 0.66 + 0.07 * math.sin(5 * a + 0.9)  # wavy jelly layer boundary
        if c.z > edge + 0.14:
            return C("lime", 13 if c.z > 0.95 else 12)
        if c.z > edge:
            return C("lime", 11)
        if c.z < 0.06:
            return C("green", 6)
        return C("green", 8 if c.z < 0.22 else 10 if c.z < 0.45 else 11)
    g.paint(layered)
    body.add(g)
    for x, z, rx, ry in ((-0.42, 0.93, 0.13, 0.07), (-0.2, 1.06, 0.06, 0.035), (0.5, 0.95, 0.05, 0.03)):
        body.add(on_surface(g, k.dome(rx, ry, 0.02, 6, C("lime", 15)), x, z, elevation=45, sink=0.003))
    for x, z, r in ((0.62, 0.22, 0.07), (0.72, 0.4, 0.045), (-0.66, 0.28, 0.06), (-0.52, 0.12, 0.04), (0.3, 0.1, 0.05)):
        body.add(on_surface(g, k.dome(r, r, r * 0.5, 6, C("green", 12)), x, z, elevation=0, sink=0.004, blend=0.2))
    # face: smug brows, big grin with a tooth, blush
    for side in (-1, 1):
        brow = k.slab([(-0.1, -0.02), (0.1, -0.02), (0.1, 0.025), (-0.1, 0.035)], 0.04, C("green", 4))
        body.add(on_surface(g, brow.copy(R(Z, side * 12)), side * 0.28, 0.86, elevation=22, sink=0.008))
        body.add(on_surface(g, k.dome(0.1, 0.05, 0.012, 6, C("pink", 13)), side * 0.5, 0.47, elevation=10, sink=0.003))
    mouth = k.slab(smile(0.34, 0.15, 7), 0.03, C("red", 2))
    body.add(on_surface(g, mouth, 0.0, 0.43, elevation=18, sink=0.01))
    body.add(on_surface(g, k.dome(0.08, 0.045, 0.02, 6, C("red", 11)), 0.03, 0.34, elevation=18, sink=0.004))
    body.add(on_surface(g, k.box(0.07, 0.065, 0.03, C("gray", 15)), -0.09, 0.405, elevation=18, sink=0.01))
    eyes, mid = k.eye_pair(g, 0.64, 0.25, 0.15, 0.19, "cute", elevation=26, segs=10)
    m.part("Eyes", mid, parent="Body").add(eyes)

    cz = 1.02
    crown = m.part("Crown", (0.0, 0.03, cz), parent="Body")
    cg = Geo()
    band = [(0.36, 0.0), (0.4, 0.2), (0.365, 0.2), (0.335, 0.19), (0.27, 0.27), (0.14, 0.32), (0.0, 0.335)]
    cband = k.lathe(band, 10, C("yellow", 13), phase=math.pi / 10)
    cband.paint(lambda c, n: C("red", 12 if c.z > 0.3 else 10) if c.z > 0.19 and math.hypot(c.x, c.y) < 0.345 else
                C("yellow", 15 if n.z > 0.5 else 12))  # gold band + red velvet cushion
    cg.add(cband)
    for i in range(5):
        a = math.radians(-90 + 72 * i)
        base_p = Vector((0.375 * math.cos(a), 0.375 * math.sin(a), 0.19))
        cg.add(k.cone(0.08, 0.24, 4, top_lit("yellow", 13, 15), phase=math.pi / 4),
               T(*base_p) @ R(Vector((-math.sin(a), math.cos(a), 0)), 8))
        gem = C("red", 10, True) if i % 2 == 0 else C("cyan", 11, True)
        cg.add(k.lathe([(0.0, -0.045), (0.045, 0.0), (0.0, 0.045)], 4, gem), T(*(base_p + Vector((0, 0, 0.26)))))
    hit, n = k.surface(cband, (0.0, -2.0, 0.1), (0, 1, 0))
    cg.add(k.dome(0.08, 0.08, 0.03, 6, C("red", 10, True)), k.frame(hit - n * 0.005, n))
    for side in (-1, 1):
        hit, n = k.surface(cband, (side * 0.26, -2.0, 0.1), (0, 1, 0))
        cg.add(k.dome(0.045, 0.045, 0.02, 5, C("cyan", 11, True)), k.frame(hit - n * 0.004, n))
    crown.add(cg, T(0.0, 0.03, cz) @ R(Y, -9) @ R(X, -6) @ S(1.08))
    return m


# ================================================================== Bone Lich
def bone_lich():
    m = k.Model("Boss_BoneLich", BOSS_BUDGET, footprint=(2, 2))
    robe = m.part("Robe", (0, 0, 0))
    segs = 12
    prof = [(0.0, 0.42), (0.6, 0.14), (0.62, 0.26), (0.56, 0.52), (0.48, 0.78), (0.445, 0.93), (0.45, 1.04),
            (0.53, 1.18), (0.42, 1.3), (0.2, 1.36), (0.0, 1.37)]
    rg = k.lathe(prof, segs, C("purple", 8), phase=0.0)

    def tatter(v):
        if abs(v.z - 0.14) < 1e-6:
            j = round(math.atan2(v.y, v.x) / (math.tau / segs)) % segs
            if j % 2:
                return Vector((v.x * 0.9, v.y * 0.9, v.z + 0.13))
        return v
    rg.deform(tatter)
    rg.paint(lambda c, n: C("purple", 1) if n.z < -0.3 else
             C("purple", 5 if c.z < 0.3 else 7 if c.z < 0.62 else 8 if c.z < 1.1 else 10))
    rg.paint(top_lit("yellow", 9, 11), where=lambda c, n: 0.93 < c.z < 1.06 and n.z > -0.3)  # gold belt
    robe.add(rg)
    hood = k.sphere(0.32, 10, 6, C("purple", 9), phase=math.pi / 10)
    hood.deform(lambda v: Vector((v.x, v.y + max(0.0, v.z - 0.12) * 0.9, v.z * 1.15 + max(0.0, v.z - 0.2) * 0.5)))
    hood.paint(lambda c, n: C("purple", 1) if (n.y < -0.5 and -0.26 < c.z < 0.24 and abs(c.x) < 0.24) else
               C("purple", 11 if n.z > 0.5 else 9 if n.z > -0.2 else 6))
    robe.add(hood, T(0, 0.06, 1.5))
    for side in (-1, 1):
        for dx, h, tilt in ((0.0, 0.3, 18), (0.14, 0.22, 34)):
            robe.add(k.cone(0.075, h, 4, top_lit(BONE, 13, 15, 11), phase=math.pi / 4),
                     T(side * (0.38 + dx), 0.05, 1.2) @ R(Y, side * tilt))
    robe.add(on_surface(rg, k.dome(0.075, 0.09, 0.035, 6, C("green", 10, True)), 0.0, 0.9, elevation=10, sink=0.01))
    robe.add(on_surface(rg, k.dome(0.12, 0.13, 0.02, 6, C("yellow", 10)), 0.0, 0.9, elevation=10, sink=0.012))

    sc = Vector((0, -0.12, 1.47))
    skull = m.part("Skull", sc, parent="Robe")
    sk = k.sphere(0.22, 10, 5, C(BONE, 14), phase=math.pi / 10)
    sk.deform(lambda v: Vector((v.x * (1.0 if v.z > -0.05 else 0.84), v.y * 0.9, v.z * 0.88)))
    sk = sk.copy(T(*sc))
    sk.paint(k.vgrad(BONE, 12, 15, sc.z - 0.15, sc.z + 0.14))
    skull.add(sk)
    skull.add(k.box(0.23, 0.15, 0.08, top_lit(BONE, 12, 14, 9)), T(sc.x, sc.y - 0.06, sc.z - 0.235))
    skull.add(k.box(0.15, 0.02, 0.016, C(BONE, 3)), T(sc.x, sc.y - 0.137, sc.z - 0.185))
    sockets, _ = k.eye_pair(sk, sc.z - 0.015, 0.088, 0.07, 0.078, "socket", elevation=28, glow=C("green", 11, True),
                            segs=6, look=(0.0, 0.0))
    skull.add(sockets)
    skull.add(on_surface(sk, k.slab([(-0.025, 0.022), (0.0, -0.022), (0.025, 0.022)], 0.02, C(BONE, 2)), 0.0,
                         sc.z - 0.09, elevation=20, sink=0.006))
    circlet = Geo()
    circlet.add(k.cyl(0.205, 0.05, 10, top_lit("yellow", 9, 11), phase=math.pi / 10, r_top=0.215))
    for i in range(5):
        a = math.radians(-90 + 40 * (i - 2))
        circlet.add(k.cone(0.035, 0.11 if i == 2 else 0.075, 4, top_lit("yellow", 10, 12), phase=math.pi / 4),
                    T(0.21 * math.cos(a), 0.21 * math.sin(a), 0.04))
    hit, n = k.surface(circlet, (0.0, -2.0, 0.025), (0, 1, 0))
    circlet.add(k.dome(0.035, 0.035, 0.02, 5, C("green", 11, True)), k.frame(hit - n * 0.004, n))
    skull.add(circlet, T(sc.x, sc.y + 0.02, sc.z + 0.07) @ R(X, -8))

    def hand(side, pose):
        hp = Vector((side * 0.74, -0.3, 0.95))
        g = Geo()
        hs = 1.35  # chunky hands read at 28 px per cell
        g.add(k.box(0.15, 0.08, 0.14, top_lit(BONE, 13, 15, 11)), T(*(hp + Vector((0, 0, -0.07)))))
        if pose == "grip":  # fingers wrapped round the staff (staff axis = hp + x offset)
            for i, dz in enumerate((-0.05, 0.0, 0.05)):
                p0 = hp + Vector((side * -0.06, -0.03, dz))
                g.add(k.sweep([p0, p0 + Vector((side * -0.03, -0.07, 0)), p0 + Vector((side * 0.04, -0.09, 0))],
                              [0.028, 0.025, 0.012], 4, C(BONE, 14)))
        else:  # open casting hand: fingers up, green wisp above the palm
            for i, dx in enumerate((-0.05, 0.0, 0.05)):
                p0 = hp + Vector((dx, -0.02, 0.06))
                g.add(k.sweep([p0, p0 + Vector((dx * 0.4, -0.02, 0.08)), p0 + Vector((dx * 0.6, 0.0, 0.15))],
                              [0.026, 0.022, 0.0], 4, C(BONE, 14)))
            g.add(k.cone(0.07, 0.22, 5, C("green", 10, True)), T(*(hp + Vector((0, -0.12, 0.08)))) @ R(X, -15))
        thumb0 = hp + Vector((side * 0.075, -0.02, -0.02))
        g.add(k.sweep([thumb0, thumb0 + Vector((side * 0.05, -0.05, 0.05))], [0.026, 0.0], 4, C(BONE, 14)))
        g = g.copy(T(*hp) @ S(hs) @ T(*(-hp)))
        cuff = k.lathe([(0.09, 0.0), (0.125, 0.12)], 8, C("purple", 9), phase=math.pi / 8)
        cuff.paint(C("purple", 2), where=lambda c, n: n.z > 0.9)
        g.add(cuff, k.aim(hp + Vector((0.0, 0.2, -0.34)), (0.0, -0.8, 0.6)))
        return hp, g

    hp_r, gr = hand(-1, "grip")
    hp_l, gl = hand(1, "open")
    m.part("HandR", hp_r, parent="Robe").add(gr)
    m.part("HandL", hp_l, parent="Robe").add(gl)

    staff = m.part("Staff", hp_r, parent="HandR")
    sx = hp_r + Vector((0.08, -0.11, 0.0))
    pts = [sx + Vector((0.0, 0.0, dz)) for dz in (-0.62, -0.3, 0.0, 0.35, 0.72)]
    shaft = k.sweep(pts, 0.052, 5, C(BONE, 12))
    shaft.paint(lambda c, n: C(BONE, 12 if int((c.z - sx.z + 1.0) * 9) % 2 else 10))
    staff.add(shaft)
    top = pts[-1]
    for i in range(3):
        a = math.radians(90 + 120 * i)
        o = Vector((math.cos(a), math.sin(a), 0.0))
        staff.add(k.sweep([top, top + o * 0.12 + Vector((0, 0, 0.1)), top + o * 0.09 + Vector((0, 0, 0.27))],
                          [0.035, 0.028, 0.0], 4, top_lit(BONE, 13, 15)))
    oc = top + Vector((0, 0, 0.17))
    orb = m.part("Orb", oc, parent="Staff")
    orb.add(k.sphere(0.135, 8, 5, lambda c, n: C("green", 13 if n.z > 0.5 else 10, True)), T(*oc))
    for off in (Vector((0.22, 0.0, 0.08)), Vector((-0.18, -0.1, -0.1))):
        orb.add(k.lathe([(0.0, -0.035), (0.035, 0.0), (0.0, 0.035)], 4, C("green", 11, True)), T(*(oc + off)))
    return m


# ================================================================== Crystal Golem
# Colour roles (gameplay read first): gold emissive = ShieldCrystal ONLY (the shielded face), cyan-white emissive =
# CoreCrystal + eyes (its life glow), jade/teal NON-emissive = decorative crystals (apart from the Act 3 dressing's
# glowing cyan/violet Env_Crystal_*). Everything except ShieldCrystal stays inside |x| <= 0.8 so the shield crystal,
# which lives in the outer 0.8..1.0 band of the footprint, is never hidden by an arm when it is turned to a side.
SHIELD_R = 0.9  # radial centre of the shield crystal band


def jade(base):
    def rule(c, n):
        return C("teal", min(15, base + (3 if n.z > 0.6 else 1 if n.z > 0.1 else -1)))
    return rule


def gold(base):
    """Saturated mid-yellow emissive swatches: lit albedo + emission stays gold instead of clipping to white."""
    def rule(c, n):
        return C("yellow", min(15, base + (2 if n.z > 0.45 else 0)), True)
    return rule


def crystal_golem():
    m = k.Model("Boss_CrystalGolem", BOSS_BUDGET, footprint=(2, 2))
    body = m.part("Body", (0, 0, 0))
    for i, side in enumerate((-1, 1)):
        leg = k.box(0.34, 0.38, 0.46, rock_rule, top_scale=(1.12, 1.08))
        k.jitter(leg, 0.02, 11 + i, keep_z_below=0.001)
        body.add(leg.paint(rock_rule), T(side * 0.3, 0.12, 0.0))
    torso = rock(rock_rule, 3, subdiv=1, amount=0.07).copy(T(0, 0.12, 0.98) @ S(0.6, 0.5, 0.55))
    body.add(torso.paint(rock_rule))
    for side, seed in ((-1, 5), (1, 6)):
        body.add(rock(rock_rule, seed).copy(T(side * 0.5, 0.1, 1.3) @ S(0.24, 0.24, 0.2)).paint(rock_rule))
    # jade crystal growths on the shoulders and upper back (kept off the centre line behind the head so the
    # shield crystal still shows over the back when it is turned to the far face)
    growth = ((0.52, 0.16, 1.42, (0.3, 0.25, 1.0), 0.1, 0.52), (0.64, 0.02, 1.36, (0.22, -0.05, 1.0), 0.07, 0.34),
              (0.4, 0.34, 1.34, (0.35, 0.7, 1.0), 0.08, 0.4), (0.3, 0.46, 1.05, (0.2, 1.0, 0.45), 0.07, 0.28))
    for i, (x, y, z, d, r, ln) in enumerate(growth):
        for side in (-1, 1):
            body.add(k.crystal(r, ln * (1.0 if side < 0 else 0.9), 5, jade(11 + i % 2), phase=0.3 * i + side),
                     k.aim((side * x, y, z), (side * d[0], d[1], d[2]), (0, -1, 0)))
    hit, n = k.surface(torso, (0.0, -3.0, 1.0), (0, 1, 0))
    body.add(k.dome(0.2, 0.24, 0.05, 6, C("indigo", 1)), k.frame(hit - n * 0.02, n))  # dark socket round the core

    # head: sunk between the shoulders, heavy brow over big glowing eyes, underbite jaw with two tusks
    hc = Vector((0.0, -0.38, 1.5))
    head = m.part("Head", (0.0, -0.22, 1.36), parent="Body")
    hg = rock(rock_rule, 9, amount=0.05).copy(T(*hc) @ S(0.3, 0.26, 0.25))
    head.add(hg.paint(rock_rule))
    for side in (-1, 1):
        eye = k.prism([(-0.06, -0.055), (0.06, -0.055), (0.06, 0.06), (-0.06, 0.02)], -0.02, 0.03,
                      C("cyan", 11, True), cap_color=C("cyan", 12, True))
        eye = eye.copy(S(-1, 1, 1)) if side < 0 else eye  # inner corner lower = frowning
        head.add(on_surface(hg, eye, side * 0.12, hc.z + 0.0, elevation=44, sink=0.015, blend=0.7))
        brow = k.box(0.2, 0.075, 0.07, top_lit("gray", 6, 9, 4)).copy(R(Z, side * 14))
        head.add(on_surface(hg, brow, side * 0.12, hc.z + 0.1, elevation=30, sink=0.03, blend=0.6))
    head.add(on_surface(hg, k.box(0.26, 0.04, 0.03, C("indigo", 1)), 0.0, hc.z - 0.1, elevation=30, sink=0.012))
    jaw = k.box(0.36, 0.16, 0.12, top_lit("gray", 8, 10, 5), top_scale=(1.06, 1.0))
    head.add(jaw, T(0.0, hc.y - 0.2, hc.z - 0.26))
    for side in (-1, 1):
        head.add(k.cone(0.03, 0.08, 4, top_lit("gray", 13, 15), phase=math.pi / 4),
                 T(side * 0.12, hc.y - 0.25, hc.z - 0.15))  # tusks
    head.add(k.crystal(0.06, 0.24, 5, jade(11)), k.aim(hc + Vector((0.05, 0.06, 0.18)), (0.2, 0.25, 1.0)))

    for name, side, seed in (("ArmR", -1, 21), ("ArmL", 1, 31)):
        sh = Vector((side * 0.5, 0.08, 1.28))
        arm = m.part(name, sh, parent="Body")
        arm.add(rock(rock_rule, seed).copy(T(side * 0.6, 0.0, 0.9) @ S(0.15, 0.16, 0.26)).paint(rock_rule))
        fist = rock(rock_rule, seed + 1, subdiv=1, amount=0.06).copy(T(side * 0.55, -0.2, 0.44) @ S(0.2, 0.23, 0.22))
        arm.add(fist.paint(rock_rule))
        arm.add(k.crystal(0.06, 0.26, 5, jade(11)), k.aim((side * 0.6, 0.06, 0.98), (side * 0.2, 0.7, 1.0), (0, -1, 0)))

    core = m.part("CoreCrystal", (0.0, -0.5, 1.0), parent="Body")
    core.add(k.lathe([(0.0, -0.2), (0.15, -0.05), (0.15, 0.07), (0.0, 0.28)], 6,
                     lambda c, n: C("cyan", 13 if n.z > 0.3 else 10, True), phase=0.0), T(0.0, -0.5, 1.0))

    # ShieldCrystal: gold wall of crystals in the outer band of the footprint on the shielded face. Built on the
    # model's front (Unity +Z = sim Face.Bottom); code turns it about the model centre (see manifest shield_face_yaw).
    shield = m.part("ShieldCrystal", (0.0, 0.0, 0.85))
    sg = Geo()
    main = k.lathe([(0.11, 0.0), (0.18, 0.12), (0.18, 0.92), (0.0, 1.34)], 6, gold(11), phase=math.pi / 6)
    sg.add(main.copy(T(0.0, -SHIELD_R, 0.03) @ S(1.0, 0.5, 1.0)))
    for side in (-1, 1):
        flank = k.lathe([(0.06, 0.0), (0.11, 0.1), (0.11, 0.56), (0.0, 0.86)], 5, gold(10), phase=0.3)
        sg.add(flank.copy(T(side * 0.3, -SHIELD_R, 0.02) @ R(Y, side * 18) @ S(1.0, 0.55, 1.0)))
        small = k.lathe([(0.05, 0.0), (0.08, 0.07), (0.08, 0.3), (0.0, 0.46)], 5, gold(10), phase=0.7)
        sg.add(small.copy(T(side * 0.52, -SHIELD_R + 0.02, 0.01) @ R(Y, side * 34) @ S(1.0, 0.6, 1.0)))
    # floating aegis shard above the shielded side: clears the golem's silhouette on every face (on the far face
    # it rises over the back; on the sides it hangs beside the head above the shoulders) and, on the front face,
    # floats above eye level so the face stays readable
    aegis = k.lathe([(0.0, -0.26), (0.17, -0.02), (0.15, 0.1), (0.0, 0.36)], 6, gold(10), phase=math.pi / 6)
    sg.add(aegis.copy(T(0.0, -0.74, 2.08) @ R(X, 10) @ S(1.0, 0.7, 1.0)))
    for side in (-1, 1):
        sg.add(k.lathe([(0.0, -0.07), (0.05, 0.0), (0.0, 0.09)], 4, gold(11)),
               T(side * 0.26, -0.7, 2.0 + 0.08 * side) @ R(Y, side * 25))
    shield.add(sg)
    return m


REGISTRY = {
    "Boss_KingSlime": king_slime,
    "Boss_BoneLich": bone_lich,
    "Boss_CrystalGolem": crystal_golem,
}
