#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Per-player input selector and the IShotInput every consumer holds (root of PlayerShotInput.prefab). Paw
    /// tracking by default; the auto-aim bot while DebugSettings.autoAimBot is on (read live, so the debug panel and
    /// DebugHooks.SetBot switch mid-run); the mouse/keyboard input while ShotInputContext.forceDebugInput says so, and
    /// in the Editor whenever the paws are not tracked (no camera or nobody in front of it). Debug sources are never
    /// selected in release builds. Raises
    /// TrackingLost after ControlConfig.trackingLostSeconds without tracking and TrackingRestored when it is back.
    /// MotionEnergy / PawPointer (GDD v2 §3-§4) are one RoutedBodyInput that follows the active source: the body
    /// meter and pointer, plus the debug input's simulated energy and mouse paws, or the bot's simulated energy.
    /// </summary>
    public sealed class ShotInputRouter : MonoBehaviour, IShotInput
    {
        [Header("Sources (wired by InputPrefabsBuilder)")]
        [Tooltip("Body tracking input on this prefab.")]
        [SerializeField] PawShotInput pawInput = null!;
        [Tooltip("Mouse / keyboard input on this prefab (Editor and debug builds).")]
        [SerializeField] DebugShotInput debugInput = null!;
        [Tooltip("Auto-aim bot on this prefab (Editor and debug builds).")]
        [SerializeField] AutoAimBot botInput = null!;
        [Tooltip("Whole-body motion energy (Hype) on this prefab.")]
        [SerializeField] MotionEnergyMeter motionMeter = null!;
        [Tooltip("Hands as screen pointers (motion reward pick) on this prefab.")]
        [SerializeField] PawPointer pawPointer = null!;

        public event Action<int>? TrackingLost;
        public event Action<int>? TrackingRestored;

        IShotInput paw = null!;
        IShotInput debug = null!;
        IShotInput bot = null!;
        IShotInput active = null!;
        ControlConfig config = null!;
        RoutedBodyInput body = null!;
        IMotionEnergy? debugEnergy;
        IPawPointer? debugPointer;
        IMotionEnergy? botEnergy;
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
        Func<bool>? forceDebugSource;
#endif
        float untrackedSeconds;

        public int PlayerIndex { get; private set; }
        public ShotInputSource ActiveSource { get; private set; }

        /// <summary>Body motion for Hype (GDD v2 §3), following the active source; set by Initialize (cacheable).</summary>
        public IMotionEnergy? MotionEnergy { get; private set; }

        /// <summary>Paws as screen pointers for motion UI (GDD v2 §4), following the active source; set by Initialize (cacheable).</summary>
        public IPawPointer? PawPointer { get; private set; }
        /// <summary>True between TrackingLost and TrackingRestored.</summary>
        public bool IsTrackingLost { get; private set; }
        /// <summary>The body input, whichever source is active (control readout).</summary>
        public PawShotInput Paw => pawInput;
        /// <summary>The body motion meter, whichever source is active (control readout); null if not on the prefab.</summary>
        public MotionEnergyMeter? BodyMotion => motionMeter != null ? motionMeter : null;

        #region Initialization

        /// <summary>Wires this prefab's paw, debug and bot inputs and starts routing.</summary>
        public void Initialize(int playerIndex, OnePlayerDetectionEngine engine, ShotInputContext ctx)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            forceDebugSource = ctx.forceDebugInput;
#endif
            pawInput.Initialize(playerIndex, engine, ctx.control, ctx.rules.arena, ctx.tuning);
            // Guarded: a PlayerShotInput.prefab built before v2 has no meter / pointer (rebuild with InputPrefabsBuilder).
            if (motionMeter != null)
            {
                motionMeter.Initialize(playerIndex, engine, ctx.control);
            }
            if (pawPointer != null)
            {
                pawPointer.Initialize(playerIndex, engine, ctx.control);
            }
            debugInput.Initialize(playerIndex, ctx.control, ctx.rules.arena, ctx.worldCamera, ctx.layout);
            botInput.Initialize(playerIndex, ctx.rules, ctx.run, ctx.control);
            Initialize(playerIndex, pawInput, debugInput, botInput, ctx.control);
        }

        /// <summary>
        /// Routes between already initialized inputs (tests, custom compositions). Debug / bot inputs that also
        /// implement IMotionEnergy / IPawPointer feed MotionEnergy / PawPointer while active; the body meter and
        /// pointer are this prefab's (when present and initialized).
        /// </summary>
        public void Initialize(int playerIndex, IShotInput aPaw, IShotInput aDebug, IShotInput aBot, ControlConfig aConfig)
        {
            PlayerIndex = playerIndex;
            paw = aPaw;
            debug = aDebug;
            bot = aBot;
            config = aConfig;
            debugEnergy = aDebug as IMotionEnergy;
            debugPointer = aDebug as IPawPointer;
            botEnergy = aBot as IMotionEnergy;
            body = new RoutedBodyInput(motionMeter != null ? motionMeter : null, pawPointer != null ? pawPointer : null);
            MotionEnergy = body;
            PawPointer = body;
            Select(SelectSource(), announce: false);
            enabled = true;
        }

        #endregion

        #region IShotInput

        public bool IsTracking => active.IsTracking;
        public float LaunchX01 => active.LaunchX01;
        public float LaunchY01 => active.LaunchY01;
        public Vector2 AimDirection => active.AimDirection;

        public bool TryConsumeStrike(out StrikeInfo strike) => active.TryConsumeStrike(out strike);

        public void ResetStrike()
        {
            paw.ResetStrike();
            debug.ResetStrike();
            bot.ResetStrike();
        }

        #endregion

        #region Routing

        void Update()
        {
            var source = SelectSource();
            if (source != ActiveSource)
            {
                Select(source, announce: true);
            }
            UpdateTracking(Time.unscaledDeltaTime);
        }

        ShotInputSource SelectSource()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (PlayerDataManager.Instance.DebugSettings.autoAimBot) return ShotInputSource.Bot;
            if (forceDebugSource != null && forceDebugSource())
            {
                return ShotInputSource.Debug;
            }
