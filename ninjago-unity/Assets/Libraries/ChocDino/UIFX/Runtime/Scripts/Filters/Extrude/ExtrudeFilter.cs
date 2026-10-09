//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum ExtrudeFillMode
	{
		Color,
		BiColor,
		Gradient,
		Texture,
	}

	public enum ExtrudeFillBlendMode
	{
		Replace,
		Multiply,
	}

	public enum ExtrudeProjection
	{
		Perspective,
		Orthographic,
	}

	/// <summary>
	/// An extrude filter for uGUI components
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Extrude Filter")]
	public class ExtrudeFilter : FilterBase
	{
		[Tooltip("The projection mode used for extrusion.")]
		[SerializeField] ExtrudeProjection _projection = ExtrudeProjection.Perspective;

		[Tooltip("The clockwise angle the shadow is cast at. Range is [0..360]. Default is 135.0.")]
		[Range(0f, 360f)]
		[SerializeField] float _angle = 135f;

		[Tooltip("")]
		[Range(0f, 512f)]
		[SerializeField] float _perspectiveDistance = 0f;

		[Tooltip("The distance the shadow is cast. Range is [0..512]. Default is 32.0.")]
		[Range(0f, 512f)]
		[SerializeField] float _distance = 32f;

		[Tooltip("The composite mode to use for rendering.")]
		[SerializeField] ExtrudeFillMode _fillMode = ExtrudeFillMode.BiColor;

		[Tooltip("The color of the main/front of the shadow.")]
		[SerializeField] Color _colorFront = Color.grey;

		[Tooltip("The color of the back of the shadow in BiColor mode.")]
		[SerializeField] Color _colorBack = Color.clear;

		[Tooltip("The color of the back of the extrusion.")]
		[SerializeField] Texture _gradientTexture = null;

		[Tooltip("The color of the back of the extrusion.")]
		[SerializeField] Gradient _gradient = ColorUtils.GetBuiltInGradient(BuiltInGradient.SoftRainbow);

		/// <summary>The resolution of the texture used by the gradient, ideally in powers of 2 size.</summary>
		public int GradientResolution { get { return _gradientResolution; } set { ChangeProperty(ref _gradientResolution, value); } }

		[Tooltip("The speed to scroll the gradient.")]
		[SerializeField] float _scrollSpeed = 0f;

		[Tooltip("Reverse the fill direction.")]
		[SerializeField] bool _reverseFill = false;

		[Tooltip("")]
		[SerializeField] ExtrudeFillBlendMode _fillBlendMode = ExtrudeFillBlendMode.Multiply;

		[Tooltip("The transparency of the source content. Set to zero to make only the outline show.")]
		[Range(0f, 1f)]
		[SerializeField] float _sourceAlpha = 1.0f;

		[Tooltip("The composite mode to use for rendering.")]
		[SerializeField] LongShadowCompositeMode _compositeMode = LongShadowCompositeMode.Normal;

		/// <summary>The projection mode used for extrusion.</summary>
		public ExtrudeProjection Projection { get { return _projection; } set { ChangeProperty(ref _projection, value); } }

		/// <summary>The clockwise angle the shadow is cast at. Range is [0..360]. Default is 135.0</summary>
		public float Angle { get { return _angle; } set { ChangeProperty(ref _angle, value); } }

		/// <summary></summary>
		public float PerspectiveDistance { get { return _perspectiveDistance; } set { ChangeProperty(ref _perspectiveDistance, value); } }

		/// <summary>The distance the shadow is cast. Range is [-512..512]. Default is 32</summary>
		public float Distance { get { return _distance; } set { ChangeProperty(ref _distance, value); } }

		/// <summary>The composite mode to use for rendering.</summary>
		public ExtrudeFillMode FillMode { get { return _fillMode; } set { _fillMode = value; ForceUpdate(); } }

		/// <summary>The color of the main/front of the shadow</summary>
		public Color ColorFront { get { return _colorFront; } set { ChangeProperty(ref _colorFront, value); } }

		/// <summary>The color of the back of the shadow</summary>
		public Color ColorBack { get { return _colorBack; } set { ChangeProperty(ref _colorBack, value); } }

		/// <summary>The color of the back of the extrusion</summary>
		public Texture GradientTexture { get { return _gradientTexture; } set { _gradientTexture = value; ForceUpdate(); } }

		/// <summary>The color of the back of the extrusion</summary>
		public Gradient Gradient { get { return _gradient; } set { _gradient = value; ForceUpdate(); } }

		/// <summary>The speed to scroll the gradient.</summary>
		public float ScrollSpeed { get { return _scrollSpeed; } set { ChangeProperty(ref _scrollSpeed, value); } }

		/// <summary>Reverse the fill direction.</summary>
		public bool ReverseFill { get { return _reverseFill; } set { ChangeProperty(ref _reverseFill, value); } }

		/// <summary></summary>
		public ExtrudeFillBlendMode FillBlendMode { get { return _fillBlendMode; } set { ChangeProperty(ref _fillBlendMode, value); } }

		/// <summary>The transparency of the source content. Set to zero to make only the outline show. Range is [0..1] Default is 1.0</summary>
		public float SourceAlpha { get { return _sourceAlpha; } set { ChangeProperty(ref _sourceAlpha, value); } }

		/// <summary>The composite mode to use for rendering.</summary>
		public LongShadowCompositeMode CompositeMode { get { return _compositeMode; } set { ChangeProperty(ref _compositeMode, value); } }

		internal bool IsPreviewScroll { get; set; }

		private int _gradientResolution = 256;
		private GradientTexture _textureFromGradient;
		private Extrude _effect = null;
		private float _scroll = 0f;

		private const string DisplayShaderPath = "Hidden/ChocDino/UIFX/Blend-Extrude";

		static new class ShaderProp
		{
			public readonly static int SourceAlpha = Shader.PropertyToID("_SourceAlpha");
		}
		static class ShaderKeyword
		{
			public const string StyleNormal = "STYLE_NORMAL";
			public const string StyleCutout = "STYLE_CUTOUT";
			public const string StyleShadow = "STYLE_SHADOW";
		}

		protected override string GetDisplayShaderPath()
		{
			return DisplayShaderPath;
		}

		internal override bool CanApplyFilter()
		{
			if (_effect == null ) return false;
			return base.CanApplyFilter();
		}

		protected override bool DoParametersModifySource()
		{
			if (_sourceAlpha < 1f) return true;
			if (_fillMode == ExtrudeFillMode.Color && _colorFront.a <= 0f) return false;
			if (_fillMode == ExtrudeFillMode.BiColor && _colorFront.a <= 0f && _colorBack.a <= 0f) return false;
			if (Mathf.Abs(_distance) < 0.1f && _compositeMode == LongShadowCompositeMode.Normal) return false;
			if (this.Strength <= 0f) return false;
			return base.DoParametersModifySource();
		}

		public void ResetScroll()
		{
			if (_scroll != 0f)
			{
				_scroll = 0f;
				ForceUpdate();
			}
		}

		protected override void OnEnable()
		{
			//_rectAdjustOptions.padding = 32;
			//_rectAdjustOptions.roundToNextMultiple = 32;

			Debug.Assert(_textureFromGradient == null);
			if (_fillMode == ExtrudeFillMode.Gradient)
			{
				ChocDino.UIFX.GradientTexture.Create(ref _textureFromGradient, _gradientResolution);
			}

			_effect = new Extrude(this);
			ResetScroll();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_effect != null)
			{
				_effect.FreeResources();
				_effect = null;
			}
			ObjectHelper.Dispose(ref _textureFromGradient);
			base.OnDisable();
		}
		
		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			// OnValidate is called when the scene is saved, which causes the material (fields without properties) to lose their properties, so we force update them here.
			if (_effect != null)
			{
				float angle = _effect.Angle;
				_effect.Angle = 0f;
				_effect.Angle = angle;
			}
			
			base.OnValidate();
		}
		#endif


		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			if (_effect != null)
			{
				SetupFilterParams();
				_effect.GetAdjustedBounds(_screenRect.GetRect(), ref leftDown, ref rightUp);
			}
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			_displayMaterial.SetFloat(ShaderProp.SourceAlpha, Mathf.LerpUnclamped(1f, _sourceAlpha, this.Strength));

			switch(_compositeMode)
			{
				case LongShadowCompositeMode.Normal:
					_displayMaterial.EnableKeyword(ShaderKeyword.StyleNormal);
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleCutout);
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleShadow);
					break;
				case LongShadowCompositeMode.Cutout:
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleNormal);
					_displayMaterial.EnableKeyword(ShaderKeyword.StyleCutout);
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleShadow);
					break;
				case LongShadowCompositeMode.Shadow:
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleNormal);
					_displayMaterial.DisableKeyword(ShaderKeyword.StyleCutout);
					_displayMaterial.EnableKeyword(ShaderKeyword.StyleShadow);
					break;
			}
	
			base.SetupDisplayMaterial(source, result);
		}

		protected override void Update()
		{
			if (_scrollSpeed != 0f
			#if UNITY_EDITOR
				&& (Application.isPlaying || IsPreviewScroll)
			#endif
			)
			{
				_scroll += _scrollSpeed * Time.deltaTime;
				ForceUpdate();
			}
			base.Update();
		}

		private void SetupFilterParams()
		{
			Debug.Assert(_effect != null);

			_effect.Projection = _projection;
			_effect.Angle = _angle;
			_effect.Distance = _distance * _strength;
			_effect.PerspectiveDistance = _perspectiveDistance;// * ResolutionScalingFactor;

			if (_fillMode == ExtrudeFillMode.Color)
			{
				_effect.Color1 = _colorFront;
				_effect.Color2 = _colorFront;
				_effect.UseGradientTexture = false;
			}
			if (_fillMode == ExtrudeFillMode.BiColor)
			{
				_effect.Color1 = _colorFront;
				_effect.Color2 = _colorBack;
				_effect.UseGradientTexture = false;
			}
			else if (_fillMode == ExtrudeFillMode.Gradient)
			{
				_effect.UseGradientTexture = true;
				ChocDino.UIFX.GradientTexture.Create(ref _textureFromGradient, _gradientResolution);
				_textureFromGradient.Update(_gradient);
				_effect.GradientTexture = _textureFromGradient.Texture;
			}
			else if (_fillMode == ExtrudeFillMode.Texture)
			{
				_effect.UseGradientTexture = true;
				_effect.GradientTexture = _gradientTexture;
			}
			_effect.ReverseFill = _reverseFill;
			_effect.Scroll = _scroll;
			_effect.MultiplySource = (_fillBlendMode == ExtrudeFillBlendMode.Multiply);

			_effect.RectRatio = _screenRect.GetRect();
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			SetupFilterParams();
			return _effect.Process(source);
		}
	}
}