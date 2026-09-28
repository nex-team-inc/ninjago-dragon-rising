"""Geometry kit for the Billiard Rogue enemy + boss models (Blender 5.2 headless, imported by the model scripts).

Meshes are assembled in pure Python (`Geo`: vertex list + faces with a palette colour each) so the vertex/face
order is fully deterministic, then converted to bmesh, palette-UV'd (bl_common.paint_faces) and turned into
named rigid parts. Conventions (TDD 14.1 + research/asset-toolchain.md 5.2):
  * Blender -Y is the model front (= Unity +Z after export), +Z up, character's right hand = Blender -X.
  * 1 unit = 1 m = 1 arena cell. Model pivot (root) = bottom centre = world origin.
  * Every part is an object whose origin is its animation pivot; objects carry location only
    (rotation/scale baked into the mesh), parents are other parts.
  * Colour spec = (family, shade 0..15, emissive). Emissive faces map to the palette's right half.
"""
import contextlib
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import tessellate_polygon

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))  # Tools/Blender -> bl_common
import bl_common as bc  # noqa: E402

TAU = math.tau
UP = Vector((0.0, 0.0, 1.0))
FRONT = Vector((0.0, -1.0, 0.0))


# ------------------------------------------------------------------ colours
def C(family, shade, emissive=False):
    return (family, max(0, min(15, int(shade))), bool(emissive))


def resolve(rule, center, normal):
    return rule(center, normal) if callable(rule) else rule


def vgrad(family, lo, hi, z0, z1, emissive=False):
    """Shade by face-centre height: `lo` at z0 .. `hi` at z1 (hand-painted top-light look)."""
    def rule(c, n):
        t = 0.0 if z1 == z0 else max(0.0, min(1.0, (c.z - z0) / (z1 - z0)))
        return C(family, round(lo + (hi - lo) * t), emissive)
    return rule


def facing(family, side, top, bottom=None, emissive=False, up=0.55, down=-0.35):
    """Shade by face normal: top-facing faces `top`, down-facing `bottom`, the rest `side`."""
    bottom = side if bottom is None else bottom

    def rule(c, n):
        return C(family, top if n.z > up else bottom if n.z < down else side, emissive)
    return rule


# ------------------------------------------------------------------ transforms
def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation((x, y, z))


def R(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1.0))


def frame(origin, z_axis, up_hint=UP):
    """4x4 matrix whose local +Z = z_axis and local +Y = up_hint projected (right-handed)."""
    z = Vector(z_axis).normalized()
    up = Vector(up_hint)
    y = up - z * up.dot(z)
    if y.length < 1e-6:
        y = Vector((0.0, 1.0, 0.0)) - z * z.y
    y.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def aim(origin, direction, roll_hint=UP):
    """Matrix mapping local +Z onto `direction` (for pieces built along +Z, e.g. crystals, cones)."""
    return frame(origin, direction, roll_hint)


# ------------------------------------------------------------------ geometry container
class Geo:
    def __init__(self):
        self.v = []
        self.f = []  # [index list, colour spec]

    def vert(self, co):
        self.v.append(Vector(co))
        return len(self.v) - 1

    def face(self, idx, color):
        self.f.append([list(idx), color])

    def add(self, other, m=None, color=None):
        """Merge `other` (optionally transformed; mirrored matrices flip winding). Returns self."""
        base = len(self.v)
        flip = m is not None and m.to_3x3().determinant() < 0
        for co in other.v:
            self.v.append(m @ co if m is not None else co.copy())
        for idx, c in other.f:
            ii = [i + base for i in idx]
            if flip:
                ii.reverse()
            self.f.append([ii, c if color is None else color])
        return self

    def copy(self, m=None):
        return Geo().add(self, m)

    def center_normal(self, idx):
        pts = [self.v[i] for i in idx]
        c = sum(pts, Vector()) / len(pts)
        n = Vector()
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n.x += (a.y - b.y) * (a.z + b.z)
            n.y += (a.z - b.z) * (a.x + b.x)
            n.z += (a.x - b.x) * (a.y + b.y)
        return c, (n.normalized() if n.length > 1e-12 else Vector(UP))

    def paint(self, rule, where=None):
        """Recolour faces (optionally only those for which where(center, normal) is true)."""
        for fc in self.f:
            c, n = self.center_normal(fc[0])
            if where is None or where(c, n):
                fc[1] = resolve(rule, c, n)
        return self

    def deform(self, fn):
        for i, co in enumerate(self.v):
            self.v[i] = Vector(fn(co.copy()))
        return self

    def tris(self):
        return sum(len(i) - 2 for i, _ in self.f)

    def bounds(self):
        xs = [v.x for v in self.v]
        ys = [v.y for v in self.v]
        zs = [v.z for v in self.v]
        return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))

    def bvh(self):
        return BVHTree.FromPolygons([tuple(v) for v in self.v], [i for i, _ in self.f])


