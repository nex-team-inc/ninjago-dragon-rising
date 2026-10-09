// Dotted aim line for a LineRenderer in Tile texture mode: UV.x runs along the line in tile units. Dashes are
// procedural (_DashRatio) and optionally multiplied by _DashTex; they scroll toward the target and fade with the
// distance from the launch point. Additive so the HDR _Color blooms; vertex colour = LineRenderer gradient.
Shader "BilliardRogue/AimGuide"
{
    Properties
    {
        _DashTex ("Dash Texture (optional)", 2D) = "white" {}
        [HDR] _Color ("Color (HDR)", Color) = (1, 1, 1, 1)
        _DashRatio ("Dash Fill Ratio", Range(0.05, 1)) = 0.5
        _ScrollSpeed ("Scroll Speed (tiles/s)", Range(-10, 10)) = 2
        _FadeStart ("Fade Start (tiles)", Float) = 6
        _FadeEnd ("Fade End (tiles)", Float) = 14
        _EdgeSoftness ("Edge Softness (across)", Range(0.01, 0.5)) = 0.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DashTex_ST;
                half4 _Color;
                half _DashRatio;
                half _ScrollSpeed;
                float _FadeStart;
                float _FadeEnd;
                half _EdgeSoftness;
            CBUFFER_END
            TEXTURE2D(_DashTex);
            SAMPLER(sampler_DashTex);

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
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.x - _Time.y * _ScrollSpeed;
                half dash = step(frac(along), _DashRatio);
                half tex = SAMPLE_TEXTURE2D(_DashTex, sampler_DashTex, TRANSFORM_TEX(float2(along, input.uv.y), _DashTex)).a;
                half soft = max(_EdgeSoftness, 0.01h);
                half across = smoothstep(0.0h, soft, input.uv.y) * smoothstep(0.0h, soft, 1.0h - input.uv.y);
                half fade = 1.0h - smoothstep(_FadeStart, max(_FadeEnd, _FadeStart + 0.01), input.uv.x);
                half4 color = _Color * input.color;
                color.a *= dash * tex * across * fade;
                return color;
            }
            ENDHLSL
        }
    }
}
