# URP 17.3 HD-2D Shader Skeletons (Billiard Rogue) — appendix

Companion to `urp-hd2d-rendering.md` (read its §7.1 for include/keyword/SRP-Batcher rules first).
Written against `com.unity.render-pipelines.universal@17.3.0` ShaderLibrary; every function/macro used was checked in source,
but the shaders have **not been compiled in the Editor yet [unverified compile]**. Suggested paths: `Assets/Shaders/BilliardRogue/*.shader`.

## 7.2 Toon (N-band cel) shader with normal + cavity maps
```hlsl
Shader "BilliardRogue/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_NORMALMAP)] _UseNormalMap ("Normal Map", Float) = 0
        [NoScaleOffset][Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        [Toggle(_CAVITYMAP)] _UseCavityMap ("Cavity Map", Float) = 0
        [NoScaleOffset] _CavityMap ("Cavity (R, 0.5 = neutral)", 2D) = "gray" {}
        _CavityStrength ("Cavity Strength", Range(0,2)) = 1
        _Bands ("Light Bands", Range(2,6)) = 3
        _BandSoftness ("Band Softness", Range(0.001,0.25)) = 0.03
        _ShadowColor ("Shadow Side Color", Color) = (0.45,0.42,0.6,1)
        _AmbientStrength ("Ambient (SH) Strength", Range(0,1)) = 0.35
        _RimColor ("Rim Color (A = strength)", Color) = (1,1,1,0)
        _RimPower ("Rim Power", Range(1,8)) = 4
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        CBUFFER_START(UnityPerMaterial)   // names _BaseMap_ST/_BaseColor/_Cutoff are required by ShadowCasterPass.hlsl
            float4 _BaseMap_ST; half4 _BaseColor; half _Cutoff; half _BumpScale; half _CavityStrength;
            half _Bands; half _BandSoftness; half4 _ShadowColor; half _AmbientStrength;
            half4 _RimColor; half _RimPower; half4 _EmissionColor;
        CBUFFER_END
        TEXTURE2D(_CavityMap); SAMPLER(sampler_CavityMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _CAVITYMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2;
            #if defined(_NORMALMAP)
                half4 tangentWS : TEXCOORD3;   // w = bitangent sign
            #endif
                half3 sh : TEXCOORD4; half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normalWS = nrm.normalWS;
            #if defined(_NORMALMAP)
                o.tangentWS = half4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            #endif
                o.sh = SampleSH(nrm.normalWS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half ToonRamp(half x)   // x in [0,1] -> _Bands levels with soft centred steps
            {
                half steps = max(_Bands, 2.0h) - 1.0h;
                half s = saturate(x) * steps;
                half f = frac(s);
                return (s - f + smoothstep(0.5h - _BandSoftness, 0.5h + _BandSoftness, f)) / steps;
            }

            half3 AdditionalToon(Light light, half3 normalWS)
            {
                half ramp = ToonRamp(saturate(dot(normalWS, light.direction)) * light.shadowAttenuation);
                return light.color * (ramp * light.distanceAttenuation);   // quantize distanceAttenuation too for banded light pools
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif

                half3 normalWS = normalize(input.normalWS);
            #if defined(_NORMALMAP)
                half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, input.tangentWS.xyz);
                normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, normalWS)));
            #endif

                InputData inputData = (InputData)0;          // name 'inputData' is required by LIGHT_LOOP_BEGIN (cluster)
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);   // per-pixel: works for cascades too

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1, 1, 1, 1));
                half ramp = ToonRamp(saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation);
                half3 lighting = lerp(_ShadowColor.rgb, mainLight.color, ramp) + input.sh * _AmbientStrength;

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
              #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                    lighting += AdditionalToon(GetAdditionalLight(dirIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
              #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    lighting += AdditionalToon(GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
                LIGHT_LOOP_END
            #endif

                half3 color = albedo.rgb * lighting;
            #if defined(_CAVITYMAP)
                half cavity = SAMPLE_TEXTURE2D(_CavityMap, sampler_CavityMap, input.uv).r;   // <0.5 crevice, >0.5 edge
                color *= 1.0h + (cavity - 0.5h) * _CavityStrength;
            #endif
                half rim = pow(1.0h - saturate(dot(normalWS, inputData.viewDirectionWS)), _RimPower) * _RimColor.a;
                color += _RimColor.rgb * (rim * ramp);
                color += _EmissionColor.rgb;                                // HDR emission feeds bloom
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // DepthOnly: same shape as ShadowCaster with Name "DepthOnly", Tags{"LightMode"="DepthOnly"}, "ZWrite On ColorMask R",
        //   #pragma vertex DepthOnlyVertex / fragment DepthOnlyFragment, #include ".../Shaders/DepthOnlyPass.hlsl".
        // DepthNormals: Name "DepthNormals", Tags{"LightMode"="DepthNormals"}, "ZWrite On",
        //   #pragma vertex DepthNormalsVertex / fragment DepthNormalsFragment, #include ".../Shaders/DepthNormalsPass.hlsl".
        // Both: shader_feature_local _ALPHATEST_ON + multi_compile_instancing. Needed when a depth prepass / depth texture is used.
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
```
Not compiled yet in the Editor **[unverified compile]** — all functions/macros used were checked against 17.3 source. `_ADDITIONAL_LIGHTS_VERTEX`, soft-shadow, cookie, lightmap and decal keywords are deliberately omitted (settings in §2.3 keep them off). Toon outline (inverted hull) can be an extra pass with `Tags{"LightMode"="SRPDefaultUnlit"}` + `Cull Front` (URP draws it in the same opaque pass) — costs a second draw per renderer; keep it off for small props.

