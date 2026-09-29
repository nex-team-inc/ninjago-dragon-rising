# Handoff — Cursor session 2026-09-28 18:20–18:50 (while Claude Code was rate-limited)

Claude Code's session `78b50d75` hit its usage limit at 16:51. Every workflow agent still running then was
killed by the limit (not by a bug). A Cursor agent picked up from 18:20 and stopped at 18:50 so Claude can
continue. Nothing else touched the repo or the Editor in between. Branch: `twigs/SimonBut/Vibe-Billiard-Rogue`.

## 1. State of Claude's workflows at the cutoff

| Workflow agent | State | What to do |
|---|---|---|
| `foundation:build` | done (`d5086804`) | — |
| `foundation:review` | killed by limit, no result | **Re-run** (the review gate never happened) |
| `create:heroprops`, `create:audio` | done (`e240510c`, `eeb56583`) | — |
| `review:heroprops`, `review:audio` | killed by limit, no result | Re-run |
| `create:enemies` | killed mid-task (preview renderer in progress) | Relaunch; continue from the WIP scripts in `Tools/Blender/enemies/` |
| `create:environment` | killed mid-task (act layouts rewrite + one-command orchestrator in progress) | Relaunch; continue from `Tools/Blender/environment/` |
| `create:art2d` | killed mid-task (both fonts verified; UI kit + logo drafted; icons/particles/surfaces scripts written, unverified) | Relaunch; continue from `Tools/Fonts/`, `Tools/Textures/` |

The WIP scripts are committed as-is in `6553fb51` (unreviewed). Their outputs are in the gitignored
`Tools/Staging/` and may be partial.

## 2. Done in this session

- `6553fb51` — preserved the three interrupted agents' generator scripts and the Unity-generated palette `.meta` files.
- `109f1fca` — **the Simulation module (TDD §3) is implemented.** Skip the "simulation" agent in the module build
  wave. Remaining Simulation work is listed in §5.
  - `BoardOps`: occupancy, scaled spawning, wave rows, damage, deaths, chained bomber explosions, burn spread,
    status effects, crates, pickups, player HP.
  - `BallSimulator` plus `BallCollision`, `BallHitResolver`, `BallTriggers`, `BallSlot`: physics, all 12 ball
    abilities, portals, mud, pickups, anti-stall, and `PredictPath`, which uses the same collision code as `Step`.
  - `EnemyPhaseResolver`, `RunFactory`, `StageGenerator`, `RewardGenerator`.
  - Tests: `Tests/TestRules.cs` (GDD defaults copied from the config builders), plus stage and reward generator tests.
- `Tools/sim_smoke_eval.cs` — a whole-run smoke test that runs in the Editor through `unity command eval_file` with an absolute path (usage in the file header).

Verification (18:36–18:39):
- `compile_check.py` passes, and the Editor recompile finished with no errors.
- **21/21 Simulation EditMode tests pass** (`unity command run_tests --mode editor --filter Nex.BilliardRogue.Simulation.Tests`).
- The smoke test drives full runs with a random-angle bot using the real config assets (launch, step, enemy phase,
  stage clear, reward, next stage) on 3 seeds. It hit no exceptions and no stuck balls, and all 3 runs took about 1 s in total.

## 3. API additions (additive; TDD §3.4 updated with a pointer here)

- `DamageSource.isCrit`, `StatusStacks.burnSpreads` (save-compatible new fields).
- `BoardOps` gains `IsFootprintFree`, `EnemyAt`, `SpawnWaveRow`, `ApplyStatus(run, e, status, stacks, burnSpreads, events)`,
  `HealEnemy`, `DamageCrate`, `CollectPickup`, `DamagePlayer`, and `HealPlayer`.
  - Every board, player-HP and pickup mutation goes through `BoardOps`.
  - `DamageEnemy` adds only the +1 poison bonus. Callers apply crit, power shot, power pickup, rubber and frozen bonuses.
  - `DamagePlayer` sets `outcome = Defeat` at 0 HP.
- `RunFactory.CompleteStage(rules, run, events)` — clears leftover pickups (`PickupExpired`), heals `bossHealFraction`
  after a boss stage, emits `StageCleared`, and sets `awaitingReward` unless it was the final stage. `AdvanceToNextStage`
  sets `outcome = Victory` when it returns false. `RewardGenerator.Roll` does **not** set `awaitingReward`.

