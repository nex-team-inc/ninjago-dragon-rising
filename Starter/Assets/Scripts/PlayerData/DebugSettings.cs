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
        // Control lab: god mode, so the run never ends; turns still end after BalanceRules.shotsPerTurn balls and the
        // enemies still advance. Off in every build (the control demo too), so the player loses HP as in the game.
        [DebugOrder(13), Description("Cheat: Practice Mode (god mode, the run never ends)")] public bool practiceMode;

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
        [DebugOrder(33), Description("Run: Force Hype while balls fly (-1 = off, 0..1)"), NumericSteps(Steps = 0.05, Min = -1, Max = 1)]
        public float forceHype = -1f;

        [DebugOrder(40), Description("Debug: Show Sim Debug")] public bool showSimDebug;
        // Hidden by default in every build, the control demo included (GDD v2 §6). Renamed from showControlDebug so a
        // value saved while the demo defaulted it on is dropped instead of keeping the readout up.
        [DebugOrder(41), Description("Debug: Show Control Readout")] public bool showControlReadout;

        [DebugOrder(50), Description("Render: Disable Pixelation")] public bool disablePixelation;
        [DebugOrder(51), Description("Render: Disable Tilt-Shift")] public bool disableTiltShift;
        [DebugOrder(52), Description("Render: Disable Bloom")] public bool disableBloom;
        [DebugOrder(53), Description("Render: Quality Tier (0 = auto by GPU, 1 = full, 2 = low)"), NumericSteps(IntSteps = 1, IntMin = 0, IntMax = 2)]
        public int renderTier;
        // [Perf] line every few seconds (FrameTimingLogger): fps, CPU main / render thread and GPU ms, GC, draw counts.
#if BR_CONTROL_DEMO
        [DebugOrder(54), Description("Debug: Log Frame Timing ([Perf] in logcat)")] public bool logFrameTiming = true;
#else
        [DebugOrder(54), Description("Debug: Log Frame Timing ([Perf] in logcat)")] public bool logFrameTiming;
#endif
        // GPU A/B on the device with the [Perf] gpu ms: the world camera's cost is what Freeze World removes (the last
        // world frame stays on screen), the shadow pass what Disable Shadows removes.
        [DebugOrder(55), Description("Render: Disable Shadows")] public bool disableShadows;
        [DebugOrder(56), Description("Render: Freeze World (skip the world camera, GPU A/B)")] public bool freezeWorld;

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
