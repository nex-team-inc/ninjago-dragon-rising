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

## 4. Rule decisions the Gameplay / Presentation / UI modules must follow

**Grid, spawning and stage clear**
- **Footprint:** an enemy at (col,row) with size w×h covers cols col..col+w-1 and rows row..row+h-1, where row 0 is
  the top. It is in the danger row when row+h-1 == `DangerRow`. Enemies never move past the danger row; they stay
  there and attack every phase.
- **Ids:** `board.nextId` is unique across enemies, field objects and pickups. Plan field objects use ids 1..N.
  `BeginStage` copies them and sets nextId = max+1.
- **Stage start:** a normal stage spawns waves[0] into row 1 and waves[1] into row 0. A boss stage spawns waves[0]
  (the boss alone) at col `(columns − w)/2`, rows 0–1. After that, one wave row per enemy phase spawns into row 0,
  skipping occupied cells.
- **Boss stage:** a stage is a boss stage when `stageInAct >= act.normalStages`. After the planned escorts,
  `waves[1 + k % (waves.Count − 1)]` spawns every `bossEscortEveryNTurns` turns while the boss lives, with
  k = turnInStage / N. Nothing spawns after the boss dies.
- **Stage cleared:** true when (boss stage ? no boss alive : all waves spawned) and no enemies remain except `BoneWall`.

**Enemy phase**
- **Cadence:** each enemy's `turnCounter` increments at the start of every enemy phase.
  - An action with cadence N fires when tc % N == 0 and is telegraphed the phase before (when N > 1).
  - Telegraph `value` codes: 0 Spawn, 1 Cast, 2 Heal, 3 Quake. A firing quake emits `EnemyAbilityTelegraph` with
    `flag = true`.
  - Enemies spawned during a phase first act in the next phase.
- **Freeze:** an enemy frozen at phase start skips abilities, advance and attack. The counter decrements at step 4 of
  that phase, so Freeze 1 skips exactly one phase. Bosses are immune.
- Ranged enemies don't also melee in the danger row on a phase where they cast.
- The resolver stamps `SimEvent.step` (0–4) and stops after the step in which the player dies.

**Balls**
- **Iteration:** `DamageEnemy` can remove several enemies at once (explosion chains). Always iterate a snapshot of
  `board.enemies`.
- **Splitter:** the first hit replaces the parent with minis. `BallSplit` ends the parent, and minis don't emit
  `BallLaunched`.
- **Bomb:** the enemy hit directly takes both the hit damage and the area damage.
- **Piercer:** it forgets a pierced enemy once it has left it, so it can hit that enemy again on a later pass.
- **Crates:** take the ball's level damage (minimum 1). Crates with odd ids drop a pickup: `CrateBroken` gets
  `flag = true`, `pickup` set, and `sourceId` = the pickup id.
- **Randomness:** crit and freeze procs use a hash of (seed, shot number, hit index, salt). No RNG state is saved,
  and a continued run replays the same procs.
- **Anti-stall:** after max flight time, max idle wall bounces, or the level's max bounces, a growing downward pull
  applies at constant speed. After 2× max flight time the ball ignores solids and drops out. Pool size is 96 balls
  with 240 Hz substeps.

**Pickups, generation and rewards**
- **Pickups** move with the waves through `EnemyMoved` with `flag = true`. Wave pickups have no spawn event, so
  presentation should sync pickups on `WaveSpawned`.
- **Shots:** `run.extraBalls` (+1 Ball pickups) is **not** reset by the simulation. `ShotSequencer` owns resetting it
  at the end of the turn.
- **Stage generation:**
  - Every row has at least one enemy and keeps at least one column open.
  - Field objects sit in rows 2..danger−1, at most one per column, at most one portal pair, and avoid boss columns.
  - Crate HP = `crateHp × (1 + hpScalePerStage × stageNumber)`.
- **Reward cards:** card 1 is a new ball, card 2 an upgrade, card 3 a Heal when HP ≤ 50% (otherwise a random kind).
  No Basic ball is offered while an ability ball is unlocked, and no two cards share a ball type.

## 5. Known issues and follow-ups (none block the module wave)

1. **Class-size rule:** `BoardOps.cs` (436 lines) and `EnemyPhaseResolver.cs` (474) exceed the 400-line limit.
   Split them, e.g. into `EnemyAbilities` and `EnemyAdvance`.
2. **Missing tests from TDD §3.6:** wall/face reflection, shield block, pierce, split, bomb area, chain, burn/poison
   ticks, freeze skip, advance with blocking and diagonal slide, danger attack, wave spawn, stage clear, anti-stall
   exit bound, `PredictPath` vs `Step`, and a `RunState` JSON round-trip.
3. **Pacing:** in the smoke test the longest single ball flight was 820 frames (13.7 s). The anti-stall pull is too
   gentle for the GDD pacing targets; tune `antiStallAccel` or the max flight time, and add the §3.6 exit-bound test.
4. **Balance:** two of three bot runs died in the act-1 boss stage with 17 enemies on board. Check the escort cadence
   and pile-up once a real aiming bot exists.
5. **Hard-coded constants** that could become `ActRules` fields: the minimum open columns per row, and the rule that
   excludes Basic from new-ball cards.
6. `DebugSettings.godMode` and similar settings must be handled by Gameplay around `BoardOps.DamagePlayer` (the
   simulation has no debug flags).

## 6. Suggested next steps for Claude

1. Re-run `foundation:review` (and apply its fixes), `review:heroprops` and `review:audio`.
2. Relaunch `create:enemies`, `create:environment` and `create:art2d` with "continue from the committed WIP scripts".
3. Start the module build wave **without** the simulation module: input, rendering, gameplay, presentation ×2, UI,
   flow, VFX. Point the gameplay and presentation agents at §3–§4 above.
4. Fit the Simulation follow-ups from §5 into the wave, or give them to a small extra agent.
