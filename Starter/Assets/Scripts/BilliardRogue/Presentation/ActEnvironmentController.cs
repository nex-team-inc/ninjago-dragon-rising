#nullable enable

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Applies an act's world look (TDD §8): shows its diorama (Env_Act{n}), swaps the arena surface materials,
    /// plays its ambient effect (VfxManager AmbientAct{n}) and blends sun (hard shadows), trilight ambient, fog, rim/light tints, god rays
    /// and the post-processing profile (faded in on a blend volume, then handed to WorldCameraRig.SetVolumeProfile)
    /// from the current look over a short unscaled DOTween; a diorama change swaps behind a brief fog veil. ApplyTitle shows the cozy golden Title variant on a backdrop
    /// act. Lives on WorldLighting.prefab under World; the scene references (arena, camera rig, placed dioramas) are
    /// wired by EnvironmentBuilder.WireScene.
    /// </summary>
    public sealed class ActEnvironmentController : MonoBehaviour
    {
        /// <summary>CurrentActIndex while the Title look is shown.</summary>
        public const int TitleActIndex = -1;
        /// <summary>CurrentActIndex before the first ApplyAct / ApplyTitle.</summary>
        public const int NoActIndex = -2;
        static readonly int WorldRimColorId = Shader.PropertyToID("_WorldRimColor");

        [Header("Config")]
        [SerializeField] EnvironmentConfig config = null!;

        [Header("Lighting")]
        [Tooltip("The world's only directional light (hard shadows); rotated in the arena frame.")]
        [SerializeField] Light sun = null!;

        [Header("Grading")]
        [Tooltip("Global Volume on the WorldVolume layer, one priority above the WorldCameraRig's world volume, that fades the next profile in before it is handed to the rig.")]
        [SerializeField] Volume blendVolume = null!;

        [Header("Scene (wired by EnvironmentBuilder.WireScene)")]
        [SerializeField] ArenaView arena = null!;
        [SerializeField] WorldCameraRig cameraRig = null!;
        [Tooltip("Env_Act{n} instances placed in the scene; an act without one is instantiated from ActDefinition.environmentPrefab on first use.")]
        [SerializeField] ActEnvironment[] environments = Array.Empty<ActEnvironment>();

        readonly List<ActEnvironment> dioramas = new();
        TweenCallback<float> stepHandler = null!;
        TweenCallback completeHandler = null!;
        EnvironmentLook current;
        EnvironmentLook from;
        EnvironmentLook to;
        ActEnvironment? shown;
        ActEnvironment? pendingDiorama;
        VfxManager.VisualEffect pendingAmbientEffect;
        Color pendingParticleTint;
        bool veiling;
        Tween? transition;
        VolumeProfile? pendingProfile;
        bool volumeBlending;

        public ArenaView Arena => arena;
        /// <summary>0-based act index being shown, TitleActIndex for the Title look, NoActIndex before the first apply.</summary>
        public int CurrentActIndex { get; private set; } = NoActIndex;
        public bool IsTransitioning => transition != null;

        #region Public Methods

        public void Initialize(ArenaConfig arenaConfig)
        {
            stepHandler = HandleTransitionStep;
            completeHandler = HandleTransitionComplete;
            arena.Initialize(arenaConfig);
            dioramas.Clear();
            foreach (var environment in environments)
            {
                environment.Initialize(config);
                environment.SetVisible(false);
                dioramas.Add(environment);
            }

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = config.FogMode;
            ResetBlendVolume();
            current = EnvironmentLook.From(config.TitleLighting);
            ApplyLook(current);
        }

        /// <summary>Title variant: the backdrop act's diorama under the cozy EnvironmentConfig.TitleLighting look and Volume_Title.</summary>
        public void ApplyTitle(ActDefinition backdrop, bool instant = false)
        {
            if (CurrentActIndex == TitleActIndex) return;
            CurrentActIndex = TitleActIndex;
            arena.SetDangerLevel(0f);
            Show(backdrop, config.TitleLighting, config.TitleVolumeProfile, instant ? 0f : config.TitleTransitionSeconds);
        }

        /// <summary>Shows the act's diorama and blends to its lighting preset and volume profile. No-op when the act is already shown.</summary>
        public void ApplyAct(ActDefinition act, bool instant = false)
        {
            var actIndex = act.Rules.actIndex;
            if (CurrentActIndex == actIndex) return;
            CurrentActIndex = actIndex;
            Show(act, act.Lighting, act.VolumeProfile, instant ? 0f : config.ActTransitionSeconds);
        }

        #endregion

        #region Transition

        // A diorama change during a timed transition hides behind a fog veil (EnvironmentConfig.swapVeilFogDensity) that
        // peaks at the half-way point, where the dioramas swap; otherwise the look simply blends.
        void Show(ActDefinition act, ActLightingPreset lighting, VolumeProfile? profile, float seconds)
        {
            var diorama = Diorama(act);
            if (transition != null) transition.Kill();
            transition = null;
            SwapPendingDiorama();
            CompleteVolumeBlend();
            from = current;
            to = EnvironmentLook.From(lighting);
            BeginVolumeBlend(profile);
            veiling = false;
            if (shown != diorama)
            {
                pendingDiorama = diorama;
                pendingAmbientEffect = act.AmbientEffect;
                pendingParticleTint = lighting.particleTint;
                veiling = seconds > 0f && shown != null;
                if (!veiling) SwapPendingDiorama();
            }
            else
            {
                diorama.ShowAmbient(act.AmbientEffect, lighting.particleTint);
            }

            if (seconds <= 0f)
            {
                HandleTransitionStep(1f);
                HandleTransitionComplete();
                return;
            }

            transition = DOVirtual.Float(0f, 1f, seconds, stepHandler)
                .SetEase(config.TransitionEase)
                .SetUpdate(true)
                .SetRecyclable(false)
                .SetLink(gameObject)
                .OnComplete(completeHandler);
        }

        void HandleTransitionStep(float t)
        {
            current = EnvironmentLook.Lerp(from, to, t);
            if (veiling)
            {
                // Additive god rays ignore fog: fade them out with the veil.
                var veil = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
                current.fogDensity += config.SwapVeilFogDensity * veil;
                current.godRayIntensity *= 1f - veil;
                if (t >= 0.5f) SwapPendingDiorama();
            }

            ApplyLook(current);
            if (volumeBlending) blendVolume.weight = Mathf.Clamp01(t);
        }

        void HandleTransitionComplete()
        {
            current = to;
            veiling = false;
            SwapPendingDiorama();
            ApplyLook(current);
            CompleteVolumeBlend();
            transition = null;
        }

        // Shows the pending diorama with the act's surfaces, ambient particles and the look on screen (never untinted).
        void SwapPendingDiorama()
        {
            if (pendingDiorama == null) return;
            if (shown != null) shown.SetVisible(false);
            shown = pendingDiorama;
            pendingDiorama = null;
            shown.SetVisible(true);
            arena.ApplySurfaces(shown.ArenaSurfaces);
            shown.ShowAmbient(pendingAmbientEffect, pendingParticleTint);
            ApplyLook(current);
        }

        void ApplyLook(in EnvironmentLook look)
        {
            sun.color = look.sunColor;
            sun.intensity = look.sunIntensity;
            sun.shadowStrength = look.shadowStrength;
            sun.transform.rotation = arena.transform.rotation * look.sunRotation;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            RenderSettings.fogColor = look.fogColor;
            RenderSettings.fogDensity = look.fogDensity;
            Shader.SetGlobalColor(WorldRimColorId, look.rimColor);
            if (shown != null) shown.ApplyAccents(look.lightTint, look.lightIntensity, look.godRayColor, look.godRayIntensity);
        }

        #endregion

        #region Volumes

        void ResetBlendVolume()
        {
            blendVolume.isGlobal = true;
            blendVolume.weight = 0f;
            blendVolume.enabled = false;
        }

        // The blend volume sits one priority above the rig's world volume (and below its feature-override volume), so
        // its weight fades the grade from the rig's current profile to the next one; the rig then takes the profile.
        // A missing profile keeps the current grade.
        void BeginVolumeBlend(VolumeProfile? profile)
        {
            if (profile == null || profile == cameraRig.WorldVolume.sharedProfile) return;
            pendingProfile = profile;
            blendVolume.sharedProfile = profile;
            blendVolume.priority = cameraRig.WorldVolume.priority + 1f;
            blendVolume.weight = 0f;
            blendVolume.enabled = true;
            volumeBlending = true;
        }

        void CompleteVolumeBlend()
        {
            if (!volumeBlending) return;
            cameraRig.SetVolumeProfile(pendingProfile!);
            pendingProfile = null;
            ResetBlendVolume();
            volumeBlending = false;
        }

        #endregion

        #region Dioramas

        ActEnvironment Diorama(ActDefinition act)
        {
            var actIndex = act.Rules.actIndex;
            foreach (var diorama in dioramas)
            {
                if (diorama.ActIndex == actIndex) return diorama;
            }

            var arenaTransform = arena.transform;
            var instance = Instantiate(act.EnvironmentPrefab, arenaTransform.position, arenaTransform.rotation, arenaTransform.parent);
            var created = instance.GetComponent<ActEnvironment>();
            created.Initialize(config);
            created.SetVisible(false);
            dioramas.Add(created);
            return created;
        }

        #endregion
    }
}
