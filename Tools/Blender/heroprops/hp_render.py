"""Preview rendering that approximates the in-game BilliardRogue/ToonLit look (review images only, never exported).

ToonLit (research/urp-hd2d-shaders.md): ramp = round(N.L * shadow * (bands-1)) / (bands-1);
lighting = lerp(ShadowTint, LightColor, ramp) + ambient; color = albedo * lighting + emission.
Here: a white Diffuse lit by a sun of strength pi -> ShaderToRGB = N.L * shadow (calibrated), constant ColorRamp with
4 bands, shared node-group parameters per lighting preset, and a 'bloom pass' switch that outputs only the emissive
palette half (Palette_Emission.png) so the venv post step can blur + add it like URP bloom.
"""
import math
import os

import bpy
from mathutils import Vector

PRESETS = {
    # light colour, shadow tint, ambient, emission strength, sun (elevation, azimuth) degrees, background
    "act1": dict(light=(1.0, 0.88, 0.70), shadow=(0.46, 0.40, 0.58), ambient=(0.10, 0.08, 0.06), emis=1.0,
                 sun=(42.0, 35.0), bg=(0.10, 0.08, 0.07)),
    "act2": dict(light=(0.62, 0.72, 1.0), shadow=(0.20, 0.22, 0.40), ambient=(0.06, 0.07, 0.12), emis=1.1,
                 sun=(55.0, 330.0), bg=(0.03, 0.04, 0.08)),
    "act3": dict(light=(0.90, 0.76, 1.0), shadow=(0.34, 0.24, 0.55), ambient=(0.10, 0.06, 0.14), emis=1.1,
                 sun=(50.0, 20.0), bg=(0.06, 0.03, 0.09)),
    "studio": dict(light=(1.0, 0.95, 0.88), shadow=(0.50, 0.47, 0.64), ambient=(0.08, 0.08, 0.10), emis=0.9,
                   sun=(45.0, 250.0), bg=(0.0, 0.0, 0.0)),
    # front key from screen-left for the UI portraits (camera sits front-right of the cat)
    "portrait": dict(light=(1.0, 0.94, 0.84), shadow=(0.52, 0.46, 0.66), ambient=(0.07, 0.06, 0.09), emis=0.8,
                     sun=(38.0, 235.0), bg=(0.0, 0.0, 0.0)),
}

_PARAMS = None


def _params_group():
    """One node group shared by every preview material: light/shadow/ambient/emission/bloom-mode values."""
    global _PARAMS
    if _PARAMS is not None:
        return _PARAMS
    ng = bpy.data.node_groups.new("HP_Params", "ShaderNodeTree")
    out = ng.nodes.new("NodeGroupOutput")
    for name, kind in (("Light", "NodeSocketColor"), ("Shadow", "NodeSocketColor"), ("Ambient", "NodeSocketColor"),
                       ("Emission", "NodeSocketFloat"), ("BloomPass", "NodeSocketFloat")):
        ng.interface.new_socket(name, in_out="OUTPUT", socket_type=kind)
    for name in ("Light", "Shadow", "Ambient"):
        n = ng.nodes.new("ShaderNodeRGB")
        n.name = name
        ng.links.new(n.outputs[0], out.inputs[name])
    for name in ("Emission", "BloomPass"):
        n = ng.nodes.new("ShaderNodeValue")
        n.name = name
        ng.links.new(n.outputs[0], out.inputs[name])
    _PARAMS = ng
    return ng


def set_params(preset, bloom_pass=False, emission=None):
    ng = _params_group()
    p = PRESETS[preset]
    ng.nodes["Light"].outputs[0].default_value = p["light"] + (1.0,)
    ng.nodes["Shadow"].outputs[0].default_value = p["shadow"] + (1.0,)
    ng.nodes["Ambient"].outputs[0].default_value = p["ambient"] + (1.0,)
    ng.nodes["Emission"].outputs[0].default_value = p["emis"] if emission is None else emission
    ng.nodes["BloomPass"].outputs[0].default_value = 1.0 if bloom_pass else 0.0


def _load(path):
    return bpy.data.images.load(os.path.abspath(path), check_existing=True)


