"""The 9 regular enemies (TDD 14.1). Each builder returns an ekit.Model; part names/hierarchy are a contract.
Blender axes: front = -Y (Unity +Z), up = +Z, character's right = -X. Units: 1 m = 1 cell.
Readability rules (TDD 14.1): ~28 px per cell in game -> silhouettes fill ~0.8-0.9 of the cell, 2-3 strong colour
blocks, big eyes tilted toward the high camera, no detail thinner than ~0.04 m.
"""
import math

from mathutils import Vector

import ekit as k
from ekit import C, R, S, T, Geo

X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))
ENEMY_BUDGET = 600
BONE = "gray"


def mirror_x(geo):
    return geo.copy(S(-1, 1, 1))


def top_lit(family, side, top, bottom=None, up=0.5, down=-0.4, emissive=False):
    return k.facing(family, side, top, bottom, emissive, up, down)


def shine(rule, family, shade, light=(-0.45, -0.55, 0.7), thresh=0.9, zmin=0.0):
    """Wrap a colour rule with a baked glossy highlight on faces facing `light`."""
    lv = Vector(light).normalized()

    def f(c, n):
        if n.dot(lv) > thresh and c.z > zmin:
            return C(family, shade)
        return k.resolve(rule, c, n)
    return f


def smile(width, height, segs=5):
    """Half-disc (flat top) mouth polygon in XY, CCW."""
    return [(width / 2 * math.cos(math.pi + math.pi * i / segs), height * math.sin(math.pi + math.pi * i / segs))
            for i in range(segs + 1)]


def on_surface(target, piece, x, z, elevation=0.0, sink=0.0, from_y=-3.0, blend=0.5):
    """Place `piece` (built facing +Z) on the front surface of `target` at (x, z)."""
    hit, n = k.surface(target, (x, from_y, z), (0, 1, 0))
    face_dir = Vector((0.0, -math.cos(math.radians(elevation)), math.sin(math.radians(elevation))))
    nn = (n.normalized() * (1 - blend) + face_dir * blend).normalized()
    return piece.copy(k.frame(hit - nn * sink, nn))


def bands(family, z_bands, default):
    """Colour by height bands: z_bands = [(z0, z1, shade), ...]."""
    def rule(c, n):
        for z0, z1, shade in z_bands:
            if z0 <= c.z < z1:
                return C(family, shade)
        return k.resolve(default, c, n)
    return rule


