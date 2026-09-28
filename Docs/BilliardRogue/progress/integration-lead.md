# Integration lead — progress checkpoint

Owner: integration lead (owns the Editor between lock sections; the localization lead runs in parallel and only
touches `Localization/**`, `Editor/LocalizationSeeder.cs` and the LocalizationTable / string-table Addressable groups).
Resume from here after a usage-limit kill: read this file + `git log --oneline -15`.

## Status: integration wave complete (Build All 19/19, tests 82/82, console clean)

- [x] Staging sync (`Tools/sync_staging.sh`): only sprite timestamps changed; content dirs committed with the .meta
      ImportSettingsBuilder rewrote (514851da).
- [x] Staged editor scripts moved into `Editor/` (db59a017): `AudioRegistryBuilder`, `FontAssetsBuilder`,
      `ImportSettingsBuilder`, `TmpStaticFontAssetBuilder`. The staged `EnvironmentLayout.cs` was DROPPED: Presentation-World
      committed its own reader of that name that `EnvironmentBuilder` is written against.
- [x] Requests applied (4815b84d), compile_check --warnings clean:
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
    already satisfied (World layer applied by the builder once the layer exists; rendering assets committed;
    ImportSettingsBuilder handles Surfaces/Palette; targetFrameRate set; volume profile via rig; TryWorldToCanvas
    centre-relative; labelLayer full-stretch; settings are read live; unlock tier captured at run start;
    ChooseReward → RewardView.TryChoose).
  - Presentation-Core 3 vs Gameplay 3 (conflict, TDD §8 tie-break: the presenter turns SimEvents into SFX): removed the
    doubled StageClear / BossAppear / Defeat / Victory stingers from `TurnController` and the batch EnemyStep / PlayerHurt
    SFX from `EnemyPhaseRunner`; the presenter's sequences / event player keep them (in sync with the visuals).
  - UI 4: `LocKeys.Reward.Hint` (◀ ▶) removed; UI 5: TDD §9 PauseView Controls = Back.
  - Build All: `VolumeProfilesBuilder` added, RenderPipeline before Materials, LocalizationSeeder before FontAssets;
    `BilliardRogueMenu.RunAll()` returns a summary and keeps going when a builder throws. TDD §17 order updated.
  - `CalibrationView.camera` renamed `cameraSession` (CS0108 warning).
- [x] Build All fixes (e545571e): `RenderPipelineBuilder.TuneQualityLevels` used Unity 5's `anisotropicFiltering`
      (Unity 6: `anisotropicTextures`) and now skips + reports unknown properties; `ImportSettingsBuilder` no longer forces
      the already-mono SFX to mono (that re-normalized their peaks); `AudioRegistryBuilder` tolerates ADPCM block padding
      (64 samples) and ±1.5 dB quantization.
- [x] `BilliardRogueMenu.RunAll()` → 19/19 builders (LocalizationSeeder 208 rows × 5 locales, CJK atlas 941 chars,
      UiViews no missing keys, MainSceneBuilder roots 7, build index 0). Main.unity reopened: not dirty, 0 errors.
- [x] EditMode tests `Nex.BilliardRogue*`: 82/82 (Simulation, InputCore, Rendering, Environment, ArenaLayout).
- [x] Generated assets committed (this commit).

## Remaining for the playable pass

- `AudioRegistryBuilder` still reports `BallLaunch_1/2.wav` ~3 dB quieter than built: ADPCM flattens their attack.
  Audio owner: re-export with a softer attack or import those two as PCM.
- Unity/package warnings during prefab saves (`[SerializeReference]` on `SmartStringColumn`, `MMF_Position`,
  `MMF_CanvasGroup`) are not ours.
- No play mode was entered in this wave (per rules). First playable checks: title → calibration (`DebugHooks.SkipCalibration`)
  → gameplay with `DebugSettings.forceDebugInput` / `autoAimBot`, act fade behind the stage intro, danger-row glow, shake
  under texel snapping, stingers once per sequence.
- Re-run `LocalizationSeeder` → `FontAssetsBuilder` → `UiViewsBuilder` (Build All does) after the localization lead's
  final strings land; the 2 file-only keys the seeder reports (`br.ui.calibration.leftPawTag/rightPawTag`) are theirs.

## Findings

- Nobody initialised `ArenaLayout` at runtime before this wave; `ActEnvironmentController.Initialize` (now called in
  StartMain) does it through `ArenaView.Initialize`.
- `BoardPresenter.Initialize` runs once per GameplayView (per run); the shaker base is re-captured at `Rebuild`.
- Build All is fast (≈3–7 s) once content is imported: every builder is idempotent; UiViews prefabs get new child IDs
  on each rebuild (known, UI-Views report), so their files always diff.
