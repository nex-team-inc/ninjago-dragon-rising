"""Re-import every staged Env_*.fbx and check the environment contract (TDD §14.1 + the kit brief): required part
names, emissive parts, identity child transforms, no '.001' suffixes, tri budget, palette UVs at texel centres in the
right half, metre-scale box UVs on *_Surface parts, 0..1 UVs on the light-shaft volume, pivot heights.

    Blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/environment/verify_env.py -- \
        [--models-dir DIR] [--report report.json]

Exit code 1 on any contract violation (build_all.py runs this after the export).
"""
import argparse
import json
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
PALETTE = os.path.join(REPO, "Starter", "Assets", "Textures", "BilliardRogue", "Palette", "palette.json")
DEFAULT_DIR = os.path.join(REPO, "Tools", "Staging", "Assets", "Models", "BilliardRogue", "Environment")
BUDGET = 800
EMISSIVE_KEYWORDS = ("Emissive", "Gem", "Orb", "Flame", "Crystal", "Fuse", "Eyes")
# required parts (exact set) for pieces other modules address by name; '*' = emissive part
REQUIRED = {
    "Env_FloorTile": ["Base", "Top_Surface"],
    "Env_FloorTile_Danger": ["Base", "Top_Surface", "DangerFrame", "DangerInlay_Emissive*"],
    "Env_LaunchPad": ["Base", "Top_Surface", "Rail", "Trim"],
    "Env_WallSegment": ["Side_Surface", "Top_Surface", "Trim"],
    "Env_WallCorner": ["Side_Surface", "Trim"],
    "Env_WallTorch": ["Sconce", "Flame*"],
    "Env_WallTorch_Arcane": ["Sconce", "Flame*"],
    "Env_StoneLantern": ["Lantern", "Flame*"],
    "Env_CryptWall": ["Side_Surface", "Trim"],
    "Env_Candles": ["Wax", "Flame*"],
    "Env_Brazier": ["Brazier", "Flame*"],
    "Env_Crystal_A": ["Base", "Crystal*"],
    "Env_Crystal_B": ["Base", "Crystal*"],
    "Env_Crystal_C": ["Base", "Crystal*"],
    "Env_GlowMushroom": ["Stem", "Cap_Emissive*"],
    "Env_RuneStone": ["Stone", "Runes_Emissive*"],
    "Env_IronGate": ["Frame", "Gate"],
    "Env_Banner": ["Pole", "Cloth"],
    "Env_Tree_A": ["Trunk", "Canopy"],
    "Env_Tree_B": ["Trunk", "Canopy"],
    "Env_Coffin": ["Bier", "Coffin", "Lid"],
    "Env_WaterPool": ["Rim", "Water", "Glints_Emissive*"],
    "Env_WaterPool_Glow": ["Rim", "Water_Emissive*"],
    "Env_LightShaft": ["Shaft"],
}
# palette families an emissive part must use (brief: crystals cyan / violet)
FAMILIES = {("Env_Crystal_A", "Crystal"): {"cyan"}, ("Env_Crystal_B", "Crystal"): {"purple", "magenta"},
            ("Env_Crystal_C", "Crystal"): {"cyan", "purple"}, ("Env_FloorTile_Danger", "DangerInlay_Emissive"): {"red"}}
UNIT_UV = {"Shaft"}


