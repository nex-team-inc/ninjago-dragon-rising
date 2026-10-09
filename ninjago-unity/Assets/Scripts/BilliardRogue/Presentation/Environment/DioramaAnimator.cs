#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Ambient life of one act diorama in a single allocation-free Update: fire lights flicker (with their flame
    /// meshes), crystal lights pulse, tree canopies and banners sway, water bobs. Entries are baked by
    /// EnvironmentBuilder; base poses are captured in Initialize. Runs on unscaled time (slow-mo never stretches the
    /// world). The component starts disabled and enables itself in Initialize.
    /// </summary>
    public sealed class DioramaAnimator : MonoBehaviour
    {
        public enum LightKind
        {
            Fire = 0,
            Crystal = 1,
        }

        [Serializable]
        public struct AnimatedLight
        {
            public Light light;
            public LightKind kind;
            [Tooltip("Intensity and colour before the act tint (ActLightingPreset.additionalLightTint).")]
            public float baseIntensity;
            public Color baseColor;
            [Tooltip("Seed so neighbouring lights never flicker in sync.")]
            public float phase;
        }

        [Serializable]
        public struct AnimatedPart
        {
            public Transform part;
            public float phase;
            [NonSerialized] public Vector3 basePosition;
            [NonSerialized] public Quaternion baseRotation;
            [NonSerialized] public Vector3 baseScale;
        }

        [Header("Lights")]
        [SerializeField] AnimatedLight[] lights = Array.Empty<AnimatedLight>();

        [Header("Parts")]
        [Tooltip("Flame meshes whose pivot sits at the flame base; scaled with fire noise.")]
        [SerializeField] AnimatedPart[] flames = Array.Empty<AnimatedPart>();
        [Tooltip("Tree canopies and banner cloths, rotated around their pivots.")]
        [SerializeField] AnimatedPart[] swaying = Array.Empty<AnimatedPart>();
        [Tooltip("Water surfaces, bobbed along local Y.")]
        [SerializeField] AnimatedPart[] bobbing = Array.Empty<AnimatedPart>();

        EnvironmentConfig config = null!;
        float lightScale = 1f;

        public int LightCount => lights.Length;

        public void Initialize(EnvironmentConfig environmentConfig)
        {
            config = environmentConfig;
            Capture(flames);
            Capture(swaying);
            Capture(bobbing);
            enabled = true;
        }

        /// <summary>
        /// Act tint multiplied into every light colour and act multiplier on every light intensity
        /// (ActLightingPreset.additionalLightTint / additionalLightIntensity); applied at once, flicker continues on top.
        /// </summary>
        public void SetLightTint(Color tint, float intensityScale)
        {
            lightScale = intensityScale;
            for (var i = 0; i < lights.Length; i++)
            {
                lights[i].light.color = lights[i].baseColor * tint;
                lights[i].light.intensity = lights[i].baseIntensity * intensityScale;
            }
        }

        #region Animation

        void Update()
        {
            var time = Time.unscaledTime;
            AnimateLights(time);
            AnimateFlames(time);
            AnimateSway(time);
            AnimateBob(time);
        }

        void AnimateLights(float time)
        {
            var fireSpeed = config.FireFlickerSpeed;
            var crystalOmega = config.CrystalPulseSpeed * 2f * Mathf.PI;
            for (var i = 0; i < lights.Length; i++)
            {
                ref var entry = ref lights[i];
                float factor;
                if (entry.kind == LightKind.Fire)
                {
                    var noise = Mathf.PerlinNoise(time * fireSpeed + entry.phase, entry.phase * 1.7f);
                    factor = 1f + config.FireFlickerAmount * (noise * 2f - 1f);
                }
                else
                {
                    factor = 1f + config.CrystalPulseAmount * Mathf.Sin(time * crystalOmega + entry.phase);
                }

                entry.light.intensity = entry.baseIntensity * lightScale * factor;
            }
        }

        void AnimateFlames(float time)
        {
            var speed = config.FireFlickerSpeed * 1.3f;
            var amount = config.FlameScaleAmount;
            for (var i = 0; i < flames.Length; i++)
            {
                ref var entry = ref flames[i];
                var height = Mathf.PerlinNoise(time * speed + entry.phase, 0.37f) * 2f - 1f;
                var width = Mathf.PerlinNoise(0.71f, time * speed + entry.phase) * 2f - 1f;
                var scale = entry.baseScale;
                entry.part.localScale = new Vector3(scale.x * (1f + amount * 0.5f * width), scale.y * (1f + amount * height), scale.z * (1f + amount * 0.5f * width));
            }
        }

        void AnimateSway(float time)
        {
            var omega = config.SwaySpeed * 2f * Mathf.PI;
            var angle = config.SwayAngleDeg;
            for (var i = 0; i < swaying.Length; i++)
            {
                ref var entry = ref swaying[i];
                var x = angle * Mathf.Sin(time * omega + entry.phase);
                var z = angle * 0.6f * Mathf.Sin(time * omega * 1.37f + entry.phase * 2.3f);
                entry.part.localRotation = entry.baseRotation * Quaternion.Euler(x, 0f, z);
            }
        }

        void AnimateBob(float time)
        {
            var omega = config.WaterBobSpeed * 2f * Mathf.PI;
            var height = config.WaterBobHeight;
            for (var i = 0; i < bobbing.Length; i++)
            {
                ref var entry = ref bobbing[i];
                entry.part.localPosition = entry.basePosition + new Vector3(0f, height * Mathf.Sin(time * omega + entry.phase), 0f);
            }
        }

        #endregion

        #region Helpers

        static void Capture(AnimatedPart[] parts)
        {
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].part;
                parts[i].basePosition = part.localPosition;
                parts[i].baseRotation = part.localRotation;
                parts[i].baseScale = part.localScale;
            }
        }

        #endregion
    }
}
