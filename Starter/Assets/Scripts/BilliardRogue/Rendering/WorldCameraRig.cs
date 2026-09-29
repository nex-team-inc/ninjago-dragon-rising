#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Root of WorldCameraRig.prefab (TDD §17): the World camera under its pose pivot, the world post-process Volume,
    /// the feature-override Volume and the Screen-Space-Camera canvas that shows the low-res world behind the UI.
    /// Initialize with the UI RootCamera (assigned at runtime by Flow); afterwards act controllers swap the volume
    /// profile and quality / DebugSettings toggles switch bloom, tilt-shift, shadows and pixelation. Weak GPUs get the
    /// low tier (HD2DVisualConfig.DetectTier): the Volume_LowTier volume lightens bloom and tilt-shift.
    /// </summary>
    public sealed class WorldCameraRig : MonoBehaviour
    {
        [Header("Wiring (WorldCameraRigBuilder)")]
        [SerializeField] Camera worldCamera = null!;
        [Tooltip("Carries the designed camera pose; CameraShaker offsets it and PixelWorldDisplay snaps the camera below it.")]
        [SerializeField] Transform cameraPivot = null!;
        [SerializeField] Volume worldVolume = null!;
        [Tooltip("Higher-priority volume whose Bloom / Tilt Shift overrides switch those effects off (quality, debug).")]
        [SerializeField] Volume featureOverrideVolume = null!;
        [Tooltip("Volume_LowTier (bloom mip chain / resolution, tilt-shift taps); enabled only in the low tier.")]
        [SerializeField] Volume lowTierVolume = null!;
        [SerializeField] Canvas displayCanvas = null!;
        [SerializeField] RawImage display = null!;
        [SerializeField] PixelWorldDisplay pixelDisplay = null!;
        [SerializeField] HD2DVisualConfig config = null!;

        [Header("Display")]
        [Tooltip("Canvas plane distance on the UI camera: behind every view (≤ 290) and in front of the starter UIBackground (300).")]
        [SerializeField, Range(200f, 300f)] float displayPlaneDistance = 295f;

        UniversalAdditionalCameraData cameraData = null!;
        Bloom? bloomOff;
        TiltShiftVolume? tiltShiftOff;
        bool qualityBloom = true;
        bool qualityTiltShift = true;
        bool qualityShadows = true;
        RenderQualityTier detectedTier;
        bool initialized;

        public Camera WorldCamera => worldCamera;
        public Transform CameraPivot => cameraPivot;
        public PixelWorldDisplay Display => pixelDisplay;
        public Volume WorldVolume => worldVolume;
        /// <summary>Tier in use (detected from the GPU, or the DebugSettings.renderTier override).</summary>
        public RenderQualityTier Tier { get; private set; }

        #region Life Cycle

        public void Initialize(Camera uiCamera)
        {
            displayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            displayCanvas.worldCamera = uiCamera;
            displayCanvas.planeDistance = displayPlaneDistance;
            cameraData = worldCamera.GetUniversalAdditionalCameraData();
            pixelDisplay.Initialize(worldCamera, display, config);

            // The runtime copy keeps the toggles out of the profile asset.
            var overrides = featureOverrideVolume.profile;
            overrides.TryGet(out bloomOff);
            overrides.TryGet(out tiltShiftOff);

            var quality = pixelDisplay.FindQualityOverride();
            if (quality != null)
            {
                qualityBloom = quality.bloom;
                qualityTiltShift = quality.tiltShift;
                qualityShadows = quality.shadows;
            }

            detectedTier = config.DetectTier(SystemInfo.graphicsDeviceName, SystemInfo.graphicsShaderLevel);
            initialized = true;
            ApplyToggles();
        }

        void LateUpdate()
        {
            if (!initialized) return;
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            ApplyToggles();
#endif
        }

        #endregion

        #region Public Methods

        /// <summary>Per-act grading (ActDefinition.volumeProfile); the rig ships with Volume_Default.</summary>
        public void SetVolumeProfile(VolumeProfile profile)
        {
            worldVolume.sharedProfile = profile;
        }

        /// <summary>Designed pose relative to the rig root (ArenaConfig camera position / pitch / FOV).</summary>
        public void SetPose(Vector3 localPosition, float pitchDeg, float fieldOfView)
        {
            cameraPivot.localPosition = localPosition;
            cameraPivot.localRotation = Quaternion.Euler(pitchDeg, 0f, 0f);
            pixelDisplay.SetBaseFieldOfView(fieldOfView);
        }

        public void SetQualityFeatures(bool bloom, bool tiltShift, bool shadows)
        {
            qualityBloom = bloom;
            qualityTiltShift = tiltShift;
            qualityShadows = shadows;
            if (initialized) ApplyToggles();
        }

        #endregion

        #region Helpers

        void ApplyToggles()
        {
            var bloom = qualityBloom;
            var tiltShift = qualityTiltShift;
            var shadows = qualityShadows;
            var pixelation = config.PixelationEnabled;
            var tier = detectedTier;
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (PlayerDataManager.Instance != null)
            {
                var debug = PlayerDataManager.Instance.DebugSettings;
                bloom &= !debug.disableBloom;
                tiltShift &= !debug.disableTiltShift;
                pixelation &= !debug.disablePixelation;
                tier = debug.renderTier switch
                {
                    1 => RenderQualityTier.Full,
                    2 => RenderQualityTier.Low,
                    _ => tier,
                };
            }
#endif
            Tier = tier;
            var low = tier == RenderQualityTier.Low;
            lowTierVolume.enabled = low;
            tiltShift &= !low || config.LowTierTiltShift;
            shadows &= !low || config.LowTierShadows;
            if (bloomOff != null) bloomOff.active = !bloom;
            if (tiltShiftOff != null) tiltShiftOff.active = !tiltShift;
            cameraData.renderShadows = shadows;
            pixelDisplay.SetPixelationEnabled(pixelation);
        }

        #endregion
    }
}
