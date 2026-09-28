# Presentation-World module — progress checkpoint

Owner: Presentation-World agent (TDD §8 ActEnvironmentController, §14.1, §17 Arena / Env_Act prefabs). Resume from here after a
usage-limit kill: read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Presentation/Environment Starter/Assets/Scripts/BilliardRogue/Editor/Environment*`.

## Status

- [x] Runtime: `ActEnvironmentController` (Presentation/), `Environment/`: `ArenaView`, `ArenaSurface`, `DangerRowPulse`, `ActEnvironment`,
      `DioramaAnimator`, `EnvironmentLook`, `EnvironmentConfig` (SO)
- [x] Configs: `ArenaConfig` (+ launchPadPrefab, DangerRowSettings, camera default z -6.947), `ActLightingPreset` (+ shadowStrength, seededFromLayout)
- [x] Editor: `EnvironmentBuilder` (entry, WorldLighting prefab, config slots, `WireScene`), `EnvironmentArenaBuilder`, `EnvironmentActBuilder`,
      `EnvironmentPieces`, `EnvironmentLayout` (layouts.json DTOs)
- [x] Builder run in the Editor: Arena.prefab, Env_Act1..3.prefab, WorldLighting.prefab, EnvironmentConfig.asset, act presets seeded, ArenaConfig
      model slots, ActDefinition.environmentPrefab filled; ArenaConfig.asset camera moved to (0, 20.4, -6.947)
- [x] Preview renders (edit mode, preview scene, stand-in rig): shots in scratchpad `modules/presentation-world/shots/v2_*.png`
- [x] Re-run after Rendering landed MaterialsBuilder / Volume_* / WorldCameraRig / layers: prefabs now use M_Palette, M_Surface_*,
      M_LightShaft, M_DangerTile on layer World; ActDefinition.volumeProfile + EnvironmentConfig.titleVolumeProfile filled
- [x] Look iteration with the real ToonLit + post stack (shots `v5_*`..`v7_*`): `EnvironmentLooks` (Editor) calibrates the layout presets
      (seeded once; builder re-seed reproduces the tuned assets exactly), `ActLightingPreset.additionalLightIntensity` (blended, drives the
      act lights through DioramaAnimator), danger idle glow 0.45 -> 0.9. EditMode tests 4/4 green.
- [x] DangerRowPulse drives the inlay emission for real (M_DangerTile ships with _EmissionStrength 0: the pulse now sets it to 1 and
      scales _EmissionColor, which also works on the URP Lit placeholder); orange-red colour, idle 0.55, pulse 0.8..2 — reads in every act
      even under the current Act 2 grade (shots `v10_*`, crops `crop_v10.png`).
- [x] Act transitions that change the diorama swap behind a fog veil (EnvironmentConfig.swapVeilFogDensity, peak at t = 0.5, god rays
      fade with it); verified frame by frame in `veil_sheet2.png`. Lazily instantiated dioramas start hidden.
- [ ] Waiting on Rendering: Volume_Act2 grade crushes red (warm torch pools / enemy reds lose colour) — see Requests

## Decisions

- Torches are act-specific (warm WallTorch vs Env_WallTorch_Arcane) → they live in Env_ActN (layouts.json act props), not in Arena.prefab.
- Arena kit is computed from ArenaConfig (columns/rows/launchZoneHeight/worldScale), reproducing layouts.json "kit" exactly.
- ActEnvironmentController lives on `Environment/WorldLighting.prefab` (controller + Sun + GradeBlendVolume). Grading: the rig's world volume is
  the base; the blend volume (priority rig+1) fades the next profile in, then `WorldCameraRig.SetVolumeProfile` takes it (Rendering request).
- Act dioramas: placed scene instances are used when wired; an act without one is instantiated from `ActDefinition.environmentPrefab` on first use.
- Lights: at most 6 realtime point lights per act (strongest `light:true` props by intensity×range), no shadows, culling mask World.
- Lighting presets: builder copies layouts.json `lighting.preset` into ActDefinition.lighting once (`seededFromLayout`), then never overwrites.
- Ground tiles pruned against the ArenaConfig camera frustum (+8° margin): 58 of 120 kept per act. Tiny decor casts no shadows.
- Fallback materials (URP Lit / Particles Unlit) in `Prefabs/BilliardRogue/Environment/Fallback/` until MaterialsBuilder runs.
- Runtime trilight ambient updates the SH probe automatically (verified: probe changes per act in edit-mode renders).
- Preview renders patch the surface textures to Repeat wrap in memory only (they are still imported as Clamp sprites until
  integration's ImportSettingsBuilder runs: ground grass shows streaks without it). View prefabs are on Default and invisible to the
  World renderer (opaque mask = World) — preview moves them to World.

## API exposed

- `ActEnvironmentController`: `Initialize(ArenaConfig)`, `ApplyTitle(ActDefinition backdrop, bool instant = false)`,
  `ApplyAct(ActDefinition act, bool instant = false)`, `Arena` (ArenaView), `CurrentActIndex` (TitleActIndex -1 / NoActIndex -2), `IsTransitioning`.
- `ArenaView` (Arena.prefab root, next to ArenaLayout): `Initialize(ArenaConfig)` (called by the controller), `Layout`, `DangerRow`,
  `ApplySurfaces(...)`, `SetDangerLevel(float level01)`.
- `DangerRowPulse.SetDangerLevel(float level01)`: 0 = empty (dim steady glow), 1 = occupied (fast bright pulse).
- `ActLightingPreset.additionalLightIntensity` (per-act multiplier on the act lights, blended in transitions).
- `EnvironmentBuilder.Run()`, `EnvironmentBuilder.WireScene(GameObject world)` (for MainSceneBuilder), `EnvironmentBuilder.WorldLightingPath`.
- Global shader colour `_WorldRimColor` (ActLightingPreset.rimColor).

## Requests (for the integrator) — see final report for exact edits

- Rendering `VolumeProfilesBuilder.DeepBlueNight`: measured red crush (danger inlay (97,30,45) -> (3,0,79), floor R -> 0). Validated
  in-memory proposal: temperature -14, tint 4, saturation 0, contrast 14, postExposure 0, lift (0.98,0.99,1.03,0), gain (1,1,1.02,0),
  split shadows (0.3,0.36,0.72) / highlights (1,0.86,0.7) balance -15, bloom tint (0.9,0.95,1) intensity 1.3.
- Rendering: commit generated Materials/BilliardRogue/*.mat + Settings/BilliardRogue/** (my prefabs reference their GUIDs).
- Presentation-Core `WorldPrefabsBuilder`: put view prefabs (Enemies/Balls/Player/Board) on layer World.
- Integration: ImportSettingsBuilder must import Textures/BilliardRogue/Surfaces/* as Default textures with Repeat wrap.
