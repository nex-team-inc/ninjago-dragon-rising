// Full-screen passes used only by TiltShiftFeature (Render Graph): pass 0 = separable Gaussian blur (run twice at
// half resolution), pass 1 = composite that blends the blurred image outside a vertical focus band.
// Uniforms are supplied per draw through a MaterialPropertyBlock (see TiltShiftPass).
Shader "BilliardRogue/TiltShiftBlur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _TiltShiftBlur;     // xy = tap step in source UV, z = taps per side (1..8), w = unused
        float4 _TiltShiftParams;   // x = focus centre (0 bottom .. 1 top), y = half band height, z = falloff, w = intensity
        TEXTURE2D_X(_TiltShiftBlurTex);

        half4 GaussianLine(float2 uv, float2 stepUV, int taps)
        {
            float sigma = max(taps * 0.5, 0.5);
            float inv2Sigma2 = 1.0 / (2.0 * sigma * sigma);
            half4 sum = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            float total = 1.0;
            [loop] for (int i = 1; i <= taps; i++)
            {
                float w = exp(-(float)(i * i) * inv2Sigma2);
                float2 offset = stepUV * i;
                sum += (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0)
                      + SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - offset, 0)) * w;
                total += 2.0 * w;
            }
            return sum / total;
        }

        // Blit.hlsl's Vert compensates UNITY_UV_STARTS_AT_TOP, so uv.y = 0 is the image bottom.
        half TiltMask(float2 uv)
        {
            float d = abs(uv.y - _TiltShiftParams.x) - _TiltShiftParams.y;
            half m = saturate(d / max(_TiltShiftParams.z, 1e-4));
            return m * m * (3.0h - 2.0h * m) * _TiltShiftParams.w;
        }
        ENDHLSL

        Pass
        {
            Name "TiltShiftBlur"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragBlur
            half4 FragBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int taps = clamp((int)_TiltShiftBlur.z, 1, 8);
                return GaussianLine(input.texcoord, _TiltShiftBlur.xy, taps);
            }
            ENDHLSL
        }

        Pass
        {
            Name "TiltShiftComposite"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragComposite
            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 sharp = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                half4 blurred = SAMPLE_TEXTURE2D_X_LOD(_TiltShiftBlurTex, sampler_LinearClamp, uv, 0);
                return lerp(sharp, blurred, TiltMask(uv));
            }
            ENDHLSL
        }
    }
}
