#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>Which input a ShotInputRouter currently forwards (HUD / debug display).</summary>
    public enum ShotInputSource
    {
        /// <summary>Body tracking (PawShotInput).</summary>
        Paw = 0,
        /// <summary>Mouse / keyboard (DebugShotInput): Editor and debug builds only.</summary>
        Debug = 1,
        /// <summary>Auto-aim bot (DebugSettings.autoAimBot): Editor and debug builds only.</summary>
        Bot = 2,
    }
}
