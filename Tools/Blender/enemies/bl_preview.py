"""Review renders of the exported enemy/boss FBX files in a mock arena seen through the game camera.

Driven by build_enemies.py. Imports every staged FBX back (round-trip check: part names, hierarchy, identity
transforms), lays them out on the 7x10 grid on the act's staged floor surface and renders, per act, four raw 640x360
data passes (albedo, emission map, sun N.L x shadow, world normal) through the REAL game camera (ArenaConfig pose,
vertical FOV). compose_previews.py re-shades them with game_look.py (ToonLit formula + act lighting + URP post of
the Unity project), so the previews show the in-game brightness, warm/cool grading and bloom.
Scenes: the roster (LAYOUT) per act; CONTEXT per act (other modules' staged props/environment pieces when present);
the Crystal Golem at the boss spawn with its ShieldCrystal turned to each sim Face (Act 3 light).
Blender layout: arena rows run along +Y away from the camera (row 9 = danger row nearest), models keep their
exported facing (-Y = Unity +Z), which here points at the camera just like EnemyView's 180 deg turn in game.
"""
import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import bl_look as look  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))  # project root (Tools/Blender/enemies -> ../../..)
ACT_SURFACES = {}  # filled in main() from the environment layouts
COLS, ROWS, LAUNCH = 7, 10, 1.6
W, H = 640, 360


def _arena_camera():
    """(cameraPosition, cameraPitchDeg, cameraFov) read from the Unity ArenaConfig asset (read-only)."""
    import re
    path = os.path.join(ROOT, "Starter", "Assets", "Configs", "BilliardRogue", "ArenaConfig.asset")
    txt = open(path).read()
    pos = tuple(float(v) for v in re.findall(r"[xyz]: ([-0-9.]+)", re.search(r"cameraPosition: (.*)", txt).group(1)))
    return pos, float(re.search(r"cameraPitchDeg: ([-0-9.]+)", txt).group(1)), \
        float(re.search(r"cameraFov: ([-0-9.]+)", txt).group(1))


CAMERA = _arena_camera()
FOV, PITCH = CAMERA[2], CAMERA[1]
_SURF = json.load(open(os.path.join(ROOT, "Tools", "Staging", "Assets", "Textures", "BilliardRogue", "Surfaces",
                                    "surfaces.json")))["uvMapping"]
UV_OFFSET, SURFACE_TILING = (_SURF["offset"][0], _SURF["offset"][2]), _SURF["tiling"]

# (model, col, row) - col/row of the footprint's top-left cell; bosses are 2x2
LAYOUT = [
    ("Boss_KingSlime", 0, 0), ("Boss_BoneLich", 2.5, 0), ("Boss_CrystalGolem", 5, 0),
    ("Enemy_Slime", 0, 3), ("Enemy_Bat", 1, 3), ("Enemy_Skeleton", 2, 3), ("Enemy_ShieldKnight", 3, 3),
    ("Enemy_Mage", 4, 3), ("Enemy_Healer", 5, 3), ("Enemy_Bomber", 6, 3),
    ("Enemy_Totem", 1, 5), ("Enemy_BoneWall", 3, 5), ("Enemy_BoneWall", 4, 5), ("Enemy_Slime", 5, 5),
    ("Enemy_Skeleton", 2, 7), ("Enemy_Slime", 3, 7), ("Enemy_Bat", 4, 7), ("Enemy_Healer", 0, 6),
    ("Enemy_Bomber", 6, 6), ("Enemy_ShieldKnight", 5, 9), ("Enemy_Mage", 1, 9),
]

# mock-arena colours only: lighting + post come from the Unity act assets via game_look.py (compose step)
ACTS = {
    "act1": dict(name="Act 1 Mossy Ruins", tiles=((0.47, 0.43, 0.33), (0.41, 0.4, 0.3)), danger=(0.55, 0.26, 0.22),
                 launch=(0.33, 0.3, 0.24), wall=(0.5, 0.45, 0.36), ground=(0.2, 0.26, 0.14), grout=(0.16, 0.13, 0.1)),
    "act2": dict(name="Act 2 Sunken Crypt", tiles=((0.26, 0.29, 0.36), (0.22, 0.25, 0.31)), danger=(0.4, 0.17, 0.2),
                 launch=(0.16, 0.17, 0.22), wall=(0.3, 0.32, 0.4), ground=(0.07, 0.08, 0.13), grout=(0.06, 0.06, 0.09)),
    "act3": dict(name="Act 3 Crystal Hollow", tiles=((0.3, 0.26, 0.42), (0.25, 0.22, 0.36)), danger=(0.46, 0.18, 0.3),
                 launch=(0.19, 0.16, 0.27), wall=(0.36, 0.3, 0.5), ground=(0.16, 0.12, 0.24), grout=(0.08, 0.06, 0.12)),
}

