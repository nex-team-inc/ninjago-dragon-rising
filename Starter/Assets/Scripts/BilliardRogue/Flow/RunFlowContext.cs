#nullable enable

using System;
using System.Threading;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Builds the per-player shot inputs for a run under the given parent (index = player index).</summary>
    public delegate IShotInput[] ShotInputsFactory(int numPlayers, Transform parent);

    /// <summary>Instantiates and fills the summary view for a finished run.</summary>
    public delegate SummaryView SummaryViewFactory(RunState run, MetaProgressData meta, bool newRecord, int unlockTierBefore);

    /// <summary>What RunFlow needs from the coordinator (prefabs, scene objects, services, view factories).</summary>
    public sealed class RunFlowContext
    {
        public ViewManager viewManager = null!;
        public BilliardRogueConfig config = null!;
        public GameRules rules = null!;
        public RunPersistence persistence = null!;
        public CameraSession camera = null!;
        public CalibrationView calibrationViewPrefab = null!;
        public GameplayView gameplayViewPrefab = null!;
        public BoardPresenter board = null!;
        public ArenaLayout layout = null!;
        public PixelWorldDisplay display = null!;
        public CalibrationShotInputFactory calibrationShotInput = null!;
        public ShotInputsFactory shotInputs = null!;
        public SummaryViewFactory summaryView = null!;
        /// <summary>A run just ended (before the Summary / Title transition); the coordinator re-registers its debug hooks.</summary>
        public Action runEnded = null!;
        /// <summary>Title is the top view again (refresh its save info, play its music).</summary>
        public Action returnedToTitle = null!;
        /// <summary>Cancelled when the coordinator dies.</summary>
        public CancellationToken lifetime;
    }
}
