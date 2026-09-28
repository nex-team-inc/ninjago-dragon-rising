# VFX progress (TDD D13, §16, §17, VfxManager.VisualEffect)

Owner paths: `Starter/Assets/Scripts/BilliardRogue/Editor/VfxPrefabsBuilder.cs`, `Starter/Assets/Scripts/BilliardRogue/Presentation/Vfx/**`
(runtime `VfxFloorPlane` + editor helpers under `Presentation/Vfx/Editor/`), `Starter/Assets/Prefabs/BilliardRogue/Vfx/**`,
`Starter/Assets/Materials/BilliardRogue/Vfx/**`, `Starter/Assets/Scenes/BilliardRogue/Tests/VfxGallery.unity`.

## Status
- [x] Runtime helper `Presentation/Vfx/VfxFloorPlane.cs` (collision plane child re-pinned to floor y in LateUpdate; pooled-safe)
- [x] Editor: `VfxLayerSpec` (data + fluent setters), `VfxRecipe`, `VfxPalette`, `VfxCombatRecipes`, `VfxFeedbackRecipes`,
      `VfxAmbientRecipes`, `VfxParticleFactory` (all modules explicit), `VfxMaterialLibrary` (particles.json + M_Vfx_* mats),
      `VfxPrefabWriter` (edits existing prefabs in place → stable root fileIDs), `VfxRegistryWriter` (VfxManager EnumDictionary +
      ActDefinition ambient slots, fill-if-null), `VfxGalleryBuilder` (additive scene, saved + closed), entry `VfxPrefabsBuilder.Run()`
- [x] Builder run in the Editor: 21 prefabs (18 bursts + 3 ambient), 18 registered, 3 ambient slots linked, gallery saved;
      Main.unity stayed clean
- [x] Verified: TSA `startFrame` is normalized (probe: 0.5 → frame 4); real LitParticle/GlowParticle shaders picked up
- [ ] Look iteration (glow colours were clipping to white: saturated palette + intensities ≤ 1.8 applied, re-render pending)
- [ ] Ambient renders at the gameplay camera pose (640×360)
- [ ] Final commit + report

## How to run
`Tools/editor_lock.sh vfx bash -c 'source Docs/BilliardRogue/research/unity-cli-helpers.sh; urecompile 300; ueval "return Nex.BilliardRogue.Editor.VfxPrefabsBuilder.Run();" 240000'`
Menu: `Nex/Billiard Rogue/VFX Prefabs`. Preview stills: scratch `run_script` (VfxPreview.cs in the agent scratchpad; Simulate +
Camera.Render in a preview scene, HDR forced on for the duration and restored).

## Decisions
- Recipes are code (editor data tables) — prefabs are regenerated, never hand-tuned (TDD §13). Pool sizes live in the recipes and
  are written to the VfxManager registry.
- Root ParticleSystem = first layer; its duration is stretched to the latest child end so VfxManager's root stop callback never
  hides a child mid-flight. Children stopAction None; bursts AlwaysSimulate (off-screen bursts still return to the pool).
- playOnAwake false for pooled bursts (VfxManager.Get plays after moving); ambient: loop + prewarm + playOnAwake, root at the arena
  origin, emitter boxes over the arena; ambient floor plane is a static child at local y 0 (no helper).
- Scaling mode Hierarchy on every system so `PlayVisualEffect(..., scale)` scales children too.
- Glow colours saturated, intensities 1.25–1.8: only the dominant channel crosses the bloom threshold, hue survives tonemapping.
- Materials: one per shading × sheet (`M_Vfx_{Lit|Glow}_{Sheet}`) shared by all prefabs (batching); fallback URP particle shaders
  only if the Rendering shaders are missing.

## API exposed
- Prefabs `Assets/Prefabs/BilliardRogue/Vfx/Vfx_<VisualEffect>.prefab` (root ParticleSystem), `Vfx_Ambient_Act{1,2,3}.prefab`.
- `VfxManager.effectSpecs` filled for every key. `ActDefinition.ambientParticlesPrefab` filled when empty.
- `Nex.BilliardRogue.VfxFloorPlane` (runtime, internal to the prefabs).

## Requests (files I do not own)
(see final report)
