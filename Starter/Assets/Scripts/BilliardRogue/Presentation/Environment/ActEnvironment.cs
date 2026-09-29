#nullable enable

using System;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Root of an Env_Act{n} diorama prefab (TDD §17), laid out in the arena frame (TDD §14.1). Holds what
    /// ActEnvironmentController needs to apply the act: the arena surface materials, the light shafts and the
    /// ambient anchor where the act's looping VfxManager effect (AmbientAct{n}) plays while the diorama is shown,
    /// and forwards the act light tint to its DioramaAnimator.
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
        [Tooltip("Identity anchor in the arena frame; the act's ambient effect (ActDefinition.ambientEffect) plays here through VfxManager.")]
        [SerializeField] Transform ambientAnchor = null!;

        MaterialPropertyBlock shaftBlock = null!;
        ParticleSystem? ambient;

        public int ActIndex => actIndex;
        public EnumDictionary<ArenaSurface, Material> ArenaSurfaces => arenaSurfaces;
        public Transform AmbientAnchor => ambientAnchor;
        public int LightCount => animator.LightCount;

        #region Life Cycle

        public void Initialize(EnvironmentConfig config)
        {
            shaftBlock = new MaterialPropertyBlock();
            animator.Initialize(config);
        }

        void OnDestroy() => StopAmbient();

        #endregion

        #region Public Methods

        /// <summary>Shows or hides the diorama; hiding returns its ambient effect to the VfxManager pool.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible) StopAmbient();
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

        /// <summary>
        /// Plays the ambient effect at the anchor through VfxManager (once per showing; a later call only retints)
        /// with the look's particle tint applied over the authored colours.
        /// </summary>
        public void ShowAmbient(VfxManager.VisualEffect effect, Color tint)
        {
            // Edit-mode builder tests apply the looks without the singleton prefabs: no atmosphere there.
            var manager = VfxManager.Instance;
            if (manager == null) return;
            if (ambient == null)
            {
                ambient = manager.PlayVisualEffect(effect, ambientAnchor.position, ambientAnchor.rotation);
                // Unregistered effect (the manager warned): nothing to tint.
                if (ambient == null) return;
            }

            var tinter = ambient.GetComponent<AmbientParticleTint>();
            if (tinter == null) tinter = ambient.gameObject.AddComponent<AmbientParticleTint>();
            tinter.Apply(tint);
        }

        #endregion

        #region Helpers

        // Clearing ends the loop at once; the pooled instance's stop callback then hands it back to the pool.
        void StopAmbient()
        {
            if (ambient == null) return;
            ambient.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ambient = null;
        }

        #endregion
    }
}
