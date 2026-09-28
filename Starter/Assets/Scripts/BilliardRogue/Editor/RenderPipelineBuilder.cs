#nullable enable

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Configures the URP asset and renderers for the HD-2D rig (TDD §0a D2, research/urp-hd2d-rendering.md §2.3, §8):
    /// HDR on, Forward, 4 per-object lights, one 1024 hard-shadow cascade fitted to the arena, no depth/opaque
    /// textures, MSAA off, HDR grading LUT; a dedicated World renderer (renderer list index 1) carrying the
    /// TiltShiftFeature; layers World / WorldVolume; quality levels trimmed for low-end Android. Idempotent.
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string UrpAssetPath = "Assets/URP/URPAsset.asset";
        public const string DefaultRendererPath = "Assets/URP/URPAsset_Renderer.asset";
        public const string SettingsRoot = "Assets/Settings/BilliardRogue";
        public const string WorldRendererPath = SettingsRoot + "/WorldRenderer.asset";
        public const string TiltShiftShaderName = "BilliardRogue/TiltShiftBlur";
        public const string WorldLayerName = "World";
        public const string WorldVolumeLayerName = "WorldVolume";
        const string TagManagerPath = "ProjectSettings/TagManager.asset";
        const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";
        const int FirstUserLayer = 8;
        const float DefaultShadowDistance = 30f;

        [MenuItem("Nex/Billiard Rogue/Render Pipeline", priority = 30)]
        static void Menu() => Debug.Log(Run());

        #region Public Methods

        public static string Run()
        {
            var report = new StringBuilder("[RenderPipelineBuilder]");
            EnsureLayer(WorldLayerName, report);
            EnsureLayer(WorldVolumeLayerName, report);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(DefaultRendererPath);
            ConfigureAsset(asset);
            ConfigureRenderer(defaultRenderer, ~0, "default renderer", report);

            var worldRenderer = BuilderAssets.LoadOrCreate<UniversalRendererData>(WorldRendererPath, out var created);
            if (created)
            {
                worldRenderer.postProcessData = defaultRenderer.postProcessData;
                report.Append(" created WorldRenderer.asset");
            }

            ConfigureRenderer(worldRenderer, LayerMask.GetMask(WorldLayerName), "world renderer", report);
            EnsureTiltShiftFeature(worldRenderer, report);
            var worldIndex = RegisterRenderer(asset, worldRenderer);
            report.Append($" worldRendererIndex={worldIndex}");

            TuneQualityLevels(report);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>Index of WorldRenderer.asset in the URP renderer list, or -1 before Run() has registered it.</summary>
        public static int WorldRendererIndex()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            var worldRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(WorldRendererPath);
            if (asset == null || worldRenderer == null) return -1;
            var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == worldRenderer) return i;
            }

            return -1;
        }

        #endregion

        #region Layers

        static void EnsureLayer(string name, StringBuilder report)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(TagManagerPath)[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = FirstUserLayer; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                report.Append($" layer {name}={i}");
                return;
            }

            Debug.LogError($"[RenderPipelineBuilder] No free user layer for '{name}'.");
        }

        #endregion

        #region Pipeline asset

        static void ConfigureAsset(UniversalRenderPipelineAsset asset)
        {
            var visual = AssetDatabase.LoadAssetAtPath<HD2DVisualConfig>(VisualConfigPath);
            var shadowDistance = visual != null ? visual.ShadowDistance : DefaultShadowDistance;
            if (visual == null) Debug.LogWarning("[RenderPipelineBuilder] HD2DVisualConfig.asset missing (ConfigAssetsBuilder); using default shadow distance.");

            asset.supportsHDR = true;
            asset.hdrColorBufferPrecision = HDRColorBufferPrecision._32Bits;
            asset.msaaSampleCount = 1;
            asset.renderScale = 1f;
            asset.upscalingFilter = UpscalingFilterSelection.Auto;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.mainLightShadowmapResolution = 1024;
            asset.shadowCascadeCount = 1;
            asset.shadowDistance = shadowDistance;
            asset.shadowDepthBias = 1f;
            asset.shadowNormalBias = 1f;
            asset.maxAdditionalLightsCount = 4;
            asset.useSRPBatcher = true;
            asset.supportsDynamicBatching = false;
            asset.colorGradingMode = ColorGradingMode.HighDynamicRange;
            asset.colorGradingLutSize = 32;

            var so = new SerializedObject(asset);
            so.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            so.FindProperty("m_SoftShadowsSupported").boolValue = false;
            so.FindProperty("m_SupportsLightCookies").boolValue = false;
            so.FindProperty("m_SupportsLightLayers").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static int RegisterRenderer(UniversalRenderPipelineAsset asset, UniversalRendererData renderer)
        {
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) return i;
            }

            list.arraySize++;
            var index = list.arraySize - 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return index;
        }

        #endregion

        #region Renderers

        static void ConfigureRenderer(UniversalRendererData renderer, int layerMask, string label, StringBuilder report)
        {
            renderer.renderingMode = RenderingMode.Forward;
            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.copyDepthMode = CopyDepthMode.AfterTransparents;
            renderer.shadowTransparentReceive = false;
            renderer.opaqueLayerMask = layerMask;
            renderer.transparentLayerMask = layerMask;
            EditorUtility.SetDirty(renderer);
            report.Append($" {label} ok");
        }

        // Mirrors ScriptableRendererDataEditor.AddComponent: sub-asset + m_RendererFeatures + m_RendererFeatureMap.
        static void EnsureTiltShiftFeature(UniversalRendererData renderer, StringBuilder report)
        {
            TiltShiftFeature? feature = null;
            foreach (var existing in renderer.rendererFeatures)
            {
                if (existing is TiltShiftFeature found) feature = found;
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<TiltShiftFeature>();
                feature.name = nameof(TiltShiftFeature);
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(renderer);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                report.Append(" added TiltShiftFeature");
            }

            var shader = Shader.Find(TiltShiftShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[RenderPipelineBuilder] Shader '{TiltShiftShaderName}' not found; TiltShiftFeature keeps an empty shader slot.");
            }
            else
            {
                var featureSo = new SerializedObject(feature);
                featureSo.FindProperty("shader").objectReferenceValue = shader;
                featureSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        #endregion

        #region Quality

        // URP owns lighting/shadow quality; here only the built-in per-level costs that still apply are trimmed.
        // Names follow the Unity 6 QualitySettings serialization; a property a later version renames is reported, not fatal.
        static void TuneQualityLevels(StringBuilder report)
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath)[0]);
            var levels = so.FindProperty("m_QualitySettings");
            var missing = new HashSet<string>();
            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                if (Find(level, "antiAliasing", missing) is { } antiAliasing) antiAliasing.intValue = 0;
                if (Find(level, "anisotropicTextures", missing) is { } anisotropic) anisotropic.intValue = 0;
                if (Find(level, "softParticles", missing) is { } softParticles) softParticles.boolValue = false;
                if (Find(level, "realtimeReflectionProbes", missing) is { } probes) probes.boolValue = false;
                if (Find(level, "particleRaycastBudget", missing) is { } raycasts) raycasts.intValue = 64;
                if (Find(level, "billboardsFaceCameraPosition", missing) is { } billboards) billboards.boolValue = true;
                if (Find(level, "resolutionScalingFixedDPIFactor", missing) is { } dpi) dpi.floatValue = 1f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            report.Append($" quality levels tuned={levels.arraySize}");
            if (missing.Count > 0) report.Append($" (unknown quality properties skipped: {string.Join(", ", missing)})");
        }

        static SerializedProperty? Find(SerializedProperty level, string name, HashSet<string> missing)
        {
            var property = level.FindPropertyRelative(name);
            if (property == null) missing.Add(name);
            return property;
        }

        #endregion
    }
}
