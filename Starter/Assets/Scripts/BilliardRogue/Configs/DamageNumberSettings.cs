#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Floating damage numbers (JuiceConfig.numbers): every ball hits in its own colour as a light-to-deep gradient over
    /// a dark outline, flashes white-hot when it lands, grows and pops harder with damage, combo and the killing blow,
    /// launches on an arc with a lean, and crits cycle the rainbow. Font sizes stay multiples of 16 (the BilliardPixel
    /// raster), so the numbers are crisp once their pop settles.
    /// </summary>
    [Serializable]
    public sealed class DamageNumberSettings
    {
        [Header("Colour")]
        [Tooltip("Deep (bottom) colour of each ball's damage numbers; the top of the gradient blends toward white.")]
        public EnumDictionary<BallType, Color> ballColors = DefaultBallColors();
        [Tooltip("How far the top of the gradient blends from the deep colour toward white.")]
        [Range(0f, 1f)] public float highlight = 0.6f;
        [Tooltip("Outline layer under every floating text (the outline font, dropped one font pixel for a 3D edge).")]
        public Color outlineColor = new(0.05f, 0.04f, 0.1f, 1f);
        [Tooltip("How far the outline takes on the text colour (0 = the plain outline colour).")]
        [Range(0f, 0.5f)] public float outlineTint = 0.18f;
        [Tooltip("Seconds a new number stays white-hot before it shows its colours.")]
        [Range(0f, 0.3f)] public float flashSeconds = 0.07f;

        [Header("Size and pop")]
        [Tooltip("Font size added at JuiceConfig mid damage and again at hard damage (keep multiples of 16).")]
        [Range(0f, 32f)] public float sizeStep = 16f;
        [Tooltip("Largest font size a number reaches with every bonus (Hype, crit, killing blow).")]
        [Range(32f, 160f)] public float maxSize = 112f;
        [Tooltip("Pop-in overshoot of a 1-damage hit.")]
        [Range(0f, 2f)] public float popSmall = 0.35f;
        [Tooltip("Pop-in overshoot from JuiceConfig hard damage on.")]
        [Range(0f, 2f)] public float popBig = 1f;
        [Tooltip("Extra overshoot for every combo hit of the same ball after the first, up to popComboMax.")]
        [Range(0f, 0.3f)] public float popPerCombo = 0.06f;
        [Range(0f, 1.5f)] public float popComboMax = 0.5f;
        [Tooltip("Lifetime multiplier of mid-or-harder, crit and killing numbers, so they linger.")]
        [Range(1f, 2f)] public float bigLifetimeScale = 1.35f;

        [Header("Motion (canvas pixels, seconds)")]
        [Tooltip("Upward launch speed, random between x and y.")]
        public Vector2 launchUp = new(210f, 300f);
        [Tooltip("Sideways launch speed to a random side, random between x and y.")]
        public Vector2 launchSide = new(40f, 130f);
        [Range(0f, 3000f)] public float gravity = 700f;
        [Tooltip("Lean into the sideways launch in degrees, easing upright.")]
        [Range(0f, 40f)] public float tilt = 12f;
        [Tooltip("Impact shake of mid-or-harder hits over the first third of their life (pixels).")]
        [Range(0f, 16f)] public float shake = 5f;

        [Header("Killing blow")]
        [Tooltip("Top colour of a killing blow's gradient.")]
        public Color killHighlight = new(1f, 1f, 0.9f, 1f);
        [Range(0f, 48f)] public float killSizeBonus = 16f;
        [Range(0f, 1.5f)] public float killPopBonus = 0.4f;

        [Header("Crit")]
        [Range(0f, 48f)] public float critSizeBonus = 16f;
        [Tooltip("Crit numbers cycle the rainbow (hue turns per second, stepped at 12 fps like the VFX).")]
        [Range(0f, 4f)] public float rainbowSpeed = 1.5f;

        [Header("Combo text")]
        [Tooltip("COMBO text colour at JuiceConfig comboShowThreshold, blending to comboHigh at comboRainbowAt.")]
        public Color comboLow = new(1f, 0.85f, 0.2f, 1f);
        public Color comboHigh = new(1f, 0.25f, 0.6f, 1f);
        [Tooltip("From this combo on the COMBO text cycles the rainbow.")]
        [Range(3, 40)] public int comboRainbowAt = 12;

        static EnumDictionary<BallType, Color> DefaultBallColors()
        {
            return new EnumDictionary<BallType, Color>
            {
                [BallType.Basic] = new(1f, 0.8f, 0.24f, 1f),
                [BallType.Flame] = new(1f, 0.42f, 0.12f, 1f),
                [BallType.Frost] = new(0.38f, 0.88f, 1f, 1f),
                [BallType.Thunder] = new(0.92f, 1f, 0.3f, 1f),
                [BallType.Bomb] = new(1f, 0.28f, 0.16f, 1f),
                [BallType.Splitter] = new(0.78f, 0.48f, 1f, 1f),
                [BallType.Piercer] = new(0.3f, 1f, 0.82f, 1f),
                [BallType.Iron] = new(0.74f, 0.82f, 0.96f, 1f),
                [BallType.Venom] = new(0.52f, 1f, 0.24f, 1f),
                [BallType.Vampire] = new(1f, 0.2f, 0.36f, 1f),
                [BallType.Rubber] = new(1f, 0.48f, 0.8f, 1f),
                [BallType.Lucky] = new(1f, 0.94f, 0.55f, 1f),
            };
        }
    }
}
