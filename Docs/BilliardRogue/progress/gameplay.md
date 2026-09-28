# Gameplay module progress (orchestration + persistence glue)

Owner paths: `Starter/Assets/Scripts/BilliardRogue/Gameplay/**`, `Persistence/**`, `Configs/PacingConfig.cs`,
`Configs/BalanceConfig.cs`, `Editor/GameplaySmokeRunner.cs`, `Editor/GameplaySmokeFakes.cs`.

## Done

- **TimeScaleController** (full): single writer of `Time.timeScale`; pause > hit-stop > slow-mo × fast-forward × phase
  scale; `Tick(unscaledDt)` driven by GameSession (works in edit mode); `Initialize(ffScale)`, `SetPhaseScale`,
  `ResetEffects`; restores 1 on destroy.
- **GameSession** (full): `Initialize(ctx)`, `RunAsync(ct)` (UniTaskCompletionSource, polled machine), `RequestPause`,
  `RequestSaveAndQuit`, `Tick(dt)` public (Update calls it), `Phase`, `IsRunning`, `NewRecord`, `Saved` event,
  platform pause binding (`CherryIntegrationManager.PreferGameStopped` → save if stable + pause), DebugHooks
  registration (Shoot, GotoStage, KillAll, ClearStage, AddEveryBall, State).
- **TurnController**: polled state machine StageIntro → PlayerTurn → (TrackingLost) → EnemyPhase → StageClear →
  Reward | Defeat | Victory → Finished. Saves: stage start (BeginRun on the first), turn start, reward roll, reward
  pick. Analytics at every boundary. God mode wraps the enemy phase (unreachable HP, restore, strip PlayerDamaged).
  fixedSeed / forceStartStage applied before the first BeginStage; forceRewardBall replaces card 0; unlockAllBalls;
  fastEnemyPhase via `SetPhaseScale`. Continue: `awaitingReward` → Reward phase (re-displays `pendingRewards`),
  else StageIntro → PlayerTurn on the saved board.
- **PlayerTurnLoop**: per-frame tracking check, aim preview (PredictPath → BoardPresenter.SetAim), strike → Launch,
  fixed-step sim from `GameplayTimeScale`, drain → Board.Consume / TurnFeedback / ShotResultTracker, straggler
  fast-forward (PacingConfig delay/scale + HUD chip), turn end (nothing to shoot or stage cleared, no balls, grace).
- **ShotSequencer**: bag order, extra balls (Basic L1), alternating shooters via `run.activePlayerIndex`, cooldown,
  infinite-balls cheat cycles the bag (turn ends when the board is empty).
- **ShotResultTracker**: per-ball aggregation → `RunAnalytics.ShotResult`; minis attributed to the parent; kills to
  the last hitter.
- **TurnFeedback**: hit-stop on kills/crits/boss hits, slow-mo on last enemy, boss analytics, HUD dirty flags.
- **HudBinder** (change-detected pushes), **BoardDriver** (headless no-op), **SessionAudio** (BGM crossfade per
  act/boss/reward, stingers, TurnStart/EnemyStep/PlayerHurt/LowHpWarning SFX), **SessionAnalytics** (nullable
  RunAnalytics), **PauseState**, **RunSimulation** (sim facade; the only SimRandom creator), **SessionServices**.
- **Persistence**: `IRunStore` + `PlayerDataRunStore`; `RunPersistence(IRunStore)` ctor added, `Saved` event added.
- **PacingConfig** additions: `turnEndGrace`, `lowHpFraction`, `aimGuideLengths[3]`, `aimGuideMaxBounces`,
  `aimGuideMaxPoints`, `bgmCrossfadeSeconds` (+ `AimGuideLength(setting)`).
- **Editor/GameplaySmokeRunner** (+ fakes): headless edit-mode run: new run → stage 2 turn 2 → crash → load →
  continue → clear stage 2 → Save & Quit keeps the save. `Run()` returns "OK ..." / "FAIL ...".

## In progress / next steps

1. Run the smoke runner in the Editor (inside `Tools/editor_lock.sh gameplay`) once the tree compiles
   (UI's `PauseView.cs` currently has a `UniTask.Forget` error — not mine).
2. Commit code + this file.

## Decisions

- Polled state machine (no dependence on the UniTask player loop) so the edit-mode smoke runner ticks synchronously.
- `GameSessionContext.headless` + `debugSettings` + nullable `analytics` (additive) make the session runnable without
  scene singletons.
- BeginStage spawn events are cleared, not consumed: `BoardPresenter.Rebuild(run)` recreates everything.
- HUD HP after the enemy phase is pushed when playback completes (presenter shows the hurt step at the right time).

## Requests (for the integrator)

See the final answer.
