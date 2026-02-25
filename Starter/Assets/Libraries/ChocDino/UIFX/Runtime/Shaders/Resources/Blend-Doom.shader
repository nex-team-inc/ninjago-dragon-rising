Shader "Hidden/ChocDino/UIFX/Blend-Doom"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}
		[PerRendererData] _TimingTex ("Timing Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_Timing ("Timing", Float) = 0

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP

	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	uniform sampler2D _TimingTex;
	uniform float _Timing;

	half4 fragDoom(v2f i) : SV_Target
	{
		// column is uv.x scaled relative to the ratio between result and timing texture
		const float TimingTextureWidth = 1024.0;
		float column = (i.uv.x * _ResultTex_TexelSize.z) / TimingTextureWidth;
		
		// timeDelay should be in 0..1 range
		float timeDelay = tex2D(_TimingTex, half2(column, 0.0)).a;

		//timeDelay = (timeDelay - 0.5) * 2.0;

		float oy = i.uv.y;

		float time = (abs(_Timing * 2.0) - timeDelay);
		float offset = saturate(time) * sign(_Timing);
		//offset = saturate((_Timing * 2.0) + timeDelay) * sign(timeDelay);
		i.uv.y += offset;

		// Clip past UV range
		if (i.uv.y < 0.0 || i.uv.y > 1.0)
		{
			return 0;
		}

		// Note: This is already pre-multiplied alpha
		half4 color = tex2D(_SourceTex, i.uv);

		/*half factor = 0.95;
		half scale = 1.0 / (1.0 - factor);
		half fade = 1.0-(saturate((1.0-oy) - factor) * scale);
		color *= fade;
		half fade2 = 1.0-(saturate((oy) - factor) * scale);
		color *= fade2;*/
	
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
			Name "Blend-Doom"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDoom
			ENDCG
		}
	}
}
