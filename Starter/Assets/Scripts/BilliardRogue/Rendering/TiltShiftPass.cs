#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Separable Gaussian blur at half resolution (H then V) composited back through a vertical focus band mask, then
    /// swapped in as the camera colour so URP bloom/grading run on the result. Each draw carries its own
    /// MaterialPropertyBlock because all render functions record into one command buffer (last material write wins).
    /// </summary>
    sealed class TiltShiftPass : ScriptableRenderPass
    {
        const int BlurShaderPass = 0;
        const int CompositeShaderPass = 1;
        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int BlurParamsId = Shader.PropertyToID("_TiltShiftBlur");
        static readonly int BandParamsId = Shader.PropertyToID("_TiltShiftParams");
        static readonly int BlurTexId = Shader.PropertyToID("_TiltShiftBlurTex");

        sealed class DrawData
        {
            public TextureHandle source;
            public TextureHandle blurred;   // composite only
            public Material material = null!;
            public MaterialPropertyBlock block = null!;
            public int shaderPass;
            public Vector4 parameters;
        }

        readonly MaterialPropertyBlock blurHBlock = new();
        readonly MaterialPropertyBlock blurVBlock = new();
        readonly MaterialPropertyBlock compositeBlock = new();
        Material material = null!;
        TiltShiftVolume settings = null!;

        #region Life Cycle

        public TiltShiftPass()
        {
            requiresIntermediateTexture = true;
        }

        public void Setup(Material aMaterial, TiltShiftVolume volume)
        {
            material = aMaterial;
            settings = volume;
        }

        #endregion

        #region Render Graph

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;
            var sourceDesc = renderGraph.GetTextureDesc(source);

            var halfDesc = sourceDesc;
            halfDesc.sizeMode = TextureSizeMode.Explicit;
            halfDesc.width = Mathf.Max(1, sourceDesc.width / 2);
            halfDesc.height = Mathf.Max(1, sourceDesc.height / 2);
            halfDesc.msaaSamples = MSAASamples.None;
            halfDesc.filterMode = FilterMode.Bilinear;
            halfDesc.clearBuffer = false;
            halfDesc.name = "_TiltShiftBlurH";
            var blurH = renderGraph.CreateTexture(halfDesc);
            halfDesc.name = "_TiltShiftBlurV";
            var blurV = renderGraph.CreateTexture(halfDesc);

            var destinationDesc = sourceDesc;
            destinationDesc.name = "_TiltShiftColor";
            destinationDesc.clearBuffer = false;
            var destination = renderGraph.CreateTexture(destinationDesc);

            var radius = settings.maxBlur.value;
            var taps = settings.sampleCount.value;
            AddDraw(renderGraph, "TiltShift BlurH", source, TextureHandle.nullHandle, blurH, BlurShaderPass, blurHBlock,
                new Vector4(radius / sourceDesc.width, 0f, taps, 0f));
            AddDraw(renderGraph, "TiltShift BlurV", blurH, TextureHandle.nullHandle, blurV, BlurShaderPass, blurVBlock,
                new Vector4(0f, radius / halfDesc.height, taps, 0f));
            AddDraw(renderGraph, "TiltShift Composite", source, blurV, destination, CompositeShaderPass, compositeBlock,
                new Vector4(settings.center.value, settings.bandWidth.value, settings.falloff.value, settings.intensity.value));

            resourceData.cameraColor = destination;
        }

        #endregion

        #region Helpers

        void AddDraw(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle blurred, TextureHandle target,
            int shaderPass, MaterialPropertyBlock block, Vector4 parameters)
        {
            using var builder = renderGraph.AddRasterRenderPass<DrawData>(name, out var data);
            data.source = source;
            data.blurred = blurred;
            data.material = material;
            data.block = block;
            data.shaderPass = shaderPass;
            data.parameters = parameters;
            builder.UseTexture(source);
            if (blurred.IsValid()) builder.UseTexture(blurred);
            builder.SetRenderAttachment(target, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (DrawData d, RasterGraphContext context) =>
            {
                d.block.SetTexture(BlitTextureId, d.source);
                d.block.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                if (d.blurred.IsValid())
                {
                    d.block.SetTexture(BlurTexId, d.blurred);
                    d.block.SetVector(BandParamsId, d.parameters);
                }
                else
                {
                    d.block.SetVector(BlurParamsId, d.parameters);
                }

                context.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, d.block);
            });
        }

        #endregion
    }
}
