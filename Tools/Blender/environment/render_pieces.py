"""Contact-sheet renders of every environment piece (review tooling, outputs to the scratchpad).

    Blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/environment/render_pieces.py -- \
        --out-dir DIR [--only Env_Bush,...] [--size 320] [--models-dir DIR] [--surfaces-dir DIR --preview-surfaces DIR]

One PNG per piece: game-like 3/4 view from the south (camera pitch 40 deg, slight yaw), warm key light, toon shading,
surfaces with the act-1 materials. Then montage with post_diorama.py --sheet (build_all does this).
"""
import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import render_common as rc  # noqa: E402

PAL_DIR = os.path.join(REPO, "Starter", "Assets", "Textures", "BilliardRogue", "Palette")
STAGING = os.path.join(REPO, "Tools", "Staging", "Assets")
SURF = {"Env_FloorTile": "StoneFloor", "Env_FloorTile_Danger": "StoneFloor", "Env_LaunchPad": "StoneFloor",
        "Env_WallSegment": "MossyBrick", "Env_WallCorner": "MossyBrick", "Env_CryptWall": "CryptBrick",
        "Env_GroundTile": "Grass", "Env_GroundMound": "Grass", "Env_PavingPatch": "MossyBrick"}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out-dir", required=True)
    p.add_argument("--only", default="")
    p.add_argument("--size", type=int, default=320)
    p.add_argument("--models-dir", default=os.path.join(STAGING, "Models", "BilliardRogue", "Environment"))
    p.add_argument("--surfaces-dir", default=os.path.join(STAGING, "Textures", "BilliardRogue", "Surfaces"))
    p.add_argument("--preview-surfaces", default="")
    a = p.parse_args(argv)
    names = sorted(f[:-4] for f in os.listdir(a.models_dir) if f.startswith("Env_") and f.endswith(".fbx"))
    if a.only:
        names = [n for n in names if n in set(a.only.split(","))]
    os.makedirs(a.out_dir, exist_ok=True)
    for name in names:
        rc.reset()
        rc.setup_render(a.size, a.size, samples=1)
        scene = bpy.context.scene
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_depth = "8"
        scene.render.film_transparent = False
        scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.03, 0.028, 0.05, 1)
        amb = (0.2, 0.2, 0.33)
        pal = rc._img(os.path.join(PAL_DIR, "Palette_Main.png"))
        emi = rc._img(os.path.join(PAL_DIR, "Palette_Emission.png"))
        m_pal = rc.toon_material("M_Pal", pal, amb, emi, 3.0)
        coll = rc.import_piece(os.path.join(a.models_dir, name + ".fbx"), name)
        if name == "Env_LightShaft":   # additive volume: show it over a mid-grey backdrop
            scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.12, 0.11, 0.16, 1)
            scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
        for o in coll.objects:
            if o.type != "MESH":
                continue
            part = rc.part_name(o)
            mat = m_pal
            if name == "Env_LightShaft":
                mat = rc.light_shaft_material("M_Shaft", (1.0, 0.82, 0.5), 2.0)
            elif part.endswith("_Surface"):
                sname = SURF.get(name, "StoneFloor")
                alb, cav, tiling = rc.surface_textures(sname, [a.surfaces_dir, a.preview_surfaces])
                mat = rc.surface_material("M_" + sname, alb, amb, cav, tiling)
            o.data.materials.clear()
            o.data.materials.append(mat)
        rc.place(coll, 0, 0, 0, 180, 1)   # pieces face +Z; the camera is south, like rotY 180 props in-game
        bpy.context.view_layer.update()
        pts = []
        for o in coll.objects:
            if o.type == "MESH":
                pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
        lo = Vector((min(q.x for q in pts), min(q.y for q in pts), min(q.z for q in pts)))
        hi = Vector((max(q.x for q in pts), max(q.y for q in pts), max(q.z for q in pts)))
        centre, radius = (lo + hi) / 2, max((hi - lo).length / 2, 0.3)
        cu = (centre.x, centre.z, centre.y)   # Unity centre of the 180-deg-rotated piece
        rc.add_sun((0.5, -0.62, 0.6), (1.0, 0.86, 0.66), 1.1)
        cam = rc.add_camera(cu, 40.0, 30.0, radius * 1.15 / math.sin(math.radians(15.0)), yaw_deg=-20.0)
        scene.render.filepath = os.path.join(os.path.abspath(a.out_dir), name + ".png")
        bpy.ops.render.render(write_still=True)
        print("PIECE", name)


main()
