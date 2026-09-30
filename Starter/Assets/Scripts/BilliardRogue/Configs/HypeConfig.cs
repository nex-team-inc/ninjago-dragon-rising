#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// POWER (GDD v2 §17, playtest 5): while balls fly, the players' body motion charges the POWER bar 0..1 and every bit
    /// of charge spends run energy (RunState.energy; defeating enemies refills it, BalanceRules). With no energy dancing
    /// charges nothing. The bar never drains on its own and resets at every turn. Read by HypeController (Gameplay),
    /// which pushes the speed / damage multipliers and the HUD meter tier. Presentation juice (glow, shake,
    /// VFX scale) lives in JuiceConfig and follows the pushed value.
    /// </summary>
    [CreateAssetMenu(fileName = "HypeConfig", menuName = "Nex/Billiard Rogue/Hype Config", order = 55)]
    public sealed class HypeConfig : ScriptableObject
    {
        public const int TierCount = 3;

        [Header("Charge (dance)")]
        [Tooltip("Body motion energy 0..1 (MotionEnergyMeter) → share of chargePerSecond.")]
        [SerializeField] AnimationCurve motionToCharge = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("POWER gained per second of full-speed dancing (1 = the whole bar in a second).")]
        [SerializeField, Range(0.05f, 5f)] float chargePerSecond = 0.6f;
        [Tooltip("Run energy the bar costs from empty to full (charging a part costs that part).")]
        [SerializeField, Range(0.5f, 50f)] float energyPerFullPower = 6f;

        [Header("Prompts")]
        [Tooltip("MOVE! shows once balls fly, energy is left, the bar is not full and the players stayed below this motion for movePromptDelay.")]
        [SerializeField, Range(0f, 1f)] float movePromptBelowMotion = 0.15f;
        [SerializeField, Range(0f, 5f)] float movePromptDelay = 1f;
        [Tooltip("Dancing at least this hard with no energy left shows \"No energy!\" on the meter.")]
        [SerializeField, Range(0f, 1f)] float noEnergyMotion = 0.3f;

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

        public float EnergyPerFullPower => energyPerFullPower;
        public float MovePromptBelowMotion => movePromptBelowMotion;
        public float MovePromptDelay => movePromptDelay;
        public float NoEnergyMotion => noEnergyMotion;
        public float TierHysteresis => tierHysteresis;
        public int MinBonusTier => minBonusTier;
        public int MinBonusDamage => minBonusDamage;

        /// <summary>
        /// One charge step: motion01 for seconds raises power (never past 1) as far as energy pays for it. Returns the
        /// new power; spent is the energy it cost (≤ energy).
        /// </summary>
        public float Charge(float power01, float energy, float motion01, float seconds, out float spent)
        {
            spent = 0f;
            var room = 1f - Mathf.Clamp01(power01);
            if (room <= 0f || energy <= 0f || seconds <= 0f) return Mathf.Clamp01(power01);
            var rise = Mathf.Clamp01(motionToCharge.Evaluate(Mathf.Clamp01(motion01))) * chargePerSecond * seconds;
            rise = Mathf.Min(rise, room, energy / energyPerFullPower);
            spent = Mathf.Min(energy, rise * energyPerFullPower);
            return Mathf.Clamp01(power01 + rise);
        }

        /// <summary>Power at which tier 1..3 starts; tiers without a configured threshold never start.</summary>
        public float TierThreshold(int tier)
        {
            var index = tier - 1;
            return index >= 0 && index < tierThresholds.Length ? tierThresholds[index] : float.PositiveInfinity;
        }

        public float SpeedMultiplier(float power01) => Mathf.Lerp(1f, maxSpeedMultiplier, power01);

        public float DamageMultiplier(float power01) => Mathf.Lerp(1f, maxDamageMultiplier, power01);
    }
}
