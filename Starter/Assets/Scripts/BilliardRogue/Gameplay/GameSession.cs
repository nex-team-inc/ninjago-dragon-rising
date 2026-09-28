#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

// Implemented by Gameplay module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// Lives under the GameplayView. Runs the turn state machine (StageIntro → PlayerTurn → EnemyPhase →
    /// StageClear/Reward | Defeat | Victory) over the simulation, drives BoardPresenter/HUD, saves at turn
    /// boundaries and reports analytics.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
#pragma warning disable CS0067
        /// <summary>Raised after every RunPersistence save (turn boundary, reward, quit).</summary>
        public event Action<RunState>? Saved;
#pragma warning restore CS0067

        GameSessionContext context = null!;

        public GameSessionContext Context => context;

        public void Initialize(GameSessionContext ctx)
        {
            context = ctx;
        }

        /// <summary>Runs until the run ends or ct is cancelled; returns the outcome (Abandoned on cancel/quit).</summary>
        public UniTask<RunOutcome> RunAsync(CancellationToken ct)
        {
            throw new NotImplementedException("Gameplay module");
        }

        /// <summary>Pause/resume from the pause view or platform (freezes the sim, TimeScaleController.SetPaused).</summary>
        public void RequestPause(bool paused)
        {
        }
    }
}
