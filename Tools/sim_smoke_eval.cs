// Whole-run smoke test for the Billiard Rogue simulation (statements only: runs inside the eval wrapper).
// Usage: unity command eval_file /Users/simonbut/project/VibeProject3/Tools/sim_smoke_eval.cs 240000 --timeout 250 --project-path /Users/simonbut/project/VibeProject3/Starter --json
// Drives seeded runs with an aiming bot through launch -> step -> enemy phase -> stage clear -> reward -> next stage
// using the real config assets and the game's ShotSequencer (BalanceRules.shotsPerTurn balls per turn). The bot samples
// launch positions x angles plus straight lines at the lowest enemies
// and scores every candidate's PredictPath by the enemy contacts it makes (danger-row and boss contacts weigh more).
// Reports outcome, act/stage reached, turns per stage, worst flight, hits per shot and enemies on board per seed, plus the
// GDD v2 §5 batch stats: enemies per batch (min / avg), the lowest row a batch enemy spawned into, dropped entries and
// skipped empty turns.
var report = new System.Text.StringBuilder();
// 0 = every shot aimed; 0.5 = half the shots go at a random angle from a random launch point (a casual player).
var randomShotChance = 0.0;
var batchSizes = new List<int>(); var batchMaxRow = -1; var batchDrops = 0; var skippedTurns = 0; var skipEvents = 0; var forbiddenSpawns = 0;
var lastSpawnRow = -1;
void Tally(List<Nex.BilliardRogue.Simulation.SimEvent> evs)
{
    var enemies = 0;
    foreach (var ev in evs)
    {
        if (ev.kind == Nex.BilliardRogue.Simulation.SimEventKind.EnemySpawned && ev.flag)
        {
            enemies++;
        }
        if (ev.kind == Nex.BilliardRogue.Simulation.SimEventKind.BatchSpawned)
        {
            batchSizes.Add(enemies); enemies = 0;
            if (ev.flag) batchDrops++;
            if (ev.sourceId > 0) { skipEvents++; skippedTurns += ev.sourceId; }
        }
    }
}
void TallyRows(Nex.BilliardRogue.Simulation.RunState r, List<Nex.BilliardRogue.Simulation.SimEvent> evs)
{
    foreach (var ev in evs)
    {
        if (ev.kind != Nex.BilliardRogue.Simulation.SimEventKind.EnemySpawned || !ev.flag) continue;
        var e = r.board.enemies.Find(x => x.id == ev.targetId);
        if (e == null) continue;
        var bottom = e.row + e.height - 1;
        batchMaxRow = Mathf.Max(batchMaxRow, bottom);
        if (bottom > lastSpawnRow) forbiddenSpawns++;
    }
}
try
{
    var cfg = AssetDatabase.LoadAssetAtPath<Nex.BilliardRogue.BilliardRogueConfig>("Assets/Configs/BilliardRogue/BilliardRogueConfig.asset");
    var rules = Nex.BilliardRogue.RulesFactory.Build(cfg);
    var arena = rules.arena;
    lastSpawnRow = arena.rows - 1 - rules.acts[0].spawnForbiddenNearRows;
    var factory = new Nex.BilliardRogue.Simulation.RunFactory();
    var ops = new Nex.BilliardRogue.Simulation.BoardOps(rules);
    var sim = new Nex.BilliardRogue.Simulation.BallSimulator(rules, ops);
    var resolver = new Nex.BilliardRogue.Simulation.EnemyPhaseResolver(rules, ops);
    var rewards = new Nex.BilliardRogue.Simulation.RewardGenerator();
    var events = new List<Nex.BilliardRogue.Simulation.SimEvent>();
    var options = new List<Nex.BilliardRogue.Simulation.RewardOption>();
    var path = new Vector2[12];
    var seeds = new[] { 1, 2, 3, 4, 5, 6 };
    var angleSamples = 12;
    var launchXs = new[] { 0.12f, 0.38f, 0.62f, 0.88f };
    var topY = Nex.BilliardRogue.Simulation.ArenaGeometry.TopWallY(arena);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var globalWorstFrames = 0; var globalMaxHits = 0; var globalOver7 = 0; var globalOver8 = 0; var globalShots = 0;

    bool OnWall(Vector2 p) => p.x <= arena.ballRadius + 0.01f || p.x >= arena.columns - arena.ballRadius - 0.01f || p.y >= topY - arena.ballRadius - 0.01f;

    float Score(Nex.BilliardRogue.Simulation.RunState run, Vector2 origin, Vector2 dir)
    {
        var n = sim.PredictPath(run, origin, dir, 24f, 4, path);
        var score = 0f;
        for (var i = 1; i < n; i++)
        {
            var p = path[i];
            if (p.y < 0f || OnWall(p)) continue;
            foreach (var e in run.board.enemies)
            {
                var r = Nex.BilliardRogue.Simulation.BallCollision.EnemyRect(arena, e);
                var pad = arena.ballRadius + 0.06f;
                if (p.x < r.xMin - pad || p.x > r.xMax + pad || p.y < r.yMin - pad || p.y > r.yMax + pad) continue;
                var shielded = e.shieldFace == Nex.BilliardRogue.Simulation.Face.Bottom && p.y < r.yMin;
                var rowBottom = e.row + e.height - 1;
                score += (shielded ? 0.1f : 1f) + 0.12f * rowBottom + (rules.enemies[(int)e.type].isBoss ? 0.4f : 0f) + (i == 1 ? 0.3f : 0f);
                break;
            }
        }
        return score;
    }

    (Vector2 origin, Vector2 dir) Aim(Nex.BilliardRogue.Simulation.RunState run, System.Random bot)
    {
        var bestScore = -1f; var bestOrigin = Vector2.zero; var bestDir = Vector2.up;
        void Consider(Vector2 origin, Vector2 dir)
        {
            dir = Nex.BilliardRogue.Simulation.ArenaGeometry.ClampAim(arena, dir);
            var s = Score(run, origin, dir) + (float)bot.NextDouble() * 0.01f;
            if (s <= bestScore) return;
            bestScore = s; bestOrigin = origin; bestDir = dir;
        }
        foreach (var x01 in launchXs)
        {
            var origin = Nex.BilliardRogue.Simulation.ArenaGeometry.LaunchOrigin(arena, x01);
            for (var a = 0; a < angleSamples; a++)
            {
                var angle = (arena.minAimAngleDeg + (180f - 2f * arena.minAimAngleDeg) * (a + 0.5f) / angleSamples) * Mathf.Deg2Rad;
                Consider(origin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }
        var lowest = run.board.enemies.OrderByDescending(e => e.row + e.height).ThenBy(e => rules.enemies[(int)e.type].isBoss ? 0 : 1).Take(4);
        foreach (var e in lowest)
        {
            var center = Nex.BilliardRogue.Simulation.ArenaGeometry.FootprintCenter(arena, e.col, e.row, e.width, e.height);
            foreach (var dx in new[] { 0f, -1.5f, 1.5f })
            {
                var origin = Nex.BilliardRogue.Simulation.ArenaGeometry.LaunchOrigin(arena, Mathf.Clamp01((center.x + dx) / arena.columns));
                Consider(origin, center - origin);
            }
        }
        return (bestOrigin, bestDir);
    }

    foreach (var seed in seeds)
    {
        var run = factory.NewRun(rules, seed, 1);
        var rng = new Nex.BilliardRogue.Simulation.SimRandom(run.rngState);
        factory.BeginStage(rules, run, rng, events);
        Tally(events); TallyRows(run, events);
        events.Clear();
        var bot = new System.Random(seed);
        // The game's own turn rule: BalanceRules.shotsPerTurn balls per turn, the bag rotation carried across turns.
        var sequencer = new Nex.BilliardRogue.ShotSequencer(run, rules.balance.shotsPerTurn, 0f);
        var turns = 0; var maxSteps = 0; var stuck = 0; var maxHits = 0; var over7 = 0; var over8 = 0; var shots = 0;
        var turnsPerStage = new List<int>(); var stageTurns = 0; var maxEnemies = 0; var minHp = run.playerHp;
        while (run.outcome == Nex.BilliardRogue.Simulation.RunOutcome.None && turns < 400)
        {
            turns++; stageTurns++;
            if (run.board.enemies.Count > maxEnemies) maxEnemies = run.board.enemies.Count;
            if (run.playerHp < minHp) minHp = run.playerHp;
            sequencer.BeginTurn(false);
            while (sequencer.HasBallToFire && run.board.enemies.Count > 0)
            {
                var ball = sequencer.Fire(out _);
                var (origin, dir) = Aim(run, bot);
                if (bot.NextDouble() < randomShotChance)
                {
                    origin = Nex.BilliardRogue.Simulation.ArenaGeometry.LaunchOrigin(arena, (float)bot.NextDouble());
                    var randomAngle = (arena.minAimAngleDeg + (180f - 2f * arena.minAimAngleDeg) * (float)bot.NextDouble()) * Mathf.Deg2Rad;
                    dir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                }
                sim.Launch(run, ball, origin, dir, bot.NextDouble() < 0.2, 0, events);
                events.Clear();
                var steps = 0; var hits = 0;
                while (sim.ActiveCount > 0 && steps < 3600)
                {
                    sim.Step(run, 1f / 60f, events); steps++;
                    foreach (var ev in events) { if (ev.kind == Nex.BilliardRogue.Simulation.SimEventKind.EnemyHit) hits++; }
                    events.Clear();
                }
                shots++;
                if (sim.ActiveCount > 0) { stuck++; sim.Clear(); }
                if (steps > maxSteps) maxSteps = steps;
                if (hits > maxHits) maxHits = hits;
                if (steps > 7 * 60) over7++;
                if (steps > 8 * 60) over8++;
                if (factory.IsStageCleared(run)) break;
            }
            sequencer.EndTurn();
            if (!factory.IsStageCleared(run))
            {
                resolver.Resolve(run, rng, events);
                Tally(events); TallyRows(run, events);
                events.Clear();
                if (run.outcome != Nex.BilliardRogue.Simulation.RunOutcome.None) break;
                if (!factory.IsStageCleared(run)) continue;
            }
            turnsPerStage.Add(stageTurns); stageTurns = 0;
            factory.CompleteStage(rules, run, events);
            events.Clear();
            if (run.awaitingReward)
            {
                options.Clear();
                rewards.Roll(rules, run, 3, rng, options);
                // Grow the bag to 8 balls first, then level up; heal when at or below half HP.
                var newBall = options.FirstOrDefault(o => o.kind == Nex.BilliardRogue.Simulation.RewardKind.NewBall);
                var upgrade = options.FirstOrDefault(o => o.kind == Nex.BilliardRogue.Simulation.RewardKind.UpgradeBall);
                var pick = (run.bag.Count < 8 ? newBall ?? upgrade : upgrade ?? newBall) ?? options[0];
                if (run.playerHp * 2 <= run.playerMaxHp) pick = options.FirstOrDefault(o => o.kind == Nex.BilliardRogue.Simulation.RewardKind.Heal) ?? pick;
                rewards.Apply(rules, run, pick);
            }
            if (!factory.AdvanceToNextStage(rules, run)) break;
            factory.BeginStage(rules, run, rng, events);
            Tally(events); TallyRows(run, events);
            events.Clear();
        }
        if (stageTurns > 0) turnsPerStage.Add(stageTurns);
        globalShots += shots; globalOver7 += over7; globalOver8 += over8;
        if (maxSteps > globalWorstFrames) globalWorstFrames = maxSteps;
        if (maxHits > globalMaxHits) globalMaxHits = maxHits;
        var bag = string.Join(",", run.bag.Select(b => b.type.ToString().Substring(0, 2) + b.level));
        report.AppendLine($"seed {seed}: outcome={run.outcome} act={run.actIndex + 1} stageInAct={run.stageInAct} stageNumber={run.stageNumber} turns={turns} turnsPerStage=[{string.Join(",", turnsPerStage)}] hp={run.playerHp}/{run.playerMaxHp} minHp={Mathf.Min(minHp, run.playerHp)} damageTaken={run.stats.damageTaken} bag=[{bag}] kills={run.stats.kills} shots={shots} bestCombo={run.stats.bestCombo} maxHitsPerShot={maxHits} worstFlight={maxSteps}f({maxSteps / 60f:F1}s) flightsOver7s={over7} flightsOver8s={over8} stuck={stuck} enemiesAtEnd={run.board.enemies.Count} maxEnemiesOnBoard={maxEnemies}");
    }
    report.AppendLine($"batches: count={batchSizes.Count} enemiesPerBatch min={(batchSizes.Count > 0 ? batchSizes.Min() : 0)} avg={(batchSizes.Count > 0 ? batchSizes.Average() : 0):F1} max={(batchSizes.Count > 0 ? batchSizes.Max() : 0)} under10={batchSizes.Count(n => n < 10)} dropped={batchDrops} lowestSpawnRow={batchMaxRow} (last allowed {lastSpawnRow}) spawnsInForbiddenRows={forbiddenSpawns} skipEvents={skipEvents} turnsSkipped={skippedTurns}");
    report.AppendLine($"all seeds: shotsPerTurn={rules.balance.shotsPerTurn} randomShotChance={randomShotChance} shots={globalShots} worstFlight={globalWorstFrames / 60f:F2}s maxHitsPerShot={globalMaxHits} flightsOver7s={globalOver7} flightsOver8s={globalOver8} elapsed={watch.ElapsedMilliseconds} ms");
}
catch (Exception ex)
{
    report.AppendLine("EXCEPTION: " + ex);
}
return report.ToString();
