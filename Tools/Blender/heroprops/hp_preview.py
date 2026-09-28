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
# the REAL environment surfaces per act (Tools/Blender/environment/make_layouts.py 'surfaces'); 2D-art owns them
ACT_SURFACES = {
    "act1": dict(floor="StoneFloor", wall="MossyBrick", ground="Grass"),
    "act2": dict(floor="CryptFloor", wall="CryptBrick", ground="Dirt"),
    "act3": dict(floor="StoneFloor", wall="CrystalRock", ground="CryptFloor"),
}
FALLBACK = {"act1": ("gray", 8), "act2": ("indigo", 4), "act3": ("purple", 5)}  # if the surfaces are not staged


def _black_png(out_dir):
    path = os.path.join(out_dir, "_black.png")
    img = bpy.data.images.new("HP_Black", 4, 4)
    img.pixels = [0.0, 0.0, 0.0, 1.0] * 16
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return path


def _surface_meta(staging):
    path = os.path.join(staging, "Textures", "BilliardRogue", "Surfaces", "surfaces.json")
    try:
        with open(path) as f:
            return json.load(f).get("surfaces", {})
    except (OSError, ValueError):
        return {}


def _surface_mat(staging, name, black, tint=None):
    albedo = os.path.join(staging, "Textures", "BilliardRogue", "Surfaces", name + "_Albedo.png")
    if not os.path.exists(albedo):
        return None
    return hr.toon_material("M_Mock_%s%s" % (name, "_T" if tint else ""), albedo, black, tint=tint)


def _uv_box(bm, uv, tile):
    """Box-project UVs in metres / tileMetres (world-aligned, like env_lib's *_Surface parts)."""
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda k: abs(n[k]))
        for loop in f.loops:
            co = loop.vert.co
            u, v = [(co.y, co.z), (co.x, co.z), (co.x, co.y)][ax]
            loop[uv].uv = (u / tile, v / tile)


def _surface_obj(name, boxes, material, col, tile):
    """Boxes [(lo, hi), ...] as one object with metre box UVs (no palette UVs) on a surface material."""
    import bmesh
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    for lo, hi in boxes:
        vs = [bm.verts.new((x, y, z)) for z in (lo[2], hi[2]) for y in (lo[1], hi[1]) for x in (lo[0], hi[0])]
        for q in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            bm.faces.new([vs[i] for i in q])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    _uv_box(bm, uv, tile)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob


def build_arena(mat, act, col, staging, black):
    """Arena mock on the real act surfaces: 7x10 floor tiles (1 m, 3 cm grout), danger row with the emissive red
    inlay of Env_FloorTile_Danger, launch pad, walls, surrounding ground. Falls back to palette colours."""
    names = ACT_SURFACES[act]
    meta = _surface_meta(staging)
    tile_m = lambda n: float(meta.get(n, {}).get("tileMetres", 2.0))  # noqa: E731
    floor = _surface_mat(staging, names["floor"], black)
    top = LAUNCH + ROWS
    g = 0.015
    if floor is not None:
        danger = _surface_mat(staging, names["floor"], black, tint=(1.0, 0.62, 0.58))
        wall = _surface_mat(staging, names["wall"], black) or floor
        ground = _surface_mat(staging, names["ground"], black) or floor
        tiles, dtiles = [], []
        for row in range(ROWS):
            for c in range(COLS):
                p = cell(c, row)
                box = ((p.x - 0.5 + g, p.y - 0.5 + g, -0.2), (p.x + 0.5 - g, p.y + 0.5 - g, 0.0))
                (dtiles if row == ROWS - 1 else tiles).append(box)
        _surface_obj("Floor_" + act, tiles, floor, col, tile_m(names["floor"]))
        _surface_obj("Danger_" + act, dtiles, danger, col, tile_m(names["floor"]))
        _surface_obj("Launch_" + act, [((-COLS / 2.0, -LAUNCH, -0.2), (COLS / 2.0, 0.6, 0.0))], floor, col,
                     tile_m(names["floor"]))
        walls = [((COLS / 2.0, -top - 0.35, -0.2), (COLS / 2.0 + 0.35, 0.6, 0.5)),
                 ((-COLS / 2.0 - 0.35, -top - 0.35, -0.2), (-COLS / 2.0, 0.6, 0.5)),
                 ((-COLS / 2.0 - 0.35, -top - 0.35, -0.2), (COLS / 2.0 + 0.35, -top, 0.5))]
        _surface_obj("Walls_" + act, walls, wall, col, tile_m(names["wall"]))
        _surface_obj("Ground_" + act, [((-9.0, -top - 6.0, -0.4), (9.0, 4.0, -0.2))], ground, col,
                     tile_m(names["ground"]))
        grout = Mesh()  # dark grout under the tile gaps
        grout.box((-COLS / 2.0, -top, -0.2), (COLS / 2.0, -LAUNCH, -0.01), C("gray", 2))
        m = grout
    else:
        fam, shade = FALLBACK[act]
        m = Mesh()
        for row in range(ROWS):
            for c in range(COLS):
                p = cell(c, row)
                fs = ("red", 7) if row == ROWS - 1 else (fam, shade + (row + c) % 2)
                m.block([(0.97, 0.97, 0.03, -0.2), (0.97, 0.97, 0.03, 0.0)], C(*fs), matrix=tr(p.x, p.y, 0.0),
                        cap0=False)
        m.box((-COLS / 2.0, -LAUNCH, -0.2), (COLS / 2.0, 0.6, 0.0), C(fam, shade - 1))
        m.box((-9.0, -top - 6.0, -0.4), (9.0, 4.0, -0.2), C(fam, max(shade - 3, 1)))
    for c in range(COLS):  # Env_FloorTile_Danger: emissive red inlay frame (inner 0.36 .. outer 0.465)
        p = cell(c, ROWS - 1)
        for (x0, y0, x1, y1) in ((-0.465, -0.465, 0.465, -0.36), (-0.465, 0.36, 0.465, 0.465),
                                 (-0.465, -0.36, -0.36, 0.36), (0.36, -0.36, 0.465, 0.36)):
            m.box((p.x + x0, p.y + y0, -0.01), (p.x + x1, p.y + y1, 0.004), C("red", 8, True))
    obj = m.to_object("ArenaPal_" + act, mat, origin=(0, 0, 0))
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


