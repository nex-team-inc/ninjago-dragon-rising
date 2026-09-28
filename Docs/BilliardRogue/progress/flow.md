# Flow module — progress checkpoint

Owner: Flow & camera session agent (TDD §2, §9, §0a D1/D5/D7). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Flow Starter/Assets/Scripts/BilliardRogue/Editor/Flow* Starter/Assets/Scripts/BilliardRogue/Editor/MainSceneBuilder.cs`.

## Status

- [x] Runtime: CameraSession, LocaleRestore, PendingFlowState, BilliardRogueInitializer, GameplayPip, GameplayHudRelay, GameplayViewContext
- [x] Runtime: CalibrationView (+ CalibrationTutorialIllustration), GameplayView (+ Overlays, typed against UI-Views)
- [x] Runtime: BilliardRogueCoordinator (+ Views / Inputs / Debug partials) + RunFlow/RunFlowContext, LocKeys.Flow.cs
- [x] Editor: FlowUiFactory, FlowPrefabsBuilder, FlowViewPrefabsBuilder, MainSceneBuilder (compile_check green)
- [ ] Editor run (inside lock): recompile, builders, open Main.unity, hierarchy check, commit generated assets + .meta
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
  run end = pop overlays until Gameplay, then ReplaceView(Summary). Flow owns `RunPersistence.CompleteRun` (needs
  `newRecord` for SummaryView) — GameSession must not call it.
- Camera session starts after the CalibrationView is pushed (view shows "Camera setup" while the MDK boots);
  it is stopped on Back from calibration, at run end, and on Save & Quit.
- D1 fallback: `CameraSession.reloadSceneOnPlayerCountChange` (prefab flag, default off) → coordinator stashes a
  `PendingFlowState` in `PlayerDataManager.appViewState.NextViewState`, shows the blocker and reloads Main.unity.

## Requests (for the integrator)

(filled in the final answer)
