#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The per-frame work of a player turn (TDD §7): for every player (2P shoot together) the tracking check, aim preview
    /// (BallSimulator.PredictPath → BoardPresenter.SetAim) and strike → Launch; then the fixed-step simulation from the
    /// gameplay time scale, event drain into BoardPresenter / TurnFeedback / ShotResultTracker, straggler fast-forward and
    /// the turn-end condition (nobody has a shot left or the stage is cleared, no balls in flight, grace elapsed).
    /// Allocation-free per frame.
    /// </summary>
    public sealed class PlayerTurnLoop
    {
        readonly SessionServices services;
        readonly TurnFeedback feedback;
        readonly HypeController hype;
        readonly Vector2[] aimPoints;
        readonly float[] trackingLostSeconds;
        readonly bool[] warned;
        float stragglerSeconds;
        float graceRemaining;
        bool fastForward;

        public PlayerTurnLoop(SessionServices aServices)
        {
            services = aServices;
            feedback = new TurnFeedback(aServices);
            hype = new HypeController(aServices);
            aimPoints = new Vector2[Mathf.Max(2, aServices.Pacing.AimGuideMaxPoints)];
            trackingLostSeconds = new float[aServices.Inputs.Length];
            warned = new bool[aServices.Inputs.Length];
        }

        public bool IsTurnDone { get; private set; }

        /// <summary>Hype from body motion while balls fly (GDD v2 §3).</summary>
        public HypeController Hype => hype;

        /// <summary>First player with a shot left whose paws stayed untracked past ControlConfig.trackingLostSeconds, -1 otherwise.</summary>
        public int TrackingLostPlayer { get; private set; } = -1;

        #region Turn Boundaries

        public void BeginTurn()
        {
            services.Sequencer.BeginTurn(services.InfiniteBalls);
            var inputs = services.Inputs;
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i].ResetStrike();
            }

            stragglerSeconds = 0f;
            graceRemaining = services.Pacing.TurnEndGrace;
            ClearTrackingLost();
            IsTurnDone = false;
            SetFastForward(false);
            feedback.BeginTurn();
            services.Hud.RefreshQueue();
        }

        public void EndTurn()
        {
            services.Tracker.Flush();
            for (var p = 0; p < services.Inputs.Length; p++)
            {
                services.Board.SetAim(p, 0.5f, Vector2.up, 0, aimPoints, false);
                ClearTrackingWarning(p);
            }

            SetFastForward(false);
            hype.Reset();
            services.TimeScale.ResetEffects();
            services.Sequencer.EndTurn();
            ClearTrackingLost();
        }

        public void ClearTrackingLost()
        {
            TrackingLostPlayer = -1;
            Array.Clear(trackingLostSeconds, 0, trackingLostSeconds.Length);
        }

        /// <summary>The player still wants a strike this turn: they have a shot left and the stage did not fall mid-turn.</summary>
        bool WantsStrike(int player) => services.Sequencer.HasShot(player) && !feedback.StageCleared;

        bool CanFire(int player) => services.Sequencer.CanFire(player) && !feedback.StageCleared;

        #endregion

        #region Frame

        public void Tick(float unscaledDeltaTime)
        {
            var scaledDeltaTime = unscaledDeltaTime * services.TimeScale.GameplayTimeScale;
            var inputs = services.Inputs;
            // Real time: hit-stop and slow-mo must not stretch the cooldowns. This loop does not tick under the menu
            // pause or the tracking-lost hold, so both still freeze them.
            services.Sequencer.Tick(unscaledDeltaTime);
            for (var p = 0; p < inputs.Length; p++)
            {
                // A player with nothing left to shoot may step away: their balls roll out on their own.
                if (WantsStrike(p)) UpdateTracking(inputs[p], p, unscaledDeltaTime);
                else ClearTrackingWarning(p);
            }

            if (TrackingLostPlayer >= 0) return;

            for (var p = 0; p < inputs.Length; p++)
            {
                var input = inputs[p];
                UpdateAim(input, p);
                // Cooldown first: a strike made during it stays pending (until ControlConfig.strikeExpirySeconds) and
                // fires when the cooldown ends instead of being consumed and dropped.
                if (CanFire(p) && input.TryConsumeStrike(out var strike))
                {
                    Fire(p, strike);
                }
            }

            hype.Tick(unscaledDeltaTime, scaledDeltaTime);
            if (scaledDeltaTime > 0f) services.Sim.Step(scaledDeltaTime);
            Drain();
            services.Board.UpdateBalls(services.Sim.Balls);
            UpdateFastForward(unscaledDeltaTime);
            UpdateTurnEnd(scaledDeltaTime);
        }

        /// <summary>
        /// Fires the next ball at angleDeg (from +x, 90 = up) from the launch position of the first player who can
        /// fire (DebugHooks.Shoot).
        /// </summary>
        public bool ForceShoot(float angleDeg)
        {
            if (IsTurnDone || TrackingLostPlayer >= 0) return false;
            var radians = angleDeg * Mathf.Deg2Rad;
            for (var p = 0; p < services.Inputs.Length; p++)
            {
                if (!CanFire(p)) continue;
                Fire(p, new StrikeInfo { direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), power01 = 0.5f });
                return true;
            }

            return false;
        }

        #endregion

        #region Steps

        void Fire(int player, StrikeInfo strike)
        {
            var arena = services.Rules.arena;
            var input = services.Inputs[player];
            var ball = services.Sequencer.Fire(player);
            var origin = ArenaGeometry.LaunchOrigin(arena, input.LaunchX01);
            var direction = ArenaGeometry.ClampAim(arena, strike.direction);
            services.Sim.Launch(ball, origin, direction, strike.isPowerShot, player);
            services.Board.PlayStrike(player, strike.isPowerShot);
            services.Analytics.ShotFired(ball.type, ball.level, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, strike.power01, player);
            services.Hud.RefreshQueue();
            graceRemaining = services.Pacing.TurnEndGrace;
        }

        void Drain()
        {
            var events = services.Sim.Events;
            if (events.Count == 0) return;
            services.Board.Consume(events, services.Run);
            feedback.Consume(events);
            services.Tracker.Consume(events);
            events.Clear();
        }

        void UpdateAim(IShotInput input, int player)
        {
            var arena = services.Rules.arena;
            var launchX = input.LaunchX01;
            var origin = ArenaGeometry.LaunchOrigin(arena, launchX);
            var direction = ArenaGeometry.ClampAim(arena, input.AimDirection);
            var visible = WantsStrike(player);
            var count = 0;
            if (visible)
            {
                var pacing = services.Pacing;
                var length = pacing.AimGuideLength(services.AimGuideSetting);
                count = services.Sim.PredictPath(origin, direction, length, pacing.AimGuideMaxBounces, aimPoints);
            }

            services.Board.SetAim(player, launchX, direction, count, aimPoints, visible);
        }

        void UpdateFastForward(float unscaledDeltaTime)
        {
            var straggling = services.Sim.ActiveBalls > 0 && (!services.Sequencer.HasBallToFire || feedback.StageCleared);
            if (!straggling)
            {
                stragglerSeconds = 0f;
                SetFastForward(false);
                return;
            }

            stragglerSeconds += unscaledDeltaTime;
            if (stragglerSeconds >= services.Pacing.FastForwardDelay) SetFastForward(true);
        }

        void UpdateTurnEnd(float scaledDeltaTime)
        {
            // An empty field ends the turn too (GDD v2 §5): the enemy phase then brings the next batch in right away.
            var noMoreShots = !services.Sequencer.HasBallToFire || feedback.StageCleared || services.Sim.LivingEnemyCount() == 0;
            if (!noMoreShots || services.Sim.ActiveBalls > 0)
            {
                graceRemaining = services.Pacing.TurnEndGrace;
                return;
            }

            graceRemaining -= scaledDeltaTime;
            if (graceRemaining <= 0f) IsTurnDone = true;
        }

        void UpdateTracking(IShotInput input, int player, float unscaledDeltaTime)
        {
            if (input.IsTracking)
            {
                ClearTrackingWarning(player);
                return;
            }

            if (!warned[player])
            {
                warned[player] = true;
                services.Hud.SetTrackingWarning(player, true);
            }

            trackingLostSeconds[player] += unscaledDeltaTime;
            if (TrackingLostPlayer < 0 && trackingLostSeconds[player] >= services.TrackingLostSeconds) TrackingLostPlayer = player;
        }

        void ClearTrackingWarning(int player)
        {
            trackingLostSeconds[player] = 0f;
            if (!warned[player]) return;
            warned[player] = false;
            services.Hud.SetTrackingWarning(player, false);
        }

        void SetFastForward(bool on)
        {
            if (on == fastForward) return;
            fastForward = on;
            services.TimeScale.SetFastForward(on);
            services.Hud.SetFastForward(on);
        }

        #endregion
    }
}
