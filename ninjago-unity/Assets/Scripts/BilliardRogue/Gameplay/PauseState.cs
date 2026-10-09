#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Composes the pause reasons into TimeScaleController.SetPaused: the menu/platform pause (GameSession.RequestPause)
    /// and the tracking-lost hold. The turn state machine stops entirely under the menu pause and only holds the sim
    /// during a tracking-lost overlay.
    /// </summary>
    public sealed class PauseState
    {
        readonly TimeScaleController timeScale;

        public PauseState(TimeScaleController aTimeScale)
        {
            timeScale = aTimeScale;
        }

        public bool MenuPaused { get; private set; }

        public bool Hold { get; private set; }

        public void SetMenuPaused(bool paused)
        {
            MenuPaused = paused;
            Apply();
        }

        public void SetHold(bool hold)
        {
            Hold = hold;
            Apply();
        }

        void Apply() => timeScale.SetPaused(MenuPaused || Hold);
    }
}
