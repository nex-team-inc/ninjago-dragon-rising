Shader "Hidden/ChocDino/UIFX/Blend-Frame"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}
		[PerRendererData] _FillTex ("Fill Texture", 2D) = "white" {}
		[PerRendererData] _BorderFillTex ("Border Fill Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_CutoutAlpha ("Cutout Alpha", Float) = 1
		_FillSoft ("Fill Soft", Float) = 1
		_FillColor ("Fill Color", Color) = (0.2, 0.2, 0.2, 1)
		_GradientAxisParams ("Axis", Vector) = (1, 0, 0, 0)
		_GradientParams ("Gradient", Vector) = (1, 0, 0, 0)
		_EdgeRounding ("Edge Rounding", Vector) = (0, 0, 0, 0)
		_BorderColor ("Border Color", Color) = (0.8, 0.8, 0.8, 1)
		_BorderGradientAxisParams ("Border Axis", Vector) = (1, 0, 0, 0)
		_BorderGradientParams ("Border Gradient", Vector) = (1, 0, 0, 0)
		_BorderSizeSoft ("Border Size,Soft", Vector) = (4.0, 1.0, 0.0, 0.0)
		
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local_fragment _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local_fragment __ SHAPE_CIRCLE SHAPE_ROUNDRECT SHAPE_CHAMFERRECT
	#pragma multi_compile_local_fragment __ BORDER
	#pragma multi_compile_local_fragment __ USE_TEXTURE
	#pragma multi_compile_local_fragment __ USE_BORDER_TEXTURE
	#pragma multi_compile_local_fragment __ GRADIENT_LINEAR GRADIENT_RADIAL GRADIENT_CONIC
	#pragma multi_compile_local_fragment __ GRADIENT_LINEAR_BORDER GRADIENT_RADIAL_BORDER GRADIENT_CONIC_BORDER
	//#pragma multi_compile_local __ MASK
		
	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"
	#include "Common/GradientUtils.cginc"

	struct v2fFrame
	{
		float4 vertex : SV_POSITION;
		float4 uv : TEXCOORD0;
		float4 color : TEXCOORD1;
		#ifdef UNITY_UI_CLIP_RECT
		float4 mask : TEXCOORD2;
		#endif

		UNITY_VERTEX_OUTPUT_STEREO // VR SUPPORT
	};

	uniform float4 _Rect_ST;
	uniform float _CutoutAlpha;
	uniform float4 _EdgeRounding;
	uniform float4 _FillColor;
	uniform float _FillSoft;
	#if USE_TEXTURE
	uniform sampler2D _FillTex;
	uniform float4 _GradientAxisParams;	// ScaleX, ScaleY, OffsetX, OffsetY
	uniform float4 _GradientParams;	// Radius, Offset, Scale, 0
	#endif
	#if GRADIENT_LINEAR
	uniform float4 _GradientLinearStartLine;	//PositionxXY, DirectionXY
	uniform float4 _GradientLinearParams;		//uvRectRatioXY, 0, 0
	#endif

	uniform float4 _BorderColor;
	uniform float2 _BorderSizeSoft;
	#if USE_BORDER_TEXTURE
	uniform sampler2D _BorderFillTex;
	uniform float4 _BorderGradientAxisParams;	// ScaleX, ScaleY, OffsetX, OffsetY
	uniform float4 _BorderGradientParams;	// Radius, Offset, Scale, 0
	#endif
	#if GRADIENT_LINEAR_BORDER
	uniform float4 _BorderGradientLinearStartLine;	//PositionxXY, DirectionXY
	uniform float4 _BorderGradientLinearParams;		//uvRectRatioXY, 0, 0
	#endif

	v2fFrame vertFrame(appdata v)
	{
		v2fFrame o;

		UNITY_SETUP_INSTANCE_ID(v); // VR SUPPORT
		UNITY_INITIALIZE_OUTPUT(v2fFrame, o); // VR SUPPORT
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // VR SUPPORT

		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv.xy = TRANSFORM_TEX(v.uv, _ResultTex);

		// Scale and offset for texture aspect ratio
		o.uv.zw = TRANSFORM_TEX(v.uv, _Rect);
		o.color = v.color;

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		{
			float2 pixelSize = o.vertex.w;
			pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			#ifdef MASK_SOFTNESS_OLD
			half2 maskSoftness = half2(_MaskSoftnessX, _MaskSoftnessY);
			#else
			half2 maskSoftness = half2(_UIMaskSoftnessX, _UIMaskSoftnessY);
			#endif
			o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));
		}
		#endif
		
		return o;
	}

	float RoundedRectEdges(float2 p, float2 b, float4 r)
	{
		r.xy = (p.x < 0.0) ? r.xz : r.yw;
		r.x  = (p.y > 0.0) ? r.x  : r.y;
		float2 q = abs(p) - b + r.x;
		return min(max(q.x,q.y), 0.0) + length(max(q, 0.0)) - r.x;
	}
	
	float Rectangle(float2 p, float2 b)
	{
		float2 d = abs(p) - b;
		return length(max(d, 0.0)) + min(max(d.x,d.y), 0.0);
	}
	
	float Segment(float2 p, float r )
	{
		float h = clamp( 0.5*(p.y - p.x + r), 0.0, r );
		return length( float2(p.x+h, p.y+r-h) );
	}

	float ChamferRectEdges(float2 p, float2 b, float4 r)
	{
		r.xy = (p.x<0.0)?r.xz : r.yw;
		r.x  = (p.y>0.0)?r.x  : r.y;

		float2 q = abs(p) - b;
		float e = q.x + q.y + r.x;
		float ds = Segment(q, r.x) * sign(e);
		float ne = min(max(e, 0.0), ds);
		float d = min(max(q.x,q.y),0.0) + length(max(q,0.0));
		
		return max(d, ne);
	}

	float4 fragFrame(v2fFrame i) : SV_Target
	{
		const float2 shapeUV = (i.uv.zw - 0.5) * _ResultTex_TexelSize.zw;
		const float borderSize = _BorderSizeSoft.x;
		
		// Note: This is already pre-multiplied alpha
		half4 fg = tex2D(_SourceTex, i.uv.xy);

		half4 bg = 0.0;
		{
			#if USE_TEXTURE
			float2 borderToShapeRatio = 1.0 - ((borderSize * 2.0) / _ResultTex_TexelSize.zw);
			float2 fillUV = 0.5 + (i.uv.xy - 0.5) / borderToShapeRatio;
			fillUV.x = (fillUV.x * _GradientAxisParams.x + _GradientAxisParams.z) + (fillUV.y * _GradientAxisParams.y + _GradientAxisParams.w);
			#if GRADIENT_RADIAL
			{
				float2 ratio = float2(_ResultTex_TexelSize.y / _ResultTex_TexelSize.x, 1.0);
				float2 uv = (i.uv.xy - 0.5) * 2.0 * ratio;
				fillUV.x = length(uv - 0.0) / _GradientParams.x;
			}
			#elif GRADIENT_CONIC
			{
				float HalfTurn = 3.141592654;
				float FullTurn = HalfTurn * 2.0;
				float2 uv = float2((1.0-i.uv.x)-0.5, (1.0-i.uv.y)-0.5); //TODO: make the rotation pivot a parameter
				fillUV.x = (atan2(uv.x, uv.y) + HalfTurn) / FullTurn;
			}
			#elif GRADIENT_LINEAR
			{
				// Scale UV by aspect ratio
				float2 uv = i.uv * float2(_GradientLinearParams.y, 1.0);

				float2 startPoint = _GradientLinearStartLine.xy;
				float2 startDir = _GradientLinearStartLine.zw;
				float dir = dot(uv - startPoint, startDir);
				float2 closestPoint = startPoint + startDir * dir;
				fillUV.x = distance(closestPoint, uv) / _GradientLinearParams.x;
			}
			#endif
			fillUV.x = WrapRepeat(fillUV.x, _GradientParams.z, 0.5, _GradientParams.y);
			bg = ToPremultiplied(tex2D(_FillTex, fillUV)) * _FillColor.a;
			#else
			// Note: This is already pre-multiplied alpha
			bg = ToPremultiplied(_FillColor);
			#endif
		}

		float fillMask = 0.0;
		float borderMask = 0.0;

		#if SHAPE_CIRCLE
		{
			float radius = (_ResultTex_TexelSize.z - (borderSize * 2.0)) / 2.0;
			float borderEdgeDistance = length(shapeUV) - (radius + borderSize);

			#if BORDER
				float borderAA = saturate(0.5 - borderEdgeDistance);
				borderMask = lerp(0.0, 1.0, saturate(-(borderEdgeDistance - 0.0) / _BorderSizeSoft.y));
				borderMask = lerp(borderAA, borderMask, saturate(_BorderSizeSoft.y));
			#endif

			float fillEdgeDistance = (borderEdgeDistance + borderSize);
			float fillAA = saturate(0.5 - fillEdgeDistance);
			fillMask = lerp(0.0, 1.0, saturate(-(fillEdgeDistance - 0.0) / _FillSoft));
			fillMask = lerp(fillAA, fillMask, saturate(_FillSoft));
		}
		#elif SHAPE_ROUNDRECT
		{
			float2 size = (_ResultTex_TexelSize.zw - (borderSize * 2.0)) / 2.0;
			float borderEdgeDistance = RoundedRectEdges(shapeUV, size + borderSize, _EdgeRounding);

		#if BORDER
			float borderAA = saturate(0.5 - borderEdgeDistance);
			borderMask = lerp(0.0, 1.0, saturate(-(borderEdgeDistance - 0.0) / _BorderSizeSoft.y));
			borderMask = lerp(borderAA, borderMask, saturate(_BorderSizeSoft.y));
		#endif

			float fillEdgeDistance = (borderEdgeDistance + borderSize);
			float fillAA = saturate(0.5 - fillEdgeDistance);
			fillMask = lerp(0.0, 1.0, saturate(-(fillEdgeDistance - 0.0) / _FillSoft));
			fillMask = lerp(fillAA, fillMask, saturate(_FillSoft));
		}
		#elif SHAPE_CHAMFERRECT
		{
			float2 size = (_ResultTex_TexelSize.zw - (borderSize * 2.0)) / 2.0;
			float borderEdgeDistance = ChamferRectEdges(shapeUV, size + borderSize, _EdgeRounding);

		#if BORDER
			float borderAA = saturate(0.5 - borderEdgeDistance);
			borderMask = lerp(0.0, 1.0, saturate(-(borderEdgeDistance - 0.0) / _BorderSizeSoft.y));
			borderMask = lerp(borderAA, borderMask, saturate(_BorderSizeSoft.y));
		#endif

			float fillEdgeDistance = (borderEdgeDistance + borderSize);
			float fillAA = saturate(0.5 - fillEdgeDistance);
			fillMask = lerp(0.0, 1.0, saturate(-(fillEdgeDistance - 0.0) / _FillSoft));
			fillMask = lerp(fillAA, fillMask, saturate(_FillSoft));
		}
		#else
		{
			float2 size = (_ResultTex_TexelSize.zw - (borderSize * 2.0)) / 2.0;

			float fillEdgeDistance = Rectangle(shapeUV, size);
			float fillAA = saturate(0.5 - fillEdgeDistance);
			fillMask = lerp(0.0, 1.0, saturate(-(fillEdgeDistance - 0.0) / _FillSoft));
			fillMask = lerp(fillAA, fillMask, saturate(_FillSoft));

			#if BORDER
				float borderEdgeDistance = Rectangle(shapeUV, _ResultTex_TexelSize.zw * 0.5);
				float borderAA = saturate(0.5 - borderEdgeDistance);
				borderMask = lerp(0.0, 1.0, saturate(-(borderEdgeDistance - 0.0) / _BorderSizeSoft.y));
				borderMask = lerp(borderAA, borderMask, saturate(_BorderSizeSoft.y));
			#endif
		}
		#endif

		// Composite
		half4 color = AlphaComp_Over(fg, bg);

		// Cutout
		color = lerp(color, AlphaComp_Out(bg, fg), _CutoutAlpha);

		const half4 backcolor = 0.0;
		
		#if BORDER
		{
			#if USE_BORDER_TEXTURE
			// TODO: account for rounded corners to scale the UV coordinates for diagonal gradients
			float2 fillUV = 0.5 + (i.uv.xy - 0.5);
			fillUV.x = (fillUV.x * _BorderGradientAxisParams.x + _BorderGradientAxisParams.z) + (fillUV.y * _BorderGradientAxisParams.y + _BorderGradientAxisParams.w);
			#if GRADIENT_RADIAL_BORDER
				float2 ratio = float2(_ResultTex_TexelSize.y / _ResultTex_TexelSize.x, 1.0);
				float2 uv = (i.uv.xy - 0.5) * 2.0 * ratio;
				fillUV.x = length(uv - 0.0) / _BorderGradientParams.x;
			#elif GRADIENT_CONIC_BORDER
			{
				float HalfTurn = 3.141592654;
				float FullTurn = HalfTurn * 2.0;
				float2 uv = float2((1.0-i.uv.x)-0.5, (1.0-i.uv.y)-0.5); //TODO: make the rotation pivot a parameter
				float2 ratio = float2(_ResultTex_TexelSize.y / _ResultTex_TexelSize.x, 1.0);
//				uv *= ratio;
				fillUV.x = (atan2(uv.x, uv.y) + HalfTurn) / FullTurn;
			}
			#elif GRADIENT_LINEAR_BORDER
			{
				// Scale UV by aspect ratio
				float2 uv = i.uv * float2(_BorderGradientLinearParams.y, 1.0);

				float2 startPoint = _BorderGradientLinearStartLine.xy;
				float2 startDir = _BorderGradientLinearStartLine.zw;
				float dir = dot(uv - startPoint, startDir);
				float2 closestPoint = startPoint + startDir * dir;
				fillUV.x = distance(closestPoint, uv) / _BorderGradientLinearParams.x;
			}
			#endif
			fillUV.x = WrapRepeat(fillUV.x, _BorderGradientParams.z, 0.5, _BorderGradientParams.y);
			half4 bordercolor = ToPremultiplied(tex2D(_BorderFillTex, fillUV)) * _BorderColor.a;
			#else
			half4 bordercolor = ToPremultiplied(_BorderColor);
			#endif

			color = lerp(bordercolor * saturate(borderSize), color, fillMask);
			color *= borderMask;
			
		}
		#else
			color *= fillMask;
		#endif

		


		// Mask with the background
		//#if (MASK)
		//color = ToStraight(color);
		//color.a = min(color.a, bg.a);
		//color = ToPremultiplied(color);
		//#endif

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
			Name "Blend-Frame"
			CGPROGRAM
			#pragma vertex vertFrame
			#pragma fragment fragFrame
			ENDCG
		}
	}
}