# ================================================================== Slime
def slime():
    m = k.Model("Enemy_Slime", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    prof = [(0.32, 0.0), (0.41, 0.03), (0.445, 0.10), (0.44, 0.20), (0.41, 0.30), (0.36, 0.40), (0.29, 0.49),
            (0.205, 0.565), (0.115, 0.62), (0.045, 0.655), (0.0, 0.668)]
    g = k.lathe(prof, 16, C("lime", 11), phase=math.pi / 16)
    g.paint(k.vgrad("lime", 9, 13, 0.03, 0.58))
    g.paint(C("lime", 7), where=lambda c, n: c.z < 0.02 or (c.z < 0.06 and n.z < -0.3))
    tip = k.sweep([(0, 0.0, 0.62), (0, 0.02, 0.70), (0, 0.07, 0.75), (0, 0.12, 0.745)], [0.05, 0.036, 0.022, 0.0], 6,
                  C("lime", 13))
    body.add(g)
    body.add(tip)
    # glossy highlight blob (upper-left), mouth, blush
    body.add(on_surface(g, k.dome(0.075, 0.042, 0.012, 6, C("lime", 15)), -0.2, 0.47, elevation=40, sink=0.002))
    body.add(on_surface(g, k.dome(0.028, 0.02, 0.01, 4, C("lime", 15)), -0.1, 0.55, elevation=50, sink=0.002))
    body.add(on_surface(g, k.slab(smile(0.095, 0.05), 0.02, C("red", 2)), 0.0, 0.22, elevation=18, sink=0.004))
    for side in (-1, 1):
        body.add(on_surface(g, k.dome(0.045, 0.024, 0.01, 6, C("pink", 13)), side * 0.215, 0.25, elevation=10,
                            sink=0.002))
    eyes, mid = k.eye_pair(g, 0.345, 0.13, 0.08, 0.105, "cute", elevation=28)
    m.part("Eyes", mid, parent="Body").add(eyes)
    return m


# ================================================================== Bat
def bat():
    m = k.Model("Enemy_Bat", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0.40))
    cz = 0.47
    g = k.sphere(0.225, 12, 7, C("purple", 11), phase=math.pi / 12).copy(T(0, 0, cz) @ S(1.0, 0.92, 0.94))
    g.paint(k.vgrad("purple", 9, 13, cz - 0.2, cz + 0.2))
    g.paint(C("purple", 14), where=lambda c, n: n.y < -0.55 and c.z < cz - 0.03)  # light belly
    body.add(g)
    for side in (-1, 1):
        ear = k.cone(0.085, 0.21, 4, top_lit("purple", 10, 12), phase=math.pi / 4)
        ear.paint(C("pink", 12), where=lambda c, n: n.y < -0.5)
        body.add(ear, T(side * 0.11, 0.02, cz + 0.13) @ R(Y, side * 24) @ R(X, 8))
        body.add(k.cone(0.024, 0.06, 3, C("gray", 15), phase=math.pi / 2), T(side * 0.04, -0.19, cz - 0.075) @ R(X, 180))
        body.add(k.cone(0.04, 0.08, 4, C("purple", 6)), T(side * 0.08, 0.0, cz - 0.17) @ R(X, 180))
    poly = [(0.0, -0.04), (0.06, -0.10), (0.13, 0.01), (0.20, -0.09), (0.27, 0.04), (0.35, -0.05), (0.41, 0.10),
            (0.44, 0.27), (0.20, 0.20), (0.0, 0.09)]
    poly = [(x * 1.12, y * 1.12) for x, y in poly]
    wing = k.slab(poly, 0.04, C("purple", 10), cap_color=C("purple", 7))
    wing_left = wing.copy(R(Y, -14) @ R(X, 42))  # membrane faces up/front (toward the high camera)
    for name, side in (("WingL", 1), ("WingR", -1)):
        pivot = Vector((side * 0.17, 0.03, cz + 0.02))
        wg = wing_left if side > 0 else mirror_x(wing_left)
        m.part(name, pivot, parent="Body").add(wg, T(*pivot))
    eyes, mid = k.eye_pair(g, cz + 0.035, 0.08, 0.066, 0.085, "cute", elevation=25)
    m.part("Eyes", mid, parent="Body").add(eyes)
    return m


