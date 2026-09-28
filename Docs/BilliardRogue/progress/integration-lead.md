# Integration lead — progress checkpoint

Owner: integration lead (owns the Editor between lock sections; the localization lead runs in parallel and only
touches `Localization/**`). Resume from here after a usage-limit kill: read this file + `git log --oneline -15`.

## Done

- [x] Staging sync (`Tools/sync_staging.sh`): only sprite timestamps changed; content dirs were already in place
      (untracked until ImportSettingsBuilder has rewritten their .meta, then committed).
- [x] Staged editor scripts moved into `Editor/`: `AudioRegistryBuilder`, `FontAssetsBuilder`, `ImportSettingsBuilder`,
      `TmpStaticFontAssetBuilder`. The staged `EnvironmentLayout.cs` was DROPPED: Presentation-World committed its own
      reader with the same name that `EnvironmentBuilder` is written against.
- [x] Requests applied (compile_check --warnings clean):
  - Input 1: `BilliardRogueCoordinator.Inputs.cs` builds one shared `ShotInputContext` and calls
    `ShotInputRouter.Initialize(playerIndex, engine, ctx)`; coordinator caches `rules` (`RulesFactory.Build`).
  - Input 2: `DebugSettings.forceDebugInput` `[DebugOrder(23)]`.
  - Rendering 1: coordinator `worldCameraRig` + `rootCamera` fields (wired by MainSceneBuilder), `Initialize(rootCamera)`
    + `SetPose(ArenaConfig camera)` in `StartMain` before the first view push; `worldDisplay` field removed
    (`worldCameraRig.Display` is the single owner).
  - Presentation-World 1–3: MainSceneBuilder instantiates `WorldLighting.prefab` + `EnvironmentBuilder.WireScene(world)`;
    coordinator `actEnvironment` → `Initialize(config.Arena)` + `ApplyTitle(Acts[0], instant)` in StartMain, `ApplyTitle`
    in `HandleReturnedToTitle`; `GameplayView.ShowStageIntroAsync` calls `ApplyAct(Acts[actIndex])` (via
    `RunFlowContext.environment` → `GameplayViewContext.environment`).
  - Presentation-World 4: `BoardPresenter.arenaView` (wired by MainSceneBuilder `WireBoardPresenter`), danger level
    refreshed after `Rebuild`, after `Consume` with an `EnemyKilled`, after `PlayEnemyPhaseAsync`, 0 on `Clear`.
  - Presentation-World 6: `VolumeProfilesBuilder.DeepBlueNight` = the validated milder grade.
  - Presentation-World 5 / 7 / 8, Rendering 2–4, Presentation-Core 1–2 / 4, Flow 3, Gameplay 1 / 4, UI 2–3: verified
    already satisfied (layer applied by the builder once the layer exists; rendering assets committed; ImportSettingsBuilder
    handles Surfaces/Palette; targetFrameRate set; volume profile via rig; TryWorldToCanvas centre-relative; labelLayer
    full-stretch; settings are read live; unlock tier captured at run start; ChooseReward → RewardView.TryChoose).
  - Presentation-Core 3 vs Gameplay 3 (conflict, TDD §8 tie-break: the presenter turns SimEvents into SFX): removed the
    doubled StageClear / BossAppear / Defeat / Victory stingers from `TurnController` and the batch EnemyStep / PlayerHurt
    SFX from `EnemyPhaseRunner`; the presenter's sequences / event player keep them (in sync with the visuals).
  - UI 4: `LocKeys.Reward.Hint` (◀ ▶) removed; UI 5: TDD §9 PauseView Controls = Back.
  - Build All: `VolumeProfilesBuilder` added, RenderPipeline before Materials, LocalizationSeeder before FontAssets;
    `BilliardRogueMenu.RunAll()` returns a summary and keeps going when a builder throws. TDD §17 order updated.
  - `CalibrationView.camera` renamed `cameraSession` (CS0108 warning).

## In progress / next

1. Editor (lock `integration`): recompile → commit the 4 builder scripts + minted .meta.
2. `ImportSettingsBuilder.Run()` first (reimports the synced content), then commit content dirs + .meta.
3. `BilliardRogueMenu.RunAll()` end-to-end; fix builder errors/warnings; Main.unity index 0 opens clean; commit generated assets.
4. EditMode tests (Simulation, InputCore, Rendering, Environment, ArenaLayout); console clean; compile_check --warnings.
5. Re-run LocalizationSeeder → FontAssetsBuilder → UiViewsBuilder → WorldPrefabsBuilder once the seeder lands.

## Findings

- Nobody initialised `ArenaLayout` at runtime before this wave; `ActEnvironmentController.Initialize` (now called in
  StartMain) does it through `ArenaView.Initialize`.
- `BoardPresenter.Initialize` runs once per GameplayView (per run); the shaker base is re-captured at `Rebuild`.
