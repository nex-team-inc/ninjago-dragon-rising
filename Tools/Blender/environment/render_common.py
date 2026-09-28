"""Preview-render helpers for the environment kit (Blender 5.2 EEVEE, headless). Review tooling only: nothing here
reaches Unity. Approximates BilliardRogue/ToonLit: banded main + point lighting, flat ambient, palette emission.

Scenes are assembled from the exported FBX files (so the preview also validates the export) and placed with Unity
transforms from layouts.json: Unity (x, y, z, rotY) -> Blender (-x, -z, y, rotZ = -rotY).
"""
import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

SURFACE_NAMES = ("StoneFloor", "MossyBrick", "CryptBrick", "CryptFloor", "WoodPlank", "CrystalRock", "Dirt", "Grass")


def u2b(x, y, z):
    return Vector((-x, -z, y))


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    return scene


# ---------------------------------------------------------------- materials
def _img(path, colorspace="sRGB"):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    img.colorspace_settings.name = colorspace
    return img


def _uv_tex(nt, img):
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    return tex.outputs["Color"]


def _world_box(nt, img, tiling, offset=(0.5, 0.0, 0.4)):
    """World-space box projection in Unity metres (what ToonLit should do for *_Surface): top faces sample (x, z),
    X-facing (z, y), Z-facing (x, y); uv = (world + offset) * tiling. Hard selection by the dominant normal axis."""
    N, L = nt.nodes.new, nt.links.new
    geo = N("ShaderNodeNewGeometry")
    pos, nrm = N("ShaderNodeSeparateXYZ"), N("ShaderNodeSeparateXYZ")
    L(geo.outputs["Position"], pos.inputs[0])
    L(geo.outputs["Normal"], nrm.inputs[0])

    def op(operation, *args):
        m = N("ShaderNodeMath")
        m.operation = operation
        for sock, arg in zip(m.inputs, args):
            if isinstance(arg, float):
                sock.default_value = arg
            else:
                L(arg, sock)
        return m.outputs[0]
    ox, oy, oz = offset
    ux = op("MULTIPLY", op("ADD", op("MULTIPLY", pos.outputs["X"], -1.0), ox), tiling)   # Unity x
    uy = op("MULTIPLY", op("ADD", pos.outputs["Z"], oy), tiling)                          # Unity y
    uz = op("MULTIPLY", op("ADD", op("MULTIPLY", pos.outputs["Y"], -1.0), oz), tiling)  # Unity z
    ax, ay, az = (op("ABSOLUTE", nrm.outputs[c]) for c in ("X", "Y", "Z"))
    m_top = op("MULTIPLY", op("GREATER_THAN", az, ax), op("GREATER_THAN", az, ay))
    m_x = op("MULTIPLY", op("SUBTRACT", 1.0, m_top), op("GREATER_THAN", ax, ay))
    m_z = op("SUBTRACT", op("SUBTRACT", 1.0, m_top), m_x)
    acc = None
    for (u, v), mask in (((ux, uz), m_top), ((uz, uy), m_x), ((ux, uy), m_z)):
        cmb = N("ShaderNodeCombineXYZ")
        L(u, cmb.inputs[0])
        L(v, cmb.inputs[1])
        tex = N("ShaderNodeTexImage")
        tex.image = img
        tex.interpolation = "Closest"
        L(cmb.outputs[0], tex.inputs["Vector"])
        sc = N("ShaderNodeVectorMath")
        sc.operation = "SCALE"
        L(tex.outputs["Color"], sc.inputs[0])
        L(mask, sc.inputs["Scale"])
        if acc is None:
            acc = sc.outputs[0]
        else:
            add = N("ShaderNodeVectorMath")
            add.operation = "ADD"
            L(acc, add.inputs[0])
            L(sc.outputs[0], add.inputs[1])
            acc = add.outputs[0]
    return acc


