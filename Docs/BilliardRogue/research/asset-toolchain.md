# Offline Asset Toolchain (Blender 5.2 headless + Python venv + ffmpeg → Unity 6.3) — Billiard Rogue

Research note, 2026-09-28. Every command, script and number below was **run on this Mac** (Apple M4 Max, macOS 26.5.2)
unless tagged **[unverified]**. Unity import behaviour was verified in a throw-away Unity 6000.3.9f1 project in the
scratchpad (the Starter project was not touched; its Editor is open and holds the project lock).

Tested, copy-ready templates live next to this note:
`/Users/simonbut/project/VibeProject3/Docs/BilliardRogue/research/asset-toolchain-templates/` (Blender/, Textures/,
Audio/, Unity/). Preview images: `asset-toolchain-img/`. Copy the Python files to `Tools/…` and the C# files to
`Starter/Assets/Scripts/Editor/BilliardRogue/` when implementing (Unity mints the `.meta` files).

---

## 0. TL;DR decisions

| Topic | Decision (verified) |
|---|---|
| Blender invocation | `Blender -b --factory-startup --python-exit-code 1 --python X.py -- <args>`. Without `--python-exit-code`, a Python exception still exits **0**. |
| Model colouring | **One shared 16x16 palette texture** (`T_Palette16.png`), every face's UVs point at one texel **centre**. Point filter, no mips, **uncompressed** (ETC2 changes ~100% of palette texels, ASTC 4x4 99%). Vertex colours also import fine but stock URP / our ToonLit ignore them. |
| FBX export | `apply_scale_options="FBX_SCALE_ALL"`, `axis_forward="-Z"`, `axis_up="Y"`, `bake_space_transform=True`, `mesh_smooth_type="FACE"`, `add_leaf_bones=False`, `bake_anim_use_all_actions=False`, `bake_anim_use_nla_strips=False`, `path_mode="STRIP"`. Unity result: root scale 1, rotation 0, 1 Blender m = 1 Unity unit, Blender −Y (front) = Unity +Z. |
| Unity model import | `bakeAxisConversion=false` (true flips our models to face −Z), `useFileScale=true`, `globalScale=1`, `importNormals=Import`, `importTangents=None`, `materialImportMode=None`, `isReadable=false`, `meshCompression=Off`, `generateSecondaryUV=false`, `preserveHierarchy=true`, animation only for `*_Anim.fbx`. |
| Animation | **Animate rigid parts in Unity with DOTween.** FBX object-transform clips work (1 clip, 9 curves) but need a Generic Animator + controller; Blender's default export makes 9 clips for 3 objects. |
| Icons | EEVEE works headless on this Mac (Metal). 256² transparent PNG, ortho camera, cel ramp + inverted-hull outline: **0.1 s warm, 2.4–3.2 s cold** (shader compile). Workbench 0.07 s, Cycles CPU 32 spp 0.05 s. |
| Pixel textures | numpy/Pillow/scipy, all ops `mode="wrap"` → seamless. Normal map = Sobel on height, **OpenGL (+G up) = Unity convention**. Cavity **0.5 = neutral** (matches `BilliardRogue/ToonLit` `_CavityMap`). |
| Particles | One 128x128 atlas: 8 effects (rows) x 8 frames of 16 px, white/grey + hard alpha, tinted by ParticleSystem colour. Texture Sheet Animation Grid 8x8, Single Row, `rowMode=Custom`. |
| SFX | sfxr-style numpy synth → 44.1 kHz mono 16-bit WAV, peak −1 dBFS. Unity: ADPCM + Decompress On Load + Force Mono (~3.4x smaller). BGM: Vorbis q0.6, Streaming, Load In Background. |
| Import rules | One folder-scoped `AssetPostprocessor`. **Never bump `GetVersion()`** — Unity then reimports every texture/model/audio in the project incl. Packages (measured). Use the targeted "Reimport Generated Assets" menu instead. Adding the postprocessor the first time also triggers that one-time global reimport. |
| Determinism | PNG + WAV outputs are byte-identical across runs. FBX never is (timestamps + memory-derived UIDs), and `bmesh.ops.create_uvsphere` face order varies run to run → `canonicalize()` + `export_fbx_if_changed()`. |

---

## 1. Environment (verified)

| Tool | Path | Version |
|---|---|---|
| Blender | `/Applications/Blender.app/Contents/MacOS/Blender` | 5.2.2 LTS (embedded Python 3.13.13) |
| Python venv | `/Users/simonbut/project/VibeProject3/Tools/.venv/bin/python` | Python 3.13.7, numpy 2.5.3, Pillow 12.3.0, scipy 1.18.1, fontTools 4.66.0 |
| ffmpeg / ffprobe | `/opt/homebrew/bin/ffmpeg`, `/opt/homebrew/bin/ffprobe` | 8.0 (has libvorbis, libopus, libmp3lame, flac) |
| Unity | `/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/MacOS/Unity` | 6000.3.9f1; Android module installed |
| `.gitignore` | already ignores `Tools/.venv/` and `__pycache__/` | |

Starter project facts that matter for assets (read from `Starter/ProjectSettings/ProjectSettings.asset`):
- `m_ActiveColorSpace: 0` → **Gamma** colour space. sRGB import flags are still set correctly (for a future Linear switch) but do not change sampling now.
- Android graphics API `m_APIs: 0b000000` = OpenGLES3 only; `m_BuildTargetDefaultTextureCompressionFormat` Android = `02000000` (= ETC2 in `UnityEditor.TextureCompressionFormat`).
- `VertexChannelCompressionMask: 4054` (UV0 may be stored as fp16 → still exact enough for 1/32-texel palette UVs).
- The Starter Editor is normally **open** (`unity status` → `ready`, `Temp/UnityLockfile` exists). Batchmode (`-batchmode -projectPath Starter`) cannot open it concurrently → use `unity command …` (see `unity-cli-cookbook.md`).

---

## 2. Recommended repo layout

