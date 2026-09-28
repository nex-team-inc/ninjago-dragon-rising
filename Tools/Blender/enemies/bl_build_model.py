"""Build ONE enemy/boss model headless: geometry -> contract checks -> FBX (only replaced if changed) -> raw icon.

Normally driven by build_enemies.py (the one-command entry point). Manual use:
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python Tools/Blender/enemies/bl_build_model.py -- --model Enemy_Slime \
      --fbx Tools/Staging/Assets/Models/BilliardRogue/Enemies/Enemy_Slime.fbx --icon-raw /tmp/Enemy_Slime_raw.png
Prints one line `ENEMY_STATS {json}` (also written to --stats).
"""
import json
import math
import os
import sys
import time

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import bl_common as bc  # noqa: E402
import bl_look as look  # noqa: E402
import contract  # noqa: E402
import ekit  # noqa: E402
import models_bosses  # noqa: E402
import models_enemies  # noqa: E402

REGISTRY = {**models_enemies.REGISTRY, **models_bosses.REGISTRY}

REQUIRED_PARTS = contract.REQUIRED_PARTS

ICON_YAW, ICON_PITCH = -28.0, 30.0          # camera on the model's right-front, looking slightly down
ICON_LIGHT_FROM = (-0.55, -0.75, 0.9)        # upper-left of the viewer
ICON_FILL = 44.0 / 48.0                      # 2 px margin per side for the outline
FOOTPRINT_TOLERANCE = 0.02                   # metres a part may pass the footprint edge (neighbours, ball hits)
SPIN_PARTS = {"ShieldCrystal"}               # parts code turns in 90 deg steps about their (centred) pivot


def extra_args(p):
    p.add_argument("--model", required=True, choices=sorted(REGISTRY))
    p.add_argument("--icon-raw", help="raw (no outline) pixel icon PNG; build_enemies.py adds the outline")
    p.add_argument("--stats", help="write the stats JSON here")
    p.add_argument("--review-dir", help="also render review views <model>_<view>.png into this folder")
    p.add_argument("--lenient", action="store_true", help="dev only: warn instead of failing on the tri budget")


def check_contract(model, objs, lenient=False):
    want = REQUIRED_PARTS[model.name]
    have = [p.name for p in model.parts]
    if sorted(want) != sorted(have):
        raise RuntimeError(f"{model.name}: parts {have} != contract {want}")
    tris = model.tris()
    if tris > model.budget and lenient:
        print(f"WARNING {model.name}: {tris} tris > budget {model.budget}")
    elif tris > model.budget:
        raise RuntimeError(f"{model.name}: {tris} tris > budget {model.budget}")
    for o in objs.values():
        if any(abs(a) > 1e-9 for a in o.rotation_euler) or any(abs(s - 1) > 1e-9 for s in o.scale):
            raise RuntimeError(f"{model.name}/{o.name}: rotation/scale must be baked")
    zmin = min((o.matrix_world @ v.co).z for o in objs.values() for v in o.data.vertices)
    if zmin < -0.02:
        raise RuntimeError(f"{model.name}: geometry below the pivot (z={zmin:.3f})")
    check_footprint(model, objs)


def check_footprint(model, objs):
    """Every part stays inside its footprint (+ tolerance, + per-part whitelist): wings/fists must not poke into
    neighbouring cells or past the hitbox the ball bounces on. Spinning parts are checked for all 4 quarter turns."""
    hx, hy = model.footprint[0] / 2.0, model.footprint[1] / 2.0
    issues = []
    for o in objs.values():
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        if not pts:
            continue
        mx, my = max(abs(p.x) for p in pts), max(abs(p.y) for p in pts)
        if o.name in SPIN_PARTS:
            mx = my = max(mx, my)
        extra = FOOTPRINT_TOLERANCE + model.overhang.get(o.name, 0.0)
        if mx > hx + extra or my > hy + extra:
            issues.append(f"{o.name} |x|={mx:.3f} |z|={my:.3f} > {hx + extra:.2f}/{hy + extra:.2f}")
    if issues:
        raise RuntimeError(f"{model.name}: outside the footprint: " + "; ".join(issues))


def frame_icon(scene, objs, direction, fill, crop):
    """Ortho icon camera. crop > 0 cuts that fraction of the projected height off at the bottom edge (the cut is
    flush with the icon's bottom border); the other three sides keep the outline margin."""
    if crop <= 0:
        return look.frame_ortho(scene, objs, direction, fill=fill)
    bpy.context.view_layer.update()
    d = Vector(direction).normalized()
    rot = d.to_track_quat("-Z", "Y").to_matrix()
    right, up = rot.col[0], rot.col[1]
    pts = [o.matrix_world @ v.co for o in objs if o.type == "MESH" for v in o.data.vertices]
    xs, ys, zs = [p.dot(right) for p in pts], [p.dot(up) for p in pts], [p.dot(d) for p in pts]
    y0 = min(ys) + crop * (max(ys) - min(ys))
    w, h = max(xs) - min(xs), max(ys) - y0
    margin = (1.0 - fill) / 2.0
    scale = max(w / fill, h / (1.0 - margin))
    cx, cy = (min(xs) + max(xs)) / 2.0, y0 + scale / 2.0
    loc = right * cx + up * cy + d * (min(zs) - 5.0)
    return look.look_camera(scene, loc, loc + d, ortho_scale=scale, name="IconCam"), (w, h)


