//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	public enum FrameShape
	{
		Rectangle,
		Square,
		Circle,
	}

	[System.Serializable]
	public struct RectPadToEdge
	{
		public bool left, right;
		public bool top, bottom;
	}

	[System.Serializable]
	public struct RectEdge
	{
		public RectEdge(float value)
		{
			left = right = top = bottom = value;
		}

		public float left;
		public float right;
		public float top;
		public float bottom;

		public Vector4 ToVector()
		{
			return new Vector4(left, right, top, bottom);
		}
	}

	[System.Serializable]
	public struct RectCorners
	{
		public RectCorners(float value)
		{
			topLeft = topRight = bottomLeft = bottomRight = value;
		}

		public float topLeft;
		public float topRight;
		public float bottomLeft;
		public float bottomRight;

		public bool IsZero()
		{
			return (topLeft <= 0f && topRight <= 0f && bottomLeft <= 0f && bottomRight <= 0f);
		}

		public Vector4 ToVector()
		{
			return new Vector4(topLeft, topRight, bottomLeft, bottomRight);
		}
	}

	public enum FrameRoundCornerMode
	{
		None,
		Small,
		Medium,
		Large,
		Circular,
		Percent,
		CustomPercent,
		Pixels,
		CustomPixels,
	}

	public enum FrameCornerShape
	{
		Round,
		Chamfer,
	}

	public enum FrameFillMode
	{
		None,
		Color,
		Texture,
		Gradient,
	}

	public enum FrameGradientShape
	{
		Horizontal,
		Vertical,
		Diagonal,
		Radial,
		Linear,
		Conic,
	}

	/// <summary>
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Frame Filter")]
	public class FrameFilter : FilterBase
	{
		[SerializeField] FrameShape _shape = FrameShape.Rectangle;

		[Tooltip("")]
		[SerializeField] FrameFillMode _fillMode = FrameFillMode.Color;
		
		[SerializeField] Color _color = Color.black;

		[Tooltip("The texture to use in FrameFillMode.Texture mode.")]
		[SerializeField] Texture _texture = null;

		[Tooltip("")]
		[SerializeField] FrameGradientShape _gradientShape = FrameGradientShape.Horizontal;

		[Tooltip("The gradient to use in FrameFillMode.Gradient mode.")]
		[SerializeField] Gradient _gradient = ColorUtils.GetBuiltInGradient(BuiltInGradient.SoftRainbow);

		[Range(0f, 360f)]
		[Tooltip("The angle for linear gradient in degrees.")]
		[SerializeField] float _gradientLinearAngle = 0f;

		[SerializeField] float _gradientRadialRadius = 1f;

		[SerializeField] float _gradientScale = 1f;
		[SerializeField] float _gradientOffset = 0f;
		[SerializeField] float _gradientScrollSpeed = 0f;

		[SerializeField] Sprite _sprite = null;

		[SerializeField] float _radiusPadding = 16f;
		[SerializeField] RectEdge _rectPadding = new RectEdge(16f);
		[SerializeField] RectPadToEdge _rectToEdge;
		[SerializeField] FrameCornerShape _rectCornerShape = FrameCornerShape.Round;
		[SerializeField] FrameRoundCornerMode _rectRoundCornerMode = FrameRoundCornerMode.Percent;
		[SerializeField] float _rectRoundCornersValue = 0.5f;
		[SerializeField] RectCorners _rectRoundCorners = new RectCorners(0.25f);
		[SerializeField, Min(0f)] float _softness = 0f;
		[SerializeField] bool _cutoutSource = false;
		[SerializeField] Color _borderColor = Color.white;

		[Tooltip("")]
		[SerializeField] FrameFillMode _borderFillMode = FrameFillMode.Color;
		
		[Tooltip("The texture to use in FrameFillMode.Texture mode.")]
		[SerializeField] Texture _borderTexture = null;

		[Tooltip("")]
		[SerializeField] FrameGradientShape _borderGradientShape = FrameGradientShape.Horizontal;

		[Tooltip("The gradient to use in FrameFillMode.Gradient mode.")]
		[SerializeField] Gradient _borderGradient = ColorUtils.GetBuiltInGradient(BuiltInGradient.SoftRainbow);

		[Range(0f, 360f)]
		[Tooltip("The angle for linear border gradient in degrees.")]
		[SerializeField] float _borderGradientLinearAngle = 0f;

		[SerializeField] float _borderGradientRadialRadius = 1f;

		[SerializeField] float _borderGradientScale = 1f;
		[SerializeField] float _borderGradientOffset = 0f;
		[SerializeField] float _borderGradientScrollSpeed = 0f;

		[SerializeField, Min(0f)] float _borderSize = 4f;
		[SerializeField, Min(0f)] float _borderSoftness = 0f;

		/// <summary>The shape of the frame.</summary>
		public FrameShape Shape { get { return _shape; } set { ChangeProperty(ref _shape, value); } }

		/// <summary></summary>
		public float Softness { get { return _softness; } set { ChangeProperty(ref _softness, Mathf.Max(0f, value)); } }

		/// <summary>The fill mode to use for the frame.</summary>
		public FrameFillMode FillMode { get { return _fillMode; } set { ChangeProperty(ref _fillMode, value); } }

		/// <summary>The color to use in FrameFillMode.Color mode.</summary>
		public Color Color { get { return _color; } set { ChangeProperty(ref _color, value); } }

		/// <summary>The texture to use in FrameFillMode.Texture mode.</summary>
		public Texture Texture { get { return _texture; } set { ChangePropertyRef(ref _texture, value); } }

		/// <summary>The shape of the gradient in FrameFillMode.Gradient mode.</summary>
		public FrameGradientShape GradientShape { get { return _gradientShape; } set { ChangeProperty(ref _gradientShape, value); } }

		/// <summary>The gradient to use in FrameFillMode.Gradient mode.</summary>
		public Gradient Gradient { get { return _gradient; } set { ChangePropertyRef(ref _gradient, value); } }

		/// <summary>The resolution of the texture used by the gradient, ideally in powers of 2 size.</summary>
		public int GradientResolution { get { return _gradientResolution; } set { ChangeProperty(ref _gradientResolution, value); } }

		/// <summary></summary>
		public float GradientScale { get { return _gradientScale; } set { ChangeProperty(ref _gradientScale, value); } }

		/// <summary></summary>
		public float GradientOffset { get { return _gradientOffset; } set { ChangeProperty(ref _gradientOffset, value); } }

		/// <summary></summary>
		public float GradientScrollSpeed { get { return _gradientScrollSpeed; } set { ChangeProperty(ref _gradientScrollSpeed, value); } }

		/// <summary>The angle for Linear gradient in degrees.</summary>
		public float GradientLinearAngle { get { return _gradientLinearAngle; } set { ChangeProperty(ref _gradientLinearAngle, value); } }

		/// <summary></summary>
		public float GradientRadialRadius { get { return _gradientRadialRadius; } set { ChangeProperty(ref _gradientRadialRadius, value); } }

		/// <summary></summary>
		public float RadiusPadding { get { return _radiusPadding; } set { ChangeProperty(ref _radiusPadding, value); } }

		/// <summary></summary>
		public RectEdge RectPadding { get { return _rectPadding; } set { ChangeProperty(ref _rectPadding, value); } }

		/// <summary></summary>
		public RectPadToEdge RectToEdge { get { return _rectToEdge; } set { ChangeProperty(ref _rectToEdge, value); } }

		/// <summary></summary>
		public FrameCornerShape RectCornerShape{ get { return _rectCornerShape; } set { ChangeProperty(ref _rectCornerShape, value); } }

		/// <summary></summary>
		public FrameRoundCornerMode RectRoundCornerMode { get { return _rectRoundCornerMode; } set { ChangeProperty(ref _rectRoundCornerMode, value); } }

		/// <summary></summary>
		public float RectRoundCornersValue { get { return _rectRoundCornersValue; } set { ChangeProperty(ref _rectRoundCornersValue, value); } }

		/// <summary></summary>
		public RectCorners RectRoundCorners { get { return _rectRoundCorners; } set { ChangeProperty(ref _rectRoundCorners, value); } }

		/// <summary></summary>
		public bool CutoutSource { get { return _cutoutSource; } set { ChangeProperty(ref _cutoutSource, value); } }

		/// <summary>The fill mode to use for the frame border.</summary>
		public FrameFillMode BorderFillMode { get { return _borderFillMode; } set { ChangeProperty(ref _borderFillMode, value); } }

		/// <summary></summary>
		public float BorderGradientScale { get { return _borderGradientScale; } set { ChangeProperty(ref _borderGradientScale, value); } }

		/// <summary></summary>
		public float BorderGradientOffset { get { return _borderGradientOffset; } set { ChangeProperty(ref _borderGradientOffset, value); } }

		/// <summary></summary>
		public float BorderGradientScrollSpeed { get { return _borderGradientScrollSpeed; } set { ChangeProperty(ref _borderGradientScrollSpeed, value); } }

		/// <summary></summary>
		public float BorderSize { get { return _borderSize; } set { ChangeProperty(ref _borderSize, Mathf.Max(0f, value)); } }

		/// <summary></summary>
		public float BorderSoftness { get { return _borderSoftness; } set { ChangeProperty(ref _borderSoftness, Mathf.Max(0f, value)); } }

		/// <summary></summary>
		public Color BorderColor { get { return _borderColor; } set { ChangeProperty(ref _borderColor, value); } }

		/// <summary>The texture to use for the border in FrameFillMode.Texture mode.</summary>
		public Texture BorderTexture { get { return _borderTexture; } set { ChangePropertyRef(ref _borderTexture, value); } }

		/// <summary>The shape of the border gradient in FrameFillMode.Gradient mode.</summary>
		public FrameGradientShape BorderGradientShape { get { return _borderGradientShape; } set { ChangeProperty(ref _borderGradientShape, value); } }

		/// <summary>The gradient to use in FrameFillMode.Gradient mode.</summary>
		public Gradient BorderGradient { get { return _borderGradient; } set { ChangePropertyRef(ref _borderGradient, value); } }

		/// <summary>The resolution of the texture used by the gradient, ideally in powers of 2 size.</summary>
		public int BorderGradientResolution { get { return _borderGradientResolution; } set { ChangeProperty(ref _borderGradientResolution, value); } }

		/// <summary>The angle for Linear border gradient in degrees.</summary>
		public float BorderGradientLinearAngle { get { return _borderGradientLinearAngle; } set { ChangeProperty(ref _borderGradientLinearAngle, value); } }

		/// <summary></summary>
		public float BorderGradientRadialRadius { get { return _borderGradientRadialRadius; } set { ChangeProperty(ref _borderGradientRadialRadius, value); } }

		/// <summary></summary>
		public FilterSourceArea SourceArea { get { return _sourceArea; } set { ChangeProperty(ref _sourceArea, value); } }

		internal bool IsPreviewScroll { get; set; }

		private int _gradientResolution = 128;
		private int _borderGradientResolution = 128;
		private float _gradientScroll = 0f;
		private float _borderGradientScroll = 0f;

		internal class FrameShader
		{
			internal const string Id = "Hidden/ChocDino/UIFX/Blend-Frame";

			internal static class Prop
			{
				internal static readonly int CutoutAlpha = Shader.PropertyToID("_CutoutAlpha");
				internal static readonly int Rect_ST = Shader.PropertyToID("_Rect_ST");
				internal static readonly int EdgeRounding = Shader.PropertyToID("_EdgeRounding");
				internal static readonly int FillColor = Shader.PropertyToID("_FillColor");
				internal static readonly int FillTex = Shader.PropertyToID("_FillTex");
				internal static readonly int GradientAxisParams = Shader.PropertyToID("_GradientAxisParams");
				internal static readonly int GradientParams = Shader.PropertyToID("_GradientParams");
				internal static readonly int GradientLinearStartLine = Shader.PropertyToID("_GradientLinearStartLine");
				internal static readonly int GradientLinearParams = Shader.PropertyToID("_GradientLinearParams");
				internal static readonly int FillSoft = Shader.PropertyToID("_FillSoft");
				internal static readonly int BorderColor = Shader.PropertyToID("_BorderColor");
				internal static readonly int BorderFillTex = Shader.PropertyToID("_BorderFillTex");
				internal static readonly int BorderGradientAxisParams = Shader.PropertyToID("_BorderGradientAxisParams");
				internal static readonly int BorderSizeSoft = Shader.PropertyToID("_BorderSizeSoft");
				internal static readonly int BorderGradientParams = Shader.PropertyToID("_BorderGradientParams");
				internal static readonly int BorderGradientLinearStartLine = Shader.PropertyToID("_BorderGradientLinearStartLine");
				internal static readonly int BorderGradientLinearParams = Shader.PropertyToID("_BorderGradientLinearParams");
			}
			internal static class Keyword
			{
				internal const string Border = "BORDER";
				internal const string ShapeCircle = "SHAPE_CIRCLE";
				internal const string ShapeRoundRect = "SHAPE_ROUNDRECT";
				internal const string ShapeChamferRect = "SHAPE_CHAMFERRECT";
				internal const string UseTexture = "USE_TEXTURE";
				internal const string UseBorderTexture = "USE_BORDER_TEXTURE";
				internal const string GradientLinear = "GRADIENT_LINEAR";
				internal const string GradientRadial = "GRADIENT_RADIAL";
				internal const string GradientConic = "GRADIENT_CONIC";
				internal const string GradientLinearBorder = "GRADIENT_LINEAR_BORDER";
				internal const string GradientRadialBorder = "GRADIENT_RADIAL_BORDER";
				internal const string GradientConicBorder = "GRADIENT_CONIC_BORDER";
			}
		}

		private GradientTexture _textureFromGradient;
		private GradientTexture _borderTextureFromGradient;
		//private FrameRoundCornerMode _previousRectRoundCornerMode = FrameRoundCornerMode.None;

		protected override string GetDisplayShaderPath()
		{
			return FrameShader.Id;
		}

		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			/*_rectPadding.left = Mathf.Max(0f, _rectPadding.left);
			_rectPadding.right = Mathf.Max(0f, _rectPadding.right);
			_rectPadding.top = Mathf.Max(0f, _rectPadding.top);
			_rectPadding.bottom = Mathf.Max(0f, _rectPadding.bottom);
			_radiusPadding = Mathf.Max(0f, _radiusPadding);*/
			/*if (_rectRoundCornerUnits == FilterUnit.Custom )
			{
				_rectRoundCorners.topLeft = Mathf.Clamp01(_rectRoundCorners.topLeft);
				_rectRoundCorners.topRight = Mathf.Clamp01(_rectRoundCorners.topRight);
				_rectRoundCorners.bottomLeft = Mathf.Clamp01(_rectRoundCorners.bottomLeft);
				_rectRoundCorners.bottomRight = Mathf.Clamp01(_rectRoundCorners.bottomRight);
			}
			else
			{
			}*/

			if (_rectRoundCornerMode != FrameRoundCornerMode.None)
			{
				if (_rectRoundCornerMode == FrameRoundCornerMode.Pixels || _rectRoundCornerMode == FrameRoundCornerMode.CustomPixels)
				{
					_rectRoundCorners.topLeft = Mathf.Max(0f, _rectRoundCorners.topLeft);
					_rectRoundCorners.topRight = Mathf.Max(0f, _rectRoundCorners.topRight);
					_rectRoundCorners.bottomLeft = Mathf.Max(0f, _rectRoundCorners.bottomLeft);
					_rectRoundCorners.bottomRight = Mathf.Max(0f, _rectRoundCorners.bottomRight);
					_rectRoundCornersValue = Mathf.Max(0f, _rectRoundCornersValue);
				}
				else
				{
					_rectRoundCorners.topLeft = Mathf.Clamp01(_rectRoundCorners.topLeft);
					_rectRoundCorners.topRight = Mathf.Clamp01(_rectRoundCorners.topRight);
					_rectRoundCorners.bottomLeft = Mathf.Clamp01(_rectRoundCorners.bottomLeft);
					_rectRoundCorners.bottomRight = Mathf.Clamp01(_rectRoundCorners.bottomRight);
					_rectRoundCornersValue = Mathf.Clamp01(_rectRoundCornersValue);
				}
			}

			/*
			if (_previousRectRoundCornerMode != _rectRoundCornerMode)
			{
				OnRectRoundCornerModeChanged();
			}
			*/

			_softness = Mathf.Max(0f, _softness);
			_borderSize = Mathf.Max(0f, _borderSize);
			_borderSoftness = Mathf.Max(0f, _borderSoftness);
			base.OnValidate();

		}
		#endif

		/*protected void LateUpdate()
		{
			var rt = this.GetComponent<RectTransform>();
			var result = _screenRect.GetRect();
			Debug.Log(result);
			Debug.Log(ResolutionScalingFactor);
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, result.width * ResolutionScalingFactor);
			rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, result.height * ResolutionScalingFactor);
			//rt.sizeDelta = new Vector2(result.width, result.height);
		}*/

		/*
		void OnRectRoundCornerModeChanged()
		{
			if (_previousRectRoundCornerMode != FrameRoundCornerMode.None)
			{
				bool fromPercent = false;
				switch (_previousRectRoundCornerMode)
				{
					case FrameRoundCornerMode.Small:
					case FrameRoundCornerMode.Medium:
					case FrameRoundCornerMode.Large:
					case FrameRoundCornerMode.Circular:
					case FrameRoundCornerMode.Percent:
					case FrameRoundCornerMode.CustomPercent:
					fromPercent = true;
					break;
				}
				bool toPercent = false;
				switch (_rectRoundCornerMode)
				{
					case FrameRoundCornerMode.Small:
					case FrameRoundCornerMode.Medium:
					case FrameRoundCornerMode.Large:
					case FrameRoundCornerMode.Circular:
					case FrameRoundCornerMode.Percent:
					case FrameRoundCornerMode.CustomPercent:
					toPercent = true;
					break;
				}
				if (fromPercent && !toPercent)
				{
					// TODO: finish this code
					Rect geometryRect = _screenRect.GetRect();
					float size = Mathf.Min(geometryRect.width - _borderSize * 2f, geometryRect.height - _borderSize * 2f) * 0.5f;
					_rectRoundCorners.topLeft = _rectRoundCorners.topLeft * size;
					_rectRoundCorners.topRight = _rectRoundCorners.topRight * size;
					_rectRoundCorners.bottomLeft = _rectRoundCorners.bottomLeft * size;
					_rectRoundCorners.bottomRight = _rectRoundCorners.bottomRight * size;
					_rectRoundCornersValue = _rectRoundCorners.Average;
				}
				else if (!fromPercent && toPercent)
				{
					// TODO: finish this code
					Rect geometryRect = _screenRect.GetRect();
					float size = Mathf.Min(geometryRect.width - _borderSize * 2f, geometryRect.height - _borderSize * 2f) * 0.5f;
					_rectRoundCorners.topLeft = _rectRoundCorners.topLeft * size;
					_rectRoundCorners.topRight = _rectRoundCorners.topRight * size;
					_rectRoundCorners.bottomLeft = _rectRoundCorners.bottomLeft * size;
					_rectRoundCorners.bottomRight = _rectRoundCorners.bottomRight * size;
					_rectRoundCornersValue = _rectRoundCorners.Average;	
				}
			}
			_previousRectRoundCornerMode = _rectRoundCornerMode;
		}*/

		protected override bool DoParametersModifySource()
		{
			if (!base.DoParametersModifySource())
			{
				return false;
			}

			if (_fillMode == FrameFillMode.Color && _color.a <= 0f && !this.IsBorderVisible()) return false;

			return true;
		}

		internal bool HasScrollSpeed()
		{
			return _gradientScrollSpeed != 0f || _borderGradientScrollSpeed != 0f;
		}

		public void ResetScroll()
		{
			if (_gradientScroll != 0f || _borderGradientScroll != 0f)
			{
				_gradientScroll = 0f;
				_borderGradientScroll = 0f;
				ForceUpdate();
			}
		}

		protected override void OnEnable()
		{
			Debug.Assert(_textureFromGradient == null);
			if (_fillMode == FrameFillMode.Gradient)
			{
				GradientTexture.Create(ref _textureFromGradient, _gradientResolution);
			}

			Debug.Assert(_borderTextureFromGradient == null);
			if (_borderFillMode == FrameFillMode.Gradient)
			{
				GradientTexture.Create(ref _borderTextureFromGradient, _borderGradientResolution);
			}

			ResetScroll();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			ObjectHelper.Dispose(ref _borderTextureFromGradient);
			ObjectHelper.Dispose(ref _textureFromGradient);

			/*RenderTextureHelper.ReleaseTemporary(ref _rt);
			ObjectHelper.Destroy(ref _slicedMesh);
			if (_cb != null)
			{
				_cb.Release();
				_cb = null;
			}*/

			base.OnDisable();
		}

		protected override void Update()
		{
			if (HasScrollSpeed()
			#if UNITY_EDITOR
				&& (Application.isPlaying || IsPreviewScroll)
			#endif
			)
			{
				_gradientScroll += _gradientScrollSpeed * Time.deltaTime;
				_borderGradientScroll += _borderGradientScrollSpeed * Time.deltaTime;
				ForceUpdate();
			}
			base.Update();
		}

		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			Rect sourceRect = _screenRect.GetRect();
			if (sourceRect.width <= 0f || sourceRect.height <= 0f) { return; }

			float resolutionScale = ResolutionScalingFactor;//_renderSpace == FilterRenderSpace.Canvas ? 1f : ResolutionScalingFactor;
			float scaledSoftness = _softness * resolutionScale;
			switch (_shape)
			{
				case FrameShape.Rectangle:
					{
						bool hasBorder = (_sprite != null && _sprite.border.sqrMagnitude > 0f);

						var monitorSize = Filters.GetMonitorResolution();

						// Left
						if (_rectToEdge.left)
						{
							leftDown.x += Mathf.CeilToInt(Mathf.Max(0f, _screenRect.GetRect().x + scaledSoftness));
						}
						else
						{
							float border = -4096f;
							if (hasBorder)
							{
								border = _sprite.border.x;
							}
							leftDown.x += (int)(Mathf.Max(_rectPadding.left, border) * resolutionScale);
						}

						// Right
						if (_rectToEdge.right)
						{
							rightUp.x += Mathf.CeilToInt(Mathf.Max(0f, monitorSize.x - _screenRect.GetRect().xMax + scaledSoftness));
						}
						else
						{
							float border = -4096f;
							if (hasBorder)
							{
								border = _sprite.border.z;
							}
							rightUp.x += (int)(Mathf.Max(_rectPadding.right, border) * resolutionScale);
						}

						// Top
						if (_rectToEdge.top)
						{
							rightUp.y += Mathf.CeilToInt(Mathf.Max(0f, monitorSize.y - _screenRect.GetRect().yMax + scaledSoftness));
						}
						else
						{
							float border = -4096f;
							if (hasBorder)
							{
								border = _sprite.border.w;
							}
							rightUp.y += (int)(Mathf.Max(_rectPadding.top, border) * resolutionScale);
						}

						// Bottom
						if (_rectToEdge.bottom)
						{
							leftDown.y += Mathf.CeilToInt(Mathf.Max(0f, _screenRect.GetRect().y + scaledSoftness));
						}
						else
						{
							float border = -4096f;
							if (hasBorder)
							{
								border = _sprite.border.y;
							}
							leftDown.y += (int)(Mathf.Max(_rectPadding.bottom, border) * resolutionScale);
						}
					}
					break;
				case FrameShape.Circle:
					{
						float hw = sourceRect.width * 0.5f;
						float hh = sourceRect.height * 0.5f;
						if (_renderSpace == FilterRenderSpace.Canvas)
						{
							hw *= resolutionScale;
							hh *= resolutionScale;
						}
						float radius = Mathf.Sqrt(hw * hw + hh * hh);
						// NOTE: Currently we can't make leftDown and rightUp negative, so just make it as small as possible while keeping the circle shape
						float radiusPadding = _radiusPadding;//Mathf.Max(-Mathf.Min(radius - hw, radius - hh), _radiusPadding);
						int paddingX = Mathf.CeilToInt(radius - hw + radiusPadding * resolutionScale);
						int paddingY = Mathf.CeilToInt(radius - hh + radiusPadding * resolutionScale);
						leftDown += new Vector2Int(paddingX, paddingY);
						rightUp += new Vector2Int(paddingX, paddingY);
						break;
					}
				case FrameShape.Square:
					{
						float hw = sourceRect.width * 0.5f;
						float hh = sourceRect.height * 0.5f;
						if (_renderSpace == FilterRenderSpace.Canvas)
						{
							hw *= resolutionScale;
							hh *= resolutionScale;
						}
						float radius = Mathf.Max(hw, hh);
						// NOTE: Currently we can't make leftDown and rightUp negative, so just make it as small as possible while keeping the square shape
						float radiusPadding = Mathf.Max(-Mathf.Min(radius - hw, radius - hh), _radiusPadding);
						int paddingX = Mathf.CeilToInt(radius - hw + radiusPadding * resolutionScale);
						int paddingY = Mathf.CeilToInt(radius - hh + radiusPadding * resolutionScale);
						leftDown += new Vector2Int(paddingX, paddingY);
						rightUp += new Vector2Int(paddingX, paddingY);
						break;
					}
			}

			if (IsBorderVisible())
			{
				int border = Mathf.CeilToInt(_borderSize * resolutionScale);
				leftDown += new Vector2Int(border, border);
				rightUp += new Vector2Int(border, border);
			}
		}

		private bool IsBorderVisible()
		{
			return (_borderSize > 0f && _borderColor.a > 0f && _borderFillMode != FrameFillMode.None);
		}

		private bool HasShapedCorners()
		{
			return (_shape != FrameShape.Circle && _rectRoundCornerMode != FrameRoundCornerMode.None && !_rectRoundCorners.IsZero());
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			if (!_displayMaterial) { return; }
			
			// Calculate scale and offset values for our texture to fit it within the geometry rectangle with various layout controls
			/*Rect textureAspectAdjust = Rect.zero;
			if (_texture)
			{
				Rect geometryRect = _screenRect.GetRect();
				float textureAspect = (float)_texture.width / (float)_texture.height;
				Rect aspectGeometryRect = MathUtils.ResizeRectToAspectRatio(geometryRect, _textureScaleMode, textureAspect);
				textureAspectAdjust = MathUtils.GetRelativeRect(geometryRect, aspectGeometryRect);
			
				// Scale centrally
				{
					textureAspectAdjust.x -= 0.5f;
					textureAspectAdjust.y -= 0.5f;
					textureAspectAdjust.xMin *= _textureScale;
					textureAspectAdjust.yMin *= _textureScale;
					textureAspectAdjust.xMax *= _textureScale;
					textureAspectAdjust.yMax *= _textureScale;
					textureAspectAdjust.x += 0.5f;
					textureAspectAdjust.y += 0.5f;
				}
			}
			_displayMaterial.SetTexture(FrameShader.Prop.FillTex, _texture);
			_displayMaterial.SetTextureScale(FrameShader.Prop.FillTex, new Vector2(textureAspectAdjust.width, textureAspectAdjust.height));
			_displayMaterial.SetTextureOffset(FrameShader.Prop.FillTex, new Vector2(textureAspectAdjust.x, textureAspectAdjust.y));*/

			_displayMaterial.SetVector(FrameShader.Prop.Rect_ST, new Vector4(1f / _rectRatio.width, 1f / _rectRatio.height, -_rectRatio.xMin / _rectRatio.width, -_rectRatio.yMin / _rectRatio.height));
			_displayMaterial.SetFloat(FrameShader.Prop.FillSoft, _softness * ResolutionScalingFactor);

			if (_cutoutSource)
			{
				_displayMaterial.SetFloat(FrameShader.Prop.CutoutAlpha, this.Strength);
			}
			else
			{
				_displayMaterial.SetFloat(FrameShader.Prop.CutoutAlpha, 0f);
			}

			switch (_shape)
			{
				case FrameShape.Circle:
					_displayMaterial.EnableKeyword(FrameShader.Keyword.ShapeCircle);
					_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeRoundRect);
					_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeChamferRect);
					break;
				case FrameShape.Rectangle:
				case FrameShape.Square:
					if (_rectRoundCornerMode != FrameRoundCornerMode.None)
					{
						if (_rectRoundCornerMode == FrameRoundCornerMode.Pixels)
						{
							_rectRoundCorners.topLeft = _rectRoundCornersValue;
							_rectRoundCorners.topRight = _rectRoundCornersValue;
							_rectRoundCorners.bottomLeft = _rectRoundCornersValue;
							_rectRoundCorners.bottomRight = _rectRoundCornersValue;
						}
						else if (_rectRoundCornerMode == FrameRoundCornerMode.CustomPixels)
						{
						}
						else if (_rectRoundCornerMode != FrameRoundCornerMode.CustomPercent)
						{
							switch (_rectRoundCornerMode)
							{
								case FrameRoundCornerMode.Small:
								_rectRoundCornersValue = 0.125f;
								break;
								case FrameRoundCornerMode.Medium:
								_rectRoundCornersValue = 0.25f;
								break;
								case FrameRoundCornerMode.Large:
								_rectRoundCornersValue = 0.5f;
								break;
								case FrameRoundCornerMode.Circular:
								_rectRoundCornersValue = 1f;
								break;

							}
							_rectRoundCorners.topLeft = _rectRoundCornersValue;
							_rectRoundCorners.topRight = _rectRoundCornersValue;
							_rectRoundCorners.bottomLeft = _rectRoundCornersValue;
							_rectRoundCorners.bottomRight = _rectRoundCornersValue;
						}
					}
					if (HasShapedCorners())
					{
						if (_rectRoundCornerMode == FrameRoundCornerMode.Pixels || _rectRoundCornerMode == FrameRoundCornerMode.CustomPixels)
						{
							Rect geometryRect = _screenRect.GetRect();
							float resolutionScale = _renderSpace == FilterRenderSpace.Canvas ? 1f : ResolutionScalingFactor;
							float maxSize = (Mathf.Min(geometryRect.width, geometryRect.height) / resolutionScale) * 0.5f;
							Vector4 v = _rectRoundCorners.ToVector();
							v.x = Mathf.Min(maxSize, v.x);
							v.y = Mathf.Min(maxSize, v.y);
							v.z = Mathf.Min(maxSize, v.z);
							v.w = Mathf.Min(maxSize, v.w);
							_displayMaterial.SetVector(FrameShader.Prop.EdgeRounding, v * ResolutionScalingFactor);
						}
						else
						{
							Rect geometryRect = _screenRect.GetRect();
							float resolutionScale = _renderSpace == FilterRenderSpace.Canvas ? 1f : ResolutionScalingFactor;
							float size = Mathf.Min(geometryRect.width, geometryRect.height) / resolutionScale;
							_displayMaterial.SetVector(FrameShader.Prop.EdgeRounding, _rectRoundCorners.ToVector() * size * 0.5f * ResolutionScalingFactor);
						}
						_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeCircle);
						if (_rectCornerShape == FrameCornerShape.Round)
						{
							_displayMaterial.EnableKeyword(FrameShader.Keyword.ShapeRoundRect);
							_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeChamferRect);
						}
						else if (_rectCornerShape == FrameCornerShape.Chamfer)
						{
							_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeRoundRect);
							_displayMaterial.EnableKeyword(FrameShader.Keyword.ShapeChamferRect);
						}
					}
					else
					{
						_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeCircle);
						_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeRoundRect);
						_displayMaterial.DisableKeyword(FrameShader.Keyword.ShapeChamferRect);
					}
				break;
			}

			_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientLinear);
			_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientRadial);
			_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientConic);
			switch (_fillMode)
			{
				case FrameFillMode.None:
					{
						_displayMaterial.DisableKeyword(FrameShader.Keyword.UseTexture);
						_displayMaterial.SetColor(FrameShader.Prop.FillColor, Color.clear);
					}
					break;
				case FrameFillMode.Color:
					{
						_displayMaterial.DisableKeyword(FrameShader.Keyword.UseTexture);
						_displayMaterial.SetColor(FrameShader.Prop.FillColor, Color.LerpUnclamped(new Color(_color.r, _color.g, _color.b, 0f), _color, this.Strength));
					}
					break;
				case FrameFillMode.Texture:
					_displayMaterial.EnableKeyword(FrameShader.Keyword.UseTexture);
					_displayMaterial.SetTexture(FrameShader.Prop.FillTex, _texture);
					_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
					_displayMaterial.SetColor(FrameShader.Prop.FillColor, Color.LerpUnclamped(Color.clear, Color.white, this.Strength));
					break;
				case FrameFillMode.Gradient:
					{
						GradientTexture.Create(ref _textureFromGradient, _gradientResolution);
						_textureFromGradient.Update(_gradient);
						_displayMaterial.EnableKeyword(FrameShader.Keyword.UseTexture);
						_displayMaterial.SetTexture(FrameShader.Prop.FillTex, _textureFromGradient.Texture);
						_displayMaterial.SetColor(FrameShader.Prop.FillColor, Color.LerpUnclamped(Color.clear, Color.white, this.Strength));
						_displayMaterial.SetVector(FrameShader.Prop.GradientParams, new Vector4(_gradientRadialRadius, _gradientOffset + _gradientScroll, _gradientScale, 0f));
						switch (_gradientShape)
						{
							default:
							case FrameGradientShape.Horizontal:
								_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
								break;
							case FrameGradientShape.Vertical:
								_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(0f, -1f, 0f, 1f));
								break;
							case FrameGradientShape.Diagonal:
								_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(0.5f, -0.5f, 0f, 0.5f));
								break;
							case FrameGradientShape.Linear:
								{
									Vector2 uvPointOnStartLine;
									Vector2 uvStartLineDirection;
									float uvGradientLength;
									float uvRectRatio;
									GradientUtils.GetCssLinearGradientShaderParams(_gradientLinearAngle, _screenRect.GetRect(), out uvPointOnStartLine, out uvStartLineDirection, out uvGradientLength, out uvRectRatio);
									_displayMaterial.SetVector(FrameShader.Prop.GradientLinearStartLine, new Vector4(uvPointOnStartLine.x, uvPointOnStartLine.y, uvStartLineDirection.x, uvStartLineDirection.y));
									_displayMaterial.SetVector(FrameShader.Prop.GradientLinearParams, new Vector4(uvGradientLength, uvRectRatio, 0f, 0f));
									_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientLinear);
								}
								break;
							case FrameGradientShape.Radial:
								_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
								_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientRadial);
								break;
							case FrameGradientShape.Conic:
								_displayMaterial.SetVector(FrameShader.Prop.GradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
								_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientConic);
								break;
						}
					}
					break;
			}

			if (IsBorderVisible())
			{
				_displayMaterial.EnableKeyword(FrameShader.Keyword.Border);
				_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientLinearBorder);
				_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientRadialBorder);
				_displayMaterial.DisableKeyword(FrameShader.Keyword.GradientConicBorder);
				_displayMaterial.SetVector(FrameShader.Prop.BorderSizeSoft, new Vector4(_borderSize * ResolutionScalingFactor, _borderSoftness * ResolutionScalingFactor, 0f, 0f));
				switch (_borderFillMode)
				{
					case FrameFillMode.None:
					{
						_displayMaterial.DisableKeyword(FrameShader.Keyword.UseBorderTexture);
						_displayMaterial.SetColor(FrameShader.Prop.BorderColor, Color.clear);
						_displayMaterial.SetVector(FrameShader.Prop.BorderSizeSoft, Vector4.zero);
					}
					break;
					case FrameFillMode.Color:
					{
						_displayMaterial.DisableKeyword(FrameShader.Keyword.UseBorderTexture);
						_displayMaterial.SetColor(FrameShader.Prop.BorderColor, Color.LerpUnclamped(new Color(_borderColor.r, _borderColor.g, _borderColor.b, 0f), _borderColor, this.Strength));
					}
					break;
					case FrameFillMode.Texture:
					_displayMaterial.EnableKeyword(FrameShader.Keyword.UseBorderTexture);
					_displayMaterial.SetTexture(FrameShader.Prop.BorderFillTex, _borderTexture);
					_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
					_displayMaterial.SetColor(FrameShader.Prop.BorderColor, Color.LerpUnclamped(Color.clear, Color.white, this.Strength));
					break;
					case FrameFillMode.Gradient:
						{
							GradientTexture.Create(ref _borderTextureFromGradient, _borderGradientResolution);
							_borderTextureFromGradient.Update(_borderGradient);
							_displayMaterial.EnableKeyword(FrameShader.Keyword.UseBorderTexture);
							_displayMaterial.SetTexture(FrameShader.Prop.BorderFillTex, _borderTextureFromGradient.Texture);
							_displayMaterial.SetColor(FrameShader.Prop.BorderColor, Color.LerpUnclamped(Color.clear, Color.white, this.Strength));
							_displayMaterial.SetVector(FrameShader.Prop.BorderGradientParams, new Vector4(_borderGradientRadialRadius, _borderGradientOffset + _borderGradientScroll, _borderGradientScale, 0f));
							switch (_borderGradientShape)
							{
								default:
								case FrameGradientShape.Horizontal:
									_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
									break;
								case FrameGradientShape.Vertical:
									_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(0f, -1f, 0f, 1f));
									break;
								case FrameGradientShape.Diagonal:
									_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(0.5f, -0.5f, 0f, 0.5f));
									break;
								case FrameGradientShape.Linear:
									{
										_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientLinearBorder);
										Vector2 uvPointOnStartLine;
										Vector2 uvStartLineDirection;
										float uvGradientLength;
										float uvRectRatio;
										GradientUtils.GetCssLinearGradientShaderParams(_borderGradientLinearAngle, _screenRect.GetRect(), out uvPointOnStartLine, out uvStartLineDirection, out uvGradientLength, out uvRectRatio);
										_displayMaterial.SetVector(FrameShader.Prop.BorderGradientLinearStartLine, new Vector4(uvPointOnStartLine.x, uvPointOnStartLine.y, uvStartLineDirection.x, uvStartLineDirection.y));
										_displayMaterial.SetVector(FrameShader.Prop.BorderGradientLinearParams, new Vector4(uvGradientLength, uvRectRatio, 0f, 0f));
									}
									break;
								case FrameGradientShape.Radial:
									_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientRadialBorder);
									_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
									break;
								case FrameGradientShape.Conic:
									_displayMaterial.EnableKeyword(FrameShader.Keyword.GradientConicBorder);
									_displayMaterial.SetVector(FrameShader.Prop.BorderGradientAxisParams, new Vector4(1f, 0f, 0f, 0f));
									break;
							}
						}
					break;
				}
			}
			else
			{
				_displayMaterial.DisableKeyword(FrameShader.Keyword.Border);
				_displayMaterial.SetVector(FrameShader.Prop.BorderSizeSoft, Vector4.zero);
			}

			base.SetupDisplayMaterial(source, result);
		}

