#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One enemy phase: resolves it through the simulation (god mode gives the player an unreachable HP for the
    /// resolve and strips the hurt events afterwards, HANDOFF §5.6; practice mode also holds the enemies one row
    /// short of the danger row), hands the events to BoardPresenter for staged
    /// playback (which plays the step / hurt SFX in sync) and, once played, pushes HP/boss HUD values and the
    /// turn_end analytics.
    /// </summary>
    public sealed class EnemyPhaseRunner
    {
        const int GodModeHp = int.MaxValue / 4;

        readonly SessionServices services;
        int enemiesAdvanced;
        int damageTaken;

        public EnemyPhaseRunner(SessionServices aServices)
        {
            services = aServices;
        }

        #region Public Methods

        /// <summary>Resolves the phase and returns the presenter's playback task (completed in headless runs).</summary>
        public UniTask Begin(CancellationToken ct)
        {
            var run = services.Run;
            var hpBefore = run.playerHp;
            var takenBefore = run.stats.damageTaken;
            var godMode = services.GodMode;
            if (godMode) run.playerHp = GodModeHp;
            services.Sim.ResolveEnemyPhase(services.HoldEnemiesBeforeDangerRow);
            var events = services.Sim.PhaseEvents;
            if (godMode)
            {
                run.playerHp = hpBefore;
                run.stats.damageTaken = takenBefore;
                run.outcome = RunOutcome.None;
                RemovePlayerDamage(events);
            }

            Summarize(events, hpBefore);
            services.TimeScale.SetPhaseScale(services.Debug.fastEnemyPhase ? 1f / services.Pacing.FastEnemyPhaseScale : 1f);
            return services.Board.PlayEnemyPhaseAsync(events, run, ct);
        }

        /// <summary>After playback: HUD, analytics. Returns true when the player was defeated during the phase.</summary>
        public bool End()
        {
            services.TimeScale.SetPhaseScale(1f);
            var run = services.Run;
            var defeated = run.outcome == RunOutcome.Defeat;
            services.Hud.RefreshHp();
            services.Hud.RefreshBoss();
            // The resolver only counts the turn when the player survives it.
            var turn = defeated ? run.turnInStage + 1 : run.turnInStage;
            services.Analytics.TurnEnd(run.stageNumber, turn, enemiesAdvanced, damageTaken, run.playerHp);
            return defeated;
        }

        /// <summary>
        /// Closes the turn that cleared the stage: the resolver never runs for it (so turnInStage was not advanced),
        /// yet GDD §11 wants a turn_end for every turn_start.
        /// </summary>
        public void EndWithoutPhase()
        {
            var run = services.Run;
            services.Analytics.TurnEnd(run.stageNumber, run.turnInStage + 1, 0, 0, run.playerHp);
        }

        #endregion

        #region Helpers

        void Summarize(List<SimEvent> events, int hpBefore)
        {
            enemiesAdvanced = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.kind == SimEventKind.EnemyMoved && !ev.flag) enemiesAdvanced++;
            }

            damageTaken = hpBefore - services.Run.playerHp;
        }

        // The sim already ran with an unreachable HP; drop the hurt feedback it still emitted.
        static void RemovePlayerDamage(List<SimEvent> events)
        {
            var write = 0;
            for (var read = 0; read < events.Count; read++)
            {
                var kind = events[read].kind;
                if (kind == SimEventKind.PlayerDamaged || kind == SimEventKind.PlayerDied) continue;
                events[write++] = events[read];
            }

            events.RemoveRange(write, events.Count - write);
        }

        #endregion
    }
}