```
Tools/
  .venv/                          (gitignored)
  Shared/palette.json             generated by make_palette.py, read by Blender + texture scripts (commit)
  Blender/bl_common.py            shared helpers (palette UVs, canonicalize, FBX export, icon render)
  Blender/model_<name>.py         one script per model (model_slime.py = reference)
  Blender/build_models.py         runs every model_*.py headless in parallel
  Textures/pixeltex.py            noise, normal-from-height, cavity, quantize, dither, save
  Textures/make_palette.py        16x16 palette PNG + palette.json
  Textures/make_<texture>.py      e.g. make_stone_brick.py
  Textures/make_particle_sheet.py 128x128 flipbook atlas
  Textures/preview.py             nearest-neighbour upscale for visual checks
  Audio/sfxsynth.py               synth engine
  Audio/make_sfx.py               presets -> WAV
Starter/Assets/
  Models/BilliardRogue/<Name>.fbx               (+ <Name>_Anim.fbx only if an FBX clip is really needed)
  Textures/BilliardRogue/T_Palette16.png
  Textures/BilliardRogue/T_<Name>_{Albedo,Normal,Cavity,Height}.png
  Textures/BilliardRogue/Vfx/T_PixelParticles.png   (material texture, NOT a Sprite)
  Sprites/BilliardRogue/Icons/Icon_<Name>.png       (UI, PPU 100, bilinear, ASTC 4x4)
  Sprites/BilliardRogue/World/<name>.png            (pixel billboards, PPU 32, point)
  Audio/Sfx/BilliardRogue/SFX_BR_<Name>[_<n>].wav
  Audio/Bgm/BilliardRogue/BGM_BR_<Name>.ogg
  Materials/BilliardRogue/M_Palette.mat             (created by editor script / CLI, never by hand)
  Prefabs/BilliardRogue/Models/<Name>.prefab        (prefab variants of the FBX, built by editor script)
  Scripts/Editor/BilliardRogue/BilliardRogue{AssetPostprocessor,ModelPrefabBuilder,Reimport,ImportAudit}.cs
```
Starter has no asmdef under `Assets/Scripts` → editor scripts compile into Assembly-CSharp-Editor; namespace
`Nex.BilliardRogue.Editor` (rule: `Nex.*.Editor`). Do not commit `.blend` files; everything is regenerated from scripts.

---

## 3. End-to-end build (commands as verified)

