#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Timings of the turn loop and its feedback (GDD §2, §7.1). Seconds unless stated.</summary>
    [CreateAssetMenu(fileName = "PacingConfig", menuName = "Nex/Billiard Rogue/Pacing Config", order = 53)]
    public sealed class PacingConfig : ScriptableObject
    {
        const int AimGuideSettingCount = 3;

        [Header("Shots")]
        [SerializeField, Range(0f, 2f)] float shotCooldown = 0.35f;
        [Tooltip("Seconds of straggler flight (no balls left to shoot) before fast-forward kicks in.")]
        [SerializeField, Range(0f, 10f)] float fastForwardDelay = 2.5f;
        [SerializeField, Range(1f, 4f)] float fastForwardScale = 2f;

        [Header("Turn")]
        [Tooltip("Seconds after the last ball exits before the enemy phase starts.")]
        [SerializeField, Range(0f, 2f)] float turnEndGrace = 0.3f;
        [Tooltip("HP fraction at or below which the low-HP warning plays at turn start and when damage crosses it.")]
        [SerializeField, Range(0f, 1f)] float lowHpFraction = 0.25f;

        [Header("Aim guide")]
        [Tooltip("Guide length in sim units for PlayerPreference.aimGuideLength 0 (short), 1 (normal), 2 (long).")]
        [SerializeField] float[] aimGuideLengths = { 5f, 9f, 16f };
        [SerializeField, Range(0, 8)] int aimGuideMaxBounces = 2;
        [SerializeField, Range(4, 64)] int aimGuideMaxPoints = 24;

        [Header("Enemy phase")]
        [SerializeField, Range(0.05f, 2f)] float statusTickDuration = 0.35f;
        [SerializeField, Range(0.05f, 2f)] float abilityDuration = 0.45f;
        [SerializeField, Range(0.05f, 2f)] float enemyHopDuration = 0.4f;
        [SerializeField, Range(0.05f, 2f)] float enemyAttackDuration = 0.35f;
        [SerializeField, Range(0f, 0.5f)] float enemyStagger = 0.04f;
        [SerializeField, Range(0.05f, 2f)] float waveSpawnDuration = 0.4f;
        [Tooltip("Multiplier applied to every enemy-phase duration when the debug fast enemy phase is on.")]
        [SerializeField, Range(0.05f, 1f)] float fastEnemyPhaseScale = 0.25f;

        [Header("Banners")]
        [SerializeField, Range(0.2f, 5f)] float stageIntroDuration = 1.4f;
        [SerializeField, Range(0.2f, 5f)] float shooterBannerDuration = 1f;
        [SerializeField, Range(0.2f, 5f)] float turnBannerDuration = 0.8f;
        [SerializeField, Range(0.2f, 5f)] float stageClearDuration = 1.2f;
        [SerializeField, Range(0.2f, 5f)] float bossIntroDuration = 0.8f;
        [SerializeField, Range(0.2f, 5f)] float defeatDuration = 1.6f;
        [SerializeField, Range(0.2f, 5f)] float victoryDuration = 2.2f;

        [Header("Time effects")]
        [SerializeField, Range(0f, 0.3f)] float hitStopKill = 0.04f;
        [SerializeField, Range(0f, 0.3f)] float hitStopBoss = 0.06f;
        [SerializeField, Range(0f, 2f)] float lastEnemySlowMoDuration = 0.3f;
        [SerializeField, Range(0.05f, 1f)] float lastEnemySlowMoScale = 0.3f;

        [Header("Music")]
        [SerializeField, Range(0.1f, 3f)] float bgmCrossfadeSeconds = 0.8f;

        [Header("Reward")]
        [SerializeField, Range(0f, 1f)] float rewardRevealStagger = 0.25f;
        [SerializeField, Range(0f, 2f)] float rewardPickDuration = 0.6f;

        public float ShotCooldown => shotCooldown;
        public float FastForwardDelay => fastForwardDelay;
        public float FastForwardScale => fastForwardScale;
        public float TurnEndGrace => turnEndGrace;
        public float LowHpFraction => lowHpFraction;
        public int AimGuideMaxBounces => aimGuideMaxBounces;
        public int AimGuideMaxPoints => aimGuideMaxPoints;
        public float StatusTickDuration => statusTickDuration;
        public float AbilityDuration => abilityDuration;
        public float EnemyHopDuration => enemyHopDuration;
        public float EnemyAttackDuration => enemyAttackDuration;
        public float EnemyStagger => enemyStagger;
        public float WaveSpawnDuration => waveSpawnDuration;
        public float FastEnemyPhaseScale => fastEnemyPhaseScale;
        public float StageIntroDuration => stageIntroDuration;
        public float ShooterBannerDuration => shooterBannerDuration;
        public float TurnBannerDuration => turnBannerDuration;
        public float StageClearDuration => stageClearDuration;
        public float BossIntroDuration => bossIntroDuration;
        public float DefeatDuration => defeatDuration;
        public float VictoryDuration => victoryDuration;
        public float HitStopKill => hitStopKill;
        public float HitStopBoss => hitStopBoss;
        public float LastEnemySlowMoDuration => lastEnemySlowMoDuration;
        public float LastEnemySlowMoScale => lastEnemySlowMoScale;
        public float BgmCrossfadeSeconds => bgmCrossfadeSeconds;
        public float RewardRevealStagger => rewardRevealStagger;
        public float RewardPickDuration => rewardPickDuration;

        /// <summary>Aim guide length (sim units) for a PlayerPreference.aimGuideLength setting; out-of-range settings use normal.</summary>
        public float AimGuideLength(int setting)
        {
            if (aimGuideLengths.Length == 0) return 9f;
            var index = setting >= 0 && setting < AimGuideSettingCount && setting < aimGuideLengths.Length ? setting : Mathf.Min(1, aimGuideLengths.Length - 1);
            return aimGuideLengths[index];
        }
    }
}
