#nullable enable

using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>The resolved collaborators of one GameSession, shared by its turn controllers.</summary>
    public sealed class SessionServices
    {
        readonly bool headless;

        public SessionServices(GameSessionContext ctx, RunSimulation sim, BoardDriver board, HudBinder hud, SessionAudio audio,
            SessionAnalytics analytics, PauseState pause, DebugSettings debug, ShotSequencer sequencer, ShotResultTracker tracker)
        {
            headless = ctx.headless;
            Config = ctx.config;
            Pacing = ctx.config.Pacing;
            Rules = ctx.rules;
            Run = ctx.run;
            Inputs = ctx.inputs;
            FlowHost = ctx.flowHost;
            Persistence = ctx.persistence;
            TimeScale = ctx.timeScale;
            Sim = sim;
            Board = board;
            Hud = hud;
            Audio = audio;
            Analytics = analytics;
            Pause = pause;
            Debug = debug;
            Sequencer = sequencer;
            Tracker = tracker;
        }

        public BilliardRogueConfig Config { get; }
        public PacingConfig Pacing { get; }
        public GameRules Rules { get; }
        public RunState Run { get; }
        public IShotInput[] Inputs { get; }
        public IGameFlowHost FlowHost { get; }
        public RunPersistence Persistence { get; }
        public TimeScaleController TimeScale { get; }
        public RunSimulation Sim { get; }
        public BoardDriver Board { get; }
        public HudBinder Hud { get; }
        public SessionAudio Audio { get; }
        public SessionAnalytics Analytics { get; }
        public PauseState Pause { get; }
        public DebugSettings Debug { get; }
        public ShotSequencer Sequencer { get; }
        public ShotResultTracker Tracker { get; }

        /// <summary>PlayerPreference.aimGuideLength (0 short, 1 normal, 2 long); normal in headless runs.</summary>
        public int AimGuideSetting => headless ? 1 : PlayerDataManager.Instance.PlayerPreference.aimGuideLength;

        public float TrackingLostSeconds => Config.Control.TrackingLostSeconds;
    }
}
