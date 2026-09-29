#nullable enable

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds Assets/Prefabs/BilliardRogue/World/WorldCameraRig.prefab (TDD §17): WorldCameraRig root + PixelWorldDisplay,
    /// CameraPivot (pose from ArenaConfig) → WorldCamera (Base, World layer only, post on, WorldVolume mask, World
    /// renderer, never in a CameraChainItem), WorldVolume + LowTierVolume + FeatureOverrideVolume (layer WorldVolume),
    /// the WorldDisplayCanvas (Screen Space-Camera, plane distance 295; the UI camera is assigned at runtime by Flow) and
    /// the development-build FrameTimingLogger.
    /// </summary>
    public static class WorldCameraRigBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/BilliardRogue/World/WorldCameraRig.prefab";
        const string VisualConfigPath = BuilderAssets.ConfigRoot + "/HD2DVisualConfig.asset";
        const string ArenaConfigPath = BuilderAssets.ConfigRoot + "/ArenaConfig.asset";
        const float DisplayPlaneDistance = 295f;
        static readonly Vector3 DefaultCameraPosition = new(0f, 20.4f, -6.15f);
        const float DefaultPitch = 58f;
        const float DefaultFov = 28f;
        static readonly Color ClearColor = new(0.05f, 0.05f, 0.09f, 1f);

        [MenuItem("Nex/Billiard Rogue/World Camera Rig", priority = 33)]
        static void Menu() => Debug.Log(Run());

        #region Public Methods

        public static string Run()
        {
            var report = new StringBuilder("[WorldCameraRigBuilder]");
            var staging = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Build(report);
                SceneManager.MoveGameObjectToScene(root, staging);
                BuilderAssets.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath)!.Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(staging);
            }

            AssetDatabase.SaveAssets();
            report.Append($" saved {PrefabPath}");
            return report.ToString();
        }

        #endregion

        #region Build

        static GameObject Build(StringBuilder report)
        {
            var worldLayer = LayerMask.NameToLayer(RenderPipelineBuilder.WorldLayerName);
            var volumeLayer = LayerMask.NameToLayer(RenderPipelineBuilder.WorldVolumeLayerName);
            if (worldLayer < 0 || volumeLayer < 0)
            {
                Debug.LogWarning("[WorldCameraRigBuilder] World / WorldVolume layers missing (run RenderPipelineBuilder first); the camera culls everything.");
            }

            var root = new GameObject("WorldCameraRig", typeof(WorldCameraRig), typeof(PixelWorldDisplay), typeof(FrameTimingLogger));
            if (worldLayer >= 0) root.layer = worldLayer;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            var arena = AssetDatabase.LoadAssetAtPath<ArenaConfig>(ArenaConfigPath);
            var fov = DefaultFov;
            if (arena != null)
            {
                pivot.localPosition = arena.CameraPosition;
                pivot.localRotation = Quaternion.Euler(arena.CameraPitchDeg, 0f, 0f);
                fov = arena.CameraFov;
            }
            else
            {
                Debug.LogWarning("[WorldCameraRigBuilder] ArenaConfig.asset missing; using the default camera pose.");
                pivot.localPosition = DefaultCameraPosition;
                pivot.localRotation = Quaternion.Euler(DefaultPitch, 0f, 0f);
            }

            var camera = BuildCamera(pivot, worldLayer, volumeLayer, fov, report);
            var worldVolume = BuildVolume(root.transform, "WorldVolume", volumeLayer, 0f, VolumeProfilesBuilder.DefaultProfilePath);
            // Between the act looks (0, grade blend 1) and the feature switches (100): only its cost parameters override.
            var lowTierVolume = BuildVolume(root.transform, "LowTierVolume", volumeLayer, 50f, VolumeProfilesBuilder.LowTierPath);
            lowTierVolume.enabled = false;
            var overrideVolume = BuildVolume(root.transform, "FeatureOverrideVolume", volumeLayer, 100f, VolumeProfilesBuilder.FeatureOverridesPath);
            var canvas = BuildDisplayCanvas(root.transform, out var display);

            var rig = root.GetComponent<WorldCameraRig>();
            var so = new SerializedObject(rig);
            so.FindProperty("worldCamera").objectReferenceValue = camera;
            so.FindProperty("cameraPivot").objectReferenceValue = pivot;
            so.FindProperty("worldVolume").objectReferenceValue = worldVolume;
            so.FindProperty("featureOverrideVolume").objectReferenceValue = overrideVolume;
            so.FindProperty("lowTierVolume").objectReferenceValue = lowTierVolume;
            so.FindProperty("displayCanvas").objectReferenceValue = canvas;
            so.FindProperty("display").objectReferenceValue = display;
            so.FindProperty("pixelDisplay").objectReferenceValue = root.GetComponent<PixelWorldDisplay>();
            so.FindProperty("displayPlaneDistance").floatValue = DisplayPlaneDistance;
            var visual = AssetDatabase.LoadAssetAtPath<HD2DVisualConfig>(VisualConfigPath);
            if (visual == null) Debug.LogWarning("[WorldCameraRigBuilder] HD2DVisualConfig.asset missing (ConfigAssetsBuilder); config slot left empty.");
            so.FindProperty("config").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();

            var logger = new SerializedObject(root.GetComponent<FrameTimingLogger>());
            logger.FindProperty("rig").objectReferenceValue = rig;
            logger.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static Camera BuildCamera(Transform pivot, int worldLayer, int volumeLayer, float fov, StringBuilder report)
        {
            var go = new GameObject("WorldCamera");
            go.transform.SetParent(pivot, false);
            if (worldLayer >= 0) go.layer = worldLayer;
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ClearColor;
            camera.cullingMask = worldLayer >= 0 ? 1 << worldLayer : 0;
            camera.depth = -2f;
            camera.fieldOfView = fov;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.volumeLayerMask = volumeLayer >= 0 ? 1 << volumeLayer : 0;
            data.renderShadows = true;
            data.stopNaN = false;
            data.dithering = false;
            var rendererIndex = RenderPipelineBuilder.WorldRendererIndex();
            if (rendererIndex >= 0)
            {
                data.SetRenderer(rendererIndex);
                report.Append($" renderer={rendererIndex}");
            }
            else
            {
                Debug.LogWarning("[WorldCameraRigBuilder] World renderer not registered (run RenderPipelineBuilder first); WorldCamera uses the default renderer.");
            }

            return camera;
        }

        static Volume BuildVolume(Transform parent, string name, int layer, float priority, string profilePath)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (layer >= 0) go.layer = layer;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 1f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) Debug.LogWarning($"[WorldCameraRigBuilder] {profilePath} missing (VolumeProfilesBuilder); {name} has no profile.");
            volume.sharedProfile = profile;
            return volume;
        }

        static Canvas BuildDisplayCanvas(Transform parent, out RawImage display)
        {
            var uiLayer = LayerMask.NameToLayer("UI");
            var canvasGo = new GameObject("WorldDisplayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.layer = uiLayer;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = DisplayPlaneDistance;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var imageGo = new GameObject("WorldDisplay", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(canvasGo.transform, false);
            imageGo.layer = uiLayer;
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            display = imageGo.GetComponent<RawImage>();
            display.raycastTarget = false;
            display.color = Color.white;
            display.maskable = false;
            return canvas;
        }

        #endregion
    }
}