def surface(geo, origin, direction):
    """Ray-cast onto `geo`; returns (hit point, face normal)."""
    hit, normal, _, _ = geo.bvh().ray_cast(Vector(origin), Vector(direction).normalized())
    if hit is None:
        raise RuntimeError(f"surface(): ray from {tuple(origin)} dir {tuple(direction)} missed")
    return hit, normal


# ------------------------------------------------------------------ primitives (all closed, outward winding)
def lathe(profile, segs, color, phase=0.0):
    """Surface of revolution around +Z. `profile` = [(r, z), ...] walked from the bottom around the OUTSIDE to
    the top (outward normal = right of the walking direction). r == 0 -> pole; first/last rings with r > 0 get
    flat caps."""
    g = Geo()
    rings = []
    for r, z in profile:
        if r <= 1e-9:
            rings.append([g.vert((0.0, 0.0, z))])
        else:
            rings.append([g.vert((r * math.cos(phase + TAU * j / segs), r * math.sin(phase + TAU * j / segs), z))
                          for j in range(segs)])
    if len(rings[0]) > 1:
        g.face(list(reversed(rings[0])), color)
    for lo, hi in zip(rings, rings[1:]):
        for j in range(segs):
            j1 = (j + 1) % segs
            if len(lo) == 1 and len(hi) == 1:
                continue
            if len(lo) == 1:
                g.face([lo[0], hi[j1], hi[j]], color)
            elif len(hi) == 1:
                g.face([lo[j], lo[j1], hi[0]], color)
            else:
                g.face([lo[j], lo[j1], hi[j1], hi[j]], color)
    if len(rings[-1]) > 1:
        g.face(list(rings[-1]), color)
    return g.paint(color) if callable(color) else g


