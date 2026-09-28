#ifndef BILLIARD_ROGUE_TOON_LIT_FORWARD_PASS_INCLUDED
#define BILLIARD_ROGUE_TOON_LIT_FORWARD_PASS_INCLUDED

// ForwardLit pass shared by ToonLit (opaque) and ToonLitTransparent (define _TOON_TRANSPARENT before including).
// Keyword expectations: _MAIN_LIGHT_SHADOWS(_CASCADE), _ADDITIONAL_LIGHTS, _CLUSTER_LIGHT_LOOP, fog, instancing,
// _NORMALMAP, _CAVITYMAP, _EMISSION, _ALPHATEST_ON, _WORLD_UV.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
#if defined(_NORMALMAP)
    half4 tangentWS : TEXCOORD3;   // w = bitangent sign
#endif
    half3 sh : TEXCOORD4;
    half fogFactor : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings ToonVert(Attributes input)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = nrm.normalWS;
#if defined(_WORLD_UV)
    float2 uv = WorldProjectedUv(pos.positionWS, nrm.normalWS) * _Tiling;
#else
    float2 uv = input.uv * _Tiling;
#endif
    o.uv = TRANSFORM_TEX(uv, _BaseMap);
#if defined(_NORMALMAP)
    o.tangentWS = half4(nrm.tangentWS, input.tangentOS.w * GetOddNegativeScale());
#endif
    o.sh = SampleSH(nrm.normalWS);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
    return o;
}

// Additional lights are banded like the main light; the distance falloff is quantized too so torches and crystals
// throw stepped light pools instead of smooth gradients.
half3 AdditionalToon(Light light, half3 normalWS)
{
    half ramp = ToonRamp(saturate(dot(normalWS, light.direction)) * light.shadowAttenuation);
    half pool = ToonRamp(saturate(light.distanceAttenuation));
    return light.color * (ramp * pool);
}

half4 ToonFrag(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 albedo = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_PointRepeat)) * _BaseColor;
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif
    // Status tint (freeze/poison/burn) pulls the albedo toward the tint while keeping some of the original shading.
    albedo.rgb = lerp(albedo.rgb, _StatusTint.rgb * (0.35h + 0.65h * albedo.rgb), _StatusTint.a);

    half3 normalWS = normalize(input.normalWS);
#if defined(_NORMALMAP)
    half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_PointRepeat), _BumpScale);
    half3 bitangentWS = input.tangentWS.w * cross(normalWS, input.tangentWS.xyz);
    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, normalWS)));
#endif

    InputData inputData = (InputData)0;   // this name is required by LIGHT_LOOP_BEGIN in the cluster path
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, half4(1, 1, 1, 1));
    half ramp = ToonRamp(saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation);
    half3 lighting = mainLight.color * lerp(_ShadowTint.rgb, half3(1, 1, 1), ramp) + input.sh * _AmbientStrength;

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
  #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        lighting += AdditionalToon(GetAdditionalLight(dirIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
    }
  #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        lighting += AdditionalToon(GetAdditionalLight(lightIndex, inputData.positionWS, half4(1, 1, 1, 1)), normalWS);
    LIGHT_LOOP_END
#endif

    half3 color = albedo.rgb * lighting;
#if defined(_CAVITYMAP)
    half cavity = SAMPLE_TEXTURE2D(_CavityMap, sampler_PointRepeat, input.uv).r;   // 0.5 neutral, <0.5 crevice, >0.5 edge
    color *= 1.0h + (cavity - 0.5h) * 2.0h * _CavityStrength;
#endif
    half rim = pow(1.0h - saturate(dot(normalWS, inputData.viewDirectionWS)), _RimPower) * _RimColor.a;
    color += _RimColor.rgb * (rim * max(ramp, 0.2h));
    color += SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_PointRepeat)) * _EmissionStrength;
    color = lerp(color, _FlashColor.rgb, _FlashAmount);
    color = MixFog(color, input.fogFactor);

#if defined(_TOON_TRANSPARENT)
    return half4(color, albedo.a * _Alpha);
#else
    return half4(color, 1.0h);
#endif
}

#endif