def toon_material(name, albedo_png, emission_png, bands=4, tint=None):
    """Palette material with the ToonLit-like node chain. `tint` multiplies albedo (ball colours in the mock)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    nt.nodes.clear()
    L = nt.links.new
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = _load(albedo_png)
    tex.interpolation = "Closest"
    etex = nt.nodes.new("ShaderNodeTexImage")
    etex.image = _load(emission_png)
    etex.interpolation = "Closest"
    diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    s2rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    steps = bands - 1
    stops = [(0.0, 0.0)] + [((k - 0.5) / steps, k / steps) for k in range(1, bands)]
    els = ramp.color_ramp.elements
    while len(els) < len(stops):
        els.new(0.5)
    for el, (pos, val) in zip(els, stops):
        el.position = pos
        el.color = (val, val, val, 1.0)
    grp = nt.nodes.new("ShaderNodeGroup")
    grp.node_tree = _params_group()
    lerp = nt.nodes.new("ShaderNodeMix")
    lerp.data_type = "RGBA"
    add_amb = nt.nodes.new("ShaderNodeMix")
    add_amb.data_type = "RGBA"
    add_amb.blend_type = "ADD"
    add_amb.inputs["Factor"].default_value = 1.0
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    emul = nt.nodes.new("ShaderNodeVectorMath")
    emul.operation = "SCALE"
    add_e = nt.nodes.new("ShaderNodeMix")
    add_e.data_type = "RGBA"
    add_e.blend_type = "ADD"
    add_e.inputs["Factor"].default_value = 1.0
    final = nt.nodes.new("ShaderNodeMix")
    final.data_type = "RGBA"
    emit = nt.nodes.new("ShaderNodeEmission")

    L(diffuse.outputs["BSDF"], s2rgb.inputs["Shader"])
    L(s2rgb.outputs["Color"], ramp.inputs["Fac"])
    L(ramp.outputs["Color"], lerp.inputs["Factor"])
    L(grp.outputs["Shadow"], lerp.inputs["A"])
    L(grp.outputs["Light"], lerp.inputs["B"])
    L(lerp.outputs["Result"], add_amb.inputs["A"])
    L(grp.outputs["Ambient"], add_amb.inputs["B"])
    albedo = tex.outputs["Color"]
    if tint is not None:
        tmul = nt.nodes.new("ShaderNodeMix")
        tmul.data_type = "RGBA"
        tmul.blend_type = "MULTIPLY"
        tmul.inputs["Factor"].default_value = 1.0
        tmul.inputs["B"].default_value = tuple(tint) + (1.0,)
        L(tex.outputs["Color"], tmul.inputs["A"])
        albedo = tmul.outputs["Result"]
    L(albedo, mul.inputs["A"])
    L(add_amb.outputs["Result"], mul.inputs["B"])
    L(etex.outputs["Color"], emul.inputs["Vector"])
    L(grp.outputs["Emission"], emul.inputs["Scale"])
    L(mul.outputs["Result"], add_e.inputs["A"])
    L(emul.outputs["Vector"], add_e.inputs["B"])
    L(add_e.outputs["Result"], final.inputs["A"])
    L(emul.outputs["Vector"], final.inputs["B"])
    L(grp.outputs["BloomPass"], final.inputs["Factor"])
    L(final.outputs["Result"], emit.inputs["Color"])
    L(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def outline_material(name="HP_Outline", rgba=(0.06, 0.04, 0.08, 1.0)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    # the flipped hull encloses the model: without this its near side shadows the whole model in EEVEE
    mat.use_backface_culling_shadow = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = rgba
    # in the bloom pass outlines must be black too -> reuse the group switch
    grp = nt.nodes.new("ShaderNodeGroup")
    grp.node_tree = _params_group()
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = rgba
    mix.inputs["B"].default_value = (0, 0, 0, 1)
    nt.links.new(grp.outputs["BloomPass"], mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def add_outline(obj, mat, thickness):
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = len(obj.data.materials) - 1
    return mod


# ---------------------------------------------------------------- scene / camera
def setup_render(size_x, size_y, pixel, samples=16):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = size_x, size_y
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.filter_size = 0.0 if pixel else 1.5
    scene.eevee.taa_render_samples = 1 if pixel else samples
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    if scene.world is None:
        world = bpy.data.worlds.new("HP_World")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.0
        scene.world = world
    return scene


def sun(preset):
    """(Re)create the single hard-shadow key light for a preset. Blender -Y = arena north (enemies)."""
    scene = bpy.context.scene
    old = bpy.data.objects.get("HP_Sun")
    if old is None:
        data = bpy.data.lights.new("HP_Sun", "SUN")
        old = bpy.data.objects.new("HP_Sun", data)
        scene.collection.objects.link(old)
    data = old.data
    data.energy = math.pi  # calibrated: white diffuse facing the sun -> ShaderToRGB = 1.0
    data.color = (1, 1, 1)
    data.angle = 0.0
    data.use_shadow = True
    elev, azim = PRESETS[preset]["sun"]
    e, a = math.radians(elev), math.radians(azim)
    # direction the light travels FROM (towards the scene): azimuth measured from +Y (south / camera side)
    to_light = Vector((math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e)))
    old.rotation_euler = (-to_light).to_track_quat("-Z", "Y").to_euler()
    return old


def look_camera(name, location, target, ortho_scale=None, fov_y_deg=None, clip=(0.1, 200.0)):
    scene = bpy.context.scene
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        scene.collection.objects.link(cam)
    cd = cam.data
    if ortho_scale is not None:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho_scale
    else:
        cd.type = "PERSP"
        cd.sensor_fit = "VERTICAL"
        cd.angle_y = math.radians(fov_y_deg)
    cd.clip_start, cd.clip_end = clip
    cam.location = Vector(location)
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    return cam


def bounds(objs):
    bpy.context.view_layer.update()
    pts = []
    for o in objs:
        if o.type != "MESH":
            continue
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def frame_ortho(objs, yaw_deg, pitch_deg, margin=1.12, name="HP_Cam", center=None, radius=None):
    lo, hi = bounds(objs)
    c = (lo + hi) / 2 if center is None else Vector(center)
    r = (hi - lo).length / 2 if radius is None else radius
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    # yaw 0 = camera in front of the model (Blender -Y side), positive yaw orbits towards +X
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    return look_camera(name, c + d * r * 6.0, c, ortho_scale=2 * r * margin, clip=(r * 0.5, r * 12.0))


def render(path):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.context.scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)


def render_pair(path_base, preset, emission=None):
    """Beauty + emission-only pass (<base>.png, <base>_emis.png) for the venv bloom composite."""
    sun(preset)
    set_params(preset, bloom_pass=False, emission=emission)
    render(path_base + ".png")
    set_params(preset, bloom_pass=True, emission=emission)
    render(path_base + "_emis.png")
    set_params(preset, bloom_pass=False)
