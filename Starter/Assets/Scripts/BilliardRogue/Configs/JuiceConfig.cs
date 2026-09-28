#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Hit feedback tuning: shake, damage numbers, combo pitch and flashes (GDD §7).</summary>
    [CreateAssetMenu(fileName = "JuiceConfig", menuName = "Nex/Billiard Rogue/Juice Config", order = 54)]
    public sealed class JuiceConfig : ScriptableObject
    {
        [Header("Camera shake")]
        [Tooltip("Shake amplitude (world display pixels) by damage dealt.")]
        [SerializeField] AnimationCurve shakeAmplitudeByDamage = AnimationCurve.Linear(0f, 0f, 20f, 6f);
        [SerializeField, Range(0f, 1f)] float shakeDuration = 0.18f;
        [SerializeField, Range(0f, 30f)] float playerHurtShake = 9f;
        [SerializeField, Range(0f, 30f)] float bossDeathShake = 14f;

        [Header("Damage numbers")]
        [SerializeField] Color normalDamageColor = Color.white;
        [SerializeField] Color critDamageColor = new(1f, 0.9f, 0.2f);
        [SerializeField] Color blockColor = new(0.45f, 0.7f, 1f);
        [SerializeField] Color healColor = new(0.45f, 1f, 0.5f);
        [SerializeField] Color burnColor = new(1f, 0.55f, 0.15f);
        [SerializeField] Color poisonColor = new(0.75f, 0.4f, 1f);
        [SerializeField, Range(8f, 96f)] float damageNumberSize = 32f;
        [SerializeField, Range(8f, 128f)] float critNumberSize = 48f;
        [SerializeField, Range(0.2f, 2f)] float damageNumberLifetime = 0.7f;
        [SerializeField, Range(0f, 200f)] float damageNumberRise = 60f;

        [Header("Combo")]
        [Tooltip("Pitch added per successive hit of one ball.")]
        [SerializeField, Range(0f, 0.25f)] float comboPitchStep = 0.06f;
        [SerializeField, Range(1f, 3f)] float comboPitchMax = 2f;
        [SerializeField, Range(1, 20)] int comboShowThreshold = 3;

        [Header("Flashes")]
        [SerializeField] Color enemyHitFlash = Color.white;
        [SerializeField] Color enemyFreezeTint = new(0.6f, 0.85f, 1f);
        [SerializeField] Color playerHurtFlash = new(1f, 0.2f, 0.2f, 0.45f);
        [SerializeField, Range(0.02f, 0.5f)] float flashDuration = 0.08f;

        public AnimationCurve ShakeAmplitudeByDamage => shakeAmplitudeByDamage;
        public float ShakeDuration => shakeDuration;
        public float PlayerHurtShake => playerHurtShake;
        public float BossDeathShake => bossDeathShake;
        public Color NormalDamageColor => normalDamageColor;
        public Color CritDamageColor => critDamageColor;
        public Color BlockColor => blockColor;
        public Color HealColor => healColor;
        public Color BurnColor => burnColor;
        public Color PoisonColor => poisonColor;
        public float DamageNumberSize => damageNumberSize;
        public float CritNumberSize => critNumberSize;
        public float DamageNumberLifetime => damageNumberLifetime;
        public float DamageNumberRise => damageNumberRise;
        public float ComboPitchStep => comboPitchStep;
        public float ComboPitchMax => comboPitchMax;
        public int ComboShowThreshold => comboShowThreshold;
        public Color EnemyHitFlash => enemyHitFlash;
        public Color EnemyFreezeTint => enemyFreezeTint;
        public Color PlayerHurtFlash => playerHurtFlash;
        public float FlashDuration => flashDuration;
    }
}