#if false
		private Mesh _slicedMesh;
		private CommandBuffer _cb;
		private VertexHelper _vh;
		private RenderTexture _rt;

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			/*RenderTextureHelper.ReleaseTemporary(ref _rt);
			_rt = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);

			if (_slicedMesh == null)
			{
				_slicedMesh = new Mesh();
			}

			RectInt textureRect = _screenRect.GetTextureRect();

			_vh = new VertexHelper();
			_vh.Clear();
			SlicedSprite.Generate9SliceGeometry_Tile(_sprite, new Rect(0f, 0f, textureRect.width, textureRect.height), true, Color.white, 1f, _vh);
			_vh.FillMesh(_slicedMesh);
			_vh.Dispose(); _vh = null;

			Graphic.defaultGraphicMaterial.mainTexture = _sprite.texture;

			if (_cb == null)
			{
				_cb = new CommandBuffer();
			}
			_cb.Clear();
			_cb.SetRenderTarget(new RenderTargetIdentifier(_rt));
			_cb.ClearRenderTarget(false, true, Color.clear, 0f);
			_cb.SetViewMatrix(Matrix4x4.identity);
			var projectionMatrix = Matrix4x4.Ortho(0f, textureRect.width, 0f, textureRect.height, -1000f, 1000f);
			projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false);
			_cb.SetProjectionMatrix(projectionMatrix);
			_cb.DrawMesh(_slicedMesh, Matrix4x4.identity, Graphic.defaultGraphicMaterial);
			Graphics.ExecuteCommandBuffer(_cb);

			return _rt;*/

			return source;
		}
#endif

	}
}