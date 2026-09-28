# Presentation-Core progress (TDD §8, §16, §17)

Owner paths: `Starter/Assets/Scripts/BilliardRogue/Presentation/**` (except ArenaLayout.cs, ActEnvironmentController.cs, Environment/**),
`Configs/JuiceConfig.cs`, `Configs/FieldObjectCatalog.cs`, `Editor/WorldPrefabsBuilder.cs` (+ helper classes `Editor/WorldPrefabModels.cs`,
`Editor/BoardPrefabBuilder.cs`), `Assets/Prefabs/BilliardRogue/{Enemies,Balls,Board,Player}/**`, `World/BoardPresenter.prefab`.

## Status
- [ ] JuiceConfig additions (nested settings groups), LocKeys.Presentation.cs
- [ ] Views: EnemyView (+EnemyIdleMotion, EnemyStatusVisuals), BallView, FieldObjectView, PickupView, CatView, AimGuideView
- [ ] Labels: WorldLabelLayer, WorldLabel, DamageNumber, FloatTextCache, ComboPresenter
- [ ] CameraShaker, pools (EnemyViewPool, BallViewPool, FieldObjectViewPool, PickupViewPool, WorldLabelPool, DamageNumberPool)
- [ ] BoardPresenter + BoardViews + BoardEventPlayer + EnemyPhasePlayer + BoardSequencePlayer
- [ ] WorldPrefabsBuilder / WorldPrefabModels / BoardPrefabBuilder
- [ ] compile_check green, Editor recompile + builder run inside the lock, prefab inspection
- [ ] commits

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
