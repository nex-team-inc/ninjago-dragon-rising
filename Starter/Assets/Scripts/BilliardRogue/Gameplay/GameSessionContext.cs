#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>Everything a GameSession needs, assembled by the coordinator / GameplayView and passed to Initialize.</summary>
    public sealed class GameSessionContext
    {
        public BilliardRogueConfig config = null!;
        public GameRules rules = null!;
        public RunState run = null!;
        public bool isContinue;
        public BoardPresenter board = null!;
        public IGameplayHud hud = null!;
        public IGameFlowHost flowHost = null!;
        /// <summary>One input per player, index = player index.</summary>
        public IShotInput[] inputs = Array.Empty<IShotInput>();
        public RunPersistence persistence = null!;
        public RunAnalytics analytics = null!;
        public TimeScaleController timeScale = null!;
        public PixelWorldDisplay display = null!;
    }
}
