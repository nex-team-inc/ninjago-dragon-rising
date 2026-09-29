#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Hit feedback tuning: shake, damage numbers, combo pitch, flashes and view motion (GDD §7).</summary>
    [CreateAssetMenu(fileName = "JuiceConfig", menuName = "Nex/Billiard Rogue/Juice Config", order = 54)]
    public sealed class JuiceConfig : ScriptableObject
    {
        [Serializable]
        public sealed class EnemyMotionSettings
        {
            [Tooltip("Fraction of a cell an enemy model's footprint fills (per footprint cell: a 2x2 boss fills 2 × this). EnemyView scales the authored model to it.")]
            [Range(0.5f, 1.2f)] public float cellFill = 0.92f;
            [Tooltip("Idle bob amplitude in cells.")]
            [Range(0f, 0.2f)] public float idleBobAmplitude = 0.03f;
            [Range(0.2f, 5f)] public float idleBobPeriod = 1.6f;
            [Tooltip("Breathing scale amplitude (x/z widen while y shrinks).")]
            [Range(0f, 0.2f)] public float breathAmplitude = 0.04f;
            [Range(0.5f, 10f)] public float blinkIntervalMin = 2.5f;
            [Range(0.5f, 10f)] public float blinkIntervalMax = 5.5f;
            [Range(0.02f, 0.3f)] public float blinkDuration = 0.1f;
            [Tooltip("Wing flap angle in degrees (bat).")]
            [Range(0f, 60f)] public float flapAngle = 25f;
            [Range(1f, 20f)] public float flapSpeed = 9f;
            [Tooltip("Squash applied on hop take-off and landing (0.25 = 25% flatter).")]
            [Range(0f, 0.6f)] public float hopSquash = 0.25f;
            [Tooltip("Scale punch on a hit.")]
            [Range(0f, 0.6f)] public float hitPunch = 0.22f;
            [Tooltip("Knockback distance in cells along the ball direction.")]
            [Range(0f, 0.5f)] public float knockbackDistance = 0.1f;
            [Range(0.05f, 0.6f)] public float knockbackDuration = 0.18f;
            [Range(0.05f, 0.6f)] public float spawnPopDuration = 0.28f;
            [Range(0.05f, 0.6f)] public float deathDuration = 0.16f;
            [Tooltip("Lunge distance in cells toward the player for a melee attack.")]
            [Range(0f, 0.8f)] public float attackLunge = 0.35f;
            [Range(0f, 0.6f)] public float castRaise = 0.15f;
            [Range(0f, 1f)] public float bossPhaseRoarScale = 0.35f;
        }

        [Serializable]
        public sealed class EmissiveSettings
        {
            [Tooltip("_EmissionStrength on parts tagged emissive by the prefab builder (× the palette's 1.6 HDR emission).")]
            [Range(0f, 8f)] public float strength = 1.2f;
            [Range(0f, 1f)] public float pulseAmount = 0.35f;
            [Range(0.2f, 6f)] public float pulsePeriod = 1.8f;
        }

        [Serializable]
        public sealed class StatusVisualSettings
        {
            public Color burnTint = new(1f, 0.45f, 0.1f);
            public Color poisonTint = new(0.7f, 0.3f, 1f);
            [Tooltip("_StatusTint alpha (blend amount) while a status is active.")]
            [Range(0f, 1f)] public float tintAmount = 0.45f;
            [Range(0f, 1f)] public float freezeTintAmount = 0.7f;
            [Tooltip("Shield marker thickness in cells.")]
            [Range(0.02f, 0.3f)] public float shieldMarkerThickness = 0.08f;
            [Range(0f, 1f)] public float shieldMarkerHeight = 0.35f;
        }

        [Serializable]
        public sealed class PropSettings
        {
            [Range(0f, 0.3f)] public float pickupBobAmplitude = 0.08f;
            [Range(0.2f, 5f)] public float pickupBobPeriod = 1.2f;
            [Range(0f, 360f)] public float pickupSpinSpeed = 90f;
            [Range(0f, 720f)] public float portalSwirlSpeed = 160f;
            [Range(0f, 0.5f)] public float crateWobble = 0.18f;
            [Range(0.05f, 0.6f)] public float crateWobbleDuration = 0.25f;
            [Range(0f, 0.3f)] public float mudPulse = 0.04f;
        }

        [Serializable]
        public sealed class BallSettings
        {
            [Tooltip("Degrees of spin per world unit travelled.")]
            [Range(0f, 720f)] public float spinPerUnit = 240f;
            [Range(0f, 1f)] public float trailTime = 0.26f;
            [Range(0f, 2f)] public float trailWidthScale = 1.15f;
            [Tooltip("Ghost ball alpha while aiming.")]
            [Range(0f, 1f)] public float ghostAlpha = 0.85f;
            [Tooltip("_EmissionStrength of a ball in flight (its BallDefinition glow colour × this): balls must be the brightest thing on screen, so this sits well above the bloom threshold.")]
            [Range(0f, 6f)] public float flightGlow = 1.8f;
            [Tooltip("HDR multiplier of the ghost ball / aim origin so the waiting ball reads at the cue.")]
            [Range(0.5f, 6f)] public float ghostIntensity = 2.2f;
        }

        [Serializable]
        public sealed class CatSettings
        {
            [Tooltip("Walk speed along the launch line in cells per second.")]
            [Range(0.5f, 20f)] public float walkSpeed = 7f;
            [Tooltip("Idle yaw in degrees (180 = facing the camera, 135 = 3/4 view).")]
            [Range(90f, 270f)] public float idleYaw = 140f;
            [Tooltip("Distance behind the ball (toward the camera) where the cat stands, in cells.")]
            [Range(0f, 1.2f)] public float standOffset = 0.5f;
            [Tooltip("Uniform scale of the cat model (Cat_Hero.fbx is authored 1.3 cells tall); the cat must read at the bottom of the screen next to the 0.4-cell ball.")]
            [Range(0.5f, 2.5f)] public float modelScale = 1.35f;
            [Tooltip("Sideways offset from the ball, in cells (P1 stands to the left of its ball, P2 to the right), so the cat never hides the waiting ball from the camera.")]
            [Range(0f, 1.5f)] public float standSideOffset = 0.62f;
            [Tooltip("Launch X (0..1) of the waiting player in 2P.")]
            [Range(0f, 0.3f)] public float waitingX01 = 0.06f;
            [Range(0.05f, 1f)] public float strikeDuration = 0.28f;
            [Range(0f, 1f)] public float cuePullBack = 0.32f;
            [Range(0.05f, 1f)] public float hurtDuration = 0.35f;
            [Range(0f, 1f)] public float hurtRecoil = 0.25f;
            [Range(0f, 2f)] public float victoryJump = 0.6f;
            [Range(0f, 4f)] public float tailWagSpeed = 2.2f;
            [Range(0f, 40f)] public float tailWagAngle = 18f;
            [Range(0f, 2f)] public float earTwitchInterval = 3f;
        }

        [Serializable]
        public sealed class AimGuideSettings
        {
            [Tooltip("Line height above the floor in cells.")]
            [Range(0f, 0.5f)] public float height = 0.12f;
            [Range(0.01f, 0.4f)] public float width = 0.09f;
            [Range(0f, 0.5f)] public float bounceMarkerSize = 0.16f;
            [Tooltip("HDR intensity multiplier of the guide colour (bloom).")]
            [Range(0f, 6f)] public float intensity = 1.8f;
        }

        [Serializable]
        public sealed class LabelSettings
        {
            [Tooltip("Label offset in canvas pixels from the projected anchor.")]
            public Vector2 hpLabelOffset = new(0f, 14f);
            [Tooltip("Pixel font size of the HP number; keep an integer multiple of 16 (the BilliardPixel raster size) so it stays crisp.")]
            [Range(8f, 96f)] public float hpLabelSize = 32f;
            [Tooltip("Padding of the dark pill behind the HP number (x per side, y per side) in canvas pixels.")]
            public Vector2 hpPillPadding = new(12f, 4f);
            public Color hpPillColor = new(0.04f, 0.04f, 0.07f, 0.86f);
            [Tooltip("HP number colour at full, half and low HP (blended by the HP fraction).")]
            public Color hpFullColor = new(0.66f, 1f, 0.6f);
            public Color hpMidColor = new(1f, 0.9f, 0.4f);
            public Color hpLowColor = new(1f, 0.38f, 0.32f);
            [Range(8f, 96f)] public float floatTextSize = 30f;
            [Range(8f, 96f)] public float comboTextSize = 26f;
            [Tooltip("Random horizontal jitter of damage numbers in canvas pixels.")]
            [Range(0f, 80f)] public float numberJitter = 18f;
            [Range(0f, 200f)] public float floatRise = 46f;
            [Range(0.2f, 3f)] public float floatLifetime = 0.9f;
            [Range(16f, 64f)] public float statusIconSize = 28f;
            [Range(16f, 96f)] public float telegraphIconSize = 40f;
        }

        [Serializable]
        public sealed class SequenceSettings
        {
            [Tooltip("Camera push-in distance (world units) for the boss intro.")]
            [Range(0f, 6f)] public float bossIntroPushIn = 2.2f;
            [Range(0f, 30f)] public float bossIntroShake = 5f;
            [Range(0f, 6f)] public float victoryPushIn = 1.4f;
            [Range(0f, 6f)] public float defeatPushIn = 1.8f;
            [Range(0f, 30f)] public float explosionShake = 6f;
            [Range(0f, 30f)] public float quakeShake = 8f;
            [Range(0f, 30f)] public float enemyAttackShake = 3f;
            [Tooltip("Damage at or above which a hit uses the mid / hard SFX.")]
            [Range(1, 50)] public int midDamage = 4;
            [Range(1, 100)] public int hardDamage = 10;
            [Range(0.5f, 3f)] public float critVfxScale = 1.5f;
            [Range(0.2f, 3f)] public float explosionVfxScalePerRadius = 1f;
        }

        /// <summary>Hype (body motion while balls fly, GDD v2 §3) scaling of the flight and hit juice. 0 hype = the v1 look.</summary>
        [Serializable]
        public sealed class HypeSettings
        {
            [Tooltip("Hype at which tiers 1 / 2 / 3 are reached (keep equal to the gameplay/HUD tiers).")]
            [Range(0f, 1f)] public float tier1 = 0.25f;
            [Range(0f, 1f)] public float tier2 = 0.55f;
            [Range(0f, 1f)] public float tier3 = 0.85f;
            [Tooltip("Seconds to follow a rising / falling Hype (exponential smoothing, unscaled time).")]
            [Range(0.01f, 1f)] public float riseTime = 0.08f;
            [Range(0.01f, 2f)] public float fallTime = 0.35f;
            [Tooltip("Tier colours 1..3: ball trails, damage numbers and the aura. Keep them saturated: the additive HDR trail and aura bloom toward white.")]
            public Color tier1Color = new(1f, 0.82f, 0.08f);
            public Color tier2Color = new(1f, 0.28f, 0.02f);
            public Color tier3Color = new(1f, 0.08f, 0.55f);

            [Header("Balls (× at full Hype)")]
            [Range(1f, 4f)] public float glowMax = 2.2f;
            [Range(1f, 1.5f)] public float sizeMax = 1.15f;
            [Range(1f, 4f)] public float trailTimeMax = 2f;
            [Range(1f, 3f)] public float trailWidthMax = 1.35f;
            [Tooltip("How far the trail colour moves from the ball's glow colour to the tier colour.")]
            [Range(0f, 1f)] public float trailTint = 0.85f;

            [Header("Hits (× at full Hype)")]
            [Range(1f, 3f)] public float hitVfxScaleMax = 2f;
            [Range(1f, 4f)] public float shakeMax = 2.5f;
            [Tooltip("Shake amplitude cap (world display pixels) for Hype-boosted shakes; stronger unboosted shakes pass unchanged.")]
            [Range(1f, 40f)] public float shakeCapPixels = 16f;
            [Tooltip("Tier from which every enemy hit adds an extra spark burst (tier 3 adds a crit spark as well).")]
            [Range(1, 4)] public int extraSparksTier = 2;
            [Range(0f, 1f)] public float extraSparkScale = 0.8f;

            [Header("Damage numbers (per tier)")]
            [Tooltip("Font size added per tier; keep a multiple of 16 so the pixel font stays crisp.")]
            [Range(0f, 32f)] public float numberSizePerTier = 16f;
            [Tooltip("Extra pop overshoot per tier (0.25 = the number peaks 25% larger).")]
            [Range(0f, 1f)] public float numberPopPerTier = 0.22f;
            [Tooltip("Normal damage numbers blend toward the tier colour by this much.")]
            [Range(0f, 1f)] public float numberTint = 0.8f;

            [Header("Tier-3 aura (arena rim)")]
            [Tooltip("Line width in cells.")]
            [Range(0.02f, 1f)] public float auraWidth = 0.26f;
            [Tooltip("Peak alpha of the additive aura (lower keeps the tier colour from blooming to white).")]
            [Range(0f, 1f)] public float auraAlpha = 0.7f;
            [Tooltip("Height above the floor in cells.")]
            [Range(0f, 1f)] public float auraHeight = 0.08f;
            [Range(0.1f, 2f)] public float auraPulsePeriod = 0.42f;
            [Range(0f, 1f)] public float auraPulseAmount = 0.45f;
            [Tooltip("Seconds to fade the aura in / out.")]
            [Range(0.02f, 1f)] public float auraFade = 0.2f;

            [Header("Cat dance")]
            [Tooltip("Hype below which the cat does not dance.")]
            [Range(0f, 0.5f)] public float danceMin = 0.05f;
            [Tooltip("Dance beats per second at low / full Hype.")]
            [Range(0.5f, 6f)] public float danceBeatsMin = 1.8f;
            [Range(0.5f, 8f)] public float danceBeatsMax = 3.6f;
            [Tooltip("Hop height in cells at full Hype.")]
            [Range(0f, 1f)] public float danceBounce = 0.22f;
            [Range(0f, 60f)] public float danceYaw = 28f;
            [Range(0f, 30f)] public float danceRoll = 10f;
            [Range(0f, 0.4f)] public float danceSquash = 0.12f;
            [Tooltip("Tail wag speed multiplier at full Hype.")]
            [Range(1f, 5f)] public float danceTailSpeed = 2.5f;
            [Tooltip("Big pose when a tier is reached: duration, jump (cells, tier 3 value; lower tiers scale down), scale punch.")]
            [Range(0.2f, 2f)] public float tierPoseDuration = 0.7f;
            [Range(0f, 2f)] public float tierPoseJump = 0.75f;
            [Range(0f, 1f)] public float tierPoseScale = 0.28f;
            [Range(0f, 3f)] public float tierPoseVfxScale = 1.2f;
        }

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

        [Header("Players")]
        [SerializeField] Color player1Color = new(1f, 0.85f, 0.3f);
        [SerializeField] Color player2Color = new(0.4f, 0.8f, 1f);

        [Header("View motion")]
        [SerializeField] EnemyMotionSettings enemyMotion = new();
        [SerializeField] EmissiveSettings emissive = new();
        [SerializeField] StatusVisualSettings statusVisuals = new();
        [SerializeField] PropSettings props = new();
        [SerializeField] BallSettings balls = new();
        [SerializeField] CatSettings cat = new();
        [SerializeField] AimGuideSettings aimGuide = new();
        [SerializeField] LabelSettings labels = new();
        [SerializeField] SequenceSettings sequences = new();

        [Header("Hype (GDD v2 §3)")]
        [SerializeField] HypeSettings hype = new();

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
        public Color Player1Color => player1Color;
        public Color Player2Color => player2Color;
        public EnemyMotionSettings EnemyMotion => enemyMotion;
        public EmissiveSettings Emissive => emissive;
        public StatusVisualSettings StatusVisuals => statusVisuals;
        public PropSettings Props => props;
        public BallSettings Balls => balls;
        public CatSettings Cat => cat;
        public AimGuideSettings AimGuide => aimGuide;
        public LabelSettings Labels => labels;
        public SequenceSettings Sequences => sequences;
        public HypeSettings Hype => hype;

        public Color PlayerColor(int playerIndex) => playerIndex == 0 ? player1Color : player2Color;
    }
}
