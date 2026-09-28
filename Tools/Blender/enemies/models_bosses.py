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
    return C("gray", 9) if n.z > 0.55 else C("indigo", 4) if n.z < -0.45 else C("gray", 7)


def crystal_rule(base):
    def rule(c, n):
        return C("cyan", min(15, base + (2 if n.z > 0.6 else 0)), True)
    return rule


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
        gem = C("red", 13, True) if i % 2 == 0 else C("cyan", 14, True)
        cg.add(k.lathe([(0.0, -0.045), (0.045, 0.0), (0.0, 0.045)], 4, gem), T(*(base_p + Vector((0, 0, 0.26)))))
    hit, n = k.surface(cband, (0.0, -2.0, 0.1), (0, 1, 0))
    cg.add(k.dome(0.08, 0.08, 0.03, 6, C("red", 13, True)), k.frame(hit - n * 0.005, n))
    for side in (-1, 1):
        hit, n = k.surface(cband, (side * 0.26, -2.0, 0.1), (0, 1, 0))
        cg.add(k.dome(0.045, 0.045, 0.02, 5, C("cyan", 14, True)), k.frame(hit - n * 0.004, n))
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
    robe.add(on_surface(rg, k.dome(0.075, 0.09, 0.035, 6, C("green", 13, True)), 0.0, 0.9, elevation=10, sink=0.01))
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
    sockets, _ = k.eye_pair(sk, sc.z - 0.015, 0.088, 0.07, 0.078, "socket", elevation=28, glow=C("green", 14, True),
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
    circlet.add(k.dome(0.035, 0.035, 0.02, 5, C("green", 14, True)), k.frame(hit - n * 0.004, n))
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
            g.add(k.cone(0.07, 0.22, 5, C("green", 13, True)), T(*(hp + Vector((0, -0.12, 0.08)))) @ R(X, -15))
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
    orb.add(k.sphere(0.135, 8, 5, lambda c, n: C("green", 15 if n.z > 0.5 else 13, True)), T(*oc))
    for off in (Vector((0.22, 0.0, 0.08)), Vector((-0.18, -0.1, -0.1))):
        orb.add(k.lathe([(0.0, -0.035), (0.035, 0.0), (0.0, 0.035)], 4, C("green", 14, True)), T(*(oc + off)))
    return m


# ================================================================== Crystal Golem
def crystal_golem():
    m = k.Model("Boss_CrystalGolem", BOSS_BUDGET, footprint=(2, 2))
    body = m.part("Body", (0, 0, 0))
    for i, side in enumerate((-1, 1)):
        leg = k.box(0.36, 0.38, 0.44, rock_rule, top_scale=(1.15, 1.1))
        k.jitter(leg, 0.025, 11 + i, keep_z_below=0.001)
        body.add(leg.paint(rock_rule), T(side * 0.36, 0.06, 0.0))
    torso = rock(rock_rule, 3, subdiv=1, amount=0.07).copy(T(0, 0.08, 0.98) @ S(0.74, 0.56, 0.54))
    body.add(torso.paint(rock_rule))
    for side, seed in ((-1, 5), (1, 6)):
        body.add(rock(rock_rule, seed).copy(T(side * 0.62, 0.06, 1.24) @ S(0.3, 0.28, 0.24)).paint(rock_rule))
    cluster = ((-0.45, 0.22, 1.35, (-0.5, 0.35, 1.0), 0.09, 0.5), (-0.25, 0.3, 1.42, (-0.2, 0.5, 1.0), 0.11, 0.62),
               (0.02, 0.32, 1.46, (0.0, 0.3, 1.0), 0.13, 0.72), (0.28, 0.28, 1.4, (0.3, 0.45, 1.0), 0.1, 0.56),
               (0.5, 0.2, 1.32, (0.6, 0.3, 1.0), 0.08, 0.44), (0.75, 0.12, 1.4, (0.4, 0.1, 1.0), 0.07, 0.34),
               (-0.76, 0.1, 1.4, (-0.4, 0.15, 1.0), 0.07, 0.36), (0.12, 0.48, 1.2, (0.2, 1.0, 0.6), 0.08, 0.4))
    for i, (x, y, z, d, r, ln) in enumerate(cluster):
        body.add(k.crystal(r, ln, 5, crystal_rule(12 + i % 3), phase=0.3 * i), k.aim((x, y, z), d, (0, -1, 0)))
    hit, n = k.surface(torso, (0.0, -3.0, 0.98), (0, 1, 0))
    body.add(k.dome(0.2, 0.22, 0.05, 6, C("indigo", 1)), k.frame(hit - n * 0.02, n))

    hc = Vector((0.0, -0.22, 1.36))
    head = m.part("Head", hc, parent="Body")
    hg = rock(rock_rule, 9, amount=0.06).copy(T(*hc) @ S(0.27, 0.23, 0.2))
    head.add(hg.paint(rock_rule))
    head.add(on_surface(hg, k.box(0.38, 0.075, 0.07, top_lit("gray", 7, 10, 5)), 0.0, hc.z + 0.06, elevation=10,
                        sink=0.03))  # brow ridge
    for side in (-1, 1):
        head.add(on_surface(hg, k.box(0.085, 0.036, 0.03, C("cyan", 15, True)), side * 0.085, hc.z - 0.02,
                            elevation=15, sink=0.012))
    head.add(k.crystal(0.06, 0.26, 5, crystal_rule(13)), k.aim(hc + Vector((0.06, 0.02, 0.14)), (0.25, 0.1, 1.0)))

    for name, side, seed in (("ArmR", -1, 21), ("ArmL", 1, 31)):
        sh = Vector((side * 0.66, 0.05, 1.2))
        arm = m.part(name, sh, parent="Body")
        arm.add(rock(rock_rule, seed).copy(T(side * 0.74, 0.0, 0.92) @ S(0.19, 0.19, 0.27)).paint(rock_rule))
        fist = rock(rock_rule, seed + 1, subdiv=1, amount=0.06).copy(T(side * 0.78, -0.12, 0.46) @ S(0.27, 0.27, 0.28))
        arm.add(fist.paint(rock_rule))
        for j, (dx, dy, dz, d, r, ln) in enumerate(((0.05, 0.05, 0.62, (side * 0.6, 0.3, 1.0), 0.07, 0.34),
                                                    (0.12, 0.0, 0.95, (side * 1.0, 0.2, 0.6), 0.06, 0.26))):
            arm.add(k.crystal(r, ln, 5, crystal_rule(12 + j)),
                    k.aim((side * (0.76 + dx), dy, dz), d, (0, -1, 0)))

    core = m.part("CoreCrystal", (0.0, -0.46, 0.98), parent="Body")
    core.add(k.lathe([(0.0, -0.17), (0.11, -0.04), (0.11, 0.05), (0.0, 0.21)], 6,
                     lambda c, n: C("sky", 15 if n.z > 0.3 else 14, True), phase=0.0), T(0.0, -0.46, 0.98))

    shield = m.part("ShieldCrystal", (0.0, 0.0, 0.85))
    sg = Geo()
    main = k.lathe([(0.1, 0.0), (0.19, 0.12), (0.19, 0.5), (0.0, 0.78)], 6, crystal_rule(13), phase=math.pi / 6)
    sg.add(main.copy(S(1.0, 0.42, 1.0)))
    for side in (-1, 1):
        shard = k.lathe([(0.05, 0.0), (0.09, 0.07), (0.09, 0.28), (0.0, 0.44)], 5, crystal_rule(12), phase=0.3)
        sg.add(shard.copy(T(side * 0.2, 0.0, 0.06) @ R(Y, side * 24) @ S(1.0, 0.6, 1.0)))
    shield.add(sg, T(0.0, -1.0, 0.42) @ R(X, -12))
    return m


REGISTRY = {
    "Boss_KingSlime": king_slime,
    "Boss_BoneLich": bone_lich,
    "Boss_CrystalGolem": crystal_golem,
}