Added by the fix pass (2026-09-28 evening; all additive, public APIs unchanged):
- Rules fields with class defaults, also written into the existing config assets: `ArenaRules.maxStallSeconds = 1`,
  `BalanceRules.offerBasicBall = false`, `ActRules.minOpenColumnsPerRow = 1`. State: `EnemyState.halfHpSummonPending`.
- `RewardGenerator.Roll` stores `rng.State` into `run.rngState`, so a save after the reward pick resumes the same run.
- `BallWallBounce.sourceId` = the pillar id for a pillar reflection (0 = arena wall); `value` stays the wall-bounce count
  (pillars do not feed the Rubber bonus).
- `WaveSpawned.flag` = true when a planned cell had to be dropped (row completely full); `value2` = cells spawned.
- Internals split for the 400-line rule: `BoardOps` delegates to `BoardOccupancy` (queries), `BoardDamage` (hits, statuses,
  deaths) and `EnemyEvents`; `EnemyPhaseResolver` orchestrates `EnemyAbilities` (step 1) and `EnemyAdvance` (step 2).
- Tests: `Tests/SimTestHarness.cs` (`SimTest`, `ShotLog`) plus `BallPhysicsTests`, `BallAbilityTests`, `EnemyPhaseTests`,
  `RunFlowTests` cover every TDD §3.6 item (52 EditMode tests in total).

## 4. Rule decisions the Gameplay / Presentation / UI modules must follow

**Grid, spawning and stage clear**
- **Footprint:** an enemy at (col,row) with size w×h covers cols col..col+w-1 and rows row..row+h-1, where row 0 is
  the top. It is in the danger row when row+h-1 == `DangerRow`. Enemies never move past the danger row; they stay
  there and attack every phase.
- **Ids:** `board.nextId` is unique across enemies, field objects and pickups. Plan field objects use ids 1..N.
  `BeginStage` copies them and sets nextId = max+1.
- **Spawning (GDD v2 §5, replaces the v1 row waves):** a stage plans `ActRules.batchesPerStage` (3) batches
  (`StagePlan.batches`), each `minEnemiesPerBatch` (10) enemies from the act's weighted pool within a budget of
  `batchBudgetRows` × (`baseBudgetPerRow` + `budgetGrowthPerStage` × stageNumber) (boss escorts: half; the cheapest
  entries fill up when the budget runs short), then `pickupsPerBatch` (2) pickups. `BoardOps.SpawnBatch` places every
  entry, in plan order, on a random free cell (one `SimRandom` draw each) in rows 0..rows − 1 − `spawnForbiddenNearRows`
  (rows 0..6 of 10: never rows 7–9), keeping `minOpenColumnsPerRow` (1) free cells in each row; entries without a cell
  are dropped (`BatchSpawned.flag`). Nothing slides in from the top any more.
- **Cadence (`StageSchedule`):** batch 0 spawns in `BeginStage` (turn 1); batch k spawns at the end of the enemy phase that
  brings turnInStage to k × `spawnEveryNTurns` (turns 4, 7). `RunState.nextBatchIndex` / `nextBatchTurn` hold the schedule.
- **Skip empty turns:** when a phase ends with no enemy but Bone Walls on the field and a batch is left, the next batch
  spawns right away and turnInStage jumps to its turn (`BatchSpawned.sourceId` = turns skipped; `stats.turns` counts
  played turns only). Gameplay also ends a player turn as soon as the field is empty, so no shot is wasted on nothing.
- **Boss stage:** `BeginStage` places `act.bossType` at col `(columns − w)/2`, rows 0–1 (its `EnemySpawned` has no pop-in
  flag), then batch 0 as the first escorts. Escort batches keep coming every `spawnEveryNTurns` while the boss lives,
  cycling `batches[k % count]`; nothing spawns once it is dead. The bosses' own spawn abilities are unchanged.
- **Stage cleared:** true when no batch is left (normal stage: all spawned; boss stage: boss dead) and no enemies remain
  except `BoneWall`.
- **Events:** batch enemies emit `EnemySpawned` with `flag = true`, `value2` = stagger index; batch pickups emit
  `PickupSpawned` (34, same fields); then one `BatchSpawned` (54: value = batch index, value2 = entries spawned, flag =
  dropped, sourceId = turns skipped). Presentation pops them in with a stagger (`BatchSpawnPlayer`); at a stage start the
  board is rebuilt with the batch hidden and it pops in after the intro (`BatchArrivals`).
