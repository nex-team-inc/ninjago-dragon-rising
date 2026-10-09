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
    /// (violet-cyan magic), Volume_FeatureOverrides (bloom / tilt-shift off switches used by WorldCameraRig) and
    /// Volume_LowTier (the weak-GPU tier's lighter bloom and tilt-shift, values from HD2DVisualConfig).
    /// Base bloom / vignette / tilt-shift values come from HD2DVisualConfig; the act moods are authored here.
    /// Fills ActDefinition.volumeProfile when the slot is still empty.
    /// </summary>
    public static class VolumeProfilesBuilder
    {
        public const string VolumeRoot = RenderPipelineBuilder.SettingsRoot + "/Volumes";
        public const string DefaultProfilePath = VolumeRoot + "/Volume_Default.asset";
        public const string FeatureOverridesPath = VolumeRoot + "/Volume_FeatureOverrides.asset";
        public const string LowTierPath = VolumeRoot + "/Volume_LowTier.asset";
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";
        const string ActConfigFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";

        // Bloom values are relative to HD2DVisualConfig (threshold 0.9 / intensity 0.8 / scatter 0.7 = 1×): only pixels
        // above ~1.2 in HDR (emissive palette cells, balls in flight, torches, crystals) may bloom; sunlit albedo, the
        // floor and the scenery must stay crisp (gameplay-view polish: the earlier 0.95 thresholds hazed every act).
        sealed class Look
        {
            public float bloomIntensity = 0.45f;
            public float bloomThreshold = 1.2f;
            public float bloomScatter = 0.6f;
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
            BuildLook(VolumeRoot + "/Volume_Title.asset", visual, GoldenHour(0.6f, 0.3f, title: true), report);
            var acts = new[]
            {
                BuildLook(VolumeRoot + "/Volume_Act1.asset", visual, GoldenHour(0.42f, 0.2f, title: false), report),
                BuildLook(VolumeRoot + "/Volume_Act2.asset", visual, DeepBlueNight(), report),
                BuildLook(VolumeRoot + "/Volume_Act3.asset", visual, VioletCyanMagic(), report),
            };
            BuildFeatureOverrides(report);
            BuildLowTier(visual, report);
            FillActProfiles(acts, defaultProfile, report);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        #endregion

        #region Looks

        // Golden hour without the yellow soup: warm highlights, cool violet shade, contrast up, white balance only a
        // touch warm (the sun colour already carries the hour). The title keeps a richer, dreamier version.
        static Look GoldenHour(float bloom, float vignette, bool title) => new()
        {
            bloomIntensity = bloom,
            bloomThreshold = title ? 1.1f : 1.25f,
            bloomTint = new Color(1f, 0.94f, 0.82f),
            contrast = title ? 12f : 18f,
            saturation = title ? 12f : 6f,
            temperature = title ? 10f : 3f,
            tint = 0f,
            splitShadows = new Color(0.28f, 0.26f, 0.48f),
            splitHighlights = title ? new Color(1f, 0.85f, 0.55f) : new Color(1f, 0.92f, 0.76f),
            splitBalance = title ? 8f : 4f,
            vignette = vignette,
            vignetteColor = new Color(0.1f, 0.06f, 0.05f),
        };

        // Calibrated on the real Act 2 diorama (Presentation-World): the first pass drove the red channel to zero,
        // which drained the warm torch pools and the danger-row inlay; this keeps the night cool but readable, and
        // the bloom now only takes the torches, crystals and balls (threshold 1.2, intensity 0.6).
        static Look DeepBlueNight() => new()
        {
            bloomIntensity = 0.6f,
            bloomThreshold = 1.2f,
            bloomScatter = 0.6f,
            bloomTint = new Color(0.92f, 0.95f, 1f),
            contrast = 20f,
            saturation = -4f,
            postExposure = 0f,
            temperature = -12f,
            tint = 3f,
            splitShadows = new Color(0.3f, 0.36f, 0.72f),
            splitHighlights = new Color(1f, 0.88f, 0.74f),
            splitBalance = -12f,
            lift = new Vector4(0.98f, 0.99f, 1.02f, 0f),
            gain = new Vector4(1f, 1f, 1.02f, 0f),
            vignette = 0.3f,
            vignetteColor = new Color(0.01f, 0.02f, 0.08f),
        };

        // Crystal cave: violet shade with cyan highlights, but far less saturation / tint than the first pass (which
        // turned the whole frame into one purple wash) and bloom limited to the crystals and balls.
        static Look VioletCyanMagic() => new()
        {
            bloomIntensity = 0.55f,
            bloomThreshold = 1.2f,
            bloomScatter = 0.65f,
            bloomTint = new Color(0.88f, 0.86f, 1f),
            contrast = 18f,
            saturation = 10f,
            tint = 8f,
            splitShadows = new Color(0.36f, 0.16f, 0.58f),
            splitHighlights = new Color(0.62f, 1f, 1f),
            splitBalance = -4f,
            gamma = new Vector4(1f, 0.99f, 1.03f, 0f),
            vignette = 0.26f,
            vignetteColor = new Color(0.08f, 0.02f, 0.14f),
            tiltShiftScale = 1f,
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

        // Only the cost parameters override, so every act keeps its own bloom look; the rig enables it in the low tier.
        static void BuildLowTier(HD2DVisualConfig visual, StringBuilder report)
        {
            var profile = BuilderAssets.LoadOrCreate<VolumeProfile>(LowTierPath, out var created);
            var bloom = Component<Bloom>(profile, overrides: false);
            bloom.maxIterations.overrideState = true;
            bloom.maxIterations.value = visual.LowTierBloomMaxIterations;
            bloom.downscale.overrideState = true;
            bloom.downscale.value = visual.LowTierBloomQuarterResolution ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = false;
            var tiltShift = Component<TiltShiftVolume>(profile, overrides: false);
            tiltShift.sampleCount.overrideState = true;
            tiltShift.sampleCount.value = visual.LowTierTiltShiftSampleCount;
            EditorUtility.SetDirty(profile);
            report.Append(created ? " created Volume_LowTier" : " updated Volume_LowTier");
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