def sphere(radius, segs, rings, color, phase=0.0):
    prof = [(radius * math.sin(math.pi * i / rings), -radius * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
    return lathe(prof, segs, color, phase)


def dome(rx, ry, h, segs, color, rings=2, phase=0.0):
    """Flat-backed dome (eyes, spots, buttons): base ellipse rx x ry in XY at z=0, apex at z=h."""
    prof = [(1.0, 0.0)]
    for i in range(1, rings):
        a = (math.pi / 2) * i / rings
        prof.append((math.cos(a), math.sin(a)))
    prof.append((0.0, 1.0))
    g = lathe(prof, segs, color, phase)
    return g.copy(S(rx, ry, h)).paint(color) if callable(color) else g.copy(S(rx, ry, h))


def hexa(corners, color):
    """Hexahedron from 8 corners indexed by (ix + 2*iy + 4*iz)."""
    g = Geo()
    ids = [g.vert(c) for c in corners]
    for quad in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        g.face([ids[i] for i in quad], color)
    return g.paint(color) if callable(color) else g


def box(sx, sy, sz, color, top_scale=(1.0, 1.0), top_shift=(0.0, 0.0), base_z=0.0):
    """Box (optionally tapered/sheared) standing on z = base_z, centred on X/Y."""
    corners = []
    for iz in (0, 1):
        for iy in (0, 1):
            for ix in (0, 1):
                kx, ky = (top_scale if iz else (1.0, 1.0))
                dx, dy = (top_shift if iz else (0.0, 0.0))
                corners.append(((ix - 0.5) * sx * kx + dx, (iy - 0.5) * sy * ky + dy, base_z + iz * sz))
    return hexa(corners, color)


def _tessellate(poly):
    """Triangulate a simple 2D polygon (CCW) -> list of CCW index triples."""
    tris = tessellate_polygon([[Vector((x, y, 0.0)) for x, y in poly]])
    out = []
    for a, b, c in tris:
        (ax, ay), (bx, by), (cx, cy) = poly[a], poly[b], poly[c]
        cross = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax)
        out.append((a, b, c) if cross > 0 else (a, c, b))
    out.sort()
    return out


def prism(poly, z0, z1, color, cap_color=None, top_scale=1.0):
    """Extrude a CCW 2D polygon (XY) from z0 to z1. Concave caps are triangulated. top_scale shrinks the top cap."""
    g = Geo()
    n = len(poly)
    bot = [g.vert((x, y, z0)) for x, y in poly]
    top = [g.vert((x * top_scale, y * top_scale, z1)) for x, y in poly]
    cap = color if cap_color is None else cap_color
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    if area < 0:
        raise ValueError("prism(): polygon must be CCW")
    convex = all(
        (poly[(i + 1) % n][0] - poly[i][0]) * (poly[(i + 2) % n][1] - poly[i][1])
        - (poly[(i + 1) % n][1] - poly[i][1]) * (poly[(i + 2) % n][0] - poly[i][0]) >= -1e-12 for i in range(n))
    if convex:
        g.face(list(reversed(bot)), cap)
        g.face(list(top), cap)
    else:
        for a, b, c in _tessellate(poly):
            g.face([bot[a], bot[c], bot[b]], cap)
            g.face([top[a], top[b], top[c]], cap)
    for i in range(n):
        i1 = (i + 1) % n
        g.face([bot[i], bot[i1], top[i1], top[i]], color)
    if callable(color) or callable(cap):
        for fc in g.f:
            c, nrm = g.center_normal(fc[0])
            rule = cap if abs(nrm.z) > 0.99 else color
            fc[1] = resolve(rule, c, nrm)
    return g


def slab(poly, thickness, color, cap_color=None):
    """Prism centred on z = 0 (thickness along Z)."""
    return prism(poly, -thickness / 2, thickness / 2, color, cap_color)


def ngon(n, rx, ry=None, phase=0.0):
    ry = rx if ry is None else ry
    return [(rx * math.cos(phase + TAU * i / n), ry * math.sin(phase + TAU * i / n)) for i in range(n)]


def sweep(points, radii, segs, color, phase=0.0, up_hint=None):
    """Tube along a polyline (rotation-minimising frames). radius 0 at an end -> pointed tip."""
    pts = [Vector(p) for p in points]
    rs = list(radii) if hasattr(radii, "__len__") else [radii] * len(pts)
    tans = []
    for i in range(len(pts)):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        tans.append((b - a).normalized())
    t0 = tans[0]
    hint = Vector(up_hint) if up_hint is not None else (Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((0, 1, 0)))
    nrm = (hint - t0 * hint.dot(t0)).normalized()
    g = Geo()
    rings = []
    prev_t = t0
    for i, (p, r, t) in enumerate(zip(pts, rs, tans)):
        if i > 0:  # parallel transport of the normal
            axis = prev_t.cross(t)
            if axis.length > 1e-9:
                ang = prev_t.angle(t)
                nrm = Matrix.Rotation(ang, 3, axis.normalized()) @ nrm
            nrm = (nrm - t * nrm.dot(t)).normalized()
            prev_t = t
        b = t.cross(nrm)
        if r <= 1e-9:
            rings.append([g.vert(p)])
        else:
            rings.append([g.vert(p + r * (math.cos(phase + TAU * j / segs) * nrm + math.sin(phase + TAU * j / segs) * b))
                          for j in range(segs)])
    if len(rings[0]) > 1:
        g.face(list(reversed(rings[0])), color)
    for lo, hi in zip(rings, rings[1:]):
        for j in range(segs):
            j1 = (j + 1) % segs
            if len(lo) == 1:
                g.face([lo[0], hi[j1], hi[j]], color)
            elif len(hi) == 1:
                g.face([lo[j], lo[j1], hi[0]], color)
            else:
                g.face([lo[j], lo[j1], hi[j1], hi[j]], color)
    if len(rings[-1]) > 1:
        g.face(list(rings[-1]), color)
    return g.paint(color) if callable(color) else g


def cone(r, h, segs, color, r_top=0.0, phase=0.0):
    return lathe([(0.0, 0.0), (r, 0.0), (r_top, h)] if r_top <= 0 else [(r, 0.0), (r_top, h)], segs, color, phase)


def cyl(r, h, segs, color, phase=0.0, r_top=None):
    return lathe([(r, 0.0), (r if r_top is None else r_top, h)], segs, color, phase)


def crystal(r, length, sides, color, tip=0.35, base_taper=0.8, phase=0.0):
    """Faceted crystal along +Z standing on z=0: tapered prism + pyramid tip."""
    return lathe([(r * base_taper, 0.0), (r, length * 0.18), (r * 0.92, length * (1 - tip)), (0.0, length)],
                 sides, color, phase)


def ico(radius, color, subdiv=0):
    """Icosahedron (20 tris) or once-subdivided (80 tris), outward winding."""
    p = (1 + 5 ** 0.5) / 2
    vs = [(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p),
          (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)]
    fs = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
          (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
          (8, 6, 7), (9, 8, 1)]
    verts = [Vector(v).normalized() for v in vs]
    for _ in range(subdiv):
        cache = {}

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                verts.append(((verts[a] + verts[b]) / 2).normalized())
                cache[key] = len(verts) - 1
            return cache[key]
        nf = []
        for a, b, c in fs:
            ab, bc_, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc_, ab), (c, ca, bc_), (ab, bc_, ca)]
        fs = nf
    g = Geo()
    for v in verts:
        g.vert(v * radius)
    for tri in fs:
        a, b, c = (g.v[i] for i in tri)
        n = (b - a).cross(c - a)
        g.face(list(tri) if n.dot(a + b + c) > 0 else [tri[0], tri[2], tri[1]], color)
    return g.paint(color) if callable(color) else g