REVIEW_VIEWS = (("game", 0.0, 58.0), ("front", 0.0, 10.0), ("threeq", -40.0, 28.0), ("back", 150.0, 30.0))


def render_review(model, ordered, args):
    """Hi-res AA views with hull outline + a true-scale pixel view (28 px per metre, game pitch)."""
    scene = bpy.context.scene
    for o in list(scene.objects):
        if o.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(o)
    emis_png = os.path.join(os.path.dirname(os.path.abspath(args.palette_png)), "Palette_Emission.png")
    toon = look.toon_material("M_ReviewToon", args.palette_png, emis_png, bands=look.BANDS4,
                              tint=(1.0, 0.97, 0.9), ambient=(0.1, 0.1, 0.12))
    for o in ordered:
        o.data.materials.clear()
        o.data.materials.append(toon)
    look.sun(scene, (0.45, 0.55, -0.75), 1.2)
    size = max(model.footprint)
    look.setup_eevee(scene, 64 * size, 64 * size, pixel=True)
    cam_from = Vector((0.0, -math.cos(math.radians(58)), math.sin(math.radians(58))))
    bpy.context.view_layer.update()
    center = Vector((0, 0, 0.35 * size))
    look.look_camera(scene, center + cam_from * 10, center, ortho_scale=64 * size / 28.0, name="PixCam")
    look.render(scene, os.path.join(args.review_dir, f"{model.name}_pixel.png"))
    look.setup_eevee(scene, 320, 320, pixel=False)
    for view, yaw_d, pitch_d in REVIEW_VIEWS:
        for o in list(scene.objects):
            if o.type == "CAMERA":
                bpy.data.objects.remove(o)
        yaw, pitch = math.radians(yaw_d), math.radians(pitch_d)
        cf = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        look.frame_ortho(scene, ordered, -cf, fill=0.9)
        look.render(scene, os.path.join(args.review_dir, f"{model.name}_{view}.png"))


def main():
    args = bc.parse_args(extra_args)
    t0 = time.time()
    bc.reset_scene()
    bc.load_palette(args.palette_json)
    mat, *_ = bc.palette_material(args.palette_png)
    model = REGISTRY[args.model]()
    objs = model.build(mat)
    check_contract(model, objs, args.lenient)
    ordered = [objs[p.name] for p in model.parts]

    stats = {"model": model.name, "tris": model.tris(), "budget": model.budget, "footprint": model.footprint,
             "parts": model.stats(), "icon_crop": model.icon_crop}
    pts = [o.matrix_world @ v.co for o in ordered for v in o.data.vertices]
    stats["bounds_min_blender"] = [round(min(p[i] for p in pts), 4) for i in range(3)]
    stats["bounds_max_blender"] = [round(max(p[i] for p in pts), 4) for i in range(3)]
    if args.fbx:
        seconds, changed = ekit.export_fbx(args.fbx, ordered)
        stats.update(fbx=os.path.abspath(args.fbx), fbx_changed=changed, fbx_bytes=os.path.getsize(args.fbx))
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))

    if args.icon_raw:
        scene = bpy.context.scene
        emis_png = os.path.join(os.path.dirname(os.path.abspath(args.palette_png)), "Palette_Emission.png")
        toon = look.toon_material("M_IconToon", args.palette_png, emis_png, bands=look.BANDS4,
                                  ambient=(0.1, 0.1, 0.12))  # same 4-band cel ramp as the game
        for o in ordered:
            o.data.materials.clear()
            o.data.materials.append(toon)
        look.setup_eevee(scene, args.icon_size, args.icon_size, pixel=True)
        look.sun(scene, [-c for c in ICON_LIGHT_FROM], 1.15)
        yaw, pitch = (math.radians(a) for a in (model.icon_view or (ICON_YAW, ICON_PITCH)))
        cam_from = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        frame_icon(scene, ordered, -cam_from, ICON_FILL, model.icon_crop)
        look.render(scene, args.icon_raw)
        stats["icon_raw"] = os.path.abspath(args.icon_raw)
    if args.review_dir:
        render_review(model, ordered, args)
    stats["seconds"] = round(time.time() - t0, 2)
    line = json.dumps(stats, sort_keys=True)
    if args.stats:
        os.makedirs(os.path.dirname(os.path.abspath(args.stats)), exist_ok=True)
        with open(args.stats, "w") as f:
            f.write(line)
    print("ENEMY_STATS " + line)


main()
