"""Deterministic low-poly mesh building blocks for the hero / ball / prop models (Blender 5.2, bmesh).

Why not bmesh.ops.create_*: their face order can vary run to run (see research/asset-toolchain.md 4.2), so every
primitive here creates its verts/faces explicitly in a fixed order. Each face is painted with one palette texel
(bl_common.palette_uv) and carries a smooth flag; `Mesh.to_object` marks sharp edges by angle, triangulates n-gons,
canonicalizes and links the object with its origin at the requested pivot.

Coordinates are Blender model space: +X = character's LEFT, -Y = front (exports to Unity +Z), +Z = up.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import bl_common as bc


def C(family, shade, emissive=False):
    """A palette colour key: (family, shade 0..15, emissive half)."""
    return (family, int(shade), bool(emissive))


class FaceInfo:
    __slots__ = ("face", "i", "j", "tag")

    def __init__(self, face, i=0, j=0, tag=""):
        self.face, self.i, self.j, self.tag = face, i, j, tag

    @property
    def c(self):
        return self.face.calc_center_median()

    @property
    def n(self):
        return self.face.normal


def _xf(matrix, deform, co):
    co = Vector(co)
    if deform is not None:
        co = Vector(deform(co))
    return matrix @ co if matrix is not None else co


class Mesh:
    """Accumulates primitives into one bmesh; `to_object` turns it into a Blender object (one model part)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()

    # ------------------------------------------------------------ painting
    def paint(self, face, color):
        family, shade, emissive = color
        uv = bc.palette_uv(family, shade, emissive)
        for loop in face.loops:
            loop[self.uv].uv = uv

    def _finish(self, infos, color, smooth, closed):
        faces = [fi.face for fi in infos]
        if closed and faces:
            bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self.bm.normal_update()
        for fi in infos:
            fi.face.smooth = smooth
            self.paint(fi.face, color(fi) if callable(color) else color)
        return infos

    # ------------------------------------------------------------ primitives
    def loft(self, rings, color, smooth=True, cap0=False, cap1=False, closed_loop=True, closed=True, flip=False,
             wrap=False):
        """Faces between consecutive rings (lists of points; a 1-point ring is a pole). Rings must share a vertex
        count (poles excepted). wrap=True also joins the last ring to the first (closed profile, e.g. a ring solid).
        Returns FaceInfo list with i = band index, j = segment index (caps: tag 'cap0/1')."""
        vrings = [[self.bm.verts.new(Vector(p)) for p in ring] for ring in rings]
        infos = []
        pairs = [(vrings[i], vrings[i + 1]) for i in range(len(vrings) - 1)]
        if wrap:
            pairs.append((vrings[-1], vrings[0]))
        for i, (a, b) in enumerate(pairs):
            n = max(len(a), len(b))
            segs = n if closed_loop else n - 1
            for j in range(segs):
                j1 = (j + 1) % n
                if len(a) == 1:
                    vs = [a[0], b[j1], b[j]]
                elif len(b) == 1:
                    vs = [a[j], a[j1], b[0]]
                else:
                    vs = [a[j], a[j1], b[j1], b[j]]
                if flip:
                    vs.reverse()
                infos.append(FaceInfo(self.bm.faces.new(vs), i, j))
        if cap0 and len(vrings[0]) > 2:
            vs = list(reversed(vrings[0])) if not flip else list(vrings[0])
            infos.append(FaceInfo(self.bm.faces.new(vs), -1, 0, "cap0"))
        if cap1 and len(vrings[-1]) > 2:
            vs = list(vrings[-1]) if not flip else list(reversed(vrings[-1]))
            infos.append(FaceInfo(self.bm.faces.new(vs), len(vrings) - 1, 0, "cap1"))
        return self._finish(infos, color, smooth, closed)

    def lathe(self, profile, segs, color, matrix=None, deform=None, phase=0.0, sx=1.0, sy=1.0, smooth=True,
              cap0=None, cap1=None, closed=True):
        """Revolve (radius, z) points around +Z. r == 0 -> pole. Closed ends are capped unless they are poles."""
        rings = []
        for r, z in profile:
            if r <= 1e-6:
                rings.append([_xf(matrix, deform, (0.0, 0.0, z))])
                continue
            ring = []
            for j in range(segs):
                a = phase + 2.0 * math.pi * j / segs
                ring.append(_xf(matrix, deform, (math.cos(a) * r * sx, math.sin(a) * r * sy, z)))
            rings.append(ring)
        c0 = (profile[0][0] > 1e-6) if cap0 is None else cap0
        c1 = (profile[-1][0] > 1e-6) if cap1 is None else cap1
        return self.loft(rings, color, smooth=smooth, cap0=c0, cap1=c1, closed=closed)

    def sphere(self, center, radii, segs, rings, color, matrix=None, deform=None, phase=0.0, smooth=True):
        """UV sphere with `rings` latitude bands. deform(co) receives unit-sphere coords before scaling."""
        rx, ry, rz = radii
        prof = []
        for k in range(rings + 1):
            t = math.pi * k / rings  # 0 bottom pole .. pi top pole
            prof.append((math.sin(t), -math.cos(t)))

        def d(co):
            if deform is not None:
                co = Vector(deform(Vector(co)))
            return Vector((co.x * rx + center[0], co.y * ry + center[1], co.z * rz + center[2]))

        return self.lathe(prof, segs, color, matrix=matrix, deform=d, phase=phase, smooth=smooth)

    def icosphere(self, center, radius, subdiv, color, smooth=True):
        t = (1.0 + 5 ** 0.5) / 2.0
        verts = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
                 (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
        verts = [Vector(v).normalized() for v in verts]
        tris = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
                (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5),
                (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
        for _ in range(subdiv - 1):
            cache, out = {}, []

            def mid(a, b):
                key = (min(a, b), max(a, b))
                if key not in cache:
                    verts.append(((verts[a] + verts[b]) / 2).normalized())
                    cache[key] = len(verts) - 1
                return cache[key]

            for a, b, c in tris:
                ab, bc_, ca = mid(a, b), mid(b, c), mid(c, a)
                out += [(a, ab, ca), (b, bc_, ab), (c, ca, bc_), (ab, bc_, ca)]
            tris = out
        bv = [self.bm.verts.new(Vector(center) + v * radius) for v in verts]
        infos = [FaceInfo(self.bm.faces.new([bv[a], bv[b], bv[c]]), k) for k, (a, b, c) in enumerate(tris)]
        return self._finish(infos, color, smooth, True)

    def box(self, lo, hi, color, matrix=None, smooth=False):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        ring0 = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)]
        ring1 = [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        rings = [[_xf(matrix, None, p) for p in r] for r in (ring0, ring1)]
        return self.loft(rings, color, smooth=smooth, cap0=True, cap1=True)

    def chamfer_ring(self, w, d, ch, z, matrix=None, cx=0.0, cy=0.0):
        """8-point chamfered rectangle (w x d, corner cut ch) at height z, CCW from +X side."""
        hw, hd = w / 2.0, d / 2.0
        pts = [(hw, -hd + ch), (hw, hd - ch), (hw - ch, hd), (-hw + ch, hd), (-hw, hd - ch), (-hw, -hd + ch),
               (-hw + ch, -hd), (hw - ch, -hd)]
        return [_xf(matrix, None, (cx + x, cy + y, z)) for x, y in pts]

    def block(self, layers, color, matrix=None, smooth=False, cap0=True, cap1=True):
        """Stack of chamfered rectangles: layers = [(w, d, chamfer, z), ...] bottom -> top."""
        rings = [self.chamfer_ring(w, d, ch, z, matrix) for w, d, ch, z in layers]
        return self.loft(rings, color, smooth=smooth, cap0=cap0, cap1=cap1)

    def grid(self, rows, color, smooth=True, closed=False):
        """Quads between consecutive rows of points (open strip, no normal recalculation): the face normal is
        (p[k][c+1] - p[k][c]) x (p[k+1][c+1] - p[k][c+1]), so callers order rows/columns. FaceInfo.i = row, .j = col."""
        vr = [[self.bm.verts.new(Vector(p)) for p in row] for row in rows]
        infos = []
        for k in range(len(vr) - 1):
            for c in range(len(vr[k]) - 1):
                vs = [vr[k][c], vr[k][c + 1], vr[k + 1][c + 1], vr[k + 1][c]]
                infos.append(FaceInfo(self.bm.faces.new(vs), k, c))
        return self._finish(infos, color, smooth, closed), vr

    def tube(self, points, radii, segs, color, cap0=True, cap1=True, smooth=True, sy=1.0, phase=0.0, up=None):
        """Circular tube along a polyline with parallel-transport frames. radii: one per point (0 -> pole)."""
        pts = [Vector(p) for p in points]
        tangents = []
        for k in range(len(pts)):
            a = pts[max(k - 1, 0)]
            b = pts[min(k + 1, len(pts) - 1)]
            tangents.append((b - a).normalized())
        ref = Vector(up) if up is not None else Vector((0.0, 0.0, 1.0))
        if abs(ref.dot(tangents[0])) > 0.95:
            ref = Vector((1.0, 0.0, 0.0))
        nrm = (ref - tangents[0] * ref.dot(tangents[0])).normalized()
        rings = []
        for k, p in enumerate(pts):
            t = tangents[k]
            nrm = (nrm - t * nrm.dot(t)).normalized()
            bin_ = t.cross(nrm)
            r = radii[k]
            if r <= 1e-6:
                rings.append([p.copy()])
                continue
            ring = []
            for j in range(segs):
                a = phase + 2.0 * math.pi * j / segs
                ring.append(p + nrm * (math.cos(a) * r) + bin_ * (math.sin(a) * r * sy))
            rings.append(ring)
        c0 = cap0 and radii[0] > 1e-6
        c1 = cap1 and radii[-1] > 1e-6
        closed = (c0 or radii[0] <= 1e-6) and (c1 or radii[-1] <= 1e-6)
        # open tubes keep their construction winding, which faces outward (bin x t = nrm)
        return self.loft(rings, color, smooth=smooth, cap0=c0, cap1=c1, closed=closed)

    def torus(self, R, r, segs, sides, color, matrix=None, smooth=True, phase=0.0, side_phase=0.0, sz=1.0):
        """Torus around +Z (ring in the XY plane). FaceInfo.i = major segment, .j = minor side."""
        vr = []
        for i in range(segs):
            a = phase + 2 * math.pi * i / segs
            ring = []
            for j in range(sides):
                b = side_phase + 2 * math.pi * j / sides
                rr = R + r * math.cos(b)
                ring.append(self.bm.verts.new(_xf(matrix, None, (rr * math.cos(a), rr * math.sin(a), r * sz * math.sin(b)))))
            vr.append(ring)
        infos = []
        for i in range(segs):
            a, b = vr[i], vr[(i + 1) % segs]
            for j in range(sides):
                j1 = (j + 1) % sides
                infos.append(FaceInfo(self.bm.faces.new([a[j], b[j], b[j1], a[j1]]), i, j))
        return self._finish(infos, color, smooth, True)

    def extrude(self, poly, layers, color, matrix=None, smooth=False, cap0=True, cap1=True):
        """Extrude a 2D polygon (x, z pairs, CCW seen from -Y = front) through `layers` = [(inset, y), ...].
        inset > 0 shrinks the outline (miter offset). FaceInfo.i = layer band, .j = edge index."""
        rings = []
        for inset, y in layers:
            outline = offset_polygon(poly, inset) if inset else poly
            rings.append([_xf(matrix, None, (x, y, z)) for x, z in outline])
        return self.loft(rings, color, smooth=smooth, cap0=cap0, cap1=cap1)

    def transform(self, matrix):
        """Apply a matrix to everything built so far (e.g. tilt a finished pickup towards the camera)."""
        bmesh.ops.transform(self.bm, matrix=matrix, verts=list(self.bm.verts))
        self.bm.normal_update()

    # ------------------------------------------------------------ output
    def to_object(self, name, material, origin=(0.0, 0.0, 0.0), parent=None, sharp_deg=None, collection=None):
        bm = self.bm
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="EAR_CLIP")
        bm.normal_update()
        for e in bm.edges:
            e.smooth = True
            if len(e.link_faces) == 2 and sharp_deg is not None:
                if e.calc_face_angle(0.0) > math.radians(sharp_deg):
                    e.smooth = False
        origin = Vector(origin)
        for v in bm.verts:
            v.co -= origin
        obj = _mesh_object(name, bm, material)
        if collection is not None:
            bpy.context.scene.collection.objects.unlink(obj)
            collection.objects.link(obj)
        if parent is not None:
            obj.parent = parent
            obj.location = origin - parent_world_origin(parent)
        else:
            obj.location = origin
        self.bm = None
        return obj


