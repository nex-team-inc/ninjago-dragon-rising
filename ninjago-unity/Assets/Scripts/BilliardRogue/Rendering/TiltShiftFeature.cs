#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Renderer feature of the dedicated World renderer: runs TiltShiftPass before URP post-processing on Game cameras
    /// whose volume stack has an active TiltShiftVolume. Render Graph only (no Compatibility Mode path).
    /// </summary>
    public sealed class TiltShiftFeature : ScriptableRendererFeature
    {
        [Tooltip("BilliardRogue/TiltShiftBlur; a serialized reference keeps the shader in builds.")]
        [SerializeField] Shader shader = null!;

        Material? material;
        TiltShiftPass? pass;

        public override void Create()
        {
            pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
            // Fully qualified: the starter's Nex.VolumeManager (audio) shadows the SRP one inside this namespace.
            var volume = UnityEngine.Rendering.VolumeManager.instance.stack.GetComponent<TiltShiftVolume>();
            if (!volume.IsActive()) return;
            if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
            pass!.Setup(material, volume);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }
    }
}