# ================================================================== Skeleton
def skeleton():
    m = k.Model("Enemy_Skeleton", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    for side in (-1, 1):
        body.add(k.sweep([(side * 0.09, 0.0, 0.22), (side * 0.095, -0.01, 0.06)], 0.047, 4, C(BONE, 12),
                         phase=math.pi / 4))
        body.add(k.box(0.12, 0.17, 0.06, top_lit(BONE, 12, 14), top_scale=(0.9, 0.8)), T(side * 0.1, -0.03, 0))
    body.add(k.box(0.27, 0.15, 0.08, top_lit(BONE, 12, 14), top_scale=(1.0, 0.9)), T(0, 0, 0.18))
    body.add(k.cyl(0.13, 0.22, 6, C(BONE, 4), phase=math.pi / 6), S(1.0, 0.8, 1.0) @ T(0, 0, 0.26))  # dark core
    for z0, z1, r in ((0.285, 0.33, 0.165), (0.35, 0.4, 0.19), (0.42, 0.47, 0.18)):  # three rib bands
        body.add(k.cyl(r, z1 - z0, 8, top_lit(BONE, 13, 15), phase=math.pi / 8, r_top=r * 0.94),
                 S(1.0, 0.82, 1.0) @ T(0, 0, z0))
    scarf = k.lathe([(0.2, 0.48), (0.195, 0.535), (0.1, 0.56)], 8, top_lit("red", 9, 11, 7), phase=math.pi / 8)
    body.add(scarf, S(1.0, 0.88, 1.0))
    tail = k.slab([(-0.05, 0.0), (0.05, 0.0), (0.06, 0.19), (0.0, 0.15), (-0.06, 0.2)], 0.03, C("red", 8),
                  cap_color=C("red", 9))
    body.add(tail, T(0.08, 0.14, 0.52) @ R(Z, 15) @ R(X, 25) @ R(X, -90))

    head = m.part("Head", (0, 0, 0.53), parent="Body")
    hz = 0.79
    skull = k.sphere(0.25, 10, 5, C(BONE, 14), phase=math.pi / 10)
    skull.deform(lambda v: Vector((v.x * (1.0 if v.z > -0.06 else 0.84), v.y * 0.9, v.z * 0.86)))
    skull = skull.copy(T(0, 0, hz))
    skull.paint(k.vgrad(BONE, 12, 15, hz - 0.17, hz + 0.16))
    head.add(skull)
    head.add(k.box(0.27, 0.17, 0.09, top_lit(BONE, 12, 14, 9), top_scale=(1.05, 1.0)), T(0, -0.06, hz - 0.255))
    head.add(k.box(0.17, 0.02, 0.018, C(BONE, 3)), T(0, -0.148, hz - 0.2))  # mouth line
    sockets, _ = k.eye_pair(skull, hz - 0.02, 0.1, 0.074, 0.08, "socket", elevation=30, glow=C("red", 12, True),
                            segs=6, look=(0.0, 0.0))
    head.add(sockets)
    nose = k.slab([(-0.028, 0.025), (0.0, -0.025), (0.028, 0.025)], 0.02, C(BONE, 2))
    head.add(on_surface(skull, nose, 0.0, hz - 0.1, elevation=20, sink=0.006))

    for name, side in (("ArmR", -1), ("ArmL", 1)):
        sh = Vector((side * 0.18, 0.0, 0.46))
        hand = Vector((side * 0.27, -0.1, 0.3))
        arm = m.part(name, sh, parent="Body")
        arm.add(k.sweep([sh, (side * 0.25, -0.02, 0.38), hand], 0.038, 5, C(BONE, 13)))
        arm.add(k.sphere(0.062, 5, 3, top_lit(BONE, 13, 15)), T(*hand))
    grip = Vector((-0.27, -0.1, 0.3))
    club = m.part("Weapon", grip, parent="ArmR")
    top = grip + Vector((-0.1, -0.1, 0.44))
    sd = (top - grip).normalized()
    club.add(k.sweep([grip - sd * 0.1, top], [0.038, 0.052], 5, C(BONE, 13)))
    side_dir = sd.cross(Y).normalized()
    for s in (-1, 1):
        club.add(k.sphere(0.074, 6, 3, top_lit(BONE, 14, 15, 12)), T(*(top + side_dir * s * 0.056)))
    club.add(k.sphere(0.05, 5, 3, C(BONE, 12)), T(*(grip - sd * 0.1)))
    return m.scale_all(1.12)


# ================================================================== Shield Knight
def shield_knight():
    m = k.Model("Enemy_ShieldKnight", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    steel = "blue"
    for side in (-1, 1):
        body.add(k.box(0.12, 0.15, 0.15, top_lit("indigo", 4, 6), top_scale=(0.9, 0.85)), T(side * 0.085, -0.01, 0))
    torso = k.lathe([(0.17, 0.13), (0.215, 0.2), (0.225, 0.32), (0.2, 0.45), (0.13, 0.52)], 10,
                    top_lit(steel, 11, 13, 8), phase=math.pi / 10)
    torso.paint(C("brown", 5), where=lambda c, n: 0.19 < c.z < 0.235)
    body.add(torso)
    for side in (-1, 1):
        pad = k.sphere(0.1, 8, 3, top_lit("gray", 12, 15, 9), phase=math.pi / 8)
        body.add(pad, T(side * 0.2, 0.01, 0.45) @ S(1.0, 1.0, 0.8))
    body.add(k.sweep([(-0.2, 0.0, 0.42), (-0.25, -0.02, 0.34), (-0.28, -0.06, 0.28)], 0.045, 5, top_lit(steel, 10, 12)))
    body.add(k.sphere(0.052, 5, 3, top_lit("indigo", 5, 7)), T(-0.285, -0.07, 0.27))
    body.add(k.sweep([(0.2, 0.0, 0.42), (0.2, -0.12, 0.38), (0.12, -0.22, 0.36)], 0.045, 5, top_lit(steel, 10, 12)))

    head = m.part("Head", (0, 0, 0.5), parent="Body")
    hz = 0.66
    helm = k.lathe([(0.12, 0.5), (0.165, 0.55), (0.175, 0.64), (0.16, 0.73), (0.11, 0.8), (0.0, 0.83)], 10,
                   top_lit(steel, 12, 14, 10), phase=math.pi / 10)
    helm.paint(shine(top_lit(steel, 12, 14, 10), "sky", 15, thresh=0.9, zmin=0.6))
    head.add(helm)
    visor = k.box(0.25, 0.1, 0.055, C("indigo", 1)).copy(T(0, -0.13, hz - 0.035) @ R(X, -12))
    head.add(visor)
    for side in (-1, 1):
        glint = k.dome(0.032, 0.02, 0.012, 5, C("yellow", 15, True))
        head.add(on_surface(visor, glint, side * 0.055, hz - 0.01, elevation=20, sink=0.004, blend=0.3))
    crest = k.slab([(-0.07, 0.0), (0.2, 0.0), (0.25, 0.11), (0.13, 0.22), (-0.03, 0.17), (-0.12, 0.09)], 0.075,
                   C("red", 10), cap_color=C("red", 12))
    head.add(crest, T(0, 0, 0.77) @ R(Z, 90) @ R(X, 90) @ T(-0.05, 0, 0))

    sc = Vector((0.02, -0.3, 0.36))
    shield = m.part("Shield", sc, parent="Body")
    heater = [(0.0, -0.32), (0.2, -0.19), (0.275, 0.03), (0.275, 0.25), (-0.275, 0.25), (-0.275, 0.03), (-0.2, -0.19)]
    sg = Geo()
    sg.add(k.prism(heater, -0.03, 0.02, C("gray", 13), cap_color=C("gray", 15)))
    inner = [(x * 0.8, y * 0.8 + 0.01) for x, y in heater]
    sg.add(k.prism(inner, 0.015, 0.035, C(steel, 10), cap_color=C(steel, 12)))
    cross = [(-0.035, -0.17), (0.035, -0.17), (0.035, 0.06), (0.12, 0.06), (0.12, 0.13), (0.035, 0.13), (0.035, 0.2),
             (-0.035, 0.2), (-0.035, 0.13), (-0.12, 0.13), (-0.12, 0.06), (-0.035, 0.06)]
    sg.add(k.prism(cross, 0.03, 0.05, C("yellow", 12), cap_color=C("yellow", 15)))
    shield.add(sg, T(*sc) @ R(X, 90 - 18))

    grip = Vector((-0.285, -0.07, 0.27))
    weapon = m.part("Weapon", grip, parent="Body")
    blade = [(-0.04, 0.0), (0.04, 0.0), (0.04, 0.42), (0.0, 0.51), (-0.04, 0.42)]
    wg = Geo()
    wg.add(k.slab(blade, 0.03, C("gray", 13), cap_color=C("gray", 15)), T(0, 0, 0.08) @ R(X, 90))
    wg.add(k.box(0.19, 0.055, 0.045, top_lit("yellow", 12, 14)), T(0, 0, 0.045))
    wg.add(k.cyl(0.024, 0.1, 5, C("brown", 6)), T(0, 0, -0.05))
    wg.add(k.box(0.055, 0.055, 0.045, C("yellow", 13)), T(0, 0, -0.09))
    weapon.add(wg, T(*grip) @ R(Y, -10) @ R(X, 12))
    return m.scale_all(1.25)


# ================================================================== Imp Mage
def mage():
    m = k.Model("Enemy_Mage", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    robe = k.lathe([(0.24, 0.0), (0.25, 0.05), (0.19, 0.22), (0.14, 0.4), (0.09, 0.47)], 10,
                   C("red", 10), phase=math.pi / 10)
    robe.paint(k.vgrad("red", 9, 12, 0.0, 0.45))
    robe.paint(top_lit("yellow", 13, 15, 10), where=lambda c, n: c.z < 0.05)  # gold hem
    body.add(robe)
    body.add(k.lathe([(0.2, 0.235), (0.185, 0.275)], 10, top_lit("yellow", 12, 14), phase=math.pi / 10))  # sash
    for side in (-1, 1):
        sh = Vector((side * 0.1, 0.0, 0.42))
        hand = Vector((side * 0.2, -0.1, 0.3))
        body.add(k.sweep([sh, (side * 0.17, -0.04, 0.36), hand + Vector((0, 0.03, 0.02))], [0.04, 0.05, 0.068], 6,
                         top_lit("red", 10, 12, 7)))
        body.add(k.sphere(0.048, 5, 3, C("indigo", 2)), T(*hand))

    head = m.part("Head", (0, 0, 0.46), parent="Body")
    hz = 0.585
    face = k.sphere(0.155, 10, 5, top_lit("indigo", 2, 3, 1), phase=math.pi / 10).copy(T(0, 0, hz) @ S(1.0, 0.95, 0.9))
    head.add(face)
    eyes, _ = k.eye_pair(face, hz - 0.03, 0.062, 0.044, 0.058, "glow", elevation=32, glow=C("yellow", 15, True),
                         segs=6)
    head.add(eyes)
    for side in (-1, 1):
        ear = k.cone(0.05, 0.18, 4, top_lit("magenta", 7, 9), phase=math.pi / 4)
        head.add(ear, T(side * 0.13, 0.02, hz + 0.01) @ R(Y, side * 72) @ R(X, -10))

    hat = m.part("Hat", (0, 0.02, 0.66), parent="Head")
    hg = k.lathe([(0.0, 0.0), (0.28, 0.0), (0.295, 0.03), (0.16, 0.05), (0.135, 0.13), (0.1, 0.23), (0.065, 0.3)], 9,
                 top_lit("yellow", 13, 15, 10), phase=math.pi / 9)
    hg.paint(C("brown", 6), where=lambda c, n: 0.05 < c.z < 0.11 and abs(n.z) < 0.6)
    hg.add(k.sweep([(0, 0, 0.29), (0, 0.02, 0.36), (0, 0.08, 0.41), (0, 0.15, 0.4)], [0.066, 0.045, 0.026, 0.0], 5,
                   top_lit("yellow", 13, 15, 11)))
    hat.add(hg, T(0, 0.02, 0.66) @ R(X, -20))  # tilted back: the brim does not hide the glowing eyes

    grip = Vector((-0.2, -0.1, 0.3))
    staff = m.part("Staff", grip, parent="Body")
    base, top = Vector((-0.22, -0.08, 0.02)), Vector((-0.235, -0.13, 0.9))
    staff.add(k.sweep([base, (-0.215, -0.1, 0.45), top], [0.024, 0.027, 0.03], 5, top_lit("brown", 8, 10)))
    for i in range(3):
        a = math.radians(90 + 120 * i)
        tipv = top + Vector((math.cos(a) * 0.055, math.sin(a) * 0.055, 0.13))
        staff.add(k.sweep([top, top + Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.05)), tipv],
                          [0.02, 0.016, 0.0], 4, C("brown", 7)))
    gc = top + Vector((0, 0, 0.085))
    m.part("StaffGem", gc, parent="Staff").add(
        k.lathe([(0.0, -0.07), (0.06, 0.0), (0.0, 0.09)], 5, C("magenta", 14, True), phase=math.pi / 5), T(*gc))
    return m.scale_all(1.2)


# ================================================================== Shroom (healer)
def healer():
    m = k.Model("Enemy_Healer", ENEMY_BUDGET)
    stem = m.part("Stem", (0, 0, 0))
    sg = k.lathe([(0.19, 0.0), (0.225, 0.06), (0.215, 0.25), (0.18, 0.4), (0.15, 0.47)], 10,
                 C("skin", 14), phase=math.pi / 10)
    sg.paint(k.vgrad("skin", 12, 15, 0.0, 0.35))
    stem.add(sg)
    for side in (-1, 1):
        stem.add(k.sweep([(side * 0.18, -0.02, 0.2), (side * 0.25, -0.05, 0.15), (side * 0.28, -0.07, 0.1)],
                         [0.042, 0.036, 0.0], 5, C("skin", 14)))
        stem.add(on_surface(sg, k.dome(0.038, 0.021, 0.008, 5, C("pink", 13)), side * 0.145, 0.15, elevation=10,
                            sink=0.002))
    stem.add(on_surface(sg, k.slab(smile(0.07, 0.035), 0.02, C("red", 3)), 0.0, 0.135, elevation=15, sink=0.004))

    cap = m.part("Cap", (0, 0.03, 0.44), parent="Stem")
    prof = [(0.12, 0.0), (0.36, 0.035), (0.405, 0.08), (0.37, 0.19), (0.26, 0.29), (0.12, 0.342), (0.0, 0.355)]
    cg = k.lathe(prof, 12, C("pink", 12), phase=math.pi / 12)
    cg.paint(lambda c, n: C("pink", 7) if n.z < -0.5 else C("pink", 11 if c.z < 0.1 else 13 if c.z < 0.25 else 14))
    spots = Geo()
    for ang, rad, size in ((0, 0.0, 0.085), (35, 0.24, 0.07), (115, 0.26, 0.062), (200, 0.25, 0.068),
                           (280, 0.24, 0.062), (160, 0.35, 0.048)):
        a = math.radians(ang)
        hit, n = k.surface(cg, (rad * math.cos(a), rad * math.sin(a), 1.0), (0, 0, -1))
        spots.add(k.dome(size, size * 0.85, 0.02, 5, C("gray", 15)), k.frame(hit - n * 0.004, n))
    cg.add(spots)
    cap.add(cg, T(0, 0.03, 0.44) @ R(X, -12))

    eyes, mid = k.eye_pair(sg, 0.235, 0.078, 0.058, 0.076, "cute", elevation=22)
    m.part("Eyes", mid, parent="Stem").add(eyes)

    spores = m.part("Spores", (0, 0, 0.55), parent="Stem")
    for i, (ang, rad, z, r) in enumerate(((50, 0.46, 0.62, 0.04), (140, 0.44, 0.8, 0.034), (220, 0.47, 0.56, 0.038),
                                          (320, 0.45, 0.76, 0.034))):
        a = math.radians(ang)
        spores.add(k.lathe([(0.0, -r), (r, 0.0), (0.0, r)], 4, C("lime", 14, True)),
                   T(rad * math.cos(a), rad * math.sin(a), z) @ R(Z, 17 * i + 20) @ R(X, 20))
    return m.scale_all(1.08)


# ================================================================== Beetle (bomber)
def bomber():
    m = k.Model("Enemy_Bomber", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    body.add(k.sphere(1.0, 8, 4, C("indigo", 2), phase=math.pi / 8).copy(T(0, 0.04, 0.155) @ S(0.27, 0.31, 0.12)))
    head = k.sphere(0.13, 8, 5, top_lit("indigo", 3, 6, 1), phase=math.pi / 8).copy(T(0, -0.27, 0.2) @ S(1.15, 0.9, 0.92))
    body.add(head)
    for side in (-1, 1):
        body.add(k.sweep([(side * 0.07, -0.36, 0.14), (side * 0.07, -0.43, 0.13), (side * 0.03, -0.47, 0.12)],
                         [0.026, 0.02, 0.0], 4, C("orange", 9)))
        body.add(k.sweep([(side * 0.05, -0.3, 0.3), (side * 0.09, -0.36, 0.38), (side * 0.13, -0.38, 0.41)],
                         [0.014, 0.012, 0.0], 3, C("indigo", 4)))
    eyes, _ = k.eye_pair(head, 0.24, 0.058, 0.052, 0.066, "cute", elevation=30)
    body.add(eyes)

    shell = m.part("Shell", (0, 0.06, 0.2), parent="Body")
    dome_prof = [(0.0, 0.0), (0.3, 0.0), (0.315, 0.07), (0.27, 0.18), (0.16, 0.26), (0.0, 0.29)]
    shg = k.lathe(dome_prof, 10, C("teal", 6), phase=math.pi / 10).copy(S(1.0, 1.08, 1.0))
    shg.paint(shine(lambda c, n: C("teal", 3 if n.z < -0.5 else 5 if c.z < 0.08 else 6 if c.z < 0.2 else 7),
                    "teal", 10, thresh=0.86, zmin=0.12))
    arc = [(0.0, -0.33, 0.04), (0.0, -0.24, 0.21), (0.0, 0.0, 0.302), (0.0, 0.24, 0.21), (0.0, 0.33, 0.04)]
    shg.add(k.sweep(arc, 0.018, 4, C("teal", 1), phase=math.pi / 4))
    shg.add(k.cyl(0.07, 0.05, 8, top_lit("yellow", 12, 14)), T(0, 0.12, 0.245))  # hazard ring at the fuse
    shell.add(shg, T(0, 0.04, 0.2))

    fb = Vector((0, 0.16, 0.48))
    fuse = m.part("Fuse", fb, parent="Shell")
    rope = [fb, fb + Vector((0, 0.01, 0.07)), fb + Vector((0, 0.06, 0.13)), fb + Vector((0, 0.12, 0.15))]
    fuse.add(k.sweep(rope, [0.03, 0.028, 0.026, 0.024], 5, C("brown", 12)))
    tipc = rope[-1] + Vector((0, 0.02, 0.015))
    fuse.add(k.ico(0.07, C("orange", 12, True)), T(*tipc) @ R(Z, 20))
    for rot in (R(X, 30), R(Y, 60) @ R(X, -40)):  # spiky yellow spark
        fuse.add(k.lathe([(0.0, -0.085), (0.035, 0.0), (0.0, 0.085)], 4, C("yellow", 15, True)),
                 T(*(tipc + Vector((0, -0.01, 0.02)))) @ rot)

    for name, side in (("LegsR", -1), ("LegsL", 1)):
        legs = m.part(name, (side * 0.2, 0.02, 0.14), parent="Body")
        for y, splay in ((-0.14, -0.06), (0.03, 0.0), (0.19, 0.07)):
            legs.add(k.sweep([(side * 0.17, y, 0.15), (side * 0.3, y + splay * 0.5, 0.13), (side * 0.36, y + splay, 0.0)],
                             [0.032, 0.028, 0.016], 3, C("indigo", 2)))
    return m.scale_all(1.15)


# ================================================================== Totem
def totem():
    m = k.Model("Enemy_Totem", ENEMY_BUDGET)
    base = m.part("Base", (0, 0, 0))
    base.add(k.box(0.64, 0.62, 0.1, top_lit("gray", 8, 10, 6), top_scale=(0.9, 0.9)))
    pole = k.lathe([(0.25, 0.1), (0.25, 0.2), (0.255, 0.23), (0.255, 0.27), (0.25, 0.3), (0.25, 0.38), (0.22, 0.41),
                    (0.25, 0.44), (0.25, 0.78), (0.22, 0.81), (0.25, 0.84), (0.25, 1.02)], 8, C("brown", 8),
                   phase=math.pi / 8)
    pole.paint(bands("brown", [(0.38, 0.44, 4), (0.78, 0.84, 4)], C("brown", 8)))
    pole.paint(C("brown", 7), where=lambda c, n: c.z < 0.2)
    base.add(pole)
    # thunderbird head on top (beak + wings = readable totem silhouette from the high camera)
    head = k.box(0.46, 0.46, 0.2, top_lit("brown", 9, 11, 7), top_scale=(0.86, 0.82), base_z=1.0)
    base.add(head)
    beak = k.slab([(0.0, -0.05), (0.12, -0.045), (0.25, -0.03), (0.14, 0.05), (0.0, 0.1)], 0.17, C("orange", 10),
                  cap_color=C("yellow", 12))
    base.add(beak, T(0, -0.19, 1.1) @ R(Z, -90) @ R(X, 90))
    for side in (-1, 1):
        base.add(on_surface(head, k.dome(0.042, 0.042, 0.015, 5, C("gray", 14)), side * 0.13, 1.14, elevation=10,
                            sink=0.004))
        base.add(on_surface(head, k.dome(0.024, 0.026, 0.02, 4, C("brown", 1)), side * 0.13, 1.14, elevation=10,
                            sink=-0.006))
    # lower carved face (static, darker) - two stacked faces read as a totem pole
    low = Geo()
    low.add(k.prism([(-0.17, -0.1), (0.17, -0.1), (0.19, 0.06), (0.13, 0.12), (-0.13, 0.12), (-0.19, 0.06)],
                    -0.02, 0.03, C("brown", 6), cap_color=C("brown", 8)))
    for side in (-1, 1):
        low.add(k.dome(0.05, 0.04, 0.015, 5, C("brown", 2)), T(side * 0.075, 0.02, 0.03))
    low.add(k.box(0.17, 0.035, 0.02, C("teal", 8), base_z=0.025), T(0, -0.055, 0))
    base.add(low, T(0, -0.245, 0.27) @ R(X, 90 - 6))
    wingp = [(0.0, 0.0), (0.2, -0.04), (0.34, 0.05), (0.31, 0.13), (0.2, 0.1), (0.0, 0.15)]
    wing = k.slab(wingp, 0.07, C("brown", 8), cap_color=C("brown", 9))
    wing.paint(C("teal", 9), where=lambda c, n: c.x > 0.23)
    for side in (-1, 1):
        wgeo = wing.copy(R(X, 90))
        if side < 0:
            wgeo = mirror_x(wgeo)
        base.add(wgeo, T(side * 0.19, 0.02, 0.99) @ R(Y, -side * 14))

    fc = Vector((0, -0.25, 0.62))
    face = m.part("Face", fc, parent="Base")
    mask = [(-0.2, -0.17), (0.2, -0.17), (0.22, 0.12), (0.16, 0.2), (-0.16, 0.2), (-0.22, 0.12)]
    fg = Geo()
    fg.add(k.prism(mask, -0.03, 0.04, C("brown", 9), cap_color=C("brown", 11)))
    fg.add(k.box(0.4, 0.05, 0.05, C("brown", 5), base_z=0.02), T(0, 0.085, 0))  # brow ridge
    for side in (-1, 1):
        fg.add(k.dome(0.07, 0.055, 0.02, 6, C("brown", 2)), T(side * 0.09, -0.01, 0.04))  # sockets
    fg.add(k.prism([(-0.03, -0.02), (0.03, -0.02), (0.0, 0.07)], 0.04, 0.08, C("brown", 8)), T(0, -0.05, 0))
    fg.add(k.prism([(-0.12, -0.14), (0.12, -0.14), (0.1, -0.08), (-0.1, -0.08)], 0.035, 0.05, C("brown", 2)))
    for tx in (-0.06, 0.0, 0.06):
        fg.add(k.box(0.035, 0.035, 0.03, C("gray", 14), base_z=0.03), T(tx, -0.105, 0))
    tilt = T(*fc) @ R(X, 90 - 10)
    face.add(fg, tilt)
    eyes = Geo()
    mids = []
    for side in (-1, 1):
        p = tilt @ Vector((side * 0.09, -0.01, 0.055))
        n = (tilt.to_3x3() @ Vector((0, 0, 1))).normalized()
        eyes.add(k.eye(p, n, 0.05, 0.038, "glow", glow=C("cyan", 15, True), segs=6))
        mids.append(p)
    m.part("Eyes", (mids[0] + mids[1]) / 2, parent="Face").add(eyes)
    return m.scale_all(1.05)


# ================================================================== Bone Wall
def bone_piece(length, radius, depth, color, cap_color):
    """Dog-bone silhouette slab along X (centred), extruded in Z. 14-point outline."""
    L, r = length / 2, radius
    kk = r * 1.6
    right = [(L, -r), (L + kk * 0.35, -kk), (L + kk, -kk * 0.55), (L + kk * 0.6, 0.0), (L + kk, kk * 0.55),
             (L + kk * 0.35, kk)]
    left = [(-x, -y) for x, y in right]
    return k.slab(right + left, depth, color, cap_color=cap_color)


def bone_wall():
    m = k.Model("Enemy_BoneWall", ENEMY_BUDGET)
    wall = m.part("Wall", (0, 0, 0))
    wall.add(k.box(0.8, 0.5, 0.56, top_lit("purple", 2, 3, 1), top_scale=(0.95, 0.92)))
    front = -0.25
    for (x, z, ang, ln) in ((-0.2, 0.09, 4, 0.32), (0.2, 0.08, -5, 0.3), (0.0, 0.27, -3, 0.5), (-0.23, 0.44, -8, 0.3),
                            (0.24, 0.45, 9, 0.3)):
        wall.add(bone_piece(ln, 0.038, 0.09, C(BONE, 12), C(BONE, 14)), T(x, front, z) @ R(Y, ang) @ R(X, 90))
    for (x, y, z, ang, ln) in ((-0.12, 0.06, 0.575, 14, 0.4), (0.16, 0.14, 0.575, -24, 0.36),
                               (0.2, -0.12, 0.575, 70, 0.3), (-0.22, 0.16, 0.6, 100, 0.3)):
        wall.add(bone_piece(ln, 0.042, 0.08, C(BONE, 13), C(BONE, 15)), T(x, y, z) @ R(Z, ang))
    for (x, z, s, y) in ((0.02, 0.4, 1.0, front + 0.02), (0.0, 0.6, 0.85, -0.02)):
        sk = k.sphere(0.13 * s, 8, 4, C(BONE, 14), phase=math.pi / 8)
        sk.deform(lambda v: Vector((v.x, v.y * 0.9, v.z * (0.95 if v.z > 0 else 0.8))))
        sk.paint(lambda c, n: C(BONE, 15 if n.z > 0.5 else 13 if n.z > -0.3 else 11))
        pos = Vector((x, y, z))
        skg = sk.copy(T(*pos))
        wall.add(skg)
        eyes, _ = k.eye_pair(skg, pos.z + 0.005 * s, 0.05 * s, 0.038 * s, 0.042 * s, "glow", elevation=30, x_center=x,
                             glow=C("purple", 15, True), segs=5)
        wall.add(eyes)
        wall.add(k.box(0.1 * s, 0.03, 0.022, C(BONE, 3)), T(x, pos.y - 0.11 * s, pos.z - 0.075 * s))
    return m.scale_all(1.08)


REGISTRY = {
    "Enemy_Slime": slime,
    "Enemy_Bat": bat,
    "Enemy_Skeleton": skeleton,
    "Enemy_ShieldKnight": shield_knight,
    "Enemy_Mage": mage,
    "Enemy_Healer": healer,
    "Enemy_Bomber": bomber,
    "Enemy_Totem": totem,
    "Enemy_BoneWall": bone_wall,
}