BALL_LOOKS = {  # (albedo tint, glow) ~ M_Ball_<Type>: Basic white, Thunder yellow, Flame orange, Vampire red, Frost
    "Basic": ((1, 1, 1), (0.25, 0.25, 0.25)), "Thunder": ((1, 0.9, 0.4), (1.0, 0.85, 0.2)),
    "Flame": ((1, 0.55, 0.35), (1.0, 0.35, 0.08)), "Vampire": ((1, 0.4, 0.45), (1.0, 0.15, 0.2)),
    "Frost": ((0.65, 0.9, 1.0), (0.3, 0.75, 1.0)),
}


def cell_centre(c, r):
    return c + 0.5, LAUNCH + (ROWS - 1 - r) + 0.5


def build_mock(args, built, mat, p2mat, emission_png, col):
    """Readability layout: enemies in rows 0-2, every prop in row 3, the three pickups in row 5 flanked by live
    balls (pickup vs ball confusion check), a ball hidden behind a pillar (occlusion check), and in the launch zone
    both cats from BEHIND (striking pose) and both idle 3/4 (P1 left, P2 right of each pair)."""
    get = lambda n: (built[n][0], built[n][1]) if n in built else None  # noqa: E731
    layout = [("Prop_Pillar", 0, 3, 0), ("Prop_Crate", 2, 3, 8), ("Prop_Portal", 4, 3, 0), ("Prop_Mud", 6, 3, 0),
              ("Pickup_ExtraBall", 1, 5, 0), ("Pickup_Heal", 3, 5, 0), ("Pickup_Power", 5, 5, 0),
              ("Prop_Pillar", 4, 7, 0), ("Prop_Mud", 1, 7, 0), ("Prop_Portal", 6, 8, 0), ("Pickup_ExtraBall", 2, 8, 0),
              ("Pickup_Power", 5, 8, 0)]
    for name, c, r, yaw in layout:
        if get(name) is not None:
            place(get(name), col, cell(c, r), yaw)
    ctx = import_context_enemies(args.context_staging or args.staging, mat, col)
    spots = [(0, 0), (1, 0), (3, 0), (4, 1), (6, 1), (2, 1), (5, 0), (1, 2), (5, 2), (3, 2), (6, 0), (0, 1)]
    for (name, holder), (c, r) in zip(sorted(ctx.items()), spots):
        holder.location = cell(c, r)
        holder.rotation_euler = (0, 0, math.pi)  # EnemyView turns enemies 180 deg to face the camera
    if get("Cat_Hero") is not None:  # striking = model default (faces +Z / north), idle = turned 3/4 to camera
        place(get("Cat_Hero"), col, sim_to_blender(1.2, 0.55), 0.0)
        place(get("Cat_Hero"), col, sim_to_blender(2.75, 0.55), 0.0, material=p2mat)
        place(get("Cat_Hero"), col, sim_to_blender(4.6, 0.5), 145.0)
        place(get("Cat_Hero"), col, sim_to_blender(6.0, 0.5), 145.0, material=p2mat)
    if get("Cue_Stick") is not None:
        # cue held behind the ball, pointing up-arena along the aim (roughly what CatView will do)
        holder = place(get("Cue_Stick"), col, sim_to_blender(1.55, 0.02, 0.33), -18.0)
        holder.rotation_euler = (math.radians(-6), 0.0, math.radians(-18.0))
    if get("Ball") is not None:
        balls = [("Basic", (0.95, 1.0)), ("Basic", (0.5, 5.6)), ("Thunder", (2.5, 5.6)), ("Flame", (4.5, 5.55)),
                 ("Vampire", (6.5, 5.6)), ("Frost", cell_centre(4, 6)), ("Thunder", (3.4, 7.9)),
                 ("Basic", (1.6, 3.3))]
        for k, (kind, (ux, uz)) in enumerate(balls):
            tint, glow = BALL_LOOKS[kind]
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
    black = _black_png(out)
    for act in ("act1", "act2", "act3"):
        ac = bpy.data.collections.new("ARENA_" + act)
        scene.collection.children.link(ac)
        build_arena(mat, act, ac, args.context_staging or args.staging, black)
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
        cam = hr.frame_ortho(parts, -28.0, 12.0, margin=1.0, center=(0.015, -0.02, 0.628), radius=0.435)
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
