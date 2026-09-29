#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>Everything the GameplayView needs to host a run, assembled by BilliardRogueCoordinator.</summary>
    public sealed class GameplayViewContext
    {
        /// <summary>The manager the view is pushed on: overlays and the pause events use it until the view dies.</summary>
        public ViewManager viewManager = null!;
        public BilliardRogueConfig config = null!;
        public GameRules rules = null!;
        public RunState run = null!;
        public bool isContinue;
        public CameraSession camera = null!;
        public BoardPresenter board = null!;
        public ArenaLayout layout = null!;
        public PixelWorldDisplay display = null!;
        /// <summary>Act dioramas, lighting and grading; the view applies the act behind each stage intro.</summary>
        public ActEnvironmentController environment = null!;
        /// <summary>One input per player, index = player index (built by the coordinator from the Input prefab).</summary>
        public IShotInput[] inputs = Array.Empty<IShotInput>();
        public RunPersistence persistence = null!;
        public RunAnalytics analytics = null!;
    }
}
