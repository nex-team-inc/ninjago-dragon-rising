Shader "Hidden/ChocDino/UIFX/LongShadow"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
	}

	CGINCLUDE

	#include "UnityCG.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		float4 colorFront : TEXCOORD1;
		float4 colorBack : TEXCOORD2;
	};

	sampler2D _DistanceTex;
	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	float2 _OffsetStart;
	int _Length;
	float2 _PixelStep;
	float4 _ColorFront;
	float4 _ColorBack;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv + _OffsetStart;
		#ifdef UNITY_COLORSPACE_GAMMA
		o.colorFront = ToPremultiplied(_ColorFront);
		o.colorBack = ToPremultiplied(_ColorBack);
		#else
		o.colorFront = StraightGammaToPremultipliedLinear(_ColorFront);
		o.colorBack = StraightGammaToPremultipliedLinear(_ColorBack);
		#endif
		return o;
	}

	float4 GetLongShadow(v2f i)
	{	
		float2 uv = i.uv;

		float alphaMask = 0.0;
		float distT = 0.0;
		float distance = 0.0;

		// Allow minimum 1 sample so that Shadow comp mode can display something at length == 0
		_Length = max(1, _Length);

		[loop]
		for (int ii = 1; ii <= _Length; ii++)
		{
			float mask = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).a;

			float t = 1.0 - saturate(distance / _Length);
			distT = max(distT, mask * t);
			alphaMask = max(alphaMask, mask);

			if (alphaMask >= 1.0) break;

			distance += 1.0;
			uv += _PixelStep;
		}

		return lerp(i.colorBack, i.colorFront, distT) * alphaMask;
	}

	float4 GetLongShadowDistanceMap(v2f i)
	{	
		//return float4(tex2Dlod(_DistanceTex, float4(i.uv, 0.0, 0.0)).xxx/1000, 1.0);
		float alphaMask = 0.0;
		float distT = 0.0;
		float distance = 0.0;

		// Allow minimum 1 sample so that Shadow comp mode can display something at length == 0
		_Length = max(1, _Length);

		float minDist = 4.0;//length(_PixelStep * _MainTex_TexelSize.zw) * 2.0;

		// Use "circle tracing" to jump through the distance map
		//float jumpSize = 1.0;

		[loop]
		for (int ii = 1; ii <= _Length; ii++)
		{
			float2 uv = i.uv + _PixelStep * distance;

			// Early-out, ray marched out of UV space
			if (uv.y > 1.0 || uv.y < 0.0 || uv.x > 1.0 || uv.x < 0.0)
			{
				break;
			}

			// Get the distance to the closest point on the surface
			float distanceToSurface = tex2Dlod(_DistanceTex, float4(uv, 0.0, 0.0)).x;

			// If we're really close to the closest point (2 pixels), then sample it's alpha (accumulate)
			if (distanceToSurface <= minDist)
			{
				float mask = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).a;

				float t = 1.0-(saturate((distance) / _Length));
				distT = max(distT, mask * t);
				alphaMask = max(alphaMask, mask);

				// Early out once alpha is full
				if (alphaMask >= 1.0)
				{
					break;
				}
			}
			else
			{
				//distanceToSurface *= jumpSize;
			}

			// Ensure the ray always moves at least 1 pixel when the distance to surface is too small
			distanceToSurface = max(1.0, distanceToSurface);

			distance += distanceToSurface;

			// Walked too far, ray didn't hit enough
			if (distance > _Length)
			{
				break;
			}
		}

		return lerp(i.colorBack, i.colorFront, distT) * alphaMask;
	}

	float4 frag(v2f i) : SV_Target
	{
		return GetLongShadow(i);
	}

	float4 fragDistanceMap(v2f i) : SV_Target
	{
		return GetLongShadowDistanceMap(i);
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
			Name "LongShadow-Normal"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
		Pass
		{
			Name "LongShadow-DistanceMap"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragDistanceMap
			ENDCG
		}
	}
}