Shader "Hidden/ChocDino/UIFX/Extrude"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _GradientTex ("Gradient Texture", 2D) = "grey" {}
		_Length ("Length", float) = 32.0
		_PixelStep("Pixel Step", Vector) = (0.01, 0.01, 0.0, 0.0)
		_VanishingPoint("Vanishing Point", Vector) = (0.5, 0.5, 0.0, 0.0)
		_Ratio("Ratio", Vector) = (1.0, 1.0, 0.0, 0.0)
		_ColorFront ("Color Front", Vector) = (0.5, 0.5, 0.5, 1)
		_ColorBack ("Color Back", Vector) = (0, 0, 0, 0)
		_ReverseFill ("Reverse Fill", float) = 0.0
		_Scroll ("Scroll", float) = 0.0
	}

	CGINCLUDE

	#include "UnityCG.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"
	#include "Common/GradientUtils.cginc"

	#pragma multi_compile_local_fragment __ USE_GRADIENT_TEXTURE
	#pragma multi_compile_local_fragment __ MULTIPLY_SOURCE_COLOR

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

	uniform sampler2D _GradientTex;
	uniform sampler2D _MainTex;
	uniform float4 _MainTex_TexelSize;
	uniform int _Length;
	uniform float2 _PixelStep;
	uniform float2 _VanishingPoint;
	uniform float3 _Ratio;
	uniform float4 _ColorFront;
	uniform float4 _ColorBack;
	uniform float _ReverseFill;
	uniform float _Scroll;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		#ifdef UNITY_COLORSPACE_GAMMA
		o.colorFront = ToPremultiplied(_ColorFront);
		o.colorBack = ToPremultiplied(_ColorBack);
		#else
		o.colorFront = StraightGammaToPremultipliedLinear(_ColorFront);
		o.colorBack = StraightGammaToPremultipliedLinear(_ColorBack);
		#endif
		return o;
	}

	void GetExtrudeDistanceColor_Perspective(v2f i, out float oDistance, out float4 oColor)
	{
		float2 uv = i.uv;

		float4 maxValues = 0.0;
		float distT = 0.0;
		float distance = 0.0;

		// Allow minimum 1 sample so that Shadow comp mode can display something at length == 0
		_Length = max(1, _Length);

		float2 center = float2(0.5, 0.5);

		float2 from = i.uv - center;
		float2 to = _VanishingPoint	- center;		// in UV space

		from *= _MainTex_TexelSize.xy;
		to *= _MainTex_TexelSize.xy;

		float textureAspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;

		float2 pixelStep = (from-to) * float2(textureAspect, 1.0);

		float2 scaledPixelStep = normalize(pixelStep * _MainTex_TexelSize.zw) * _MainTex_TexelSize.xy * 0.5;

		float pixelStepRatio = length(pixelStep) / length(scaledPixelStep);

		float scaledLength = _Length * _Ratio.z * pixelStepRatio;

		// NOTE: Found odd instance when text font size = 1 would cause hanging because the length would get too large - but not 100% sure how.
		scaledLength = min(1024.0, scaledLength);
	
		// TODO: fractional length
		[loop]
		for (int ii = 1; ii <= scaledLength; ii++)
		{
			// Early-out, ray marched out of UV space
			if (uv.y > 1.0 || uv.y < 0.0 || uv.x > 1.0 || uv.x < 0.0)
			{
				break;
			}
			
			float4 color = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0));
			float mask = color.a;
			
			float t = 1.0 - saturate(distance / scaledLength);
			distT = max(distT, mask * t);
			maxValues = max(maxValues, color);
		
			if (mask >= 1.0)
			{
				break;
			}

			distance += 1.0;
			uv += scaledPixelStep;
		}

		oDistance = distT;
		oColor = maxValues;
	}

	void GetExtrudeDistanceColor_Orthographic(v2f i, out float oDistance, out float4 oColor)
	{
		float2 uv = i.uv;

		float4 maxValues = 0.0;
		float distT = 0.0;
		float distance = 0.0;

		// Allow minimum 1 sample so that Shadow comp mode can display something at length == 0
		_Length = max(1, _Length);

		// TODO: fractional length
		[loop]
		for (int ii = 1; ii <= _Length; ii++)
		{
			// Early-out, ray marched out of UV space
			if (uv.y > 1.0 || uv.y < 0.0 || uv.x > 1.0 || uv.x < 0.0)
			{
				break;
			}

			float4 color = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0));
			float mask = color.a;
			
			float t = 1.0 - saturate(distance / _Length);
			distT = max(distT, mask * t);
			maxValues = max(maxValues, color);
		
			if (mask >= 1.0)
			{
				break;
			}

			distance += 1.0;
			uv += _PixelStep;
		}

		oDistance = distT;
		oColor = maxValues;
	}

	float4 fragPerspective(v2f i) : SV_Target
	{
		float4 sampleColor = 0;
		float normDistance = 0;
		GetExtrudeDistanceColor_Perspective(i, normDistance, sampleColor);

		// Reverse the fill or not
		normDistance = abs(_ReverseFill - normDistance);

		float4 color = 0;

		#if USE_GRADIENT_TEXTURE
		float gt = 1.0 - normDistance;
		gt = WrapMirror(gt, 1.0, 0.5, _Scroll);
		color = ToPremultiplied(tex2D(_GradientTex, float2(gt, 0.0)));
		#else
		float gt = 1.0 - normDistance;
		gt = WrapMirror(gt, 1.0, 0.5, _Scroll);
		color = lerp(i.colorFront, i.colorBack, gt);
		#endif

		#if MULTIPLY_SOURCE_COLOR
		color *= sampleColor;
		#else
		color *= sampleColor.a;
		#endif

		return color;
	}
	
	float4 fragOrthographic(v2f i) : SV_Target
	{
		float4 sampleColor = 0;
		float normDistance = 0;
		GetExtrudeDistanceColor_Orthographic(i, normDistance, sampleColor);

		// Reverse the fill or not
		normDistance = abs(_ReverseFill - normDistance);

		float4 color = 0;

		#if USE_GRADIENT_TEXTURE
		float gt = 1.0 - normDistance;
		gt = WrapMirror(gt, 1.0, 0.5, _Scroll);
		color = ToPremultiplied(tex2D(_GradientTex, float2(gt, 0.0)));
		#else
		float gt = 1.0 - normDistance;
		gt = WrapMirror(gt, 1.0, 0.5, _Scroll);
		color = lerp(i.colorFront, i.colorBack, gt);
		#endif

		#if MULTIPLY_SOURCE_COLOR
		color *= sampleColor;
		#else
		color *= sampleColor.a;
		#endif

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

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "Extrude-Perspective"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragPerspective
			ENDCG
		}
		Pass
		{
			Name "Extrude-Orthographic"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragOrthographic
			ENDCG
		}
	}
}