def jitter(geo, amount, seed, keep_z_below=None):
    """Deterministic per-vertex noise (for rocks); vertices at identical positions move together."""
    import random
    rnd = random.Random(seed)
    offsets = {}
    for i, co in enumerate(geo.v):
        key = (round(co.x, 4), round(co.y, 4), round(co.z, 4))
        if key not in offsets:
            offsets[key] = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * amount
        off = offsets[key]
        if keep_z_below is not None and co.z <= keep_z_below:
            off = Vector((off.x, off.y, 0.0))
        geo.v[i] = co + off
    return geo


# ------------------------------------------------------------------ eyes
# Eye whites use a mid-grey swatch from the palette's EMISSIVE half (~62 % value): lit albedo + this emission
# lifts them to near-white in the night acts, yet the emission alone stays well under the bloom threshold (1.0),
# so cute enemies do not become two full-strength bloom sources competing with the ball and the gameplay tells.
SCLERA = ("gray", 8, True)
GLINT = ("gray", 15, True)  # tiny (sub-pixel to 1 px at game scale): the only full-white emissive on an eye


def eye(center, normal, rx, ry, style="cute", up_hint=UP, sclera=None, pupil=None, glow=None, look=(0.0, -0.12),
        depth=None, segs=8):
    """One eye built in a surface frame (local +Z = outward normal, +Y = up).
    styles: 'cute'  light sclera (SCLERA, dim emissive -> readable in dark acts) + big dark pupil + glint
            'glow'  one emissive dome (colour `glow`) - spooky / magic eyes
            'socket' dark recessed socket + small emissive pupil (colour `glow`)."""
    m = frame(center, normal, up_hint)
    d = depth if depth is not None else min(rx, ry) * 0.55
    g = Geo()
    if style == "cute":
        g.add(dome(rx, ry, d, segs, sclera or C(*SCLERA)), T(0, 0, -d * 0.35))
        pr = 0.64
        g.add(dome(rx * pr, ry * pr * 1.02, d * 0.5, max(6, segs - 2), pupil or C("indigo", 1)),
              T(look[0] * rx, look[1] * ry, d * 0.35))
        g.add(dome(rx * 0.2, ry * 0.2, d * 0.3, 4, C(*GLINT), rings=1),
              T(-rx * 0.26 + look[0] * rx, ry * 0.26 + look[1] * ry, d * 0.72))
    elif style == "glow":
        g.add(dome(rx, ry, d, segs, glow), T(0, 0, -d * 0.3))
    elif style == "socket":
        g.add(dome(rx, ry, d * 0.45, segs, pupil or C("gray", 0)), T(0, 0, -d * 0.2))
        g.add(dome(rx * 0.5, ry * 0.55, d * 0.55, max(5, segs - 1), glow), T(look[0] * rx, look[1] * ry, 0.0))
    else:
        raise ValueError(style)
    return g.copy(m)


