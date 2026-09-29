# v2 spawning (GDD v2 §5) + hidden debug overlay (§6) — checkpoint

Owner key: `spawn` (editor lock label). Spec: `GDD-v2-Changes.md` §5–§6. Scratch: `<session scratchpad>/spawn/`.
The perf agent (`final-perf.md`) shares the Editor: short `Tools/editor_lock.sh spawn bash -c '...'` sections only.

## Plan / status
- [x] 1. Simulation: batch plan (`StagePlan.batches`, `SpawnBatch`), `BoardOps.SpawnBatch` (random free cells rows
      0..rows-1-spawnForbiddenNearRows, SimRandom), cadence + skip-empty-turn in `EnemyPhaseResolver`, stage clear,
      boss stage (boss rows 0-1 centre + escort batches), v1 save upgrade, new events (`EnemySpawned.flag`/`value2`,
      `PickupSpawned`, `BatchSpawned`), new `ActRules` fields, tests.
- [x] 2. Config: `ActDefinitionsBuilder` values + `ActSpawnRulesUpgrade` writes them into Act_1..3.
- [x] 3. Gameplay/HUD: turn_end uses the phase's own turn number, banner shows the jumped turn, empty field ends the turn,
      "Enemies incoming!" banner (`br.hud.incoming`, 5 locales) + SFX.
- [x] 4. Presentation: pop-in (scale from 0 with bounce, spawn VFX, stagger, SFX) for batch enemies and pickups, also for
      the stage-opening batch after the intro.
- [x] 5. §6: control readout hidden by default everywhere (renamed setting so a saved "on" from the old default is dropped).
- [x] 6. Editor: recompile, act upgrade, LocalizationSeeder + FontAssetsBuilder (no prefab change needed: the incoming
      ribbon reuses the turn ribbon), EditMode tests, sim smoke, play smoke screenshots.
- [ ] 7. APK + device.
- [ ] 8. Docs (HANDOFF §4, ControlDemo.md), commit.

## Log
- Sim done: `SpawnBatch`/`BatchEntry` + `StagePlan.batches`, `RunState.nextBatchIndex/nextBatchTurn`, `BoardSpawning`
  (placement), `StageSchedule` (cadence, skip, legacy upgrade), `SimEventKind.PickupSpawned = 34`, `BatchSpawned = 54`.
  `ActRules`: minEnemiesPerBatch 10, spawnEveryNTurns 3, batchesPerStage 3, spawnForbiddenNearRows 3, pickupsPerBatch 2,
  batchBudgetRows 4 (written into Act_1..3 by `ActSpawnRulesUpgrade.Run()`). EditMode Simulation 76/76 (10 new in
  `BatchSpawnTests`, 3 new in `StageGeneratorTests`).
- Smoke (6 seeds, real configs): aimed bot 6/6 victories, min HP 18–26, turns/stage 4–18 (boss 6–18), 218 batches all
  exactly 10 enemies, lowest spawn row 6, 0 in rows 7–9, 0 dropped, 94 skip events / 116 turns skipped. 30% random
  shots: 6/6, min HP 14–22. 60% random shots: 5/6 (one defeat at the act-3 boss), min HP 0–22. No config change needed.
- Gameplay/presentation done: `BatchArrivals` (stage-start pop-in after the intro, HUD hook), `BatchSpawnPlayer`
  (stagger 0.06 s, portal flash + dust + Split pop SFX rising in pitch, Portal whoosh, labels grow with the pop),
  `PickupView` pop, `IGameplayHud.ShowIncomingBanner` on the turn ribbon (`br.hud.incoming`, 5 locales, CJK atlas
  rebuilt), `EnemyPhaseRunner` closes the turn it opened, an empty field ends the turn, `TurnController` 397 lines
  (intro → BatchArrivals, debug start → RunSimulation). Fixed on the way: `BoardEventPlayer.SfxSlots` was 200 while
  the game SoundEffect values are 200..607, so every board SFX was dropped (now 1024; board SFX play again).
- §6: `DebugSettings.showControlReadout` (renamed from showControlDebug, default off in every build).
- Editor: EditMode "Nex.BilliardRogue" 169/169. Play smoke (seed 2929/3131): stage 1 opens with 10 enemies in rows 1–6
  + 2 pickups, "Enemies incoming!" ribbon, turn 1; KillAll on turn 1 → phase jumps to turn 4 with batch 2 popping in,
  banner "Turn 4 — shoot!", analytics turn_start 1 / turn_end 1 / turn_start 4 / turn_end 4; boss stage 3: King Slime at
  cols 3–4 rows 0–1 + 10 escorts in rows 0–6; bot: cadence batch on turn 4. Console: no game warnings or errors.
  Shots in scratch `spawn/shots/`.
