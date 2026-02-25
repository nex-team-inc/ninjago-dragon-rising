Shader "Hidden/ChocDino/UIFX/Blend-LongShadow"
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

		_SourceAlpha ("Source Alpha", Float) = 1

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local __ STYLE_NORMAL STYLE_CUTOUT STYLE_SHADOW

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	float _SourceAlpha = 1.0;

	struct v2fLongShadow
	{
		float4 vertex : SV_POSITION;
		float4 uv : TEXCOORD0;
		float4 color : TEXCOORD1;
		#ifdef UNITY_UI_CLIP_RECT
		float4 mask : TEXCOORD2;
		#endif

		UNITY_VERTEX_OUTPUT_STEREO // VR SUPPORT
	};

	v2fLongShadow vertLongshadow (appdata v)
	{
		v2fLongShadow o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2fLongShadow, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT
	
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
		o.uv.zw = TRANSFORM_TEX(v.uv, _ResultTex);
		o.color = v.color;

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		{
			float2 pixelSize = o.vertex.w;
			pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			#ifdef MASK_SOFTNESS_OLD
			half2 maskSoftness = half2(_MaskSoftnessX, _MaskSoftnessY);
			#else
			half2 maskSoftness = half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
			#endif
			o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
		}
		#endif
		
		return o;
	}

	float4 fragLongShadow(v2fLongShadow i) : SV_Target
	{
		float4 color = 0.0;
		
		float4 shadowColor = tex2D(_ResultTex, i.uv.zw);

		#if (STYLE_SHADOW)
		{
			color = shadowColor;
		}
		#else
		{
			// Note: Need to do this explicit swizzling for console compilers
			float zero = 0.0;
			color = lerp(zero.xxxx, tex2D(_SourceTex, i.uv.xy), _SourceAlpha.xxxx);

			#if (STYLE_NORMAL)
			{
				color = AlphaComp_Over(color, shadowColor);
			}
			#elif (STYLE_CUTOUT)
			{
				color = AlphaComp_Out(shadowColor, color);
			}
			#endif
		}
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
			Name "Blend-LongShadow"
			CGPROGRAM
			#pragma vertex vertLongshadow
			#pragma fragment fragLongShadow
			ENDCG
		}
	}
}
