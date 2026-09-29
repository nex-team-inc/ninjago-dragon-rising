#nullable enable

using Nex.BilliardRogue.Simulation;
using TMPro;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Look and motion of every Billiard Rogue view and the HUD: colours, pixel-kit sprites, fonts, unscaled animation
    /// timings and UI sounds. UiViewsBuilder authors the prefabs from it; widgets read colours and timings at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "UiTheme", menuName = "Nex/Billiard Rogue/UI Theme", order = 70)]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Fonts")]
        [Tooltip("Pixel TMP font (BilliardPixel_TMP). Sizes are multiples of 16; 48 = one font pixel per 3x UI art pixel.")]
        [SerializeField] TMP_FontAsset? regularFont;
        [SerializeField] TMP_FontAsset? boldFont;

        [Header("Text colours")]
        [SerializeField] Color textPrimary = new(1f, 0.957f, 0.863f, 1f);
        [SerializeField] Color textMuted = new(0.72f, 0.77f, 0.9f, 1f);
        [Tooltip("Text on parchment (reward cards).")]
        [SerializeField] Color textDark = new(0.227f, 0.157f, 0.098f, 1f);
        [Tooltip("Drop-shadow copy behind every label (bitmap fonts have no outline).")]
        [SerializeField] Color textShadow = new(0.039f, 0.047f, 0.094f, 1f);
        [SerializeField] Color accent = new(1f, 0.784f, 0.267f, 1f);
        [SerializeField] Color danger = new(1f, 0.353f, 0.29f, 1f);
        [SerializeField] Color positive = new(0.49f, 0.894f, 0.42f, 1f);
        [Tooltip("Empty level stars and other disabled marks.")]
        [SerializeField] Color disabled = new(0.33f, 0.36f, 0.5f, 1f);

        [Header("Players (P1, P2)")]
        [SerializeField] Color[] playerColors = { new(1f, 0.604f, 0.235f, 1f), new(0.62f, 0.72f, 1f, 1f) };

        [Header("Rarity")]
        [SerializeField] Color rarityCommon = new(0.85f, 0.87f, 0.93f, 1f);
        [SerializeField] Color rarityUncommon = new(0.43f, 0.89f, 0.54f, 1f);
        [SerializeField] Color rarityRare = new(1f, 0.71f, 0.24f, 1f);

        [Header("HP")]
        [Tooltip("HP fraction at or below which the heart pulses and numbers turn red.")]
        [SerializeField, Range(0f, 1f)] float lowHpFraction = 0.3f;

        [Header("Overlays")]
        [Tooltip("Tint of the Overlay_Dim sprite (the art already is a 72% navy; white keeps it).")]
        [SerializeField] Color dimColor = new(1f, 1f, 1f, 0.9f);
        [Tooltip("Tint of the Overlay_Vignette sprite.")]
        [SerializeField] Color vignetteColor = new(1f, 1f, 1f, 0.85f);
        [Tooltip("Stage intro band (flat colour).")]
        [SerializeField] Color stageBandColor = new(0.03f, 0.04f, 0.1f, 0.8f);
        [Tooltip("Stage intro band on boss stages.")]
        [SerializeField] Color bossTint = new(0.42f, 0.04f, 0.07f, 0.85f);
        [Tooltip("Opaque fade between calibration and the first stage intro (hides the board being built).")]
        [SerializeField] Color curtainColor = new(0.047f, 0.071f, 0.157f, 1f);
        [SerializeField, Range(0.05f, 1.5f)] float curtainFadeInDuration = 0.3f;
        [SerializeField, Range(0.05f, 1.5f)] float curtainFadeOutDuration = 0.5f;

        [Header("Sprites (pixel UI kit, filled by UiViewsBuilder from Assets/Sprites/BilliardRogue/UI)")]
        [SerializeField] Sprite? panel;
        [SerializeField] Sprite? card;
        [SerializeField] Sprite? button;
        [SerializeField] Sprite? buttonFocused;
        [SerializeField] Sprite? banner;
        [SerializeField] Sprite? slot;
        [SerializeField] Sprite? slotActive;
        [SerializeField] Sprite? barBackground;
        [SerializeField] Sprite? hpFill;
        [SerializeField] Sprite? bossFill;
        [SerializeField] Sprite? chip;
        [SerializeField] Sprite? heart;
        [SerializeField] Sprite? ball;
        [SerializeField] Sprite? skull;
        [SerializeField] Sprite? turn;
        [SerializeField] Sprite? arrowRight;
        [SerializeField] Sprite? arrowLeft;
        [SerializeField] Sprite? cursor;
        [SerializeField] Sprite? dim;
        [SerializeField] Sprite? vignette;
        [SerializeField] Sprite? logo;
        [SerializeField] Sprite? portraitP1;
        [SerializeField] Sprite? portraitP2;
        [SerializeField] Sprite? rewardHeal;
        [SerializeField] Sprite? rewardMaxHp;
        [Tooltip("Camera placeholder glyph (PiP and calibration before the first camera frame).")]
        [SerializeField] Sprite? cameraGlyph;
        [Tooltip("Calibration controls: paw, cue and strike glyphs.")]
        [SerializeField] Sprite? paw;
        [SerializeField] Sprite? cue;
        [SerializeField] Sprite? strike;
        [Tooltip("Reward motion pick (make_arms.py): fur sleeve segments and paw frames per player, hold ring, glow, shadow.")]
        [SerializeField] Sprite? armP1;
        [SerializeField] Sprite? armP2;
        [SerializeField] Sprite? pawOpenP1;
        [SerializeField] Sprite? pawGrabP1;
        [SerializeField] Sprite? pawOpenP2;
        [SerializeField] Sprite? pawGrabP2;
        [SerializeField] Sprite? ringHold;
        [SerializeField] Sprite? glowDisc;
        [SerializeField] Sprite? shadowBall;
        [Tooltip("Grey bar fill of the HUD Hype meter (tinted per tier).")]
        [SerializeField] Sprite? hypeFill;

        [Header("View motion (unscaled seconds)")]
        [SerializeField, Range(0.05f, 1f)] float presentDuration = 0.26f;
        [SerializeField, Range(0.05f, 1f)] float dismissDuration = 0.18f;
        [Tooltip("Fade of a view that another view covers or uncovers.")]
        [SerializeField, Range(0.05f, 1f)] float backgroundDuration = 0.18f;
        [Tooltip("Scale the main panel pops in from.")]
        [SerializeField, Range(0.5f, 1f)] float presentScaleFrom = 0.92f;

        [Header("Focus highlight")]
        [SerializeField, Range(1f, 1.3f)] float focusScale = 1.06f;
        [Tooltip("Seconds of one half of the focus scale pulse.")]
        [SerializeField, Range(0.1f, 2f)] float focusPulseDuration = 0.55f;
        [SerializeField, Range(0f, 32f)] float cursorBobDistance = 9f;
        [SerializeField, Range(0.05f, 1f)] float cursorBobDuration = 0.32f;

        [Header("HUD motion")]
        [SerializeField, Range(0f, 1f)] float barTweenDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] float damageShakeDuration = 0.35f;
        [SerializeField, Range(0f, 64f)] float damageShakeStrength = 14f;
        [SerializeField, Range(0.05f, 1f)] float bannerSlideDuration = 0.22f;
        [SerializeField, Range(0f, 400f)] float bannerSlideDistance = 160f;
        [SerializeField, Range(1f, 1.5f)] float chipPulseScale = 1.08f;
        [SerializeField, Range(0.1f, 2f)] float chipPulseDuration = 0.45f;
        [Tooltip("HUD columns slide in from the screen edges after the stage intro band has left.")]
        [SerializeField, Range(0.05f, 1f)] float hudRevealDuration = 0.35f;
        [SerializeField, Range(0f, 480f)] float hudRevealSlide = 96f;

        [Header("Overlay timings (unscaled seconds)")]
        [Tooltip("How long 'Found you! Resuming…' stays before the tracking-lost overlay closes.")]
        [SerializeField, Range(0f, 3f)] float trackingResumeHold = 0.7f;
        [SerializeField, Range(0f, 3f)] float summaryCountUpDuration = 0.9f;
        [SerializeField, Range(0.05f, 1f)] float rewardRevealDuration = 0.32f;
        [Tooltip("Extra scale of the picked reward card.")]
        [SerializeField, Range(0f, 0.5f)] float rewardPickPunch = 0.14f;

        [Header("Reward motion pick (GDD v2 §4, unscaled seconds)")]
        [Tooltip("Both paws on one ball for this long picks it.")]
        [SerializeField, Range(0.2f, 3f)] float rewardHoldSeconds = 0.8f;
        [Tooltip("Drain speed of the hold fill relative to its fill speed once a paw leaves.")]
        [SerializeField, Range(0.5f, 6f)] float rewardHoldDrain = 2f;
        [Tooltip("Paw hit radius as a share of the ball visual radius (generous = easier on TV).")]
        [SerializeField, Range(0.8f, 2f)] float rewardPawHitScale = 1.25f;
        [Tooltip("Paw follow sharpness (1/s, exponential smoothing of the tracked hand).")]
        [SerializeField, Range(2f, 40f)] float rewardPawSharpness = 16f;
        [SerializeField, Range(1f, 1.5f)] float rewardHoverScale = 1.18f;
        [SerializeField, Range(0f, 40f)] float rewardBobDistance = 12f;
        [SerializeField, Range(0.3f, 4f)] float rewardBobPeriod = 1.7f;
        [Tooltip("Arms rise from below the screen edge after the balls appear.")]
        [SerializeField, Range(0.05f, 1.5f)] float rewardArmRiseDuration = 0.4f;
        [Tooltip("Paws travel to the picked ball and close.")]
        [SerializeField, Range(0.05f, 1f)] float rewardGrabDuration = 0.16f;
        [SerializeField] SfxManager.SoundEffect rewardHoverSfx = SfxManager.SoundEffect.UiMove;
        [SerializeField] VfxManager.VisualEffect rewardPickVfx = VfxManager.VisualEffect.LevelUpBurst;

        [Header("Hype meter + MOVE prompt (GDD v2 §3)")]
        [Tooltip("Meter fill colour per tier 0..3.")]
        [SerializeField] Color[] hypeTierColors =
        {
            new(0.45f, 0.62f, 1f, 1f), new(1f, 0.784f, 0.267f, 1f), new(1f, 0.55f, 0.2f, 1f), new(1f, 0.33f, 0.45f, 1f),
        };
        [Tooltip("Stinger when a tier is reached (index = tier - 1).")]
        [SerializeField] SfxManager.SoundEffect[] hypeTierSfx =
        {
            SfxManager.SoundEffect.PickupPower, SfxManager.SoundEffect.PowerShot, SfxManager.SoundEffect.LevelUp,
        };
        [Tooltip("Alpha of the meter while Hype is 0 (no ball in flight).")]
        [SerializeField, Range(0f, 1f)] float hypeIdleAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] float hypeTierPunch = 0.3f;
        [Tooltip("Seconds per dance step of the MOVE prompt cat (sway + paw wave).")]
        [SerializeField, Range(0.1f, 1f)] float moveDanceBeat = 0.32f;
        [SerializeField, Range(0f, 45f)] float moveDanceAngle = 14f;

        [Header("Sounds")]
        [SerializeField] SfxManager.SoundEffect moveSfx = SfxManager.SoundEffect.UiMove;
        [SerializeField] SfxManager.SoundEffect selectSfx = SfxManager.SoundEffect.UiSelect;
        [Tooltip("Played by views for in-view back actions; the top-level Back button already plays GenericExit.")]
        [SerializeField] SfxManager.SoundEffect backSfx = SfxManager.SoundEffect.UiBack;
        [SerializeField] SfxManager.SoundEffect pauseSfx = SfxManager.SoundEffect.UiPause;
        [SerializeField] SfxManager.SoundEffect resumeSfx = SfxManager.SoundEffect.UiResume;
        [SerializeField] SfxManager.SoundEffect stageIntroSfx = SfxManager.SoundEffect.TurnStart;
        [SerializeField] SfxManager.SoundEffect bossIntroSfx = SfxManager.SoundEffect.BossAppear;
        [SerializeField] SfxManager.SoundEffect rewardRevealSfx = SfxManager.SoundEffect.RewardReveal;
        [SerializeField] SfxManager.SoundEffect rewardPickSfx = SfxManager.SoundEffect.RewardPick;
        [SerializeField] SfxManager.SoundEffect newRecordSfx = SfxManager.SoundEffect.LevelUp;
        [SerializeField] SfxManager.SoundEffect trackingLostSfx = SfxManager.SoundEffect.LowHpWarning;

        #region Properties

        public TMP_FontAsset? RegularFont => regularFont;
        public TMP_FontAsset? BoldFont => boldFont;

        public Color TextPrimary => textPrimary;
        public Color TextMuted => textMuted;
        public Color TextDark => textDark;
        public Color TextShadow => textShadow;
        public Color Accent => accent;
        public Color Danger => danger;
        public Color Positive => positive;
        public Color Disabled => disabled;
        public float LowHpFraction => lowHpFraction;
        public Color DimColor => dimColor;
        public Color VignetteColor => vignetteColor;
        public Color StageBandColor => stageBandColor;
        public Color BossTint => bossTint;
        public Color CurtainColor => curtainColor;
        public float CurtainFadeInDuration => curtainFadeInDuration;
        public float CurtainFadeOutDuration => curtainFadeOutDuration;

        public Sprite? Panel => panel;
        public Sprite? Card => card;
        public Sprite? Button => button;
        public Sprite? ButtonFocused => buttonFocused;
        public Sprite? Banner => banner;
        public Sprite? Slot => slot;
        public Sprite? SlotActive => slotActive;
        public Sprite? BarBackground => barBackground;
        public Sprite? HpFill => hpFill;
        public Sprite? BossFill => bossFill;
        public Sprite? Chip => chip;
        public Sprite? Heart => heart;
        public Sprite? Ball => ball;
        public Sprite? Skull => skull;
        public Sprite? Turn => turn;
        public Sprite? ArrowRight => arrowRight;
        public Sprite? ArrowLeft => arrowLeft;
        public Sprite? Cursor => cursor;
        public Sprite? Dim => dim;
        public Sprite? Vignette => vignette;
        public Sprite? Logo => logo;
        public Sprite? PortraitP1 => portraitP1;
        public Sprite? PortraitP2 => portraitP2;
        public Sprite? RewardHeal => rewardHeal;
        public Sprite? RewardMaxHp => rewardMaxHp;
        public Sprite? CameraGlyph => cameraGlyph;
        public Sprite? Paw => paw;
        public Sprite? Cue => cue;
        public Sprite? Strike => strike;
        public Sprite? RingHold => ringHold;
        public Sprite? GlowDisc => glowDisc;
        public Sprite? ShadowBall => shadowBall;
        public Sprite? HypeFill => hypeFill;

        public float PresentDuration => presentDuration;
        public float DismissDuration => dismissDuration;
        public float BackgroundDuration => backgroundDuration;
        public float PresentScaleFrom => presentScaleFrom;
        public float FocusScale => focusScale;
        public float FocusPulseDuration => focusPulseDuration;
        public float CursorBobDistance => cursorBobDistance;
        public float CursorBobDuration => cursorBobDuration;
        public float BarTweenDuration => barTweenDuration;
        public float DamageShakeDuration => damageShakeDuration;
        public float DamageShakeStrength => damageShakeStrength;
        public float BannerSlideDuration => bannerSlideDuration;
        public float BannerSlideDistance => bannerSlideDistance;
        public float ChipPulseScale => chipPulseScale;
        public float ChipPulseDuration => chipPulseDuration;
        public float HudRevealDuration => hudRevealDuration;
        public float HudRevealSlide => hudRevealSlide;
        public float TrackingResumeHold => trackingResumeHold;
        public float SummaryCountUpDuration => summaryCountUpDuration;
        public float RewardRevealDuration => rewardRevealDuration;
        public float RewardPickPunch => rewardPickPunch;
        public float RewardHoldSeconds => rewardHoldSeconds;
        public float RewardHoldDrain => rewardHoldDrain;
        public float RewardPawHitScale => rewardPawHitScale;
        public float RewardPawSharpness => rewardPawSharpness;
        public float RewardHoverScale => rewardHoverScale;
        public float RewardBobDistance => rewardBobDistance;
        public float RewardBobPeriod => rewardBobPeriod;
        public float RewardArmRiseDuration => rewardArmRiseDuration;
        public float RewardGrabDuration => rewardGrabDuration;
        public SfxManager.SoundEffect RewardHoverSfx => rewardHoverSfx;
        public VfxManager.VisualEffect RewardPickVfx => rewardPickVfx;
        public float HypeIdleAlpha => hypeIdleAlpha;
        public float HypeTierPunch => hypeTierPunch;
        public float MoveDanceBeat => moveDanceBeat;
        public float MoveDanceAngle => moveDanceAngle;

        public SfxManager.SoundEffect MoveSfx => moveSfx;
        public SfxManager.SoundEffect SelectSfx => selectSfx;
        public SfxManager.SoundEffect BackSfx => backSfx;
        public SfxManager.SoundEffect PauseSfx => pauseSfx;
        public SfxManager.SoundEffect ResumeSfx => resumeSfx;
        public SfxManager.SoundEffect StageIntroSfx => stageIntroSfx;
        public SfxManager.SoundEffect BossIntroSfx => bossIntroSfx;
        public SfxManager.SoundEffect RewardRevealSfx => rewardRevealSfx;
        public SfxManager.SoundEffect RewardPickSfx => rewardPickSfx;
        public SfxManager.SoundEffect NewRecordSfx => newRecordSfx;
        public SfxManager.SoundEffect TrackingLostSfx => trackingLostSfx;

        #endregion

        #region Public Methods

        /// <summary>Player colour by index (wraps for safety when a third player is ever added).</summary>
        public Color PlayerColor(int playerIndex) => playerColors[playerIndex % playerColors.Length];

        public Color RarityColor(BallRarity rarity) => rarity switch
        {
            BallRarity.Uncommon => rarityUncommon,
            BallRarity.Rare => rarityRare,
            _ => rarityCommon,
        };

        public Sprite? Portrait(int playerIndex) => playerIndex == 0 ? portraitP1 : portraitP2;

        /// <summary>Fur sleeve segment of the player's cat arm (P1 orange, P2 charcoal).</summary>
        public Sprite? Arm(int playerIndex) => playerIndex == 0 ? armP1 : armP2;

        public Sprite? PawOpen(int playerIndex) => playerIndex == 0 ? pawOpenP1 : pawOpenP2;
        public Sprite? PawGrab(int playerIndex) => playerIndex == 0 ? pawGrabP1 : pawGrabP2;

        public Color HypeTierColor(int tier) => hypeTierColors[Mathf.Clamp(tier, 0, hypeTierColors.Length - 1)];

        public SfxManager.SoundEffect HypeTierSfx(int tier) =>
            tier >= 1 && tier <= hypeTierSfx.Length ? hypeTierSfx[tier - 1] : SfxManager.SoundEffect.None;

        public static void PlaySfx(SfxManager.SoundEffect effect)
        {
            if (effect == SfxManager.SoundEffect.None) return;
            SfxManager.Instance.PlaySoundEffect(effect);
        }

        #endregion
    }
}
