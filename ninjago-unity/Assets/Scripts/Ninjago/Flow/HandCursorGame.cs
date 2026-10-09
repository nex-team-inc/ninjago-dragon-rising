#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A mini-game played with hand cursors (Stone Kick, Earth Seal): the setup view also confirms the hands.</summary>
    public abstract class HandCursorGame : NinjagoGame
    {
        [Header("Hand Cursors")]
        [SerializeField] HandCursorTracker cursors = null!;
        [Header("Hand Cursor Config")]
        [SerializeField] HandCursorConfig cursorConfig = null!;

        protected HandCursorTracker Cursors => cursors;
        protected override HandCursorTracker? SetupCursors => cursors;

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            base.Initialize(detectionManager, viewManager);
            cursors.Initialize(detectionManager, cursorConfig, Bodies);
        }

        #endregion
    }
}
