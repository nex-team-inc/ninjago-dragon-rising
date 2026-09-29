#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Guards the Mali-G52 budget decisions of the final perf pass (progress/final-perf.md pass 3): the UI renders with one
    // Base camera straight into the backbuffer (no 1080p camera-stack intermediate or final blit), the low-end GPU tier
    // detection and its volume, the player settings the [Perf] logger and the surface blit depend on, and the one-layer
    // overlay backdrop.
    public class RenderingPerfContractTests
    {
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";

        #region UI camera

        [Test]
        public void UiCameraRendersStraightToTheBackbuffer()
        {
            var viewManager = LoadOrIgnore<GameObject>(FlowPrefabsBuilder.ViewManagerPath);
            var camera = viewManager.GetComponentInChildren<Camera>(true);
            var data = camera.GetUniversalAdditionalCameraData();
            Assert.AreEqual(CameraRenderType.Base, data.renderType, "RootCamera is the only screen camera");
            Assert.AreEqual(0, data.cameraStack.Count);
            Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
            Assert.IsFalse(camera.allowHDR, "HDR would render the UI through an intermediate texture");
            Assert.IsFalse(camera.allowMSAA);
            Assert.IsFalse(data.renderPostProcessing);
            Assert.IsFalse(data.renderShadows);
            Assert.AreEqual(CameraOverrideOption.Off, data.requiresDepthOption);
            Assert.AreEqual(CameraOverrideOption.Off, data.requiresColorOption);
            var world = LayerMask.NameToLayer(RenderPipelineBuilder.WorldLayerName);
            if (world >= 0)
            {
                Assert.AreEqual(0, camera.cullingMask & (1 << world), "the UI camera culls the World layer");
            }
        }

        [Test]
        public void MainSceneHasNoSecondScreenCamera()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainSceneBuilder.ScenePath) == null)
            {
                Assert.Ignore("Main.unity not built");
            }

            var scene = EditorSceneManager.OpenPreviewScene(MainSceneBuilder.ScenePath);
            try
            {
                var screenCameras = 0;
                var listeners = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (camera.targetTexture == null && camera.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base
                            && camera.GetComponentInParent<WorldCameraRig>(true) == null)
                        {
                            screenCameras++;
                        }
                    }

                    listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                }

                Assert.AreEqual(1, screenCameras, "only the view manager's RootCamera renders to the screen");
                Assert.AreEqual(1, listeners, "one AudioListener");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void PlayerSettingsServeTheDeviceProfiling()
        {
            Assert.IsTrue(PlayerSettings.enableFrameTimingStats, "FrameTimingManager GPU / CPU times (RenderPipelineBuilder)");
            Assert.AreEqual(AndroidBlitType.Auto, PlayerSettings.Android.blitType, "no extra offscreen-to-surface blit");
        }

        #endregion

        #region Low tier

        [Test]
        public void MaliG52GetsTheLowTier()
        {
            var config = ScriptableObject.CreateInstance<HD2DVisualConfig>();
            try
            {
                Assert.AreEqual(RenderQualityTier.Low, config.DetectTier("Mali-G52", 50), "the Nex Playground GPU");
                Assert.AreEqual(RenderQualityTier.Low, config.DetectTier("ARM mali-g52 MC2", 50), "case-insensitive substring");
                Assert.AreEqual(RenderQualityTier.Low, config.DetectTier("Some GPU", 30), "OpenGL ES 3.0 class shader level");
                Assert.AreEqual(RenderQualityTier.Full, config.DetectTier("Apple M2 Max", 50));
                Assert.AreEqual(RenderQualityTier.Full, config.DetectTier("Adreno (TM) 650", 50));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void LowTierVolumeOnlyOverridesCost()
        {
            var config = LoadOrIgnore<HD2DVisualConfig>(VisualConfigPath);
            var profile = LoadOrIgnore<VolumeProfile>(VolumeProfilesBuilder.LowTierPath);
            Assert.IsTrue(profile.TryGet(out Bloom bloom));
            Assert.IsTrue(bloom.maxIterations.overrideState);
            Assert.AreEqual(config.LowTierBloomMaxIterations, bloom.maxIterations.value);
            Assert.IsFalse(bloom.intensity.overrideState, "each act keeps its bloom look");
            Assert.IsFalse(bloom.threshold.overrideState);
            Assert.IsTrue(profile.TryGet(out TiltShiftVolume tiltShift));
            Assert.IsTrue(tiltShift.sampleCount.overrideState);
            Assert.AreEqual(config.LowTierTiltShiftSampleCount, tiltShift.sampleCount.value);
            Assert.IsFalse(tiltShift.intensity.overrideState);

            var rig = LoadOrIgnore<GameObject>(WorldCameraRigBuilder.PrefabPath);
            var volume = new SerializedObject(rig.GetComponent<WorldCameraRig>()).FindProperty("lowTierVolume").objectReferenceValue as Volume;
            Assert.IsNotNull(volume, "rig wires LowTierVolume");
            Assert.AreSame(profile, volume!.sharedProfile);
            Assert.IsFalse(volume.enabled, "enabled at runtime in the low tier only");
            Assert.Greater(volume.priority, 1f, "above the act looks");
            Assert.Less(volume.priority, 100f, "below the feature switches");
            var logger = rig.GetComponent<FrameTimingLogger>();
            Assert.IsNotNull(logger, "the [Perf] logger rides on the rig");
            Assert.IsNotNull(new SerializedObject(logger).FindProperty("rig").objectReferenceValue);
        }

        #endregion

        #region Overlays

        [Test]
        public void OverlaysDimWithOneFullScreenLayer()
        {
            var reward = LoadOrIgnore<GameObject>(UiViewsBuilder.ViewPath("RewardView"));
            var content = reward.transform.Find("Content");
            Assert.IsNotNull(content.Find("DimVignette"), "composed backdrop (UiOverlaySpriteComposer)");
            Assert.IsNull(content.Find("Dim"), "no second full-screen layer");
            Assert.IsNull(content.Find("Vignette"));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiOverlaySpriteComposer.ComposedPath);
            Assert.IsNotNull(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(UiOverlaySpriteComposer.ComposedPath);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode, "smooth alpha, like the vignette it is made from");
        }

        #endregion

        #region Helpers

        static T LoadOrIgnore<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Assert.Ignore($"{path} not built (run Build All)");
            }

            return asset!;
        }

        #endregion
    }
}