# ShieldCrystal turn per sim Face (Unity local Euler Y on the ShieldCrystal transform; valid with EnemyView's 180 deg
# root turn). Sim order Bottom -> Left -> Top -> Right = +90 deg per rotation. Same table as build_enemies.py.
SHIELD_FACE_YAW = (("Bottom", 0), ("Left", 90), ("Top", 180), ("Right", 270))
BOSS_SPAWN = (2.5, 0)  # boss stage: col (7 - 2) / 2, rows 0-1

# context scene (review only): tall enemies directly in front of short ones (occlusion), the bone wall between
# other modules' crate / bone pile, the golem next to the Act 3 crystal dressing. Missing non-enemy FBX are skipped.
CONTEXT = [
    ("Enemy_Slime", 0, 2), ("Enemy_Bat", 1, 2), ("Enemy_Bomber", 2, 2), ("Enemy_Healer", 3, 2), ("Enemy_Slime", 4, 2),
    ("Enemy_Mage", 5, 2), ("Enemy_Bat", 6, 2),
    ("Enemy_Totem", 0, 3), ("Enemy_Mage", 1, 3), ("Enemy_ShieldKnight", 2, 3), ("Enemy_Skeleton", 3, 3),
    ("Enemy_BoneWall", 4, 3), ("Enemy_Totem", 5, 3), ("Enemy_ShieldKnight", 6, 3),
    ("Enemy_Skeleton", 0, 5), ("Enemy_Healer", 1, 5), ("Enemy_BoneWall", 2, 5), ("Prop_Crate", 3, 5),
    ("Env_BonePile", 4, 5), ("Prop_Pillar", 5, 5), ("Enemy_BoneWall", 6, 5),
    ("Enemy_Healer", 0, 7), ("Env_Mushrooms", 1, 7), ("Enemy_Bat", 3, 7), ("Enemy_Mage", 4, 7), ("Enemy_Bomber", 5, 7),
    ("Enemy_Slime", 6, 7),
    ("Boss_CrystalGolem", 0, 8), ("Env_Crystal_A", 3, 8), ("Env_Crystal_B", 4, 8), ("Enemy_Skeleton", 5, 9),
    ("Cat_Hero", 6, 9),
]
OTHER_FOLDERS = {"Env_": "Environment", "Prop_": "Props", "Cat_": "Player"}

REQUIRED = None  # filled from bl_build_model.REQUIRED_PARTS


def import_model(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y", use_custom_normals=True,
                             bake_space_transform=True)
    return [o for o in bpy.data.objects if o not in before]


def check_import(model, objs, report):
    names = sorted(o.name.split(".")[0] for o in objs)
    want = sorted(REQUIRED[model])
    issues = []
    if names != want:
        issues.append(f"parts {names} != {want}")
    for o in objs:
        if any(abs(a) > 1e-4 for a in o.rotation_euler) or any(abs(s - 1) > 1e-4 for s in o.scale):
            issues.append(f"{o.name}: rot {tuple(round(math.degrees(a), 2) for a in o.rotation_euler)} "
                          f"scale {tuple(round(s, 4) for s in o.scale)}")
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
    hierarchy = {o.name.split(".")[0]: (o.parent.name.split(".")[0] if o.parent else None) for o in objs}
    report[model] = {"parts_ok": not issues, "issues": issues, "tris": tris, "hierarchy": hierarchy}


def cell_center(col, row, w=1, h=1):
    x = col + w / 2 - COLS / 2
    y = LAUNCH + ROWS - row - h / 2
    return Vector((x, y, 0.0))


