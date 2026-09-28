#nullable enable

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the world post-process profiles (TDD §17) into Assets/Settings/BilliardRogue/Volumes: Volume_Default,
    /// Volume_Title (golden hour), Volume_Act1 (golden hour ruins), Volume_Act2 (deep blue night), Volume_Act3
    /// (violet-cyan magic) and Volume_FeatureOverrides (bloom / tilt-shift off switches used by WorldCameraRig).
    /// Base bloom / vignette / tilt-shift values come from HD2DVisualConfig; the act moods are authored here.
    /// Fills ActDefinition.volumeProfile when the slot is still empty.
    /// </summary>
    public static class VolumeProfilesBuilder
    {
        public const string VolumeRoot = RenderPipelineBuilder.SettingsRoot + "/Volumes";
        public const string DefaultProfilePath = VolumeRoot + "/Volume_Default.asset";
        public const string FeatureOverridesPath = VolumeRoot + "/Volume_FeatureOverrides.asset";
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";
        const string ActConfigFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";

        sealed class Look
        {
            public float bloomIntensity = 1f;
            public float bloomThreshold = 0.9f;
            public float bloomScatter = 0.7f;
            public Color bloomTint = Color.white;
            public float contrast = 10f;
            public float saturation = 10f;
            public float postExposure;
            public float temperature;
            public float tint;
            public Color splitShadows = Color.grey;
            public Color splitHighlights = Color.grey;
            public float splitBalance;
            public Vector4 lift = new(1f, 1f, 1f, 0f);
            public Vector4 gamma = new(1f, 1f, 1f, 0f);
            public Vector4 gain = new(1f, 1f, 1f, 0f);
            public float vignette = 0.25f;
            public Color vignetteColor = Color.black;
            public float tiltShiftScale = 1f;
        }

        [MenuItem("Nex/Billiard Rogue/Volume Profiles", priority = 32)]
        static void Menu() => Debug.Log(Run());

        #region Public Methods

        public static string Run()
        {
            var report = new StringBuilder("[VolumeProfilesBuilder]");
            BuilderAssets.EnsureFolder(VolumeRoot);
            var visual = AssetDatabase.LoadAssetAtPath<HD2DVisualConfig>(VisualConfigPath);
            if (visual == null)
            {
                Debug.LogWarning("[VolumeProfilesBuilder] HD2DVisualConfig.asset missing (ConfigAssetsBuilder); using a transient default config.");
                visual = ScriptableObject.CreateInstance<HD2DVisualConfig>();
            }

            var defaultProfile = BuildLook(DefaultProfilePath, visual, new Look(), report);
            BuildLook(VolumeRoot + "/Volume_Title.asset", visual, GoldenHour(1.2f, 0.3f), report);
            var acts = new[]
            {
                BuildLook(VolumeRoot + "/Volume_Act1.asset", visual, GoldenHour(1f, 0.25f), report),
                BuildLook(VolumeRoot + "/Volume_Act2.asset", visual, DeepBlueNight(), report),
                BuildLook(VolumeRoot + "/Volume_Act3.asset", visual, VioletCyanMagic(), report),
            };
            BuildFeatureOverrides(report);
            FillActProfiles(acts, defaultProfile, report);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        #endregion

        #region Looks

        static Look GoldenHour(float bloom, float vignette) => new()
        {
            bloomIntensity = bloom,
            bloomTint = new Color(1f, 0.95f, 0.85f),
            contrast = 12f,
            saturation = 14f,
            temperature = 12f,
            tint = 2f,
            splitShadows = new Color(0.3f, 0.25f, 0.45f),
            splitHighlights = new Color(1f, 0.85f, 0.55f),
            splitBalance = 8f,
            vignette = vignette,
            vignetteColor = new Color(0.12f, 0.06f, 0.04f),
        };

        static Look DeepBlueNight() => new()
        {
            bloomIntensity = 1.6f,
            bloomThreshold = 0.8f,
            bloomScatter = 0.75f,
            bloomTint = new Color(0.8f, 0.9f, 1f),
            contrast = 16f,
            saturation = -4f,
            postExposure = -0.15f,
            temperature = -28f,
            tint = 6f,
            splitShadows = new Color(0.12f, 0.18f, 0.5f),
            splitHighlights = new Color(0.8f, 0.86f, 1f),
            splitBalance = -10f,
            lift = new Vector4(0.92f, 0.95f, 1.12f, 0f),
            gain = new Vector4(0.95f, 1f, 1.08f, 0f),
            vignette = 0.38f,
            vignetteColor = new Color(0.01f, 0.02f, 0.08f),
        };

        static Look VioletCyanMagic() => new()
        {
            bloomIntensity = 2f,
            bloomThreshold = 0.85f,
            bloomScatter = 0.8f,
            bloomTint = new Color(0.85f, 0.8f, 1f),
            contrast = 12f,
            saturation = 26f,
            tint = 18f,
            splitShadows = new Color(0.38f, 0.14f, 0.6f),
            splitHighlights = new Color(0.5f, 1f, 1f),
            splitBalance = -6f,
            gamma = new Vector4(1.02f, 0.98f, 1.06f, 0f),
            vignette = 0.32f,
            vignetteColor = new Color(0.08f, 0.02f, 0.14f),
            tiltShiftScale = 1.1f,
        };

        #endregion

        #region Profiles

        static VolumeProfile BuildLook(string path, HD2DVisualConfig visual, Look look, StringBuilder report)
        {
            var profile = BuilderAssets.LoadOrCreate<VolumeProfile>(path, out var created);

            var bloom = Component<Bloom>(profile);
            bloom.threshold.value = look.bloomThreshold * (visual.BloomThreshold / 0.9f);
            bloom.intensity.value = look.bloomIntensity * (visual.BloomIntensity / 0.8f);
            bloom.scatter.value = Mathf.Clamp01(look.bloomScatter * (visual.BloomScatter / 0.7f));
            bloom.tint.value = look.bloomTint;
            bloom.filter.value = BloomFilterMode.Dual;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = visual.BloomMaxIterations;
            bloom.highQualityFiltering.value = false;

            var tonemapping = Component<Tonemapping>(profile);
            tonemapping.mode.value = TonemappingMode.Neutral;

            var adjustments = Component<ColorAdjustments>(profile);
            adjustments.contrast.value = look.contrast;
            adjustments.saturation.value = look.saturation;
            adjustments.postExposure.value = look.postExposure;

            var whiteBalance = Component<WhiteBalance>(profile);
            whiteBalance.temperature.value = look.temperature;
            whiteBalance.tint.value = look.tint;

            var splitToning = Component<SplitToning>(profile);
            splitToning.shadows.value = look.splitShadows;
            splitToning.highlights.value = look.splitHighlights;
            splitToning.balance.value = look.splitBalance;

            var liftGammaGain = Component<LiftGammaGain>(profile);
            liftGammaGain.lift.value = look.lift;
            liftGammaGain.gamma.value = look.gamma;
            liftGammaGain.gain.value = look.gain;

            var vignette = Component<Vignette>(profile);
            vignette.intensity.value = look.vignette * (visual.VignetteIntensity / 0.25f);
            vignette.smoothness.value = 0.4f;
            vignette.color.value = look.vignetteColor;
            vignette.rounded.value = false;

            var tiltShift = Component<TiltShiftVolume>(profile);
            tiltShift.intensity.value = Mathf.Clamp01(visual.TiltShiftIntensity * look.tiltShiftScale);
            tiltShift.center.value = visual.TiltShiftCenter;
            tiltShift.bandWidth.value = visual.TiltShiftHalfWidth;
            tiltShift.falloff.value = visual.TiltShiftFalloff;
            tiltShift.maxBlur.value = visual.TiltShiftMaxBlur;
            tiltShift.sampleCount.value = visual.TiltShiftSampleCount;

            EditorUtility.SetDirty(profile);
            report.Append(created ? $" created {profile.name}" : $" updated {profile.name}");
            return profile;
        }

        // Only the intensity parameters override, so the rig can switch a feature off without touching anything else.
        static void BuildFeatureOverrides(StringBuilder report)
        {
            var profile = BuilderAssets.LoadOrCreate<VolumeProfile>(FeatureOverridesPath, out var created);
            var bloom = Component<Bloom>(profile, overrides: false);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0f;
            var tiltShift = Component<TiltShiftVolume>(profile, overrides: false);
            tiltShift.intensity.overrideState = true;
            tiltShift.intensity.value = 0f;
            EditorUtility.SetDirty(profile);
            report.Append(created ? " created Volume_FeatureOverrides" : " updated Volume_FeatureOverrides");
        }

        static void FillActProfiles(VolumeProfile[] acts, VolumeProfile fallback, StringBuilder report)
        {
            for (var act = 1; act <= 3; act++)
            {
                var path = string.Format(ActConfigFormat, act);
                var definition = AssetDatabase.LoadAssetAtPath<ActDefinition>(path);
                if (definition == null)
                {
                    Debug.LogWarning($"[VolumeProfilesBuilder] {path} missing (ConfigAssetsBuilder); volume profile not assigned.");
                    continue;
                }

                var so = new SerializedObject(definition);
                if (!BuilderAssets.FillIfNull(so, "volumeProfile", acts[act - 1] != null ? acts[act - 1] : fallback)) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                report.Append($" Act_{act}.volumeProfile filled");
            }
        }

        static T Component<T>(VolumeProfile profile, bool overrides = true) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                existing.SetAllOverridesTo(overrides);
                existing.active = true;
                return existing;
            }

            var component = profile.Add<T>(overrides);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        #endregion
    }
}
