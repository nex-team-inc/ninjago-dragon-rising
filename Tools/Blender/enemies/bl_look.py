"""HD-2D preview look for Blender renders (icons + review sheets only - Unity uses its own BilliardRogue/ToonLit).

Unity runs in Gamma colour space, so shading is emulated on raw sRGB values: palette textures are loaded as
Non-Color and the view transform is Raw (PNG = palette colour x band multiplier, exactly like the game shader).
Toon = Diffuse -> ShaderToRGB -> luminance -> constant ColorRamp (N bands) -> x light tint + ambient -> x albedo.
Faces in the palette's emissive half are drawn unlit at full albedo; mode "emit" renders only them (bloom pass).
"""
import math
import os

import bpy
from mathutils import Vector

# 4-band cel ramp (luminance threshold -> light multiplier), matches the ToonLit "4 bands" art direction
BANDS4 = ((0.0, 0.46), (0.1, 0.64), (0.38, 0.84), (0.7, 1.0))
BANDS3 = ((0.0, 0.62), (0.2, 0.82), (0.55, 1.0))


def _img(path):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    img.colorspace_settings.name = "Non-Color"
    return img


def raw_color(scene):
    scene.display_settings.display_device = "sRGB"
    scene.view_settings.view_transform = "Raw"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0


def toon_material(name, albedo, emission_png=None, bands=BANDS4, tint=(1, 1, 1), ambient=(0.0, 0.0, 0.0),
                  mode="beauty", emit_gain=1.0):
    """albedo: palette PNG path or an (r, g, b) raw sRGB tuple. mode: 'beauty' | 'emit' | 'flat'."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit_sh = nt.nodes.new("ShaderNodeEmission")
    emit_sh.inputs["Strength"].default_value = 1.0
    nt.links.new(emit_sh.outputs["Emission"], out.inputs["Surface"])

    if isinstance(albedo, str):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = _img(albedo)
        tex.interpolation = "Closest"
        alb = tex.outputs["Color"]
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = tuple(albedo) + (1.0,)
        alb = rgb.outputs[0]
    emis = None
    if emission_png:
        et = nt.nodes.new("ShaderNodeTexImage")
        et.image = _img(emission_png)
        et.interpolation = "Closest"
        emis = et.outputs["Color"]

    def mix(a, b, blend, fac=1.0):
        node = nt.nodes.new("ShaderNodeMix")
        node.data_type = "RGBA"
        node.blend_type = blend
        node.clamp_result = False
        node.inputs["Factor"].default_value = fac
        for sock, val in (("A", a), ("B", b)):
            target = node.inputs[6 if sock == "A" else 7]
            if isinstance(val, tuple):
                target.default_value = val + (1.0,) if len(val) == 3 else val
            else:
                nt.links.new(val, target)
        return node

    if mode == "emit":
        if emis is None:
            emit_sh.inputs["Color"].default_value = (0, 0, 0, 1)
        else:
            nt.links.new(mix(emis, (emit_gain,) * 3, "MULTIPLY").outputs[2], emit_sh.inputs["Color"])
        return mat
    if mode == "flat":
        nt.links.new(alb, emit_sh.inputs["Color"])
        return mat

    diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    s2rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    nt.links.new(diffuse.outputs["BSDF"], s2rgb.inputs["Shader"])
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    nt.links.new(s2rgb.outputs["Color"], bw.inputs["Color"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    while len(els) < len(bands):
        els.new(0.5)
    for el, (pos, val) in zip(els, bands):
        el.position = pos
        el.color = (val, val, val, 1.0)
    nt.links.new(bw.outputs["Val"], ramp.inputs["Fac"])
    light = mix(ramp.outputs["Color"], tuple(tint), "MULTIPLY")
    light = mix(light.outputs[2], tuple(ambient), "ADD")
    lit = mix(light.outputs[2], alb, "MULTIPLY")
    result = lit.outputs[2]
    if emis is not None:
        # emissive faces: unlit full albedo (Unity adds HDR emission on top; bloom comes from the 'emit' pass)
        ebw = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(emis, ebw.inputs["Color"])
        mask = nt.nodes.new("ShaderNodeMath")
        mask.operation = "GREATER_THAN"
        mask.inputs[1].default_value = 0.002
        nt.links.new(ebw.outputs["Val"], mask.inputs[0])
        sel = nt.nodes.new("ShaderNodeMix")
        sel.data_type = "RGBA"
        sel.clamp_result = False
        nt.links.new(mask.outputs["Value"], sel.inputs["Factor"])
        nt.links.new(result, sel.inputs[6])
        nt.links.new(alb, sel.inputs[7])
        result = sel.outputs[2]
    nt.links.new(result, emit_sh.inputs["Color"])
    return mat


def outline_material(name="M_Outline", rgb=(0.05, 0.047, 0.063)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = tuple(rgb) + (1.0,)
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return mat


def add_hull(obj, mat, thickness):
    """Inverted-hull outline (review renders only; never exported)."""
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.use_even_offset = True
    mod.material_offset = len(obj.data.materials) - 1


def setup_eevee(scene, width, height, pixel, samples=16, transparent=True):
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.filter_size = 0.0 if pixel else 1.5
    scene.eevee.taa_render_samples = 1 if pixel else samples
    scene.eevee.use_shadows = True
    scene.eevee.shadow_resolution_scale = 1.0
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    raw_color(scene)
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0, 0, 0, 1)
    bg.inputs["Strength"].default_value = 0.0
    scene.world = world


def sun(scene, direction, intensity=1.0, name="Sun"):
    """Sun shining ALONG `direction` (from the light towards the scene). Hard shadows.
    intensity 1.0 = a white diffuse face pointing at the sun gets luminance 1.0 (Blender: energy / pi)."""
    data = bpy.data.lights.new(name, "SUN")
    data.energy = intensity * math.pi
    data.angle = 0.0
    data.use_shadow = True
    data.shadow_filter_radius = 0.0
    data.use_shadow_jitter = False
    obj = bpy.data.objects.new(name, data)
    obj.rotation_euler = Vector(direction).normalized().to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(obj)
    return obj


def look_camera(scene, location, target, lens_fov_deg=None, ortho_scale=None, name="Cam", vertical_fov=True):
    data = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, data)
    scene.collection.objects.link(cam)
    cam.location = Vector(location)
    d = (Vector(target) - Vector(location)).normalized()
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if ortho_scale is not None:
        data.type = "ORTHO"
        data.ortho_scale = ortho_scale
    else:
        data.sensor_fit = "VERTICAL" if vertical_fov else "AUTO"
        data.angle_y = math.radians(lens_fov_deg) if vertical_fov else math.radians(lens_fov_deg)
    data.clip_start = 0.05
    data.clip_end = 200.0
    scene.camera = cam
    return cam


def frame_ortho(scene, objs, direction, fill=1.0, aspect=1.0, name="IconCam"):
    """Ortho camera looking along `direction` that tightly frames all mesh vertices of `objs`.
    fill = fraction of the frame the larger extent may use."""
    bpy.context.view_layer.update()
    d = Vector(direction).normalized()
    rot = d.to_track_quat("-Z", "Y").to_matrix()
    right, up = rot.col[0], rot.col[1]
    pts = [o.matrix_world @ v.co for o in objs if o.type == "MESH" for v in o.data.vertices]
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    zs = [p.dot(d) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    # ortho_scale spans the larger render dimension (sensor fit AUTO)
    scale = max(w, h * aspect) / fill if aspect >= 1 else max(w / aspect, h) / fill
    depth = min(zs) - 5.0
    loc = right * cx + up * cy + d * depth
    cam = look_camera(scene, loc, loc + d, ortho_scale=scale, name=name)
    return cam, (w, h)


def render(scene, path):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