def act_surfaces():
    """{act: (floor surface, ground surface or None)} from the environment layouts (Env_FloorTile Top_Surface and the
    act's ground tiles), so the mock arena uses the same staged surface textures as the game."""
    layouts = json.load(open(os.path.join(ROOT, "Tools", "Blender", "environment", "layouts.json")))
    out = {}
    for act in layouts["acts"]:
        floor = next((s["surface"] for s in act["surfaces"] if s["piece"] == "Env_FloorTile"), None)
        out[f"act{act['id']}"] = (floor, act["ground"].get("surface") or None)
    return out


def uv_plane(name, x0, y0, x1, y1, z, uv_offset=(0.0, 0.0), tiling=0.5):
    """Flat quad with arena-local world-projected UVs, uv = (xz + offset) * tiling (surfaces.json uvMapping)."""
    import bmesh
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
    face = bm.faces.new(vs)
    uv = bm.loops.layers.uv.new()
    for loop in face.loops:
        co = loop.vert.co
        loop[uv].uv = ((co.x + uv_offset[0]) * tiling, (co.y + uv_offset[1]) * tiling)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_arena(act, surfaces_dir):
    """Mock arena: per-cell floor tiles (0.94 m, dark grout between) textured with the act's staged floor surface,
    danger-row tint, launch pad, stone walls, ground plane with the act's ground surface. Returns [(obj, albedo)]."""
    p = ACTS[act]
    floor, ground = ACT_SURFACES.get(act, (None, None))
    tex = lambda name: os.path.join(surfaces_dir, f"{name}_Albedo.png") if name else None  # noqa: E731
    floor_tex = tex(floor) if floor and os.path.exists(tex(floor)) else None
    ground_tex = tex(ground) if ground and os.path.exists(tex(ground)) else None
    objs = []

    def box(name, center, size, albedo):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
        o = bpy.context.active_object
        o.name = name
        o.scale = size
        objs.append((o, albedo))

    for r in range(ROWS):
        for c in range(COLS):
            cc = cell_center(c, r)
            o = uv_plane(f"Arena_Tile_{c}_{r}", cc.x - 0.47, cc.y - 0.47, cc.x + 0.47, cc.y + 0.47, 0.0,
                         UV_OFFSET, SURFACE_TILING)
            objs.append((o, (p["danger"] if r == ROWS - 1 else floor_tex or p["tiles"][(r + c) % 2])))
    objs.append((uv_plane("Arena_Grout", -COLS / 2, LAUNCH, COLS / 2, LAUNCH + ROWS, -0.01), p["grout"]))
    objs.append((uv_plane("Arena_Launch", -COLS / 2, 0.0, COLS / 2, LAUNCH, -0.005, UV_OFFSET, SURFACE_TILING),
                 floor_tex or p["launch"]))
    top = LAUNCH + ROWS
    for name, center, size in (("Arena_WallL", (-COLS / 2 - 0.2, top / 2, 0.2), (0.4, top + 0.4, 0.4)),
                               ("Arena_WallR", (COLS / 2 + 0.2, top / 2, 0.2), (0.4, top + 0.4, 0.4)),
                               ("Arena_WallT", (0.0, top + 0.2, 0.2), (COLS + 0.8, 0.4, 0.4))):
        box(name, center, size, p["wall"])
    objs.append((uv_plane("Arena_Ground", -30.0, -20.0, 30.0, 40.0, -0.2, (0.0, 0.0), SURFACE_TILING),
                 ground_tex or p["ground"]))
    return objs


