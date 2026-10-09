Shader "Hidden/ChocDino/UIFX/MotionBlur-Resolve"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _MainTex2 ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

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
		ZTest Always
		//Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		//Blend One One // Additive
		ColorMask [_ColorMask]

		Pass
		{
			Name "Default"
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP

			// Actually 2019.4.18 but UNITY_VERSION doesn't support double digits
			#if UNITY_VERSION < 201949
				#define MASK_SOFTNESS_OLD
			#endif

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				float2 texcoord  : TEXCOORD0;
				float4 color : TEXCOORD1;
				#ifdef UNITY_UI_CLIP_RECT
				float4 mask : TEXCOORD2;
				#endif
			};

			sampler2D _MainTex;
			sampler2D _MainTex2;
			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _MainTex_ST;
			float4 _MainTex2_ST;
			float _InvSampleCount;

			float4 _ClipRect;
			#ifdef MASK_SOFTNESS_OLD
			float _MaskSoftnessX;
			float _MaskSoftnessY;
			#else
			float _UIMaskSoftnessX;
			float _UIMaskSoftnessY;
			#endif
			
			v2f vert(appdata_t IN)
			{
				v2f OUT;
				UNITY_INITIALIZE_OUTPUT(v2f, OUT);
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex2);
				OUT.color = IN.color;

				// 2D rect clipping
				#ifdef UNITY_UI_CLIP_RECT
				{
					float2 pixelSize = OUT.vertex.w;
					pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

					float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
					#ifdef MASK_SOFTNESS_OLD
					half2 maskSoftness = half2(_MaskSoftnessX, _MaskSoftnessY);
					#else
					half2 maskSoftness = half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
					#endif
					OUT.mask = float4(IN.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
				}
				#endif

				return OUT;
			}


			float4 frag(v2f IN) : SV_Target
			{
				// Source texture should always be linear
				float4 color = tex2D(_MainTex2, IN.texcoord);

				// Average
				color.rgba *= _InvSampleCount;

				// Vertex colour, pre-multiply it
				color.rgb *= IN.color.rgb * IN.color.a;
				color.a *= IN.color.a;

				// 2D rect clipping
				#ifdef UNITY_UI_CLIP_RECT
				half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
				color.a *= m.x * m.y;
				color.rgb *= m.x * m.y;
				#endif

				// Alpha clipping
				#ifdef UNITY_UI_ALPHACLIP
				clip (color.a - 0.001);
				#endif

				#if UNITY_COLORSPACE_GAMMA
				//color.rgb = LinearToGammaSpace(color.rgb);
				#endif

				#if !UNITY_COLORSPACE_GAMMA
				//color.rgb = GammaToLinearSpace(color.rgb);
				#endif

				//color.rgb = LinearToGammaSpace(color.rgb);
				//color.rgb *= color.a;

				// Seems to be fine to output linear value
				return color;
			}
		ENDCG
		}
	}
}