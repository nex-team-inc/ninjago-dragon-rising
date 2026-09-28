# Rendering / HD-2D module — progress checkpoint

Owner: Rendering agent (TDD §10, §16, §17 WorldCameraRig, §0a D2/D3). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Rendering Starter/Assets/Shaders/BilliardRogue Starter/Assets/Scripts/BilliardRogue/Editor/{RenderPipeline,Materials,VolumeProfiles,WorldCameraRig,RenderTestScene}Builder.cs`.

## Status

- [x] Shaders (`Assets/Shaders/BilliardRogue/`): ToonLit (+ ToonLitInput.hlsl / ToonLitForwardPass.hlsl shared includes),
      ToonLitTransparent, LitParticle, GlowParticle, LightShaft, AimGuide, TiltShiftBlur — TDD §16 property names.
- [x] Runtime (`Rendering/`): PixelWorldDisplay (full), WorldCameraRig, TiltShiftVolume, TiltShiftFeature + TiltShiftPass (Render Graph only).
- [x] `HD2DVisualConfig`: additive `tiltShiftSampleCount`, `bloomMaxIterations`.
- [ ] Editor builders: RenderPipelineBuilder, MaterialsBuilder, VolumeProfilesBuilder, WorldCameraRigBuilder, RenderTestSceneBuilder.
- [ ] Editor verification (inside `Tools/editor_lock.sh rendering`): recompile, shader compile check (GLES3 + Metal via ShaderData.CompileVariant),
      run builders, build + render RenderTest.unity, iterate on the look, commit generated assets with .meta.

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
- `Nex.VolumeManager` (starter audio) shadows `UnityEngine.Rendering.VolumeManager` inside `Nex.BilliardRogue`; use the fully qualified name.

## API exposed to other modules

- `WorldCameraRig`: `Initialize(Camera uiCamera)` (Flow calls it with the RootCamera before pushing views), `SetVolumeProfile(VolumeProfile)`,
  `SetPose(Vector3 localPosition, float pitchDeg, float fov)`, `SetQualityFeatures(bloom, tiltShift, shadows)`, `WorldCamera`, `CameraPivot`,
  `Display` (PixelWorldDisplay), `WorldVolume`.
- `PixelWorldDisplay`: `Initialize(Camera, RawImage, HD2DVisualConfig)`, `Target`, `WorldCamera`, `PixelationEnabled`, `SetPixelationEnabled`,
  `TryWorldToCanvas(world, canvasRect, out anchored)` (centre-relative), `WorldToScreenNormalized`, `SetFocusPoint`, `SetBaseFieldOfView`,
  `SetVisibleHeight`, `FindQualityOverride`, `VisibleWidth/Height`.
- `TiltShiftVolume` (VolumeComponent): intensity, center, bandWidth, falloff, maxBlur, sampleCount.

## Requests (for the integrator)

- Flow (`BilliardRogueCoordinator` + `MainSceneBuilder.WireCoordinator`): add `[SerializeField] WorldCameraRig worldCameraRig` (wire with
  `world.GetComponentInChildren<WorldCameraRig>(true)`), call `worldCameraRig.Initialize(rootCamera)` in `StartMain` before the first view is
  pushed (RootCamera = the ViewManager chain camera), and `worldCameraRig.SetPose(config.Arena.CameraPosition, config.Arena.CameraPitchDeg,
  config.Arena.CameraFov)` (the builder bakes the same pose from ArenaConfig.asset, so this only matters if the asset changes later).
- Flow (`BilliardRogueInitializer`): `Application.targetFrameRate = 60` (starter ApplicationManager caps at min(60, refresh) already; keep explicit).
- Presentation-World (`ActEnvironmentController`): apply `ActDefinition.VolumeProfile` through `WorldCameraRig.SetVolumeProfile`.
