# Review fixer "core" — checkpoint

Areas: simulation, gameplay, presentation, config, vfx, audio, rendering. Findings source:
`scratchpad/review_findings.json` (20 of 40 are mine; the numbers below index that filtered list in file order).

## Status

| # | Sev | Finding | Status |
|---|---|---|---|
| 0 | major | Outcome saved only in Finish (Save & Quit / kill during the enemy phase or defeat/victory sequence escapes it) | fixed: TurnController commits the outcome (`CommitOutcome` → `RunPersistence.CompleteRun`) right after `EnemyPhaseRunner.Begin` decides a defeat and before `EnterVictory`; otherwise saves after the phase (`SaveAtBoundary`, EnterPlayerTurn skips its own) and at the stage clear when a reward follows; `Abandon()` finishes with a decided outcome and `GameSession.Abandon` completes with `turns.Result` |
| 1 | minor | Firing after the stage fell mid-turn | fixed: `PlayerTurnLoop.CanFire` = sequencer CanFire && !StageCleared (Tick + ForceShoot) |
| 2 | minor | Reward after a boss kill rolled with the old unlock tier; summary unlock row after a continue | fixed: `RunPersistence.RefreshUnlocks` called at the start of `RewardFlow.Offer` (the stage-clear save also precedes it); `RunState.unlockTierAtRunStart` set in `RunFlow.CreateNewRun`, read for the summary |
| 3 | minor | Continue at a reward skips the HUD stage label / act look | fixed: continue branch pushes `Hud.SetStage`/`RefreshAll`; `GameplayView.ChooseRewardAsync` applies `Acts[run.actIndex]` (no-op when shown) |
| 4 | minor | Telegraph icons missing after a continue | fixed: `EnemyPhaseResolver.PendingTelegraph(rules, e)` (public, pure) + `BoardPresenter.Rebuild → RestoreTelegraphs` |
| 5/11 | minor/major | tracking_lost logged twice | fixed by the flow fixer while this pass ran (TurnController call removed, view keeps the real duration); verified |
| 6 | minor | fastEnemyPhase applied twice (16×) | fixed: `EnemyPhasePlayer` no longer scales its waits; the phase scale in `TimeScaleController` is the single route |
| 7 | minor | 2P hand-off after the turn's last ball | fixed: `PlayerTurnLoop.TryHandOff` passes the cue only while `HasBallToFire`, retried after each drain (a +1 Ball pickup reopens the turn) |
| 8/12 | minor/major | turn_end missing for the stage-clearing turn | fixed: `EnemyPhaseRunner.EndWithoutPhase()` from `EnterEnemyPhase`'s cleared branch (turn = turnInStage + 1, matching TurnStart) |
| 9 | major | Strike consumed before the cooldown gate | fixed by the input fixer while this pass ran (`CanFire && TryConsumeStrike`); verified, covered by `StrikeDuringTheCooldownStaysPendingAndFiresAfterIt` |
| 10 | minor | Tracking check after the last ball; warning left on at turn end | fixed: `UpdateTracking` only while `WantsStrike`, else `ClearTrackingWarning()`; also cleared in `EndTurn` |
| 13 | major | Ambient particles bypass VfxManager | in progress |
| 14 | minor | Enum-indexed pool arrays instead of EnumDictionary | deferred: needs `WorldPrefabsBuilder`/`BoardPrefabBuilder` changes and a rebuild of BoardPresenter.prefab, and WorldPrefabsBuilder.cs is being edited/run by the content integrator right now |
| 15 | minor | Hard-coded tunables | fixed: `BallLevelStats.splitFanDegrees/miniRadiusScale`, `ArenaRules.pickupInset/portalInset/mudInset` (class defaults = old constants, assets pick them up), `ControlConfig.sustainSpeedFraction` → `StrikeSettings.sustainSpeedFraction` |
| 16 | minor | English fallbacks in FloatTextCache / SetupWarningMessage | fixed: table only, empty until loaded, `LogError` for a missing key |
| 17 | minor | showSimDebug unread | fixed: `GameplayDebugCommands.Tick` prints a "sim" DebugPrinter line (phase, stage/turn, shots, in-flight, enemies, HP), guarded, skipped headless |
| 18 | minor | VolumeManager debounced save lost on Home/quit | fixed: `Flush()` from `OnApplicationPause(true)` and `OnApplicationQuit` (debounce kept: slider drags must not rewrite the file per tick) |
| 19 | minor | #region rule | fixed: TiltShiftPass, StaticMeshCombiner, EnvironmentLooks got regions; BilliardRogueInitializer lost its two |

## Tests added

- `Simulation/Tests/TelegraphTests.cs` (4): cadence-2 caster warns only the phase before, frozen/dead/cadence-1 show nothing, spawn wins when both are due, and a resolver round-trip (`PendingTelegraph` == the telegraph the phase emitted).
- `Editor/Tests/GameplaySessionTests.cs` (8, headless GameSession over the real config): enemy phase saved before it plays; stage clear saves the reward point; defeat committed when the sim decides it and Save & Quit keeps it; Save & Quit before the decision stays Abandoned; cooldown strike stays pending and fires after; no shot after the stage fell mid-turn; 2P hand-off only while a shot is left; tracking not required once the last ball is fired.

## Shared-file note

Other fixers had uncommitted hunks in files this pass touched. Committed here with their self-contained hunks: PlayerTurnLoop.cs (input fixer's cooldown gate), GameSession.cs (flow fixer's platform-pause save-only). Left uncommitted because they depend on the flow fixer's other uncommitted files: `GameplayView.Overlays.cs` (my ApplyAct hunk in ChooseRewardAsync), `RunFlow.cs` (my unlockTierAtRunStart hunk) — the flow fixer's commit carries them.

## Log

- Verified all 20 findings against the current code; 2 already fixed by parallel fixers, 1 deferred.
- compile_check green; Editor recompile clean; EditMode: Simulation 56/56, InputCore 19/19, GameplaySessionTests 8/8.
