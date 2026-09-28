"""Render a preview diorama of one act: arena kit + layouts.json dressing, game-like camera, toon shading.
Review tooling (outputs go to the scratchpad); post_diorama.py adds bloom / tilt-shift / grade / pixel upscale.

    Blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/environment/render_diorama.py -- \
        --act 1 --out /tmp/act1 [--layouts L.json] [--models-dir DIR] [--surfaces-dir DIR] [--preview-surfaces DIR]
        [--width 640 --height 360] [--camera overview]

Writes <out>.exr and <out>.npy (linear RGB float32, rows top-down).
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import render_common as rc  # noqa: E402

PAL_DIR = os.path.join(REPO, "Starter", "Assets", "Textures", "BilliardRogue", "Palette")
STAGING = os.path.join(REPO, "Tools", "Staging", "Assets")
POINT_POWER = 55.0      # Blender W per Unity point-light intensity unit (tuned by eye for the preview)
EMISSION_STRENGTH = 3.2


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--act", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--layouts", default=os.path.join(HERE, "layouts.json"))
    p.add_argument("--models-dir", default=os.path.join(STAGING, "Models", "BilliardRogue", "Environment"))
    p.add_argument("--surfaces-dir", default=os.path.join(STAGING, "Textures", "BilliardRogue", "Surfaces"))
    p.add_argument("--preview-surfaces", default="")
    p.add_argument("--width", type=int, default=640)
    p.add_argument("--height", type=int, default=360)
    p.add_argument("--camera", default="game", choices=["game", "overview"])
    p.add_argument("--no-props", action="store_true")
    return p.parse_args(argv)


def main():
    a = args()
    with open(a.layouts) as f:
        doc = json.load(f)
    act = doc["acts"][a.act]
    light = act["lighting"]
    rc.reset()
    rc.setup_render(a.width, a.height)
    amb = [c * light["ambientIntensity"] for c in light["ambient"]]

    pal = rc._img(os.path.join(PAL_DIR, "Palette_Main.png"))
    emi = rc._img(os.path.join(PAL_DIR, "Palette_Emission.png"))
    m_pal = rc.toon_material("M_Palette_Preview", pal, amb, emi, EMISSION_STRENGTH)
    surf_mats, used_dirs = {}, set()

    def surface_mat(name):
        if name not in surf_mats:
            alb, cav, tiling = rc.surface_textures(name, [a.surfaces_dir, a.preview_surfaces])
            used_dirs.add(os.path.dirname(alb.filepath) + f" tiling {tiling:g}")
            surf_mats[name] = rc.surface_material("M_Surface_" + name, alb, amb, cav, tiling)
        return surf_mats[name]

    entries = list(doc["kit"]) + ([] if a.no_props else list(act["props"]))
    g = act["ground"]
    pieces = sorted({e["piece"] for e in entries} | {g["piece"]})
    colls = {}
    for piece in pieces:
        path = os.path.join(a.models_dir, piece + ".fbx")
        coll = rc.import_piece(path, piece)
        colls[piece] = coll
        for o in coll.objects:
            if o.type != "MESH":
                continue
            part = rc.part_name(o)
            if part.endswith("_Surface"):
                key = f"{piece}/{part}"
                sname = g["surface"] if piece == g["piece"] else act["surfaces"].get(key, "StoneFloor")
                mat = surface_mat(sname)
            else:
                mat = m_pal
            o.data.materials.clear()
            o.data.materials.append(mat)

    for e in entries:
        rc.place(colls[e["piece"]], e["x"], e["y"], e["z"], e["rotY"], e["scale"])
    for x, z in g["tiles"]:
        rc.place(colls[g["piece"]], x, g["y"], z)

    sun = light["sun"]
    rc.add_sun(sun["direction"], sun["color"], sun["intensity"])
    for i, pl in enumerate(act.get("pointLights", [])):
        rc.add_point(pl["position"], pl["color"], pl["intensity"] * POINT_POWER, name=f"Point{i}")

    cam = doc["camera"]
    if a.camera == "game":
        rc.add_camera(cam["target"], cam["pitchDeg"], cam["fovDeg"], cam["distance"])
    else:   # wide review shot of the whole dressing
        rc.add_camera([0.0, 0.0, 5.5], 62.0, 48.0, 34.0)
    rc.render_to_npy(a.out + ".exr", a.out + ".npy")
    print("RENDERED", a.out, "surfaces from", sorted(used_dirs))


main()
