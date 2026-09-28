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


def on_top(target, piece, x, y, sink=0.0, from_z=3.0):
    """Place `piece` (built facing +Z) on the upper surface of `target` at (x, y), ray-cast from above."""
    hit, n = k.surface(target, (x, y, from_z), (0, 0, -1))
    return piece.copy(k.frame(hit - n * sink, n))


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
    # green family at mid shades: highest floor contrast in all three acts (game_look swatch check: min dE ~78 vs
    # the Act 1-3 floors; teal ~50, and the old lime 11-13 clipped to flat lemon-yellow under the warm Act 1 grade),
    # dark rim at the floor for the silhouette
    g = k.lathe(prof, 16, C("green", 9), phase=math.pi / 16)
    g.paint(k.vgrad("green", 7, 10, 0.03, 0.58))
    g.paint(C("green", 4), where=lambda c, n: c.z < 0.02 or (c.z < 0.06 and n.z < -0.3))
    tip = k.sweep([(0, 0.0, 0.62), (0, 0.02, 0.70), (0, 0.07, 0.75), (0, 0.12, 0.745)], [0.05, 0.036, 0.022, 0.0], 6,
                  C("green", 10))
    body.add(g)
    body.add(tip)
    # glossy highlight blob (upper-left), mouth, blush
    for x, y, rx, ry in ((-0.17, 0.02, 0.085, 0.05), (-0.05, 0.1, 0.03, 0.022)):  # glossy highlight, from above
        body.add(on_top(g, k.dome(rx, ry, 0.012, 6, C("green", 13)), x, y, sink=0.002))
    body.add(on_surface(g, k.slab(smile(0.13, 0.064), 0.02, C("red", 2)), 0.0, 0.25, elevation=26, sink=0.004))
    for side in (-1, 1):
        body.add(on_surface(g, k.dome(0.05, 0.03, 0.01, 6, C("pink", 12)), side * 0.27, 0.29, elevation=16,
                            sink=0.002))
    # big eyes high on the front, tilted up toward the 58 deg camera (~8 px wide each at game scale)
    eyes, mid = k.eye_pair(g, 0.43, 0.16, 0.125, 0.155, "cute", elevation=46)
    m.part("Eyes", mid, parent="Body").add(eyes)
    return m


# ================================================================== Bat
def bat():
    m = k.Model("Enemy_Bat", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0.40))
    cz = 0.47
    # light lavender bat: against the mid-dark Act 2-3 floors only a LIGHT purple separates (game_look metric: dark
    # plum / crimson variants dropped to dE ~36-44), with dark wing bones + rim and a pink face mask as the value
    # breaks that keep it readable on the mid Act 1 floor
    g = k.sphere(0.225, 12, 7, C("purple", 10), phase=math.pi / 12).copy(T(0, 0, cz) @ S(1.0, 0.92, 0.94))
    g.paint(k.vgrad("purple", 8, 11, cz - 0.2, cz + 0.2))
    g.paint(C("purple", 12), where=lambda c, n: n.y < -0.55 and c.z < cz - 0.03)  # lighter belly
    g.paint(C("pink", 13), where=lambda c, n: n.y < -0.45 and cz - 0.03 <= c.z < cz + 0.15)  # light face mask
    body.add(g)
    for side in (-1, 1):
        ear = k.cone(0.085, 0.21, 4, top_lit("purple", 8, 10), phase=math.pi / 4)
        ear.paint(C("pink", 12), where=lambda c, n: n.y < -0.5)
        body.add(ear, T(side * 0.11, 0.02, cz + 0.13) @ R(Y, side * 24) @ R(X, 8))
        body.add(k.cone(0.024, 0.06, 3, C("gray", 15), phase=math.pi / 2), T(side * 0.04, -0.19, cz - 0.075) @ R(X, 180))
        body.add(k.cone(0.04, 0.08, 4, C("purple", 6)), T(side * 0.08, 0.0, cz - 0.17) @ R(X, 180))
    poly = [(0.0, -0.04), (0.06, -0.10), (0.13, 0.01), (0.20, -0.09), (0.27, 0.04), (0.35, -0.05), (0.41, 0.10),
            (0.44, 0.27), (0.20, 0.20), (0.0, 0.09)]
    poly = [(x * 1.12, y * 1.12) for x, y in poly]
    wing = k.slab(poly, 0.04, C("purple", 4), cap_color=C("purple", 11))  # light membrane (caps), dark rim (sides)
    # light wing bones on the camera side of the membrane: arm along the leading edge + three fingers
    wrist = Vector((0.2 * 1.12, 0.2 * 1.12, 0.02))
    wing.add(k.sweep([(0.0, 0.09 * 1.12, 0.02), wrist, (0.44 * 1.12, 0.27 * 1.12, 0.02)], [0.026, 0.022, 0.012], 3,
                     C("purple", 4)))
    for tip in ((0.13, 0.01), (0.27, 0.04), (0.41, 0.10)):
        wing.add(k.sweep([wrist, (tip[0] * 1.12, tip[1] * 1.12, 0.02)], [0.018, 0.009], 3, C("purple", 4)))
    # wings raised in a V (mid-flap): tips stay inside +-0.48 m, so they never cut into neighbouring cells
    wing_left = wing.copy(R(Y, -38) @ R(X, 42))  # membrane faces up/front (toward the high camera)
    for name, side in (("WingL", 1), ("WingR", -1)):
        pivot = Vector((side * 0.14, 0.03, cz + 0.02))
        wg = wing_left if side > 0 else mirror_x(wing_left)
        m.part(name, pivot, parent="Body").add(wg, T(*pivot))
    eyes, mid = k.eye_pair(g, cz + 0.045, 0.1, 0.09, 0.112, "cute", elevation=40)
    m.part("Eyes", mid, parent="Body").add(eyes)
    return m


