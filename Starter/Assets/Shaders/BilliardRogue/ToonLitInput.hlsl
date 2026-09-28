#ifndef BILLIARD_ROGUE_TOON_LIT_INPUT_INCLUDED
#define BILLIARD_ROGUE_TOON_LIT_INPUT_INCLUDED

// Shared by every pass of BilliardRogue/ToonLit and ToonLitTransparent so the UnityPerMaterial layout is identical
// (SRP Batcher). ShadowCasterPass/DepthOnlyPass/DepthNormalsPass.hlsl rely on _BaseMap_ST, _BaseColor, _Cutoff and the
// _BaseMap/_BumpMap samplers declared by SurfaceInput.hlsl.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Cutoff;
    float _Tiling;
    half _BumpScale;
    half _CavityStrength;
    half _Bands;
    half _BandSoftness;
    half4 _ShadowTint;
    half _AmbientStrength;
    half4 _RimColor;
    half _RimPower;
    half4 _EmissionColor;
    half _EmissionStrength;
    half4 _FlashColor;
    half _FlashAmount;
    half4 _StatusTint;
    half _Alpha;
CBUFFER_END

// Every ToonLit map is sampled through URP's global point/repeat sampler state (GlobalSamplers.hlsl): the palette
// atlas and the 64 px surface sets are pixel textures (TDD §14: point, no mips; surfaces repeat, palette UVs stay
// inside the atlas), so the look does not depend on importer filter / wrap settings.
TEXTURE2D(_CavityMap);

// Quantizes x (0..1) into _Bands levels with a soft step in the middle of every band so the terminator does not alias.
half ToonRamp(half x)
{
    half steps = max(_Bands, 2.0h) - 1.0h;
    half s = saturate(x) * steps;
    half f = frac(s);
    half soft = max(_BandSoftness, 0.001h);
    return (s - f + smoothstep(0.5h - soft, 0.5h + soft, f)) / steps;
}

// Box projection for *_Surface meshes and primitives: picks the plane facing the dominant normal axis.
float2 WorldProjectedUv(float3 positionWS, float3 normalWS)
{
    float3 n = abs(normalWS);
    float2 uv = positionWS.xz;
    if (n.x > n.y && n.x > n.z) uv = positionWS.zy;
    else if (n.z > n.y) uv = positionWS.xy;
    return uv;
}

#endif
