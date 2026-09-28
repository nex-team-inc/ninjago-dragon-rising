"""Shared helpers for headless Blender asset scripts (Blender 5.2, run with -b --factory-startup).

Import from a model script with:
    import os, sys; sys.path.insert(0, os.path.dirname(__file__)); import bl_common as bc
"""
import argparse
import json
import math
import os
import sys
import time

import bpy
from mathutils import Vector

_PALETTE = None


# ---------------------------------------------------------------- args / scene
def parse_args(extra=None):
    """Parse args after the '--' separator (Blender eats everything before it)."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--palette-json", required=True)
    p.add_argument("--palette-png", required=True)
    p.add_argument("--fbx", help="output .fbx path")
    p.add_argument("--icon", help="output icon .png path")
    p.add_argument("--icon-size", type=int, default=256)
    p.add_argument("--pixel", action="store_true", help="hard-edged low-res icon: no AA, 1 sample (upscale later)")
    p.add_argument("--engine", default="EEVEE", choices=["EEVEE", "WORKBENCH", "CYCLES"])
    p.add_argument("--persp", action="store_true", help="perspective icon camera instead of ortho")
    p.add_argument("--fbx-blender-defaults", action="store_true", help="test only: export with Blender's default FBX options")
    p.add_argument("--vcol", action="store_true", help="also write a vertex-colour attribute")
    p.add_argument("--anim", action="store_true", help="add + export object keyframe animation")
    p.add_argument("--blend", help="optional: save the .blend for manual inspection")
    if extra:
        extra(p)
    return p.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


# ---------------------------------------------------------------- palette
def load_palette(path):
    global _PALETTE
    with open(path) as f:
        _PALETTE = json.load(f)
    return _PALETTE


def palette_uv(family, shade):
    """UV of the texel centre for (family row, shade column). Image row 0 is the TOP => v = 1 - ..."""
    size = _PALETTE["size"]
    row = _PALETTE["families"][family]["row"]
    return ((shade + 0.5) / size, 1.0 - (row + 0.5) / size)


def palette_rgba(family, shade):
    h = _PALETTE["families"][family]["hex"][shade].lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)) + (1.0,)


def paint_faces(bm, faces, family, shade, vcol_layer=None):
    """Point every loop UV of `faces` at one palette texel (and optionally write the vertex colour)."""
    uv_layer = bm.loops.layers.uv.verify()
    uv = palette_uv(family, shade)
    rgba = palette_rgba(family, shade)
    for f in faces:
        for loop in f.loops:
            loop[uv_layer].uv = uv
            if vcol_layer is not None:
                loop[vcol_layer] = rgba


def palette_material(palette_png, name="M_Palette"):
    """Unlit-ish preview material for Blender renders. Unity ignores it (materialImportMode = None)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.abspath(palette_png), check_existing=True)  # relative paths fail on GPU upload
    tex.interpolation = "Closest"
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat, tex, bsdf, out


def make_toon(mat, steps=((0.0, 0.6), (0.25, 0.82), (0.6, 1.0))):
    """EEVEE-only cel look for icons: Diffuse -> ShaderToRGB -> constant ColorRamp -> * albedo -> Emission."""
    nt = mat.node_tree
    tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    while len(els) < len(steps):
        els.new(0.5)
    for el, (pos, val) in zip(els, steps):
        el.position = pos
        el.color = (val, val, val, 1.0)
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(diffuse.outputs["BSDF"], s2rgb.inputs["Shader"])
    nt.links.new(s2rgb.outputs["Color"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], mul.inputs["A"])
    nt.links.new(tex.outputs["Color"], mul.inputs["B"])
    nt.links.new(mul.outputs["Result"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])


