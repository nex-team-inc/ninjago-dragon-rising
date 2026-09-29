#nullable enable

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

using Nex.BilliardRogue;
using Nex.Dev.Attributes;

namespace Nex
{
    // Rows of the DebugSettingsPanel (public fields, properties and void methods with primitive parameters).
    // Keep it a flat class: no const/static fields, no nested collections. Release builds never load it.
    public class DebugSettings
    {
        [DebugOrder(0)] public bool enableDebugPrinter = false;

        // Billiard Rogue cheats, flow and render toggles.
        [DebugOrder(10), Description("Cheat: God Mode")] public bool godMode;
        [DebugOrder(11), Description("Cheat: Infinite Balls")] public bool infiniteBalls;
        [DebugOrder(12), Description("Cheat: Unlock All Balls")] public bool unlockAllBalls;
        // Control lab: god mode + infinite balls, and enemies stop one row short of the danger row. The control demo
        // APK (BR_CONTROL_DEMO) starts with it and the control readout on.
#if BR_CONTROL_DEMO
        [DebugOrder(13), Description("Cheat: Practice Mode (god, infinite balls, no danger row)")] public bool practiceMode = true;
#else
        [DebugOrder(13), Description("Cheat: Practice Mode (god, infinite balls, no danger row)")] public bool practiceMode;
#endif

        [DebugOrder(20), Description("Flow: Auto Aim Bot")] public bool autoAimBot;
        [DebugOrder(21), Description("Flow: Skip Calibration (Editor only)")] public bool skipCalibration;
        [DebugOrder(22), Description("Flow: Fast Enemy Phase")] public bool fastEnemyPhase;
        [DebugOrder(23), Description("Input: Force Mouse/Keyboard")] public bool forceDebugInput;

        // Control lab live tuning: ControlConfig value x scale (the asset stays untouched), read every frame.
        [DebugOrder(24), Description("Input: Strike Speed x (higher = harder thrust)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float strikeSpeedScale = 1f;
        [DebugOrder(25), Description("Input: Contact Distance x (higher = paws may stay apart)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float contactDistanceScale = 1f;
        [DebugOrder(26), Description("Input: Aim Smoothing x (higher = steadier, more lag)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float aimSmoothingScale = 1f;
        [DebugOrder(27), Description("Input: Launch Range x (lower = less left-paw travel)"), NumericSteps(Steps = 0.05, Min = 0.25, Max = 3)]
        public float launchRangeScale = 1f;

        [DebugOrder(30), Description("Run: Force Start Stage (-1 = off, 0..11)"), NumericSteps(IntSteps = 1, IntMin = -1, IntMax = 11)]
        public int forceStartStage = -1;
        [DebugOrder(31), Description("Run: Force Reward Ball (-1 = off, 0..11 = BallType)"), NumericSteps(IntSteps = 1, IntMin = -1, IntMax = 11)]
        public int forceRewardBall = -1;
        [DebugOrder(32), Description("Run: Fixed Seed (0 = random)")] public int fixedSeed;

        [DebugOrder(40), Description("Debug: Show Sim Debug")] public bool showSimDebug;
#if BR_CONTROL_DEMO
        [DebugOrder(41), Description("Debug: Show Control Readout")] public bool showControlDebug = true;
#else
        [DebugOrder(41), Description("Debug: Show Control Readout")] public bool showControlDebug;
#endif

        [DebugOrder(50), Description("Render: Disable Pixelation")] public bool disablePixelation;
        [DebugOrder(51), Description("Render: Disable Tilt-Shift")] public bool disableTiltShift;
        [DebugOrder(52), Description("Render: Disable Bloom")] public bool disableBloom;

        // Method rows close the panel first, then run; gameplay registers the handlers in DebugHooks.
        [DebugOrder(60), Description("Run: Kill All Enemies")]
        public void KillAllEnemies()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.KillAll()}");
#endif
        }

        [DebugOrder(61), Description("Run: Clear Stage")]
        public void ClearStage()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.ClearStage()}");
#endif
        }

        [DebugOrder(62), Description("Run: Add Every Ball")]
        public void AddEveryBall()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            UnityEngine.Debug.Log($"[DebugSettings] {DebugHooks.AddEveryBall()}");
#endif
        }

        // Same preference as Settings > Left-Handed Cue; the shot inputs poll it, so it applies mid-run. Saving first
        // keeps the tuning edits made in the same panel visit.
        [DebugOrder(28), Description("Input: Toggle Left-Handed Cue"), SaveBeforeInvoking]
        public void ToggleLeftHandedCue()
        {
            PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(preference => preference.leftHandedCue = !preference.leftHandedCue);
        }

        [DebugOrder(63), Description("Save: Reset Meta Progress"), SaveBeforeInvoking]
        public void ResetMetaProgress() => PlayerDataManager.Instance.ResetMetaProgress();

        [DebugOrder(64), Description("Save: Clear Saved Run"), SaveBeforeInvoking]
        public void ClearSavedRun() => PlayerDataManager.Instance.ClearRun();

        // Volumes go through the PlayerDataManager properties so VolumeManager applies and persists them.
        public void MuteMusic() => SetMusicVolume(0);

        public void MuteSfx() => SetSfxVolume(0);

        public void SetMusicVolume(float volume) => PlayerDataManager.Instance.BgmVolumeProperty.Value = volume;
        public void SetSfxVolume(float volume) => PlayerDataManager.Instance.SfxVolumeProperty.Value = volume;

        public void ClearAllDataCache()
        {
            var playerDataManager = PlayerDataManager.Instance;
            playerDataManager.ResetPlayerPreference();
            playerDataManager.ResetMetaProgress();
            playerDataManager.ClearRun();
        }
    }
}
