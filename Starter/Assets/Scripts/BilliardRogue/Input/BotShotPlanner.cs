#nullable enable

using System;
using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using Nex.Utils;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The auto-aim bot's search (TDD §6): scores launch positions × aim angles with BallSimulator.PredictPath on a
    /// private read-only simulator (PredictPath never mutates the run) and keeps the best. Each predicted bounce is
    /// matched to the enemy, crate or shielded face it touches: damaging contacts score more the lower the enemy
    /// stands (danger row first) and when they kill it, contacts after a predicted kill are ignored (the real ball
    /// would fly on), shield blocks are penalized, crossed pickups add a bonus. The search runs incrementally
    /// (Step with a candidate budget) so it never spikes a frame. Allocation-free after construction.
    /// </summary>
    public sealed class BotShotPlanner
    {
        const int PathCapacity = 64;
        const int MaxTargets = 96;
        const float ContactSlack = 0.06f;
        const float AngleMarginDeg = 1f;
        const float PickupReach = 0.3f;
        // Equal scores prefer less walking and straighter shots, like a person would.
        const float TieBreakWeight = 0.01f;

        readonly GameRules rules;
        readonly ControlConfig config;
        readonly BallSimulator predictor;
        readonly Vector2[] points = new Vector2[PathCapacity];
        readonly int[] dealt = new int[MaxTargets];
        readonly bool[] collected = new bool[MaxTargets];
        readonly int damagePerHit;
        int launchSamples;
        int angleSamples;
        int next;
        int total;
        float startLaunch01;
        float bestScore;

        public BotShotPlanner(GameRules aRules, ControlConfig aConfig)
        {
            rules = aRules;
            config = aConfig;
            predictor = new BallSimulator(rules, new BoardOps(rules));
            // The bot does not know the sequencer's next ball: it assumes a level-1 Basic hit.
            damagePerHit = Mathf.Max(1, rules.balls[(int)BallType.Basic].levels[0].damage);
        }

        public bool IsSearching => next < total;
        public float BestLaunch01 { get; private set; } = 0.5f;
        public Vector2 BestDirection { get; private set; } = Vector2.up;
        public float BestScore => bestScore;

        #region Public Methods

        /// <summary>Starts a new search; currentLaunch01 is where the cat stands now (tie-break toward less walking).</summary>
        public void Begin(float currentLaunch01)
        {
            launchSamples = Mathf.Max(1, config.BotLaunchSamples);
            angleSamples = Mathf.Max(2, config.BotSampleCount);
            total = launchSamples * angleSamples;
            next = 0;
            startLaunch01 = currentLaunch01;
            bestScore = float.NegativeInfinity;
            BestLaunch01 = 0.5f;
            BestDirection = Vector2.up;
        }

        /// <summary>Scores up to budget more candidates against the current board.</summary>
        public void Step(RunState run, int budget)
        {
            var arena = rules.arena;
            var minAngle = arena.minAimAngleDeg + AngleMarginDeg;
            var maxAngle = 180f - arena.minAimAngleDeg - AngleMarginDeg;
            var end = Mathf.Min(total, next + Mathf.Max(1, budget));
            for (; next < end; next++)
            {
                var launch = (next / angleSamples + 0.5f) / launchSamples;
                var angle = Mathf.Lerp(minAngle, maxAngle, next % angleSamples / (angleSamples - 1f));
                var direction = Vector2Utils.PolarDeg(angle);
                var score = Score(run, ArenaGeometry.LaunchOrigin(arena, launch), direction)
                            - TieBreakWeight * (Mathf.Abs(launch - startLaunch01) + Mathf.Abs(angle - 90f) / 90f);
                if (score <= bestScore) continue;
                bestScore = score;
                BestLaunch01 = launch;
                BestDirection = direction;
            }
        }

        #endregion

        #region Scoring

        float Score(RunState run, Vector2 origin, Vector2 direction)
        {
            var count = predictor.PredictPath(run, origin, direction, config.BotPathLength, config.BotPathBounces, points);
            var board = run.board;
            var enemyCount = Mathf.Min(board.enemies.Count, MaxTargets);
            Array.Clear(dealt, 0, enemyCount);
            Array.Clear(collected, 0, Mathf.Min(board.pickups.Count, MaxTargets));
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            var score = 0f;
            for (var i = 1; i < count; i++)
            {
                var point = points[i];
                score += PickupsAlong(board.pickups, points[i - 1], point);
                var index = FindEnemyContact(board.enemies, enemyCount, point, out var face);
                if (index < 0)
                {
                    if (TouchesCrate(board.fieldObjects, point)) score += config.BotCrateScore;
                    continue;
                }

                var enemy = board.enemies[index];
                if (enemy.shieldFace != Face.None && face == enemy.shieldFace)
                {
                    score -= config.BotShieldPenalty;
                    continue;
                }

                if (dealt[index] >= enemy.hp) continue;
                dealt[index] += damagePerHit;
                var bottomRow = enemy.row + enemy.height - 1;
                var depth01 = dangerRow > 0 ? Mathf.Clamp01(bottomRow / (float)dangerRow) : 1f;
                score += config.BotHitScore + config.BotRowWeight * depth01;
                if (bottomRow >= dangerRow) score += config.BotDangerRowBonus;
                if (dealt[index] >= enemy.hp) score += config.BotKillBonus * (1f + depth01);
            }

            return score;
        }

        /// <summary>Index of the enemy the ball touches at a bounce point (deepest overlap), -1 for none.</summary>
        int FindEnemyContact(List<EnemyState> enemies, int enemyCount, Vector2 point, out Face face)
        {
            face = Face.None;
            var arena = rules.arena;
            var radius = arena.ballRadius + ContactSlack;
            var best = -1;
            var bestDepth = float.NegativeInfinity;
            for (var i = 0; i < enemyCount; i++)
            {
                if (!BallCollision.CircleRect(BallCollision.EnemyRect(arena, enemies[i]), point, radius, out var normal, out var depth)) continue;
                if (depth <= bestDepth) continue;
                best = i;
                bestDepth = depth;
                face = BallCollision.FaceFromNormal(normal);
            }

            return best;
        }

        bool TouchesCrate(List<FieldObjectState> objects, Vector2 point)
        {
            var arena = rules.arena;
            var radius = arena.ballRadius + ContactSlack;
            for (var i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                if (o.type != FieldObjectType.Crate) continue;
                if (BallCollision.CircleRect(ArenaGeometry.CellRect(arena, o.col, o.row), point, radius, out _, out _)) return true;
            }

            return false;
        }

        float PickupsAlong(List<PickupState> pickups, Vector2 from, Vector2 to)
        {
            var arena = rules.arena;
            var reach = PickupReach + arena.ballRadius;
            var count = Mathf.Min(pickups.Count, MaxTargets);
            var score = 0f;
            for (var i = 0; i < count; i++)
            {
                if (collected[i]) continue;
                var pickup = pickups[i];
                var center = ArenaGeometry.CellCenter(arena, pickup.col, pickup.row);
                if (DistanceToSegment(center, from, to) > reach) continue;
                collected[i] = true;
                score += config.BotPickupScore;
            }

            return score;
        }

        static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var segment = to - from;
            var lengthSq = segment.sqrMagnitude;
            var t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSq) : 0f;
            return (from + segment * t - point).magnitude;
        }

        #endregion
    }
}
