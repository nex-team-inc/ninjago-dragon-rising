"""Review renders for the hero/props set (Blender side). Imported by heroprops_models.py after the FBX export.

Outputs into --preview-dir (raw passes; build_heroprops.py composites bloom / tilt-shift / contact sheet):
  model_<Name>_<view>.png + _emis.png   studio/game-angle beauty shots with an inverted-hull outline
  game_act<N>.png + _emis.png           640x360 pixel-mode mock of the real gameplay camera (FOV 28, pitch 58)
  portrait_p<N>_raw.png                 128x128 pixel-mode bust (post-processed into the staged UI sprite)
"""
import glob
import json
import math
import os

import bpy
from mathutils import Vector

import hp_render as hr
from hp_mesh import C, Mesh, rot, tr

GAME_FOV = 28.0
GAME_PITCH = 58.0
GAME_DIST = 23.5  # ~28 px per cell at the arena centre on a 640x360 target
COLS, ROWS, LAUNCH = 7, 10, 1.6


def cell(col, row, z=0.0):
    """Sim cell -> Blender position. Unity X = sim x, Unity Z = sim y; Blender (x, y) = (-(ux - 3.5), -uz)."""
    ux = col + 0.5
    uz = LAUNCH + (ROWS - 1 - row) + 0.5
    return Vector((-(ux - COLS / 2.0), -uz, z))


def sim_to_blender(ux, uz, z=0.0):
    return Vector((-(ux - COLS / 2.0), -uz, z))


def set_visible(cols_on, all_cols):
    for c in all_cols:
        c.hide_render = c not in cols_on


# ------------------------------------------------------------------------------------------ arena mock
FLOORS = {
    "act1": dict(tile=("skin", 7, 8), danger=("red", 7, 8), launch=("brown", 6), wall=("gray", 7), moss=("lime", 6)),
    "act2": dict(tile=("indigo", 4, 5), danger=("red", 5, 6), launch=("indigo", 3), wall=("gray", 4), moss=None),
    "act3": dict(tile=("purple", 5, 6), danger=("red", 6, 7), launch=("indigo", 4), wall=("indigo", 6),
                 moss=("cyan", 8)),
}


def build_arena(mat, act, col):
    f = FLOORS[act]
    m = Mesh()
    for row in range(ROWS):
        for c in range(COLS):
            p = cell(c, row)
            fam, a, b = f["danger"] if row == ROWS - 1 else f["tile"]
            shade = a if (row + c) % 2 == 0 else b
            if f["moss"] and (row * 3 + c * 5) % 11 == 0 and row != ROWS - 1:
                fam, shade = f["moss"][0], f["moss"][1]
            m.block([(0.95, 0.95, 0.05, -0.12), (0.95, 0.95, 0.05, -0.02), (0.9, 0.9, 0.04, 0.0)],
                    C(fam, shade), matrix=tr(p.x, p.y, 0.0), cap0=False)
    # launch zone slab
    m.box((-COLS / 2.0, -LAUNCH, -0.12), (COLS / 2.0, 0.6, -0.005), C(f["launch"][0], f["launch"][1]))
    wall = C(f["wall"][0], f["wall"][1])
    top = LAUNCH + ROWS
    m.box((COLS / 2.0, -top - 0.35, -0.12), (COLS / 2.0 + 0.35, 0.6, 0.5), wall)  # left wall (screen left)
    m.box((-COLS / 2.0 - 0.35, -top - 0.35, -0.12), (-COLS / 2.0, 0.6, 0.5), wall)
    m.box((-COLS / 2.0 - 0.35, -top - 0.35, -0.12), (COLS / 2.0 + 0.35, -top, 0.5), wall)
    # surrounding ground so the frame is not empty
    m.box((-9.0, -top - 6.0, -0.2), (9.0, 4.0, -0.12), C(f["launch"][0], max(f["launch"][1] - 2, 1)))
    obj = m.to_object("Arena_" + act, mat, origin=(0, 0, 0))
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)
    return obj


def place(model, col, pos, yaw_deg=0.0, scale=1.0, material=None):
    """Clone every top-level part of a built model (root, parts) under one placement empty."""
    root, parts = model
    holder = bpy.data.objects.new(root.name + ".place", None)
    col.objects.link(holder)
    holder.location = pos
    holder.rotation_euler = (0.0, 0.0, math.radians(yaw_deg))
    holder.scale = (scale, scale, scale)
    for p in parts:
        if p.parent is None:
            _clone(p, material, holder, col)
    return holder


