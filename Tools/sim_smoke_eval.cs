// Whole-run smoke test for the Billiard Rogue simulation (statements only: runs inside the eval wrapper).
// Usage: unity command eval_file /Users/simonbut/project/VibeProject3/Tools/sim_smoke_eval.cs 90000 --timeout 100 --project-path /Users/simonbut/project/VibeProject3/Starter --json
// Drives 3 seeded runs with a random-angle bot through launch -> step -> enemy phase -> stage clear -> reward -> next stage
// using the real config assets, and reports outcome, progress, longest flight and stuck balls per seed.
var report = new System.Text.StringBuilder();
try
{
    var cfg = AssetDatabase.LoadAssetAtPath<Nex.BilliardRogue.BilliardRogueConfig>("Assets/Configs/BilliardRogue/BilliardRogueConfig.asset");
    var rules = Nex.BilliardRogue.RulesFactory.Build(cfg);
    var factory = new Nex.BilliardRogue.Simulation.RunFactory();
    var ops = new Nex.BilliardRogue.Simulation.BoardOps(rules);
    var sim = new Nex.BilliardRogue.Simulation.BallSimulator(rules, ops);
    var resolver = new Nex.BilliardRogue.Simulation.EnemyPhaseResolver(rules, ops);
    var rewards = new Nex.BilliardRogue.Simulation.RewardGenerator();
    var events = new List<Nex.BilliardRogue.Simulation.SimEvent>();
    var options = new List<Nex.BilliardRogue.Simulation.RewardOption>();
    var path = new Vector2[16];
    var watch = System.Diagnostics.Stopwatch.StartNew();
    foreach (var seed in new[] { 1, 2, 3 })
    {
        var run = factory.NewRun(rules, seed, 1);
        var rng = new Nex.BilliardRogue.Simulation.SimRandom(run.rngState);
        factory.BeginStage(rules, run, rng, events);
        events.Clear();
        var bot = new System.Random(seed);
        var turns = 0; var maxSteps = 0; var stuck = 0; var eventCount = 0;
        while (run.outcome == Nex.BilliardRogue.Simulation.RunOutcome.None && turns < 400)
        {
            turns++;
            for (var s = 0; s < run.bag.Count + run.extraBalls; s++)
            {
                var ball = s < run.bag.Count ? run.bag[s] : new Nex.BilliardRogue.Simulation.BallInstance { type = Nex.BilliardRogue.Simulation.BallType.Basic, level = 1 };
                var angle = (float)(15 + bot.NextDouble() * 150) * Mathf.Deg2Rad;
                var dir = Nex.BilliardRogue.Simulation.ArenaGeometry.ClampAim(rules.arena, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                var origin = Nex.BilliardRogue.Simulation.ArenaGeometry.LaunchOrigin(rules.arena, (float)bot.NextDouble());
                sim.PredictPath(run, origin, dir, 20f, 3, path);
                sim.Launch(run, ball, origin, dir, bot.NextDouble() < 0.2, 0, events);
                var steps = 0;
                while (sim.ActiveCount > 0 && steps < 3600) { sim.Step(run, 1f / 60f, events); steps++; eventCount += events.Count; events.Clear(); }
                if (sim.ActiveCount > 0) { stuck++; sim.Clear(); }
                if (steps > maxSteps) maxSteps = steps;
                if (factory.IsStageCleared(run)) break;
            }
            run.extraBalls = 0;
            if (!factory.IsStageCleared(run))
            {
                resolver.Resolve(run, rng, events);
                eventCount += events.Count; events.Clear();
                if (run.outcome != Nex.BilliardRogue.Simulation.RunOutcome.None) break;
                if (!factory.IsStageCleared(run)) continue;
            }
            factory.CompleteStage(rules, run, events);
            events.Clear();
            if (run.awaitingReward)
            {
                options.Clear();
                rewards.Roll(rules, run, 3, rng, options);
                rewards.Apply(rules, run, options[bot.Next(options.Count)]);
            }
            if (!factory.AdvanceToNextStage(rules, run)) break;
            factory.BeginStage(rules, run, rng, events);
            events.Clear();
        }
        report.AppendLine($"seed {seed}: outcome={run.outcome} stageNumber={run.stageNumber} turns={turns} hp={run.playerHp}/{run.playerMaxHp} bag={run.bag.Count} kills={run.stats.kills} shots={run.stats.shots} bestCombo={run.stats.bestCombo} maxFlightFrames={maxSteps} stuckBalls={stuck} events={eventCount} enemiesLeft={run.board.enemies.Count}");
    }
    report.AppendLine($"elapsed {watch.ElapsedMilliseconds} ms");
}
catch (Exception ex)
{
    report.AppendLine("EXCEPTION: " + ex);
}
return report.ToString();
