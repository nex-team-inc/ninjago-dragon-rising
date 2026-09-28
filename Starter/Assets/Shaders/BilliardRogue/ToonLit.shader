// Opaque cel-shaded surface for every Billiard Rogue model (palette atlas or tiling surface sets) and the balls.
// TDD §16: property names are a contract with Presentation (MaterialPropertyBlocks) and the builders.
Shader "BilliardRogue/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_WORLD_UV)] _UseWorldUv ("World UV Projection", Float) = 0
        _Tiling ("Tiling (UV multiplier)", Float) = 1

        [Toggle(_NORMALMAP)] _UseNormalMap ("Normal Map", Float) = 0
        [NoScaleOffset][Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 4)) = 2
        [Toggle(_CAVITYMAP)] _UseCavityMap ("Cavity Map", Float) = 0
        [NoScaleOffset] _CavityMap ("Cavity (R, 0.5 = neutral)", 2D) = "gray" {}
        _CavityStrength ("Cavity Strength", Range(0, 2)) = 0.75

        _Bands ("Light Bands", Range(3, 5)) = 4
        _BandSoftness ("Band Softness", Range(0.001, 0.25)) = 0.02
        _ShadowTint ("Shadow Tint", Color) = (0.42, 0.4, 0.62, 1)
        _AmbientStrength ("Ambient (SH) Strength", Range(0, 1)) = 0.35
        _RimColor ("Rim Color (A = strength)", Color) = (1, 0.95, 0.85, 0.35)
        _RimPower ("Rim Power", Range(1, 8)) = 4

        [Toggle(_EMISSION)] _UseEmission ("Emission", Float) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 1

        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _StatusTint ("Status Tint (A = amount)", Color) = (1, 1, 1, 0)
        _Alpha ("Alpha (transparent variant only)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "ToonLitInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVert
            #pragma fragment ToonFrag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _WORLD_UV
            #pragma shader_feature_local_fragment _CAVITYMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "ToonLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
