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


def toon_material(name, albedo_img, ambient, emission_img=None, emission_strength=0.0, bands=None,
                  cavity_img=None, world_tiling=None):
    """Emission-only node tree: albedo * (ambient + banded(direct light)) [* cavity] + emission map * strength.
    world_tiling != None -> albedo/cavity use world-space box mapping (surface materials)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True     # Unity culls back faces: flipped normals must show up in previews
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    albedo = _world_box(nt, albedo_img, world_tiling) if world_tiling else _uv_tex(nt, albedo_img)
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


def surface_material(name, albedo, ambient, cavity, tiling):
    return toon_material(name, albedo, ambient, cavity_img=cavity, world_tiling=tiling)


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
