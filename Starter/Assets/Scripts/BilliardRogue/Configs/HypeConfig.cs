#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// POWER (playtest 4; it replaced the body-motion Hype of GDD v2 §3): every enemy hit while a shot's balls fly adds
    /// to the combo, and the combo charges the power 0..1. Read by HypeController (Gameplay), which pushes the speed /
    /// damage / hit-stop multipliers and the HUD meter tier. Presentation juice (glow, shake, VFX scale) lives in
    /// JuiceConfig and follows the pushed value.
    /// </summary>
    [CreateAssetMenu(fileName = "HypeConfig", menuName = "Nex/Billiard Rogue/Hype Config", order = 55)]
    public sealed class HypeConfig : ScriptableObject
    {
        public const int TierCount = 3;

        [Header("Charge")]
        [Tooltip("Enemy hits in one flight (every ball in the air counts, both players in 2P) that give full power.")]
        [SerializeField, Range(1, 200)] int comboForFullPower = 24;
        [Tooltip("Share of comboForFullPower reached (0..1) → power 0..1.")]
        [SerializeField] AnimationCurve comboToPower = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Seconds to close ~63% of the gap as the power rises (the meter's fill).")]
        [SerializeField, Range(0.01f, 2f)] float attackSeconds = 0.1f;

        [Header("Tiers (HUD meter, stingers)")]
        [Tooltip("Power at which tiers 1, 2 and 3 start (ascending).")]
        [SerializeField] float[] tierThresholds = { 0.25f, 0.55f, 0.85f };
        [Tooltip("A tier drops only once the power falls this far below its threshold (no stinger flicker).")]
        [SerializeField, Range(0f, 0.2f)] float tierHysteresis = 0.04f;

        [Header("Power")]
        [Tooltip("Ball speed multiplier at power 1 (lerp from 1). The simulation clamps it to BalanceRules.maxHypeSpeedMultiplier.")]
        [SerializeField, Range(1f, 3f)] float maxSpeedMultiplier = 1.8f;
        [Tooltip("Ball damage multiplier at power 1 (lerp from 1); hits round half up.")]
        [SerializeField, Range(1f, 5f)] float maxDamageMultiplier = 2.5f;
        [Tooltip("From this tier on, every ball hit gains at least minBonusDamage from the power.")]
        [SerializeField, Range(1, TierCount)] int minBonusTier = 2;
        [SerializeField, Range(0, 5)] int minBonusDamage = 1;

        [Header("Hit-stop")]
        [Tooltip("Hit-stop duration multiplier at power 1 (lerp from 1).")]
        [SerializeField, Range(1f, 5f)] float maxHitStopMultiplier = 3f;
        [Tooltip("Most extra hit-stop (seconds) the power may add per real second, so pacing never stalls.")]
        [SerializeField, Range(0f, 0.5f)] float hitStopExtraCapPerSecond = 0.12f;

        public float AttackSeconds => attackSeconds;
        public float TierHysteresis => tierHysteresis;
        public int MinBonusTier => minBonusTier;
        public int MinBonusDamage => minBonusDamage;
        public float HitStopExtraCapPerSecond => hitStopExtraCapPerSecond;

        /// <summary>Power 0..1 for the enemy hits of the current flight.</summary>
        public float TargetFor(int comboHits)
        {
            var share = Mathf.Clamp01((float)comboHits / Mathf.Max(1, comboForFullPower));
            return Mathf.Clamp01(comboToPower.Evaluate(share));
        }

        /// <summary>Power at which tier 1..3 starts; tiers without a configured threshold never start.</summary>
        public float TierThreshold(int tier)
        {
            var index = tier - 1;
            return index >= 0 && index < tierThresholds.Length ? tierThresholds[index] : float.PositiveInfinity;
        }

        public float SpeedMultiplier(float power01) => Mathf.Lerp(1f, maxSpeedMultiplier, power01);

        public float DamageMultiplier(float power01) => Mathf.Lerp(1f, maxDamageMultiplier, power01);

        public float HitStopMultiplier(float power01) => Mathf.Lerp(1f, maxHitStopMultiplier, power01);
    }
}