- **v1 saves:** a stage planned with row waves (`StagePlan.batches` empty) is converted by
  `StageSchedule.UpgradeLegacyStage` (on load and at the next phase end): the unspawned waves (boss: all escort waves) are
  packed into batches of ≥ 10 enemies, the first due at the next phase end. `waves`, `nextWaveIndex`, `SpawnWaveRow` and
  `WaveSpawned` remain for that path only.

**Enemy phase**
- **Cadence:** each enemy's `turnCounter` increments at the start of every enemy phase.
  - An action with cadence N fires when tc % N == 0 and is telegraphed the phase before (when N > 1).
  - Telegraph `value` codes: 0 Spawn, 1 Cast, 2 Heal, 3 Quake. A firing quake emits `EnemyAbilityTelegraph` with
    `flag = true`.
  - Enemies spawned during a phase first act in the next phase.
- **Freeze:** an enemy frozen at phase start skips abilities, advance and attack. The counter decrements at step 4 of
  that phase, so Freeze 1 skips exactly one phase. Bosses are immune.
- Ranged enemies don't also melee in the danger row on a phase where they cast.
- **Spawn-around abilities** (Totem, King Slime, Bone Lich walls and the half-HP summon) pick random free cells of the
  caster's 8-neighbourhood ring outside the danger row, and never the cells straight below a caster that moves, so a boss
  can no longer wall itself in with its own spawns. The Bone Lich's one-time summon is `EnemyState.halfHpSummonPending`
  (set with `bossHalfTriggered`, consumed by the next abilities step), so it survives a save and any object re-creation.
- The resolver stamps `SimEvent.step` (0–4) and stops after the step in which the player dies.

**Balls**
- **Iteration:** `DamageEnemy` can remove several enemies at once (explosion chains). Always iterate a snapshot of
  `board.enemies`.
- **Splitter:** the first hit replaces the parent with minis. `BallSplit` ends the parent, and minis don't emit
  `BallLaunched`. The fan (±18° around the reflected direction) is reflected against the contact normal, so a grazing
  hit never sends a mini straight back into the face that triggered the split.
- **Pillars:** a pillar reflection emits `BallWallBounce` with `sourceId` = pillar id (arena walls use 0).
- **Bomb:** the enemy hit directly takes both the hit damage and the area damage.
- **Piercer:** it forgets a pierced enemy once it has left it, so it can hit that enemy again on a later pass.
- **Crates:** take the ball's level damage (minimum 1). Crates with odd ids drop a pickup: `CrateBroken` gets
  `flag = true`, `pickup` set, and `sourceId` = the pickup id.
- **Randomness:** crit and freeze procs use a hash of (seed, shot number, hit index, salt). No RNG state is saved,
  and a continued run replays the same procs.
- **Anti-stall:** after `maxFlightSeconds` (7), `maxIdleWallBounces` (10) or the level's max bounces, a growing downward
  pull applies at constant speed. Idle wall bounces are wall bounces since the last contact with a *new* solid: bouncing
  between the walls and one enemy (the 90° "juggle") counts as idle, so it stalls after 10 wall bounces. Once the pull
  has run for `maxStallSeconds` (1 s) the ball ignores every solid and drops straight out at `ballSpeed`, so no flight
  exceeds `maxFlightSeconds + maxStallSeconds + TopWallY / ballSpeed` = 8.9 s (measured worst case 8.8 s over 3034
  bot shots; 1.9% of shots reach the drop). Pool size is 96 balls with 240 Hz substeps.

**Pickups, generation and rewards**
- **Pickups** move down with the enemies through `EnemyMoved` with `flag = true`. Batch pickups spawn with
  `PickupSpawned`; presentation still syncs pickups on `BatchSpawned` as a safety net.
- **Shots:** `run.extraBalls` (+1 Ball pickups) is **not** reset by the simulation. `ShotSequencer` owns resetting it
  at the end of the turn.
- **Stage generation:**
  - Batches as above (the v1 rule "every row has an enemy" is gone with the rows).
  - Field objects sit in rows 2..6 (never the 3 rows nearest the player), at most one per column, at most one portal pair,
    and avoid boss columns.
  - Crate HP = `crateHp × (1 + hpScalePerStage × stageNumber)`.
