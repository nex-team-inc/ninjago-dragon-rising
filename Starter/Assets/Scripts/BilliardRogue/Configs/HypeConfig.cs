#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Hype (GDD v2 §3): body motion while balls fly powers them. Read by HypeController (Gameplay), which turns the
    /// players' IMotionEnergy into Hype 0..1 and pushes speed / damage / hit-stop multipliers, the HUD meter tier and
    /// the MOVE prompt. Presentation juice (glow, shake, VFX scale) lives in JuiceConfig and follows the pushed value.
    /// </summary>
    [CreateAssetMenu(fileName = "HypeConfig", menuName = "Nex/Billiard Rogue/Hype Config", order = 55)]
    public sealed class HypeConfig : ScriptableObject
    {
        public const int TierCount = 3;

        [Header("Charge")]
        [Tooltip("Motion energy 0..1 (max across players) → Hype target 0..1.")]
        [SerializeField] AnimationCurve energyToHype = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Seconds to close ~63% of the gap when the target rises (fast attack).")]
        [SerializeField, Range(0.01f, 2f)] float attackSeconds = 0.1f;
        [Tooltip("Seconds to close ~63% of the gap when the target falls (slower release).")]
        [SerializeField, Range(0.01f, 3f)] float releaseSeconds = 0.5f;

        [Header("Tiers (HUD meter, stingers)")]
        [Tooltip("Hype at which tiers 1, 2 and 3 start (ascending).")]
        [SerializeField] float[] tierThresholds = { 0.25f, 0.55f, 0.85f };
        [Tooltip("A tier drops only once Hype falls this far below its threshold (no stinger flicker).")]
        [SerializeField, Range(0f, 0.2f)] float tierHysteresis = 0.04f;

        [Header("Power")]
        [Tooltip("Ball speed multiplier at Hype 1 (lerp from 1). The simulation clamps it to BalanceRules.maxHypeSpeedMultiplier.")]
        [SerializeField, Range(1f, 3f)] float maxSpeedMultiplier = 1.8f;
        [Tooltip("Ball damage multiplier at Hype 1 (lerp from 1); hits round half up.")]
        [SerializeField, Range(1f, 5f)] float maxDamageMultiplier = 2.5f;
        [Tooltip("From this tier on, every ball hit gains at least minBonusDamage from Hype.")]
        [SerializeField, Range(1, TierCount)] int minBonusTier = 2;
        [SerializeField, Range(0, 5)] int minBonusDamage = 1;

        [Header("Hit-stop")]
        [Tooltip("Hit-stop duration multiplier at Hype 1 (lerp from 1).")]
        [SerializeField, Range(1f, 5f)] float maxHitStopMultiplier = 3f;
        [Tooltip("Most extra hit-stop (seconds) Hype may add per real second, so pacing never stalls.")]
        [SerializeField, Range(0f, 0.5f)] float hitStopExtraCapPerSecond = 0.12f;

        [Header("MOVE prompt")]
        [Tooltip("Hype below this while balls fly counts as standing still.")]
        [SerializeField, Range(0f, 1f)] float movePromptBelow = 0.2f;
        [Tooltip("Seconds of standing still (balls flying) before the MOVE prompt shows.")]
        [SerializeField, Range(0f, 5f)] float movePromptDelay = 1f;

        public float AttackSeconds => attackSeconds;
        public float ReleaseSeconds => releaseSeconds;
        public float TierHysteresis => tierHysteresis;
        public int MinBonusTier => minBonusTier;
        public int MinBonusDamage => minBonusDamage;
        public float HitStopExtraCapPerSecond => hitStopExtraCapPerSecond;
        public float MovePromptBelow => movePromptBelow;
        public float MovePromptDelay => movePromptDelay;

        /// <summary>Hype target for a motion energy 0..1 (clamped 0..1).</summary>
        public float TargetFor(float energy01) => Mathf.Clamp01(energyToHype.Evaluate(Mathf.Clamp01(energy01)));

        /// <summary>Hype at which tier 1..3 starts; tiers without a configured threshold never start.</summary>
        public float TierThreshold(int tier)
        {
            var index = tier - 1;
            return index >= 0 && index < tierThresholds.Length ? tierThresholds[index] : float.PositiveInfinity;
        }

        public float SpeedMultiplier(float hype01) => Mathf.Lerp(1f, maxSpeedMultiplier, hype01);

        public float DamageMultiplier(float hype01) => Mathf.Lerp(1f, maxDamageMultiplier, hype01);

        public float HitStopMultiplier(float hype01) => Mathf.Lerp(1f, maxHitStopMultiplier, hype01);
    }
}