def _clone(root, material, parent, col):
    ob = bpy.data.objects.new(root.name + ".m", root.data)
    col.objects.link(ob)
    ob.location = root.location.copy()
    ob.parent = parent
    if material is not None and ob.type == "MESH":
        ob.material_slots[0].link = "OBJECT"
        ob.material_slots[0].material = material
    for ch in root.children:
        _clone(ch, material, ob, col)
    return ob


def import_context_enemies(staging, mat, col):
    """Optional: other agents' staged enemy FBX for scale/contrast context (read-only, skipped if missing)."""
    found = {}
    for path in sorted(glob.glob(os.path.join(staging, "Models", "BilliardRogue", "Enemies", "Enemy_*.fbx"))):
        name = os.path.basename(path)[:-4]
        before = set(bpy.data.objects)
        try:
            bpy.ops.import_scene.fbx(filepath=path)
        except Exception as exc:  # partially written by another agent, etc.
            print("context import failed", path, exc)
            continue
        new = [o for o in bpy.data.objects if o not in before]
        holder = bpy.data.objects.new(name + ".ctx", None)
        col.objects.link(holder)
        for o in new:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            col.objects.link(o)
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mat)
            if o.parent is None:
                o.parent = holder
        found[name] = holder
    return found


def ball_material(name, tint, glow, emission_png, albedo_png):
    m = hr.toon_material(name, albedo_png, emission_png, tint=tint)
    # scale the emission sample by the glow colour (ball glow): patch the SCALE node into a colour multiply
    nt = m.node_tree
    emul = next(n for n in nt.nodes if n.type == "VECT_MATH")
    gm = nt.nodes.new("ShaderNodeMix")
    gm.data_type = "RGBA"
    gm.blend_type = "MULTIPLY"
    gm.inputs["Factor"].default_value = 1.0
    gm.inputs["B"].default_value = tuple(glow) + (1.0,)
    links = [l for l in nt.links if l.from_node == emul]
    targets = [(l.to_node, l.to_socket) for l in links]
    for l in links:
        nt.links.remove(l)
    nt.links.new(emul.outputs["Vector"], gm.inputs["A"])
    for node, sock in targets:
        nt.links.new(gm.outputs["Result"], sock)
    return m


def build_mock(args, built, mat, p2mat, emission_png, col):
    get = lambda n: (built[n][0], built[n][1]) if n in built else None  # noqa: E731
    # props
    layout = [("Prop_Pillar", 1, 5, 0), ("Prop_Pillar", 5, 1, 0), ("Prop_Crate", 5, 6, 8), ("Prop_Crate", 2, 3, -5),
              ("Prop_Portal", 0, 2, 0), ("Prop_Portal", 6, 7, 0), ("Prop_Mud", 3, 7, 0), ("Pickup_ExtraBall", 2, 8, 0),
              ("Pickup_Heal", 4, 4, 0), ("Pickup_Power", 5, 3, 0)]
    for name, c, r, yaw in layout:
        if get(name) is not None:
            place(get(name), col, cell(c, r), yaw)
    # context enemies (if another agent already staged them)
    ctx = import_context_enemies(args.context_staging or args.staging, mat, col)
    spots = [(0, 0), (1, 0), (3, 0), (4, 1), (6, 1), (2, 1), (0, 4), (6, 4), (3, 2), (1, 3), (4, 0), (5, 0)]
    for (name, holder), (c, r) in zip(sorted(ctx.items()), spots):
        holder.location = cell(c, r)
        holder.rotation_euler = (0, 0, math.pi)  # EnemyView turns enemies 180 deg to face the camera
    # cats: P1 striking (faces +Z / north = model default), P2 idle turned 3/4 to the camera
    if get("Cat_Hero") is not None:
        place(get("Cat_Hero"), col, sim_to_blender(2.6, 0.55), 0.0)
        place(get("Cat_Hero"), col, sim_to_blender(5.3, 0.45), 145.0, material=p2mat)
    if get("Cue_Stick") is not None:
        # cue held behind the ball, pointing up-arena along the aim (roughly what CatView will do)
        holder = place(get("Cue_Stick"), col, sim_to_blender(2.95, 0.02, 0.33), -18.0)
        holder.rotation_euler = (math.radians(-6), 0.0, math.radians(-18.0))
    if get("Ball") is not None:
        balls = [((1, 1, 1), (0.25, 0.25, 0.25), (2.35, 0.95)), ((1, 0.55, 0.35), (1.0, 0.35, 0.08), (3.4, 5.2)),
                 ((0.65, 0.9, 1.0), (0.3, 0.75, 1.0), (5.2, 8.6)), ((1, 0.9, 0.4), (1.0, 0.85, 0.2), (1.2, 7.9))]
        for k, (tint, glow, (ux, uz)) in enumerate(balls):
            bm = ball_material("M_BallMock%d" % k, tint, glow, emission_png, args.palette_png)
            place(get("Ball"), col, sim_to_blender(ux, uz, 0.2), 0.0, scale=0.4, material=bm)