def eye_pair(target, height, spread, rx, ry, style="cute", elevation=25.0, x_center=0.0, y_front=-3.0,
             toe_out=0.0, **kw):
    """Two eyes ray-cast onto `target` Geo from the front, at `height`, +-`spread` apart. The eye plane is
    tilted `elevation` degrees up (towards the high game camera). Returns (Geo, midpoint)."""
    g = Geo()
    mids = []
    for side in (-1, 1):
        x = x_center + side * spread
        hit, n = surface(target, (x, y_front, height), (0.0, 1.0, 0.0))
        # blend surface normal with a camera-facing normal so eyes read from the high 3/4 camera
        face_dir = Vector((side * math.sin(math.radians(toe_out)), -math.cos(math.radians(elevation)),
                           math.sin(math.radians(elevation)))).normalized()
        nn = (n.normalized() * 0.35 + face_dir * 0.65).normalized()
        g.add(eye(hit, nn, rx, ry, style, **kw))
        mids.append(hit)
    return g, (mids[0] + mids[1]) / 2


# ------------------------------------------------------------------ parts / model
def _triangulate_nonplanar(bm, eps=1e-4):
    """Split warped quads/n-gons (jittered rocks, tattered hems) into triangles here, so Unity cannot pick a
    different diagonal than the flat normals we export. Tri count is unchanged."""
    warped = []
    for f in bm.faces:
        if len(f.verts) > 3:
            c = f.calc_center_median()
            if max(abs((v.co - c).dot(f.normal)) for v in f.verts) > eps:
                warped.append(f)
    if warped:
        bmesh.ops.triangulate(bm, faces=warped, quad_method="FIXED", ngon_method="EAR_CLIP")
        bm.normal_update()



class Part:
    def __init__(self, name, pivot, parent=None):
        self.name = name
        self.pivot = Vector(pivot)
        self.parent = parent
        self.geo = Geo()

    def add(self, geo, m=None, color=None):
        self.geo.add(geo, m, color)
        return self


