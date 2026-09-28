#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Root of an Env_Act{n} diorama prefab (TDD §17), laid out in the arena frame (TDD §14.1). Holds what
    /// ActEnvironmentController needs to apply the act: the arena surface materials, the light shafts and the
    /// ambient-particle anchor, and forwards the act light tint to its DioramaAnimator.
    /// </summary>
    public sealed class ActEnvironment : MonoBehaviour
    {
        static readonly int ShaftColorId = Shader.PropertyToID("_Color");
        static readonly int ShaftIntensityId = Shader.PropertyToID("_Intensity");

        [Serializable]
        public struct LightShaft
        {
            public Renderer renderer;
            [Tooltip("Relative tint of this shaft (multiplied with ActLightingPreset.godRayColor).")]
            public Color color;
            [Tooltip("Relative intensity of this shaft (multiplied with ActLightingPreset.godRayIntensity).")]
            public float intensity;
        }

        [Header("Identity")]
        [Tooltip("ActRules.actIndex this diorama belongs to (0-based).")]
        [SerializeField, Range(0, 2)] int actIndex;

        [Header("Arena")]
        [Tooltip("Materials the arena kit's *_Surface parts take while this act is shown.")]
        [SerializeField] EnumDictionary<ArenaSurface, Material> arenaSurfaces = new();

        [Header("Parts")]
        [SerializeField] DioramaAnimator animator = null!;
        [SerializeField] LightShaft[] lightShafts = Array.Empty<LightShaft>();
        [Tooltip("Identity anchor in the arena frame; the act's ambient particle prefab (Vfx_Ambient_Act{n}) is attached here at runtime.")]
        [SerializeField] Transform ambientAnchor = null!;

        readonly List<ParticleSystem> particleBuffer = new();
        MaterialPropertyBlock shaftBlock = null!;
        GameObject? ambientInstance;

        public int ActIndex => actIndex;
        public EnumDictionary<ArenaSurface, Material> ArenaSurfaces => arenaSurfaces;
        public Transform AmbientAnchor => ambientAnchor;
        public int LightCount => animator.LightCount;

        #region Public Methods

        public void Initialize(EnvironmentConfig config)
        {
            shaftBlock = new MaterialPropertyBlock();
            animator.Initialize(config);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>Applies the blended act accents: light tint and intensity on every act light and the god-ray colour/intensity on every shaft.</summary>
        public void ApplyAccents(Color lightTint, float lightIntensity, Color godRayColor, float godRayIntensity)
        {
            animator.SetLightTint(lightTint, lightIntensity);
            for (var i = 0; i < lightShafts.Length; i++)
            {
                ref var shaft = ref lightShafts[i];
                shaftBlock.SetColor(ShaftColorId, shaft.color * godRayColor);
                shaftBlock.SetFloat(ShaftIntensityId, shaft.intensity * godRayIntensity);
                shaft.renderer.SetPropertyBlock(shaftBlock);
            }
        }

        /// <summary>Instantiates the act's ambient particles under the anchor once (kept while the diorama lives) and tints their start colours.</summary>
        public void EnsureAmbientParticles(GameObject? prefab, Color tint)
        {
            if (ambientInstance != null || prefab == null) return;
            ambientInstance = Instantiate(prefab, ambientAnchor, false);
            ambientInstance.GetComponentsInChildren(true, particleBuffer);
            foreach (var particles in particleBuffer)
            {
                var main = particles.main;
                main.startColor = Tint(main.startColor, tint);
            }

            particleBuffer.Clear();
        }

        #endregion

        #region Helpers

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient gradient, Color tint)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    gradient.color *= tint;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    gradient.colorMin *= tint;
                    gradient.colorMax *= tint;
                    break;
            }

            return gradient;
        }

        #endregion
    }
}
