#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>
    /// GameplayView's pause bookkeeping, free of Unity so the platform-pause races stay unit tested (GameplayPauseGateTests).
    /// One pause at a time and none after the run ended; the pause view is queued until the ViewManager is idle, so a
    /// pause can start while an overlay is still closing, and only a pause view that was actually shown and then left
    /// without announcing a resume lets the view resume on its own.
    /// </summary>
    public sealed class GameplayPauseGate
    {
        bool overlayShown;

        public bool IsPaused { get; private set; }
        public bool RunEnded { get; private set; }

        /// <summary>Session overlays (stage intro, reward, tracking lost) wait while this is true: the pause view stays on top.</summary>
        public bool BlocksOverlays => IsPaused;

        /// <summary>Back, Escape or a platform stop. True when a pause starts: freeze the run and queue the pause view.</summary>
        public bool TryBegin()
        {
            if (IsPaused || RunEnded)
            {
                return false;
            }

            IsPaused = true;
            return true;
        }

        /// <summary>The queued pause view reached an idle ViewManager. True when it must be pushed now.</summary>
        public bool TryShowOverlay()
        {
            if (!IsPaused || RunEnded || overlayShown)
            {
                return false;
            }

            overlayShown = true;
            return true;
        }

        /// <summary>The pause view announced a resume. True when the run must be unfrozen.</summary>
        public bool TryResume()
        {
            if (!IsPaused)
            {
                return false;
            }

            IsPaused = false;
            overlayShown = false;
            return true;
        }

        /// <summary>
        /// GameplayView is the top view again. True when a shown pause view left without announcing a resume; an
        /// overlay that closed while the pause view was still queued keeps the pause.
        /// </summary>
        public bool ShouldResumeOnTop()
        {
            return IsPaused && overlayShown;
        }

        public void EndRun()
        {
            RunEnded = true;
        }
    }
}
