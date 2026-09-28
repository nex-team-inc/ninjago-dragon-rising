# Flow module — progress checkpoint

Owner: Flow & camera session agent (TDD §2, §9, §0a D1/D5/D7). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Flow Starter/Assets/Scripts/BilliardRogue/Editor/Flow* Starter/Assets/Scripts/BilliardRogue/Editor/MainSceneBuilder.cs`.

## Status

- [x] Runtime: CameraSession, LocaleRestore, PendingFlowState, BilliardRogueInitializer, GameplayPip, GameplayHudRelay, GameplayViewContext
- [x] Runtime: CalibrationView (+ CalibrationTutorialIllustration), GameplayView (+ Overlays, typed against UI-Views)
- [x] Runtime: BilliardRogueCoordinator (+ Views / Inputs / Debug partials) + RunFlow/RunFlowContext, LocKeys.Flow.cs
- [x] Editor: FlowUiFactory, FlowPrefabsBuilder, FlowViewPrefabsBuilder, MainSceneBuilder (compile_check green)
- [x] Editor run (inside lock): recompile OK, FlowPrefabsBuilder + MainSceneBuilder OK, Main.unity opened (7 roots, not dirty, 0 console errors), generated assets + .meta committed
- [x] UI-Views seams filled (typed TitleView/PlayerModeView/SummaryView/SettingsView/StageIntro/Reward/TrackingLost/Pause)
- [ ] Input seam (`BilliardRogueCoordinator.Inputs.cs`): still generic (prefab → first IShotInput, NullShotInput fallback) — Input module not landed

## Decisions

- UI-Views landed mid-way: `BilliardRogueCoordinator.Views.cs` and `GameplayView.Overlays.cs` are wired against the
  real view APIs. `BilliardRogueCoordinator.Inputs.cs` stays a seam until the Input module lands (instantiates
  PlayerShotInput.prefab and takes its first IShotInput; NullShotInput when the prefab is missing).
- Class-size rule: run lifecycle lives in `RunFlow` (plain class, `RunFlowContext`), coordinator = boot + title menu.
- GameSession (Gameplay module) calls RunPersistence.CompleteRun and logs pause analytics itself; Flow reads
  `GameSession.NewRecord` through `GameplayView.RunEnded(run, outcome, newRecord)` and never duplicates them.
- GameplayDebugCommands overwrites DebugHooks.StateHandler during a run; RunFlow's `runEnded` callback re-registers.
- GameplayView receives ready-made `IShotInput[]` from the coordinator (no Input-module type inside the view);
  CalibrationView receives a `Func<int, OnePlayerDetectionEngine, Transform, IShotInput?>` factory for the test strike.
- `GameplayHudRelay : IGameplayHud` forwards to the nested GameplayHud (found via `GetComponentInChildren<IGameplayHud>`)
  and mirrors `SetActivePlayer` into the PiP; it is a no-op HUD when the UI prefab is missing (warning).
- PiP (`GameplayPip.prefab`) is its own Screen Space Overlay canvas root (D5), instantiated/destroyed by GameplayView.
- Gameplay entry = transaction: pop until Title is top, then PushView(Gameplay) (handles both New Run and Continue);
  run end = pop overlays until Gameplay, then ReplaceView(Summary). `RunPersistence.CompleteRun` is called by
  GameSession (Gameplay module); Flow only reads `newRecord` and snapshots `highestUnlockTier` at run start for the
  summary's unlock row.
- Camera session starts after the CalibrationView is pushed (view shows "Camera setup" while the MDK boots);
  it is stopped on Back from calibration, at run end, and on Save & Quit.
- D1 fallback: `CameraSession.reloadSceneOnPlayerCountChange` (prefab flag, default off) → coordinator stashes a
  `PendingFlowState` in `PlayerDataManager.appViewState.NextViewState`, shows the blocker and reloads Main.unity.

## Requests (for the integrator)

- Input module: replace `BilliardRogueCoordinator.Inputs.cs` body with the real PlayerShotInput initialization
  (paw/debug/bot + router) using (playerIndex, engine, config.Control, PlayerPreference.leftHandedCue); then re-run
  FlowPrefabsBuilder so `playerShotInputPrefab` and the hidden engine variant get wired.
- LocalizationSeeder: add the keys from `Flow/LocKeys.Flow.cs` (Starting, TutorialHint, StrikeSuccess, AllReady, PipTitle).
- Build All order matters: RenderPipelineBuilder (World layers) → … → UiViewsBuilder → FlowPrefabsBuilder → MainSceneBuilder
  (the Main Camera culling mask and the UI/Input prefab slots are only filled when those assets exist).
