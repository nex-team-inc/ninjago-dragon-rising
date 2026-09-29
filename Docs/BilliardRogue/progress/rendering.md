# Rendering / HD-2D module — progress checkpoint

Owner: Rendering agent (TDD §10, §16, §17 WorldCameraRig, §0a D2/D3). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Rendering Starter/Assets/Shaders/BilliardRogue Starter/Assets/Scripts/BilliardRogue/Editor/{RenderPipeline,Materials,VolumeProfiles,WorldCameraRig,RenderTestScene}Builder.cs Starter/Assets/Scripts/BilliardRogue/Editor/Tests/RenderingContractTests.cs`.

## Status

- [x] Shaders (`Assets/Shaders/BilliardRogue/`): ToonLit (+ ToonLitInput.hlsl / ToonLitForwardPass.hlsl shared includes),
      ToonLitTransparent, LitParticle, GlowParticle, LightShaft, AimGuide, TiltShiftBlur — TDD §16 property names. All 7 compile for
      GLES3 (Android) + Metal across 240 representative variants (ShaderData.CompileVariant), no import errors.
- [x] Runtime (`Rendering/`): PixelWorldDisplay (full), WorldCameraRig, TiltShiftVolume, TiltShiftFeature + TiltShiftPass (Render Graph only).
- [x] `HD2DVisualConfig`: additive `tiltShiftSampleCount`, `bloomMaxIterations`.
- [x] Editor builders: RenderPipelineBuilder, MaterialsBuilder, VolumeProfilesBuilder, WorldCameraRigBuilder, RenderTestSceneBuilder — all run;
      generated assets committed with .meta (materials, Volumes, WorldRenderer.asset, WorldCameraRig.prefab, RenderTest.unity, URP asset
      + renderer settings, World/WorldVolume layers in TagManager).
- [x] Look verified in the Editor (RenderTest.unity rendered through the rig into the 640×360 target and composited under a 1920×1080 UI
      canvas): banded toon shading, hard sun shadows, stepped point-light pools, 1:1 pixel surface textures, bloom on emissives only,
      tilt-shift blur top/bottom, crisp UI, `TryWorldToCanvas` labels land on enemy heads. Shots (outside the repo):
      `/private/tmp/claude-501/-Users-simonbut-project-VibeProject3/78b50d75-0262-494a-a64b-9a4cc3dfcbc6/scratchpad/modules/rendering/shots/`
      (`composite_fx_pix.png` / `composite_nofx_pix.png` = before, `composite_fx_v2.png`, `composite_nofx_v2.png`, `composite_fx_v3.png` = after).
- [x] EditMode tests `Editor/Tests/RenderingContractTests.cs` (shader/material contract, URP asset, World renderer, layers, volumes, rig
      wiring, PixelWorldDisplay projection in a preview scene): 8/8 pass
      (`utests Nex.BilliardRogue.Editor.Tests.RenderingContractTests`; the test runner leaves an untitled scene open — reopen Main.unity).

## Decisions

- Dedicated World renderer asset (`Assets/Settings/BilliardRogue/WorldRenderer.asset`, index 1 in the URP asset renderer list) carries the
  TiltShiftFeature, so UI cameras (default renderer 0) never run it. WorldCamera uses `SetRenderer(1)`.
- Rig hierarchy: `WorldCameraRig [WorldCameraRig, PixelWorldDisplay] → CameraPivot (pose) → WorldCamera (local identity)`. CameraShaker
  moves the pivot (camera's parent); PixelWorldDisplay re-derives the camera from the pivot every LateUpdate (execution order 200) and
  snaps it to the texel grid, pushing the sub-texel remainder into the RawImage uvRect (margin texels from HD2DVisualConfig).
- `TryWorldToCanvas` returns centre-relative anchored coordinates (label anchors/pivot at 0.5,0.5), matching WorldLabelLayer.Project.
- Feature toggles (quality overrides + DebugSettings.disableBloom/disableTiltShift) are a second, higher-priority Volume
  (`Volume_FeatureOverrides`: Bloom intensity 0, TiltShift intensity 0) whose runtime `profile` copy has its components `active`
  toggled — no asset mutation, no statics. Pixelation off = native-res bilinear target (the world camera never renders to screen).
- Shadow tint semantics: `light * lerp(_ShadowTint, 1, ramp)`; additional lights band both NdotL and distance falloff (stepped pools).
- `_Tiling` multiplies UVs (surfaces.json: `_Surface` meshes carry 1 UV = 1 m, so M_Surface_* use `_Tiling = tiling` from the manifest, 0.5).
  `_WORLD_UV` keyword = box projection for primitives / meshes without surface UVs.
- **Sampler policy**: ToonLit samples `_BaseMap/_BumpMap/_CavityMap/_EmissionMap` through URP's global `sampler_PointRepeat`
  (GlobalSamplers.hlsl). Palette UVs never leave the atlas and the 64 px surface sets must repeat, so the look no longer depends on
  importer wrap/filter (today the PNGs still import Clamp/Bilinear, which stretched the texture's top row into a column over the floor).
- Bloom is relative to HD2DVisualConfig defaults (threshold 0.9 / intensity 0.8 / scatter 0.7 = 1×): Default 1.05/0.6, Title 0.8×,
  Act1 0.65×, Act2 0.95/0.9, Act3 0.95/1.1 so sunlit albedo stays crisp and only emissives/glows bloom. Ball resting glow = 0.45× the
  BallDefinition glow (Presentation pulses `_EmissionStrength`).
- `Nex.VolumeManager` (starter audio) shadows `UnityEngine.Rendering.VolumeManager` inside `Nex.BilliardRogue`; use the fully qualified name.
- The project renders in Gamma colour space (`UNITY_COLORSPACE_GAMMA`); PixelWorldDisplay picks the RT format from the active colour space.
- (Perf pass 3, `final-perf.md`) The view manager's RootCamera is the only screen camera (Base, HDR / MSAA / post /
  shadows off, World layers culled; built by FlowPrefabsBuilder); there is no Main Camera and no camera stack, so the
  1080p UI renders straight into the surface. Android Blit Type Auto, Frame Timing Stats on (RenderPipelineBuilder).
- Low-end GPU tier: `HD2DVisualConfig.DetectTier` (GPU name list, shader level) → `WorldCameraRig` enables LowTierVolume
  (`Volume_LowTier`: bloom mip count / resolution, tilt-shift taps; priority 50, between the act looks and the feature
  switches); `DebugSettings.renderTier` 0 auto / 1 full / 2 low. `FrameTimingLogger` on the rig prints `[Perf]` lines.

## API exposed to other modules

- `WorldCameraRig`: `Initialize(Camera uiCamera)` (Flow calls it with the RootCamera before pushing views), `SetVolumeProfile(VolumeProfile)`,
  `SetPose(Vector3 localPosition, float pitchDeg, float fov)`, `SetQualityFeatures(bloom, tiltShift, shadows)`, `WorldCamera`, `CameraPivot`,
  `Display` (PixelWorldDisplay), `WorldVolume`.
- `PixelWorldDisplay`: `Initialize(Camera, RawImage, HD2DVisualConfig)`, `Target`, `WorldCamera`, `PixelationEnabled`, `SetPixelationEnabled`,
  `TryWorldToCanvas(world, canvasRect, out anchored)` (centre-relative), `WorldToScreenNormalized`, `SetFocusPoint`, `SetBaseFieldOfView`,
  `SetVisibleHeight`, `FindQualityOverride`, `VisibleWidth/Height`.
- `TiltShiftVolume` (VolumeComponent): intensity, center, bandWidth, falloff, maxBlur, sampleCount.
- Builders (menu `Nex/Billiard Rogue/…` or `eval`): `RenderPipelineBuilder.Run()`, `MaterialsBuilder.Run()`, `VolumeProfilesBuilder.Run()`,
  `WorldCameraRigBuilder.Run()`, `RenderTestSceneBuilder.Run()` (order: pipeline → materials → volumes → rig → test scene).

## Requests (for the integrator)

- Flow (`BilliardRogueCoordinator` + `MainSceneBuilder.WireCoordinator`): add `[SerializeField] WorldCameraRig worldCameraRig` (wire with
  `world.GetComponentInChildren<WorldCameraRig>(true)`), call `worldCameraRig.Initialize(rootCamera)` in `StartMain` before the first view is
  pushed (RootCamera = the ViewManager chain camera), and `worldCameraRig.SetPose(config.Arena.CameraPosition, config.Arena.CameraPitchDeg,
  config.Arena.CameraFov)` (the builder bakes the same pose from ArenaConfig.asset, so this only matters if the asset changes later).
- Flow (`BilliardRogueInitializer`): `Application.targetFrameRate = 60` (starter ApplicationManager caps at min(60, refresh) already; keep explicit).
- Presentation-World (`ActEnvironmentController`): apply `ActDefinition.VolumeProfile` through `WorldCameraRig.SetVolumeProfile`.
- ImportSettingsBuilder (integration): `Assets/Textures/BilliardRogue/Surfaces/*` → Point, Repeat, no mips; `Palette/*` → Point, Clamp,
  uncompressed (per asset-toolchain.md). Rendering is already immune (sampler policy) but Inspector previews and any stock-shader use are not.
