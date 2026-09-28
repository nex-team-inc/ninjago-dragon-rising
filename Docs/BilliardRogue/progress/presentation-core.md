# Presentation-Core progress (TDD §8, §16, §17)

Owner paths: `Starter/Assets/Scripts/BilliardRogue/Presentation/**` (except ArenaLayout.cs, ActEnvironmentController.cs, Environment/**),
`Configs/JuiceConfig.cs`, `Configs/FieldObjectCatalog.cs`, `Editor/WorldPrefabsBuilder.cs` (+ helper classes `Editor/WorldPrefabModels.cs`,
`Editor/BoardPrefabBuilder.cs`), `Assets/Prefabs/BilliardRogue/{Enemies,Balls,Board,Player}/**`, `World/BoardPresenter.prefab`.

## Status
- [x] JuiceConfig additions (nested settings groups), LocKeys.Presentation.cs (commit 8ddaa2cb)
- [x] Views: EnemyView (+EnemyIdleMotion, EnemyStatusVisuals), BallView, FieldObjectView, PickupView, CatView, AimGuideView
- [x] Labels: WorldLabelLayer, WorldLabel, DamageNumber, FloatTextCache, ComboPresenter (commit 7a99e4c8)
- [x] CameraShaker, pools (EnemyViewPool, BallViewPool, FieldObjectViewPool, PickupViewPool, WorldLabelPool, DamageNumberPool)
- [x] BoardPresenter + BoardViews + BoardEventPlayer + EnemyPhasePlayer + BoardSequencePlayer
- [x] WorldPrefabsBuilder / WorldPrefabModels / BoardPrefabBuilder written (runtime compiles; editor assembly pending a
      green tree — other modules had transient errors at the time)
- [x] Editor: recompile OK, `WorldPrefabsBuilder.Run()` OK → 12 enemy prefabs, Ball, 4 field objects, 3 pickups, Cat_Hero,
      WorldLabel/DamageNumber/WorldLabelLayer, World/BoardPresenter; 43 config slots filled (EnemyDefinition.prefab/icon,
      BallDefinition.icon, FieldObjectCatalog). Expected warnings: World layer, M_Palette/M_GlowParticle/M_AimGuide, TMP fonts
      missing → fallbacks (`Prefabs/BilliardRogue/Board/Fallback/M_Fallback_*.mat`, default TMP font). Re-run after
      MaterialsBuilder / RenderPipelineBuilder / FontAssetsBuilder / ImportSettingsBuilder to pick up the real assets.
- [x] Builder wraps every FBX instance in a "Model" container: a default import merges the single top node into the FBX root
      (no `Body` child) — bobPart falls back to the FBX root; with preserveHierarchy (ImportSettingsBuilder) the named parts are found.
- [ ] Commit prefabs + .meta + config assets; final report (Requests: TryWorldToCanvas convention, rig parent for CameraShaker,
      stingers played by presenter sequences, Build All order)

## Next steps if resuming
1. `python3 Tools/compile_check.py` must be green (editor assembly included). Fix any error in my Editor files.
2. `Tools/editor_lock.sh presentation-core bash -c 'source Docs/BilliardRogue/research/unity-cli-helpers.sh; urecompile 600'`
3. `Tools/editor_lock.sh presentation-core bash -c '... ueval "return Nex.BilliardRogue.Editor.WorldPrefabsBuilder.Run();" 300000'`
4. Inspect: `ueval` loading Assets/Prefabs/BilliardRogue/World/BoardPresenter.prefab, Enemies/Enemy_Slime.prefab (children, components).
5. `git add Starter/Assets/Prefabs/BilliardRogue Starter/Assets/Scripts/BilliardRogue/Presentation/*.meta Starter/Assets/Scripts/BilliardRogue/Editor/{WorldPrefabModels,WorldPrefabsBuilder,BoardPrefabBuilder}.cs.meta` + commit.

## Decisions
- No DOTween in per-event paths: views run small manual tweens in Update (no allocations); sequences use UniTask.Delay (scaled time).
- Enemies face the camera: view root rotation = layout.rotation * Euler(0,180,0). Model local +Z = sim "Bottom" face.
- Emissive parts: same M_Palette material (its `_EMISSION` + Palette_Emission drive the swatches); views raise `_EmissionStrength`
  through a MaterialPropertyBlock on parts the builder tagged emissive.
- Labels/damage numbers are UI (TMP) under the label layer, re-projected every LateUpdate with `PixelWorldDisplay.TryWorldToCanvas`
  (label anchors at the rect centre, `anchoredPosition = anchored + offsetPx`).
- CameraShaker offsets the world camera *rig* (camera's parent when present, else the camera) along its right/up/forward from a base
  captured at Initialize; trauma-based, respects `PlayerPreference.screenShake`.
- Pool layer: pools apply the `World` layer recursively to every instance they create (works even if the layer did not exist at build time).

## API exposed (other modules call)
See BoardPresenter.cs (contract kept; additive: `SetNextBall(BallType, int level)`, `SetCameraRig(Transform)`).

## Requests (files I do not own)
1. Rendering `PixelWorldDisplay.TryWorldToCanvas(world, canvasRect, out anchored)`: WorldLabelLayer places labels whose
   anchors/pivot sit at the rect centre (0.5, 0.5): `rt.anchoredPosition = anchored + offsetPx`. If the implementation returns
   bottom-left-relative coordinates, subtract `canvasRect.rect.size * 0.5f` there (or tell me and I adapt `WorldLabelLayer.Project`).
2. Rendering/Flow (WorldCameraRig): `CameraShaker` offsets the world camera's **parent** transform (rig) when one exists, else the
   camera itself, from a base captured in `BoardPresenter.Initialize`. Keep the WorldCamera under a rig root that nothing else
   moves; if PixelWorldDisplay snaps the camera transform every frame, snap from the current pose (do not restore a stored base).
3. Gameplay: `BoardPresenter.PlayStageClearAsync / PlayBossIntroAsync / PlayDefeatAsync / PlayVictoryAsync` already play the
   BgmManager stingers (StageClear, BossAppear, Defeat, Victory) and the matching SFX — do not play them again.
4. Flow (GameplayView): pass a full-stretch `RectTransform` on the gameplay canvas as `labelLayer`; the presenter instantiates
   `WorldLabelLayer.prefab` under it. Call `BoardPresenter.Initialize` after `PixelWorldDisplay.Initialize` (needs `WorldCamera`).
5. Integration: re-run `WorldPrefabsBuilder` after MaterialsBuilder, RenderPipelineBuilder, FontAssetsBuilder and
   ImportSettingsBuilder (Build All order already does) so the fallback materials / default font / Default layer get replaced.
   `BallDefinition.material` stays null until `M_Ball_<Type>.mat` exist (BallView tints the base material through an MPB meanwhile).
