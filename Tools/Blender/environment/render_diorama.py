"""Render a preview diorama of one act: arena kit + layouts.json dressing, lights, god-ray volumes and a snapshot of
the ambient particles from the game camera, with toon shading. Review tooling (outputs go to the scratchpad);
post_diorama.py adds bloom / tilt-shift / grade / pixel upscale.

    Blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/environment/render_diorama.py -- \
        --act 1 --out /tmp/act1 [--layouts L.json] [--models-dir DIR] [--surfaces-dir DIR] [--preview-surfaces DIR]
        [--width 640 --height 360] [--camera game|requested|overview] [--actors] [--mask] [--no-fx]

--camera game = the ArenaConfig pose recorded in layouts.json "camera"; requested = camera.requested (the pose asked
of the ArenaConfig owner); overview = a wide review shot.
--actors drops the act boss, every enemy type, field objects, a ball and the cat onto the grid (staging models from
the other model areas, when present) to judge arena readability against the scenery.
--mask renders the same actors alone as flat ID colours (R = (index + 1) / 32, G = 1) for measure_previews.py.
Danger-row inlays render at rest (M_DangerTile emission 0: only lit albedo), like the idle game board.
Writes <out>.exr and <out>.npy (linear RGB float32, rows top-down).
"""
import argparse
import json
import math
import os
import re
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
MATERIALS_BUILDER = os.path.join(REPO, "Starter", "Assets", "Scripts", "BilliardRogue", "Editor", "MaterialsBuilder.cs")
FLOOR_BASE_PIECES = ("Env_FloorTile", "Env_FloorTile_Danger", "Env_LaunchPad")
POINT_POWER = 55.0      # Blender W per Unity point-light intensity unit (tuned by eye for the preview)
EMISSION_STRENGTH = 3.2
SHAFT_GAIN = 1.4        # preview emission per LightShaft intensity unit
BOSS = {"1": "Boss_KingSlime", "2": "Boss_BoneLich", "3": "Boss_CrystalGolem"}
# (model, col, row, hover): a representative mid-stage board with every enemy type; row 0 = top row. The act boss
# takes cols 2-3 of rows 0-1 (BOSS_CELL), where the simulation spawns it.
ACTORS = (("Enemies/Enemy_Slime", 0, 5, 0.0), ("Enemies/Enemy_Skeleton", 2, 4, 0.0), ("Enemies/Enemy_Bat", 4, 3, 0.0),
          ("Enemies/Enemy_ShieldKnight", 5, 5, 0.0), ("Enemies/Enemy_Mage", 6, 2, 0.0),
          ("Enemies/Enemy_Bomber", 1, 7, 0.0), ("Enemies/Enemy_Healer", 3, 6, 0.0), ("Enemies/Enemy_Totem", 0, 2, 0.0),
          ("Enemies/Enemy_BoneWall", 5, 1, 0.0), ("Enemies/Enemy_Slime", 4, 9, 0.0), ("Props/Prop_Pillar", 6, 7, 0.0),
          ("Props/Prop_Crate", 2, 8, 0.0), ("Props/Pickup_Heal", 5, 7, 0.25))
BOSS_CELL = (2, 0, 2)
HERO = (("Player/Cat_Hero", (0.6, 0.0, 0.55), 200.0, 1.0), ("Balls/Ball", (-0.9, 0.05, 4.1), 0.0, 0.4))
MASK_STEP = 1.0 / 32.0


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
    p.add_argument("--camera", default="game", choices=["game", "requested", "overview"])
    p.add_argument("--no-props", action="store_true")
    p.add_argument("--no-fx", action="store_true", help="skip light shafts + particles")
    p.add_argument("--actors", action="store_true")
    p.add_argument("--mask", action="store_true", help="actors only, flat ID colours (implies --actors)")
    p.add_argument("--no-unity-floor", action="store_true",
                   help="skip the MaterialsBuilder floor tint / M_ArenaFloorBase mirror (raw surface albedo)")
    p.add_argument("--world-uv", action="store_true",
                   help="sample *_Surface parts with world box mapping (ToonLit _WORLD_UV) instead of the mesh UVs "
                        "x _Tiling Unity uses today")
    return p.parse_args(argv)


def roster(act):
    """[(key, fbx path, unity position, rotY, scale)] of the actors preview, in mask-ID order."""
    out = []
    for m, col, row, hover in ACTORS + ((f"Bosses/{BOSS[act]}", *BOSS_CELL[:2], 0.0),):
        size = BOSS_CELL[2] if m.startswith("Bosses/") else 1
        x, z = cell_centre(col, row, size, size)
        out.append((m.split("/")[1], os.path.join(MODELS, m + ".fbx"), (x, hover, z), 180.0, 1.0))
    for m, pos, rot, s in HERO:
        out.append((m.split("/")[1], os.path.join(MODELS, m + ".fbx"), pos, rot, s))
    return [r for r in out if os.path.exists(r[1])]


def flat_material(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (*rgb, 1.0)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(emit.outputs[0], out.inputs["Surface"])
    return mat


def render_mask(a, doc):
    """Actors alone, one flat colour per actor instance (no lights, no environment)."""
    rc.reset()
    rc.setup_render(a.width, a.height)
    ids = []
    for i, (key, path, pos, rot, s) in enumerate(roster(a.act)):
        coll = rc.import_piece(path, f"{key}_{i}")
        mat = flat_material(f"M_Id{i}", ((i + 1) * MASK_STEP, 1.0, 0.0))
        for o in coll.objects:
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mat)
        rc.place(coll, *pos, rot, s)
        ids.append({"id": i, "model": key, "position": list(pos)})
    add_game_camera(a, doc)
    rc.render_to_npy(a.out + ".exr", a.out + ".npy")
    with open(a.out + ".json", "w") as f:
        json.dump({"act": a.act, "camera": a.camera, "maskStep": MASK_STEP, "actors": ids}, f, indent=1)
    print("RENDERED MASK", a.out, len(ids), "actors")


