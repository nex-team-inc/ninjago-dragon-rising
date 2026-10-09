Shader "Hidden/ChocDino/UIFX/Blur-Directional"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_KernelRadius ("Kernel Radius", Float) = 0.0
		_TexelStep ("Texel Step", Vector) = (0.0, 0.0, 0.0, 0.0)
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
	uniform float _KernelRadius;
	uniform float2 _TexelStep;
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
		float4 accum = tex2D(_MainTex, i.uv);

		float2 uv = i.uv.xy;
		float2 step = _TexelStep;
		float2 offset = step;

		int m = floor(_KernelRadius);
		for (int j = 0; j < m; j++)
		{
			#if DIR_BOTH
			accum += tex2D(_MainTex, uv - offset);
			#endif
			accum += tex2D(_MainTex, uv + offset);
			offset += step;
		}

		float remainder = _KernelRadius - m;
		if (remainder > 0.0)
		{
			float2 correction = step * remainder;
			#if DIR_BOTH
			accum += tex2D(_MainTex, uv - (step * m + correction)) * remainder;
			#endif
			accum += tex2D(_MainTex, uv + (step * m + correction)) * remainder;
		}

		float total = 1.0 + _KernelRadius;
		#if DIR_BOTH
		total += _KernelRadius;
		#endif

		accum /= total;

		#if USE_DITHER

		// Add noise for dithering
		float noise = (hash13(float3(i.uv.xy * 2566.0, _Time.x)) - 0.5) * _Dither;

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
		float4 accum = tex2D(_MainTex, i.uv);

		float2 uv = i.uv.xy;
		float2 step = _TexelStep;
		float2 offset = step;
		float total = 1.0;

		int m = floor(_KernelRadius);
		for (int j = 1; j < m; j++)
		{
			float w = pow(1.0 - ((float)j/(float)m), _WeightsPower);

			#if DIR_BOTH
			accum += tex2D(_MainTex, uv - offset) * w;
			total += w;
			#endif

			accum += tex2D(_MainTex, uv + offset) * w;
			total += w;

			offset += step;
		}

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

	/*float4 fragWeightedColored(v2f i) : SV_Target
	{
		float4 accum = tex2D(_MainTex, i.uv);

		float2 uv = i.uv.xy;
		float2 step = _TexelStep;
		float2 offset = step;
		float total = 1.0;

		float4 c0 = float4(0, 0, 1, 1);
		float4 c1 = float4(1, 0, 0, 1);
		float4 cz = float4(1, 1, 1, 1);

		float4 col0 = float4(1, 0, 0, 1);
		float4 col1 = float4(0, 0, 1, 1);
		float4 colt = float4(1, 1, 1, 1);

		int m = floor(_KernelRadius);
		for (int j = 0; j < m; j++)
		{
			float w = 1.0 - ((float)j/(float)m);

			float4 cola = tex2D(_MainTex, uv - offset);
			float4 colb = tex2D(_MainTex, uv + offset);

			cola = lerp(col0 * cola, cola * colt, w);
			colb = lerp(col1 * colb, colb * colt, w);

			cola *= w;
			colb *= w;

			accum += cola;
			accum += colb;

			offset += step;
			total += w + w;
		}

		return accum / total;
	}*/

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
			Name "Blur-Directional"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}

		Pass
		{
			Name "Blur-Directional-Weighted"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragWeighted
			ENDCG
		}
	}
}
