# Polish — gameplay view (art direction + tech art) — progress checkpoint

Owner: gameplay-view polish agent. Resume from here: read this file + `git log --oneline -10 -- Starter/Assets/Scripts/BilliardRogue/{Configs,Rendering,Presentation,Editor}`.
Shots (outside the repo): `/private/tmp/claude-501/-Users-simonbut-project-VibeProject3/78b50d75-0262-494a-a64b-9a4cc3dfcbc6/scratchpad/polish/shots/`
(`before_*.png` = baseline, `after_*.png` = final; `v1/v2/v3_*` = intermediate; `*_s.png` = 960 px previews; `c*_*.png` = full-res crops).
Helpers: `scratchpad/polish/ps.sh` (`pshot`, `cam`, `stats` on top of the playable-pass `pl.sh`), `scratchpad/polish/camsolve.py` (framing solver).

## Status: DONE (this pass) — all acts, both bosses and 2P captured after; EditMode tests green (Rendering 8/8, Environment 4/4, ArenaLayout 2/2)

## Baseline (before)
- Camera ArenaConfig (0, 20.4, −6.947) pitch 58 fov 28: grid 0.14–0.83 of the screen height, launch line at 0.92, cat feet at
  0.97 → cat / cue / waiting ball in the bottom 3–7 % inside the tilt-shift blur; top row 607 px wide.
- Floor: palette-brown bevelled Base + the plank-like StoneFloor quadrant under a +12 temperature grade → wooden crates.
- Bloom threshold 0.95–1.05 / intensity 0.65–1.3 + warm split toning: yellow (Act 1), blue (Act 2), purple (Act 3) haze;
  emissive palette ×2.2 × JuiceConfig 2.2 → bloom blobs (Crystal Golem invisible, Shield Knight marker a white blob).
  Sun 49/62/77° → shadows invisible.
- Enemies 0.8–0.95 units (bounds) but ~30 texels per cell; HP = 32 px white text, no plate; boss also had a world HP label.
- Cost (play mode, unfocused Editor, 640×360 world RT + 1080p UI): Act 2: 579 batches / 57 SetPass / 58.7k tris / 117k verts,
  289 shadow casters; Act 3: 501 / 75 / 52.2k / 113k; Act 3 boss: 412 / 53 / 41.2k / 89.9k (arena 252 renderers + every
  diorama prop drawn one by one; Unity static batching was active but interleaved materials kept one draw per object).

## Done
- [x] Camera: `ArenaConfig` (0, 22.1, −11.7), pitch 52, FOV 22 (solved: top-row heads 0.927, cat feet 0.048 / head 0.142,
      pad front 0.00; grid 665 px top / 825 px danger row on 1920; left edge ≥ 557 px clears the HUD columns).
      `HD2DVisualConfig` tilt-shift centre 0.49 / half 0.44 / falloff 0.06 (sharp band = whole playfield), shadow distance 42.
- [x] Floor: `M_ArenaFloorBase` (dark matte) on the Base part of floor / danger / launch-pad pieces; floor surfaces
      (`MaterialsBuilder.FloorSurfaces` = StoneFloor, CryptFloor, RuinFloor, HollowFloor) tint 0.82, bump 1.1, cavity 0.4;
      `MaterialsBuilder` now also builds every surface listed in surfaces.json (RuinFloor / HollowFloor from the 2D pipeline).
- [x] Grading: `VolumeProfilesBuilder` (thresholds 1.2–1.25, intensities 0.42–0.6, contrast 18–20, temperature 3 / −12, saturation
      6 / −4 / 10, tint 0 / 3 / 8, vignette 0.2–0.3; Title keeps a richer grade). Palette emission 2.2 → 1.6, `JuiceConfig.Emissive.strength`
      2.2 → 1.2, shield marker ×2 → ×0.9 HDR.
- [x] Lighting: `EnvironmentLooks` re-calibrated (sun 46° / 55° / 58°, shadowStrength 0.9–0.92, cooler ambient, act lights 1.2 / 2.0 / 1.7,
      fog 0.006–0.009); presets re-seeded (Act_1..3 + title). Cast shadows now read under actors (see `c3_boss.png`).
- [x] Actors: `M_Palette` rim 0.55 / power 3, shadow tint (0.34, 0.32, 0.5); `EnemyView.authoredFootprint` (builder) ×
      `JuiceConfig.EnemyMotion.cellFill` 0.92 → 1x1 scale 0.97–1.14, bosses 0.98–1.08; pillar 1.6× tall, crate 1.1×, portal 1.12×,
      pickups 1.35×; cat `modelScale` 1.35 and `standSideOffset` 0.62 (P1 left / P2 right of the ball so the ball is never hidden).