def outline_material(name="M_Outline", rgba=(0.05, 0.04, 0.08, 1.0)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = rgba
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def add_outline(obj, mat, thickness=0.03):
    """Inverted-hull outline for icon renders only (add AFTER FBX export)."""
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Outline", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.material_offset = len(obj.data.materials) - 1


# ---------------------------------------------------------------- mesh
def canonicalize(bm):
    """bmesh primitive ops (e.g. create_uvsphere) emit faces in a run-dependent order in Blender 5.2.
    Sort verts by position and faces by vertex set so identical input -> identical FBX topology.
    BMElemSeq.sort() needs a numeric key, so sort in Python first and use the rank."""
    vkey = {v: (round(v.co.z, 5), round(v.co.y, 5), round(v.co.x, 5)) for v in bm.verts}
    vrank = {v: i for i, v in enumerate(sorted(bm.verts, key=vkey.get))}
    bm.verts.sort(key=vrank.get)
    bm.verts.index_update()
    fkey = {f: tuple(sorted(v.index for v in f.verts)) for f in bm.faces}
    frank = {f: i for i, f in enumerate(sorted(bm.faces, key=fkey.get))}
    bm.faces.sort(key=frank.get)
    bm.faces.index_update()


def mesh_object(name, bm, material, smooth=False):
    canonicalize(bm)
    me = bpy.data.meshes.new(name)
    for f in bm.faces:
        f.smooth = smooth
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def tri_count(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")


# ---------------------------------------------------------------- export
def _fbx_nodes(path):
    """Flatten a binary FBX 7.x into [(node path, props)], skipping CreationTime* nodes and int64 ('L')
    values: Blender derives object UIDs from memory addresses, so they change on every export."""
    import struct
    import zlib
    with open(path, "rb") as f:
        data = f.read()
    wide = struct.unpack_from("<I", data, 23)[0] >= 7500
    head_fmt, head_len, sentinel = ("<QQQ", 24, 25) if wide else ("<III", 12, 13)
    out = []

    def read_props(pos, count):
        vals = []
        for _ in range(count):
            t = data[pos:pos + 1]
            pos += 1
            if t in b"YCIFDL":
                fmt = {b"Y": "<h", b"C": "<?", b"I": "<i", b"F": "<f", b"D": "<d", b"L": "<q"}[t]
                if t != b"L":
                    vals.append(struct.unpack_from(fmt, data, pos)[0])
                pos += struct.calcsize(fmt)
            elif t in b"fdlibc":  # arrays, optionally zlib-compressed
                _, enc, clen = struct.unpack_from("<III", data, pos)
                raw = data[pos + 12:pos + 12 + clen]
                vals.append(zlib.decompress(raw) if enc == 1 else raw)
                pos += 12 + clen
            else:  # S (string) / R (raw)
                n = struct.unpack_from("<I", data, pos)[0]
                vals.append(data[pos + 4:pos + 4 + n])
                pos += 4 + n
        return vals

    def walk(pos, parent, limit):
        while pos < limit - sentinel:
            end, count, plen = struct.unpack_from(head_fmt, data, pos)
            if end == 0:
                break
            name_len = data[pos + head_len]
            name = data[pos + head_len + 1:pos + head_len + 1 + name_len].decode()
            props_at = pos + head_len + 1 + name_len
            node_path = parent + "/" + name
            if "CreationTime" not in node_path:
                out.append((node_path, read_props(props_at, count)))
            if props_at + plen < end:
                walk(props_at + plen, node_path, end)
            pos = end

    walk(27, "", len(data))
    return out


def fbx_same_content(a, b):
    """True when two FBX files differ only by creation timestamps / object UIDs."""
    return os.path.exists(a) and os.path.exists(b) and _fbx_nodes(a) == _fbx_nodes(b)


def export_fbx_if_changed(path, objects, **kwargs):
    """Export to a temp file and replace `path` only if the content changed (no git/import churn)."""
    # leading "." => Unity ignores the temp file even if the Editor refreshes mid-export
    tmp = os.path.join(os.path.dirname(os.path.abspath(path)), "." + os.path.basename(path))
    seconds = export_fbx(tmp, objects, **kwargs)
    if fbx_same_content(tmp, path):
        os.remove(tmp)
        return seconds, False
    os.replace(tmp, path)
    return seconds, True


def export_fbx(path, objects, bake_anim=False, blender_defaults=False):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    t = time.time()
    if blender_defaults:  # comparison only - do not use for game assets
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, bake_anim=bake_anim)
        return time.time() - t
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY", "ARMATURE"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",  # Unity: root scale (1,1,1), 1 Blender m = 1 Unity unit
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,  # bake the axis change into mesh data -> identity child rotations
        use_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",  # write smoothing groups; per-corner normals are always written
        use_tspace=False,  # Unity computes MikkTSpace tangents itself when a shader needs them
        colors_type="SRGB",  # only used when the mesh has a colour attribute
        use_triangles=False,
        add_leaf_bones=False,
        bake_anim=bake_anim,
        bake_anim_use_all_bones=False,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=1.0,
        path_mode="STRIP",  # no absolute texture paths from this machine inside the FBX
        embed_textures=False,
        use_custom_props=False,
        use_metadata=False,  # no exporter/version strings; timestamps + UIDs still change per export
    )
    return time.time() - t


# ---------------------------------------------------------------- icon render
def setup_icon_render(engine, size, target_objs, ortho=True, yaw_deg=20.0, pitch_deg=18.0, margin=1.1, pixel=False):
    scene = bpy.context.scene
    if pixel:
        scene.render.filter_size = 0.0  # no pixel filter -> hard alpha edges
    scene.render.engine = {"EEVEE": "BLENDER_EEVEE", "WORKBENCH": "BLENDER_WORKBENCH", "CYCLES": "CYCLES"}[engine]
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "Standard"  # AgX/Filmic would desaturate the palette
    scene.view_settings.look = "None"
    if engine == "CYCLES":
        scene.cycles.device = "CPU"
        scene.cycles.samples = 32
        scene.cycles.use_denoising = False
    elif engine == "EEVEE":
        scene.eevee.taa_render_samples = 1 if pixel else 16
    else:
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "TEXTURE"

    world = bpy.data.worlds.new("IconWorld")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.58, 0.7, 1.0)
    bg.inputs["Strength"].default_value = 0.35
    scene.world = world

    sun_data = bpy.data.lights.new("Key", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("Key", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    scene.collection.objects.link(sun)

    # frame the bounding box of the targets; matrix_world is stale until the view layer updates
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in target_objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center, radius = (lo + hi) / 2, (hi - lo).length / 2
    cam_data = bpy.data.cameras.new("IconCam")
    cam = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam)
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    # Blender front view looks along +Y, so the camera sits at -Y.
    direction = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = radius * 2 * margin
        dist = radius * 4.0
    else:
        cam_data.lens = 85  # long lens = little perspective distortion
        dist = radius * margin / math.sin(cam_data.angle / 2)
    cam.location = center + direction * dist
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_start = dist * 0.05
    cam_data.clip_end = dist * 4
    scene.camera = cam
    return cam


def render_png(path):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.context.scene.render.filepath = os.path.abspath(path)
    t = time.time()
    bpy.ops.render.render(write_still=True)
    return time.time() - t
