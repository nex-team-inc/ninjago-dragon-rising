"""Modeling helpers for the Billiard Rogue environment kit (Blender 5.2, headless).

Everything here is authored in *Unity world space*: x = right, y = up, z = forward (north, up the arena).
Blender stores the geometry in its own frame (Unity (x, y, z) == Blender (-x, -z, y)); the shared FBX export in
bl_common converts back, so a part authored at Unity z = +1 lands at Unity z = +1 after import.

A piece is a list of `Part`s (one Blender object each, name = the Unity child name). Palette parts point every face
at one texel of Palette_Main.png (right half = emissive); parts whose name ends with `_Surface` get box UVs in
metres (1 UV = 1 m) for the tiling detail materials (M_Surface_*), offset so pieces placed on the 1 m grid tile
seamlessly.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bl_common as bc  # noqa: E402

EMISSIVE_KEYWORDS = ("Emissive", "Gem", "Orb", "Flame", "Crystal", "Fuse", "Eyes")


# ---------------------------------------------------------------- space conversion
def u2b(x, y, z):
    """Unity world point -> Blender point."""
    return Vector((-x, -z, y))


def b2u(v):
    return (-v.x, v.z, -v.y)


def xform(pos=(0.0, 0.0, 0.0), yaw=0.0, lean_f=0.0, lean_r=0.0, scale=(1.0, 1.0, 1.0)):
    """Blender matrix for a Unity-space placement. yaw = Unity rotation about +Y in degrees (clockwise seen from
    above, +Z -> +X); lean_f tips the top toward local +Z, lean_r toward local +X (degrees). scale = Unity (sx, sy, sz)."""
    if not isinstance(scale, (tuple, list)):
        scale = (scale, scale, scale)
    s = Matrix.Diagonal((scale[0], scale[2], scale[1], 1.0))
    r = (Matrix.Rotation(math.radians(-yaw), 4, "Z")
         @ Matrix.Rotation(math.radians(lean_f), 4, "X")
         @ Matrix.Rotation(math.radians(-lean_r), 4, "Y"))
    return Matrix.Translation(u2b(*pos)) @ r @ s


def hash01(*vals):
    """Deterministic pseudo-random in [0, 1) from floats (stable across runs, unlike random with set order)."""
    s = 0.0
    for i, v in enumerate(vals):
        s += v * (12.9898 + 31.7 * i) + (i + 1) * 4.1414
    x = math.sin(s) * 43758.5453
    return x - math.floor(x)


class Rng:
    """Tiny deterministic LCG so pieces never depend on Python's global random state."""

    def __init__(self, seed):
        self.state = (seed * 2654435761 + 12345) & 0xFFFFFFFF

    def next(self):
        self.state = (1103515245 * self.state + 12345) & 0x7FFFFFFF
        return self.state / 0x7FFFFFFF

    def uniform(self, a, b):
        return a + (b - a) * self.next()

    def choice(self, seq):
        return seq[min(int(self.next() * len(seq)), len(seq) - 1)]


# ---------------------------------------------------------------- face helpers
def face_normal_u(f):
    n = f.normal
    return (-n.x, n.z, -n.y)


def face_center_u(f):
    return b2u(f.calc_center_median())


def clamp_shade(s):
    return max(0, min(15, int(round(s))))