- **Reward cards:** card 1 is a new ball, card 2 an upgrade, card 3 a Heal when HP ≤ 50% (otherwise a random kind).
  No Basic ball is offered while an ability ball is unlocked (`balance.offerBasicBall = false`), and no two cards share
  a ball type. `Roll` writes `run.rngState`, so saving right after the pick is safe.

## 5. Known issues and follow-ups (none block the module wave)

Status after the Simulation fix pass (2026-09-28 evening): items 1, 2, 3 and 5 are done, 4 was measured, 6 stands.

1. ~~**Class-size rule**~~ Done: `BoardOps` → `BoardOccupancy` + `BoardDamage` + `EnemyEvents`; `EnemyPhaseResolver`
   → `EnemyAbilities` + `EnemyAdvance`. Largest Simulation class is now `BallSimulator` (333 lines).
2. ~~**Missing tests from TDD §3.6**~~ Done: 31 new EditMode tests (52 total, all green) in `Simulation/Tests`.
3. ~~**Pacing**~~ Done: flights are bounded by `ArenaRules.maxStallSeconds` (see §4 Anti-stall). Worst flight in the
   smoke run: 8.8 s (was 13.7 s); `antiStallAccel` and `maxFlightSeconds` keep their GDD values.
4. **Balance (measured, no designer values changed).** `Tools/sim_smoke_eval.cs` now aims: it scores sampled launch
   positions × angles and straight lines at the lowest enemies by their `PredictPath` contacts, grows the bag to 8 balls,
   then levels up, and heals at ≤ 50% HP. Six seeds with the real config assets:
   - Act-1 boss stage cleared on 6/6 seeds in 5–8 turns (it is not a wall); normal act-1 stages take 4–8 turns.
   - Outcomes: 2 victories (89 and 110 turns), defeats at stage numbers 7, 7 (Bone Lich), 9 and 10. Turns per stage
     4–18 (longest: act-3 boss). Max enemies on board 13–17 with the aiming bot (16–22 with the earlier random bot).
   - Ball-flight pacing: worst flight 8.77 s; 98/3034 shots exceed 7 s and 57 reach the ghost drop.
   - Best combo per shot 20–32 on dense boards. If that reads as too much, lower `maxIdleWallBounces` (juggles) or
     the level `maxBounces` in the ball assets; both are designer values.
   The escort cadence / King Slime numbers were left as in the GDD. Act 2–3 difficulty for a naive bot is a designer
   call; the bot never picks Bomb/Piercer deliberately and only uses straight or single-bounce aims.
5. ~~**Hard-coded constants**~~ Done: `ActRules.minOpenColumnsPerRow` and `BalanceRules.offerBasicBall` (current
   behaviour as defaults, written into the assets).
6. `DebugSettings.godMode` and similar settings must be handled by Gameplay around `BoardOps.DamagePlayer` (the
   simulation has no debug flags).
7. **Pile-ups on boss stages** are visible instead of silently trimmed. Since GDD v2 §5 the levers are
   `ActRules.spawnEveryNTurns` / `minEnemiesPerBatch` (escort batches) and the boss `spawnCount`; the simulation reports
   drops through `BatchSpawned.flag`.
8. **Balance with batches (2026-09-29, `Tools/sim_smoke_eval.cs`, 6 seeds, real configs, no Hype):** the aiming bot wins
   6/6 (min HP 18–26), 30% random shots 6/6, 60% random shots 5/6; turns per stage 4–18 (boss stages longest). Every
   batch had exactly 10 enemies, none spawned below row 6, 94 empty-turn skips. No value was changed; if it plays too
   easy, raise `batchesPerStage`, `batchBudgetRows` or `hpScalePerStage`.

## 6. Suggested next steps for Claude

1. Re-run `foundation:review` (and apply its fixes), `review:heroprops` and `review:audio`.
2. Relaunch `create:enemies`, `create:environment` and `create:art2d` with "continue from the committed WIP scripts".
3. Start the module build wave **without** the simulation module: input, rendering, gameplay, presentation ×2, UI,
   flow, VFX. Point the gameplay and presentation agents at §3–§4 above.
4. Fit the Simulation follow-ups from §5 into the wave, or give them to a small extra agent.
