#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>HD-2D look: low-res world render target, tilt-shift band, post defaults and per-quality overrides.</summary>
    [CreateAssetMenu(fileName = "HD2DVisualConfig", menuName = "Nex/Billiard Rogue/HD2D Visual Config", order = 55)]
    public sealed class HD2DVisualConfig : ScriptableObject
    {
        [Serializable]
        public sealed class QualityOverride
        {
            [Tooltip("QualitySettings level name this override applies to.")]
            public string qualityName = "";
            [Tooltip("Visible RT height; 0 keeps the default.")]
            public int renderHeight;
            public bool bloom = true;
            public bool tiltShift = true;
            public bool shadows = true;
        }

        [Header("World render target")]
        [Tooltip("Visible texels; 1080 / 360 = 3× integer upscale.")]
        [SerializeField] Vector2Int renderResolution = new(640, 360);
        [Tooltip("Extra texels per side so sub-texel camera motion can shift the uvRect.")]
        [SerializeField, Range(0, 4)] int marginTexels = 1;
        [SerializeField] bool pixelSnapping = true;
        [SerializeField] bool pixelationEnabled = true;

        [Header("Tilt-shift band")]
        [SerializeField, Range(0f, 1f)] float tiltShiftIntensity = 0.8f;
        [Tooltip("Focus band centre, 0 = screen bottom, 1 = top.")]
        [SerializeField, Range(0f, 1f)] float tiltShiftCenter = 0.49f;
        [Tooltip("Half height of the sharp band in screen fraction. The playfield (cat at 0.05 .. top-row heads at 0.93 with the ArenaConfig camera) must stay inside it; only the scenery strips above and below blur.")]
        [SerializeField, Range(0f, 0.5f)] float tiltShiftHalfWidth = 0.44f;
        [SerializeField, Range(0.01f, 1f)] float tiltShiftFalloff = 0.06f;
        [SerializeField, Range(0.5f, 4f)] float tiltShiftMaxBlur = 1.5f;
        [Tooltip("Gaussian taps per side of each half-resolution blur pass.")]
        [SerializeField, Range(1, 8)] int tiltShiftSampleCount = 4;

        [Header("Post defaults")]
        [SerializeField, Range(0f, 2f)] float bloomThreshold = 0.9f;
        [SerializeField, Range(0f, 4f)] float bloomIntensity = 0.8f;
        [SerializeField, Range(0f, 1f)] float bloomScatter = 0.7f;
        [Tooltip("Bloom mip chain length; 4–5 keeps the 640×360 target cheap.")]
        [SerializeField, Range(2, 8)] int bloomMaxIterations = 4;
        [SerializeField, Range(0f, 1f)] float vignetteIntensity = 0.25f;
        [Tooltip("URP shadow distance measured from the world camera, which sits about 28 m from the arena centre (23–33 m to its far corners with the ArenaConfig pose); shadows must reach the top row.")]
        [SerializeField, Range(10f, 60f)] float shadowDistance = 42f;

        [Header("Quality")]
        [SerializeField] QualityOverride[] qualityOverrides = Array.Empty<QualityOverride>();

        public Vector2Int RenderResolution => renderResolution;
        public int MarginTexels => marginTexels;
        public bool PixelSnapping => pixelSnapping;
        public bool PixelationEnabled => pixelationEnabled;
        public float TiltShiftIntensity => tiltShiftIntensity;
        public float TiltShiftCenter => tiltShiftCenter;
        public float TiltShiftHalfWidth => tiltShiftHalfWidth;
        public float TiltShiftFalloff => tiltShiftFalloff;
        public float TiltShiftMaxBlur => tiltShiftMaxBlur;
        public int TiltShiftSampleCount => tiltShiftSampleCount;
        public float BloomThreshold => bloomThreshold;
        public float BloomIntensity => bloomIntensity;
        public float BloomScatter => bloomScatter;
        public int BloomMaxIterations => bloomMaxIterations;
        public float VignetteIntensity => vignetteIntensity;
        public float ShadowDistance => shadowDistance;
        public QualityOverride[] QualityOverrides => qualityOverrides;
    }
}
