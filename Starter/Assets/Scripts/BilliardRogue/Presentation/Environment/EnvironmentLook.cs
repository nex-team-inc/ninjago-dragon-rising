#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Blendable snapshot of an ActLightingPreset, so act transitions can interpolate every value without allocating.</summary>
    public struct EnvironmentLook
    {
        public Color sunColor;
        public float sunIntensity;
        public Quaternion sunRotation;
        public float shadowStrength;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public Color fogColor;
        public float fogDensity;
        public Color rimColor;
        public Color lightTint;
        public Color godRayColor;
        public float godRayIntensity;

        public static EnvironmentLook From(ActLightingPreset preset)
        {
            return new EnvironmentLook
            {
                sunColor = preset.sunColor,
                sunIntensity = preset.sunIntensity,
                sunRotation = Quaternion.Euler(preset.sunEuler),
                shadowStrength = preset.shadowStrength,
                ambientSky = preset.ambientSky,
                ambientEquator = preset.ambientEquator,
                ambientGround = preset.ambientGround,
                fogColor = preset.fogColor,
                fogDensity = preset.fogDensity,
                rimColor = preset.rimColor,
                lightTint = preset.additionalLightTint,
                godRayColor = preset.godRayColor,
                godRayIntensity = preset.godRayIntensity,
            };
        }

        public static EnvironmentLook Lerp(in EnvironmentLook a, in EnvironmentLook b, float t)
        {
            return new EnvironmentLook
            {
                sunColor = Color.LerpUnclamped(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.LerpUnclamped(a.sunIntensity, b.sunIntensity, t),
                sunRotation = Quaternion.SlerpUnclamped(a.sunRotation, b.sunRotation, t),
                shadowStrength = Mathf.LerpUnclamped(a.shadowStrength, b.shadowStrength, t),
                ambientSky = Color.LerpUnclamped(a.ambientSky, b.ambientSky, t),
                ambientEquator = Color.LerpUnclamped(a.ambientEquator, b.ambientEquator, t),
                ambientGround = Color.LerpUnclamped(a.ambientGround, b.ambientGround, t),
                fogColor = Color.LerpUnclamped(a.fogColor, b.fogColor, t),
                fogDensity = Mathf.LerpUnclamped(a.fogDensity, b.fogDensity, t),
                rimColor = Color.LerpUnclamped(a.rimColor, b.rimColor, t),
                lightTint = Color.LerpUnclamped(a.lightTint, b.lightTint, t),
                godRayColor = Color.LerpUnclamped(a.godRayColor, b.godRayColor, t),
                godRayIntensity = Mathf.LerpUnclamped(a.godRayIntensity, b.godRayIntensity, t),
            };
        }
    }
}
