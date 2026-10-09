#nullable enable

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global

using Nex.BilliardRogue;
using Nex.Dev.Attributes;
using Nex.Ninjago;

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
        [DebugOrder(33), Description("Run: Force POWER while balls fly (-1 = off, 0..1)"), NumericSteps(Steps = 0.05, Min = -1, Max = 1)]
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

        [DebugOrder(63), Description("Save: Reset Meta Progress"), SaveBeforeInvoking]
        public void ResetMetaProgress() => PlayerDataManager.Instance.ResetMetaProgress();

        [DebugOrder(64), Description("Save: Clear Saved Run"), SaveBeforeInvoking]
        public void ClearSavedRun() => PlayerDataManager.Instance.ClearRun();

        // Ninjago playtest rows. The properties edit the config assets directly (session only, never saved with these
        // settings); keep good values by copying them into the assets under Assets/Configs/Ninjago.
        [DebugOrder(-100), Description("Ninja: Simulated Body (keys A/D W/S Space, J/L I/K RightShift)")]
        public bool ninjaSimulatedBody;

        [DebugOrder(-99), Description("Fight: Slip Window (s)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 2), ES3NonSerializable]
        public float FightSlipWindow { get => Fight.SlipWindowSeconds; set => Fight.SlipWindowSeconds = value; }
        [DebugOrder(-98), Description("Fight: Lean Threshold (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 12), ES3NonSerializable]
        public float FightLeanThreshold { get => Fight.LeanThresholdInches; set => Fight.LeanThresholdInches = value; }
        [DebugOrder(-97), Description("Fight: Telegraph (s)"), NumericSteps(Steps = 0.05, Min = 0.2, Max = 2), ES3NonSerializable]
        public float FightTelegraph { get => Fight.TelegraphSeconds; set => Fight.TelegraphSeconds = value; }
        [DebugOrder(-87), Description("Fight: First Telegraph (s)"), NumericSteps(Steps = 0.1, Min = 0.2, Max = 4), ES3NonSerializable]
        public float FightFirstTelegraph { get => Fight.FirstTelegraphSeconds; set => Fight.FirstTelegraphSeconds = value; }
        [DebugOrder(-96), Description("Fight: Counter Window (s)"), NumericSteps(Steps = 0.05, Min = 0.3, Max = 3), ES3NonSerializable]
        public float FightCounterWindow { get => Fight.CounterWindowSeconds; set => Fight.CounterWindowSeconds = value; }
        [DebugOrder(-95), Description("Fight: Slow-mo Scale"), NumericSteps(Steps = 0.05, Min = 0.1, Max = 1), ES3NonSerializable]
        public float FightSlowMotion { get => Fight.SlowMotionScale; set => Fight.SlowMotionScale = value; }
        [DebugOrder(-94), Description("Fight: Hand Travel To Fill (in)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float FightHandTravel { get => Fight.HandTravelInches; set => Fight.HandTravelInches = value; }
        [DebugOrder(-93), Description("Fight: Hand Speed Peak (in/s)"), NumericSteps(Steps = 5, Min = 10, Max = 300), ES3NonSerializable]
        public float FightSpeedPeak { get => Fight.HandSpeedPeakInchesPerSecond; set => Fight.HandSpeedPeakInchesPerSecond = value; }
        [DebugOrder(-92), Description("Fight: Hand Jitter Floor (in)"), NumericSteps(Steps = 0.05, Min = 0, Max = 2), ES3NonSerializable]
        public float FightHandNoiseFloor { get => Fight.HandNoiseFloorInches; set => Fight.HandNoiseFloorInches = value; }
        [DebugOrder(-91), Description("Fight: Body Hand Fallback"), ES3NonSerializable]
        public bool FightBodyHandFallback { get => Fight.UseBodyHandFallback; set => Fight.UseBodyHandFallback = value; }
        [DebugOrder(-90), Description("Fight: Hearts"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 9), ES3NonSerializable]
        public int FightHearts { get => Fight.Hearts; set => Fight.Hearts = value; }
        [DebugOrder(-89), Description("Fight: Sweeps Per Player"), NumericSteps(IntSteps = 1, IntMin = 1, IntMax = 12), ES3NonSerializable]
        public int FightSweeps { get => Fight.SweepsPerPlayer; set => Fight.SweepsPerPlayer = value; }
        [DebugOrder(-88), Description("Fight: Rest Between Sweeps (s)"), NumericSteps(Steps = 0.25, Min = 0.5, Max = 8), ES3NonSerializable]
        public float FightRest { get => Fight.RestBetweenSweepsSeconds; set => Fight.RestBetweenSweepsSeconds = value; }

        [DebugOrder(-80), Description("Chase: Deadzone (in)"), NumericSteps(Steps = 0.25, Min = 0, Max = 6), ES3NonSerializable]
        public float ChaseDeadzone { get => Chase.DeadzoneInches; set => Chase.DeadzoneInches = value; }
        [DebugOrder(-79), Description("Chase: Full Lean X (in)"), NumericSteps(Steps = 0.5, Min = 2, Max = 20), ES3NonSerializable]
        public float ChaseFullLean { get => Chase.FullLeanInches; set => Chase.FullLeanInches = value; }
        [DebugOrder(-78), Description("Chase: Full Rise Y (in)"), NumericSteps(Steps = 0.5, Min = 1, Max = 20), ES3NonSerializable]
        public float ChaseFullRise { get => Chase.FullRiseInches; set => Chase.FullRiseInches = value; }
        [DebugOrder(-77), Description("Chase: 2P Steer Blend (P1 share)"), NumericSteps(Steps = 0.05, Min = 0, Max = 1), ES3NonSerializable]
        public float ChaseSteerBlend { get => Chase.PlayerOneSteerShare; set => Chase.PlayerOneSteerShare = value; }
        [DebugOrder(-76), Description("Chase: Car Steer Smoothing (s)"), NumericSteps(Steps = 0.05, Min = 0.05, Max = 2), ES3NonSerializable]
        public float ChaseCarSmoothing { get => Car.steerSmoothingSeconds; set => Car.steerSmoothingSeconds = value; }
        [DebugOrder(-75), Description("Chase: Sky Steer Smoothing (s)"), NumericSteps(Steps = 0.05, Min = 0.05, Max = 2), ES3NonSerializable]
        public float ChaseSkySmoothing { get => Sky.steerSmoothingSeconds; set => Sky.steerSmoothingSeconds = value; }
        [DebugOrder(-74), Description("Chase: Car Obstacle Spacing (s)"), NumericSteps(Steps = 0.1, Min = 0.6, Max = 5), ES3NonSerializable]
        public float ChaseCarSpacing { get => Car.obstacleSpacingSeconds; set => Car.obstacleSpacingSeconds = value; }
        [DebugOrder(-73), Description("Chase: Sky Obstacle Spacing (s)"), NumericSteps(Steps = 0.1, Min = 0.6, Max = 5), ES3NonSerializable]
        public float ChaseSkySpacing { get => Sky.obstacleSpacingSeconds; set => Sky.obstacleSpacingSeconds = value; }
        [DebugOrder(-72), Description("Chase: Obstacle Lead (s)"), NumericSteps(Steps = 0.1, Min = 0.5, Max = 4), ES3NonSerializable]
        public float ChaseObstacleLead { get => Car.obstacleLeadSeconds; set => Car.obstacleLeadSeconds = Sky.obstacleLeadSeconds = value; }
        [DebugOrder(-71), Description("Chase: Bump Speed Drop"), NumericSteps(Steps = 0.05, Min = 0, Max = 0.9), ES3NonSerializable]
        public float ChaseBumpDrop { get => Chase.BumpSpeedDrop; set => Chase.BumpSpeedDrop = value; }

        [DebugOrder(-70), Description("Setup: Solo Fallback Wait (s)"), NumericSteps(Steps = 1, Min = 1, Max = 30), ES3NonSerializable]
        public float SetupSoloFallback { get => Players.SoloFallbackSeconds; set => Players.SoloFallbackSeconds = value; }

        [DebugOrder(-69), Description("Save: Reset Ninjago Progress"), SaveBeforeInvoking]
        public void ResetNinjagoProgress() => PlayerDataManager.Instance.ResetNinjagoProgress();

        static FightConfig Fight => GameConfigsManager.Instance.FightConfig;
        static ChaseConfig Chase => GameConfigsManager.Instance.ChaseConfig;
        static ChaseConfig.VehicleSettings Car => Chase.GetVehicle(VehicleType.Car);
        static ChaseConfig.VehicleSettings Sky => Chase.GetVehicle(VehicleType.Skycraft);
        static NinjagoPlayersConfig Players => GameConfigsManager.Instance.PlayersConfig;

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