def _trilight(nt, amb):
    """Unity 'Gradient' ambient: sky colour for up-facing normals, equator for horizontal, ground for down.
    amb = {"sky": rgb, "equator": rgb, "ground": rgb, "intensity": k}."""
    N, L = nt.nodes.new, nt.links.new
    geo = N("ShaderNodeNewGeometry")
    sep = N("ShaderNodeSeparateXYZ")
    L(geo.outputs["Normal"], sep.inputs[0])

    def op(operation, a, b):
        m = N("ShaderNodeMath")
        m.operation = operation
        for sock, arg in zip(m.inputs, (a, b)):
            if isinstance(arg, float):
                sock.default_value = arg
            else:
                L(arg, sock)
        return m.outputs[0]
    up = sep.outputs["Z"]                                   # Blender Z = Unity Y
    w_sky = op("MAXIMUM", up, 0.0)
    w_gnd = op("MAXIMUM", op("MULTIPLY", up, -1.0), 0.0)
    w_eq = op("SUBTRACT", op("SUBTRACT", 1.0, w_sky), w_gnd)
    k = amb.get("intensity", 1.0)
    acc = None
    for key, w in (("sky", w_sky), ("equator", w_eq), ("ground", w_gnd)):
        sc = N("ShaderNodeVectorMath")
        sc.operation = "SCALE"
        sc.inputs[0].default_value = [c * k for c in amb[key]]
        L(w, sc.inputs["Scale"])
        if acc is None:
            acc = sc.outputs[0]
        else:
            add = N("ShaderNodeVectorMath")
            add.operation = "ADD"
            L(acc, add.inputs[0])
            L(sc.outputs[0], add.inputs[1])
            acc = add.outputs[0]
    return acc


def toon_material(name, albedo_img, ambient, emission_img=None, emission_strength=0.0, bands=None,
                  cavity_img=None, world_tiling=None, tint=None):
    """Emission-only node tree: albedo [* tint] * (ambient + banded(direct light)) [* cavity] + emission * strength.
    world_tiling != None -> albedo/cavity use world-space box mapping (surface materials); tint = _BaseColor."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True     # Unity culls back faces: flipped normals must show up in previews
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    albedo = _world_box(nt, albedo_img, world_tiling) if world_tiling else _uv_tex(nt, albedo_img)
    if tint is not None and tuple(tint) != (1.0, 1.0, 1.0):
        tm = N("ShaderNodeVectorMath")
        tm.operation = "MULTIPLY"
        L(albedo, tm.inputs[0])
        tm.inputs[1].default_value = tuple(tint)
        albedo = tm.outputs[0]
    diffuse = N("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    s2rgb = N("ShaderNodeShaderToRGB")
    L(diffuse.outputs["BSDF"], s2rgb.inputs["Shader"])
    bw = N("ShaderNodeRGBToBW")
    L(s2rgb.outputs["Color"], bw.inputs["Color"])
    ramp = N("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    stops = bands or ((0.0, 0.0), (0.06, 0.28), (0.3, 0.62), (0.62, 1.0), (1.35, 1.45))
    els = ramp.color_ramp.elements
    while len(els) < len(stops):
        els.new(0.99)
    # ramp input is lum / 2 so levels above 1 (strong point lights) still quantize
    for el, (pos, val) in zip(els, stops):
        el.position = min(pos / 2.0, 0.999)
        el.color = (val, val, val, 1)
    half = N("ShaderNodeMath")
    half.operation = "MULTIPLY"
    half.inputs[1].default_value = 0.5
    L(bw.outputs["Val"], half.inputs[0])
    L(half.outputs[0], ramp.inputs["Fac"])
    mx = N("ShaderNodeMath")
    mx.operation = "MAXIMUM"
    mx.inputs[1].default_value = 1e-4
    L(bw.outputs["Val"], mx.inputs[0])
    div = N("ShaderNodeMath")
    div.operation = "DIVIDE"
    L(ramp.outputs["Color"], div.inputs[0])
    L(mx.outputs[0], div.inputs[1])
    lit = N("ShaderNodeVectorMath")
    lit.operation = "SCALE"
    L(s2rgb.outputs["Color"], lit.inputs[0])
    L(div.outputs[0], lit.inputs["Scale"])
    amb = N("ShaderNodeVectorMath")
    amb.operation = "ADD"
    if isinstance(ambient, dict):
        L(_trilight(nt, ambient), amb.inputs[1])
    else:
        amb.inputs[1].default_value = ambient
    L(lit.outputs[0], amb.inputs[0])
    col = N("ShaderNodeVectorMath")
    col.operation = "MULTIPLY"
    L(albedo, col.inputs[0])
    L(amb.outputs[0], col.inputs[1])
    last = col.outputs[0]
    if cavity_img is not None:
        cav = _world_box(nt, cavity_img, world_tiling) if world_tiling else _uv_tex(nt, cavity_img)
        bwc = N("ShaderNodeRGBToBW")
        L(cav, bwc.inputs["Color"])
        cmul = N("ShaderNodeMath")          # 1 + (cavity - 0.5) * 1.2
        cmul.operation = "MULTIPLY_ADD"
        cmul.inputs[1].default_value = 1.2
        cmul.inputs[2].default_value = 0.4
        L(bwc.outputs["Val"], cmul.inputs[0])
        cm = N("ShaderNodeVectorMath")
        cm.operation = "SCALE"
        L(last, cm.inputs[0])
        L(cmul.outputs[0], cm.inputs["Scale"])
        last = cm.outputs[0]
    if emission_img is not None and emission_strength > 0:
        es = N("ShaderNodeVectorMath")
        es.operation = "SCALE"
        es.inputs["Scale"].default_value = emission_strength
        L(_uv_tex(nt, emission_img), es.inputs[0])
        add = N("ShaderNodeVectorMath")
        add.operation = "ADD"
        L(last, add.inputs[0])
        L(es.outputs[0], add.inputs[1])
        last = add.outputs[0]
    emit = N("ShaderNodeEmission")
    L(last, emit.inputs["Color"])
    L(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def surface_textures(name, dirs):
    """(albedo, cavity|None, tiling) for a surface name; tiling = 1 / tileMetres from surfaces.json if present."""
    import json
    for d in dirs:
        if not d:
            continue
        alb = os.path.join(d, f"{name}_Albedo.png")
        if os.path.exists(alb):
            cav = os.path.join(d, f"{name}_Cavity.png")
            tiling = 1.0
            meta = os.path.join(d, "surfaces.json")
            if os.path.exists(meta):
                with open(meta) as f:
                    tiling = 1.0 / json.load(f)["surfaces"].get(name, {}).get("tileMetres", 1.0)
            return _img(alb), (_img(cav, "Non-Color") if os.path.exists(cav) else None), tiling
    raise SystemExit(f"no surface texture for {name} in {dirs}")


def surface_material(name, albedo, ambient, cavity, tiling, tint=None):
    return toon_material(name, albedo, ambient, cavity_img=cavity, world_tiling=tiling, tint=tint)


# ---------------------------------------------------------------- scene assembly
def import_piece(fbx_path, name):
    """Import one FBX into a (non-rendered) collection and return it for instancing."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(fbx_path))
    objs = [o for o in bpy.data.objects if o not in before]
    coll = bpy.data.collections.new(name)
    for o in objs:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
    return coll


