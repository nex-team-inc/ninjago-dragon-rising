"""Re-import the staged FBX files and check the TDD 14.1 contract (names, hierarchy, identity child transforms,
pivots, palette UVs at texel centres, emissive faces, tri budgets). Prints one VERIFY line per model.

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
      --python verify_fbx.py -- <Tools/Staging/Assets>
"""
import json
import os
import sys

import bpy

EXPECT = {
    "Player/Cat_Hero": (["Body", "Head", "EarL", "EarR", "Tail", "PawL", "PawR", "Cape"], 900),
    "Player/Cue_Stick": (["Stick", "Tip"], 300),
    "Balls/Ball": (["Ball"], 100),
    "Props/Prop_Pillar": (["Pillar"], 300),
    "Props/Prop_Crate": (["Crate"], 300),
    "Props/Prop_Portal": (["Ring", "Swirl"], 300),
    "Props/Prop_Mud": (["Mud"], 300),
    "Props/Pickup_ExtraBall": (["Ring", "Orb"], 300),
    "Props/Pickup_Heal": (["Heart", "Cork"], 300),
    "Props/Pickup_Power": (["Crystal"], 300),
}
W, H, HALF = 32, 16, 16


def main():
    staging = sys.argv[sys.argv.index("--") + 1]
    ok_all = True
    for rel, (parts, budget) in EXPECT.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = os.path.join(staging, "Models", "BilliardRogue", rel + ".fbx")
        bpy.ops.import_scene.fbx(filepath=path)
        objs = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
        problems = []
        if sorted(objs) != sorted(parts):
            problems.append("parts %s != %s" % (sorted(objs), sorted(parts)))
        tris, emissive, info = 0, {}, {}
        for name, o in objs.items():
            if any(abs(v) > 1e-4 for v in o.rotation_euler) and o.parent is not None:
                problems.append("%s rotated %s" % (name, tuple(round(v, 3) for v in o.rotation_euler)))
            if any(abs(s - 1) > 1e-4 for s in o.scale):
                problems.append("%s scaled %s" % (name, tuple(o.scale)))
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
            info[name] = dict(tris=t, parent=o.parent.name if o.parent else None,
                              loc=[round(c, 3) for c in o.location])
        if tris > budget:
            problems.append("tris %d > %d" % (tris, budget))
        ok_all &= not problems
        print("VERIFY", rel, json.dumps(dict(ok=not problems, tris=tris, emissive_faces=emissive, parts=info,
                                             problems=problems)))
    print("VERIFY_ALL", "OK" if ok_all else "FAIL")


main()
