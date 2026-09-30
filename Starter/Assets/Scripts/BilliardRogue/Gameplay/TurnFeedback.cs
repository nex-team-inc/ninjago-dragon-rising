#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Session-level reactions to player-turn events: HUD refreshes on boss hits, heals and pickups, slow-mo on the last
    /// enemy of a stage, boss analytics and the HUD values those events change. Visual/audio hit feedback belongs to
    /// BoardPresenter.Consume.
    /// </summary>
    public sealed class TurnFeedback
    {
        readonly SessionServices services;

        public TurnFeedback(SessionServices aServices)
        {
            services = aServices;
        }

        /// <summary>True once the stage-clear condition was seen this turn (the last enemy died with no waves left).</summary>
        public bool StageCleared { get; private set; }

        public void BeginTurn()
        {
            StageCleared = false;
        }

        public void Consume(List<SimEvent> events)
        {
            var pacing = services.Pacing;
            var timeScale = services.TimeScale;
            var sim = services.Sim;
            var killed = false;
            var bossDirty = false;
            var hpDirty = false;
            var powerDirty = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                switch (ev.kind)
                {
                    case SimEventKind.EnemyHit:
                        bossDirty |= sim.IsBoss(ev.enemyType);
                        break;
                    case SimEventKind.EnemyKilled:
                        killed = true;
                        if (ev.flag)
                        {
                            services.Analytics.BossDefeated(ev.enemyType, services.Run.turnInStage + 1);
                            bossDirty = true;
                        }

                        break;
                    case SimEventKind.EnemyHealed:
                        bossDirty |= sim.IsBoss(ev.enemyType);
                        break;
                    case SimEventKind.PlayerHealed:
                        hpDirty = true;
                        break;
                    case SimEventKind.PickupCollected:
                    case SimEventKind.PowerShotConsumed:
                        powerDirty = true;
                        break;
                }
            }

            var hud = services.Hud;
            if (bossDirty) hud.RefreshBoss();
            if (hpDirty) hud.RefreshHp();
            if (powerDirty)
            {
                hud.RefreshPower();
                hud.RefreshQueue();
            }

            if (!killed || StageCleared || !sim.IsStageCleared) return;
            StageCleared = true;
            timeScale.SlowMo(pacing.LastEnemySlowMoScale, pacing.LastEnemySlowMoDuration);
        }
    }
}
