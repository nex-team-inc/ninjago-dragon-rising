Shader "Hidden/ChocDino/UIFX/MotionBlur-Additive"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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
		}

		Blend One One, One One
		BlendOp Add, Add
		Cull Off
		Zwrite Off
		ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _TextureSampleAdd;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float4 col = tex2D(_MainTex, i.uv);

				// In gamma mode we have to manually convert to linear
				#if UNITY_COLORSPACE_GAMMA
				// NOTE: I've commented this out in Unity 2019.4...not sure if it's needed in other Unity versions (need to test)
				// I'm not sure why we wouldn't need to convert from gamma to linear space...
				//col.rgb = GammaToLinearSpace(col.rgb);
				#endif
				
				// Adjustment for Alpha8 texture used by fonts
				col += _TextureSampleAdd;

				// Vertex colour
				#if UNITY_COLORSPACE_GAMMA
				col.rgba *= i.color.rgba;
				#else
				col.rgb *= GammaToLinearSpace(i.color.rgb);
				col.a *= i.color.a;
				#endif

				// Premultiply (not sure why? but seems to fix dark fringing issues)
				col.rgb *= col.a;

				return col;
			}
			ENDCG
		}
	}
}
