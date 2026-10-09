Shader "Hidden/ChocDino/UIFX/Blend-Fill-Texture"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _FillTex ("Fill Texture", 2D) = "clear" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_Color      ("Color", Color) = (1, 1, 1, 1)
		_Strength   ("Strength", Float) = 1.0
		_BlendMode   ("Blend Mode", Int) = 0

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment _ WRAP_CLAMP WRAP_REPEAT WRAP_MIRROR
	#pragma multi_compile_local_fragment _ BLEND_FORCEGAMMASPACE

	#include "BlendUtils.cginc"
	#include "ColorUtils.cginc"
	#include "CompUtils.cginc"
	#include "Common/BlendModes.cginc"

	struct v2fFill
	{
		float4 vertex : SV_POSITION;
		float4 uv : TEXCOORD0;
		float4 color : TEXCOORD1;
		#ifdef UNITY_UI_CLIP_RECT
		float4 mask : TEXCOORD2;
		#endif

		UNITY_VERTEX_OUTPUT_STEREO // VR SUPPORT
	};

	#if WRAP_CLAMP
	Texture2D _FillTex;
	SamplerState my_linear_clamp_sampler;
	#elif WRAP_REPEAT
	Texture2D _FillTex;
	SamplerState my_linear_repeat_sampler;
	#elif WRAP_MIRROR
	Texture2D _FillTex;
	SamplerState my_linear_mirror_sampler;
	#else
	uniform sampler2D _FillTex;
	#endif

	uniform float4x4 _FillTex_Matrix;
	uniform half4 _Color;
	uniform float _SourceAlpha;
	uniform float _Strength;
	uniform int _BlendMode;
	
	v2fFill vertFill(appdata v)
	{
		v2fFill o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2fFill, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT
		
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv.xy = TRANSFORM_TEX(v.uv, _ResultTex);
		o.uv.zw = mul(_FillTex_Matrix, float4(v.uv.xy, 0.0, 1.0)).xy;

		o.color = v.color;

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		{
			float2 pixelSize = o.vertex.w;
			pixelSize /= float2(1.0, 1.0) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			#ifdef MASK_SOFTNESS_OLD
			half2 maskSoftness = half2(_MaskSoftnessX, _MaskSoftnessY);
			#else
			half2 maskSoftness = half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
			#endif
			o.mask = float4(v.vertex.xy * 2.0 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
		}
		#endif
		
		return o;
	}

	float4 frag(v2fFill i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 result = tex2D(_ResultTex, i.uv.xy);

		#if WRAP_CLAMP
		float4 fill = _FillTex.Sample(my_linear_clamp_sampler, i.uv.zw);
		#elif WRAP_REPEAT
		float4 fill = _FillTex.Sample(my_linear_repeat_sampler, i.uv.zw);
		#elif WRAP_MIRROR
		float4 fill = _FillTex.Sample(my_linear_mirror_sampler, i.uv.zw);
		#else
		float4 fill = tex2D(_FillTex, i.uv.zw);
		#endif

		fill = ToPremultiplied(fill) * ToPremultiplied(_Color);
		half4 blend = DoBlend(_BlendMode, result, fill);
		half4 unblend = fill * result.a;
		blend = lerp(unblend, blend, _SourceAlpha);
		fixed4 color = lerp(result, blend, _Strength);

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
			Name "Blend-Fill-Texture"
			CGPROGRAM
			#pragma vertex vertFill
			#pragma fragment frag
			ENDCG
		}
	}
}