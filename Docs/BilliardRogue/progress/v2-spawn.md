# v2 spawning (GDD v2 §5) + hidden debug overlay (§6) — checkpoint

Owner key: `spawn` (editor lock label). Spec: `GDD-v2-Changes.md` §5–§6. Scratch: `<session scratchpad>/spawn/`.
The perf agent (`final-perf.md`) shares the Editor: short `Tools/editor_lock.sh spawn bash -c '...'` sections only.

## Plan / status
- [x] 1. Simulation: batch plan (`StagePlan.batches`, `SpawnBatch`), `BoardOps.SpawnBatch` (random free cells rows
      0..rows-1-spawnForbiddenNearRows, SimRandom), cadence + skip-empty-turn in `EnemyPhaseResolver`, stage clear,
      boss stage (boss rows 0-1 centre + escort batches), v1 save upgrade, new events (`EnemySpawned.flag`/`value2`,
      `PickupSpawned`, `BatchSpawned`), new `ActRules` fields, tests.
- [x] 2. Config: `ActDefinitionsBuilder` values + `ActSpawnRulesUpgrade` writes them into Act_1..3.
- [ ] 3. Gameplay/HUD: turn_end uses the phase's own turn number, banner shows the jumped turn, empty field ends the turn,
      "Enemies incoming!" banner (`br.hud.incoming`, 5 locales) + SFX.
- [ ] 4. Presentation: pop-in (scale from 0 with bounce, spawn VFX, stagger, SFX) for batch enemies and pickups, also for
      the stage-opening batch after the intro.
- [ ] 5. §6: control readout hidden by default everywhere (renamed setting so a saved "on" from the old default is dropped).
- [ ] 6. Editor: recompile, act upgrade, Build All (UI prefab + fonts), EditMode tests, sim smoke, play smoke screenshots.
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
