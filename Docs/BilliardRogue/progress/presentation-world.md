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
- [ ] Re-run after Rendering lands MaterialsBuilder (M_Palette, M_Surface_*, M_LightShaft, M_DangerTile), Volume_Act*/Title, WorldCameraRig
      prefab and the World/WorldVolume layers; then iterate on the look with the real ToonLit + post stack

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

## API exposed

- `ActEnvironmentController`: `Initialize(ArenaConfig)`, `ApplyTitle(ActDefinition backdrop, bool instant = false)`,
  `ApplyAct(ActDefinition act, bool instant = false)`, `Arena` (ArenaView), `CurrentActIndex` (TitleActIndex -1 / NoActIndex -2), `IsTransitioning`.
- `ArenaView` (Arena.prefab root, next to ArenaLayout): `Initialize(ArenaConfig)` (called by the controller), `Layout`, `DangerRow`,
  `ApplySurfaces(...)`, `SetDangerLevel(float level01)`.
- `DangerRowPulse.SetDangerLevel(float level01)`: 0 = empty (dim steady glow), 1 = occupied (fast bright pulse).
- `EnvironmentBuilder.Run()`, `EnvironmentBuilder.WireScene(GameObject world)` (for MainSceneBuilder), `EnvironmentBuilder.WorldLightingPath`.
- Global shader colour `_WorldRimColor` (ActLightingPreset.rimColor).

## Requests (for the integrator) — see final report for exact edits
