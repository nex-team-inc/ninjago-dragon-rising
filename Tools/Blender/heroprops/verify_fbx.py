"""Re-import the staged FBX files and check the TDD 14.1 contract: exact part names AND parents (nested hierarchy,
e.g. EarL/EarR under Head), mesh objects only, no actions, one material (M_Palette) per mesh, identity child
rotation/scale, palette UVs at texel centres, emissive faces, tri budgets, and (with --stats, the build's stats.json)
that every part re-imports at the same world-space bounds it was built with. Prints one VERIFY line per model.

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python verify_fbx.py -- <Tools/Staging/Assets> [--stats <preview-dir>/raw/stats.json]
"""
import json
import os
import sys

import bpy
from mathutils import Vector

# model: ({part: parent or None}, tri budget)
EXPECT = {
    "Player/Cat_Hero": ({"Body": None, "Head": "Body", "EarL": "Head", "EarR": "Head", "Tail": "Body",
                         "PawL": "Body", "PawR": "Body", "Cape": "Body"}, 900),
    "Player/Cue_Stick": ({"Stick": None, "Tip": "Stick"}, 300),
    "Balls/Ball": ({"Ball": None}, 100),
    "Props/Prop_Pillar": ({"Pillar": None, "Runes_Emissive": "Pillar"}, 300),
    "Props/Prop_Crate": ({"Crate": None}, 300),
    "Props/Prop_Portal": ({"Ring": None, "Runes_Emissive": "Ring", "Swirl": None}, 300),
    "Props/Prop_Mud": ({"Mud": None}, 300),
    "Props/Pickup_ExtraBall": ({"Coin": None, "Plus_Emissive": "Coin"}, 300),
    "Props/Pickup_Heal": ({"Heart": None, "Cross_Emissive": "Heart"}, 300),
    "Props/Pickup_Power": ({"Sword": None, "PommelGem": "Sword"}, 150),
}
W, H, HALF = 32, 16, 16
TOL = 2e-3


def world_bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return ([min(p[k] for p in pts) for k in range(3)], [max(p[k] for p in pts) for k in range(3)])


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    staging = argv[0]
    stats = {}
    if "--stats" in argv:
        with open(argv[argv.index("--stats") + 1]) as f:
            stats = json.load(f)
    ok_all = True
    for rel, (parts, budget) in EXPECT.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = os.path.join(staging, "Models", "BilliardRogue", rel + ".fbx")
        bpy.ops.import_scene.fbx(filepath=path)
        bpy.context.view_layer.update()
        problems = []
        others = sorted("%s(%s)" % (o.name, o.type) for o in bpy.data.objects if o.type != "MESH")
        if others:
            problems.append("non-mesh objects %s" % others)
        if bpy.data.actions:
            problems.append("actions %s" % sorted(a.name for a in bpy.data.actions))
        mats = sorted(m.name for m in bpy.data.materials)
        if mats != ["M_Palette"]:
            problems.append("materials %s != ['M_Palette']" % mats)
        objs = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
        if sorted(objs) != sorted(parts):
            problems.append("parts %s != %s" % (sorted(objs), sorted(parts)))
        built = stats.get(os.path.basename(rel), {}).get("part_info", {})
        tris, emissive, info = 0, {}, {}
        for name, o in objs.items():
            parent = o.parent.name if o.parent else None
            if name in parts and parent != parts[name]:
                problems.append("%s parent %s != %s" % (name, parent, parts[name]))
            if any(abs(v) > 1e-4 for v in o.rotation_euler) and o.parent is not None:
                problems.append("%s rotated %s" % (name, tuple(round(v, 3) for v in o.rotation_euler)))
            if any(abs(s - 1) > 1e-4 for s in o.scale):
                problems.append("%s scaled %s" % (name, tuple(o.scale)))
            if len(o.data.materials) != 1:
                problems.append("%s has %d material slots" % (name, len(o.data.materials)))
            if name in built:
                lo, hi = world_bounds(o)
                want_lo, want_hi = built[name]["world_min"], built[name]["world_max"]
                err = max(abs(a - b) for a, b in zip(lo + hi, want_lo + want_hi))
                if err > TOL:
                    problems.append("%s world bounds off by %.4f m after re-import" % (name, err))
            me = o.data
            t = sum(len(p.vertices) - 2 for p in me.polygons)
            tris += t
            uv = me.uv_layers.active.data
            bad = 0
            emis = 0
            for p in me.polygons:
                cols = set()
                for li in p.loop_indices:
                    u, v = uv[li].uv
                    x, y = u * W - 0.5, (1 - v) * H - 0.5
                    if abs(x - round(x)) > 1e-3 or abs(y - round(y)) > 1e-3:
                        bad += 1
                    cols.add((round(x), round(y)))
                if len(cols) != 1:
                    bad += 1
                if next(iter(cols))[0] >= HALF:
                    emis += 1
            if bad:
                problems.append("%s: %d loops off texel centre / mixed" % (name, bad))
            emissive[name] = emis
            info[name] = dict(tris=t, parent=parent, loc=[round(c, 3) for c in o.location])
        if tris > budget:
            problems.append("tris %d > %d" % (tris, budget))
        if stats and not built:
            problems.append("no build stats for %s" % rel)
        ok_all &= not problems
        print("VERIFY", rel, json.dumps(dict(ok=not problems, tris=tris, emissive_faces=emissive, parts=info,
                                             problems=problems)))
    print("VERIFY_ALL", "OK" if ok_all else "FAIL")


main()
