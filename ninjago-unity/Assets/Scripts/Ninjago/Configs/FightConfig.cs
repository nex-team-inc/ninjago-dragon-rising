#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slip and Spin tuning. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "FightConfig", menuName = "Nex/Ninjago/Fight Config")]
    public class FightConfig : ScriptableObject
    {
        [Header("Rounds")]
        [Tooltip("Hearts each player starts with. At 0 the player is out for the rest of the fight.")]
        [SerializeField, Range(1, 9)] int hearts = 3;
        [Tooltip("Staff sweeps each player faces.")]
        [SerializeField, Range(1, 12)] int sweepsPerPlayer = 4;
        [Tooltip("Safe side of player 1's first sweep; player 2 starts mirrored so both first lean outward. Sides then alternate.")]
        [SerializeField] SweepSide firstSafeSide = SweepSide.Left;

        [Header("Pacing")]
        [Tooltip("Seconds of 'Get ready' before the first sweep.")]
        [SerializeField, Range(0f, 6f)] float introSeconds = 3f;
        [Tooltip("Seconds between the end of one exchange and the next telegraph; players catch their breath after a storm.")]
        [SerializeField, Range(0.5f, 10f)] float restBetweenSweepsSeconds = 5.5f;

        [Header("Telegraph")]
        [Tooltip("The brute cocks the staff to the danger side and the arrow shows the safe side.")]
        [SerializeField, Range(0.2f, 2f)] float telegraphSeconds = 0.6f;
        [Tooltip("The first sweep winds up longer so a new player can read the SLIP hint before the staff comes.")]
        [SerializeField, Range(0.2f, 4f)] float firstTelegraphSeconds = 1.6f;

        [Header("Slip")]
        [Tooltip("Real seconds to lean the chest past the threshold toward the safe side.")]
        [SerializeField, Range(0.2f, 2f)] float slipWindowSeconds = 0.7f;
        [Tooltip("Chest lean from the calibrated center, in body inches, that counts as a slip (or a wrong-way lean).")]
        [SerializeField, Range(1f, 12f)] float leanThresholdInches = 4.5f;
        [Tooltip("Seconds the staff takes to finish its sweep once the player has slipped.")]
        [SerializeField, Range(0.05f, 1f)] float slipResolveSeconds = 0.22f;

        [Header("Counter")]
        [Tooltip("Real seconds the swirl can be filled after a successful slip.")]
        [SerializeField, Range(0.3f, 3f)] float counterWindowSeconds = 1f;
        [Tooltip("Time scale of that player's courtyard during the counter window.")]
        [SerializeField, Range(0.1f, 1f)] float slowMotionScale = 0.55f;
        [Tooltip("Real seconds to ease into and out of slow motion.")]
        [SerializeField, Range(0f, 0.5f)] float slowMotionEaseSeconds = 0.12f;
        [Tooltip("Summed travel of both hands (body inches, relative to the chest) that fills the swirl.")]
        [SerializeField, Range(10f, 300f)] float handTravelInches = 60f;
        [Tooltip("Hand speed (body inches per second) that must be reached at least once in the window.")]
        [SerializeField, Range(10f, 300f)] float handSpeedPeakInchesPerSecond = 55f;
        [Tooltip("Hand steps shorter than this are tracking jitter and add no travel.")]
        [SerializeField, Range(0f, 2f)] float handNoiseFloorInches = 0.35f;
        [Tooltip("Two samples of one hand further apart than this are a tracking gap: no travel or speed across it.")]
        [SerializeField, Range(0.05f, 1f)] float handSampleGapSeconds = 0.25f;
        [Tooltip("Use the body hand nodes for a hand the hand detector has not reported recently.")]
        [SerializeField] bool useBodyHandFallback = true;
        [Tooltip("Seconds without a hand-detector sample before the body hand node takes over for that hand.")]
        [SerializeField, Range(0.05f, 1f)] float bodyFallbackAfterSeconds = 0.15f;

        [Header("Outcome")]
        [Tooltip("Seconds of the Spinjitzu spin after the swirl fills.")]
        [SerializeField, Range(0.2f, 2f)] float spinSeconds = 0.9f;
        [Tooltip("Seconds the ninja staggers after the staff hits.")]
        [SerializeField, Range(0.2f, 3f)] float hitRecoverSeconds = 1f;
        [Tooltip("Seconds the brute takes to recover after a whiffed counter.")]
        [SerializeField, Range(0.1f, 2f)] float whiffRecoverSeconds = 0.6f;

        #region Public API

        public int Hearts { get => hearts; set => hearts = Mathf.Max(1, value); }
        public int SweepsPerPlayer { get => sweepsPerPlayer; set => sweepsPerPlayer = Mathf.Max(1, value); }
        public SweepSide FirstSafeSide => firstSafeSide;
        public float IntroSeconds => introSeconds;
        public float RestBetweenSweepsSeconds { get => restBetweenSweepsSeconds; set => restBetweenSweepsSeconds = value; }
        public float TelegraphSeconds { get => telegraphSeconds; set => telegraphSeconds = value; }
        public float FirstTelegraphSeconds { get => firstTelegraphSeconds; set => firstTelegraphSeconds = value; }
        public float SlipWindowSeconds { get => slipWindowSeconds; set => slipWindowSeconds = value; }
        public float LeanThresholdInches { get => leanThresholdInches; set => leanThresholdInches = value; }
        public float SlipResolveSeconds => slipResolveSeconds;
        public float CounterWindowSeconds { get => counterWindowSeconds; set => counterWindowSeconds = value; }
        public float SlowMotionScale { get => slowMotionScale; set => slowMotionScale = Mathf.Clamp(value, 0.05f, 1f); }
        public float SlowMotionEaseSeconds => slowMotionEaseSeconds;
        public float HandTravelInches { get => handTravelInches; set => handTravelInches = value; }
        public float HandSpeedPeakInchesPerSecond { get => handSpeedPeakInchesPerSecond; set => handSpeedPeakInchesPerSecond = value; }
        public float HandNoiseFloorInches { get => handNoiseFloorInches; set => handNoiseFloorInches = value; }
        public float HandSampleGapSeconds => handSampleGapSeconds;
        public bool UseBodyHandFallback { get => useBodyHandFallback; set => useBodyHandFallback = value; }
        public float BodyFallbackAfterSeconds => bodyFallbackAfterSeconds;
        public float SpinSeconds => spinSeconds;
        public float HitRecoverSeconds => hitRecoverSeconds;
        public float WhiffRecoverSeconds => whiffRecoverSeconds;

        #endregion
    }
}
