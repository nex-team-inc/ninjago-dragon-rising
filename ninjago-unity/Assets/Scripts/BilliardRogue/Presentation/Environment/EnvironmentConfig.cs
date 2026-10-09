#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// World look tunables that are not per act: the Title backdrop look, act transition timing, fog mode and the
    /// ambient animation of the dioramas (fire flicker, crystal pulse, foliage sway, water bob). Per-act looks live in
    /// ActDefinition.lighting; the danger-row pulse lives in ArenaConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentConfig", menuName = "Nex/Billiard Rogue/Environment Config", order = 51)]
    public sealed class EnvironmentConfig : ScriptableObject
    {
        [Header("Title backdrop")]
        [Tooltip("Cozy golden look shown behind the Title menu (on the backdrop act's diorama).")]
        [SerializeField] ActLightingPreset titleLighting = new();
        [Tooltip("Post-processing profile behind the Title menu (Assets/Settings/BilliardRogue/Volumes/Volume_Title.asset).")]
        [SerializeField] VolumeProfile? titleVolumeProfile;

        [Header("Transitions")]
        [Tooltip("Seconds to blend lighting, fog and grading when a new act starts (unscaled time).")]
        [SerializeField, Range(0f, 5f)] float actTransitionSeconds = 1.6f;
        [Tooltip("Seconds to blend into the Title look.")]
        [SerializeField, Range(0f, 5f)] float titleTransitionSeconds = 1f;
        [SerializeField] Ease transitionEase = Ease.InOutSine;
        [Tooltip("Extra fog density at the middle of a transition that changes the diorama (the dioramas swap behind it). 0.07 hides ~95% of the scene at the camera distance.")]
        [SerializeField, Range(0f, 0.2f)] float swapVeilFogDensity = 0.07f;

        [Header("Fog")]
        [Tooltip("Fog falloff for ActLightingPreset.fogDensity. The camera is ~22-30 m from the floor, so ExponentialSquared keeps the arena clear while the far background hazes.")]
        [SerializeField] FogMode fogMode = FogMode.ExponentialSquared;

        [Header("Fire flicker (torches, braziers, candles, lanterns)")]
        [Tooltip("Relative intensity noise of fire lights (0.2 = ±20%).")]
        [SerializeField, Range(0f, 1f)] float fireFlickerAmount = 0.18f;
        [Tooltip("Noise frequency of fire lights and flames.")]
        [SerializeField, Range(0.1f, 20f)] float fireFlickerSpeed = 6f;
        [Tooltip("Relative scale noise of flame meshes whose pivot sits at the flame base.")]
        [SerializeField, Range(0f, 0.5f)] float flameScaleAmount = 0.1f;

        [Header("Crystal pulse (crystals, runes, glow mushrooms)")]
        [SerializeField, Range(0f, 1f)] float crystalPulseAmount = 0.22f;
        [Tooltip("Pulses per second.")]
        [SerializeField, Range(0.05f, 4f)] float crystalPulseSpeed = 0.35f;

        [Header("Foliage and cloth sway")]
        [SerializeField, Range(0f, 10f)] float swayAngleDeg = 1.6f;
        [Tooltip("Sways per second.")]
        [SerializeField, Range(0.05f, 3f)] float swaySpeed = 0.28f;

        [Header("Water bob")]
        [SerializeField, Range(0f, 0.1f)] float waterBobHeight = 0.012f;
        [Tooltip("Bobs per second.")]
        [SerializeField, Range(0.05f, 3f)] float waterBobSpeed = 0.3f;

        public ActLightingPreset TitleLighting => titleLighting;
        public VolumeProfile? TitleVolumeProfile => titleVolumeProfile;
        public float ActTransitionSeconds => actTransitionSeconds;
        public float TitleTransitionSeconds => titleTransitionSeconds;
        public Ease TransitionEase => transitionEase;
        public float SwapVeilFogDensity => swapVeilFogDensity;
        public FogMode FogMode => fogMode;
        public float FireFlickerAmount => fireFlickerAmount;
        public float FireFlickerSpeed => fireFlickerSpeed;
        public float FlameScaleAmount => flameScaleAmount;
        public float CrystalPulseAmount => crystalPulseAmount;
        public float CrystalPulseSpeed => crystalPulseSpeed;
        public float SwayAngleDeg => swayAngleDeg;
        public float SwaySpeed => swaySpeed;
        public float WaterBobHeight => waterBobHeight;
        public float WaterBobSpeed => waterBobSpeed;
    }
}