class Model:
    def __init__(self, name, budget, footprint=(1, 1)):
        self.name = name
        self.budget = budget
        self.footprint = footprint
        self.parts = []
        self.icon_view = None  # optional (yaw_deg, pitch_deg) override of the default icon camera
        # icon framing by visual mass: icon_crop = fraction of the projected height cut off at the BOTTOM edge
        # (tall, thin models: robe hem / staff foot) so the body fills the 48 px icon like the round enemies do
        self.icon_crop = 0.0
        # footprint overhang whitelist {part name: extra metres allowed past half-cell + 0.02 tolerance}
        self.overhang = {}

    def part(self, name, pivot=(0, 0, 0), parent=None):
        p = Part(name, pivot, parent)
        self.parts.append(p)
        return p

    def scale_all(self, s):
        """Uniformly scale every part (geometry + pivots) about the model origin."""
        m = S(s)
        for p in self.parts:
            p.geo = p.geo.copy(m)
            p.pivot = p.pivot * s
        return self

    def get(self, name):
        return next(p for p in self.parts if p.name == name)

    def tris(self):
        return sum(p.geo.tris() for p in self.parts)

    def build(self, material):
        """Create Blender objects (in part order). Returns {name: obj}."""
        objs = {}
        for p in self.parts:
            bm = bmesh.new()
            uv = bm.loops.layers.uv.verify()
            verts = [bm.verts.new(co - p.pivot) for co in p.geo.v]
            by_color = {}
            for idx, col in p.geo.f:
                if len(set(idx)) < 3:
                    continue
                f = bm.faces.new([verts[i] for i in idx])
                by_color.setdefault(col, []).append(f)
            del uv
            for (fam, shade, emissive), faces in sorted(by_color.items()):
                bc.paint_faces(bm, faces, fam, shade, emissive=emissive)
            for v in [v for v in bm.verts if not v.link_faces]:
                bm.verts.remove(v)
            bm.normal_update()
            _triangulate_nonplanar(bm)
            obj = bc.mesh_object(p.name, bm, material, smooth=False)
            if p.parent:
                par = self.get(p.parent)
                obj.parent = objs[p.parent]
                obj.location = p.pivot - par.pivot
            else:
                obj.location = p.pivot
            objs[p.name] = obj
        bpy.context.view_layer.update()
        return objs

    def stats(self):
        out = []
        for p in self.parts:
            lo, hi = p.geo.bounds()
            path = [p.name]
            q = p
            while q.parent:
                q = self.get(q.parent)
                path.insert(0, q.name)
            out.append({
                "name": p.name,
                "path": "/".join(path),
                "parent": p.parent,
                "tris": p.geo.tris(),
                "pivot_blender": [round(c, 4) for c in p.pivot],
                "pivot_unity": [round(-p.pivot.x, 4), round(p.pivot.z, 4), round(-p.pivot.y, 4)],
                "bounds_min_blender": [round(c, 4) for c in lo],
                "bounds_max_blender": [round(c, 4) for c in hi],
                "emissive_faces": sum(1 for _, c in p.geo.f if c[2]),
            })
        return out


# ------------------------------------------------------------------ FBX export (nested hierarchy fix)
@contextlib.contextmanager
def _nested_bake_space_fix():
    """Blender 5.2's FBX exporter with bake_space_transform=True mis-computes the local transform of objects
    nested 2+ levels deep (it mixes the parent's Blender-local and FBX-local matrices): e.g. Skeleton/ArmR/Weapon
    came out with Lcl Rotation 90 deg and a wrong offset. Correct value for mesh/empty objects: conjugate the
    Blender parent-space matrix by the axis conversion, M_fbx = G @ (P_world^-1 @ C_world) @ G^-1."""
    from io_scene_fbx import fbx_utils
    wrapper = fbx_utils.ObjectWrapper
    original = wrapper.fbx_object_matrix

    def patched(self, scene_data, rest=False, local_space=False, global_space=False):
        if not (self.use_bake_space_transform(scene_data) and self._tag == "OB" and not self.parented_to_armature
                and self.bdata.type in {"MESH", "EMPTY"}):
            return original(self, scene_data, rest=rest, local_space=local_space, global_space=global_space)
        gm = scene_data.settings.global_matrix
        gi = scene_data.settings.global_matrix_inv
        parent = self.parent if self.has_valid_parent(scene_data.objects) else None
        world = self.matrix_global
        if parent is not None and not global_space:
            if parent._tag != "OB" or parent.bdata.type not in {"MESH", "EMPTY"}:
                return original(self, scene_data, rest=rest, local_space=local_space, global_space=global_space)
            world = parent.matrix_global.inverted_safe() @ world
        return gm @ world @ gi

    wrapper.fbx_object_matrix = patched
    try:
        yield
    finally:
        wrapper.fbx_object_matrix = original


def export_fbx(path, objects):
    """bl_common.export_fbx_if_changed with the nested-hierarchy fix; returns (seconds, changed)."""
    bpy.context.view_layer.update()
    with _nested_bake_space_fix():
        return bc.export_fbx_if_changed(path, objects)
