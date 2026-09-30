# Painted sprites: how Billiard Rogue's 2D art is made

Every sprite in this folder is **painted the way a pixel artist paints**: sketch the line art first, bucket-fill
the flats, then shade by hand, then colour the lines, then clean up. `paintkit.py` renders a `.paint` file pass by
pass, so each stage stays visible and reviewable. The format reference is in the `paintkit.py` docstring.

```
Tools/.venv/bin/python Tools/Textures/paintkit.py Tools/Textures/paint/Icons/Ball_Basic.paint --out-dir DIR
```

That prints the lint line and writes `DIR/<name>.png` and `DIR/<name>_process.png`. The process sheet shows
1 draft (on paper) | 2 flats | 3 shaded | 4 final | the asset it replaces, plus the final at the game's 3x scale on
a navy panel, a parchment card, a crypt floor and grass. **Look at the process sheet after every change.**

A painting `paint/<Folder>/<Name>.paint` replaces the procedural sprite of the same name in
`Sprites/BilliardRogue/<Folder>/` (`paintkit.override`, called by every generator). `Enemy_*` and `Portrait_*`
exist only as paintings (`make_painted.py`). `Tools/Textures/build_art2d.py` builds everything.

## Why: the tells of generated art (none of these may survive)

| Tell | What it looks like | What a painter does instead |
|---|---|---|
| Pillow shading | Shade rings that follow the outline all the way round, bands of equal width | One light (top-left): the form shadow is a crescent on the far side, plus a thin reflected light at the rim |
| Maths bands | Lambert term cut into bands, jagged terminators, 5 near-identical bands | 2-3 shade steps with deliberately shaped edges |
| Doubled corners | Thick outline corners (a dilated mask), L-shaped staircases | Pixel-perfect 1 px lines, one diagonal step per corner |
| Uneven curves | Stair runs like 3-1-4-2, lopsided circles from rounding | Monotonic runs (6-3-2-1-1-2-3-6), mirror-symmetric circles |
| Sticker outlines | Every emblem and inner shape wrapped in the same black ring | Outer silhouette dark; inner lines sel-out (a darker tone of the colour they sit in); decals without outlines |
| Noise and dither | Speckle, random pixels, checkerboard gradients, random "dissolve" | Flat clusters; fades and break-ups drawn as shapes (the puff splits into 2-3 clumps that shrink) |
| Colour soup | 100+ colours, pairs a few units apart, shadows = colour x 0.5 (grey mud) | A small hue-shifted ramp per material: shadows cooler and more saturated, highlights warmer |
| Render residue | Downsampled 3D renders: blurry clusters, illegible detail, broken silhouettes | Designed at the target size: silhouette first, 2-3 readable features, everything else left out |
| Random scatter | Sparkles and stars dropped at random spots | Each accent placed on purpose (on the highlight, on the corner) |
| Soft fringes | Partial alpha on hard-alpha sprites | Hard alpha only; glows and shadows use a few clean alpha steps (`alpha soft`) |
| Misreads | A letter or symbol that reads as something else (the old logo's D read as O) | Check the silhouette at 1x and 3x |

## The process (and what each pass must show)

1. **Draft**: the line art on paper. Silhouette plus every region boundary, 1 px, clean. Use a `draft` grid for small
   sprites, `strokes` (ellipse, arc, curve, line) for big curves. `ellipse` is mirror-symmetric; a 30 px ball on
   pixels 1..30 is `ellipse K 16 16 14.8 14.8`. Tiny solid shapes (toes, eyes, 1-2 px detail) may be drawn directly
   in their colour.
2. **Flats**: one seed per region (`seeds` or a `flats` grid). A `leak` error means a gap in the draft: fix the draft,
   never the fill. `unfilled enclosed region` warnings are holes: fill them or make sure they are meant.
3. **Shade**: `band R P dx dy n` (the region worn back from one side, dx dy = the direction away from the light,
   usually `1 1`), `blob`, `poly`, `px`, and `shade` grids for hand touches. Paint decals and emblems *after* the
   form is shaded (a `shade at X Y` grid), then shade the decal (`band =B b 1 1 1`).
4. **Lines**: `lines K` recolours interior ink to the key's `line=` colour; `edge K k -1 -1` lightens the outline
   where it faces the light.
5. **Cleanup**: the lint line must be clean. Fix every warning, or know exactly why it stays.

## House style

- **Light**: top-left, slightly in front. Every sprite, every frame. Cast and form shadows fall bottom-right.
- **Outline**: 1 px. The outer silhouette uses the darkest tone of the object's own hue ramp (never pure #000).
  UI kit ink is `#0a0c18`. Inner lines are sel-out.
- **Ramps**: 3-5 steps per material, hue-shifted. Reuse the UI kit ramps so the kit stays one family:
  - GOLD `#fff4b8 #ffd65a #e8a830 #a8661a #5c360c`
  - NAVY `#34447a #283867 #1f2c56 #172246 #111936 #0c1228`
  - PARCHMENT `#fbf1d2 #f2e2b4 #e6d29e #d4b97e #b8965a #8a6a38`
  - CRIMSON `#ff7a86 #e0485a #b8283a #8c1c2c #6a1222 #4a0a16`
  - Cats (match the 3D cats): P1 orange tabby `#ee8a5e #e47b52 #d67244 #c96936 #bb612a #8f4c0d`; P2 charcoal
    `#6a7082 #5d6272 #595e6c #535865 #474b58 #292b34`; white socks and muzzle `#f4f6fb #e3e8f2 #d6dae4 #babdc9`;
    toe beans `#e88aa6 #bb698e`
  - Enemy and cat colours come from the 3D models (`Tools/Blender/enemies/models_*.py`,
    `Starter/Assets/Textures/BilliardRogue/Palette/palette.json`), so the icons match what is on the board.
  - Ball colours: `Sprites/BilliardRogue/Icons/icons.json` (`color`, `glowColor`).
- **Colour budget** (per sprite, or per particle sheet): 16x16 at most 6, 32x32 at most 10, 48x48 at most 16,
  128x128 at most 32, logo at most 24. `near_dupes` must be 0.
- **Clusters**: no lone pixels except deliberate glints and eye highlights (a few per sprite). No checker dither.
- **Readability**: judge it at the game scale (the 3x strip) as well as zoomed in.

## Contracts (the game depends on them)

- Same file name and **exact pixel size** as the sprite it replaces (.meta, 9-slice borders, pivots and UI rects
  assume them; `override` refuses a size change).
- UI frames: `make_ui.validate_slices`. Edge strips must be uniform along their stretch axis, and a Sliced centre must
  be flat. Borders are in `Sprites/BilliardRogue/UI/ui_slices.json`.
- Particles: 8 frames in a horizontal strip, 16 or 32 px frames, a 1 px empty gutter round every frame, alpha 0/255,
  grey values only (255 / 214 / 170 / 128, plus contour 56 for Leaf and Debris) because the vertex colour tints them.
- Paws: 36x40, palm centre (18, 20) = pivot; the cuff (rows 30+) is 22 px wide and joins the 24x16 arm sleeve, which
  tiles vertically (top and bottom rows must meet seamlessly).
- Hard alpha everywhere except sprites that are glows or shadows by design (declare `alpha soft`).