#endif
#if UNITY_EDITOR
            if (!paw.IsTracking) return ShotInputSource.Debug;
#endif
            return ShotInputSource.Paw;
        }

        void Select(ShotInputSource source, bool announce)
        {
            ActiveSource = source;
            active = source switch
            {
                ShotInputSource.Bot => bot,
                ShotInputSource.Debug => debug,
                _ => paw,
            };
            SetEnabled(debug, source == ShotInputSource.Debug);
            SetEnabled(bot, source == ShotInputSource.Bot);
            body.Route(
                source switch
                {
                    ShotInputSource.Bot => botEnergy,
                    ShotInputSource.Debug => debugEnergy,
                    _ => null,
                },
                source == ShotInputSource.Debug ? debugPointer : null);
            active.ResetStrike();
            if (announce)
            {
                Debug.Log($"[ShotInputRouter] P{PlayerIndex + 1} shot input: {source}");
            }
        }

        void UpdateTracking(float unscaledDeltaTime)
        {
            if (active.IsTracking)
            {
                untrackedSeconds = 0f;
                if (!IsTrackingLost) return;
                IsTrackingLost = false;
                TrackingRestored?.Invoke(PlayerIndex);
                return;
            }

            untrackedSeconds += unscaledDeltaTime;
            if (IsTrackingLost || untrackedSeconds < config.TrackingLostSeconds)
            {
                return;
            }
            IsTrackingLost = true;
            TrackingLost?.Invoke(PlayerIndex);
        }

        static void SetEnabled(IShotInput input, bool on)
        {
            if (input is Behaviour behaviour)
            {
                behaviour.enabled = on;
            }
        }

        #endregion
    }
}
