"""Render a preview diorama of one act: arena kit + layouts.json dressing, lights, god-ray volumes and a snapshot of
the ambient particles from the game camera, with toon shading. Review tooling (outputs go to the scratchpad);
post_diorama.py adds bloom / tilt-shift / grade / pixel upscale.

    Blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/environment/render_diorama.py -- \
        --act 1 --out /tmp/act1 [--layouts L.json] [--models-dir DIR] [--surfaces-dir DIR] [--preview-surfaces DIR]
        [--width 640 --height 360] [--camera game|overview] [--actors] [--no-fx]

--actors drops the act boss, a spread of enemies, field objects, a ball and the cat onto the grid (staging models
from the other model areas, when present) to judge arena readability against the scenery.
Writes <out>.exr and <out>.npy (linear RGB float32, rows top-down).
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
MODELS = os.path.join(STAGING, "Models", "BilliardRogue")
POINT_POWER = 55.0      # Blender W per Unity point-light intensity unit (tuned by eye for the preview)
EMISSION_STRENGTH = 3.2
SHAFT_GAIN = 1.4        # preview emission per LightShaft intensity unit
BOSS = {"1": "Boss_KingSlime", "2": "Boss_BoneLich", "3": "Boss_CrystalGolem"}
# (model, col, row, hover): a representative mid-stage board; row 0 = top row
ACTORS = (("Enemies/Enemy_Slime", 0, 5, 0.0), ("Enemies/Enemy_Skeleton", 2, 4, 0.0), ("Enemies/Enemy_Bat", 4, 3, 0.0),
          ("Enemies/Enemy_ShieldKnight", 5, 5, 0.0), ("Enemies/Enemy_Mage", 6, 2, 0.0),
          ("Enemies/Enemy_Bomber", 1, 7, 0.0), ("Enemies/Enemy_Healer", 3, 6, 0.0), ("Enemies/Enemy_Totem", 0, 2, 0.0),
          ("Enemies/Enemy_Slime", 4, 9, 0.0), ("Props/Prop_Pillar", 6, 7, 0.0), ("Props/Prop_Crate", 2, 8, 0.0),
          ("Props/Pickup_Heal", 5, 7, 0.25))


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--act", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--layouts", default=os.path.join(HERE, "layouts.json"))
    p.add_argument("--models-dir", default=os.path.join(MODELS, "Environment"))
    p.add_argument("--surfaces-dir", default=os.path.join(STAGING, "Textures", "BilliardRogue", "Surfaces"))
    p.add_argument("--preview-surfaces", default="")
    p.add_argument("--width", type=int, default=640)
    p.add_argument("--height", type=int, default=360)
    p.add_argument("--camera", default="game", choices=["game", "overview"])
    p.add_argument("--no-props", action="store_true")
    p.add_argument("--no-fx", action="store_true", help="skip light shafts + particles")
    p.add_argument("--actors", action="store_true")
    return p.parse_args(argv)


def cell_centre(col, row, w=1, h=1):
    """TDD frame: col 0 at x -3.5..-2.5; row 0 is the top row (z 10.6..11.6)."""
    return col + w / 2 - 3.5, 1.6 + 10 - (row + h) + h / 2


def main():
    a = args()
    with open(a.layouts) as f:
        doc = json.load(f)
    act = next(x for x in doc["acts"] if str(x["id"]) == a.act)
    surfaces = {(m["piece"], m["part"]): m["surface"] for m in act["surfaces"]}
    light = act["lighting"]
    rc.reset()
    rc.setup_render(a.width, a.height)
    amb = dict(light["ambient"])

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

    props = [] if a.no_props else list(act["props"])
    entries = list(doc["kit"]) + [e for e in props if e["piece"].startswith("Env_")]
    shafts = [e for e in props if e["piece"] == "LightShaft"] if not a.no_fx else []
    emitters = [e for e in props if e["piece"] == "AmbientParticles"] if not a.no_fx else []
    g = act["ground"]
    pieces = sorted({e["piece"] for e in entries} | {g["piece"]})
    colls = {}

    def load(path, key):
        coll = rc.import_piece(path, key)
        for o in coll.objects:
            if o.type != "MESH":
                continue
            part = rc.part_name(o)
            if part.endswith("_Surface"):
                sname = g["surface"] if key == g["piece"] else surfaces.get((key, part), "StoneFloor")
                mat = surface_mat(sname)
            else:
                mat = m_pal
            o.data.materials.clear()
            o.data.materials.append(mat)
        return coll

    for piece in pieces:
        colls[piece] = load(os.path.join(a.models_dir, piece + ".fbx"), piece)
    for e in entries:
        rc.place(colls[e["piece"]], e["x"], e["y"], e["z"], e["rotY"], e["scale"])
    for t in g["tiles"]:
        rc.place(colls[g["piece"]], t["x"], g["y"], t["z"], t["rotY"])

    if a.actors:
        placed = []
        boss = os.path.join(MODELS, "Bosses", BOSS[a.act] + ".fbx")
        roster = [(os.path.join(MODELS, m + ".fbx"), c, r, 1, hv) for m, c, r, hv in ACTORS]
        roster.append((boss, 2, 0, 2, 0.0))
        for path, col, row, size, hover in roster:
            if not os.path.exists(path):
                continue
            key = os.path.basename(path)[:-4]
            if key not in colls:
                colls[key] = load(path, key)
            x, z = cell_centre(col, row, size, size)
            rc.place(colls[key], x, hover, z, 180.0, 1.0)
            placed.append(key)
        for path, pos, rot, s in ((os.path.join(MODELS, "Player", "Cat_Hero.fbx"), (0.6, 0.0, 0.55), 200.0, 1.0),
                                  (os.path.join(MODELS, "Balls", "Ball.fbx"), (-0.9, 0.05, 4.1), 0.0, 0.4)):
            if os.path.exists(path):
                key = os.path.basename(path)[:-4]
                colls[key] = load(path, key)
                rc.place(colls[key], *pos, rot, s)
                placed.append(key)
        print("ACTORS", placed)

    sun = light["sun"]
    rc.add_sun(sun["direction"], sun["color"], sun["intensity"])
    for i, pl in enumerate(act.get("pointLights", [])):
        rc.add_point(pl["position"], pl["color"], pl["intensity"] * POINT_POWER, name=f"Point{i}")

    cam = doc["camera"]
    if a.camera == "game":
        rc.add_camera(cam["target"], cam["pitchDeg"], cam["fovDeg"], cam["distance"])
        pitch = math.radians(cam["pitchDeg"])
    else:   # wide review shot of the whole dressing
        rc.add_camera([0.0, 0.0, 5.5], 62.0, 48.0, 34.0)
        pitch = math.radians(62.0)

    preset = light["preset"]
    if shafts:
        coll = rc.import_piece(os.path.join(a.models_dir, "Env_LightShaft.fbx"), "Env_LightShaft")
        src = [o for o in coll.objects if o.type == "MESH"][0]
        for i, e in enumerate(shafts):
            col = [c * t for c, t in zip(e["color"], preset["godRayColor"])]
            mat = rc.light_shaft_material(f"M_Shaft{i}", col, e["intensity"] * preset["godRayIntensity"] * SHAFT_GAIN)
            ob = bpy.data.objects.new(f"Shaft{i}", src.data.copy())
            ob.data.materials.clear()
            ob.data.materials.append(mat)
            ob.visible_shadow = False
            rot = rc.unity_euler_matrix(*e["euler"])
            ob.matrix_world = rc.unity_matrix((e["x"], e["y"], e["z"]), rot, e["size"])
            bpy.context.scene.collection.objects.link(ob)
            # sanity: the Euler must carry local -Y onto the stored direction
            d = rot @ Vector((0.0, -1.0, 0.0))
            if (d - Vector(e["direction"])).length > 1e-3:
                raise SystemExit(f"shaft {i}: euler {e['euler']} -> {tuple(d)} != direction {e['direction']}")
    if emitters:
        right_u, up_u = (1.0, 0.0, 0.0), (0.0, math.cos(pitch), math.sin(pitch))
        tint = preset["particleTint"]
        m_glow = rc.glow_material("M_GlowParticle", 2.6)
        m_lit = rc.glow_material("M_LitParticle", 0.85)
        m_mist = rc.glow_material("M_Mist", 0.12, additive=True)
        for i, e in enumerate(emitters):
            em = dict(e)
            em["colors"] = [[c * t for c, t in zip(e[k], tint)] for k in ("colorA", "colorB")]
            ob = rc.particle_billboards(f"FX{i}_{e['type']}", em, right_u, up_u, seed=1000 + i)
            mat = m_mist if e["type"] == "Smoke" else (m_glow if "Glow" in e["shader"] else m_lit)
            ob.data.materials.append(mat)
    rc.render_to_npy(a.out + ".exr", a.out + ".npy")
    print("RENDERED", a.out, "surfaces from", sorted(used_dirs), f"shafts {len(shafts)} emitters {len(emitters)}")


main()
