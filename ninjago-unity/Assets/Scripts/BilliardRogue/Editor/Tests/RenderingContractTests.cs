#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Guards the Rendering outputs: the TDD §16 shader / material contract, the URP asset and World renderer tuned by
    // RenderPipelineBuilder, the World / WorldVolume layers, the volume profiles, WorldCameraRig.prefab wiring and
    // PixelWorldDisplay's target and projection (exercised in a preview scene, so the open scene is never touched).
    public class RenderingContractTests
    {
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";
        const string ToonLit = "BilliardRogue/ToonLit";

        static readonly (string shader, string[] properties)[] ShaderContract =
        {
            (ToonLit, new[]
            {
                "_BaseMap", "_BaseColor", "_EmissionMap", "_EmissionColor", "_EmissionStrength", "_BumpMap", "_BumpScale", "_CavityMap",
                "_CavityStrength", "_Bands", "_ShadowTint", "_RimColor", "_RimPower", "_FlashColor", "_FlashAmount", "_StatusTint", "_Tiling",
            }),
            ("BilliardRogue/ToonLitTransparent", new[] { "_BaseMap", "_BaseColor", "_EmissionColor", "_EmissionStrength", "_Bands", "_ShadowTint", "_FlashColor", "_FlashAmount", "_StatusTint", "_Alpha" }),
            ("BilliardRogue/LitParticle", new[] { "_BaseMap", "_BaseColor", "_Cutoff", "_LightInfluence", "_EmissionStrength" }),
            ("BilliardRogue/GlowParticle", new[] { "_BaseMap", "_BaseColor", "_Intensity" }),
            ("BilliardRogue/LightShaft", new[] { "_Color", "_Intensity", "_NoiseTex", "_NoiseScroll", "_EdgeSoftness", "_FadeDistance" }),
            ("BilliardRogue/AimGuide", new[] { "_DashTex", "_Color", "_ScrollSpeed", "_FadeStart", "_FadeEnd" }),
            ("BilliardRogue/TiltShiftBlur", new string[0]),
        };

        static readonly (string material, string shader)[] MaterialContract =
        {
            ("M_Palette", ToonLit), ("M_Palette_CatP2", ToonLit), ("M_Palette_Ghost", "BilliardRogue/ToonLitTransparent"),
            ("M_Surface_StoneFloor", ToonLit), ("M_Ball_Basic", ToonLit), ("M_Ball_Flame", ToonLit), ("M_DangerTile", ToonLit),
            ("M_LightShaft", "BilliardRogue/LightShaft"), ("M_AimGuide", "BilliardRogue/AimGuide"),
            ("M_GlowParticle_Default", "BilliardRogue/GlowParticle"), ("M_LitParticle_Default", "BilliardRogue/LitParticle"),
        };

        #region Shaders & Materials

        [Test]
        public void ShadersCompileAndExposeContractProperties()
        {
            foreach (var (name, properties) in ShaderContract)
            {
                var shader = Shader.Find(name);
                Assert.IsNotNull(shader, name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{name} has compile errors");
                foreach (var property in properties)
                {
                    Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, $"{name} lacks {property}");
                }
            }
        }

        [Test]
        public void MaterialsUseContractShaders()
        {
            foreach (var (name, shader) in MaterialContract)
            {
                var material = LoadOrIgnore<Material>($"{MaterialsBuilder.MaterialRoot}/{name}.mat");
                Assert.AreEqual(shader, material.shader.name, name);
            }

            var surface = LoadOrIgnore<Material>($"{MaterialsBuilder.MaterialRoot}/M_Surface_StoneFloor.mat");
            Assert.Greater(surface.GetFloat("_Tiling"), 0f, "surfaces tile by surfaces.json");
            Assert.IsNotNull(surface.GetTexture("_BaseMap"), "StoneFloor albedo");
            var ball = LoadOrIgnore<Material>($"{MaterialsBuilder.MaterialRoot}/M_Ball_Flame.mat");
            Assert.Greater(ball.GetColor("_EmissionColor").maxColorComponent, 0f, "ball glow");
            Assert.Less(ball.GetColor("_EmissionColor").maxColorComponent, 1.5f, "resting glow keeps the ball colour readable");
        }

        #endregion

        #region Pipeline

        [Test]
        public void PipelineAssetIsTunedForAndroidTv()
        {
            var asset = LoadOrIgnore<UniversalRenderPipelineAsset>(RenderPipelineBuilder.UrpAssetPath);
            var config = LoadOrIgnore<HD2DVisualConfig>(VisualConfigPath);
            Assert.IsTrue(asset.supportsHDR, "HDR for bloom");
            Assert.AreEqual(1, asset.msaaSampleCount, "MSAA off");
            Assert.AreEqual(LightRenderingMode.PerPixel, asset.additionalLightsRenderingMode);
            Assert.AreEqual(4, asset.maxAdditionalLightsCount);
            Assert.IsTrue(asset.supportsMainLightShadows);
            Assert.AreEqual(1024, asset.mainLightShadowmapResolution);
            Assert.AreEqual(1, asset.shadowCascadeCount);
            Assert.AreEqual(config.ShadowDistance, asset.shadowDistance, 0.01f);
            Assert.IsTrue(asset.useSRPBatcher);
            Assert.IsFalse(asset.supportsCameraDepthTexture, "tilt-shift needs no depth texture");
            Assert.Greater(RenderPipelineBuilder.WorldRendererIndex(), 0, "dedicated World renderer beside the default UI renderer");
        }

        [Test]
        public void WorldRendererCarriesTheTiltShiftFeature()
        {
            var world = LoadOrIgnore<UniversalRendererData>(RenderPipelineBuilder.WorldRendererPath);
            var ui = LoadOrIgnore<UniversalRendererData>(RenderPipelineBuilder.DefaultRendererPath);
            Assert.AreEqual(1, CountTiltShift(world), "World renderer has exactly one TiltShiftFeature");
            Assert.AreEqual(0, CountTiltShift(ui), "UI renderer never runs the tilt-shift");
            Assert.AreEqual(RenderingMode.Forward, world.renderingMode);
            Assert.IsFalse(world.shadowTransparentReceive);
        }

        [Test]
        public void WorldLayersExist()
        {
            var world = LayerMask.NameToLayer(RenderPipelineBuilder.WorldLayerName);
            var volume = LayerMask.NameToLayer(RenderPipelineBuilder.WorldVolumeLayerName);
            if (world < 0 || volume < 0) Assert.Ignore("layers not built (run RenderPipelineBuilder)");
            Assert.GreaterOrEqual(world, 8, "user layer");
            Assert.GreaterOrEqual(volume, 8, "user layer");
            Assert.AreNotEqual(world, volume);
        }

        [Test]
        public void VolumeProfilesCarryBloomAndTiltShift()
        {
            foreach (var name in new[] { "Default", "Title", "Act1", "Act2", "Act3" })
            {
                var profile = LoadOrIgnore<VolumeProfile>($"{VolumeProfilesBuilder.VolumeRoot}/Volume_{name}.asset");
                Assert.IsTrue(profile.TryGet(out Bloom bloom), $"{name} bloom");
                Assert.Greater(bloom.intensity.value, 0f, $"{name} bloom intensity");
                Assert.GreaterOrEqual(bloom.threshold.value, 0.9f, $"{name} bloom only from HDR emissives");
                Assert.IsTrue(profile.TryGet(out TiltShiftVolume tiltShift), $"{name} tilt-shift");
                Assert.Greater(tiltShift.intensity.value, 0f, $"{name} tilt-shift intensity");
                Assert.IsTrue(profile.TryGet(out Tonemapping _), $"{name} tonemapping");
            }

            var overrides = LoadOrIgnore<VolumeProfile>(VolumeProfilesBuilder.FeatureOverridesPath);
            Assert.IsTrue(overrides.TryGet(out Bloom bloomOff));
            Assert.IsTrue(bloomOff.intensity.overrideState && bloomOff.intensity.value == 0f, "bloom switch");
            Assert.IsFalse(bloomOff.threshold.overrideState, "only the intensity overrides");
            Assert.IsTrue(overrides.TryGet(out TiltShiftVolume tiltOff));
            Assert.IsTrue(tiltOff.intensity.overrideState && tiltOff.intensity.value == 0f, "tilt-shift switch");
        }

        #endregion

        #region Rig

        [Test]
        public void RigPrefabRendersOnlyTheWorldLayer()
        {
            var prefab = LoadOrIgnore<GameObject>(WorldCameraRigBuilder.PrefabPath);
            var world = LayerMask.NameToLayer(RenderPipelineBuilder.WorldLayerName);
            var volume = LayerMask.NameToLayer(RenderPipelineBuilder.WorldVolumeLayerName);
            var rig = prefab.GetComponent<WorldCameraRig>();
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.Display);
            Assert.IsNotNull(rig.CameraPivot);
            var camera = rig.WorldCamera;
            Assert.AreEqual(1 << world, camera.cullingMask, "World layer only");
            Assert.IsTrue(camera.allowHDR);
            var data = camera.GetUniversalAdditionalCameraData();
            Assert.AreEqual(CameraRenderType.Base, data.renderType);
            Assert.IsTrue(data.renderPostProcessing);
            Assert.AreEqual(1 << volume, data.volumeLayerMask.value, "WorldVolume mask");
            Assert.AreEqual(RenderPipelineBuilder.WorldRendererIndex(), new SerializedObject(data).FindProperty("m_RendererIndex").intValue);
            foreach (var component in camera.GetComponents<Component>())
            {
                Assert.AreNotEqual("CameraChainItem", component.GetType().Name, "the world camera stays outside the UI camera chain");
            }

            Assert.IsNotNull(rig.WorldVolume.sharedProfile, "default profile");
            Assert.IsNotNull(prefab.GetComponentInChildren<Canvas>(true), "display canvas");
        }

        [Test]
        public void PixelWorldDisplayPixelatesAndProjects()
        {
            var prefab = LoadOrIgnore<GameObject>(WorldCameraRigBuilder.PrefabPath);
            var config = LoadOrIgnore<HD2DVisualConfig>(VisualConfigPath);
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject? rigGo = null;
            GameObject? uiGo = null;
            try
            {
                uiGo = new GameObject("UiCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(uiGo, scene);
                rigGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var rig = rigGo.GetComponent<WorldCameraRig>();
                rig.Initialize(uiGo.GetComponent<Camera>());
                var display = rig.Display;
                var camera = rig.WorldCamera;
                var target = display.Target;
                var margin = config.MarginTexels;
                Assert.AreEqual(config.RenderResolution.x + 2 * margin, target.width, "low-res target width + margin");
                Assert.AreEqual(config.RenderResolution.y + 2 * margin, target.height, "low-res target height + margin");
                Assert.AreEqual(FilterMode.Point, target.filterMode);
                Assert.AreSame(target, camera.targetTexture);
                var pixelatedFov = camera.fieldOfView;

                var canvasRect = (RectTransform)rigGo.GetComponentInChildren<Canvas>(true).transform;
                var centre = camera.transform.position + camera.transform.forward * 10f;
                Assert.IsTrue(display.TryWorldToCanvas(centre, canvasRect, out var anchored));
                Assert.AreEqual(0f, anchored.x, 1f, "view axis maps to the canvas centre");
                Assert.AreEqual(0f, anchored.y, 1f);
                Assert.Greater(display.WorldToScreenNormalized(centre + camera.transform.right * 2f).x, 0.5f);
                Assert.Less(display.WorldToScreenNormalized(centre - camera.transform.right * 2f).x, 0.5f);
                Assert.IsFalse(display.TryWorldToCanvas(camera.transform.position - camera.transform.forward * 5f, canvasRect, out _), "behind the camera");

                display.SetPixelationEnabled(false);
                Assert.IsFalse(display.PixelationEnabled);
                Assert.AreEqual(FilterMode.Bilinear, display.Target.filterMode, "native target is filtered");
                Assert.GreaterOrEqual(display.Target.height, config.RenderResolution.y);
                Assert.Less(camera.fieldOfView, pixelatedFov, "no margin texels without pixelation");
                display.SetPixelationEnabled(true);
                Assert.AreEqual(FilterMode.Point, display.Target.filterMode);
            }
            finally
            {
                if (rigGo != null) Object.DestroyImmediate(rigGo);
                if (uiGo != null) Object.DestroyImmediate(uiGo);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        #endregion

        #region Helpers

        static int CountTiltShift(ScriptableRendererData renderer)
        {
            var count = 0;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is TiltShiftFeature && feature.isActive) count++;
            }

            return count;
        }

        static T LoadOrIgnore<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Assert.Ignore($"{path} not built (run the Rendering builders)");
            return asset!;
        }

        #endregion
    }
}
