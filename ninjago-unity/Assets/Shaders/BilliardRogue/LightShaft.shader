// Fake volumetric god rays: additive quads/cones (UV.x across the shaft, UV.y 0 at the light source, 1 at the floor)
// with two octaves of scrolling noise, soft UV edges, a view-angle silhouette fade and a camera-distance fade.
// Optional _DEPTH_FADE needs the World camera depth texture (off by default: the pipeline does not request depth).
Shader "BilliardRogue/LightShaft"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.9, 0.7, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _NoiseScroll ("Noise Scroll A.xy B.zw", Vector) = (0.02, 0.08, -0.015, 0.05)
        _EdgeSoftness ("Edge Softness (UV)", Range(0.01, 0.5)) = 0.25
        _EdgePower ("Silhouette Fade Power", Range(0.5, 8)) = 2
        _FadeDistance ("Camera Fade Distance", Range(0.1, 20)) = 3
        [Toggle(_DEPTH_FADE)] _UseDepthFade ("Depth Fade (needs depth texture)", Float) = 0
        _DepthFade ("Depth Fade Distance", Range(0.01, 5)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _DEPTH_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float4 _NoiseTex_ST;
                float4 _NoiseScroll;
                half _EdgeSoftness;
                half _EdgePower;
                half _FadeDistance;
                half _DepthFade;
            CBUFFER_END
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.eyeDepth = -pos.positionVS.z;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv + _Time.y * _NoiseScroll.xy).r
                           * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 1.7 + _Time.y * _NoiseScroll.zw).r * 2.0h;

                half soft = max(_EdgeSoftness, 0.01h);
                half across = smoothstep(0.0h, soft, input.uv.x) * smoothstep(0.0h, soft, 1.0h - input.uv.x);
                half along = smoothstep(0.0h, 0.15h, input.uv.y) * (1.0h - input.uv.y);
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half silhouette = pow(saturate(abs(dot(normalize(input.normalWS), viewDir))), _EdgePower);
                half cameraFade = saturate(input.eyeDepth / max(_FadeDistance, 0.1h));
                half fade = across * along * silhouette * cameraFade;
            #if defined(_DEPTH_FADE)
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS)), _ZBufferParams);
                fade *= saturate((sceneEye - input.eyeDepth) / _DepthFade);
            #endif
                return half4(_Color.rgb * (_Intensity * noise * fade), 0.0h);
            }
            ENDHLSL
        }
    }
}