def check_piece(path, pal):
    name = os.path.basename(path)[:-4]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    problems = []
    w, h = pal["size"]
    half = pal["emissiveOffset"]
    rows = {v["row"]: k for k, v in pal["families"].items()}
    names = sorted(o.name for o in meshes)
    if any("." in n for n in names):
        problems.append(f"suffixed part names {names}")
    req = REQUIRED.get(name)
    if req is not None and sorted(r.rstrip("*") for r in req) != names:
        problems.append(f"parts {names} != required {sorted(r.rstrip('*') for r in req)}")
    must_emit = {r.rstrip("*") for r in (req or []) if r.endswith("*")}
    tris, lo_y, parts = 0, 1e9, {}
    for o in meshes:
        # the Blender importer puts the file's Y-up -> Z-up conversion on parentless objects (rot X 90 deg); in Unity
        # (bakeAxisConversion off) these parts are children of the file root with identity transforms
        want = (0.0, 0.0, 0.0) if o.parent is not None else (math.pi / 2, 0.0, 0.0)
        if any(abs(v - t) > 1e-4 for v, t in zip(o.rotation_euler, want)) or any(abs(s - 1) > 1e-4 for s in o.scale):
            problems.append(f"{o.name}: non-identity transform rot {tuple(o.rotation_euler)} scale {tuple(o.scale)}")
        me = o.data
        t = sum(len(p.vertices) - 2 for p in me.polygons)
        tris += t
        parts[o.name] = t
        lo_y = min(lo_y, min((o.matrix_world @ v.co).z for v in me.vertices))
        uv = me.uv_layers.active.data if me.uv_layers.active else None
        if uv is None:
            problems.append(f"{o.name}: no UVs")
            continue
        us = [uv[i].uv for p in me.polygons for i in p.loop_indices]
        if o.name.endswith("_Surface"):
            span = max(max(u.x for u in us) - min(u.x for u in us), max(u.y for u in us) - min(u.y for u in us))
            if span < 0.3:
                problems.append(f"{o.name}: surface UV span {span:.2f} m (expected metre-scale box UVs)")
            continue
        if o.name in UNIT_UV:
            if min(min(u.x, u.y) for u in us) < -1e-4 or max(max(u.x, u.y) for u in us) > 1 + 1e-4:
                problems.append(f"{o.name}: UVs outside 0..1")
            continue
        emissive_name = any(k in o.name for k in EMISSIVE_KEYWORDS)
        fams = set()
        for u in us:
            col, row = u.x * w - 0.5, (1 - u.y) * h - 0.5
            if abs(col - round(col)) > 1e-3 or abs(row - round(row)) > 1e-3:
                problems.append(f"{o.name}: UV {tuple(u)} off texel centre")
                break
            if (round(col) >= half) != emissive_name:
                problems.append(f"{o.name}: UV in the wrong palette half (emissive name: {emissive_name})")
                break
            fams.add(rows[int(round(row))])
        if o.name in must_emit and not emissive_name:
            problems.append(f"{o.name}: must be emissive")
        want = FAMILIES.get((name, o.name))
        if want and not fams <= want:
            problems.append(f"{o.name}: families {sorted(fams)} not within {sorted(want)}")
    if tris > BUDGET:
        problems.append(f"{tris} tris > {BUDGET}")
    if lo_y < -0.61:     # Blender Z = Unity Y; only the light shaft (hangs from its top) and sunk walls go below
        if name not in ("Env_LightShaft", "Env_Banner"):   # both hang from their top pivot
            problems.append(f"geometry reaches y {lo_y:.2f} (pivot convention: bottom / top-face centre)")
    return {"piece": name, "tris": tris, "parts": parts, "fbx_bytes": os.path.getsize(path), "problems": problems}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--models-dir", default=DEFAULT_DIR)
    ap.add_argument("--report")
    a = ap.parse_args(argv)
    with open(PALETTE) as f:
        pal = json.load(f)
    files = sorted(f for f in os.listdir(a.models_dir) if f.startswith("Env_") and f.endswith(".fbx"))
    missing = sorted(set(REQUIRED) - {f[:-4] for f in files})
    results = [check_piece(os.path.join(a.models_dir, f), pal) for f in files]
    bad = [r for r in results if r["problems"]]
    for r in results:
        print("VERIFY", r["piece"], r["tris"], "OK" if not r["problems"] else "; ".join(r["problems"]))
    if a.report:
        with open(a.report, "w") as f:
            json.dump({"pieces": results, "missing": missing}, f, indent=1)
    print(f"VERIFY_SUMMARY {len(results)} pieces, {len(bad)} with problems, missing {missing}")
    if bad or missing:
        raise SystemExit(1)


main()
