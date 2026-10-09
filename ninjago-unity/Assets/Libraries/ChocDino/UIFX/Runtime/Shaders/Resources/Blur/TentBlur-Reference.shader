Shader "Hidden/ChocDino/UIFX/TentBlur-Reference"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_KernelRadius ("Kernel Radius", Float) = 0
	}

	CGINCLUDE
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

	sampler2D _MainTex;
	float4 _MainTex_ST;
	float4 _MainTex_TexelSize;
	float _KernelRadius;
	float _WeightsPower;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		return o;
	}

	float4 Blur1D(float2 uv, float2 texelSize)
	{
		float4 accum = tex2D(_MainTex, uv);
		float weights = 1.0;

		float2 step = texelSize;
		int m = round(_KernelRadius);
		float2 offset = step;

		for (int i = 0; i < m; i++)
		{
			float weight = pow(1.0 - ((float)(i+1)/(float)(m+1)), _WeightsPower);
			accum += tex2D(_MainTex, uv + offset) * weight;
			accum += tex2D(_MainTex, uv - offset) * weight;
			weights += weight + weight;
			offset += step;
		}

		return accum/ weights;
	}

	float4 Blur1DFrac(float2 uv, float2 texelSize)
	{
		float4 accum = tex2D(_MainTex, uv);
		float weights = 1.0;

		float2 step = texelSize;
		int m = floor(_KernelRadius);
		float2 offset = step;
		
		for (int i = 0; i < m; i++)
		{
			float weight = pow(1.0 - ((float)(i+1)/(float)(_KernelRadius+1)), _WeightsPower);
			accum += tex2D(_MainTex, uv + offset) * weight;
			accum += tex2D(_MainTex, uv - offset) * weight;
			weights += weight + weight;
			offset += step;
		}

		float remainder = _KernelRadius - m;
		{
			float2 correction = step * remainder;
			float weight = pow(1.0 - (_KernelRadius/(float)(_KernelRadius+1)), _WeightsPower);
			accum += tex2D(_MainTex, uv + (step * m + correction)) * weight;
			accum += tex2D(_MainTex, uv - (step * m + correction)) * weight;
			weights += weight + weight;
		}

		return accum / weights;
	}

	float4 fragH(v2f i) : SV_Target
	{
		return Blur1D(i.uv, float2(_MainTex_TexelSize.x, 0.0));
	}

	float4 fragV(v2f i) : SV_Target
	{
		return Blur1D(i.uv, float2(0.0, _MainTex_TexelSize.y));
	}

	float4 fragHFrac(v2f i) : SV_Target
	{
		return Blur1DFrac(i.uv, float2(_MainTex_TexelSize.x, 0.0));
	}

	float4 fragVFrac(v2f i) : SV_Target
	{
		return Blur1DFrac(i.uv, float2(0.0, _MainTex_TexelSize.y));
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
			Name "BlurHorizontal"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragH
			ENDCG
		}

		Pass
		{
			Name "BlurVertical"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragV
			ENDCG
		}

		Pass
		{
			Name "BlurHorizontal-Frac"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragHFrac
			ENDCG
		}

		Pass
		{
			Name "BlurVertical-Frac"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragVFrac
			ENDCG
		}
	}
}