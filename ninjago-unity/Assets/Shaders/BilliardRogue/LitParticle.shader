// Alpha-clipped pixel-sprite particles lit by the main light (banded, shadowed) and additional lights.
// AlphaTest queue: renders with the opaques, writes depth, no sorting problems; no ShadowCaster pass (D13).
// Vertex colour (particle colour over lifetime) multiplies the sprite. Flipbooks come from Texture Sheet Animation.
Shader "BilliardRogue/LitParticle"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite Sheet", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _LightInfluence ("Light Influence (0 unlit, 1 lit)", Range(0, 1)) = 0.7
        _EmissionStrength ("Self Emission", Range(0, 8)) = 0
        _Bands ("Light Bands", Range(2, 5)) = 3
        _ShadowTint ("Shadow Tint", Color) = (0.5, 0.45, 0.65, 1)
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _LightInfluence;
            half _EmissionStrength;
            half _Bands;
            half4 _ShadowTint;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Band(half x)
            {
                half steps = max(_Bands, 2.0h) - 1.0h;
                return floor(saturate(x) * steps + 0.5h) / steps;
            }

            half3 SpriteLight(Light light, half3 normalWS)
            {
                half wrap = dot(normalWS, light.direction) * 0.5h + 0.5h;
                return light.color * (Band(wrap * light.shadowAttenuation) * Band(saturate(light.distanceAttenuation)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                clip(tex.a - _Cutoff);

                InputData inputData = (InputData)0;   // required by LIGHT_LOOP_BEGIN in the cluster path
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 normalWS = normalize(input.normalWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));
                half wrap = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;   // half-lambert: flat sprites never go black
                half3 lighting = mainLight.color * lerp(_ShadowTint.rgb, half3(1, 1, 1), Band(wrap * mainLight.shadowAttenuation));

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
              #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    lighting += SpriteLight(GetAdditionalLight(dirIndex, input.positionWS, half4(1, 1, 1, 1)), normalWS);
                }
              #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    lighting += SpriteLight(GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1)), normalWS);
                LIGHT_LOOP_END
            #endif

                half3 lit = lerp(half3(1, 1, 1), lighting, _LightInfluence) + _EmissionStrength;
                half3 color = MixFog(tex.rgb * lit, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