def game_camera(scene):
    """The real game pose (ArenaConfig.cameraPosition relative to the ArenaLayout origin, rig Euler(pitch, 0, 0),
    vertical FOV). Unity (x, y, z) -> Blender (x, z, y) here, because the preview keeps models unturned."""
    pos, pitch, fov = CAMERA
    loc = Vector((pos[0], pos[2], pos[1]))
    fwd = Vector((0.0, math.cos(math.radians(pitch)), -math.sin(math.radians(pitch))))
    cam = look.look_camera(scene, loc, loc + fwd, lens_fov_deg=fov, name="GameCam")
    centre = cell_center(3, ROWS // 2 - 1)  # a mid-arena cell
    return cam, (centre - loc).length


def px_per_cell(scene, cam):
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    c = cell_center(3, ROWS // 2 - 1)
    a = world_to_camera_view(scene, cam, c - Vector((0.5, 0, 0)))
    b = world_to_camera_view(scene, cam, c + Vector((0.5, 0, 0)))
    return (b.x - a.x) * W


def main():
    global REQUIRED, ACT_SURFACES
    argv = sys.argv[sys.argv.index("--") + 1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--staging", required=True)
    ap.add_argument("--work", required=True)
    ap.add_argument("--palette-dir", required=True)
    ap.add_argument("--models", required=True)
    a = ap.parse_args(argv)
    import contract  # noqa: E402
    REQUIRED = contract.REQUIRED_PARTS
    ACT_SURFACES = act_surfaces()
    os.makedirs(a.work, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    main_png = os.path.join(a.palette_dir, "Palette_Main.png")
    emis_png = os.path.join(a.palette_dir, "Palette_Emission.png")

    report = {}
    prototypes = {}
    for model in a.models.split(","):
        folder = "Bosses" if model.startswith("Boss_") else "Enemies"
        objs = import_model(os.path.join(a.staging, "Models", "BilliardRogue", folder, model + ".fbx"))
        check_import(model, objs, report)
        prototypes[model] = objs
    with open(os.path.join(a.work, "import_report.json"), "w") as f:
        json.dump(report, f, indent=1, sort_keys=True)
    bad = [m for m, r in report.items() if not r["parts_ok"]]
    if bad:
        raise RuntimeError(f"FBX round-trip problems: { {m: report[m]['issues'] for m in bad} }")

    others = {}
    for model, _, _ in CONTEXT:
        prefix = next((p for p in OTHER_FOLDERS if model.startswith(p)), None)
        if prefix and model not in others:
            path = os.path.join(a.staging, "Models", "BilliardRogue", OTHER_FOLDERS[prefix], model + ".fbx")
            if os.path.exists(path):
                others[model] = import_model(path)
    prototypes.update(others)
    for objs in prototypes.values():
        for o in objs:
            o.hide_render = True

    look.setup_eevee(scene, W, H, pixel=True, transparent=False)
    scene.render.dither_intensity = 0.0  # data passes: exact texels / ids, no output dither noise
    cam, dist = game_camera(scene)
    info = {"camera_distance": round(dist, 3), "fov": FOV, "pitch": PITCH, "camera_position": CAMERA[0],
            "px_per_cell": round(px_per_cell(scene, cam), 1), "act_surfaces": ACT_SURFACES,
            "layout": LAYOUT, "context_skipped": sorted({m for m, _, _ in CONTEXT if m not in prototypes})}
    for act, p in ACTS.items():
        info[act] = p["name"]

    placed = instance_layout(scene, prototypes, LAYOUT)
    for act in ACTS:
        render_act(scene, a, act, placed, f"arena_{act}", main_png, emis_png)
    clear(placed)

    placed = instance_layout(scene, prototypes, [(m, c, r) for m, c, r in CONTEXT if m in prototypes])
    for act in ACTS:
        render_act(scene, a, act, placed, f"context_{act}", main_png, emis_png)
    clear(placed)

    # the golem at its real spawn position, shield turned to each sim face, beside the Act 3 crystal dressing
    golem_px = None
    for face, yaw in SHIELD_FACE_YAW:
        lay = [("Boss_CrystalGolem", BOSS_SPAWN[0], BOSS_SPAWN[1])]
        lay += [(mdl, c, r) for mdl, c, r in (("Env_Crystal_A", 1, 1), ("Env_Crystal_B", 5, 0), ("Enemy_Slime", 1, 2),
                                             ("Enemy_Bat", 5, 2)) if mdl in prototypes]
        placed = instance_layout(scene, prototypes, lay, part_yaw={"ShieldCrystal": yaw})
        render_act(scene, a, "act3", placed, f"golem_{face}", main_png, emis_png)
        clear(placed)
        golem_px = footprint_pixels(scene, cam, BOSS_SPAWN[0], BOSS_SPAWN[1], 2, 2.4)
    info["golem_faces"] = [f for f, _ in SHIELD_FACE_YAW]
    info["golem_yaw"] = dict(SHIELD_FACE_YAW)
    info["golem_px"] = golem_px
    with open(os.path.join(a.work, "arena_info.json"), "w") as f:
        json.dump(info, f, indent=1, sort_keys=True)
    print("PREVIEW_OK", json.dumps({"camera_distance": dist}))


def instance_layout(scene, prototypes, layout, part_yaw=None):
    """Duplicate each imported hierarchy under a placement empty. part_yaw {part: Unity local yaw deg} turns a part
    about its pivot (Unity +yaw = Blender -Z rotation, since Unity +Z = Blender -Y and Unity +X = Blender -X)."""
    placed = []
    for i, (model, col, row) in enumerate(layout):
        size = 2 if model.startswith("Boss_") else 1
        root = bpy.data.objects.new(f"Place_{i}_{model}", None)
        scene.collection.objects.link(root)
        root.location = cell_center(col, row, size, size)
        mapping = {}
        for o in prototypes[model]:
            dup = o.copy()
            dup.hide_render = False
            scene.collection.objects.link(dup)
            mapping[o] = dup
        for o, dup in mapping.items():
            dup.parent = mapping[o.parent] if o.parent in mapping else root
            if o.parent not in mapping:
                dup.matrix_parent_inverse.identity()
                dup.location = o.location
            name = o.name.split(".")[0]
            if part_yaw and name in part_yaw:
                dup.rotation_euler = (0.0, 0.0, -math.radians(part_yaw[name]))
        placed.append(root)
        placed.extend(mapping.values())
    return placed


def clear(placed):
    for o in placed:
        bpy.data.objects.remove(o)


PASSES = ("albedo", "emis", "light", "normal", "id")


def render_act(scene, a, act, placed, name, main_png, emis_png):
    """Raw data passes {name}_{albedo,emis,light,normal}.png of the placed models in the act's mock arena, lit by
    the act's sun (direction from the Unity act asset). compose_previews.py shades them with game_look.py."""
    import game_look  # noqa: E402  (numpy-only helpers; Blender bundles numpy)
    for o in list(scene.objects):
        if o.type == "LIGHT" or o.name.startswith("Arena_"):
            data = o.data
            bpy.data.objects.remove(o)
            if data is not None and data.users == 0 and isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
    arena = build_arena(act, os.path.join(a.staging, "Textures", "BilliardRogue", "Surfaces"))
    lit = game_look.act_lighting(int(act[-1]))
    look.sun(scene, [-c for c in game_look.sun_to_light(lit["euler"])], 1.0)
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0, 0, 0, 1)
    meshes = [o for o in placed if o.type == "MESH"]
    cache = {}

    def mat(kind, albedo, emission):
        key = (kind, albedo if kind == "albedo" else None, emission if kind == "emis" else None)
        if key not in cache:
            cache[key] = look.pass_material(f"M_{kind}_{len(cache)}", kind, albedo, emission)
        return cache[key]

    roots = [o for o in placed if o.name.startswith("Place_")]
    for kind in PASSES:
        base = mat("albedo" if kind == "id" else kind, main_png, emis_png)
        for data in {o.data for o in meshes}:  # duplicates share mesh data
            data.materials.clear()
            data.materials.append(base)
        for o in meshes:
            slot = o.material_slots[0]
            slot.link = "DATA"
            if kind == "id":  # placement index + 1 in red (measure_readability.py masks), 0 = arena
                top = o
                while top.parent is not None:
                    top = top.parent
                slot.link = "OBJECT"
                slot.material = mat("albedo", ((roots.index(top) + 1) / 255.0, 0.0, 0.0), None)
        for o, albedo in arena:
            o.data.materials.clear()
            if kind == "id":
                o.data.materials.append(mat("albedo", (0.0, 0.0, 0.0), None))
            else:
                o.data.materials.append(mat(kind, albedo if isinstance(albedo, str) else tuple(albedo), None))
        look.render(scene, os.path.join(a.work, f"{name}_{kind}.png"))


def footprint_pixels(scene, cam, col, row, size, height):
    """Pixel box (x0, y0, x1, y1) of a footprint volume in the 640x360 render (for crops in compose_previews)."""
    from bpy_extras.object_utils import world_to_camera_view
    c = cell_center(col, row, size, size)
    xs, ys = [], []
    for dx in (-size / 2, size / 2):
        for dy in (-size / 2, size / 2):
            for z in (0.0, height):
                v = world_to_camera_view(scene, cam, Vector((c.x + dx, c.y + dy, z)))
                xs.append(v.x * W)
                ys.append((1.0 - v.y) * H)
    return [int(math.floor(min(xs))), int(math.floor(min(ys))), int(math.ceil(max(xs))), int(math.ceil(max(ys)))]


main()