- [x] Labels: `WorldLabel` dark rounded pill (built-in sliced sprite) sized to the number, colour full→mid→low by HP %, offset (0, 14);
      bosses show no HP number (HUD bar); `JuiceConfig.Labels` colours / padding designer-tunable.
- [x] Balls: `flightGlow` 1.8 (`_EmissionStrength` MPB), trail 0.26 s × 1.15 width on HDR `M_BallTrail` (intensity 2.4), ball material
      shadow tint 0.7, resting glow 0.7; the waiting ball at the cue is a lit `M_Ball_Basic` sphere with emission (`ghostIntensity` 2.2).
- [x] Cost: `Editor/StaticMeshCombiner` (build-time merge per group × material × shadow mode into mesh assets under
      `Prefabs/BilliardRogue/{World,Environment}/Combined`): Arena 259 renderers → 10, Env_Act1 205 → 5 (+26 animated kept),
      Act2 186 → 4 (+23), Act3 156 → 3 (+8). Surface slots and danger inlays keep their own combined renderer (ArenaView swap / pulse verified).

## Measurements (play mode, unfocused Editor ~10 fps, world RT 642×362 (640×360 visible) + 1920×1080 UI; UnityStats)
| Scene | before batches / SetPass / tris / shadow casters | after |
|---|---|---|
| Act 1 stage 1 (5 enemies) | 456 / 54 / 45.6k / 202 (v2, same content) | 152 / 52 / 54.1k / 51 |
| Act 1 boss (King Slime) | 441 / 45 / — / 194 | 137 / 43 / 51.0k / 43 |
| Act 2 stage 1 (12 enemies) | 579 / 57 / 58.7k / 289 | 194 / 54 / 55.2k / 72 |
| Act 3 stage 1 (12 enemies) | 501 / 75 / 52.2k / 265 | 210 / 74 / 55.2k / 80 |
| Act 3 boss (Crystal Golem) | 412 / 53 / 41.2k / 218 | 116 / 48 / 43.9k / 33 |
| 2P act 1 | — | 171 / 52 / 56.1k / 60 |
Batches include the UI canvases (HUD, labels, PiP) and the shadow pass. What remains is per enemy rigid part (Skeleton 6 renderers,
bosses 7) + their shadow-map draws, the ball trail and the act flames / shafts; 12-enemy stages sit at ~200, boss stages under 120.

## Next / remaining issues
- 12-enemy stages are above the 120-batch budget: enemy rigid parts (5–7 renderers each) would need either single-mesh enemies
  (Blender export with baked part animation → Animator) or fewer shadow casters (cast from the Body part only).
- `Prop_Pillar.fbx` is a squat cube with a green belt: scaled 1.6× tall it reads as a block; needs re-authoring (Tools/Blender/heroprops).
- Crystal Golem base glow (yellow emissive core / intro VFX) still blooms strongly; other emissives are calm now.
- Tilt-shift is now only a 5 % strip top and bottom (playfield fills the frame); the diorama feel comes from the lens + shadows.
- `_WorldRimColor` (per-act rim) is set but not read by ToonLit — rim is per material; hooking it in would make the rim act-tinted.

## Findings / coordination
- The 2D-art and environment agents are mid-revision (uncommitted `Tools/Textures/make_surfaces.py`, `Tools/Blender/environment/layouts.json`
  +723/−1416, staging models/textures). Their staging output (calm one-slab-per-quadrant floors, RuinFloor / HollowFloor, Env_MossBorder,
  Env_CryptGroundTile, regenerated trees / tombstones / pools) was synced into `Starter/Assets` (Surfaces + Environment models only) and
  the Env_Act prefabs were rebuilt from their WIP layouts.json, so Build All must be re-run once they land (their layouts may change again).
- `UiViewsBuilder` / `FlowPrefabsBuilder` / `MainSceneBuilder` were re-run to get a playable scene with the Flow agent's new `curtain`
  fields; the rebuilt UI / Views / Flow prefabs are their outputs and are left for them to commit.
- Kit pieces show the bottom-left 32×32 quadrant of each 64 px surface (own box UVs, `_Tiling` 0.5) — the surface generator is built for it.
- Enemy prefab bounds include the inactive ShieldMarker (1.0 × 0.7 at z 0.5); the builder measures the model before it exists.
