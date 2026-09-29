#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor.Tests
{
    /// <summary>
    /// Headless GameSession rules (GameplaySmokeFakes, real config assets): the GDD §9 save points and the committed
    /// outcome, the shot cooldown gate, the stage-cleared fire guard, the shots per turn, 2P shooting together and the
    /// tail tracking check.
    /// </summary>
    public class GameplaySessionTests
    {
        const string ConfigPath = "Assets/Configs/BilliardRogue/BilliardRogueConfig.asset";
        const float Dt = 1f / 60f;
        const int MaxTicks = 20000;

        /// <summary>Always tracked, strikes only while Armed; counts the consumes the loop attempts.</summary>
        sealed class ArmedInput : IShotInput
        {
            public bool Armed;
            public int Consumes;
            public bool IsTracking { get; set; } = true;
            public float LaunchX01 => 0.5f;
            public Vector2 AimDirection => Vector2.up;

            public bool TryConsumeStrike(out StrikeInfo strike)
            {
                Consumes++;
                strike = new StrikeInfo { direction = Vector2.up, power01 = 0.5f };
                if (!Armed) return false;
                Armed = false;
                return true;
            }

            public void ResetStrike() => Armed = false;
        }

        /// <summary>Always tracked and always striking; counts the strikes the loop fired.</summary>
        sealed class EagerInput : IShotInput
        {
            public int Strikes;
            public bool IsTracking => true;
            public float LaunchX01 { get; set; } = 0.5f;
            public Vector2 AimDirection => Vector2.up;

            public bool TryConsumeStrike(out StrikeInfo strike)
            {
                strike = new StrikeInfo { direction = Vector2.up, power01 = 0.5f };
                Strikes++;
                return true;
            }

            public void ResetStrike()
            {
            }
        }

        sealed class Harness : IDisposable
        {
            public readonly GameObject host;
            public readonly GameSession session;
            public readonly SmokeHud hud = new();
            public readonly SmokeFlowHost flow = new();
            public readonly MemoryRunStore store = new();
            public readonly RunPersistence persistence;
            public readonly RunState run;
            public readonly GameRules rules;
            public readonly BilliardRogueConfig config;
            public UniTask<RunOutcome> outcome;
            public int ticks;

            public Harness(int players, IShotInput? input = null, DebugSettings? debug = null, IShotInput[]? perPlayer = null)
            {
                config = AssetDatabase.LoadAssetAtPath<BilliardRogueConfig>(ConfigPath);
                Assert.IsNotNull(config, "run ConfigAssetsBuilder first");
                rules = RulesFactory.Build(config);
                persistence = new RunPersistence(store);
                run = new RunFactory().NewRun(rules, 4321, players);
                host = new GameObject("GameplaySessionTests") { hideFlags = HideFlags.HideAndDontSave };
                var timeScale = host.AddComponent<TimeScaleController>();
                session = host.AddComponent<GameSession>();
                var inputs = new IShotInput[players];
                for (var i = 0; i < players; i++)
                {
                    inputs[i] = perPlayer?[i] ?? input ?? new SmokeShotInput(run, rules, 7 + i);
                }

                session.Initialize(new GameSessionContext
                {
                    config = config, rules = rules, run = run, isContinue = false, hud = hud, flowHost = flow, inputs = inputs,
                    persistence = persistence, analytics = null, timeScale = timeScale, headless = true, debugSettings = debug ?? new DebugSettings(),
                });
                outcome = session.RunAsync(default);
            }

            public TurnPhase Phase => session.Phase;

            /// <summary>Ticks until stop() holds after a tick (or the run ends); returns false on the tick budget.</summary>
            public bool DriveUntil(Func<bool> stop)
            {
                while (ticks < MaxTicks && session.IsRunning)
                {
                    session.Tick(Dt);
                    ticks++;
                    if (stop()) return true;
                }

                return false;
            }

            public void Tick(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    session.Tick(Dt);
                    ticks++;
                }
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(host);
                Time.timeScale = 1f;
            }
        }

        #region Save points

        [Test]
        public void EnemyPhaseIsSavedBeforeItPlays()
        {
            using var h = new Harness(1);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.EnemyPhase), "first enemy phase");
            var saved = h.store.LoadRun();
            Assert.IsNotNull(saved);
            Assert.AreEqual(h.run.turnInStage, saved!.turnInStage, "the resolved phase (turn counted) is on disk before its playback");
            Assert.AreEqual(1, saved.stats.turns);
            Assert.AreEqual(RunOutcome.None, saved.outcome);
        }

        [Test]
        public void StageClearSavesTheRewardPoint()
        {
            using var h = new Harness(1);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            Assert.IsTrue(DebugHooks.ClearStage().EndsWith("ok"));
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.StageClear), "stage clear");
            var saved = h.store.LoadRun();
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved!.awaitingReward, "a quit during the sting resumes at the reward");
        }

        [Test]
        public void DefeatIsCommittedWhenTheSimDecidesItAndSaveAndQuitKeepsIt()
        {
            using var h = new Harness(1);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            ArrangeLethalPhase(h);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.EnemyPhase), "lethal enemy phase");
            Assert.AreEqual(RunOutcome.Defeat, h.run.outcome, "the sim decided the defeat");
            Assert.IsNull(h.store.LoadRun(), "the save is dropped as soon as the outcome is decided");
            Assert.AreEqual(h.run.stageNumber, h.store.MetaProgress.bestStageNumber, "meta progress applied before the sequence");

            // Save & Quit while the phase / defeat sequence would still be playing: the outcome stands.
            h.session.RequestSaveAndQuit();
            Assert.IsTrue(h.outcome.Status.IsCompleted());
            Assert.AreEqual(RunOutcome.Defeat, h.outcome.GetAwaiter().GetResult());
            Assert.IsFalse(h.persistence.HasSave);
        }

        [Test]
        public void SaveAndQuitBeforeTheDecisionStaysAbandoned()
        {
            using var h = new Harness(1);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            h.session.RequestSaveAndQuit();
            Assert.AreEqual(RunOutcome.Abandoned, h.outcome.GetAwaiter().GetResult());
            Assert.IsTrue(h.persistence.HasSave);
            Assert.AreEqual(RunOutcome.None, h.store.LoadRun()!.outcome);
        }

        #endregion

        #region Player turn

        [Test]
        public void StrikeDuringTheCooldownStaysPendingAndFiresAfterIt()
        {
            var input = new ArmedInput();
            using var h = new Harness(1, input);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            input.Armed = true;
            h.Tick(1);
            Assert.AreEqual(1, h.run.stats.shots);
            var consumesAfterFirst = input.Consumes;

            input.Armed = true;
            var cooldownTicks = Mathf.CeilToInt(h.config.Pacing.ShotCooldown / Dt);
            h.Tick(cooldownTicks - 1);
            Assert.AreEqual(1, h.run.stats.shots, "inside the cooldown");
            Assert.AreEqual(consumesAfterFirst, input.Consumes, "the strike is not consumed (and lost) while the shot cannot fire");
            Assert.IsTrue(input.Armed);

            h.Tick(3);
            Assert.AreEqual(2, h.run.stats.shots, "fires once the cooldown ends, without a new strike");
            Assert.IsFalse(input.Armed);
        }

        [Test]
        public void StrikeExpiryOutlastsTheShotCooldown()
        {
            var config = AssetDatabase.LoadAssetAtPath<BilliardRogueConfig>(ConfigPath);
            Assert.IsNotNull(config, "run ConfigAssetsBuilder first");
            // A strike made during the cooldown stays pending until the cooldown ends; a shorter expiry would drop it.
            Assert.GreaterOrEqual(config.Control.StrikeExpirySeconds, config.Pacing.ShotCooldown + 0.1f);
        }

        [Test]
        public void NoShotAfterTheStageFellMidTurn()
        {
            var input = new ArmedInput();
            using var h = new Harness(1, input);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            Assert.IsTrue(DebugHooks.ClearStage().EndsWith("ok"));
            h.Tick(1);
            input.Armed = true;
            h.Tick(5);
            Assert.AreEqual(0, h.run.stats.shots, "the stage is cleared: strikes no longer launch balls");
            Assert.IsTrue(input.Armed, "and are not consumed either");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TurnEndsAfterShotsPerTurnAndTheEnemiesAdvance(bool practiceMode)
        {
            var input = new ArmedInput();
            using var h = new Harness(1, input, new DebugSettings { practiceMode = practiceMode });
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            var shotsPerTurn = h.rules.balance.shotsPerTurn;
            Assert.Greater(h.run.bag.Count, shotsPerTurn, "the opening bag holds more balls than one turn fires");
            var rowsBefore = new Dictionary<int, int>();
            foreach (var enemy in h.run.board.enemies)
            {
                // Immortal, so no shot can clear the field and cut the turn short.
                enemy.hp = enemy.maxHp = 100000;
                rowsBefore[enemy.id] = enemy.row;
            }

            for (var shot = 0; shot < shotsPerTurn; shot++)
            {
                input.Armed = true;
                Assert.IsTrue(h.DriveUntil(() => h.run.stats.shots == shot + 1), "shot " + (shot + 1));
            }

            input.Armed = true;
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.EnemyPhase), "the turn ends after shotsPerTurn balls");
            Assert.AreEqual(shotsPerTurn, h.run.stats.shots, "the rest of the bag waits for the next turn");
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn), "next turn");

            var advanced = 0;
            foreach (var enemy in h.run.board.enemies)
            {
                if (rowsBefore.TryGetValue(enemy.id, out var row) && enemy.row > row) advanced++;
            }

            Assert.Greater(advanced, 0, "the enemies stepped toward the player at the turn end");
            Assert.AreEqual(shotsPerTurn % h.run.bag.Count, h.run.nextBagIndex, "the next turn continues the bag rotation");
        }

        [Test]
        public void TwoPlayersShootTogetherWithTheirOwnShotsAgainstDoubleHpEnemies()
        {
            var p1 = new EagerInput { LaunchX01 = 0.25f };
            var p2 = new EagerInput { LaunchX01 = 0.75f };
            using var h = new Harness(2, perPlayer: new IShotInput[] { p1, p2 });
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            Assert.AreEqual(0, h.run.stageNumber);
            foreach (var enemy in h.run.board.enemies)
            {
                var enemyRules = h.rules.enemies[(int)enemy.type];
                Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(enemyRules.hp * h.rules.balance.coopEnemyHpScale)), enemy.maxHp, enemy.type.ToString());
                // Immortal, so no shot can clear the field and cut the turn short.
                enemy.hp = enemy.maxHp = 100000;
            }

            // No +1 Ball pickups either: each player fires exactly their own shots.
            h.run.board.pickups.Clear();
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.EnemyPhase), "the turn ends once both players used their shots");
            var shots = h.rules.balance.shotsPerTurn;
            Assert.AreEqual(shots, p1.Strikes, "P1 fired their own shots");
            Assert.AreEqual(shots, p2.Strikes, "P2 fired theirs in the same turn");
            Assert.AreEqual(-1, h.hud.ActivePlayer, "the HUD shows both players active");
        }

        [Test]
        public void TrackingIsNotRequiredOnceTheLastBallIsFired()
        {
            var input = new ArmedInput();
            using var h = new Harness(1, input);
            Assert.IsTrue(h.DriveUntil(() => h.Phase == TurnPhase.PlayerTurn));
            var total = h.rules.balance.shotsPerTurn;
            for (var shot = 0; shot < total; shot++)
            {
                input.Armed = true;
                Assert.IsTrue(h.DriveUntil(() => h.run.stats.shots == shot + 1), "shot " + (shot + 1));
            }

            input.IsTracking = false;
            var lostTicks = Mathf.CeilToInt(h.config.Control.TrackingLostSeconds / Dt) + 5;
            for (var i = 0; i < lostTicks && h.Phase == TurnPhase.PlayerTurn; i++)
            {
                h.Tick(1);
            }

            Assert.AreEqual(0, h.flow.TrackingLostShown, "the balls roll out on their own; nobody has to stay in view");
            Assert.AreNotEqual(TurnPhase.TrackingLost, h.Phase);
        }

        #endregion

        #region Helpers

        // One HP, every enemy immortal, and one melee enemy standing in the danger row: the next phase kills the player.
        static void ArrangeLethalPhase(Harness h)
        {
            var run = h.run;
            run.playerHp = 1;
            var dangerRow = ArenaGeometry.DangerRow(h.rules.arena);
            EnemyState? attacker = null;
            foreach (var enemy in run.board.enemies)
            {
                enemy.hp = enemy.maxHp = 100000;
                var enemyRules = h.rules.enemies[(int)enemy.type];
                if (attacker == null && enemy.attack > 0 && !enemyRules.ranged) attacker = enemy;
            }

            Assert.IsNotNull(attacker, "a melee enemy on the opening board");
            attacker!.row = dangerRow - attacker.height + 1;
            attacker.status.frozenTurns = 0;
        }

        #endregion
    }
}
