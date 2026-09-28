#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>Everything the GameplayView needs to host a run, assembled by BilliardRogueCoordinator.</summary>
    public sealed class GameplayViewContext
    {
        public BilliardRogueConfig config = null!;
        public GameRules rules = null!;
        public RunState run = null!;
        public bool isContinue;
        public CameraSession camera = null!;
        public BoardPresenter board = null!;
        public ArenaLayout layout = null!;
        public PixelWorldDisplay display = null!;
        /// <summary>One input per player, index = player index (built by the coordinator from the Input prefab).</summary>
        public IShotInput[] inputs = Array.Empty<IShotInput>();
        public RunPersistence persistence = null!;
        public RunAnalytics analytics = null!;
    }
}
