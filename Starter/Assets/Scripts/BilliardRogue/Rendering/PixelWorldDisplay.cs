#nullable enable

using UnityEngine;
using UnityEngine.UI;

// Implemented by Rendering module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the low-res world render target: creates it from HD2DVisualConfig, assigns it to the world camera, shows
    /// it through a point-filtered full-screen RawImage with integer upscaling, snaps camera motion to texels and
    /// maps world positions onto UI canvases for crisp labels.
    /// </summary>
    public sealed class PixelWorldDisplay : MonoBehaviour
    {
        Camera worldCamera = null!;
        RawImage display = null!;
        HD2DVisualConfig config = null!;

        public Camera WorldCamera => worldCamera;

        /// <summary>The low-res render target (created in Initialize).</summary>
        public RenderTexture Target => null!;

        public bool PixelationEnabled { get; private set; } = true;

        public void Initialize(Camera aWorldCamera, RawImage aDisplay, HD2DVisualConfig aConfig)
        {
            worldCamera = aWorldCamera;
            display = aDisplay;
            config = aConfig;
            PixelationEnabled = config.PixelationEnabled;
        }

        /// <summary>Anchored position of a world point inside canvasRect (a full-screen stretched rect); false when behind the camera.</summary>
        public bool TryWorldToCanvas(Vector3 world, RectTransform canvasRect, out Vector2 anchored)
        {
            anchored = Vector2.zero;
            return false;
        }

        /// <summary>Toggles between the low-res RT and native resolution rendering (debug / quality).</summary>
        public void SetPixelationEnabled(bool enabled)
        {
            PixelationEnabled = enabled;
        }
    }
}
