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
- [ ] IN PROGRESS: wait for green compile_check → inside the editor lock: `urecompile` (first import of Models/Sprites/Audio),
      `ueval 'return Nex.BilliardRogue.Editor.WorldPrefabsBuilder.Run();'`, inspect prefabs, commit prefabs + .meta files
- [ ] Final report (Requests: TryWorldToCanvas convention, rig parent for CameraShaker, stingers played by presenter)

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

## Requests
(filled at the end)
