"""Build the Billiard Rogue environment kit FBXs (Blender 5.2 headless).

Regenerate EVERYTHING (FBX kit + layouts.json + preview renders) with one command from the repo root:
    Tools/.venv/bin/python Tools/Blender/environment/build_all.py

This script alone (subset / custom output):
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
        --python Tools/Blender/environment/build_env.py -- [--only Env_Bush,Env_Fence] [--out-dir DIR] [--stats F.json]

Output: <out-dir>/Env_*.fbx (default Tools/Staging/Assets/Models/BilliardRogue/Environment). FBX files are only
rewritten when their content changed (bl_common.export_fbx_if_changed). Prints one `ASSET_STATS {...}` line per
piece and fails (exit 1) when a piece breaks the budget or the palette/emissive/surface UV contract.
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)

import env_lib as el  # noqa: E402
import env_pieces as ep  # noqa: E402

bc = el.bc
DEFAULT_OUT = os.path.join(REPO, "Tools", "Staging", "Assets", "Models", "BilliardRogue", "Environment")


def check_uvs(obj, emissive, unit_uv=False):
    """Palette parts: every UV at a texel centre of the correct half. Surface / unit-UV parts: UVs finite only."""
    if obj.name.endswith("_Surface") or unit_uv:
        bad = [tuple(uv.uv) for uv in obj.data.uv_layers.active.data if not all(map(math.isfinite, uv.uv))]
        return f"{obj.name}: non-finite UVs {bad[:3]}" if bad else None
    w, h = bc._PALETTE["size"]
    half = bc._PALETTE["emissiveOffset"]
    for uv in obj.data.uv_layers.active.data:
        u, v = uv.uv
        col, row = u * w - 0.5, (1.0 - v) * h - 0.5
        if abs(col - round(col)) > 1e-3 or abs(row - round(row)) > 1e-3:
            return f"{obj.name}: UV {tuple(uv.uv)} not at a texel centre"
        if (round(col) >= half) != emissive:
            return f"{obj.name}: UV in the {'emissive' if round(col) >= half else 'base'} half (emissive part: {emissive})"
    return None


def bounds_u(objs):
    pts = []
    for o in objs:
        for v in o.data.vertices:
            pts.append(el.b2u(o.matrix_world @ v.co))
    lo = [round(min(p[i] for p in pts), 3) for i in range(3)]
    hi = [round(max(p[i] for p in pts), 3) for i in range(3)]
    return lo, hi


def main():
    def extra(p):
        p.add_argument("--out-dir", default=DEFAULT_OUT)
        p.add_argument("--only", default="")
        p.add_argument("--stats", help="write per-piece stats JSON here")

    args = bc.parse_args(extra)
    bc.reset_scene()
    bc.load_palette(args.palette_json)
    mat, *_ = bc.palette_material(args.palette_png)
    names = [n for n in ep.PIECES if not args.only or n in set(args.only.split(","))]
    unknown = set(filter(None, args.only.split(","))) - set(ep.PIECES)
    if unknown:
        raise SystemExit(f"unknown pieces: {sorted(unknown)}")
    stats, errors = {}, []
    for name in names:
        t = time.time()
        el.clear_data()
        parts = ep.PIECES[name]()
        objs = [p.to_object(mat) for p in parts]
        import bpy
        bpy.context.view_layer.update()
        tris = el.tri_count(objs)
        for p, o in zip(parts, objs):
            if not o.data.polygons:
                errors.append(f"{name}: part {o.name} is empty")
            err = check_uvs(o, p.emissive_name, p.unit_uv)
            if err:
                errors.append(f"{name}: {err}")
        if tris > ep.TRI_BUDGET:
            errors.append(f"{name}: {tris} tris > budget {ep.TRI_BUDGET}")
        path = os.path.join(args.out_dir, name + ".fbx")
        seconds, changed = bc.export_fbx_if_changed(path, objs)
        lo, hi = bounds_u(objs)
        st = {"name": name, "tris": tris, "parts": {o.name: el.tri_count([o]) for o in objs}, "bounds_min": lo,
              "bounds_max": hi, "fbx_bytes": os.path.getsize(path), "fbx_changed": changed,
              "build_s": round(time.time() - t, 3)}
        stats[name] = st
        print("ASSET_STATS", json.dumps(st))
    if args.stats:
        os.makedirs(os.path.dirname(os.path.abspath(args.stats)), exist_ok=True)
        with open(args.stats, "w") as f:
            json.dump(stats, f, indent=1, sort_keys=True)
    if errors:
        for e in errors:
            print("ERROR", e)
        raise SystemExit(1)


main()