def part_name(obj):
    n = obj.name
    return n.rsplit(".", 1)[0] if "." in n and n.rsplit(".", 1)[1].isdigit() else n


def place(coll, x, y, z, rot_y=0.0, scale=1.0, name=None):
    inst = bpy.data.objects.new(name or coll.name, None)
    inst.instance_type = "COLLECTION"
    inst.instance_collection = coll
    inst.location = u2b(x, y, z)
    inst.rotation_euler = Euler((0.0, 0.0, math.radians(-rot_y)))
    s = scale if isinstance(scale, (list, tuple)) else (scale, scale, scale)
    inst.scale = (s[0], s[2], s[1])
    bpy.context.scene.collection.objects.link(inst)
    return inst


def unity_point(x, y, z, rot_y, scale, local):
    """Transform a piece-local Unity point by a Unity placement."""
    lx, ly, lz = (c * (scale if not isinstance(scale, (list, tuple)) else 1.0) for c in local)
    a = math.radians(rot_y)
    return (x + lx * math.cos(a) + lz * math.sin(a), y + ly, z - lx * math.sin(a) + lz * math.cos(a))


def add_sun(direction_u, color, strength, angle_deg=0.2):
    """direction_u = Unity light travel direction (like a Directional Light's forward)."""
    data = bpy.data.lights.new("Sun", "SUN")
    data.color = color
    data.energy = strength
    data.angle = math.radians(angle_deg)
    data.use_shadow = True
    for attr, val in (("shadow_filter_radius", 0.0), ("use_shadow_jitter", False), ("shadow_maximum_resolution", 0.004)):
        if hasattr(data, attr):
            setattr(data, attr, val)
    obj = bpy.data.objects.new("Sun", data)
    d = u2b(*direction_u).normalized()
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(obj)
    return obj


