Shader "Hidden/ChocDino/UIFX/Blend-Composite"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15

		_Strength     ("Strength", Float) = 1.0
		_TintColor    ("Tint Color", Vector) = (1.0, 1.0, 1.0, 1.0)
		_PowerIntensity ("Power Intensity", Vector) = (1.0, 1.0, 1.0, 1.0)
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment _ BLEND_BEHIND BLEND_OVER BLEND_ADDITIVE

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	uniform half _Strength;
	uniform half4 _TintColor;
	uniform half2 _PowerIntensity;

	float4 fragComposite(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		half4 result = tex2D(_ResultTex, i.uv) * _TintColor;

		result = saturate((pow(result, _PowerIntensity.x)) * _PowerIntensity.y);

		half4 color = result;

		#if BLEND_BEHIND || BLEND_OVER || BLEND_ADDITIVE

		// Note: This is already pre-multiplied alpha
		half4 source = tex2D(_SourceTex, i.uv);

		#if BLEND_BEHIND
		color = AlphaComp_Over(source, result);
		#elif BLEND_OVER
		color = AlphaComp_Over(result, source);
		#elif BLEND_ADDITIVE
		color = saturate(result + source);
		#endif

		color = lerp(source, color, _Strength);

		#endif

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		color = ApplyClipRect(color, i.mask);
		#endif

		// Alpha clipping
		#ifdef UNITY_UI_ALPHACLIP
		clip (color.a - 0.001);
		#endif

		color.rgb *= i.color.a;
		color *= i.color;

		return color;
	}
	
	ENDCG

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"OutputsPremultipliedAlpha"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		ColorMask [_ColorMask]

		Pass
		{
			Name "Blend-Composite"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragComposite
			ENDCG
		}
	}
}