def add_game_camera(a, doc):
    cam = doc["camera"]
    if a.camera == "overview":   # wide review shot of the whole dressing
        rc.add_camera([0.0, 0.0, 5.5], 62.0, 48.0, 34.0)
        return 62.0
    pose = cam["requested"] if a.camera == "requested" else cam
    rc.add_camera(pose["target"], pose["pitchDeg"], pose["fovDeg"], pose["distance"])
    return pose["pitchDeg"]


def unity_floor_look():
    """Mirror of MaterialsBuilder's arena-floor treatment, so the previews match the game: the floor surfaces get
    _BaseColor = FloorTint and the bevelled Base of the floor pieces takes the flat M_ArenaFloorBase colour.
    Returns (floor surface names, tint, base colour); empty / None parts when the builder does not define them."""
    try:
        src = open(MATERIALS_BUILDER).read()
    except OSError:
        return (), None, None
    num = r"([\d.]+)f"
    names = re.search(r"FloorSurfaces\s*=\s*\{([^}]*)\}", src)
    tint = re.search(rf"FloorTint\s*=\s*new\({num},\s*{num},\s*{num}\)", src)
    base = re.search(rf'"M_ArenaFloorBase".*?"_BaseColor",\s*new Color\({num},\s*{num},\s*{num}\)', src, re.S)
    def lin(m):   # Unity material colours are sRGB values, linearised on upload (linear colour space project)
        return tuple(round(((c + 0.055) / 1.055) ** 2.4 if c > 0.04045 else c / 12.92, 4)
                     for c in (float(v) for v in m.groups())) if m else None
    return tuple(re.findall(r'"(\w+)"', names.group(1))) if names else (), lin(tint), lin(base)


def cell_centre(col, row, w=1, h=1):
    """TDD frame: col 0 at x -3.5..-2.5; row 0 is the top row (z 10.6..11.6)."""
    return col + w / 2 - 3.5, 1.6 + 10 - (row + h) + h / 2


def main():
    a = args()
    rc.SURFACE_UV_MODE = "world" if a.world_uv else "mesh"
    with open(a.layouts) as f:
        doc = json.load(f)
    if a.mask:
        render_mask(a, doc)
        return
    act = next(x for x in doc["acts"] if str(x["id"]) == a.act)
    surfaces = {(m["piece"], m["part"]): (m["surface"], tuple(m.get("tint") or (1.0, 1.0, 1.0)))
                for m in act["surfaces"]}
    light = act["lighting"]
    rc.reset()
    rc.setup_render(a.width, a.height)
    amb = dict(light["ambient"])

    pal = rc._img(os.path.join(PAL_DIR, "Palette_Main.png"))
    emi = rc._img(os.path.join(PAL_DIR, "Palette_Emission.png"))
    m_pal = rc.toon_material("M_Palette_Preview", pal, amb, emi, EMISSION_STRENGTH)
    m_rest = rc.toon_material("M_DangerTile_Rest", pal, amb)     # M_DangerTile at rest: emission 0
    floor_names, floor_tint, floor_base = ((), None, None) if a.no_unity_floor else unity_floor_look()
    m_floor_base = None
    if floor_base is not None:
        white = bpy.data.images.new("White1px", 1, 1)
        white.pixels = [1.0, 1.0, 1.0, 1.0]
        m_floor_base = rc.toon_material("M_ArenaFloorBase_Preview", white, amb, tint=floor_base)
    print("UNITY_FLOOR_LOOK", floor_names, floor_tint, floor_base)
    surf_mats, used_dirs = {}, set()

    def surface_mat(name, tint=(1.0, 1.0, 1.0)):
        if (name, tint) not in surf_mats:
            alb, cav, tiling = rc.surface_textures(name, [a.surfaces_dir, a.preview_surfaces])
            used_dirs.add(os.path.basename(os.path.dirname(alb.filepath)) + f"/{name} tiling {tiling:g}")
            surf_mats[(name, tint)] = rc.surface_material("M_Surface_" + name, alb, amb, cav, tiling, tint)
        return surf_mats[(name, tint)]

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
                sname, tint = (g["surface"], (1.0, 1.0, 1.0)) if key == g["piece"] else \
                    surfaces.get((key, part), ("StoneFloor", (1.0, 1.0, 1.0)))
                if sname in floor_names and floor_tint is not None and tint == (1.0, 1.0, 1.0):
                    tint = floor_tint
                mat = surface_mat(sname, tint)
            elif part == "Base" and key in FLOOR_BASE_PIECES and m_floor_base is not None:
                mat = m_floor_base
            elif part == "DangerInlay_Emissive":
                mat = m_rest
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
        for key, path, pos, rot, s in roster(a.act):
            if key not in colls:
                colls[key] = load(path, key)
            rc.place(colls[key], *pos, rot, s)
            placed.append(key)
        print("ACTORS", placed)

    sun = light["sun"]
    rc.add_sun(sun["direction"], sun["color"], sun["intensity"])
    for i, pl in enumerate(act.get("pointLights", [])):
        rc.add_point(pl["position"], pl["color"], pl["intensity"] * POINT_POWER, name=f"Point{i}")

    pitch = math.radians(add_game_camera(a, doc))

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
