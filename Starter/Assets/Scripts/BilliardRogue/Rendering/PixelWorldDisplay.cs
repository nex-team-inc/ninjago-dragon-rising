#nullable enable

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the low-res world render target: creates it from HD2DVisualConfig (plus the active quality override),
    /// assigns it to the world camera, shows it through a point-filtered full-screen RawImage, snaps the camera to the
    /// texel grid every LateUpdate (sub-texel remainder goes into the RawImage uvRect so pans and shakes stay smooth)
    /// and maps world positions onto UI canvases for crisp labels. The world camera must sit at local identity under
    /// a pivot transform: the pivot carries the designed pose (and CameraShaker offsets), the camera is re-derived
    /// from it every frame so snapping never drifts.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class PixelWorldDisplay : MonoBehaviour
    {
        /// <summary>Runs after CameraShaker (default order) so the snap sees the shaken pivot pose.</summary>
        public const int ExecutionOrder = 200;
        const int MinVisibleHeight = 90;

        Camera worldCamera = null!;
        RawImage display = null!;
        HD2DVisualConfig config = null!;
        Transform cameraTransform = null!;
        Transform? pivot;
        RenderTexture? target;
        int visibleWidth;
        int visibleHeight;
        int margin;
        float baseFieldOfView = 28f;
        Vector3 focusPoint;
        bool hasFocusPoint;
        Rect uvRect = new(0f, 0f, 1f, 1f);

        public Camera WorldCamera => worldCamera;

        /// <summary>The world render target (created in Initialize, recreated on quality/pixelation changes).</summary>
        public RenderTexture Target => target!;

        public bool PixelationEnabled { get; private set; } = true;
        public int VisibleWidth => visibleWidth;
        public int VisibleHeight => visibleHeight;

        #region Life Cycle

        public void Initialize(Camera aWorldCamera, RawImage aDisplay, HD2DVisualConfig aConfig)
        {
            worldCamera = aWorldCamera;
            display = aDisplay;
            config = aConfig;
            cameraTransform = worldCamera.transform;
            pivot = cameraTransform.parent;
            baseFieldOfView = worldCamera.fieldOfView;
            PixelationEnabled = config.PixelationEnabled;
            var height = config.RenderResolution.y;
            var quality = FindQualityOverride();
            if (quality != null && quality.renderHeight > 0) height = quality.renderHeight;
            SetVisibleHeight(height);
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (!PixelationEnabled || !config.PixelSnapping || pivot == null)
            {
                ApplyUvRect(Vector2.zero);
                return;
            }

            SnapToTexelGrid(pivot);
        }

        void OnDestroy()
        {
            ReleaseTarget();
        }

        #endregion

        #region Public Methods

        /// <summary>Quality override matching the active QualitySettings level name, if the config declares one.</summary>
        public HD2DVisualConfig.QualityOverride? FindQualityOverride()
        {
            var names = QualitySettings.names;
            var level = QualitySettings.GetQualityLevel();
            if (level < 0 || level >= names.Length) return null;
            var overrides = config.QualityOverrides;
            for (var i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].qualityName == names[level]) return overrides[i];
            }

            return null;
        }

        /// <summary>Visible texel height of the world (width follows the config aspect); rebuilds the target.</summary>
        public void SetVisibleHeight(int height)
        {
            visibleHeight = Mathf.Max(MinVisibleHeight, height);
            var aspect = config.RenderResolution.x / (float)Mathf.Max(1, config.RenderResolution.y);
            visibleWidth = Mathf.RoundToInt(visibleHeight * aspect);
            RebuildTarget();
        }

        /// <summary>Designed vertical field of view; the display widens it to cover the margin texels.</summary>
        public void SetBaseFieldOfView(float fieldOfView)
        {
            baseFieldOfView = fieldOfView;
            if (target == null)
            {
                worldCamera.fieldOfView = fieldOfView;
                return;
            }

            ApplyFieldOfView();
        }

        /// <summary>World point the texel size is measured at (arena centre); defaults to where the view axis meets y = 0.</summary>
        public void SetFocusPoint(Vector3 world)
        {
            focusPoint = world;
            hasFocusPoint = true;
        }

        /// <summary>
        /// Anchored position of a world point for a label whose anchors and pivot sit at the centre of canvasRect
        /// (a full-screen stretched rect); false when the point is behind the camera.
        /// </summary>
        public bool TryWorldToCanvas(Vector3 world, RectTransform canvasRect, out Vector2 anchored)
        {
            var viewport = worldCamera.WorldToViewportPoint(world);
            if (viewport.z <= 0f)
            {
                anchored = Vector2.zero;
                return false;
            }

            var normalized = ToDisplayNormalized(viewport);
            var size = canvasRect.rect.size;
            anchored = new Vector2((normalized.x - 0.5f) * size.x, (normalized.y - 0.5f) * size.y);
            return true;
        }

        /// <summary>0..1 over the displayed image for a world position (usable as anchorMin/anchorMax).</summary>
        public Vector2 WorldToScreenNormalized(Vector3 world)
        {
            return ToDisplayNormalized(worldCamera.WorldToViewportPoint(world));
        }

        /// <summary>Toggles between the low-res target and a native-resolution target (debug / quality).</summary>
        public void SetPixelationEnabled(bool enabled)
        {
            if (PixelationEnabled == enabled) return;
            PixelationEnabled = enabled;
            RebuildTarget();
        }

        #endregion

        #region Helpers

        void RebuildTarget()
        {
            ReleaseTarget();
            int width;
            int height;
            if (PixelationEnabled)
            {
                margin = config.MarginTexels;
                width = visibleWidth + 2 * margin;
                height = visibleHeight + 2 * margin;
            }
            else
            {
                margin = 0;
                height = Mathf.Max(MinVisibleHeight, Screen.height);
                width = Mathf.RoundToInt(height * (visibleWidth / (float)visibleHeight));
            }

            var format = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
            target = new RenderTexture(width, height, 24, format)
            {
                name = "PixelWorldRT",
                filterMode = PixelationEnabled ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1,
            };
            target.Create();
            worldCamera.targetTexture = target;
            display.texture = target;
            ApplyFieldOfView();
            ApplyUvRect(Vector2.zero);
        }

        void ReleaseTarget()
        {
            if (target == null) return;
            worldCamera.targetTexture = null;
            target.Release();
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);   // Editor builders / captures rebuild the target outside play mode
            target = null;
        }

        // The margin texels lie outside the designed framing, so the field of view grows to keep the visible part unchanged.
        void ApplyFieldOfView()
        {
            var visible = target!.height - 2 * margin;
            var halfTan = Mathf.Tan(0.5f * baseFieldOfView * Mathf.Deg2Rad) * target.height / visible;
            worldCamera.fieldOfView = 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
        }

        void SnapToTexelGrid(Transform cameraPivot)
        {
            var rotation = cameraPivot.rotation;
            var desired = cameraPivot.position;
            var local = Quaternion.Inverse(rotation) * desired;
            var texel = WorldUnitsPerTexel(desired, rotation * Vector3.forward);
            var snapped = new Vector3(Mathf.Round(local.x / texel) * texel, Mathf.Round(local.y / texel) * texel, local.z);
            cameraTransform.SetPositionAndRotation(rotation * snapped, rotation);
            ApplyUvRect(new Vector2((local.x - snapped.x) / texel, (local.y - snapped.y) / texel));
        }

        float WorldUnitsPerTexel(Vector3 cameraPosition, Vector3 forward)
        {
            if (worldCamera.orthographic) return 2f * worldCamera.orthographicSize / target!.height;
            float distance;
            if (hasFocusPoint)
            {
                distance = Vector3.Dot(focusPoint - cameraPosition, forward);
            }
            else
            {
                distance = forward.y < -0.01f ? -cameraPosition.y / forward.y : 20f;
            }

            distance = Mathf.Max(1f, distance);
            return 2f * distance * Mathf.Tan(0.5f * worldCamera.fieldOfView * Mathf.Deg2Rad) / target!.height;
        }

        void ApplyUvRect(Vector2 subTexelError)
        {
            var width = (float)target!.width;
            var height = (float)target.height;
            uvRect = new Rect((margin + subTexelError.x) / width, (margin + subTexelError.y) / height,
                (width - 2 * margin) / width, (height - 2 * margin) / height);
            display.uvRect = uvRect;
        }

        Vector2 ToDisplayNormalized(Vector3 viewport)
        {
            return new Vector2((viewport.x - uvRect.x) / uvRect.width, (viewport.y - uvRect.y) / uvRect.height);
        }

        #endregion
    }
}