# ================================================================== Skeleton
def skeleton():
    m = k.Model("Enemy_Skeleton", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    for side in (-1, 1):
        body.add(k.sweep([(side * 0.09, 0.0, 0.22), (side * 0.095, -0.01, 0.06)], 0.047, 4, C(BONE, 11),
                         phase=math.pi / 4))
        body.add(k.box(0.12, 0.17, 0.06, top_lit(BONE, 11, 13), top_scale=(0.9, 0.8)), T(side * 0.1, -0.03, 0))
    body.add(k.box(0.27, 0.15, 0.08, top_lit(BONE, 11, 13), top_scale=(1.0, 0.9)), T(0, 0, 0.18))
    body.add(k.cyl(0.13, 0.2, 6, C(BONE, 4), phase=math.pi / 6), S(1.0, 0.8, 1.0) @ T(0, 0, 0.25))  # dark core
    for z0, z1, r in ((0.27, 0.315, 0.165), (0.33, 0.38, 0.19), (0.395, 0.445, 0.18)):  # three rib bands
        body.add(k.cyl(r, z1 - z0, 8, top_lit(BONE, 12, 14), phase=math.pi / 8, r_top=r * 0.94),
                 S(1.0, 0.82, 1.0) @ T(0, 0, z0))
    scarf = k.lathe([(0.2, 0.45), (0.195, 0.505), (0.1, 0.53)], 8, top_lit("red", 9, 11, 7), phase=math.pi / 8)
    body.add(scarf, S(1.0, 0.88, 1.0))
    tail = k.slab([(-0.05, 0.0), (0.05, 0.0), (0.06, 0.19), (0.0, 0.15), (-0.06, 0.2)], 0.03, C("red", 8),
                  cap_color=C("red", 9))
    body.add(tail, T(0.08, 0.14, 0.49) @ R(Z, 15) @ R(X, 25) @ R(X, -90))

    head = m.part("Head", (0, 0, 0.5), parent="Body")
    hz = 0.735  # skull top ~1.03 m after scaling: solid mass stays under ~1 m so the cell behind stays readable
    skull = k.sphere(0.25, 10, 5, C(BONE, 13), phase=math.pi / 10)
    skull.deform(lambda v: Vector((v.x * (1.0 if v.z > -0.06 else 0.84), v.y * 0.9, v.z * 0.86)))
    skull = skull.copy(T(0, 0, hz))
    skull.paint(k.vgrad(BONE, 11, 14, hz - 0.17, hz + 0.16))
    head.add(skull)
    head.add(k.box(0.27, 0.17, 0.09, top_lit(BONE, 11, 13, 10), top_scale=(1.05, 1.0)), T(0, -0.06, hz - 0.255))
    head.add(k.box(0.17, 0.02, 0.018, C(BONE, 3)), T(0, -0.148, hz - 0.2))  # mouth line
    # big dark sockets with a hot red pupil: the skull is the skeleton's read at ~29 px per cell
    sockets, _ = k.eye_pair(skull, hz - 0.015, 0.105, 0.1, 0.104, "socket", elevation=30, glow=C("red", 10, True),
                            segs=6, look=(0.0, 0.0))
    head.add(sockets)
    nose = k.slab([(-0.028, 0.025), (0.0, -0.025), (0.028, 0.025)], 0.02, C(BONE, 2))
    head.add(on_surface(skull, nose, 0.0, hz - 0.1, elevation=20, sink=0.006))

    for name, side in (("ArmR", -1), ("ArmL", 1)):
        sh = Vector((side * 0.18, 0.0, 0.43))
        hand = Vector((side * 0.27, -0.1, 0.28))
        arm = m.part(name, sh, parent="Body")
        arm.add(k.sweep([sh, (side * 0.25, -0.02, 0.35), hand], 0.038, 5, C(BONE, 12)))
        arm.add(k.sphere(0.062, 5, 3, top_lit(BONE, 12, 14)), T(*hand))
    grip = Vector((-0.27, -0.1, 0.28))
    club = m.part("Weapon", grip, parent="ArmR")
    top = grip + Vector((-0.02, -0.1, 0.42))  # held upright: the knobbed end stays inside the cell
    sd = (top - grip).normalized()
    club.add(k.sweep([grip - sd * 0.1, top], [0.038, 0.052], 5, C(BONE, 12)))
    side_dir = sd.cross(Y).normalized()
    for s in (-1, 1):
        club.add(k.sphere(0.074, 6, 3, top_lit(BONE, 13, 14, 11)), T(*(top + side_dir * s * 0.056)))
    club.add(k.sphere(0.05, 5, 3, C(BONE, 11)), T(*(grip - sd * 0.1)))
    m.tilt_up("Head", 18)  # skull looks up at the camera: sockets + jaw face it instead of the cranium
    return m.scale_all(1.1)


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
        pad = k.sphere(0.1, 8, 3, top_lit("gray", 10, 12, 7), phase=math.pi / 8)
        body.add(pad, T(side * 0.2, 0.01, 0.45) @ S(1.0, 1.0, 0.8))
    body.add(k.sweep([(-0.2, 0.0, 0.42), (-0.25, -0.02, 0.34), (-0.28, -0.06, 0.28)], 0.045, 5, top_lit(steel, 10, 12)))
    body.add(k.sphere(0.052, 5, 3, top_lit("indigo", 5, 7)), T(-0.285, -0.07, 0.27))
    body.add(k.sweep([(0.2, 0.0, 0.42), (0.2, -0.12, 0.38), (0.12, -0.22, 0.36)], 0.045, 5, top_lit(steel, 10, 12)))

    head = m.part("Head", (0, 0, 0.5), parent="Body")
    hz = 0.66
    helm = k.lathe([(0.12, 0.5), (0.165, 0.55), (0.175, 0.64), (0.16, 0.73), (0.11, 0.8), (0.0, 0.83)], 10,
                   top_lit("gray", 10, 12, 7), phase=math.pi / 10)  # silver helm over the blue body + shield
    helm.paint(shine(top_lit("gray", 10, 12, 7), "gray", 13, thresh=0.9, zmin=0.6))
    head.add(helm)
    visor = k.box(0.25, 0.1, 0.055, C("indigo", 1)).copy(T(0, -0.13, hz - 0.035) @ R(X, -12))
    head.add(visor)
    for side in (-1, 1):
        glint = k.dome(0.045, 0.028, 0.012, 5, C("yellow", 12, True))
        head.add(on_surface(visor, glint, side * 0.055, hz - 0.01, elevation=20, sink=0.004, blend=0.3))
    crest = k.slab([(-0.07, 0.0), (0.2, 0.0), (0.25, 0.11), (0.13, 0.22), (-0.03, 0.17), (-0.12, 0.09)], 0.075,
                   C("red", 10), cap_color=C("red", 12))
    head.add(crest, T(0, 0, 0.77) @ R(Z, 90) @ R(X, 90) @ T(-0.05, 0, 0))

    sc = Vector((0.02, -0.29, 0.36))
    shield = m.part("Shield", sc, parent="Body")
    heater = [(0.0, -0.32), (0.2, -0.19), (0.275, 0.03), (0.275, 0.25), (-0.275, 0.25), (-0.275, 0.03), (-0.2, -0.19)]
    sg = Geo()
    # gold rim around a saturated blue field: the blocking face must read on the Act 2 blue floor as well
    sg.add(k.prism(heater, -0.03, 0.02, C("yellow", 8), cap_color=C("yellow", 11)))
    inner = [(x * 0.8, y * 0.8 + 0.01) for x, y in heater]
    sg.add(k.prism(inner, 0.015, 0.035, C(steel, 8), cap_color=C(steel, 10)))
    cross = [(-0.035, -0.17), (0.035, -0.17), (0.035, 0.06), (0.12, 0.06), (0.12, 0.13), (0.035, 0.13), (0.035, 0.2),
             (-0.035, 0.2), (-0.035, 0.13), (-0.12, 0.13), (-0.12, 0.06), (-0.035, 0.06)]
    sg.add(k.prism(cross, 0.03, 0.05, C("yellow", 10), cap_color=C("yellow", 13)))
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
    m.tilt_up("Head", 12)  # visor + glowing eye slits face the high camera
    return m.scale_all(1.25)


# ================================================================== Imp Mage
def mage():
    m = k.Model("Enemy_Mage", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    robe = k.lathe([(0.25, 0.0), (0.24, 0.06), (0.15, 0.38), (0.09, 0.47)], 10, C("red", 10), phase=math.pi / 10)
    robe.paint(k.vgrad("red", 9, 12, 0.0, 0.45))
    robe.paint(top_lit("yellow", 10, 12, 8), where=lambda c, n: c.z < 0.06)  # gold hem
    body.add(robe)
    body.add(k.lathe([(0.2, 0.235), (0.185, 0.275)], 10, top_lit("yellow", 10, 12), phase=math.pi / 10))  # sash
    for side in (-1, 1):
        sh = Vector((side * 0.1, 0.0, 0.42))
        hand = Vector((side * 0.2, -0.1, 0.3))
        body.add(k.sweep([sh, (side * 0.17, -0.04, 0.36), hand + Vector((0, 0.03, 0.02))], [0.04, 0.05, 0.068], 6,
                         top_lit("red", 10, 12, 7)))
        body.add(k.sphere(0.048, 5, 3, C("indigo", 2)), T(*hand))

    head = m.part("Head", (0, 0, 0.46), parent="Body")
    hz = 0.585
    # imp: dark warm-red face under the brim (keeps the dark face band + glowing eyes), long pointed ears
    face = k.sphere(0.155, 10, 5, top_lit("red", 3, 4, 2), phase=math.pi / 10).copy(T(0, 0, hz) @ S(1.0, 0.95, 0.9))
    head.add(face)
    eyes, _ = k.eye_pair(face, hz + 0.005, 0.078, 0.06, 0.076, "glow", elevation=40, glow=C("yellow", 13, True),
                         segs=6)
    head.add(eyes)
    for side in (-1, 1):
        ear = k.cone(0.055, 0.25, 4, top_lit("red", 7, 9, 5), phase=math.pi / 4)
        head.add(ear, T(side * 0.12, 0.02, hz + 0.0) @ R(Y, side * 76) @ R(X, -12))

    hat = m.part("Hat", (0, 0.02, 0.66), parent="Head")
    hg = k.lathe([(0.0, 0.0), (0.28, 0.0), (0.295, 0.03), (0.16, 0.05), (0.135, 0.13), (0.1, 0.23), (0.065, 0.3)], 9,
                 top_lit("yellow", 10, 12, 8), phase=math.pi / 9)
    hg.paint(C("brown", 6), where=lambda c, n: 0.05 < c.z < 0.11 and abs(n.z) < 0.6)
    hg.add(k.sweep([(0, 0, 0.29), (0, 0.02, 0.36), (0, 0.08, 0.41), (0, 0.15, 0.4)], [0.066, 0.045, 0.026, 0.0], 5,
                   top_lit("yellow", 10, 12, 9)))
    for side in (-1, 1):  # little imp horns poking up through the brim
        hg.add(k.sweep([(side * 0.17, -0.1, 0.0), (side * 0.215, -0.11, 0.09), (side * 0.235, -0.07, 0.16)],
                       [0.04, 0.026, 0.0], 4, top_lit("red", 5, 7, 4), phase=math.pi / 4))
    hat.add(hg, T(0, 0.02, 0.66) @ R(X, -26))  # tilted back: the brim does not hide the glowing eyes

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
        k.lathe([(0.0, -0.07), (0.06, 0.0), (0.0, 0.09)], 5, C("magenta", 11, True), phase=math.pi / 5), T(*gc))
    m.icon_crop = 0.16  # tall + thin: frame hat, face and robe; the hem and staff foot run off the bottom edge
    m.icon_view = (24.0, 26.0)  # from the left-front: the staff stands beside the body instead of across the face
    m.tilt_up("Head", 10)  # imp face + eyes toward the camera (the hat tips back with it)
    return m.scale_all(1.2)


# ================================================================== Shroom (healer)
def healer():
    m = k.Model("Enemy_Healer", ENEMY_BUDGET)
    stem = m.part("Stem", (0, 0, 0))
    sg = k.lathe([(0.19, 0.0), (0.225, 0.06), (0.215, 0.25), (0.18, 0.4), (0.13, 0.53)], 10,
                 C("skin", 12), phase=math.pi / 10)
    sg.paint(k.vgrad("skin", 10, 13, 0.0, 0.35))
    stem.add(sg)
    for side in (-1, 1):
        stem.add(k.sweep([(side * 0.18, -0.02, 0.2), (side * 0.25, -0.05, 0.15), (side * 0.28, -0.07, 0.1)],
                         [0.042, 0.036, 0.0], 5, C("skin", 12)))
        stem.add(on_surface(sg, k.dome(0.038, 0.022, 0.008, 5, C("pink", 11)), side * 0.165, 0.15, elevation=12,
                            sink=0.002))
    stem.add(on_surface(sg, k.slab(smile(0.072, 0.036), 0.02, C("red", 3)), 0.0, 0.125, elevation=18, sink=0.004))

    cap = m.part("Cap", (0, 0.03, 0.44), parent="Stem")
    prof = [(0.12, 0.0), (0.36, 0.035), (0.405, 0.08), (0.37, 0.19), (0.26, 0.29), (0.12, 0.342), (0.0, 0.355)]
    # orange toadstool with cream spots: pink / rose scored the lowest floor contrast of any family (min dE ~41 vs
    # orange ~69) and washed out to pastel in the Act 1 capture
    cg = k.lathe(prof, 12, C("orange", 10), phase=math.pi / 12)
    cg.paint(lambda c, n: C("orange", 5) if n.z < -0.5 else C("orange", 8 if c.z < 0.1 else 9 if c.z < 0.25 else 10))
    spots = Geo()
    for ang, rad, size in ((0, 0.0, 0.085), (35, 0.24, 0.07), (115, 0.26, 0.062), (200, 0.25, 0.068),
                           (280, 0.24, 0.062), (160, 0.35, 0.048)):
        a = math.radians(ang)
        hit, n = k.surface(cg, (rad * math.cos(a), rad * math.sin(a), 1.0), (0, 0, -1))
        spots.add(k.dome(size * 1.1, size * 0.95, 0.02, 5, C("gray", 13)), k.frame(hit - n * 0.004, n))
    cg.add(spots)
    cap.add(cg, T(0, 0.03, 0.44) @ R(X, -20))  # pushed back: the face stays visible from the high camera

    eyes, mid = k.eye_pair(sg, 0.26, 0.092, 0.078, 0.098, "cute", elevation=36)
    m.part("Eyes", mid, parent="Stem").add(eyes)

    spores = m.part("Spores", (0, 0, 0.55), parent="Stem")
    for i, (ang, rad, z, r) in enumerate(((50, 0.46, 0.62, 0.04), (140, 0.44, 0.8, 0.034), (220, 0.47, 0.56, 0.038),
                                          (320, 0.45, 0.76, 0.034))):
        a = math.radians(ang)
        spores.add(k.lathe([(0.0, -r), (r, 0.0), (0.0, r)], 4, C("lime", 11, True)),
                   T(rad * math.cos(a), rad * math.sin(a), z) @ R(Z, 17 * i + 20) @ R(X, 20))
    return m.scale_all(1.08)


# ================================================================== Beetle (bomber)
def bomber():
    m = k.Model("Enemy_Bomber", ENEMY_BUDGET)
    body = m.part("Body", (0, 0, 0))
    body.add(k.sphere(1.0, 6, 3, C("indigo", 2), phase=math.pi / 6).copy(T(0, 0.04, 0.155) @ S(0.27, 0.31, 0.12)))
    head = k.sphere(0.145, 8, 5, top_lit("indigo", 3, 5, 1), phase=math.pi / 8).copy(T(0, -0.27, 0.21) @ S(1.15, 0.9, 0.92))
    body.add(head)
    for side in (-1, 1):
        body.add(k.sweep([(side * 0.07, -0.35, 0.14), (side * 0.07, -0.41, 0.13), (side * 0.035, -0.44, 0.12)],
                         [0.026, 0.02, 0.0], 4, C("orange", 9)))  # mandibles stay inside the cell
        body.add(k.sweep([(side * 0.05, -0.3, 0.3), (side * 0.09, -0.36, 0.38), (side * 0.13, -0.38, 0.41)],
                         [0.014, 0.012, 0.0], 3, C("indigo", 4)))
    # big white eyes on the dark head, high and tilted up so they clear the shell rim from the 58 deg camera
    eyes, _ = k.eye_pair(head, 0.27, 0.075, 0.074, 0.088, "cute", elevation=50)
    body.add(eyes)

    shell = m.part("Shell", (0, 0.06, 0.2), parent="Body")
    dome_prof = [(0.0, 0.0), (0.3, 0.0), (0.315, 0.07), (0.27, 0.18), (0.16, 0.26), (0.0, 0.29)]
    shg = k.lathe(dome_prof, 10, C("yellow", 10), phase=math.pi / 10).copy(S(1.0, 1.08, 1.0))

    # bright yellow "bomb bug" shell with big black spots and the dark elytra seam: a strong colour block that reads
    # on the brown Act 1 and the blue / violet Act 2-3 floors alike (the old dark teal shell sat at floor value)
    shg.paint(shine(lambda c, n: C("indigo", 1) if n.z < -0.5 else C("yellow", 8 if c.z < 0.06 else 10 if c.z < 0.2
                                                                       else 11), "yellow", 12, thresh=0.86, zmin=0.12))
    for ang, rad, size in ((35, 0.2, 0.075), (145, 0.2, 0.075), (215, 0.19, 0.07), (325, 0.19, 0.07)):
        a = math.radians(ang)
        hit, n = k.surface(shg, (rad * math.cos(a), rad * math.sin(a) * 1.08, 1.0), (0, 0, -1))
        shg.add(k.dome(size, size, 0.014, 6, C("indigo", 1), rings=1), k.frame(hit - n * 0.004, n))
    arc = [(0.0, -0.33, 0.04), (0.0, -0.24, 0.21), (0.0, 0.0, 0.302), (0.0, 0.24, 0.21), (0.0, 0.33, 0.04)]
    shg.add(k.sweep(arc, 0.022, 4, C("indigo", 1), phase=math.pi / 4))
    shg.add(k.cyl(0.07, 0.05, 8, top_lit("indigo", 2, 4)), T(0, 0.12, 0.245))  # dark cap at the fuse
    shell.add(shg, T(0, 0.04, 0.2))

    fb = Vector((0, 0.16, 0.48))
    fuse = m.part("Fuse", fb, parent="Shell")
    rope = [fb, fb + Vector((0, 0.01, 0.07)), fb + Vector((0, 0.06, 0.13)), fb + Vector((0, 0.12, 0.15))]
    fuse.add(k.sweep(rope, [0.03, 0.028, 0.026, 0.024], 5, C("brown", 9)))
    tipc = rope[-1] + Vector((0, 0.02, 0.015))
    fuse.add(k.ico(0.07, C("orange", 10, True)), T(*tipc) @ R(Z, 20))
    for rot in (R(X, 30), R(Y, 60) @ R(X, -40)):  # spiky yellow spark
        fuse.add(k.lathe([(0.0, -0.085), (0.035, 0.0), (0.0, 0.085)], 4, C("yellow", 13, True)),
                 T(*(tipc + Vector((0, -0.01, 0.02)))) @ rot)

    for name, side in (("LegsR", -1), ("LegsL", 1)):
        legs = m.part(name, (side * 0.2, 0.02, 0.14), parent="Body")
        for y, splay in ((-0.14, -0.06), (0.03, 0.0), (0.19, 0.07)):
            leg = k.sweep([(side * 0.17, y, 0.15), (side * 0.3, y + splay * 0.5, 0.13), (side * 0.36, y + splay, 0.0)],
                          [0.032, 0.028, 0.016], 3, C("indigo", 2))
            leg.paint(C("indigo", 5), where=lambda c, n: abs(c.x) < 0.25)  # lighter upper leg / knee joint
            legs.add(leg)
    return m.scale_all(1.15)


# ================================================================== Totem
def totem():
    """Two stacked carved faces + thunderbird head. Solid mass kept under ~1.0 m (at the 58 deg pitch a model of
    height h covers ~0.62 h of the cell behind it, and the totem never moves)."""
    m = k.Model("Enemy_Totem", ENEMY_BUDGET)
    base = m.part("Base", (0, 0, 0))
    base.add(k.box(0.62, 0.6, 0.08, top_lit("gray", 8, 10, 6), top_scale=(0.9, 0.9)))
    pole = k.lathe([(0.24, 0.08), (0.24, 0.16), (0.245, 0.19), (0.245, 0.23), (0.24, 0.26), (0.24, 0.31), (0.21, 0.33),
                    (0.24, 0.35), (0.24, 0.66), (0.21, 0.68), (0.23, 0.7), (0.23, 0.8)], 8, C("brown", 5),
                   phase=math.pi / 8)
    # dark carved wood pole with painted blocks (red face mask, teal thunderbird): the old all-brown totem vanished
    # into the brown-lit Act 1 floor
    pole.paint(bands("brown", [(0.31, 0.35, 2), (0.66, 0.7, 2)], C("brown", 5)))
    pole.paint(C("brown", 4), where=lambda c, n: c.z < 0.16)
    base.add(pole)
    # thunderbird head on top (beak + wings = readable totem silhouette from the high camera), narrower than before
    hz = 0.78
    head = k.box(0.42, 0.42, 0.17, top_lit("teal", 8, 10, 5), top_scale=(0.86, 0.82), base_z=hz)
    base.add(head)
    beak = k.slab([(0.0, -0.05), (0.11, -0.045), (0.22, -0.03), (0.13, 0.05), (0.0, 0.09)], 0.16, C("orange", 9),
                  cap_color=C("yellow", 11))
    base.add(beak, T(0, -0.17, hz + 0.085) @ R(Z, -90) @ R(X, 90))
    for side in (-1, 1):
        base.add(on_surface(head, k.dome(0.046, 0.036, 0.016, 5, C("indigo", 1)), side * 0.12, hz + 0.12,
                            elevation=20, sink=0.004))
        base.add(on_surface(head, k.dome(0.03, 0.024, 0.02, 4, C("cyan", 11, True)), side * 0.12, hz + 0.12,
                            elevation=20, sink=-0.004))
    # lower carved face (static, darker) - two stacked faces read as a totem pole
    low = Geo()
    low.add(k.prism([(-0.15, -0.08), (0.15, -0.08), (0.17, 0.05), (0.12, 0.1), (-0.12, 0.1), (-0.17, 0.05)],
                    -0.02, 0.03, C("brown", 6), cap_color=C("orange", 8)))
    for side in (-1, 1):
        low.add(k.dome(0.045, 0.035, 0.015, 5, C("brown", 2)), T(side * 0.07, 0.015, 0.03))
    low.add(k.box(0.15, 0.03, 0.02, C("teal", 8), base_z=0.025), T(0, -0.045, 0))
    base.add(low, T(0, -0.235, 0.2) @ R(X, 90 - 6))
    wingp = [(0.0, 0.0), (0.18, -0.035), (0.29, 0.045), (0.265, 0.115), (0.18, 0.09), (0.0, 0.13)]
    wing = k.slab(wingp, 0.07, C("teal", 7), cap_color=C("teal", 9))
    wing.paint(C("red", 9), where=lambda c, n: c.x > 0.2)
    for side in (-1, 1):
        wgeo = wing.copy(R(X, 90))
        if side < 0:
            wgeo = mirror_x(wgeo)
        base.add(wgeo, T(side * 0.17, 0.02, hz - 0.02) @ R(Y, -side * 14))

    fc = Vector((0, -0.245, 0.5))
    face = m.part("Face", fc, parent="Base")
    mask = [(-0.19, -0.155), (0.19, -0.155), (0.21, 0.11), (0.15, 0.18), (-0.15, 0.18), (-0.21, 0.11)]
    fg = Geo()
    fg.add(k.prism(mask, -0.03, 0.04, C("red", 7), cap_color=C("red", 9)))  # the big red face-mask block
    fg.add(k.box(0.38, 0.05, 0.05, C("indigo", 2), base_z=0.02), T(0, 0.075, 0))  # brow ridge
    for side in (-1, 1):
        fg.add(k.dome(0.095, 0.076, 0.02, 6, C("indigo", 1)), T(side * 0.095, -0.012, 0.04))  # sockets
    fg.add(k.prism([(-0.03, -0.02), (0.03, -0.02), (0.0, 0.065)], 0.04, 0.08, C("red", 5)), T(0, -0.05, 0))
    fg.add(k.prism([(-0.11, -0.13), (0.11, -0.13), (0.095, -0.075), (-0.095, -0.075)], 0.035, 0.05, C("indigo", 1)))
    for tx in (-0.055, 0.0, 0.055):
        fg.add(k.box(0.033, 0.033, 0.03, C("gray", 12), base_z=0.03), T(tx, -0.098, 0))
    tilt = T(*fc) @ R(X, 90 - 12)
    face.add(fg, tilt)
    eyes = Geo()
    mids = []
    for side in (-1, 1):
        p = tilt @ Vector((side * 0.095, -0.012, 0.055))
        n = (tilt.to_3x3() @ Vector((0, 0, 1))).normalized()
        eyes.add(k.eye(p, n, 0.078, 0.062, "glow", glow=C("cyan", 12, True), segs=6))
        mids.append(p)
    m.part("Eyes", (mids[0] + mids[1]) / 2, parent="Face").add(eyes)
    return m.scale_all(1.08)


# ================================================================== Bone Wall
BONE_TOP = top_lit(BONE, 11, 14, 8)    # round bones: bright tops, dark undersides -> dark gaps between bone rows
KNOB = top_lit(BONE, 12, 14, 10)


def bone(p0, p1, r, knob, spread=None, double=False, segs=4):
    """Chunky cartoon bone from p0 to p1: shaft + knobbed ends. double=True splits each end into two balls along
    `spread` (the classic dog-bone lobes, used where the camera sees the bone face-on)."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0).normalized()
    if not double:  # one lathe along the axis: round knob - shaft - round knob (40 tris)
        L = (p1 - p0).length
        prof = [(0.0, -knob * 0.9), (knob, -knob * 0.15), (r, knob * 0.9), (r, L - knob * 0.9), (knob, L + knob * 0.15),
                (0.0, L + knob * 0.9)]
        g = k.lathe(prof, 5, C(BONE, 12), phase=math.pi / 10)
        return g.copy(k.frame(p0, axis, up_hint=Vector((0.0, -0.53, 0.85)))).paint(KNOB)
    g = Geo().add(k.sweep([p0, p1], r, segs, BONE_TOP))
    if double:
        sp = Vector(spread)
        sp = (sp - axis * sp.dot(axis)).normalized()
        for end, sgn in ((p0, -1.0), (p1, 1.0)):
            for s in (-1.0, 1.0):
                g.add(k.sphere(knob, 4, 3, KNOB, phase=math.pi / 4),
                      T(*(end + axis * sgn * knob * 0.3 + sp * s * knob * 0.62)))
    return g


def big_skull(pos, r, glow):
    """Large skull facing -Y (tilted toward the high camera): white cranium, DARK eye sockets with one small
    emissive pupil each (dark sockets read at ~28 px per cell far better than a glow on white), nose, jaw."""
    sk = k.sphere(r, 7, 4, C(BONE, 13), phase=math.pi / 7)
    sk.deform(lambda v: Vector((v.x * (1.0 if v.z > -0.2 * r else 0.86), v.y * 0.9, v.z * (0.95 if v.z > 0 else 0.8))))
    sk.paint(lambda c, n: C(BONE, 14 if n.z > 0.45 else 12 if n.z > -0.3 else 9))
    g = sk.copy(T(*pos) @ R(X, -18))  # face tipped up toward the 58 deg camera
    out = Geo().add(g)
    face_dir = Vector((0.0, -math.cos(math.radians(50)), math.sin(math.radians(50))))
    for side in (-1, 1):
        hit, n = k.surface(g, (pos.x + side * 0.4 * r, -3.0, pos.z + 0.06 * r), (0, 1, 0))
        nn = (n.normalized() * 0.4 + face_dir * 0.6).normalized()
        m = k.frame(hit - nn * 0.012, nn)
        out.add(k.dome(0.38 * r, 0.41 * r, 0.06 * r, 6, C("gray", 0), rings=1), m)
        out.add(k.dome(0.13 * r, 0.14 * r, 0.09 * r, 4, glow, rings=1), m @ T(0.03 * r * side, -0.03 * r, 0.02 * r))
    tri = k.slab([(-0.1 * r, 0.09 * r), (0.0, -0.1 * r), (0.1 * r, 0.09 * r)], 0.02, C("gray", 1))
    out.add(on_surface(g, tri, pos.x, pos.z - 0.3 * r, elevation=40, sink=0.006))
    out.add(k.box(1.02 * r, 0.5 * r, 0.3 * r, top_lit(BONE, 11, 13, 10)), T(pos.x, pos.y - 0.42 * r, pos.z - 0.86 * r))
    return out


def bone_wall():
    """Summoned barricade: an irregular pile of chunky bones (knobbed ends sticking out past the sides and top,
    dark gaps between the bone rows) topped by two big skulls. No backing box: bones all round."""
    m = k.Model("Enemy_BoneWall", ENEMY_BUDGET)
    wall = m.part("Wall", (0, 0, 0))
    # shadowed ossuary filler deep inside the crib (reads as the gaps between bones, never as a box)
    core = k.ico(1.0, C("gray", 4)).copy(T(0.0, 0.04, 0.2) @ S(0.28, 0.24, 0.19))
    k.jitter(core, 0.03, 71)
    wall.add(core.paint(lambda c, n: C("gray", 5 if n.z > 0.5 else 3)))
    # log-cabin crib of chunky bones, slightly skewed so it reads as a pile, knobs poking past the corners
    for p0, p1 in (((-0.35, -0.31, 0.07), (0.36, -0.28, 0.07)),       # layer 1: across the front / back
                   ((-0.33, 0.29, 0.07), (0.34, 0.25, 0.07)),
                   ((-0.33, -0.36, 0.19), (-0.29, 0.34, 0.2)),         # layer 2: front-to-back, left / right
                   ((0.31, -0.35, 0.2), (0.34, 0.33, 0.18)),
                   ((-0.37, -0.25, 0.31), (0.35, -0.29, 0.32)),        # layer 3: across the front, under the skulls
                   ((-0.34, 0.24, 0.31), (0.33, 0.29, 0.3))):          #          and the back (bones from behind too)
        wall.add(bone(p0, p1, 0.056, 0.08))
    # crossbones behind the skulls, lying in a plane that faces the camera: knobs poke out above and beside them
    cam = Vector((0.0, -math.cos(math.radians(58)), math.sin(math.radians(58))))
    u = Vector((1.0, 0.0, 0.0))
    v = cam.cross(u).normalized()  # in-plane "up", pointing up and away from the camera
    c = Vector((0.0, 0.14, 0.5))
    for s in (-1, 1):
        a, b = c - u * 0.35 * s - v * 0.25, c + u * 0.35 * s + v * 0.25
        wall.add(bone(a, b, 0.055, 0.075, spread=cam.cross(b - a), double=True))
    glow = C("green", 11, True)  # the Bone Lich's necromantic green
    for x, y, z, r in ((-0.2, -0.13, 0.52, 0.19), (0.2, -0.11, 0.53, 0.19)):
        wall.add(big_skull(Vector((x, y, z)), r, glow))
    m.icon_view = (-18.0, 30.0)
    return m.scale_all(1.05)


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
