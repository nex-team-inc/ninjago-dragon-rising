#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The per-frame work of a player turn (TDD §7): tracking check, aim preview (BallSimulator.PredictPath →
    /// BoardPresenter.SetAim), strike → Launch, fixed-step simulation from the gameplay time scale, event drain into
    /// BoardPresenter / TurnFeedback / ShotResultTracker, straggler fast-forward and the turn-end condition
    /// (nothing left to shoot or stage cleared, no balls in flight, grace elapsed). Allocation-free per frame.
    /// </summary>
    public sealed class PlayerTurnLoop
    {
        readonly SessionServices services;
        readonly TurnFeedback feedback;
        readonly HypeController hype;
        readonly Vector2[] aimPoints;
        float stragglerSeconds;
        float graceRemaining;
        float trackingLostSeconds;
        int warnedPlayer = -1;
        bool fastForward;
        bool handOffPending;

        public PlayerTurnLoop(SessionServices aServices)
        {
            services = aServices;
            feedback = new TurnFeedback(aServices);
            hype = new HypeController(aServices);
            aimPoints = new Vector2[Mathf.Max(2, aServices.Pacing.AimGuideMaxPoints)];
        }

        public bool IsTurnDone { get; private set; }

        /// <summary>Hype from body motion while balls fly (GDD v2 §3).</summary>
        public HypeController Hype => hype;

        /// <summary>Player whose paws stayed untracked past ControlConfig.trackingLostSeconds, -1 otherwise.</summary>
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
            trackingLostSeconds = 0f;
            TrackingLostPlayer = -1;
            IsTurnDone = false;
            SetFastForward(false);
            feedback.BeginTurn();
            services.Hud.RefreshQueue();
        }

        public void EndTurn()
        {
            services.Tracker.Flush();
            services.Board.SetAim(services.Run.activePlayerIndex, 0.5f, Vector2.up, 0, aimPoints, false);
            SetFastForward(false);
            hype.Reset();
            services.TimeScale.ResetEffects();
            services.Sequencer.EndTurn();
            handOffPending = false;
            ClearTrackingWarning();
            ClearTrackingLost();
        }

        public void ClearTrackingLost()
        {
            TrackingLostPlayer = -1;
            trackingLostSeconds = 0f;
        }

        /// <summary>A strike is still wanted this turn: a ball is left and the stage did not fall mid-turn.</summary>
        bool WantsStrike => services.Sequencer.HasBallToFire && !feedback.StageCleared;

        bool CanFire => services.Sequencer.CanFire && !feedback.StageCleared;

        #endregion

        #region Frame

        public void Tick(float unscaledDeltaTime)
        {
            var scaledDeltaTime = unscaledDeltaTime * services.TimeScale.GameplayTimeScale;
            var run = services.Run;
            var shooter = run.activePlayerIndex;
            var input = services.Inputs[shooter];
            // Real time: hit-stop and slow-mo must not stretch the cooldown. This loop does not tick under the menu
            // pause or the tracking-lost hold, so both still freeze it.
            services.Sequencer.Tick(unscaledDeltaTime);
            // Once nothing is left to shoot (or the stage fell mid-turn) the balls roll out on their own: the shooter
            // may step away without the game stopping to wait for them.
            if (WantsStrike) UpdateTracking(input, shooter, unscaledDeltaTime);
            else ClearTrackingWarning();
            if (TrackingLostPlayer >= 0) return;

            UpdateAim(input, shooter);
            // Cooldown first: a strike made during it stays pending (until ControlConfig.strikeExpirySeconds) and
            // fires when the cooldown ends instead of being consumed and dropped.
            if (CanFire && input.TryConsumeStrike(out var strike))
            {
                Fire(strike);
            }

            hype.Tick(unscaledDeltaTime, scaledDeltaTime);
            if (scaledDeltaTime > 0f) services.Sim.Step(scaledDeltaTime);
            Drain();
            TryHandOff();
            services.Board.UpdateBalls(services.Sim.Balls);
            UpdateFastForward(unscaledDeltaTime);
            UpdateTurnEnd(scaledDeltaTime);
        }

        /// <summary>Fires the next ball at angleDeg (from +x, 90 = up) from the active shooter's launch position (DebugHooks.Shoot).</summary>
        public bool ForceShoot(float angleDeg)
        {
            if (IsTurnDone || TrackingLostPlayer >= 0 || !CanFire) return false;
            var radians = angleDeg * Mathf.Deg2Rad;
            Fire(new StrikeInfo { direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), power01 = 0.5f });
            return true;
        }

        #endregion

        #region Steps

        void Fire(StrikeInfo strike)
        {
            var run = services.Run;
            var arena = services.Rules.arena;
            var input = services.Inputs[run.activePlayerIndex];
            var ball = services.Sequencer.Fire(out var shooter);
            var origin = ArenaGeometry.LaunchOrigin(arena, input.LaunchX01);
            var direction = ArenaGeometry.ClampAim(arena, strike.direction);
            services.Sim.Launch(ball, origin, direction, strike.isPowerShot, shooter);
            services.Board.PlayStrike(shooter, strike.isPowerShot);
            services.Analytics.ShotFired(ball.type, ball.level, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, strike.power01, shooter);
            handOffPending = run.numPlayers > 1;
            TryHandOff();
            services.Hud.RefreshQueue();
            graceRemaining = services.Pacing.TurnEndGrace;
        }

        // 2P: the cue (cat pose, HUD marker, banner) passes only while a shot is left (GDD §8). After the turn's last
        // ball it waits: a +1 Ball pickup collected by the rolling balls reopens the turn and passes it then.
        void TryHandOff()
        {
            if (!handOffPending || !services.Sequencer.HasBallToFire) return;
            handOffPending = false;
            var run = services.Run;
            services.Inputs[run.activePlayerIndex].ResetStrike();
            services.Board.SetActiveShooter(run.activePlayerIndex, run.numPlayers);
            services.Hud.RefreshActivePlayer();
            services.Hud.ShowShooterBanner(run.activePlayerIndex);
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

        void UpdateAim(IShotInput input, int shooter)
        {
            var arena = services.Rules.arena;
            var launchX = input.LaunchX01;
            var origin = ArenaGeometry.LaunchOrigin(arena, launchX);
            var direction = ArenaGeometry.ClampAim(arena, input.AimDirection);
            var visible = services.Sequencer.HasBallToFire && !feedback.StageCleared;
            var count = 0;
            if (visible)
            {
                var pacing = services.Pacing;
                var length = pacing.AimGuideLength(services.AimGuideSetting);
                count = services.Sim.PredictPath(origin, direction, length, pacing.AimGuideMaxBounces, aimPoints);
            }

            services.Board.SetAim(shooter, launchX, direction, count, aimPoints, visible);
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
            var sequencer = services.Sequencer;
            var noMoreShots = !sequencer.HasBallToFire || feedback.StageCleared || (sequencer.Infinite && services.Sim.LivingEnemyCount() == 0);
            if (!noMoreShots || services.Sim.ActiveBalls > 0)
            {
                graceRemaining = services.Pacing.TurnEndGrace;
                return;
            }

            graceRemaining -= scaledDeltaTime;
            if (graceRemaining <= 0f) IsTurnDone = true;
        }

        void UpdateTracking(IShotInput input, int shooter, float unscaledDeltaTime)
        {
            if (warnedPlayer >= 0 && warnedPlayer != shooter) ClearTrackingWarning();
            if (input.IsTracking)
            {
                ClearTrackingWarning();
                return;
            }

            if (warnedPlayer != shooter)
            {
                warnedPlayer = shooter;
                services.Hud.SetTrackingWarning(shooter, true);
            }

            trackingLostSeconds += unscaledDeltaTime;
            if (trackingLostSeconds >= services.TrackingLostSeconds) TrackingLostPlayer = shooter;
        }

        void ClearTrackingWarning()
        {
            trackingLostSeconds = 0f;
            if (warnedPlayer < 0) return;
            services.Hud.SetTrackingWarning(warnedPlayer, false);
            warnedPlayer = -1;
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
