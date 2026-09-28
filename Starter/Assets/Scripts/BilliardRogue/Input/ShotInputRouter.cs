#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Per-player input selector and the IShotInput every consumer holds (root of PlayerShotInput.prefab). Paw
    /// tracking by default; the auto-aim bot while DebugSettings.autoAimBot is on (read live, so the debug panel and
    /// DebugHooks.SetBot switch mid-run); in the Editor the mouse/keyboard input whenever the paws are not tracked
    /// (no camera or nobody in front of it). Debug sources are never selected in release builds. Raises
    /// TrackingLost after ControlConfig.trackingLostSeconds without tracking and TrackingRestored when it is back.
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

        public event Action<int>? TrackingLost;
        public event Action<int>? TrackingRestored;

        IShotInput paw = null!;
        IShotInput debug = null!;
        IShotInput bot = null!;
        IShotInput active = null!;
        ControlConfig config = null!;
        float untrackedSeconds;

        public int PlayerIndex { get; private set; }
        public ShotInputSource ActiveSource { get; private set; }
        /// <summary>True between TrackingLost and TrackingRestored.</summary>
        public bool IsTrackingLost { get; private set; }

        #region Initialization

        /// <summary>Wires this prefab's paw, debug and bot inputs and starts routing.</summary>
        public void Initialize(int playerIndex, OnePlayerDetectionEngine engine, ShotInputContext ctx)
        {
            pawInput.Initialize(playerIndex, engine, ctx.control, ctx.rules.arena, ctx.leftHanded);
            debugInput.Initialize(playerIndex, ctx.control, ctx.rules.arena, ctx.worldCamera, ctx.layout);
            botInput.Initialize(playerIndex, ctx.rules, ctx.run, ctx.control);
            Initialize(playerIndex, pawInput, debugInput, botInput, ctx.control);
        }

        /// <summary>Routes between already initialized inputs (tests, custom compositions).</summary>
        public void Initialize(int playerIndex, IShotInput aPaw, IShotInput aDebug, IShotInput aBot, ControlConfig aConfig)
        {
            PlayerIndex = playerIndex;
            paw = aPaw;
            debug = aDebug;
            bot = aBot;
            config = aConfig;
            Select(SelectSource(), announce: false);
            enabled = true;
        }

        #endregion

        #region IShotInput

        public bool IsTracking => active.IsTracking;
        public float LaunchX01 => active.LaunchX01;
        public Vector2 AimDirection => active.AimDirection;

        public bool TryConsumeStrike(out StrikeInfo strike) => active.TryConsumeStrike(out strike);

        public void ResetStrike()
        {
            paw.ResetStrike();
            debug.ResetStrike();
            bot.ResetStrike();
        }

        #endregion

        #region Public Methods

        /// <summary>Applies a changed PlayerPreference.leftHandedCue to this player's paw input.</summary>
        public void SetLeftHanded(bool leftHanded) => pawInput.SetLeftHanded(leftHanded);

        #endregion

        #region Routing

        void Update()
        {
            var source = SelectSource();
            if (source != ActiveSource) Select(source, announce: true);
            UpdateTracking(Time.unscaledDeltaTime);
        }

        ShotInputSource SelectSource()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (PlayerDataManager.Instance.DebugSettings.autoAimBot) return ShotInputSource.Bot;
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
            active.ResetStrike();
            if (announce) Debug.Log($"[ShotInputRouter] P{PlayerIndex + 1} shot input: {source}");
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
            if (IsTrackingLost || untrackedSeconds < config.TrackingLostSeconds) return;
            IsTrackingLost = true;
            TrackingLost?.Invoke(PlayerIndex);
        }

        static void SetEnabled(IShotInput input, bool on)
        {
            if (input is Behaviour behaviour) behaviour.enabled = on;
        }

        #endregion
    }
}