def add_point(pos_u, color, power, radius=0.05, shadow=False, name="Point"):
    data = bpy.data.lights.new(name, "POINT")
    data.color = color
    data.energy = power
    data.shadow_soft_size = radius
    data.use_shadow = shadow
    obj = bpy.data.objects.new(name, data)
    obj.location = u2b(*pos_u)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def add_camera(target_u, pitch_deg, fov_deg, distance, yaw_deg=0.0):
    """Game-like camera: sits south of the target (low Z), pitched down; FOV is vertical (Unity convention)."""
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.sensor_fit = "VERTICAL"
    cam_data.angle_y = math.radians(fov_deg)
    cam = bpy.data.objects.new("Cam", cam_data)
    p, yw = math.radians(pitch_deg), math.radians(yaw_deg)
    fwd_u = (math.sin(yw) * math.cos(p), -math.sin(p), math.cos(yw) * math.cos(p))
    tx, ty, tz = target_u
    pos_u = (tx - fwd_u[0] * distance, ty - fwd_u[1] * distance, tz - fwd_u[2] * distance)
    cam.location = u2b(*pos_u)
    cam.rotation_euler = u2b(*fwd_u).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_start = 1.0
    cam_data.clip_end = 200.0
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


def setup_render(width, height, samples=1):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.resolution_percentage = 100
    scene.render.filter_size = 0.0 if samples == 1 else 1.0
    scene.eevee.taa_render_samples = samples
    if hasattr(scene.eevee, "use_shadows"):
        scene.eevee.use_shadows = True
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0, 0, 0, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.0
    scene.world = world
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "32"
    scene.render.image_settings.exr_codec = "ZIP"
    return scene


def render_to_npy(exr_path, npy_path):
    """Render, then reload the EXR and dump linear RGB float32 (rows top-down) for the venv post-processing."""
    import numpy as np
    scene = bpy.context.scene
    scene.render.filepath = os.path.abspath(exr_path)
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(os.path.abspath(exr_path))
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    arr = px.reshape(h, w, 4)[::-1, :, :3]
    np.save(npy_path, arr.astype(np.float32))
    return arr


# ---------------------------------------------------------------- effects (preview stand-ins for the Unity shaders)
U2B = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))


def unity_matrix(pos, rot3, scale3):
    """Blender world matrix for a Unity TRS (rot3 = Unity 3x3 rotation, scale3 = Unity local scale)."""
    m = U2B @ rot3 @ Matrix.Diagonal(scale3) @ U2B.inverted()
    out = m.to_4x4()
    out.translation = u2b(*pos)
    return out


def unity_euler_matrix(ex, ey, ez=0.0):
    """Quaternion.Euler(ex, ey, ez) as a 3x3 (Unity applies z, then x, then y)."""
    a, b, c = (math.radians(v) for v in (ex, ey, ez))
    rx = Matrix(((1, 0, 0), (0, math.cos(a), -math.sin(a)), (0, math.sin(a), math.cos(a))))
    ry = Matrix(((math.cos(b), 0, math.sin(b)), (0, 1, 0), (-math.sin(b), 0, math.cos(b))))
    rz = Matrix(((math.cos(c), -math.sin(c), 0), (math.sin(c), math.cos(c), 0), (0, 0, 1)))
    return ry @ rx @ rz


