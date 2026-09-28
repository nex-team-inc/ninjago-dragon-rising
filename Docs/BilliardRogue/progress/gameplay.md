# Gameplay module progress (orchestration + persistence glue)

Owner paths: `Starter/Assets/Scripts/BilliardRogue/Gameplay/**`, `Persistence/**`, `Configs/PacingConfig.cs`,
`Configs/BalanceConfig.cs`, `Editor/GameplaySmokeRunner.cs`, `Editor/GameplaySmokeFakes.cs`.

Status: **complete and verified** (commits a1e79d8c, 5ace2b06, 5a78f5ca, 1b6eca36 + final polish).

## Done

- **TimeScaleController** (full): single writer of `Time.timeScale`; pause > hit-stop > slow-mo × fast-forward × phase
  scale; `Tick(unscaledDt)` driven by GameSession (works in edit mode); `Initialize(ffScale)`, `SetPhaseScale`,
  `ResetEffects`; restores 1 on destroy.
- **GameSession** (full): `Initialize(ctx)`, `RunAsync(ct)` (UniTaskCompletionSource over a polled machine),
  `RequestPause`, `RequestSaveAndQuit`, public `Tick(dt)` (Update calls it), `Phase`, `IsRunning`, `IsPaused`,
  `NewRecord`, `Saved` event, platform pause binding (`CherryIntegrationManager.PreferGameStopped` → save if stable +
  pause), DebugHooks registration (Shoot, GotoStage, KillAll, ClearStage, AddEveryBall, State), quit guard.
- **TurnController**: polled state machine StageIntro → PlayerTurn → (TrackingLost) → EnemyPhase → StageClear →
  Reward | Defeat | Victory → Finished. Saves: stage start (BeginRun on the first), turn start, reward roll, reward
  pick. Analytics at every boundary. fixedSeed / forceStartStage before the first BeginStage. Continue:
  `awaitingReward` → Reward (re-displays `pendingRewards`), else StageIntro → PlayerTurn on the saved board.
- **EnemyPhaseRunner**: god mode (unreachable HP for the resolve, restore, strip PlayerDamaged/PlayerDied),
  EnemyStep / PlayerHurt batch SFX, fastEnemyPhase via `SetPhaseScale`, turn_end analytics.
- **RewardFlow**: roll (+ forceRewardBall card 0, unlockAllBalls), save before and after the pick.
- **PlayerTurnLoop**: tracking check, aim preview (PredictPath → BoardPresenter.SetAim), strike → Launch,
  fixed-step sim from `GameplayTimeScale`, drain → Board.Consume / TurnFeedback / ShotResultTracker, straggler
  fast-forward (+ HUD chip), turn end (nothing to shoot or stage cleared, no balls, grace).
- **ShotSequencer**: bag order, extra balls (Basic L1, reset at turn end), alternating shooters via
  `run.activePlayerIndex`, cooldown, infinite-balls cheat cycles the bag (turn ends when the board is empty).
- **ShotResultTracker**: per-ball `RunAnalytics.ShotResult` (minis → parent, kills → last hitter).
- **TurnFeedback**: hit-stop on kills / crits / boss hits, slow-mo on the last enemy, boss analytics, HUD dirty flags.
- **HudBinder** (change-detected pushes), **BoardDriver** (headless no-op), **SessionAudio** (BGM crossfade per
  act/boss/reward, stingers, TurnStart/LowHpWarning SFX), **SessionAnalytics** (nullable RunAnalytics),
  **PauseState**, **RunSimulation** (sim facade; only SimRandom creator), **SessionServices**.
- **Persistence**: `IRunStore` + `PlayerDataRunStore`; `RunPersistence(IRunStore)` ctor + `Saved` event (additive).
- **PacingConfig** additions: `turnEndGrace`, `lowHpFraction`, `aimGuideLengths[3]`, `aimGuideMaxBounces`,
  `aimGuideMaxPoints`, `bgmCrossfadeSeconds` (+ `AimGuideLength(setting)`).
- **Editor/GameplaySmokeRunner** (+ fakes): headless edit-mode run with a PredictPath-scored shooter, fake HUD /
  flow host, in-memory JSON store. Scenarios: new run → stage 2 turn 2 (KillAll hook) → crash → load → continue →
  clear stage 2 → Save & Quit keeps the save; boss stage via forceStartStage + GotoStage with god mode /
  unlockAllBalls / AddEveryBall / State hooks. Menu `Nex/Billiard Rogue/Gameplay Smoke Run`; CLI
  `ueval 'return Nex.BilliardRogue.Editor.GameplaySmokeRunner.Run(seed, players, maxTicks);'`.

## Verification

- `python3 Tools/compile_check.py` green (runtime + editor, no warnings in my files).
- Editor (inside `Tools/editor_lock.sh gameplay`, script `<scratchpad>/modules/gameplay/run_smoke.sh`):
  - seed 1234 / 1P: `OK | new-run: ticks=1686 turns=5 shots=27 hits=59 kills=19 hp=30 reward=NewBall:Frost bag=5
    saves=9 fastForwards=4 | continue: resumedTurn=2 ticks=2652 turns+=7 shots+=41 hits+=113 kills+=22 hp=30
    stageNumber=2 phase=StageIntro | boss: ticks=836 turns=0 shots=13 kills=1 bag=12 hp=30 stageNumber=3`
  - seed 777 / 2P: `OK | new-run: ticks=3417 turns=9 shots=43 hits=96 kills=27 ... | continue: resumedTurn=2
    turns+=5 shots+=30 hits+=50 kills+=14 stageNumber=2 | boss: ... stageNumber=3` (shooter banners seen).
  - No dirty scenes afterwards; ~10 s per run.

## Decisions

- Polled state machine (no dependence on the UniTask player loop) so the edit-mode smoke runner ticks synchronously.
- `GameSessionContext.headless` + `debugSettings` + nullable `analytics` (additive) make the session runnable without
  scene singletons.
- BeginStage spawn events are cleared, not consumed: `BoardPresenter.Rebuild(run)` recreates everything.
- Enemy-phase events live in `RunSimulation.PhaseEvents`, handed to `PlayEnemyPhaseAsync` and left untouched until
  the next enemy phase; HUD HP after the phase is pushed when playback completes.
- Low-HP warning is a repeated one-shot cue (turn start while low, and when damage crosses the line): SfxManager has
  no loop API.

## Requests (for the integrator)

See the final answer (Input: do not also register `DebugHooks.ShootHandler`; UI RewardView registers
`ChooseRewardHandler`; Presentation must not duplicate PlayerHurt / EnemyStep SFX).