```bash
PY=/Users/simonbut/project/VibeProject3/Tools/.venv/bin/python
R=/Users/simonbut/project/VibeProject3
A=$R/Starter/Assets
# 1. palette (PNG goes straight into Assets, JSON into Tools/Shared)
$PY $R/Tools/Textures/make_palette.py $A/Textures/BilliardRogue/T_Palette16.png $R/Tools/Shared/palette.json
# 2. textures + particle sheet
$PY $R/Tools/Textures/make_stone_brick.py --palette-json $R/Tools/Shared/palette.json --out-dir $A/Textures/BilliardRogue
$PY $R/Tools/Textures/make_particle_sheet.py --out $A/Textures/BilliardRogue/Vfx/T_PixelParticles.png
# 3. SFX (3 variants each -> SfxManager picks randomly)
$PY $R/Tools/Audio/make_sfx.py --out-dir $A/Audio/Sfx/BilliardRogue --variants 3
# 4. models + icons (parallel Blender processes)
$PY $R/Tools/Blender/build_models.py --unity-assets $A --palette-json $R/Tools/Shared/palette.json \
    --palette-png $A/Textures/BilliardRogue/T_Palette16.png --jobs 4
# 5. let the OPEN Editor import (an unfocused Editor does not notice new files by itself)
unity command recompile --project-path $R/Starter          # = AssetDatabase.Refresh() + compile tracking
unity command menu --path "Nex/Billiard Rogue/Build Model Prefabs" --project-path $R/Starter
unity command menu --path "Nex/Billiard Rogue/Log Import Audit" --project-path $R/Starter   # then read `console`
```
Python scripts can run from any cwd (Python puts the script's folder on `sys.path`, so `import pixeltex` / `import sfxsynth` work; `bl_common` is added explicitly).
With the Editor closed, the same Editor code runs in batchmode:
`Unity -batchmode -nographics -projectPath <proj> [-buildTarget Android] -executeMethod Nex.BilliardRogue.Editor.BilliardRogueModelPrefabBuilder.BuildAll -quit -logFile <log>`.

### Measured timings / sizes (warm unless noted)

| Step | Time | Output |
|---|---|---|
| Blender process start + slime build + FBX + EEVEE icon | 0.6–0.8 s wall | FBX 31 KB, icon 34 KB |
| — FBX export only | 0.03 s | 324 tris, 3 objects |
| — EEVEE 256² icon, cold (first process / parallel cold) | 2.4–3.2 s | shader compilation; cached afterwards |
| `build_models.py` 4 models, `--jobs 4`, cold | 3.9 s total | |
| make_palette / make_stone_brick / make_particle_sheet | ~0.2 s each (script body 0.01 s) | 568 B / 4 maps 0.6–1.9 KB / 2.3 KB |
| make_sfx 14 presets x 3 variants | 0.4–0.7 s | 42 WAV, 1.15 MB |
| **First** venv run after install (numpy/scipy first import) | 5–11 s | one-off |
| ffmpeg 20 s stereo BGM → Ogg Vorbis q5 + loudnorm | 0.7 s | 341 KB |
| Unity: create empty project (batchmode) | 7.3 s | |
| Unity: batch import of all test assets + report | 4.5 s (8.7 s with `-buildTarget Android`) | FBX import ≈ 7 ms each |

---

## 4. Blender headless

### 4.1 Invocation contract
```bash
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
    --python Tools/Blender/model_slime.py -- \
    --palette-json Tools/Shared/palette.json --palette-png Starter/Assets/Textures/BilliardRogue/T_Palette16.png \
    --fbx Starter/Assets/Models/BilliardRogue/Slime.fbx --icon Starter/Assets/Sprites/BilliardRogue/Icons/Icon_Slime.png
```
- `-b` background, `--factory-startup` ignores user prefs/add-ons (reproducible). Everything after `--` is yours:
  `argv = sys.argv[sys.argv.index("--") + 1:]` (`bl_common.parse_args`).
- `--python-exit-code 1` is mandatory in automation (verified: exception without it → exit 0; with `--python-exit-code 3` → exit 3).
- Scripts print one machine-readable line `ASSET_STATS {...}` (tris, fbx_s, fbx_changed, render_s, sizes) — `build_models.py` greps it.
- `bl_common.reset_scene()` = `bpy.ops.wm.read_factory_settings(use_empty=True)` + metric units, scale 1.
- Render engine ids in 5.2: `"BLENDER_EEVEE"`, `"BLENDER_WORKBENCH"`, `"CYCLES"` (all three rendered headless).

### 4.2 Blender 5.2 API gotchas hit while building the slime
| Symptom | Cause | Fix (in templates) |
|---|---|---|
| Magenta icon, log `gpu.texture | ERROR Failed to create GPU texture from Blender image` | `bpy.data.images.load("rel/path.png")` | `images.load(os.path.abspath(p), check_existing=True)` |
| FBX topology differs between identical runs (2 of 3 runs matched) | `bmesh.ops.create_uvsphere` emits faces in a run-dependent order | `canonicalize(bm)`: sort verts by rounded position, faces by sorted vertex indices |
| `ValueError: the value returned by the 'key' function is not a number` | `BMElemSeq.sort(key=…)` needs a numeric key | sort in Python, pass `rank.get` |
| Face shades/glints wrong after deforming | `BMFace.normal` not refreshed after moving verts | `bm.normal_update()` after deformation |
| Icon framed far too wide | `obj.matrix_world` stale after setting transforms/parents | `bpy.context.view_layer.update()` before reading `matrix_world` / `bound_box` |
| Washed-out palette in renders | default view transform AgX | `scene.view_settings.view_transform = "Standard"` |

### 4.3 Low-poly model with bmesh (pattern from `model_slime.py`)
```python
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=8, radius=1.0)   # 84 quads + 28 tris = 196 tris
for v in bm.verts:                         # sculpt by formula: wide bottom, flat underside, tip on top
    x, y, z = v.co; t = (z + 1) / 2; radial = 1.12 - 0.42 * t * t
    if z < -0.55: z = -0.55 - (z + 0.55) * 0.12
    if t > 0.97: z += 0.28
    v.co = Vector((x * radial * 0.5, y * radial * 0.5, (z + 0.57) * 0.42))   # origin = bottom centre (pivot)
bm.normal_update()
for f in bm.faces:                         # colour = palette texel per face
    shade = 7 if f.calc_center_median().z < 0.08 else 9 if f.calc_center_median().z < 0.3 else 11
    bc.paint_faces(bm, [f], "sky", shade)
body = bc.mesh_object("Body", bm, mat, smooth=False)   # canonicalize + per-face smooth flag + link to scene
```
- Second part (eyes): separate objects placed with `BVHTree.FromObject(body, depsgraph).ray_cast(origin, dir)` from the
  front (−Y), parented to Body. **Bake rotation + non-uniform scale into the mesh** (`eye.data.transform(orient @ Matrix.Diagonal(...))`)
  and keep only location on the object → Unity children have identity rotation/scale and the object origin is the
  pivot, so a blink is `eye.DOScaleY(...)` (verified identity transforms after import).
- Result: Body 196 tris (361 Unity verts, flat), each eye 64 tris (144 verts) → 324 tris total.
- Flat vs smooth: `mesh_object(..., smooth=False)` writes per-face normals; `smooth=True` for organic shapes that should
  get clean curved toon bands. Unity `importNormals=Import` keeps whatever Blender wrote (verified: eyes/body
  face-normal dot = 1.0000 / 0.9945 — the 0.9945 is non-planar jittered quads sharing one quad normal).
  Alternative verified: `importNormals=Calculate`, `normalSmoothingSource=FromAngle`, `normalSmoothingAngle=0` → also flat (351 verts).
- `use_triangles=False` lets Unity triangulate quads; set `True` if a non-planar quad's diagonal must match Blender.

### 4.4 Palette atlas vs vertex colours (evaluated)

| | Palette texture (chosen) | Vertex colours |
|---|---|---|
| Unity import | UV0 at texel centres, e.g. `(0.7188, 0.5313)` (verified) | `Color` attribute `UNorm8` (verified with `colors_type="SRGB"`) |
| Works with stock URP Lit/SimpleLit/Unlit and `BilliardRogue/ToonLit` (`_BaseMap * _BaseColor`) | yes, zero shader work | **no** — none of them read vertex colour |
| Recolour (elite enemy, P1/P2 tint, damage flash) | swap `_BaseMap` via a second material (`M_Palette_Elite`) — same mesh | needs a shader param path |
| Cost | 16x16 RGB24 = 1.7 KB GPU, one extra sample | +4 B/vertex, FBX +1.6 KB |
| Batching | every prop shares one material → SRP Batcher friendly | same |
| Filtering robustness | UV at texel centre + all 3 corners equal → exact colour with point **or** bilinear, mips irrelevant (zero derivatives) | n/a |

UV mapping (image row 0 is the TOP of the PNG): `u = (shade + 0.5) / 16`, `v = 1 − (row + 0.5) / 16`
(`bl_common.palette_uv`). Palette layout (`make_palette.py`): 16 rows = colour families
`red orange yellow lime green teal cyan sky blue indigo purple magenta pink brown skin gray`, 16 columns = shade
0 (dark) → 15 (light), hue-shifted ramps (cool shadows, warm highlights); the gray row ends in near-black (12,12,16) and white.
`palette.json` = `{"size":16,"families":{"sky":{"row":7,"hex":[16 × "#rrggbb"]}, …}}`.
Do not use palette-swap via `MaterialPropertyBlock` on many renderers — it drops them out of the SRP Batcher; use material variants.

![palette](asset-toolchain-img/palette16_preview.png)

---

## 5. FBX export for Unity

### 5.1 The call (`bl_common.export_fbx`, all names verified against the 5.2 operator RNA)
```python
bpy.ops.export_scene.fbx(
    filepath=path, use_selection=True, object_types={"MESH", "EMPTY", "ARMATURE"},
    global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, use_space_transform=True,
    use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False, colors_type="SRGB",
    use_triangles=False, add_leaf_bones=False,
    bake_anim=bake_anim, bake_anim_use_all_bones=False, bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
    bake_anim_step=1.0, bake_anim_simplify_factor=1.0,
    path_mode="STRIP", embed_textures=False, use_custom_props=False, use_metadata=False)
```
Operator defaults to be aware of: `apply_scale_options="FBX_SCALE_NONE"`, `bake_space_transform=False`,
`mesh_smooth_type="OFF"`, `add_leaf_bones=True`, `bake_anim=True`, `bake_anim_use_all_actions=True`, `bake_anim_use_nla_strips=True`, `path_mode="AUTO"`.

### 5.2 What Unity imports (verified in 6000.3.9f1)

| Export / import combo | Root transform | Body mesh size | Front | Clips |
|---|---|---|---|---|
| **Ours** + `bakeAxisConversion=false` | pos 0, rot (0,0,0), scale 1, `fileScale=1` | (1.000, 0.761, 1.007) m, pivot at bottom | eyes at z=+0.445 → **faces +Z** | 1 (`Scene`, 1.0 s, 24 fps, 9 scale curves) |
| Ours + `bakeAxisConversion=true` | rot 0 | same | eyes at z=−0.445 → **faces −Z (wrong)** | — |
| Blender defaults | **rot (270,0,0), scale 100**, `fileScale=0.01` | 0.01 (cm mesh) | — | **9** (`Body|BodyAction`, `EyeL|BodyAction`, … every action x object) |

Axis map (ours): Blender (x, y, z) → Unity (−x, z, −y). A character modelled facing Blender −Y (front view) faces
Unity +Z; its right hand (Blender −X) is Unity +X — no mirroring. Model with the origin at the feet (bottom centre).

Hierarchy: with `preserveHierarchy=true` → `Slime` (empty root, named after the file) / `Body` (mesh) / `EyeL`, `EyeR`.
With `false` the single top-level object is merged into the root (`Slime` carries Body's MeshFilter) — then scaling the
body for squash also scales the root and any colliders on it. Imported objects get fileIDs derived from node names/paths
(`internalIDToNameTable: []`, hash-like fileIDs in the variant YAML) **[inferred]**: keep object names stable across
re-exports or prefab overrides on renamed nodes are lost.

### 5.3 Deterministic output / no churn
- Binary FBX always differs per export: `CreationTimeStamp` + object UIDs derived from memory addresses (verified by
  parsing both files; all geometry arrays identical after `canonicalize`).
- `bl_common.export_fbx_if_changed(path, objects, **kw)` exports to a hidden sibling `.<name>.fbx` (Unity ignores
  dot-files even if it refreshes mid-export), parses both FBX files (`_fbx_nodes`, ignores `CreationTime*` nodes and
  int64 `L` values = UIDs) and replaces the target only if content changed. Verified: 4 identical re-runs →
  `fbx_changed: False`; a 2% geometry tweak → `True`.
- PNG (Pillow `optimize=True`) and WAV outputs are byte-identical across runs (verified with `cmp`).

---

## 6. Animation: FBX clips vs DOTween

Verified: `--anim` keyframes Body scale (squash/stretch) + eye blink; with our export flags Unity imports **one**
clip named `Scene` (after the Blender scene; renaming the scene should rename it **[unverified]**), 1.000 s, 9 curves,
`Keyframe reduction: Ratio 32%`, `animationType=Generic` → needs an Animator + AnimatorController per prefab.

**Recommendation: no FBX animation for rigid parts.** Author pivots in Blender (object origins: Body at feet, eyes at
eye centres, identity rotation/scale), and animate in Unity with DOTween (`team.nex.as-dotween`, `DG.Tweening`),
which Starter already uses with UniTask (`await tween.WithCancellation(token)` in `BgmManager`). Only use `_Anim.fbx`
for complex authored motion (e.g. a boss) — the postprocessor enables animation import for that suffix only.

```csharp
#nullable enable
using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    // Sketch - standard DOTween API, not compiled in this research (no DOTween in the scratch project).
    public class EnemyModelTweens : MonoBehaviour
    {
        [SerializeField] Transform body = null!;      // "Body" child of the model prefab (pivot at the feet)
        [SerializeField] Transform[] eyes = null!;    // identity-rotation children, pivot at eye centre

        public void PlayIdle() =>
            body.DOScale(new Vector3(1.06f, 0.94f, 1.06f), 0.45f).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);

        public void PlayHit()
        {
            body.DOComplete();
            body.DOPunchScale(new Vector3(0.25f, -0.25f, 0.25f), 0.3f, 8, 0.6f).SetLink(gameObject);
        }

        public void Blink()
        {
            foreach (var eye in eyes) eye.DOScaleY(0.1f, 0.06f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject);
        }
    }
}
```

---

## 7. Icon rendering (headless)

![engines](asset-toolchain-img/engines_eevee_workbench_cycles.png)
Left → right: EEVEE (cel ramp + outline), Workbench (studio light, `color_type="TEXTURE"`), Cycles CPU 32 spp.

| Engine | Headless on this Mac | 256² render | Notes |
|---|---|---|---|
| EEVEE (`BLENDER_EEVEE`) | yes (Metal GPU, no window) | 0.10–0.16 s warm, 2.4–3.2 s cold | supports `ShaderNodeShaderToRGB` → true cel look matching the game |
| Workbench | yes | 0.07 s | no custom shading; quick previews |
| Cycles CPU | yes | 0.05 s (32 spp, no denoise) | physically lit; no Shader-to-RGB |

`bl_common.setup_icon_render(engine, size, objs, ortho=True, yaw_deg=20, pitch_deg=18, margin=1.1, pixel=False)`:
transparent film (`film_transparent=True`, RGBA 8-bit PNG), Standard view transform, sun (energy 3) + flat world
(0.35), camera frames the bounding sphere (ortho: `ortho_scale = 2·r·margin`; perspective: 85 mm,
`dist = r·margin / sin(angle/2)`). `make_toon(mat)` = Diffuse → Shader to RGB → constant ColorRamp
(0.6 / 0.82 / 1.0) × palette colour → Emission. `add_outline(obj, mat, 0.012)` = Solidify (flip normals, offset 1,
material offset → back-face-culled dark emission) — added **after** FBX export so it never reaches Unity.

Pixel-art variant (verified, 2 alpha levels): `--icon-size 64 --pixel` (`filter_size=0`, 1 TAA sample) then
`ffmpeg -i in.png -vf "scale=iw*4:ih*4:flags=neighbor" out.png` (0.4 s).

![icon](asset-toolchain-img/slime_icon_eevee.png) ![pixel icon](asset-toolchain-img/slime_icon_pixel64x4.png)

---

## 8. Procedural pixel textures (Python venv)

### 8.1 Helpers (`Textures/pixeltex.py`)
```python
def value_noise(size, cells, rng):                    # tileable: 3x3 tiled lattice + bicubic zoom, crop centre
def normal_from_height(h, strength=2.0):
    dx = ndimage.sobel(h, axis=1, mode="wrap") / 8.0
    dy = ndimage.sobel(h, axis=0, mode="wrap") / 8.0
    n = np.dstack([-dx * strength, dy * strength, np.ones_like(h)])   # +dy: image rows grow downward
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).round().astype(np.uint8)          # OpenGL / Unity convention (+G = up)
def cavity_from_height(h, radius=1.0, gain=1.2):
    blur = ndimage.gaussian_filter(h, radius, mode="wrap")
    return np.clip(0.5 + (h - blur) * gain, 0, 1)                    # 0.5 neutral, <0.5 crevice, >0.5 edge
def quantize_to_ramp(values01, ramp)                  # keep albedo on-palette (ramp = palette row, shades lo..hi)
def ordered_dither(h, w)                              # 4x4 Bayer, -0.5..0.5
def save_rgb / save_gray / save_rgba(path, arr)       # Pillow, optimize=True
```
Normal-map convention check (visual): brick top edges are cyan (+G), bottom edges magenta, left edges blue (−R),
right edges pink (+R) → correct for Unity's `NormalMap` import on GLES (URP unpacks RGB when `UNITY_NO_DXT5nm`).

### 8.2 32x32 stone brick (`make_stone_brick.py`)
Running-bond layout (bricks 16x8, 1 px mortar, alternate rows offset 8, wraps), taxicab distance to mortar → 2 px
bevel, value-noise + random chips → **height**; albedo = per-brick tone + noise + top-left light from height gradient
+ Bayer dither, quantized to palette `gray` shades 3–10; normal strength 3.
Outputs `T_StoneBrick_Albedo.png` (730 B), `_Normal` (1,859 B), `_Cavity` (807 B), `_Height` (577 B). 2x2 tiling check:

![brick](asset-toolchain-img/stone_brick_albedo_normal_cavity_height.png)

Texel density suggestion: 32 px per 1 m tile. With the HD-2D note's low-res world RT (640x360), 1 texel ≈ 1 RT pixel
when the camera shows ~20 m across **[depends on final camera; verify in scene]**. Pack cavity/height into one RG
texture only if the shader wants it; separate R8 maps are 2 KB each.

### 8.3 Pixel particle flipbook (`make_particle_sheet.py`)
128x128 RGBA, cell 16 px, 8 frames per row. Rows: `0 spark, 1 dust, 2 leaf, 3 snowflake, 4 orb, 5 smoke, 6 ring, 7 twinkle`.
Looping rows (dust, leaf, snowflake, orb) use `t = f/8`; one-shots (spark, smoke, ring, twinkle) `t = f/7`.
White/grey value + binary alpha (smoke uses Bayer-dithered binary alpha); RGB zeroed where alpha = 0.
Tint with `ParticleSystem.main.startColor` / colour-over-lifetime (HDR > 1 feeds bloom). 2,355 B PNG, 64 KB RGBA32 in memory.

![particles](asset-toolchain-img/particle_sheet_preview.png)

Particle system setup (API compiled + executed in Unity 6000.3.9f1 — logged `mode=Grid tiles=8x8 anim=SingleRow rowMode=Custom row=3 cycles=2 timeMode=Lifetime`):
```csharp
var tsa = ps.textureSheetAnimation;
tsa.enabled = true;
tsa.mode = ParticleSystemAnimationMode.Grid;
tsa.numTilesX = 8; tsa.numTilesY = 8;
tsa.animation = ParticleSystemAnimationType.SingleRow;
tsa.rowMode = ParticleSystemAnimationRowMode.Custom;
tsa.rowIndex = 3;                                   // 3 = snowflake
tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
tsa.cycleCount = 2;
```
Material: `BilliardRogue/LitPixelParticle` from `urp-hd2d-rendering.md` §7.3 (UVs come from the sheet module), or stock
`Universal Render Pipeline/Particles/Unlit` (`_BaseMap`). If a script changes `_Surface`/`_Blend` on a stock URP
material, call `UnityEditor.BaseShaderGUI.SetupMaterialBlendMode(material)` /
`UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords(material)` (public static, found in URP 17.3 source; **not executed**).

---

## 9. Retro SFX synthesis (`Audio/sfxsynth.py`, `Audio/make_sfx.py`)

Engine (numpy, `SR = 44100`): a sound = list of `Voice` layers mixed. Condensed API (exact code in the template;
`arp` is `field(default_factory=list)` there):
```python
@dataclass
class Voice:
    wave: str = "square"   # square | saw | triangle | sine | noise | pnoise (93-step NES-style periodic noise)
    freq: float = 440.0; slide: float = 0.0; slide_accel: float = 0.0   # octaves/s, octaves/s^2
    min_freq: float = 20.0; duty: float = 0.5; duty_sweep: float = 0.0
    vib_depth: float = 0.0; vib_speed: float = 0.0                         # semitones, Hz
    arp: list = []          # [(time_s, semitone_jump), ...] cumulative
    attack: float = 0.005; sustain: float = 0.05; punch: float = 0.0; decay: float = 0.15   # sfxr envelope
    volume: float = 1.0; lowpass: float = 0.0; highpass: float = 0.0   # one-pole, Hz (0 = off)
    start: float = 0.0      # delay inside the sound
    crush_bits: int = 0; crush_rate: int = 0   # bit depth / sample-and-hold rate (Hz)
render(voices, seed=0, peak_db=-1.0, tail_fade_ms=5.0) -> np.ndarray   # mix, remove DC, fade tail, normalise peak
write_wav(path, samples)                                                 # stdlib wave, mono, 16-bit PCM
```
Phase-accumulator oscillators (`phase = cumsum(f/SR)`), so slides/vibrato/arpeggios are click-free. Noise picks a new
random value per oscillator cycle, so `freq` sets the noise colour (sfxr behaviour). Oscillators are naive
(no band-limiting) → audible aliasing on high square slides (visible as "X" folds in the spectrogram) = chiptune
character; oversample if a clean tone is needed.

Presets (`make_sfx.py`, names `SFX_BR_<Name>[_<n>].wav`; `--variants N` shifts pitch ×[1, 1.06, 0.94, 1.12, 0.89] + seed):

| Name | Dur | WAV | Unity ADPCM | Recipe |
|---|---|---|---|---|
| BallShoot | 0.15 s | 13.3 KB | 3.8 KB | square 520 Hz slide +2.5 oct/s duty sweep + high-passed noise burst |
| BallHitWall | 0.065 s | 5.8 KB | 1.7 KB | square 880 Hz slide −1, 6-bit crush |
| BallHitEnemy | 0.12 s | 10.6 KB | 3.1 KB | square 330 Hz slide −3 punch + low-passed noise |
| BallReturn | 0.093 s | 8.2 KB | 2.4 KB | sine + triangle rising blip |
| EnemyDie | 0.51 s | 45 KB | 12.8 KB | noise slide −2.2, 11 kHz sample-hold + falling square |
| Coin / Combo | 0.28 s | 24.7 KB | 7.1 KB | square arpeggio (+5) / (+4 +3 +5, 5-bit) |
| PowerUp | 0.45 s | 39.7 KB | 11.3 KB | square + triangle rising slide with vibrato |
| PlayerHurt | 0.27 s | 23.9 KB | 6.8 KB | square falling + crushed noise |
| BossRoar | 1.15 s | 101 KB | 28.7 KB | saw 90 Hz vibrato, low-pass, 7-bit + noise |
| TurnStart | 0.59 s | 51.6 KB | 14.7 KB | triangle + thin square major arpeggio |
| UiMove / UiConfirm / UiBack | 0.04 / 0.2 / 0.16 s | 3.7 / 17.7 / 14.2 KB | 1.2 / 5.1 / 4.1 KB | tick / up-fifth / down-fourth |

Checks run: `ffprobe -v error -show_entries stream=codec_name,sample_rate,channels,bits_per_sample,duration -of compact f.wav`
→ `pcm_s16le|44100|1|16`; `ffmpeg -i f.wav -af volumedetect -f null -` → `max_volume: -1.0 dB`.
Visual check: `ffmpeg -f concat -safe 0 -i list.txt -filter_complex "showspectrumpic=s=1400x300:legend=0:scale=log" spec.png`
(and `showwavespic=s=1400x200`).

### 9.1 ffmpeg recipes (verified)
```bash
# BGM: any source -> stereo 44.1 kHz Ogg Vorbis at -16 LUFS (Unity re-encodes; keep q5+ to limit double-lossy loss)
ffmpeg -y -i in.wav -af "aformat=channel_layouts=stereo,loudnorm=I=-16:TP=-1.5:LRA=11" -ar 44100 -c:a libvorbis -q:a 5 BGM_BR_X.ogg
# loudnorm upsamples internally -> always pass -ar 44100.   Measure: -af ebur128=peak=true -f null -
# concatenate clips for review
for f in SFX_BR_*.wav; do echo "file '$PWD/$f'"; done > list.txt; ffmpeg -f concat -safe 0 -i list.txt -c:a pcm_s16le all.wav
# nearest-neighbour upscale for pixel-art previews
ffmpeg -i icon64.png -vf "scale=iw*4:ih*4:flags=neighbor" icon256.png
```
Extra ideas **[not run]**: `silenceremove=start_periods=1:start_threshold=-60dB` to trim; `aresample=22050` for smaller retro SFX.

---

## 10. Unity import settings (AssetPostprocessor)

Template: `asset-toolchain-templates/Unity/BilliardRogueAssetPostprocessor.cs` (compiled and exercised in 6000.3.9f1,
Standalone and Android targets). Folder-scoped by `assetPath.StartsWith(...)`; settings re-applied on every import.

### 10.1 Rules

| Asset (folder / name) | Settings |
|---|---|
| **Models** `Assets/Models/BilliardRogue/` | `globalScale=1`, `useFileScale=true`, `bakeAxisConversion=false`, `importBlendShapes/Visibility/Cameras/Lights=false`, `preserveHierarchy=true`, `sortHierarchyByName=true`, `meshCompression=Off`, `isReadable=false`, `optimizeMeshPolygons/Vertices=true`, `addCollider=false`, `keepQuads=false`, `weldVertices=true`, `indexFormat=Auto`, `generateSecondaryUV=false`, `importNormals=Import`, `importTangents=None`, `materialImportMode=None`, `animationType=None` + `importAnimation=false` (`*_Anim`: `Generic`, `true`, `animationCompression=Optimal`, `resampleCurves=true`), `importConstraints=false`, `importAnimatedCustomProperties=false` |
| **Pixel textures** `Assets/Textures/BilliardRogue/**` | `textureType`: `NormalMap` (`*_Normal`), `SingleChannel` + `singleChannelComponent=Red` (`*_Cavity/_Height/_Mask` → R8), else `Default`; `sRGBTexture` true only for colour; `mipmapEnabled=false`, `filterMode=Point`, `anisoLevel=0`, `wrapMode=Repeat` (Clamp for `/Vfx/` and `*Palette*`), `alphaIsTransparency` for clamp ones, `npotScale=None`, `isReadable=false`, `streamingMipmaps=false` |
| **Sprites** `Assets/Sprites/BilliardRogue/**` | `Sprite`, `Single`, PPU 100 + Bilinear (UI icons) or PPU 32 + Point (`/World/`), no mips, Clamp, `alphaIsTransparency`, `spriteMeshType=FullRect`, `spriteGenerateFallbackPhysicsShape=false` |
| Texture compression (all above) | `GetSourceTextureWidthAndHeight` → `maxTextureSize = NextPowerOfTwo` (32..2048). Palette, normal maps and anything ≤128 px: `Uncompressed` + Android override `format=Automatic` (→ RGB24 / RGBA32 / R8). Larger: `CompressedHQ` + Android **ASTC_4x4** |
| **SFX** `Assets/Audio/Sfx/BilliardRogue/` | `forceToMono=true`, `loadInBackground=false`, `ambisonic=false`; sample settings `loadType=DecompressOnLoad`, `compressionFormat=ADPCM`, `preloadAudioData=true`, `sampleRateSetting=PreserveSampleRate` |
| **BGM** `Assets/Audio/Bgm/BilliardRogue/` | `forceToMono=false`, `loadInBackground=true`; `loadType=Streaming`, `compressionFormat=Vorbis`, `quality=0.6`, `preloadAudioData=false` |

Unity 6 API notes (compile-verified): `preloadAudioData` lives on `AudioImporterSampleSettings` (the meta shows it inside
`defaultSettings`); `TextureImporter.GetSourceTextureWidthAndHeight(out int, out int)` is public and valid inside
`OnPreprocessTexture`; `spriteMeshType` / `singleChannelComponent` are set through `TextureImporterSettings`
(`ReadTextureSettings` → modify → `SetTextureSettings`). There is no public `AudioImporter.normalize`; it defaults on
(`normalize: 1` in metas) and only matters with Force To Mono — our SFX are already peak-normalised.

### 10.2 Core of the postprocessor
```csharp
public class BilliardRogueAssetPostprocessor : AssetPostprocessor
{
    const string ModelsRoot = "Assets/Models/BilliardRogue/";   // + TexturesRoot, SpritesRoot, SfxRoot, BgmRoot
    public override uint GetVersion() => 1;                     // never bump (see 10.4)

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelsRoot)) return;
        var importer = (ModelImporter)assetImporter;
        importer.bakeAxisConversion = false;
        importer.preserveHierarchy = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        // ... full list in the template
    }

    void ApplyCompression(TextureImporter importer, bool forceUncompressed)
    {
        importer.GetSourceTextureWidthAndHeight(out var width, out var height);
        var longest = Mathf.Max(width, height);
        var uncompressed = forceUncompressed || longest <= UncompressedMaxSize;   // 128
        importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(longest), 32, 2048);
        importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = importer.maxTextureSize;
        android.textureCompression = importer.textureCompression;
        android.format = uncompressed ? TextureImporterFormat.Automatic : TextureImporterFormat.ASTC_4x4;
        importer.SetPlatformTextureSettings(android);
    }

    static void ConfigureSfx(AudioImporter importer)
    {
        importer.forceToMono = true;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.ADPCM;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }
}
```

### 10.3 Verified import results (`-buildTarget Android`)

| Asset | Imported as | Runtime memory |
|---|---|---|
| T_Palette16 (16²) | RGB24, 1 mip, Point, Clamp | 1,728 B |
| T_StoneBrick_Albedo (32²) | RGB24, Point, Repeat | 4,032 B |
| T_StoneBrick_Normal | NormalMap, RGB24 on Android (RGBA32 on Standalone) | 4,032 B |
| T_StoneBrick_Cavity / Height | SingleChannel **R8** | 1,984 B |
| Vfx/T_PixelParticles (128²) | RGBA32, Point, Clamp | 66,496 B |
| Icons/Icon_Slime (256²) | Sprite, **ASTC_4x4**, Bilinear | 66,496 B (vs 256 KB RGBA32) |
| SFX_BR_* | mono 44.1 kHz, DecompressOnLoad, ADPCM | ~29% of WAV size |
| BGM_BR_Test.ogg (20 s stereo) | Streaming Vorbis q0.6 | 160 KB compressed (source 341 KB) |

Compression quality measured with `EditorUtility.CompressTexture(tex, fmt, TextureCompressionQuality.Best)` + decode:

| Texture | ASTC 4x4 | ASTC 6x6 | ETC2 RGBA8 |
|---|---|---|---|
| Palette 16² | 35.2 dB, 99% texels changed | 28.0 dB | 28.4 dB, 100% changed |
| Brick albedo 32² | 53.9 dB | 38.3 dB | 37.1 dB |
| Brick normal 32² | 33.3 dB | 27.3 dB | 24.3 dB |
| Particle sheet 128² | 57.7 dB, 1.1% px changed | 38.5 dB | 34.4 dB, 11.8% changed |
| Slime icon 256² | 50.8 dB | 43.4 dB | 45.5 dB |

![compression](asset-toolchain-img/compression_src_astc4x4_etc2.png)
(rows: particles, brick, palette; columns: source, ASTC 4x4, ETC2 — ETC2 visibly merges palette cells and dents the dust mote.)

### 10.4 Reimport cost — measured, important
- Bumping `GetVersion()` reimported **every** texture, model and audio clip in the project (incl. `Packages/…` icons
  and an unrelated `Assets/Other/*.png/.wav`), not just the rule folders.
- **Adding** a new texture postprocessor class did the same (all textures reimported). Removing it again reimported
  nothing (cached artifacts reused).
- Starter has ~3,900 PNG + ~160 other images, 130 FBX, 329 audio files (Assets + PackageCache) → the first compile of
  `BilliardRogueAssetPostprocessor` in Starter will trigger a one-time full reimport of those **[duration not measured;
  expect minutes]**. Land it once, early, when the Editor can be left alone.
- Editing rule code without a version bump reimports nothing. To apply changed rules to our assets only, run
  `Nex/Billiard Rogue/Reimport Generated Assets` (`BilliardRogueReimport.ReimportAll`: `AssetDatabase.Refresh()` then
  `AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate)` per root).
  Verified: only the five BilliardRogue folders reimported and the new rule took effect (particle sheet switched
  RGBA32 → ASTC_4x4 → back).

---

## 11. Editor tools (templates, compiled + run in batchmode)

| File | Entry point | What it does |
|---|---|---|
| `BilliardRogueAssetPostprocessor.cs` | automatic | rules of §10 |
| `BilliardRogueModelPrefabBuilder.cs` | `[MenuItem("Nex/Billiard Rogue/Build Model Prefabs")] BuildAll()` | For every FBX in `Assets/Models/BilliardRogue`: new → `PrefabUtility.InstantiatePrefab(model)` + assign `M_Palette` to all MeshRenderers + `SaveAsPrefabAsset` (**creates a Prefab Variant of the FBX**, verified `GetPrefabAssetType == Variant`); existing → `LoadPrefabContents` / reassign / `SaveAsPrefabAsset` / `UnloadPrefabContents` (keeps GUID and added components; idempotent, verified by running twice). Also `lightProbeUsage=Off`, `reflectionProbeUsage=Off`, shadows On. Throws if `Assets/Materials/BilliardRogue/M_Palette.mat` is missing. |
| `BilliardRogueReimport.cs` | `[MenuItem("Nex/Billiard Rogue/Reimport Generated Assets")] ReimportAll()` | targeted force reimport (§10.4) |
| `BilliardRogueImportAudit.cs` | `[MenuItem("Nex/Billiard Rogue/Log Import Audit")] Run()` | logs transforms, verts/tris, vertex attributes, clips, texture formats, audio settings (`[ImportAudit]` console line) |

With `materialImportMode=None` the FBX renderers get Unity's default material (`Default-Material`) until the prefab
builder assigns `M_Palette` — gameplay must reference the **prefab variants**, never the raw FBX. The variant stores
the material as an override on `m_Materials.Array.data[0]` per renderer, keyed by node name.

Create the material once through the Editor (CLI), e.g. with `ueval` from `unity-cli-helpers.sh`:
```bash
ueval - <<'EOF'
System.IO.Directory.CreateDirectory("Assets/Materials/BilliardRogue");
var mat = new Material(Shader.Find("BilliardRogue/ToonLit"));   // shader from urp-hd2d-rendering.md §7.2
mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/BilliardRogue/T_Palette16.png"));
AssetDatabase.CreateAsset(mat, "Assets/Materials/BilliardRogue/M_Palette.mat");
AssetDatabase.SaveAssets();
return AssetDatabase.AssetPathToGUID("Assets/Materials/BilliardRogue/M_Palette.mat");
EOF
```
(`Shader.Find` returns null until the ToonLit shader exists and is compiled; `Universal Render Pipeline/Simple Lit` is a stand-in.)

---

## 12. How Billiard Rogue should use this

1. **Bootstrap once**: create `Tools/{Blender,Textures,Audio,Shared}` from the templates; generate the palette; add the
   four editor scripts under `Starter/Assets/Scripts/Editor/BilliardRogue/`; `unity command recompile`; wait for the
   one-time global reimport (§10.4); create `M_Palette` (§11).
2. **Per model**: copy `model_slime.py` → `model_<name>.py`; build geometry with bmesh ops + formulas; colour with
   `bc.paint_faces(bm, faces, family, shade)`; call `bc.mesh_object(name, bm, mat, smooth=...)` (canonicalizes);
   put pivots at object origins (root part at the feet); bake rotation/scale of sub-parts into mesh data; names
   `Body`, `EyeL`, `Head`, … stay stable. Budget ~150–600 tris per enemy (slime = 324); count with `bc.tri_count`.
3. **Build**: run §3 steps 1–5. Check `ASSET_STATS` (`fbx_changed`) and view every generated PNG (Read tool) before
   committing — the icon PNG doubles as the visual check of the model.
4. **Gameplay prefabs** wrap or extend `Prefabs/BilliardRogue/Models/<Name>.prefab` (variant); add colliders/scripts on
   the root, tween `Body` (§6). Elite/boss recolours: a second palette PNG with the same 16x16 layout but different
   colours + a material `M_Palette_<Variant>` using it (same mesh/UVs), assigned on a prefab variant.
5. **Ball / reward icons**: render with `--icon` at 256 (UI, ASTC 4x4) and reference the Sprite from configs
   (ScriptableObject + `EnumDictionary`). Put UI icons in a SpriteAtlas when several are on screen.
6. **VFX**: one material on `T_PixelParticles`, many ParticleSystem prefabs differing by `rowIndex`, colour, size;
   register them in `VfxManager.VisualEffect` (currently an empty enum; `VfxManager` pools `ParticleSystem` prefabs
   and returns them via `OnParticleSystemStopped` with `stopAction=Callback`).
7. **SFX**: add entries to `SfxManager.SoundEffect` (currently `None=-1, GenericEnter=0, GenericExit=1`; give new
   entries explicit values so the serialized `EnumDictionary` stays stable), fill
   `EnumDictionary<SoundEffect, SoundEffectSpec>.clips` with the `_1.._3` variants (`PickSingleClip` rotates them,
   `PlayAudioClip` dedups the same clip within a frame) through an editor script / `set_serialized_field` — not by
   hand-editing the prefab. Call `SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.BallHitWall)`.
8. **BGM**: `BGM_BR_*.ogg` in `Audio/Bgm/BilliardRogue/` → Streaming Vorbis; fed to `BgmManager` (see its note).

---

## 13. Gotchas checklist
- Blender exits 0 on script errors unless `--python-exit-code 1`.
- Always pass absolute paths to `bpy.data.images.load` and the render output.
- `view_layer.update()` before reading `matrix_world`; `bm.normal_update()` after moving verts.
- `bakeAxisConversion` must stay **false** with `bake_space_transform=True` exports (true = model faces −Z).
- Never ship Blender-default FBX exports (scale 100, rot 270°, one clip per action x object).
- Palette texture: point, no mips, **uncompressed**, Clamp; UVs exactly at texel centres; v flipped (PNG row 0 = top).
- Normal maps uncompressed at pixel sizes (ETC2 24 dB); textures ≤128 px uncompressed (cost ≤64 KB each).
- Sprite dims multiple of 4 or in a SpriteAtlas, else no compression (project rule) — icons 256² comply.
- Do not write temp files with normal names under `Assets/` (Unity may import them and mint `.meta`); use dot-prefixed names or the scratchpad.
- Never bump `GetVersion()`; use the targeted reimport menu.
- The open Starter Editor locks the project → no batchmode on Starter; use `unity command recompile` / `menu` / `run_script`.
- First venv / first EEVEE run per machine is slow (5–11 s / 2–3 s) — do not mistake for a hang.
- FBX bytes change every export → only `export_fbx_if_changed` keeps git and the importer quiet.

## 14. Unverified / risks
- **ASTC support on the Nex Playground GPU [unverified]** — if absent, Unity decompresses ASTC to RGBA32 at load
  (works, slower load, 4x memory). Only icons/UI > 128 px use ASTC; everything pixel-art is uncompressed anyway.
- One-time global reimport cost in Starter when the postprocessor lands (§10.4) — not measured on Starter.
- DOTween snippet and the URP material helper calls were not compiled here (no DOTween/URP in the scratch project).
- FBX clip name follows the Blender scene name (`Scene`) — renaming via `scene.name` not tested.
- Texel density vs final camera framing (§8.2) must be checked in the real scene.
