#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Per-act lighting look applied by ActEnvironmentController (sun, ambient, fog, rim, god rays, particles).
    /// Directions use the arena frame of TDD §14.1 (x right, z up the arena, camera south looking north).
    /// </summary>
    [Serializable]
    public sealed class ActLightingPreset
    {
        [Header("Sun")]
        public Color sunColor = new(1f, 0.93f, 0.8f);
        [Range(0f, 8f)] public float sunIntensity = 1.4f;
        [Tooltip("Euler rotation of the directional light: x = elevation (degrees below the horizon), y = yaw in the arena frame.")]
        public Vector3 sunEuler = new(52f, -28f, 0f);
        [Tooltip("Darkness of the hard sun shadows (0 = none, 1 = black before ambient).")]
        [Range(0f, 1f)] public float shadowStrength = 0.85f;

        [Header("Ambient (trilight)")]
        public Color ambientSky = new(0.55f, 0.62f, 0.7f);
        public Color ambientEquator = new(0.4f, 0.42f, 0.38f);
        public Color ambientGround = new(0.18f, 0.16f, 0.14f);

        [Header("Fog")]
        public Color fogColor = new(0.6f, 0.7f, 0.75f);
        [Range(0f, 0.2f)] public float fogDensity = 0.012f;

        [Header("Accents")]
        [Tooltip("Published as the global shader colour _WorldRimColor for BilliardRogue/ToonLit.")]
        public Color rimColor = new(1f, 0.85f, 0.6f);
        [Tooltip("Tint multiplied into every additional light of the act environment (torches, crystals).")]
        public Color additionalLightTint = Color.white;
        [Tooltip("Feeds the HDR _Color of BilliardRogue/LightShaft (TDD §16); values above 1 bloom.")]
        [ColorUsage(false, true)] public Color godRayColor = new(1f, 0.9f, 0.7f);
        [Range(0f, 3f)] public float godRayIntensity = 0.8f;
        [Tooltip("Multiplied into the start colour of the act's ambient particles when they are spawned.")]
        public Color particleTint = Color.white;

        [Header("Authoring")]
        [Tooltip("EnvironmentBuilder copies the art-directed preset from Tools/Blender/environment/layouts.json into this block while this is false, then sets it. Clear it to re-seed; edits are kept while it is set.")]
        public bool seededFromLayout;
    }
}
