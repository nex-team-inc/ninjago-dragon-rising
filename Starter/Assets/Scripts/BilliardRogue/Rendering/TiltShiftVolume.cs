#nullable enable

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Volume override read by TiltShiftFeature: blurs the world outside a horizontal focus band (HD-2D diorama look).
    /// Lives in the per-act Volume profiles; the feature-override volume on the rig sets intensity 0 to switch it off.
    /// </summary>
    [Serializable, VolumeComponentMenu("Billiard Rogue/Tilt Shift")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class TiltShiftVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Blend of the blurred image outside the focus band; 0 disables the effect.")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);

        [Tooltip("Focus band centre, 0 = screen bottom, 1 = top.")]
        public ClampedFloatParameter center = new(0.45f, 0f, 1f);

        [Tooltip("Half height of the sharp band, in screen fraction.")]
        public ClampedFloatParameter bandWidth = new(0.18f, 0f, 0.5f);

        [Tooltip("Screen fraction over which the blur ramps from sharp to fully blurred.")]
        public ClampedFloatParameter falloff = new(0.25f, 0.01f, 1f);

        [Tooltip("Tap spacing in texels of each half-resolution blur pass; higher = wider blur.")]
        public ClampedFloatParameter maxBlur = new(1.5f, 0.5f, 4f);

        [Tooltip("Gaussian taps per side and pass (cost grows linearly).")]
        public ClampedIntParameter sampleCount = new(4, 1, 8);

        public bool IsActive() => intensity.value > 0f;
    }
}
