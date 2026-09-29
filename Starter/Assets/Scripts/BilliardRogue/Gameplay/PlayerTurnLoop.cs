#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The per-frame work of a player turn (TDD §7): for every player (2P shoot together) the tracking check, aim preview
    /// (BallSimulator.PredictPath → BoardPresenter.SetAim) and strike → a volley of the whole bag streamed along the
    /// locked aim, PacingConfig.volleyInterval apart; the dance POWER (HypeController, reset every turn); then the
    /// fixed-step simulation from the gameplay time scale, the event drain into BoardPresenter (with the flight's combo
    /// for the COMBO floats) / TurnFeedback / ShotResultTracker, straggler fast-forward and
    /// the turn-end condition (nobody has a shot left or the stage is cleared, no volley launching, no balls in flight,
    /// grace elapsed). Allocation-free per frame.
    /// </summary>
    public sealed class PlayerTurnLoop
    {
        /// <summary>One player's volley while it launches: next bag index (-1 = none), timer and the locked shot.</summary>
        struct Volley
        {
            public int next;
            public float timer;
            public float launchX01;
            public Vector2 direction;
            public bool powerShot;
            public float power01;
        }

        readonly SessionServices services;
        readonly TurnFeedback feedback;
        readonly HypeController hype;
        readonly Vector2[] aimPoints;
        readonly float[] trackingLostSeconds;
        readonly bool[] warned;
        readonly Volley[] volleys;
        float stragglerSeconds;
        float graceRemaining;
        bool fastForward;
        int flightCombo;

        public PlayerTurnLoop(SessionServices aServices)
        {
            services = aServices;
            feedback = new TurnFeedback(aServices);
            hype = new HypeController(aServices);
            aimPoints = new Vector2[Mathf.Max(2, aServices.Pacing.AimGuideMaxPoints)];
            var players = aServices.Inputs.Length;
            trackingLostSeconds = new float[players];
            warned = new bool[players];
            volleys = new Volley[players];
            StopVolleys();
        }

        public bool IsTurnDone { get; private set; }

        /// <summary>POWER charged by dancing while balls fly, paid with energy (GDD v2 §17).</summary>
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

            StopVolleys();
            stragglerSeconds = 0f;
            graceRemaining = services.Pacing.TurnEndGrace;
            ClearTrackingLost();
            IsTurnDone = false;
            SetFastForward(false);
            hype.ResetTurn();
            flightCombo = 0;
            feedback.BeginTurn();
            services.Hud.RefreshQueue();
        }

        public void EndTurn()
        {
            services.Tracker.Flush();
            StopVolleys();
            for (var p = 0; p < services.Inputs.Length; p++)
            {
                services.Board.SetAim(p, 0.5f, Vector2.up, 0, aimPoints, false);
                ClearTrackingWarning(p);
            }

            SetFastForward(false);
            hype.ResetTurn();
            services.TimeScale.ResetEffects();
            services.Sequencer.EndTurn();
            ClearTrackingLost();
        }

        public void ClearTrackingLost()
        {
            TrackingLostPlayer = -1;
            Array.Clear(trackingLostSeconds, 0, trackingLostSeconds.Length);
        }

        bool Launching(int player) => volleys[player].next >= 0;

        /// <summary>The player still wants a strike: a shot left, no volley of theirs launching, the stage still up.</summary>
        bool WantsStrike(int player) => services.Sequencer.HasShot(player) && !Launching(player) && !feedback.StageCleared;

        bool CanFire(int player) => services.Sequencer.CanFire(player) && !Launching(player) && !feedback.StageCleared;

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
                    Fire(p, input, strike);
                }
            }

            var launching = StreamVolleys(scaledDeltaTime);
            var flying = launching || services.Sim.ActiveBalls > 0;
            if (!flying) flightCombo = 0;
            hype.Tick(unscaledDeltaTime, scaledDeltaTime, flying);
            if (scaledDeltaTime > 0f) services.Sim.Step(scaledDeltaTime);
            Drain();
            services.Board.UpdateBalls(services.Sim.Balls);
            UpdateFastForward(unscaledDeltaTime);
            UpdateTurnEnd(scaledDeltaTime, launching);
        }

        /// <summary>
        /// Shoots a volley at angleDeg (from +x, 90 = up) from the launch position of the first player who can fire
        /// (DebugHooks.Shoot).
        /// </summary>
        public bool ForceShoot(float angleDeg)
        {
            if (IsTurnDone || TrackingLostPlayer >= 0) return false;
            var radians = angleDeg * Mathf.Deg2Rad;
            for (var p = 0; p < services.Inputs.Length; p++)
            {
                if (!CanFire(p)) continue;
                Fire(p, services.Inputs[p], new StrikeInfo { direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), power01 = 0.5f });
                return true;
            }

            return false;
        }

        #endregion

        #region Steps

        /// <summary>Spends the player's shot and locks the volley on the aim at the strike; StreamVolleys launches it.</summary>
        void Fire(int player, IShotInput input, StrikeInfo strike)
        {
            services.Sequencer.Fire(player);
            volleys[player] = new Volley
            {
                next = 0,
                timer = 0f,
                launchX01 = input.LaunchX01,
                direction = ArenaGeometry.ClampAim(services.Rules.arena, strike.direction),
                powerShot = strike.isPowerShot,
                power01 = strike.power01,
            };
            services.Board.PlayStrike(player, strike.isPowerShot);
            services.Hud.RefreshQueue();
            graceRemaining = services.Pacing.TurnEndGrace;
        }

        /// <summary>Launches the volleys' balls that are due; returns whether a volley is still launching.</summary>
        bool StreamVolleys(float scaledDeltaTime)
        {
            var bag = services.Run.bag;
            var arena = services.Rules.arena;
            // With the stage down or the field empty the rest of a volley stays home: there is nothing left to hit.
            var cut = feedback.StageCleared || services.Sim.LivingEnemyCount() == 0;
            var launching = false;
            var progress = 0;
            for (var p = 0; p < volleys.Length; p++)
            {
                ref var volley = ref volleys[p];
                if (volley.next < 0) continue;
                if (cut)
                {
                    volley.next = -1;
                    continue;
                }

                volley.timer -= scaledDeltaTime;
                while (volley.timer <= 0f && volley.next < bag.Count)
                {
                    var ball = bag[volley.next++];
                    var origin = ArenaGeometry.LaunchOrigin(arena, volley.launchX01);
                    services.Sim.Launch(ball, origin, volley.direction, volley.powerShot, p);
                    services.Analytics.ShotFired(ball.type, ball.level, Mathf.Atan2(volley.direction.y, volley.direction.x) * Mathf.Rad2Deg,
                        volley.power01, p);
                    volley.timer += services.Pacing.VolleyInterval;
                }

                if (volley.next >= bag.Count)
                {
                    volley.next = -1;
                    continue;
                }

                launching = true;
                progress = Mathf.Max(progress, volley.next);
            }

            services.Hud.SetVolleyProgress(progress);
            return launching;
        }

        void StopVolleys()
        {
            for (var p = 0; p < volleys.Length; p++)
            {
                volleys[p].next = -1;
            }

            services.Hud.SetVolleyProgress(0);
        }

        void Drain()
        {
            var events = services.Sim.Events;
            if (events.Count == 0) return;
            var hits = 0;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].kind == SimEventKind.EnemyHit) hits++;
            }

            services.Board.Consume(events, services.Run, flightCombo);
            flightCombo += hits;
            feedback.Consume(events);
            services.Tracker.Consume(events);
            events.Clear();
        }

        /// <summary>The live aim, or while the player's volley launches its locked line (the cue thrusts along it).</summary>
        void UpdateAim(IShotInput input, int player)
        {
            var arena = services.Rules.arena;
            var volley = volleys[player];
            var launching = volley.next >= 0;
            var launchX = launching ? volley.launchX01 : input.LaunchX01;
            var direction = launching ? volley.direction : ArenaGeometry.ClampAim(arena, input.AimDirection);
            var visible = launching || WantsStrike(player);
            var count = 0;
            if (visible)
            {
                var pacing = services.Pacing;
                var length = pacing.AimGuideLength(services.AimGuideSetting);
                count = services.Sim.PredictPath(ArenaGeometry.LaunchOrigin(arena, launchX), direction, length, pacing.AimGuideMaxBounces, aimPoints);
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

        void UpdateTurnEnd(float scaledDeltaTime, bool launching)
        {
            // An empty field ends the turn too (GDD v2 §5): the enemy phase then brings the next batch in right away.
            var noMoreShots = !services.Sequencer.HasBallToFire || feedback.StageCleared || services.Sim.LivingEnemyCount() == 0;
            if (!noMoreShots || launching || services.Sim.ActiveBalls > 0)
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
