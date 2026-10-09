#nullable enable

namespace Nex.BilliardRogue
{
    // Flow module keys (calibration extras). English copy in the trailing "// en:" comment; the localization pass
    // translates them. Base calibration keys (Header, MoveIn, RaiseHand, PoseTutorial, TestStrike, Ready,
    // PlayerReady, Waiting) live in LocKeys.cs.
    public static partial class LocKeys
    {
        public static partial class Calibration
        {
            public const string Starting = "br.ui.calibration.starting";          // en: Starting the camera…
            public const string TutorialHint = "br.ui.calibration.tutorialHint";  // en: Upper paw = ball · Lower paw = cue
            public const string StrikeSuccess = "br.ui.calibration.strikeSuccess"; // en: Nice strike!
            public const string AllReady = "br.ui.calibration.allReady";          // en: Everyone's ready. Let's go!
            public const string BallPawTag = "br.ui.calibration.ballPawTag";      // en: Ball
            public const string CuePawTag = "br.ui.calibration.cuePawTag";        // en: Cue
            public const string ControlsHeader = "br.ui.calibration.controlsHeader"; // en: How to play
            public const string ControlBall = "br.ui.calibration.controlBall";    // en: Upper paw: move the ball
            public const string ControlCue = "br.ui.calibration.controlCue";      // en: Lower paw: aim the cue
            public const string ControlStrike = "br.ui.calibration.controlStrike"; // en: Snap lower paw into upper paw: shoot!
        }

        public static partial class Hud
        {
            public const string PipTitle = "br.hud.pip.title";                    // en: Camera
        }
    }
}
