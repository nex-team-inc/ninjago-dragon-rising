"""Cute low-poly slime (~200 tri body + 2 eyes), palette-UV'd, flat shaded -> FBX + icon PNG.

Blender -b --factory-startup --python model_slime.py -- \
    --palette-json palette.json --palette-png palette_16.png --fbx out/Slime.fbx --icon out/Slime.png
"""
import os
import random
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bl_common as bc  # noqa: E402

BODY_FAMILY = "sky"


def build_body_bmesh(vcol):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)
    rnd = random.Random(7)
    for v in bm.verts:
        x, y, z = v.co
        t = (z + 1.0) / 2.0  # 0 bottom .. 1 top
        radial = 1.12 - 0.42 * t * t  # wide bottom, narrow top
        if z < -0.55:  # flatten the underside into a soft disc
            z = -0.55 - (z + 0.55) * 0.12
        if t > 0.97:  # small tip on top
            z += 0.28
        jitter = 1.0 + rnd.uniform(-0.03, 0.03)
        v.co = Vector((x * radial * jitter * 0.5, y * radial * jitter * 0.5, (z + 0.57) * 0.42))
    bm.normal_update()  # face normals are NOT refreshed automatically after moving verts
    vcol_layer = bm.loops.layers.color.new("Col") if vcol else None
    light_dir = Vector((-0.5, -0.6, 0.8)).normalized()
    for f in bm.faces:
        c = f.calc_center_median()
        n = f.normal
        shade = 7 if c.z < 0.08 else 9 if c.z < 0.3 else 11
        if n.dot(light_dir) > 0.7 and c.z > 0.25:
            shade = 14  # baked specular glint
        bc.paint_faces(bm, [f], BODY_FAMILY, shade, vcol_layer)
    return bm


def build_eye_bmesh(vcol):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=1.0)
    vcol_layer = bm.loops.layers.color.new("Col") if vcol else None
    glint = Vector((-0.4, 0.5, 0.75)).normalized()  # eye-local: +Z = surface normal, +Y = up
    best = max(bm.faces, key=lambda f: f.normal.dot(glint))
    for f in bm.faces:
        bc.paint_faces(bm, [f], "gray", 15 if f is best else 0, vcol_layer)
    return bm


def main():
    args = bc.parse_args()
    t0 = time.time()
    bc.reset_scene()
    bc.load_palette(args.palette_json)
    mat, *_ = bc.palette_material(args.palette_png)

    body = bc.mesh_object("Body", build_body_bmesh(args.vcol), mat, smooth=False)

    # place eyes by ray-casting onto the body from the front (Blender front = -Y = Unity +Z)
    bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    eyes = []
    for side, name in ((-1, "EyeR"), (1, "EyeL")):  # character's right is Blender -X
        hit, normal, _, _ = bvh.ray_cast(Vector((side * 0.12, -2.0, 0.31)), Vector((0, 1, 0)))
        eye = bc.mesh_object(name, build_eye_bmesh(args.vcol), mat, smooth=False)
        # Bake orientation + flattening into the mesh so the Unity child has identity rotation/scale:
        # then a blink is simply transform.DOScaleY() around the eye centre (the object origin).
        orient = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
        eye.data.transform(orient @ Matrix.Diagonal((0.06, 0.095, 0.025, 1.0)))
        eye.location = hit + normal * 0.005
        eye.parent = body  # squash the body -> eyes follow
        eye.matrix_parent_inverse = body.matrix_world.inverted()
        eyes.append(eye)
    parts = [body] + eyes

    if args.anim:  # rigid-part test clip: body squash-stretch + eye blink, 24 fps, 1 s
        scene = bpy.context.scene
        scene.render.fps = 24
        scene.frame_start, scene.frame_end = 1, 25
        for f, s in ((1, (1, 1, 1)), (7, (1.12, 1.12, 0.85)), (13, (0.92, 0.92, 1.12)), (25, (1, 1, 1))):
            body.scale = s
            body.keyframe_insert("scale", frame=f)
        for eye in eyes:
            base = eye.scale.copy()
            for f, k in ((1, 1.0), (18, 1.0), (20, 0.1), (22, 1.0)):
                eye.scale = (base.x, base.y, base.z * k)  # Blender Z = Unity Y
                eye.keyframe_insert("scale", frame=f)

    stats = {"tris": bc.tri_count(parts), "body_tris": bc.tri_count([body])}
    if args.fbx:
        seconds, changed = bc.export_fbx_if_changed(args.fbx, parts, bake_anim=args.anim,
                                                    blender_defaults=args.fbx_blender_defaults)
        stats["fbx_s"], stats["fbx_changed"] = round(seconds, 3), changed
        stats["fbx_bytes"] = os.path.getsize(args.fbx)
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
    if args.icon:
        if args.engine == "EEVEE":
            bc.make_toon(mat)
            outline = bc.outline_material()
            bc.add_outline(body, outline, thickness=0.012)
        bc.setup_icon_render(args.engine, args.icon_size, parts, ortho=not args.persp, pixel=args.pixel)
        stats["render_s"] = round(bc.render_png(args.icon), 3)
        stats["icon_bytes"] = os.path.getsize(args.icon)
    stats["total_s"] = round(time.time() - t0, 3)
    print("ASSET_STATS", stats)


main()
