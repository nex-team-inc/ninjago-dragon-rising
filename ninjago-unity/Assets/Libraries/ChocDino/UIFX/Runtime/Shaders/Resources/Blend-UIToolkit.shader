Shader "Hidden/ChocDino/UIFX/Blend-UIToolkit"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP

	#include "UnityCG.cginc"
	#include "BlendUtils.cginc"

	v2f vertResolve(appdata v)
	{
		v2f o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2f, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
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

	float4 frag(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 color = tex2D(_MainTex, i.uv);

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
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		ColorMask [_ColorMask]

		Pass
		{
			Name "Normal"
			CGPROGRAM
			#pragma vertex vertResolve
			#pragma fragment frag
			ENDCG
		}
	}
}