#nullable enable

using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Drives a headless GameSession in edit mode (no play mode, no scene objects beyond a hidden host): scripted
    /// shooter, fake HUD / flow host, in-memory JSON store. Plays a new run until stage 2 turn 2 (so a mid-stage
    /// turn-boundary save exists), drops the session like a crash, loads the save, continues and clears stage 2.
    /// Menu: Nex/Billiard Rogue/Gameplay Smoke Run. CLI: unity command eval
    /// 'return Nex.BilliardRogue.Editor.GameplaySmokeRunner.Run();'
    /// </summary>
    public static class GameplaySmokeRunner
    {
        const string ConfigPath = "Assets/Configs/BilliardRogue/BilliardRogueConfig.asset";
        const float Dt = 1f / 60f;

        sealed class Harness
        {
            public GameObject host = null!;
            public GameSession session = null!;
            public SmokeHud hud = null!;
            public SmokeFlowHost flow = null!;
            public RunState run = null!;
            public int ticks;
        }

        [MenuItem("Nex/Billiard Rogue/Gameplay Smoke Run", priority = 40)]
        public static void Menu() => Debug.Log(Run());

        /// <summary>Default smoke: seed 1234, 1 player. Returns a one-line report starting with OK or FAIL.</summary>
        public static string Run() => Run(1234, 1, 30000);

        public static string Run(int seed, int players, int maxTicks)
        {
            var config = AssetDatabase.LoadAssetAtPath<BilliardRogueConfig>(ConfigPath);
            if (config == null) return "FAIL: config missing at " + ConfigPath + " (run ConfigAssetsBuilder)";
            var rules = RulesFactory.Build(config);
            var store = new MemoryRunStore();
            var persistence = new RunPersistence(store);
            var report = new StringBuilder();
            var previousTimeScale = Time.timeScale;
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var ok = true;
            try
            {
                ok &= PlayNewRun(config, rules, persistence, store, seed, players, maxTicks, report);
                ok &= ContinueRun(config, rules, persistence, store, seed, maxTicks, report);
                ok &= PlayBossStage(config, rules, persistence, seed + 2, players, maxTicks, report);
            }
            catch (Exception e)
            {
                ok = false;
                report.Append(" EXCEPTION: ").Append(e);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                RestoreCleanScene(scene, wasDirty, report);
            }

            return (ok ? "OK" : "FAIL") + report;
        }

        #region Scenarios

        // New run until the turn-start save of stage 2 turn 2, then the host is destroyed without Save & Quit.
        static bool PlayNewRun(BilliardRogueConfig config, GameRules rules, RunPersistence persistence, MemoryRunStore store, int seed, int players, int maxTicks, StringBuilder report)
        {
            var run = new RunFactory().NewRun(rules, seed, players);
            var saves = 0;
            var harness = Create(config, rules, run, false, persistence, seed, _ => saves++);
            try
            {
                Drive(harness, maxTicks, h => h.run.stageNumber >= 1 && h.run.turnInStage >= 1 && h.session.Phase == TurnPhase.PlayerTurn);
                // KillAll mutates the live board only; the turn-start save taken before it is what the continue loads.
                var killAll = DebugHooks.KillAll();
                var ok = Check(report, "stage1-cleared", harness.flow.RewardsChosen >= 1)
                         & Check(report, "hook-kill-all", killAll.EndsWith("ok") && LivingEnemies(harness.run) == 0)
                         & Check(report, "reached-stage2-turn2", harness.run.stageNumber == 1 && harness.run.turnInStage >= 1)
                         & Check(report, "shots-fired", harness.run.stats.shots > 0)
                         & Check(report, "hits-landed", harness.run.stats.hits > 0)
                         & Check(report, "saves", saves >= 3 && store.Saves == saves)
                         & Check(report, "hud-turn-banners", harness.hud.TurnBanners >= 2)
                         & Check(report, "hud-shooter-banners", players == 1 || harness.hud.ShooterBanners > 0)
                         & Check(report, "hud-hp", harness.hud.MaxHp == harness.run.playerMaxHp);
                report.Append(" | new-run: ticks=").Append(harness.ticks).Append(" turns=").Append(harness.run.stats.turns)
                    .Append(" shots=").Append(harness.run.stats.shots).Append(" hits=").Append(harness.run.stats.hits)
                    .Append(" kills=").Append(harness.run.stats.kills).Append(" hp=").Append(harness.run.playerHp)
                    .Append(" reward=").Append(harness.flow.LastReward).Append(" bag=").Append(harness.run.bag.Count)
                    .Append(" saves=").Append(saves).Append(" fastForwards=").Append(harness.hud.FastForwardOns);
                return ok;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(harness.host);
            }
        }

        // Continue from the store: the loaded run must resume at stage 2 turn 2 and clear the stage.
        static bool ContinueRun(BilliardRogueConfig config, GameRules rules, RunPersistence persistence, MemoryRunStore store, int seed, int maxTicks, StringBuilder report)
        {
            var loaded = persistence.Load();
            if (!Check(report, "save-loads", loaded != null)) return false;
            var run = loaded!;
            var ok = Check(report, "save-stage", run.stageNumber == 1 && run.turnInStage >= 1)
                     & Check(report, "save-board", run.board.enemies.Count > 0 && run.stage.waves.Count > 0)
                     & Check(report, "save-outcome-none", run.outcome == RunOutcome.None)
                     & Check(report, "save-bag", run.bag.Count >= 4);
            var savedTurn = run.turnInStage;
            var savedTurns = run.stats.turns;
            var savedShots = run.stats.shots;
            var savedHits = run.stats.hits;
            var savedKills = run.stats.kills;
            var harness = Create(config, rules, run, true, persistence, seed + 1, null);
            try
            {
                Drive(harness, maxTicks, h => h.run.stageNumber >= 2 || h.session.Phase == TurnPhase.Finished);
                // One intro for the resumed stage, one for the stage that follows its reward.
                ok &= Check(report, "continue-resumed-same-turn", harness.hud.TurnBanners >= 1 && harness.flow.StageIntros == 2)
                      & Check(report, "continue-cleared-stage2", harness.run.stageNumber == 2 && harness.flow.RewardsChosen == 1)
                      & Check(report, "continue-no-defeat", harness.session.Phase != TurnPhase.Finished);
                var stats = harness.run.stats;
                report.Append(" | continue: resumedTurn=").Append(savedTurn + 1).Append(" ticks=").Append(harness.ticks)
                    .Append(" turns+=").Append(stats.turns - savedTurns).Append(" shots+=").Append(stats.shots - savedShots)
                    .Append(" hits+=").Append(stats.hits - savedHits).Append(" kills+=").Append(stats.kills - savedKills)
                    .Append(" hp=").Append(harness.run.playerHp)
                    .Append(" stageNumber=").Append(harness.run.stageNumber).Append(" phase=").Append(harness.session.Phase)
                    .Append(" ended=").Append(harness.flow.Ended?.ToString() ?? "-");
                harness.session.RequestSaveAndQuit();
                ok &= Check(report, "save-quit-keeps-save", persistence.HasSave);
                return ok;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(harness.host);
            }
        }

        // Debug settings and hooks on the way to the act-1 boss: forceStartStage (stage 2), GotoStage (boss stage),
        // god mode, unlockAllBalls, AddEveryBall, State, and KillAll as a finisher when the boss survives two turns.
        static bool PlayBossStage(BilliardRogueConfig config, GameRules rules, RunPersistence persistence, int seed, int players, int maxTicks, StringBuilder report)
        {
            var bossStage = rules.acts[0].normalStages;
            var run = new RunFactory().NewRun(rules, seed, players);
            var debug = new DebugSettings { forceStartStage = 1, godMode = true, unlockAllBalls = true };
            var harness = Create(config, rules, run, false, persistence, seed, null, debug);
            try
            {
                var ok = Check(report, "force-start-stage", run.stageNumber == 1 && !run.stage.isBoss)
                         & Check(report, "hook-add-every-ball", DebugHooks.AddEveryBall().EndsWith("ok"))
                         & Check(report, "hook-state", DebugHooks.State().Contains("phase="));
                Drive(harness, maxTicks, h => h.session.Phase == TurnPhase.PlayerTurn);
                ok &= Check(report, "hook-goto-stage", DebugHooks.GotoStage(bossStage).EndsWith("ok"));
                Drive(harness, maxTicks, h => h.run.stageNumber == bossStage && h.session.Phase == TurnPhase.PlayerTurn || h.session.Phase == TurnPhase.Finished);
                ok &= Check(report, "goto-boss-stage", run.stage.isBoss && run.stageNumber == bossStage && harness.flow.BossIntros == 1);
                Drive(harness, maxTicks, h => h.run.turnInStage >= 2 || h.flow.RewardsChosen >= 1 || h.session.Phase == TurnPhase.Finished);
                var killAll = "";
                Drive(harness, maxTicks, h =>
                {
                    if (h.flow.RewardsChosen >= 1 || h.session.Phase == TurnPhase.Finished) return true;
                    if (h.session.Phase == TurnPhase.PlayerTurn && killAll.Length == 0) killAll = DebugHooks.KillAll();
                    return false;
                });
                ok &= Check(report, "boss-kill-all", killAll.Length == 0 || killAll.EndsWith("ok"))
                      & Check(report, "boss-hud", harness.hud.BossSeen)
                      & Check(report, "boss-cleared", harness.flow.RewardsChosen == 1 && harness.run.stats.bossesDefeated == 1)
                      & Check(report, "god-mode-hp", harness.run.playerHp == harness.run.playerMaxHp)
                      & Check(report, "unlock-tier", persistence.MetaProgress.highestUnlockTier >= 1);
                report.Append(" | boss: ticks=").Append(harness.ticks).Append(" turns=").Append(harness.run.stats.turns)
                    .Append(" shots=").Append(harness.run.stats.shots).Append(" kills=").Append(harness.run.stats.kills)
                    .Append(" bag=").Append(harness.run.bag.Count).Append(" hp=").Append(harness.run.playerHp)
                    .Append(" killAllUsed=").Append(killAll.Length > 0)
                    .Append(" stageNumber=").Append(harness.run.stageNumber).Append(" phase=").Append(harness.session.Phase);
                return ok;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(harness.host);
            }
        }

        #endregion

        #region Helpers

        static Harness Create(BilliardRogueConfig config, GameRules rules, RunState run, bool isContinue, RunPersistence persistence, int seed, Action<RunState>? onSaved, DebugSettings? debug = null)
        {
            var host = new GameObject("GameplaySmokeRunner") { hideFlags = HideFlags.HideAndDontSave };
            var timeScale = host.AddComponent<TimeScaleController>();
            var session = host.AddComponent<GameSession>();
            var hud = new SmokeHud();
            var flow = new SmokeFlowHost();
            var inputs = new IShotInput[run.numPlayers];
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new SmokeShotInput(run, rules, seed + i);
            }

            session.Initialize(new GameSessionContext
            {
                config = config, rules = rules, run = run, isContinue = isContinue, hud = hud, flowHost = flow, inputs = inputs,
                persistence = persistence, analytics = null, timeScale = timeScale, headless = true, debugSettings = debug ?? new DebugSettings(),
            });
            if (onSaved != null) session.Saved += onSaved;
            session.RunAsync(default).Forget();
            return new Harness { host = host, session = session, hud = hud, flow = flow, run = run };
        }

        static void Drive(Harness harness, int maxTicks, Func<Harness, bool> stop)
        {
            while (harness.ticks < maxTicks && harness.session.IsRunning && !stop(harness))
            {
                harness.session.Tick(Dt);
                harness.ticks++;
            }
        }

        static bool Check(StringBuilder report, string name, bool condition)
        {
            if (!condition) report.Append(" [failed: ").Append(name).Append(']');
            return condition;
        }

        static int LivingEnemies(RunState run)
        {
            var count = 0;
            foreach (var enemy in run.board.enemies)
            {
                if (enemy.type != EnemyType.BoneWall) count++;
            }

            return count;
        }

        // The hidden host is destroyed again, so a scene that was clean before the run is reloaded to drop the dirty flag.
        static void RestoreCleanScene(Scene scene, bool wasDirty, StringBuilder report)
        {
            if (wasDirty || !scene.isDirty) return;
            if (string.IsNullOrEmpty(scene.path))
            {
                report.Append(" [note: untitled scene left dirty]");
                return;
            }

            EditorSceneManager.OpenScene(scene.path);
        }

        #endregion
    }
}
