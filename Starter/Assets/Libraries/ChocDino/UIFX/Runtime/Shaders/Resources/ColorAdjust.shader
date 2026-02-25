Shader "Hidden/ChocDino/UIFX/ColorAdjust"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_BCPO ("BCPO", Vector) = (0.0, 0.0, 255.0, 1.0)
		_BrightnessRGBA ("Brightness RGBA", Vector) = (0.0, 0.0, 0.0, 0.0)
		_ContrastRGBA ("Contrast RGBA", Vector) = (0.0, 0.0, 0.0, 0.0)
		_PosterizeRGBA ("Posterize RGBA", Vector) = (255.0, 255.0, 255.0, 255.0)
	}

	CGINCLUDE

	#pragma multi_compile_local_fragment __ POSTERIZE

	#include "UnityCG.cginc"
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
	};

	uniform sampler2D _MainTex;
	uniform	float4 _MainTex_ST;

	uniform half4x4 _ColorMatrix;
	uniform half4 _BCPO; //Brightness, Contrast, Posterize, Opacity
	uniform half4 _BrightnessRGBA;
	uniform half4 _ContrastRGBA;
	uniform half4 _PosterizeRGBA;

	v2f vert (appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.uv, _MainTex);
		return o;
	}

	half4 frag(v2f i) : SV_Target
	{
		half4 color = tex2D(_MainTex, i.uv);

		half4 result = float4(ToStraight(color).rgb, 1.0h);

		#if UNITY_COLORSPACE_GAMMA
		// HSV matrix transform looks better in linear colorspace
		result.rgb = GammaToLinearSpace(result.rgb);
		#endif

		// Apply HSV+CB matrix
		// Note: we have to set alpha channel to 1.0 otherwise brightness of zero leaves an outline
		// Note: we have to multiply by RGBA otherwise the brightness component doesn't get applied
		result.rgb = saturate(mul(_ColorMatrix, result).rgb);

		// Other color transforms look better in gamma colorspace
		result.rgb = LinearToGammaSpace(result.rgb);

		// Contrast
		result.rgb = saturate(((result.rgb - 0.5h) * (_BCPO.y + 1.0h)) + 0.5h);

		// Brightness
		result.rgb = saturate(result.rgb + _BCPO.x);

		// Restore alpha
		result.a = color.a;

		// Posterize
		// Note: this check if here because for some reason even if Posterize is set to 255.0, there
		// is some sort of minor adjustment that happens sometimes randomly - not visible unless you apply
		// the LongShadow filter..Need to investigate this.
		#if POSTERIZE
		result = round(result * _BCPO.z) / _BCPO.z;
		#endif

		// Apply per-channel adjustments
		result = saturate(((result - 0.5h) * (_ContrastRGBA + 1.0h)) + 0.5h);
		result = saturate(result + _BrightnessRGBA);
		#if POSTERIZE
		result = round(result * _PosterizeRGBA) / _PosterizeRGBA;
		#endif

		// Restore gamma
		#if !UNITY_COLORSPACE_GAMMA
		result.rgb = GammaToLinearSpace(result.rgb);
		#endif

		result = ToPremultiplied(result);

		// Opacity
		return result * _BCPO.w;
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
		
			Name "ColorAdjust"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}