class Part:
    """One exported child object. surface=True (name ends with _Surface) -> box UVs instead of palette UVs.
    unit_uv=True -> the primitive writes its own 0..1 UVs (e.g. the light-shaft volume); no palette contract."""

    def __init__(self, name, pivot=(0.0, 0.0, 0.0), unit_uv=False):
        self.name = name
        self.pivot = pivot
        self.surface = name.endswith("_Surface")
        self.unit_uv = unit_uv
        self.emissive_name = any(k in name for k in EMISSIVE_KEYWORDS)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.pid = self.bm.faces.layers.int.new("pid")

    # -------- bookkeeping
    # bmesh reuses freed element slots, so "faces after index n" is wrong once anything was deleted. Instead every
    # face that exists when a primitive starts is tagged 1; the primitive's own faces are the untagged ones.
    def _mark(self):
        for f in self.bm.faces:
            if f[self.pid] == 0:
                f[self.pid] = 1
        return 0

    def _since(self, _n0=0):
        return [f for f in self.bm.faces if f[self.pid] == 0]

    @staticmethod
    def _verts_of(faces):
        seen, out = set(), []
        for f in faces:
            for v in f.verts:
                if v not in seen:
                    seen.add(v)
                    out.append(v)
        return out

    # -------- primitives (all return their faces)
    def box(self, size, m, bevel=0.0, taper=None, skip_bottom=False):
        """Unit cube scaled to Unity size (sx, sy, sz), centred on the matrix origin. taper=(tx, tz) scales the
        top face (1 = straight). bevel = chamfer width in metres (1 segment)."""
        n0 = self._mark()
        sx, sy, sz = size
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        for v in verts:
            x, y, z = -v.co.x * sx, v.co.z * sy, -v.co.y * sz
            if taper is not None and v.co.z > 0:
                x *= taper[0]
                z *= taper[1]
            v.co = Vector((-x, -z, y))
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=1, profile=0.5,
                            affect="EDGES", clamp_overlap=True)
        faces = self._since(n0)
        if skip_bottom:
            self._delete_down_faces(faces)
            faces = self._since(n0)
        bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def lathe(self, profile, segs, m=None, rot_offset=0.0, cap_top=True, cap_bot=True, jitter=0.0, seed=0,
              squash=(1.0, 1.0)):
        """Surface of revolution around local +Y. profile = [(radius, y), ...] bottom to top; radius 0 at an end
        makes a point (cone tip). rot_offset in degrees; jitter = per-vertex radial noise fraction (deterministic);
        squash = (x scale, z scale) of every ring."""
        rng = Rng(seed)
        rows = []
        for r, y in profile:
            if r <= 1e-6:
                rows.append([(0.0, y, 0.0)])
                continue
            ring = []
            for i in range(segs):
                a = math.radians(rot_offset) + 2 * math.pi * i / segs
                k = 1.0 + (rng.uniform(-jitter, jitter) if jitter else 0.0)
                ring.append((math.cos(a) * r * k * squash[0], y, math.sin(a) * r * k * squash[1]))
            rows.append(ring)
        n0 = self._mark()
        prof = [(r, y) for r, y in profile]

        def expect(band, c):   # analytic outward normal of the profile segment (interior on the axis side)
            if band == -1:
                return Vector((0, 0, -1))
            if band == -2:
                return Vector((0, 0, 1))
            (r0, y0), (r1, y1) = prof[band], prof[band + 1]
            radial = Vector((c.x, c.y, 0.0))
            radial = radial.normalized() if radial.length > 1e-9 else Vector((0, 0, 0))
            return radial * (y1 - y0) + Vector((0, 0, -(r1 - r0)))
        self.quads(rows, None, close_ends=True, expect=expect)
        faces = self._since(n0)
        if not cap_top or not cap_bot:
            self.bm.normal_update()
            kill = [f for f in faces if len(f.verts) == segs and len(faces) > segs and (
                (not cap_top and f.normal.z > 0.999) or (not cap_bot and f.normal.z < -0.999))]
            if kill:
                bmesh.ops.delete(self.bm, geom=kill, context="FACES_ONLY")
            faces = [f for f in self._since(n0) if f.is_valid]
        if m is not None:
            bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def cyl(self, segs, r_bot, r_top, h, m=None, cap_top=True, cap_bot=True, rot_offset=0.0, **kw):
        """Cylinder / cone / frustum from y=0 to y=h around local +Y (r_top=0 -> cone)."""
        return self.lathe([(r_bot, 0.0), (r_top, h)], segs, m, rot_offset, cap_top, cap_bot, **kw)

    def ico(self, subdiv, radius, m, jitter=0.0, seed=0, flatten_below=None, squash=None):
        """Faceted blob. jitter = radial noise fraction; flatten_below = clamp local y (before m) to this value."""
        n0 = self._mark()
        geom = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        rng = Rng(seed)
        for v in sorted(geom["verts"], key=lambda v: (round(v.co.z, 4), round(v.co.y, 4), round(v.co.x, 4))):
            k = 1.0 + rng.uniform(-jitter, jitter)
            v.co *= k
            if flatten_below is not None and v.co.z < flatten_below:
                v.co.z = flatten_below + (v.co.z - flatten_below) * 0.15
        faces = self._since(n0)
        bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def poly_prism(self, pts, depth, m, bevel=0.0):
        """Extrude a closed 2D outline given in the local XY plane (Unity x right, y up; CCW seen from +Z... any
        winding, normals are recomputed) along local z from -depth/2 to +depth/2."""
        n0 = self._mark()
        front = [self.bm.verts.new(Vector((-x, -depth / 2, y))) for x, y in pts]   # Unity z = +depth/2
        back = [self.bm.verts.new(Vector((-x, depth / 2, y))) for x, y in pts]
        n = len(pts)
        self.bm.faces.new(front)
        self.bm.faces.new(list(reversed(back)))
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((front[i], back[i], back[j], front[j]))
        faces = self._since(n0)
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        if bevel > 0:
            vs = self._verts_of(faces)
            edges = [e for e in {e for v in vs for e in v.link_edges} if e.calc_face_angle(0) > math.radians(40)]
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES", clamp_overlap=True)
            faces = self._since(n0)
        bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def quads(self, rows, m=None, close_ends=True, expect=None):
        """Loft through rings of Unity-local points (list of rings, each a list of (x, y, z)). A ring with a single
        point makes a fan (pointed tip). By default faces are oriented away from the centroid of their two rings
        and caps away from the neighbouring ring (right for convex lofts). expect(band, center) -> Blender
        direction overrides that per band (band = ring index, -1 bottom cap, -2 top cap)."""
        n0 = self._mark()
        rings = [[self.bm.verts.new(u2b(*p)) for p in ring] for ring in rows]
        cents = [sum((v.co for v in r), Vector()) / len(r) for r in rings]
        todo = []
        for band, (a, b, ca, cb) in enumerate(zip(rings, rings[1:], cents, cents[1:])):
            mid = (ca + cb) / 2
            if len(a) == 1 or len(b) == 1:
                tip, ring = (a[0], b) if len(a) == 1 else (b[0], a)
                k = len(ring)
                for i in range(k):
                    todo.append((self.bm.faces.new((ring[i], ring[(i + 1) % k], tip)), band, mid))
            else:
                k = len(a)
                for i in range(k):
                    j = (i + 1) % k
                    todo.append((self.bm.faces.new((a[i], a[j], b[j], b[i])), band, mid))
        if close_ends:
            for band, ring, cen, other in ((-1, rings[0], cents[0], cents[1]), (-2, rings[-1], cents[-1], cents[-2])):
                if len(ring) >= 3:
                    todo.append((self.bm.faces.new(ring), band, cen - (cen - other) * 10.0))
        self.bm.normal_update()
        flip = []
        for f, band, ref in todo:
            c = f.calc_center_median()
            d = expect(band, c) if expect is not None else None
            if d is None:
                d = c - ref
            if f.normal.dot(d) < 0:
                flip.append(f)
        if flip:
            bmesh.ops.reverse_faces(self.bm, faces=flip)
        faces = self._since(n0)
        if m is not None:
            bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def orient(self, faces, outward):
        """Flip faces whose Unity-space normal points against outward(center) -> (dx, dy, dz) or a fixed tuple."""
        self.bm.normal_update()
        flip = []
        for f in faces:
            n, c = face_normal_u(f), face_center_u(f)
            d = outward(c) if callable(outward) else outward
            if n[0] * d[0] + n[1] * d[1] + n[2] * d[2] < 0:
                flip.append(f)
        if flip:
            bmesh.ops.reverse_faces(self.bm, faces=flip)
            self.bm.normal_update()
        return faces

    def raw(self, verts_u, faces_idx, m=None, outward=None):
        """Explicit geometry in Unity-local coordinates. outward: see orient() (applied before m)."""
        n0 = self._mark()
        vs = [self.bm.verts.new(u2b(*p)) for p in verts_u]
        for idx in faces_idx:
            self.bm.faces.new([vs[i] for i in idx])
        faces = self._since(n0)
        if outward is not None:
            self.orient(faces, outward)
        if m is not None:
            bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def extrude_y(self, pts_xz, y0, y1, m=None, cap_bottom=True):
        """Prism from a closed outline in the XZ plane (Unity), from y0 to y1."""
        rings = [[(x, y0, z) for x, z in pts_xz], [(x, y1, z) for x, z in pts_xz]]
        faces = self.quads(rings, None, close_ends=True)
        if not cap_bottom:
            self.bm.normal_update()
            kill = [f for f in faces if f.normal.z < -0.99]
            bmesh.ops.delete(self.bm, geom=kill, context="FACES_ONLY")
            faces = [f for f in faces if f.is_valid]
        if m is not None:
            bmesh.ops.transform(self.bm, matrix=m, verts=self._verts_of(faces))
        return faces

    def _delete_down_faces(self, faces):
        self.bm.normal_update()
        kill = [f for f in faces if f.normal.z < -0.99]
        bmesh.ops.delete(self.bm, geom=kill, context="FACES_ONLY")

    def delete_faces(self, pred):
        self.bm.normal_update()
        kill = [f for f in self.bm.faces if pred(f)]
        if kill:
            bmesh.ops.delete(self.bm, geom=kill, context="FACES_ONLY")

    # -------- painting
    def paint(self, faces, family, shade, emissive=None, top=1, bottom=-2, jitter=0, grad=None, seed=0.0,
              up_family=None, up_shade=None, up_thresh=0.6):
        """Palette-paint faces with hand-painted facet shading:
        top faces +top, downward faces +bottom, optional per-face jitter (+-jitter shades),
        grad=(y0, y1, delta): add up to `delta` shades from height y0 to y1 (piece space),
        up_family/up_shade: faces whose normal.y > up_thresh use another colour (moss caps, snow, grass tops)."""
        if emissive is None:
            emissive = self.emissive_name
        self.bm.normal_update()
        for f in faces:
            if not f.is_valid:
                continue
            nx, ny, nz = face_normal_u(f)
            cx, cy, cz = face_center_u(f)
            fam, s = family, shade
            if up_family is not None and ny > up_thresh:
                fam, s = up_family, up_shade if up_shade is not None else shade
            if ny > 0.7:
                s += top
            elif ny < -0.5:
                s += bottom
            if grad is not None:
                y0, y1, d = grad
                t = min(1.0, max(0.0, (cy - y0) / max(1e-6, y1 - y0)))
                s += d * t
            if jitter:
                r = hash01(cx, cy, cz, seed)
                s += -jitter if r < 0.3 else (jitter if r > 0.75 else 0)
            bc.paint_faces(self.bm, [f], fam, clamp_shade(s), emissive=emissive)

    def paint_fn(self, faces, fn, emissive=None):
        """fn(face, (nx,ny,nz), (cx,cy,cz)) -> (family, shade)"""
        if emissive is None:
            emissive = self.emissive_name
        self.bm.normal_update()
        for f in faces:
            if f.is_valid:
                fam, s = fn(f, face_normal_u(f), face_center_u(f))
                bc.paint_faces(self.bm, [f], fam, clamp_shade(s), emissive=emissive)

    # -------- surfaces
    def box_uv(self, offset=(0.5, 0.0, 0.5)):
        """World-scale box projection (1 UV = 1 m) in Unity local space. Top faces: (x, z); +X faces: (z, y);
        -X faces: (-z, y); -Z faces (toward the camera): (x, y); +Z faces: (-x, y). offset shifts x/y/z before
        projecting so 1 m grid pieces centred on cell centres start their texture tile at the cell edge."""
        ox, oy, oz = offset
        self.bm.normal_update()
        for f in self.bm.faces:
            nx, ny, nz = face_normal_u(f)
            ax = max((abs(nx), 0), (abs(ny), 1), (abs(nz), 2))[1]
            for loop in f.loops:
                x, y, z = b2u(loop.vert.co)
                x, y, z = x + ox, y + oy, z + oz
                if ax == 1:
                    uv = (x, z) if ny > 0 else (x, -z)
                elif ax == 0:
                    uv = (z, y) if nx > 0 else (-z, y)
                else:
                    uv = (x, y) if nz < 0 else (-x, y)
                loop[self.uv].uv = uv

    # -------- finalize
    def to_object(self, material):
        self.bm.faces.layers.int.remove(self.pid)
        if self.surface:
            self.box_uv()
        px, py, pz = self.pivot
        if (px, py, pz) != (0.0, 0.0, 0.0):
            bmesh.ops.translate(self.bm, vec=-u2b(px, py, pz), verts=list(self.bm.verts))
        self.bm.normal_update()
        obj = bc.mesh_object(self.name, self.bm, material, smooth=False)
        obj.location = u2b(px, py, pz)
        return obj


# ---------------------------------------------------------------- scene / export
def clear_data():
    """Free every object/mesh so the next piece can reuse part names without '.001' suffixes."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def tri_count(objs):
    return bc.tri_count(objs)