def game_camera():
    target = Vector((0.0, -(LAUNCH + ROWS) / 2.0 - 0.1, 0.0))
    p = math.radians(GAME_PITCH)
    loc = target + Vector((0.0, math.cos(p), math.sin(p))) * GAME_DIST
    return hr.look_camera("HP_GameCam", loc, target, fov_y_deg=GAME_FOV, clip=(1.0, 80.0))


# ------------------------------------------------------------------------------------------ main entry
def render_all(args, built, mat, emission_png, p2_png, stats):
    out = args.preview_dir
    scene = bpy.context.scene
    p2mat = hr.toon_material("M_Palette_CatP2", p2_png, emission_png)
    outline = hr.outline_material()
    src_cols = [v[2] for v in built.values()]
    mock_col = bpy.data.collections.new("MOCK")
    scene.collection.children.link(mock_col)
    arena_cols = {}
    for act in ("act1", "act2", "act3"):
        ac = bpy.data.collections.new("ARENA_" + act)
        scene.collection.children.link(ac)
        build_arena(mat, act, ac)
        arena_cols[act] = ac
    build_mock(args, built, mat, p2mat, emission_png, mock_col)
    every = src_cols + [mock_col] + list(arena_cols.values())

    # -------- game mock renders (no outlines: the game may not have them; honest readability check)
    hr.setup_render(640, 360, pixel=True)
    game_camera()
    for act, ac in arena_cols.items():
        set_visible([mock_col, ac], every)
        hr.render_pair(os.path.join(out, "game_" + act), act)

    # -------- beauty shots (outline on, AA on)
    hr.setup_render(256, 256, pixel=False, samples=16)
    for name, (root, parts, col) in built.items():
        lo, hi = hr.bounds(parts)
        radius = (hi - lo).length / 2
        for p in parts:
            hr.add_outline(p, outline, thickness=max(radius * 0.022, 0.006))
        set_visible([col], every)
        views = [("front", -35.0, 22.0, "studio"), ("game", 180.0, GAME_PITCH, "act1")]
        if name == "Cat_Hero":
            views.append(("idle", 35.0, GAME_PITCH, "act1"))  # CatView turns the idle cat 3/4 to the camera
        for view, yaw, pitch, preset in views:
            hr.frame_ortho(parts, yaw, pitch, margin=1.08)
            hr.render_pair(os.path.join(out, "model_%s_%s" % (name, view)), preset,
                           emission=0.0 if name == "Ball" else None)
        if name == "Cat_Hero":
            for p in parts:
                p.data.materials[0] = p2mat
            hr.frame_ortho(parts, -35.0, 22.0, margin=1.08)
            hr.render_pair(os.path.join(out, "model_Cat_HeroP2_front"), "studio")
            hr.frame_ortho(parts, 180.0, GAME_PITCH, margin=1.08)
            hr.render_pair(os.path.join(out, "model_Cat_HeroP2_game"), "act1")
            for p in parts:
                p.data.materials[0] = mat

    # -------- UI portraits: 128x128 pixel mode bust, 3/4 facing screen-right
    if "Cat_Hero" in built:
        root, parts, col = built["Cat_Hero"]
        set_visible([col], every)
        for p in parts:
            for mod in p.modifiers:
                if mod.type == "SOLIDIFY":
                    mod.thickness = 0.0055
        hr.setup_render(128, 128, pixel=True)
        cam = hr.frame_ortho(parts, -28.0, 12.0, margin=1.0, center=(0.015, -0.02, 0.605), radius=0.42)
        hr.sun("portrait")
        hr.set_params("portrait")
        hr.render(os.path.join(out, "portrait_p1_raw.png"))
        # where the mouth sits in the sprite: the 2D pass draws a pixel ':3' there (too small to model)
        from bpy_extras.object_utils import world_to_camera_view
        bpy.context.view_layer.update()
        mouth = world_to_camera_view(scene, cam, Vector((0.0, -0.243, 0.573)))
        with open(os.path.join(out, "portrait_meta.json"), "w") as f:
            json.dump({"mouth_px": [mouth.x * 128.0, (1.0 - mouth.y) * 128.0]}, f)
        for p in parts:
            p.data.materials[0] = p2mat
        hr.render(os.path.join(out, "portrait_p2_raw.png"))
        for p in parts:
            p.data.materials[0] = mat
