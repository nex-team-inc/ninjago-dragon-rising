#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>Every event of one shot driven at 60 Hz, tagged with the frame it was emitted on (frame 0 = launch).</summary>
    public sealed class ShotLog
    {
        public readonly List<SimEvent> events = new();
        public readonly List<int> frames = new();
        public int frameCount;
        public int stillActive;

        public float Seconds => frameCount / SimTest.Fps;

        public int Count(SimEventKind kind)
        {
            var n = 0;
            foreach (var ev in events)
            {
                if (ev.kind == kind) n++;
            }
            return n;
        }

        public int HitsOn(int enemyId, int fromFrame = 0, int toFrame = int.MaxValue)
        {
            var n = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].kind != SimEventKind.EnemyHit || events[i].targetId != enemyId) continue;
                if (frames[i] >= fromFrame && frames[i] <= toFrame) n++;
            }
            return n;
        }

        public int FirstFrame(SimEventKind kind)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].kind == kind) return frames[i];
            }
            return -1;
        }

        public SimEvent First(SimEventKind kind)
        {
            foreach (var ev in events)
            {
                if (ev.kind == kind) return ev;
            }
            return default;
        }

        public int Sum(SimEventKind kind)
        {
            var total = 0;
            foreach (var ev in events)
            {
                if (ev.kind == kind) total += ev.value;
            }
            return total;
        }

        public void Append(List<SimEvent> batch, int frame)
        {
            foreach (var ev in batch)
            {
                events.Add(ev);
                frames.Add(frame);
            }
        }
    }

    /// <summary>Board setup and shot driving shared by the simulation tests (public API only).</summary>
    public static class SimTest
    {
        public const float Fps = 60f;
        public const int MaxFrames = 60 * 30;
        public const int ImmortalHp = 100000;

        public static GameRules Rules() => TestRules.Create();

        /// <summary>A run with an empty plan so the enemy phase spawns nothing on its own.</summary>
        public static RunState NewRun(GameRules rules, int seed = 1)
        {
            var run = new RunFactory().NewRun(rules, seed, 1);
            run.stage = new StagePlan();
            return run;
        }

        public static EnemyState Put(RunState run, BoardOps ops, EnemyType type, int col, int row, int hp = ImmortalHp)
        {
            var e = ops.SpawnEnemy(run, type, col, row, new List<SimEvent>());
            e.hp = hp;
            e.maxHp = hp;
            return e;
        }

        public static FieldObjectState PutObject(RunState run, FieldObjectType type, int id, int col, int row, int hp = 0)
        {
            var o = new FieldObjectState { id = id, type = type, col = col, row = row, hp = hp };
            run.board.fieldObjects.Add(o);
            if (id >= run.board.nextId) run.board.nextId = id + 1;
            return o;
        }

        public static BallInstance Ball(BallType type, int level = 1) => new() { type = type, level = level };

        public static Vector2 Direction(float angleDeg) => new(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));

        public static Vector2 LaunchAt(GameRules rules, float x) => new(x, rules.arena.launchY);

        /// <summary>Launches from origin at angleDeg (from +x) and steps until every ball has exited or maxFrames passed.</summary>
        public static ShotLog Shoot(BallSimulator sim, RunState run, BallInstance ball, Vector2 origin, float angleDeg, bool powerShot = false, int maxFrames = MaxFrames)
        {
            var log = new ShotLog();
            var events = new List<SimEvent>();
            sim.Launch(run, ball, origin, Direction(angleDeg), powerShot, 0, events);
            log.Append(events, 0);
            events.Clear();
            while (sim.ActiveCount > 0 && log.frameCount < maxFrames)
            {
                sim.Step(run, 1f / Fps, events);
                log.frameCount++;
                log.Append(events, log.frameCount);
                events.Clear();
            }
            log.stillActive = sim.ActiveCount;
            return log;
        }

        /// <summary>Velocity of the single ball in flight (zero when none).</summary>
        public static Vector2 Velocity(BallSimulator sim)
        {
            var velocity = Vector2.zero;
            sim.ForEachBall((in BallSnapshot ball) => velocity = ball.velocity);
            return velocity;
        }

        /// <summary>Upper bound on any flight: maxFlightSeconds + maxStallSeconds + the ghost drop from the top wall.</summary>
        public static float ExitBoundSeconds(GameRules rules)
        {
            var a = rules.arena;
            return a.maxFlightSeconds + a.maxStallSeconds + ArenaGeometry.TopWallY(a) / a.ballSpeed;
        }

        public static List<SimEvent> Resolve(EnemyPhaseResolver resolver, RunState run, SimRandom rng)
        {
            var events = new List<SimEvent>();
            resolver.Resolve(run, rng, events);
            return events;
        }

        public static int Count(List<SimEvent> events, SimEventKind kind)
        {
            var n = 0;
            foreach (var ev in events)
            {
                if (ev.kind == kind) n++;
            }
            return n;
        }
    }
}