def parent_world_origin(obj):
    o = Vector(obj.location)
    p = obj.parent
    while p is not None:
        o += Vector(p.location)
        p = p.parent
    return o


def _mesh_object(name, bm, material):
    """Like bl_common.mesh_object but keeps per-face smooth flags + sharp edges set by the caller."""
    bc.canonicalize(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------- 2D helpers
def offset_polygon(poly, d):
    """Miter-offset a CCW polygon inward by d (x, z pairs)."""
    n = len(poly)
    out = []
    for k in range(n):
        p0, p1, p2 = Vector(poly[k - 1]), Vector(poly[k]), Vector(poly[(k + 1) % n])
        e0 = (p1 - p0).normalized()
        e1 = (p2 - p1).normalized()
        n0 = Vector((-e0.y, e0.x))  # inward normal for CCW polygons
        n1 = Vector((-e1.y, e1.x))
        m = (n0 + n1)
        if m.length < 1e-6:
            m = n0
        m.normalize()
        cos_half = max(m.dot(n0), 0.35)
        q = p1 + m * (d / cos_half)
        out.append((q.x, q.y))
    return out


def polygon_area(poly):
    return 0.5 * sum(poly[k - 1][0] * poly[k][1] - poly[k][0] * poly[k - 1][1] for k in range(len(poly)))


def ensure_ccw(poly):
    return poly if polygon_area(poly) > 0 else list(reversed(poly))


def rot(axis, deg):
    return Matrix.Rotation(math.radians(deg), 4, axis)


def tr(x, y, z):
    return Matrix.Translation((x, y, z))


def scl(x, y, z):
    return Matrix.Diagonal((x, y, z, 1.0))
