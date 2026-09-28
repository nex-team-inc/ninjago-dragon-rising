#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Shared VFX colours (vertex colours are LDR; glow materials add the HDR boost, so glow colours stay saturated: only
    /// the dominant channel crosses the bloom threshold and the hue survives tonemapping) and lifetime curves. Hues follow the
    /// GDD damage-number colours: white normal, yellow crit, orange burn, purple poison, green heal, cyan freeze.
    /// </summary>
    public static class VfxPalette
    {
        // Flipbooks play at 12 fps: one frame lasts 1/12 s, a full 8-frame one-shot 2/3 s.
        public const float Frame = 1f / 12f;
        public const float FullSheet = 8f * Frame;

        public static readonly Color WarmWhite = new(1f, 0.92f, 0.72f);
        public static readonly Color PaleYellow = new(1f, 0.86f, 0.42f);
        public static readonly Color SparkYellow = new(1f, 0.74f, 0.22f);
        public static readonly Color Gold = new(1f, 0.6f, 0.12f);
        public static readonly Color Orange = new(1f, 0.42f, 0.08f);
        public static readonly Color DeepOrange = new(0.9f, 0.26f, 0.05f);
        public static readonly Color Red = new(1f, 0.2f, 0.16f);
        public static readonly Color Ice = new(0.7f, 0.92f, 1f);
        public static readonly Color Cyan = new(0.3f, 0.74f, 1f);
        public static readonly Color Violet = new(0.62f, 0.3f, 1f);
        public static readonly Color Pink = new(1f, 0.42f, 0.88f);
        public static readonly Color HeartPink = new(1f, 0.28f, 0.44f);
        public static readonly Color HealGreen = new(0.38f, 1f, 0.42f);
        public static readonly Color PoisonGreen = new(0.5f, 0.95f, 0.18f);
        public static readonly Color PoisonPurple = new(0.6f, 0.24f, 0.9f);
        public static readonly Color SmokeLight = new(0.93f, 0.91f, 0.98f);
        public static readonly Color SmokeLavender = new(0.7f, 0.66f, 0.84f);
        public static readonly Color Soot = new(0.42f, 0.38f, 0.42f);
        public static readonly Color SootDark = new(0.28f, 0.26f, 0.3f);
        public static readonly Color Dust = new(0.88f, 0.82f, 0.68f);
        public static readonly Color DustDark = new(0.76f, 0.68f, 0.55f);
        public static readonly Color Stone = new(0.8f, 0.78f, 0.74f);
        public static readonly Color Rubble = new(0.52f, 0.47f, 0.44f);
        public static readonly Color RubbleDark = new(0.36f, 0.32f, 0.32f);
        public static readonly Color Wood = new(0.78f, 0.55f, 0.32f);
        public static readonly Color WoodLight = new(0.92f, 0.8f, 0.6f);
        public static readonly Color LeafGreen = new(0.6f, 0.84f, 0.3f);
        public static readonly Color LeafAutumn = new(0.96f, 0.64f, 0.22f);

        /// <summary>Grows in over the first tenth of the life and shrinks away over the last sixth (ambient pop-in guard).</summary>
        public static AnimationCurve PopInOut() => new(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.84f, 1f), new Keyframe(1f, 0f));

        /// <summary>Quick overshoot for flashes and rings: 70 % → 115 % → 100 %.</summary>
        public static AnimationCurve Punch() => new(new Keyframe(0f, 0.7f), new Keyframe(0.25f, 1.15f), new Keyframe(1f, 1f));

        /// <summary>Holds then shrinks in the last third (sparks and embers taper instead of popping).</summary>
        public static AnimationCurve Taper() => new(new Keyframe(0f, 1f), new Keyframe(0.66f, 1f), new Keyframe(1f, 0.35f));
    }
}
