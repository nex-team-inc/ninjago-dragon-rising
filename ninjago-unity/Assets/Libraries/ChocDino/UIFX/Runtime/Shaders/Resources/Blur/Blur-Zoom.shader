Shader "Hidden/ChocDino/UIFX/Blur-Zoom"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_CenterInvScale ("Center InvScale", Vector) = (0.0, 0.0, 0.0, 0.0)
		_Dither ("Dither", Float) = 0.0
		_WeightsPower ("Weights Power", Float) = 1.0
	}

	CGINCLUDE

	#pragma multi_compile_local_fragment _ USE_DITHER
	#pragma multi_compile_local_fragment _ DIR_BOTH

	#include "UnityCG.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	uniform sampler2D _MainTex;
	uniform float4 _MainTex_ST;
	uniform float4 _MainTex_TexelSize;
	uniform float3 _CenterInvScale;
	uniform float _Dither;
	uniform float _WeightsPower;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		return o;
	}

	float hash13(float3 p3)
	{
		p3 = frac(p3 * .1031);
		p3 += dot(p3, p3.yzx + 19.19);
		return frac((p3.x + p3.y) * p3.z);
	}

	float4 frag(v2f i) : SV_Target
	{
		float2 srcUV = i.uv.xy;
		float2 dstUV = ((srcUV - _CenterInvScale.xy) * _CenterInvScale.z) + _CenterInvScale.xy;

		float2 srcTexel = srcUV * _MainTex_TexelSize.zw;
		float2 dstTexel = dstUV * _MainTex_TexelSize.zw;

		float texelDistance = length(dstTexel - srcTexel);

		int m = floor(texelDistance);
		float2 step = normalize(dstTexel - srcTexel) * _MainTex_TexelSize.xy;

		float4 accum = tex2Dlod(_MainTex, float4(srcUV, 0.0, 0.0));

		float2 uv = srcUV;
		float2 offset = step;
		[loop]
		for (int j = 0; j < m; j++)
		{
			accum += tex2Dlod(_MainTex, float4(uv + offset, 0.0, 0.0));

			#if DIR_BOTH
			accum += tex2Dlod(_MainTex, float4(uv - offset, 0.0, 0.0));
			#endif

			offset += step;
		}

		float remainder = texelDistance - m;
		{
			float2 correction = step * remainder;
			accum += tex2Dlod(_MainTex, float4(uv + (step * m + correction), 0.0, 0.0)) * remainder;
			#if DIR_BOTH
			accum += tex2Dlod(_MainTex, float4(uv - (step * m + correction), 0.0, 0.0)) * remainder;
			#endif
		}

		float total = 1.0 + texelDistance;
		#if DIR_BOTH
		total += texelDistance;
		#endif

		accum /= total;

		#if USE_DITHER

		// Add noise for dithering
		float noise = (hash13(float3(i.uv.xy * 2566.0, _Time.w)) - 0.5) * _Dither;

		// Modulate noise by brightness, adding more noise to darker areas
		float3 accumy = pow(accum.rgb, 1.0/2.2);
		//float bright = max(max(accumy.r, accumy.g), accumy.b);
		float bright = saturate(accumy.r + accumy.g + accumy.b) / 3.0;
		float luma = saturate(bright);
		luma *= accum.a;
		luma = 1.0-pow(luma, 1.0/2.2);
		luma *= bright;

		accum += noise * luma;
		#endif

		return accum;
	}
		
	float4 fragWeighted(v2f i) : SV_Target
	{
		float2 srcUV = i.uv.xy;
		float2 dstUV = ((srcUV - _CenterInvScale.xy) * _CenterInvScale.z) + _CenterInvScale.xy;

		float2 srcTexel = srcUV * _MainTex_TexelSize.zw;
		float2 dstTexel = dstUV * _MainTex_TexelSize.zw;

		float texelDistance = length(dstTexel - srcTexel);

		int m = floor(texelDistance);
		float2 step = normalize(dstTexel - srcTexel) * _MainTex_TexelSize.xy;

		float w = 1.0;
		float4 accum = tex2Dlod(_MainTex, float4(srcUV, 0.0, 0.0)) * w;
		float total = w;

		float2 uv = srcUV;
		float2 offset = step;

		[loop]
		for (int j = 1; j < m; j++)
		{
			w = pow(1.0 - ((float)j/(float)m), _WeightsPower);

			accum += tex2Dlod(_MainTex, float4(uv + offset, 0.0, 0.0)) * w;
			total += w;
			
			#if DIR_BOTH
			accum += tex2Dlod(_MainTex, float4(uv - offset, 0.0, 0.0)) * w;
			total += w;
			#endif

			offset += step;
		}

		// TODO: remainder?

		accum /= total;

		#if USE_DITHER

		// Add noise for dithering
		float noise = (hash13(float3(i.uv.xy * 2566.0, _Time.w)) - 0.5) * _Dither;

		// Modulate noise by brightness, adding more noise to darker areas
		float3 accumy = pow(accum.rgb, 1.0/2.2);
		//float bright = max(max(accumy.r, accumy.g), accumy.b);
		float bright = saturate(accumy.r + accumy.g + accumy.b) / 3.0;
		float luma = saturate(bright);
		luma *= accum.a;
		luma = 1.0-pow(luma, 1.0/2.2);
		luma *= bright;

		accum += noise * luma;
		#endif

		return accum;
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

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Blur-Zoom"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}

		Pass
		{
			Name "Blur-Zoom-Weighted"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragWeighted
			ENDCG
		}
	}
}