def light_shaft_material(name, color, intensity):
    """Additive god-ray: emission * N.V edge softness * length fade (UV v) * streak noise, no depth write."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = False
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new

    def op(operation, a, b=None, c=None):
        m = N("ShaderNodeMath")
        m.operation = operation
        for sock, arg in zip(m.inputs, (a, b, c)):
            if arg is None:
                continue
            if isinstance(arg, float):
                sock.default_value = arg
            else:
                L(arg, sock)
        return m.outputs[0]
    out = N("ShaderNodeOutputMaterial")
    lw = N("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    edge = op("POWER", op("SUBTRACT", 1.0, lw.outputs["Facing"]), 2.2)
    uv = N("ShaderNodeUVMap")
    sep = N("ShaderNodeSeparateXYZ")
    L(uv.outputs["UV"], sep.inputs[0])
    v = sep.outputs["Y"]
    fade = op("MULTIPLY", op("SMOOTH_MIN", op("DIVIDE", v, 0.38), 1.0, 0.1),
              op("SMOOTH_MIN", op("DIVIDE", op("SUBTRACT", 1.0, v), 0.14), 1.0, 0.1))
    cmb = N("ShaderNodeCombineXYZ")
    L(op("MULTIPLY", sep.outputs["X"], 5.0), cmb.inputs[0])
    L(op("MULTIPLY", v, 0.6), cmb.inputs[1])
    noise = N("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.0
    L(cmb.outputs[0], noise.inputs["Vector"])
    streak = op("MULTIPLY_ADD", noise.outputs["Fac"], 0.9, 0.55)
    k = op("MULTIPLY", op("MULTIPLY", edge, fade), streak)
    emit = N("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (*color, 1.0)
    L(op("MULTIPLY", k, float(intensity)), emit.inputs["Strength"])
    tr = N("ShaderNodeBsdfTransparent")
    add = N("ShaderNodeAddShader")
    L(tr.outputs[0], add.inputs[0])
    L(emit.outputs[0], add.inputs[1])
    L(add.outputs[0], out.inputs["Surface"])
    return mat


def glow_material(name, strength=1.0, additive=False):
    """Vertex-colour emission for particle stand-ins (colour attribute 'Col')."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = False
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    attr = N("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    emit = N("ShaderNodeEmission")
    L(attr.outputs["Color"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = strength
    if additive:
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"
        tr = N("ShaderNodeBsdfTransparent")
        add = N("ShaderNodeAddShader")
        L(tr.outputs[0], add.inputs[0])
        L(emit.outputs[0], add.inputs[1])
        L(add.outputs[0], out.inputs["Surface"])
    else:
        L(emit.outputs[0], out.inputs["Surface"])
    return mat


PARTICLE_SHAPES = {   # (quad aspect, shape) of the preview billboards
    "Leaf": "diamond", "Mote": "square", "Dust": "square", "Ember": "square", "Star": "plus", "Snow": "square",
    "Spark": "square", "Smoke": "disc", "Bubble": "square",
}


def particle_billboards(name, emitter, cam_right_u, cam_up_u, seed):
    """Deterministic snapshot of an ambient emitter: steady-state alive count of camera-facing sprites in its box."""
    import bmesh
    import random
    rng = random.Random(seed)
    life = sum(emitter["lifetime"]) / 2
    count = int(min(emitter["maxParticles"], emitter["rate"] * life))
    cx, cy, cz = emitter["x"], emitter["y"], emitter["z"]
    sx, sy, sz = emitter["size"]
    shape = PARTICLE_SHAPES.get(emitter["type"], "square")
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    R, U = Vector(cam_right_u), Vector(cam_up_u)
    for _ in range(count):
        c = Vector((cx + rng.uniform(-sx, sx) / 2, cy + rng.uniform(-sy, sy) / 2, cz + rng.uniform(-sz, sz) / 2))
        s = rng.uniform(*emitter["startSize"]) / 2
        rgb = rng.choice(emitter["colors"])
        if shape == "diamond":
            rot = rng.uniform(0, math.pi)
            a, b = R * math.cos(rot) + U * math.sin(rot), -R * math.sin(rot) + U * math.cos(rot)
            quads = [[c + a * s, c + b * s * 0.55, c - a * s, c - b * s * 0.55]]
        elif shape == "plus":
            quads = [[c + R * s + U * s * 0.2, c - R * s + U * s * 0.2, c - R * s - U * s * 0.2, c + R * s - U * s * 0.2],
                     [c + U * s + R * s * 0.2, c - U * s + R * s * 0.2, c - U * s - R * s * 0.2, c + U * s - R * s * 0.2]]
        elif shape == "disc":
            quads = [[c + (R * math.cos(t) + U * math.sin(t) * 0.55) * s for t in
                      (2 * math.pi * k / 8 for k in range(8))]]
        else:
            quads = [[c + R * s + U * s, c - R * s + U * s, c - R * s - U * s, c + R * s - U * s]]
        for q in quads:
            f = bm.faces.new([bm.verts.new(u2b(*p)) for p in q])
            for loop in f.loops:
                loop[col] = (*rgb, 1.0)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    obj.visible_shadow = False
    bpy.context.scene.collection.objects.link(obj)
    return obj
