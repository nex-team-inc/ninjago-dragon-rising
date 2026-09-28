// Additive HDR glow for flashes, trails, light motes and bounce markers. Vertex colour × _BaseColor × _Intensity;
// values above the bloom threshold bloom. Fog fades additive colour toward black so glows do not tint the fog.
Shader "BilliardRogue/GlowParticle"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite", 2D) = "white" {}
        [HDR][MainColor] _BaseColor ("Color (HDR)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        [Toggle(_SOFT_DISC)] _SoftDisc ("Procedural soft disc (no texture)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _SOFT_DISC
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Intensity;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            #if defined(_SOFT_DISC)
                half d = length(input.uv - 0.5h) * 2.0h;
                tex *= saturate(1.0h - d * d);
            #endif
                half4 color = tex * _BaseColor * input.color;
                color.rgb = MixFogColor(color.rgb * _Intensity, half3(0, 0, 0), input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
