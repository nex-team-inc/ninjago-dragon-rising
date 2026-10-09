#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Gameplay glue for GDD v2 §5 batch arrivals. At a stage start the board is rebuilt with the first batch hidden
    /// (BeginStage's spawn events are kept) and the batch pops in after the stage intro and the boss intro; a continued
    /// run shows its board as saved. Every batch pop-in, at a stage start or at the end of an enemy phase, shows the HUD's
    /// "Enemies incoming!" ribbon (BoardDriver.BatchIncoming).
    /// </summary>
    public sealed class BatchArrivals
    {
        const int EventCapacity = 64;

        readonly SessionServices services;
        readonly List<SimEvent> openingSpawn = new(EventCapacity);

        public BatchArrivals(SessionServices aServices)
        {
            services = aServices;
            services.Board.BatchIncoming = services.Hud.ShowIncomingBanner;
        }

        /// <summary>Right after RunSimulation.BeginStage, with its events: rebuilds the board without the first batch.</summary>
        public void RebuildForStageStart(List<SimEvent> beginStageEvents, RunState run)
        {
            openingSpawn.Clear();
            openingSpawn.AddRange(beginStageEvents);
            services.Board.RebuildForPopIn(run, openingSpawn);
        }

        /// <summary>Stage intro band, the boss intro on a boss stage, then the pending first batch pops in.</summary>
        public async UniTask IntroAsync(CancellationToken ct)
        {
            var run = services.Run;
            await services.FlowHost.ShowStageIntroAsync(run.actIndex, run.stageInAct, run.stage.isBoss, ct);
            if (run.stage.isBoss) await services.Board.PlayBossIntroAsync(services.Sim.Act.bossType, ct);
            if (openingSpawn.Count == 0) return;
            await services.Board.PlayBatchSpawnAsync(openingSpawn, run, ct);
            openingSpawn.Clear();
        }
    }
}
