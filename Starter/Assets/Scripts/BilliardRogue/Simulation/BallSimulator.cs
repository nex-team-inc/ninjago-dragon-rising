#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Fixed-substep 2D ball physics in sim space: elastic reflections off the left/right/top walls, enemy
    /// footprints (inset per rules) and static objects; open bottom exit; per-ball abilities (GDD §5); pickups,
    /// portals, mud, anti-stall. Allocation-free per step; balls are pooled internally. A Splitter's BallSplit
    /// event ends the parent ball; its minis then exit individually (BallExited, flag = isMini).
    /// </summary>
    public sealed class BallSimulator
    {
        const int Capacity = 96;
        const int MaxSubstepsPerStep = 120;

        readonly GameRules rules;
        readonly BoardOps ops;
        readonly BallHitResolver hitResolver;
        readonly BallTriggers triggers;
        readonly BallSlot[] slots = new BallSlot[Capacity];
        float accumulator;
        int nextBallId = 1;
        int activeCount;
        float hypeSpeed = 1f;
        float hypeDamage = 1f;
        int hypeMinBonus;

        #region Life Cycle

        public BallSimulator(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
            hitResolver = new BallHitResolver(rules, ops);
            triggers = new BallTriggers(rules, ops);
            for (var i = 0; i < Capacity; i++)
            {
                slots[i] = new BallSlot();
            }
        }

        #endregion

        /// <summary>Number of balls (including minis) still in flight.</summary>
        public int ActiveCount => activeCount;

        /// <summary>Current Hype speed multiplier (1 = none), as clamped by SetHype.</summary>
        public float HypeSpeedMultiplier => hypeSpeed;

        /// <summary>Current Hype damage multiplier (1 = none), as clamped by SetHype.</summary>
        public float HypeDamageMultiplier => hypeDamage;

        #region Public Methods

        /// <summary>
        /// Fires one ball from origin (on the launch line, see ArenaGeometry.LaunchOrigin) along direction (already
        /// clamped) at ballSpeed × speedMultiplier. powerShot adds balance.powerShotBonusDamage to the first enemy
        /// hit; a Power pickup doubles the first hit and is consumed (PowerShotConsumed). Emits BallLaunched
        /// (value = level, value2 = shooterIndex, position2 = direction, flag = powerShot) and increments stats.shots.
        /// </summary>
        public void Launch(RunState run, BallInstance ball, Vector2 origin, Vector2 direction, bool powerShot, int shooterIndex, List<SimEvent> events)
        {
            var slot = Acquire();
            if (slot == null) return;
            var stats = hitResolver.LevelStats(ball.type, ball.level);
            run.stats.shots++;
            slot.Begin(nextBallId++, ball.type, ball.level, false, shooterIndex, run.stats.shots, 0, origin, direction.normalized,
                rules.arena.ballSpeed * stats.speedMultiplier, rules.arena.ballRadius);
            slot.powerShot = powerShot;
            events.Add(new SimEvent
            {
                kind = SimEventKind.BallLaunched, ballId = slot.id, ballType = ball.type, value = ball.level, value2 = shooterIndex,
                position = origin, position2 = slot.direction, flag = powerShot,
            });
            if (!run.powerPickupArmed) return;
            run.powerPickupArmed = false;
            slot.powerPickup = true;
            events.Add(new SimEvent { kind = SimEventKind.PowerShotConsumed, ballId = slot.id, ballType = ball.type, position = origin });
        }

        /// <summary>
        /// Advances every ball by dt using substepsPerSecond internally. Handles wall and pillar bounces (BallWallBounce
        /// with sourceId = pillar id, 0 for an arena wall; value = wall bounces so far, which feed the Rubber bonus and
        /// maxBounces), enemy hits via BallHitResolver/BoardOps (shield BLOCK from the shielded face, pierce, split,
        /// bomb area, chain lightning, freeze/burn/poison procs, crit, vampire heal cap, combo per ball → ComboChanged),
        /// crates, pickups, portal teleports with re-entry lock, mud slow, and BallExited (value = combo,
        /// value2 = bounces) when y &lt; 0. Anti-stall: after maxFlightSeconds, maxIdleWallBounces wall bounces since
        /// the last new solid (juggling one enemy between the walls counts as idle) or the level's maxBounces, a growing
        /// downward pull applies; once it has run for maxStallSeconds the ball ignores every solid and drops straight
        /// out at ballSpeed, so a flight never exceeds maxFlightSeconds + maxStallSeconds + TopWallY / ballSpeed.
        /// </summary>
        public void Step(RunState run, float dt, List<SimEvent> events)
        {
            if (activeCount == 0)
            {
                accumulator = 0f;
                return;
            }
            // Hype speeds balls up: split every substep so no ball moves further per substep than at 1× (no tunnelling).
            var split = HypeSubstepSplit();
            var h = 1f / (rules.arena.substepsPerSecond * split);
            var maxSteps = MaxSubstepsPerStep * split;
            accumulator += dt;
            var steps = 0;
            while (accumulator >= h && steps < maxSteps)
            {
                accumulator -= h;
                steps++;
                for (var i = 0; i < Capacity; i++)
                {
                    var b = slots[i];
                    if (b.active) Substep(run, b, h, events);
                }
            }
            if (steps == maxSteps) accumulator = 0f;
        }

        /// <summary>Visits every active ball (id, type, level, position, velocity, isMini, radius) for presentation.</summary>
        public void ForEachBall(BallVisitor visitor)
        {
            for (var i = 0; i < Capacity; i++)
            {
                var b = slots[i];
                if (!b.active) continue;
                var snapshot = new BallSnapshot(b.id, b.type, b.level, b.position, b.direction * CurrentSpeed(b), b.isMini, b.radius);
                visitor(in snapshot);
            }
        }

        /// <summary>
        /// Traces the aim guide from origin along dir against walls, enemies and objects without mutating state,
        /// stopping after maxLength sim units, after maxBounces reflections (the point of the next contact is the
        /// last one), at a portal or at the exit. Writes the polyline (origin first) into pointsOut and returns the
        /// number of points written (≤ pointsOut.Length). Uses the same substep length and collision code as Step.
        /// </summary>
        public int PredictPath(RunState run, Vector2 origin, Vector2 dir, float maxLength, int maxBounces, Vector2[] pointsOut)
        {
            if (pointsOut.Length == 0) return 0;
            var a = rules.arena;
            var position = origin;
            var direction = dir.normalized;
            var count = 0;
            pointsOut[count++] = origin;
            var stepLength = a.ballSpeed / a.substepsPerSecond;
            var travelled = 0f;
            var reflections = 0;
            while (travelled < maxLength && count < pointsOut.Length)
            {
                position += direction * stepLength;
                travelled += stepLength;
                if (position.y < 0f || triggers.PortalIndexAt(run.board, position, a.ballRadius) >= 0)
                {
                    pointsOut[count++] = position;
                    return count;
                }
                var bounced = BallCollision.CollideWalls(a, ref position, ref direction, a.ballRadius);
                if (BallCollision.FindSolidContact(a, run.board, position, direction, a.ballRadius, null, 0, out var contact))
                {
                    position += contact.normal * contact.depth;
                    direction = BallCollision.Reflect(direction, contact.normal);
                    bounced = true;
                }
                if (!bounced) continue;
                pointsOut[count++] = position;
                reflections++;
                if (reflections > maxBounces) return count;
            }
            if (count < pointsOut.Length) pointsOut[count++] = position;
            return count;
        }

        /// <summary>
        /// Hype from body motion (GDD v2 §3), applied every step to all balls in flight: speed and damage multipliers
        /// (1 = no change). Speed is clamped to 1..balance.maxHypeSpeedMultiplier and scales every ball's current speed
        /// (mud still slows on top; the aim guide never uses it); Step re-derives the substep length from it. Damage
        /// (≥ 1) scales every ball hit (direct, chain, explosion, crate), rounded half up; EnemyHit.hypeBonus reports
        /// the added part. Stays in effect until changed; Clear resets it. Deterministic: the same (dt, hype) call
        /// sequence gives the same events.
        /// </summary>
        public void SetHype(float speedMultiplier, float damageMultiplier)
        {
            SetHype(speedMultiplier, damageMultiplier, 0);
        }

        /// <summary>SetHype with a floor on the added damage per hit (GDD v2 §3: at least +1 from Hype tier 2).</summary>
        public void SetHype(float speedMultiplier, float damageMultiplier, int minBonusDamage)
        {
            hypeSpeed = Mathf.Clamp(float.IsNaN(speedMultiplier) ? 1f : speedMultiplier, 1f, Mathf.Max(1f, rules.balance.maxHypeSpeedMultiplier));
            hypeDamage = float.IsNaN(damageMultiplier) ? 1f : Mathf.Max(1f, damageMultiplier);
            hypeMinBonus = Mathf.Max(0, minBonusDamage);
            hitResolver.SetHype(hypeDamage, hypeMinBonus);
        }

        /// <summary>Removes every ball without emitting events (stage transitions, abandon) and resets Hype.</summary>
        public void Clear()
        {
            for (var i = 0; i < Capacity; i++)
            {
                slots[i].active = false;
            }
            activeCount = 0;
            accumulator = 0f;
            SetHype(1f, 1f, 0);
        }

        #endregion

        #region Helpers

        void Substep(RunState run, BallSlot b, float h, List<SimEvent> events)
        {
            var a = rules.arena;
            b.flightTime += h;
            b.portalLock -= h;
            b.slowTimer -= h;
            UpdateStall(b, h);
            if (b.stalled && b.stallTime > a.maxStallSeconds)
            {
                // The pull lost against the geometry (a ball resting on enemy top faces converges to vertical): drop
                // straight out through everything so the flight ends within TopWallY / ballSpeed.
                b.direction = Vector2.down;
                b.position.y -= a.ballSpeed * hypeSpeed * h;
                if (b.position.y < 0f) Exit(b, events);
                return;
            }
            b.position += b.direction * (CurrentSpeed(b) * h);
            if (b.position.y < 0f)
            {
                Exit(b, events);
                return;
            }
            if (BallCollision.CollideWalls(a, ref b.position, ref b.direction, b.radius))
            {
                b.bounces++;
                b.wallBounces++;
                b.idleWallBounces++;
                events.Add(WallBounce(b, 0));
            }
            triggers.CollectPickups(run, b, events);
            if (triggers.TryTeleport(run, b, events)) return;
            triggers.UpdateMud(run, b, events);
            if (b.piercedCount > 0) triggers.PrunePierced(run.board, b);
            if (!BallCollision.FindSolidContact(a, run.board, b.position, b.direction, b.radius, b.piercedIds, b.piercedCount, out var contact)) return;
            var solidId = contact.kind == BallContactKind.Enemy ? contact.enemy!.id : contact.fieldObject!.id;
            // Bouncing between the walls and one solid is a juggle, not progress: only a new solid resets the idle count.
            if (solidId != b.lastSolidId)
            {
                b.idleWallBounces = 0;
                b.lastSolidId = solidId;
            }
            if (contact.kind == BallContactKind.Enemy)
            {
                var outcome = hitResolver.Resolve(run, b, contact.enemy!, contact.normal, events);
                if (outcome == HitOutcome.PassThrough) return;
                Bounce(b, contact);
                if (outcome == HitOutcome.Split) Split(b, contact.normal, events);
                return;
            }
            Bounce(b, contact);
            if (contact.kind == BallContactKind.Crate)
            {
                ops.DamageCrate(run, contact.fieldObject!, CrateDamage(b), events);
                return;
            }
            events.Add(WallBounce(b, contact.fieldObject!.id));
        }

        void UpdateStall(BallSlot b, float h)
        {
            var a = rules.arena;
            if (!b.stalled)
            {
                var maxBounces = hitResolver.LevelStats(b.type, b.level).maxBounces;
                b.stalled = b.flightTime > a.maxFlightSeconds || b.idleWallBounces >= a.maxIdleWallBounces
                    || (maxBounces > 0 && b.bounces >= maxBounces);
                if (!b.stalled) return;
            }
            b.stallTime += h;
            var pull = a.antiStallAccel * (1f + b.stallTime) * h / Mathf.Max(CurrentSpeed(b), 0.01f);
            b.direction = (b.direction + Vector2.down * pull).normalized;
        }

        static void Bounce(BallSlot b, in BallContact contact)
        {
            b.position += contact.normal * contact.depth;
            b.direction = BallCollision.Reflect(b.direction, contact.normal);
            b.bounces++;
        }

        static SimEvent WallBounce(BallSlot b, int sourceId)
        {
            return new SimEvent { kind = SimEventKind.BallWallBounce, ballId = b.id, ballType = b.type, sourceId = sourceId, value = b.wallBounces, position = b.position };
        }

        void Split(BallSlot parent, Vector2 normal, List<SimEvent> events)
        {
            var stats = hitResolver.LevelStats(parent.type, parent.level);
            var count = stats.splitCount;
            events.Add(new SimEvent { kind = SimEventKind.BallSplit, ballId = parent.id, ballType = parent.type, value = count, position = parent.position });
            var baseAngle = Mathf.Atan2(parent.direction.y, parent.direction.x);
            for (var i = 0; i < count; i++)
            {
                var mini = Acquire();
                if (mini == null) break;
                var angle = baseAngle + (i - (count - 1) * 0.5f) * stats.splitFanDegrees * Mathf.Deg2Rad;
                // A grazing parent leaves part of the fan pointing back into the face it just hit; those minis go onward.
                var direction = BallCollision.Reflect(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), normal);
                mini.Begin(nextBallId++, parent.type, parent.level, true, parent.shooterIndex, parent.shotNumber, i + 1,
                    parent.position, direction, parent.speed, parent.radius * stats.miniRadiusScale);
                mini.flightTime = parent.flightTime;
                mini.combo = parent.combo;
                mini.hitIndex = parent.hitIndex;
                mini.healedThisShot = parent.healedThisShot;
                mini.areaUsed = parent.areaUsed;
                mini.splitUsed = true;
                mini.firstHitPending = false;
                mini.lastSolidId = parent.lastSolidId;
            }
            Release(parent);
        }

        int CrateDamage(BallSlot b)
        {
            var stats = hitResolver.LevelStats(b.type, b.level);
            return hitResolver.Hyped(Mathf.Max(1, b.isMini ? stats.splitDamage : stats.damage), out _);
        }

        float CurrentSpeed(BallSlot b)
        {
            var speed = b.speed * hypeSpeed;
            return b.slowTimer > 0f ? speed * rules.balance.mudSlowFactor : speed;
        }

        /// <summary>Substeps per base substep: ⌈hype speed⌉, so the distance per substep never exceeds the 1× one.</summary>
        int HypeSubstepSplit()
        {
            return hypeSpeed <= 1f ? 1 : Mathf.CeilToInt(hypeSpeed - 0.0001f);
        }

        void Exit(BallSlot b, List<SimEvent> events)
        {
            events.Add(new SimEvent
            {
                kind = SimEventKind.BallExited, ballId = b.id, ballType = b.type, value = b.combo, value2 = b.bounces,
                position = b.position, flag = b.isMini,
            });
            Release(b);
        }

        BallSlot? Acquire()
        {
            for (var i = 0; i < Capacity; i++)
            {
                var slot = slots[i];
                if (slot.active) continue;
                slot.active = true;
                activeCount++;
                return slot;
            }
            return null;
        }

        void Release(BallSlot b)
        {
            b.active = false;
            activeCount--;
        }

        #endregion
    }
}
