#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Stone Kick tuning. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "StoneKickConfig", menuName = "Nex/Ninjago/Stone Kick Config")]
    public class StoneKickConfig : ScriptableObject
    {
        [Header("Rounds")]
        [Tooltip("Hearts each player starts with. A rock that reaches the player whole costs one; at 0 the player is out.")]
        [SerializeField, Range(1, 9)] int hearts = 3;
        [Tooltip("Rocks each player's boss throws.")]
        [SerializeField, Range(1, 12)] int throwsPerPlayer = 6;

        [Header("Pacing")]
        [Tooltip("Seconds of 'Get ready' before the first throw.")]
        [SerializeField, Range(0f, 6f)] float introSeconds = 3f;
        [Tooltip("Seconds between the end of one throw and the boss winding up the next.")]
        [SerializeField, Range(0.3f, 8f)] float restBetweenThrowsSeconds = 2.2f;

        [Header("Throw")]
        [Tooltip("Seconds the rock takes from the boss's hand to the hang point, slowing down at the end.")]
        [SerializeField, Range(0.3f, 4f)] float throwSeconds = 1.4f;
        [Tooltip("Seconds the rock hangs mid-screen on the first throws, waiting for a slash.")]
        [SerializeField, Range(0.5f, 10f)] float longHangSeconds = 4.5f;
        [Tooltip("How many throws, from the first, hang for the long time.")]
        [SerializeField, Range(0, 12)] int longHangCount = 2;
        [Tooltip("Seconds the rock hangs on the later throws.")]
        [SerializeField, Range(0.3f, 8f)] float hangSeconds = 2.4f;
        [Tooltip("Seconds an unslashed rock takes from the hang point to the player.")]
        [SerializeField, Range(0.1f, 2f)] float missFlySeconds = 0.4f;
        [Tooltip("Seconds after a rock hits the player before the next rest starts.")]
        [SerializeField, Range(0.2f, 3f)] float hitRecoverSeconds = 1f;

        [Header("Slash")]
        [Tooltip("Cursor speed (body inches per second of the hand) a stroke must reach while it crosses the rock.")]
        [SerializeField, Range(10f, 300f)] float slashSpeedInchesPerSecond = 55f;
        [Tooltip("Seconds the pieces take to spread into their row after the slash.")]
        [SerializeField, Range(0.05f, 1.5f)] float splitSeconds = 0.35f;

        [Header("Kick")]
        [Tooltip("Pieces a slash makes; each accepted kick launches one, and all of them in time is a full return.")]
        [SerializeField, Range(1, 6)] int kicksRequired = 3;
        [Tooltip("Seconds the KICK prompt stays open once the pieces hang in their row.")]
        [SerializeField, Range(0.5f, 6f)] float kickWindowSeconds = 2f;
        [Tooltip("Knee lift toward the hip (body inches above its resting drop) that counts as a kick pulse.")]
        [SerializeField, Range(1f, 14f)] float kneeLiftInches = 4.5f;
        [Tooltip("Fraction of the lift the knee must come back below before the next pulse; holding the knee up is one kick.")]
        [SerializeField, Range(0.1f, 0.9f)] float kneeReleaseRatio = 0.5f;
        [Tooltip("Seconds the knee may take from the release height to the full lift; a slow raise is not a kick.")]
        [SerializeField, Range(0.1f, 2f)] float kickRiseMaxSeconds = 0.3f;
        [Tooltip("Seconds after an accepted kick in which another pulse is rejected as a double count.")]
        [SerializeField, Range(0f, 1f)] float kickCooldownSeconds = 0.18f;
        [Tooltip("Seconds the resting knee drop takes to follow a change of stance while the knees are down.")]
        [SerializeField, Range(0.2f, 10f)] float kneeRestAdaptSeconds = 2f;

        [Header("Pieces")]
        [Tooltip("Seconds a kicked piece takes to fly back into the boss.")]
        [SerializeField, Range(0.1f, 2f)] float pieceFlySeconds = 0.45f;
        [Tooltip("Seconds a leftover piece takes to fall after the prompt closes.")]
        [SerializeField, Range(0.1f, 2f)] float pieceDropSeconds = 0.8f;

        #region Public API

        public int Hearts { get => hearts; set => hearts = Mathf.Max(1, value); }
        public int ThrowsPerPlayer { get => throwsPerPlayer; set => throwsPerPlayer = Mathf.Max(1, value); }
        public float IntroSeconds => introSeconds;
        public float RestBetweenThrowsSeconds { get => restBetweenThrowsSeconds; set => restBetweenThrowsSeconds = value; }
        public float ThrowSeconds { get => throwSeconds; set => throwSeconds = Mathf.Max(0.1f, value); }
        public float LongHangSeconds { get => longHangSeconds; set => longHangSeconds = value; }
        public int LongHangCount { get => longHangCount; set => longHangCount = Mathf.Max(0, value); }
        public float HangSeconds { get => hangSeconds; set => hangSeconds = value; }
        public float MissFlySeconds => missFlySeconds;
        public float HitRecoverSeconds => hitRecoverSeconds;
        public float SlashSpeedInchesPerSecond { get => slashSpeedInchesPerSecond; set => slashSpeedInchesPerSecond = value; }
        public float SplitSeconds => splitSeconds;
        public int KicksRequired { get => kicksRequired; set => kicksRequired = Mathf.Clamp(value, 1, 6); }
        public float KickWindowSeconds { get => kickWindowSeconds; set => kickWindowSeconds = value; }
        public float KneeLiftInches { get => kneeLiftInches; set => kneeLiftInches = value; }
        public float KneeReleaseRatio { get => kneeReleaseRatio; set => kneeReleaseRatio = Mathf.Clamp(value, 0.05f, 0.95f); }
        public float KickRiseMaxSeconds { get => kickRiseMaxSeconds; set => kickRiseMaxSeconds = value; }
        public float KickCooldownSeconds { get => kickCooldownSeconds; set => kickCooldownSeconds = Mathf.Max(0f, value); }
        public float KneeRestAdaptSeconds => kneeRestAdaptSeconds;
        public float PieceFlySeconds => pieceFlySeconds;
        public float PieceDropSeconds => pieceDropSeconds;

        public float HangSecondsFor(int throwIndex) => throwIndex < longHangCount ? longHangSeconds : hangSeconds;

        public KickDetector.Settings KickSettings => new(kneeLiftInches, kneeReleaseRatio, kickRiseMaxSeconds, kickCooldownSeconds, kneeRestAdaptSeconds);

        #endregion
    }
}