## 7.3 Lit pixel billboard particle (alpha-clip, flipbook)
ParticleSystemRenderer: Billboard, material with this shader, texture Point/no-compression. Either use the **Texture Sheet Animation** module (UVs arrive already per-frame; leave `_FLIPBOOK_CUSTOMDATA` off) or enable **Custom Vertex Streams** `Position, Normal, Color, UV, Custom1.x` → frame index lands in `TEXCOORD0.z` (verify the stream table in the Renderer module shows `TEXCOORD0.zw` for Custom1) **[verify in Editor]**.
```hlsl
Shader "BilliardRogue/LitPixelParticle"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite Sheet", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_FLIPBOOK_CUSTOMDATA)] _FlipbookCustom ("Frame From Custom1.x", Float) = 0
        _FlipbookGrid ("Flipbook Cols, Rows", Vector) = (4,4,0,0)
        _Bands ("Light Bands", Range(2,5)) = 3
        _ShadowColor ("Shadow Side Color", Color) = (0.5,0.45,0.65,1)
        _Emission ("Self Emission (HDR mult)", Range(0,8)) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half _Cutoff; float4 _FlipbookGrid; half _Bands; half4 _ShadowColor; half _Emission;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _FLIPBOOK_CUSTOMDATA
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;    // billboard verts; TransformObjectToWorld stays valid
                float3 normalOS   : NORMAL;      // add the Normal stream; faces the camera for billboards
                half4  color      : COLOR;
                float4 texcoord0  : TEXCOORD0;   // xy = UV, z = Custom1.x (frame) with custom streams
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2; half4 color : TEXCOORD3; half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 uv = input.texcoord0.xy;
            #if defined(_FLIPBOOK_CUSTOMDATA)
                float frame = floor(input.texcoord0.z);
                float2 grid = max(_FlipbookGrid.xy, 1.0);
                float col = fmod(frame, grid.x);
                float row = grid.y - 1.0 - floor(frame / grid.x);   // frame 0 = top-left
                uv = (uv + float2(col, row)) / grid;
            #endif
                o.uv = TRANSFORM_TEX(uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half Band(half x) { half s = max(_Bands, 2.0h) - 1.0h; return floor(saturate(x) * s + 0.5h) / s; }
            half3 SpriteLight(Light l, half3 n) { return l.color * (Band(dot(n, l.direction) * 0.5h + 0.5h) * l.distanceAttenuation); }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color;
                clip(tex.a - _Cutoff);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 n = normalize(input.normalWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1, 1, 1, 1));
                half wrap = dot(n, mainLight.direction) * 0.5h + 0.5h;      // half-lambert: flat sprites never go black
                half3 lighting = lerp(_ShadowColor.rgb, mainLight.color, Band(wrap * mainLight.shadowAttenuation));

            #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
              #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint d = 0; d < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); d++)
                    lighting += SpriteLight(GetAdditionalLight(d, input.positionWS), n);
              #endif
                LIGHT_LOOP_BEGIN(count)
                    lighting += SpriteLight(GetAdditionalLight(lightIndex, input.positionWS), n);
                LIGHT_LOOP_END
            #endif

                half3 color = tex.rgb * (lighting + _Emission);
                return half4(MixFog(color, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }
    }
}
```
Queue AlphaTest renders in the opaque pass (receives shadows normally, writes depth, no sorting). Particles should not cast shadows (no ShadowCaster pass). The particle system gets per-object light indices from its bounds (Forward) — large systems may miss lights.

## 7.4 Cheap god rays: additive light-shaft meshes
Mesh: a few long quads/open cone aligned to the key light, UV.y 0 at the source, 1 at the floor. Transparent queue, additive, rendered by the World camera (low res → bloom picks it up if colour > threshold). Depth fade needs the World camera's depth texture (`requiresDepthOption = On`); drop `_DEPTH_FADE` to avoid it.
```hlsl
Shader "BilliardRogue/LightShaft"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.9,0.7,1)
        _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _Scroll ("Scroll A.xy B.zw", Vector) = (0.02,0.1,-0.015,0.06)
        _EdgePower ("Edge Fade Power", Range(0.5,8)) = 2
        _DepthFade ("Depth Fade Distance", Range(0.01,5)) = 1
        [Toggle(_DEPTH_FADE)] _UseDepthFade ("Depth Fade", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _DEPTH_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float4 _NoiseTex_ST; float4 _Scroll; half _EdgePower; half _DepthFade;
            CBUFFER_END
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1;
                              half3 normalWS : TEXCOORD2; float eyeDepth : TEXCOORD3; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.uv = v.uv;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.eyeDepth = -p.positionVS.z;   // explicit eye depth (SV_Position.w semantics differ per API)
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = TRANSFORM_TEX(i.uv, _NoiseTex);
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv + _Time.y * _Scroll.xy).r
                           * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * 1.7 + _Time.y * _Scroll.zw).r * 2.0h;
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half edge = pow(saturate(abs(dot(normalize(i.normalWS), v))), _EdgePower);  // hide polygon silhouettes
                half along = smoothstep(0.0h, 0.15h, i.uv.y) * (1.0h - i.uv.y);               // fade at source and floor
                half fade = 1.0h;
            #if defined(_DEPTH_FADE)
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams); // perspective only
                fade = saturate((sceneEye - i.eyeDepth) / _DepthFade);
            #endif
                return half4(_Color.rgb * (noise * edge * along * fade), 0.0h);
            }
            ENDHLSL
        }
    }
}
```
Alternative (no meshes): screen-space radial blur of a thresholded sun mask at quarter res — more expensive and needs a visible light source; not recommended here.

---
