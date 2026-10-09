//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum DissolveEdgeColorMode
	{
		None,
		Color,
		Ramp,
	}
	/// <summary>
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Dissolve Filter")]
	public class DissolveFilter : FilterBase
	{
		[Range(0f, 1f)]
		[SerializeField] float _dissolve = 0.25f;
		[SerializeField] Texture _texture = null;
		[SerializeField] ScaleMode _textureScaleMode = ScaleMode.ScaleAndCrop;
		[SerializeField] float _scale = 1f;
		[SerializeField] bool _invert = false;

		[Range(0f, 1f)]
		[SerializeField] float _edgeLength = 0.1f;
		[SerializeField] DissolveEdgeColorMode _edgeColorMode;
		[ColorUsageAttribute(showAlpha: false, hdr: false)]
		[SerializeField] Color _edgeColor = Color.black;
		[SerializeField] Texture _edgeTexture = null;
		[Range(0f, 100f)]
		[SerializeField] float _edgeEmissive = 0f;

		public float Dissolve { get { return _dissolve; } set { ChangeProperty(ref _dissolve, value); } }
		public Texture Texture { get { return _texture; } set { ChangePropertyRef(ref _texture, value); } }
		public ScaleMode TextureScaleMode { get { return _textureScaleMode; } set { ChangeProperty(ref _textureScaleMode, value); } }
		public float TextureScale { get { return _scale; } set { ChangeProperty(ref _scale, value); } }
		public bool TextureInvert { get { return _invert; } set { ChangeProperty(ref _invert, value); } }
		public float EdgeLength { get { return _edgeLength; } set { ChangeProperty(ref _edgeLength, value); } }
		public DissolveEdgeColorMode EdgeColorMode { get { return _edgeColorMode; } set { ChangeProperty(ref _edgeColorMode, value); } }
		public Color EdgeColor { get { return _edgeColor; } set { ChangeProperty(ref _edgeColor, value); } }
		public Texture EdgeTexture { get { return _edgeTexture; } set { ChangePropertyRef(ref _edgeTexture, value); } }
		public float EdgeEmissive { get { return _edgeEmissive; } set { ChangeProperty(ref _edgeEmissive, value); } }

		static new class ShaderProp
		{
			public readonly static int Dissolve = Shader.PropertyToID("_Dissolve");
			public readonly static int FillTex = Shader.PropertyToID("_FillTex");
			public readonly static int EdgeTex = Shader.PropertyToID("_EdgeTex");
			public readonly static int EdgeColor = Shader.PropertyToID("_EdgeColor");
			public readonly static int EdgeEmissive = Shader.PropertyToID("_EdgeEmissive");
			public readonly static int InvertFactor = Shader.PropertyToID("_InvertFactor");
		}
		static class ShaderKeyword
		{
			public const string EdgeColor = "EDGE_COLOR";
			public const string EdgeRamp = "EDGE_RAMP";
		}

		private const string BlendShaderPath = "Hidden/ChocDino/UIFX/Blend-Dissolve";
		private readonly static Vector4 InvertFalse = new Vector4(0f, -1f, 0f, 0f);
		private readonly static Vector4 InvertTrue = new Vector4(1f, 1f, 0f, 0f);

		protected override string GetDisplayShaderPath()
		{
			return BlendShaderPath;
		}

		protected override bool DoParametersModifySource()
		{
			if (base.DoParametersModifySource())
			{
				if (_dissolve <= 0f) return false;
				return true;
			}
			return false;
		}

		protected override void OnEnable()
		{
			_expand = FilterExpand.None;
			base.OnEnable();
		}

		public override bool IsOutputHdr()
		{
			if (_edgeEmissive > 0f)
			{
				return true;
			}
			return base.IsOutputHdr();
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			// Calculate scale and offset values for our texture to fit it within the geometry rectangle with various layout controls
			Rect textureAspectAdjust = Rect.zero;
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
					textureAspectAdjust.xMin *= _scale;
					textureAspectAdjust.yMin *= _scale;
					textureAspectAdjust.xMax *= _scale;
					textureAspectAdjust.yMax *= _scale;
					textureAspectAdjust.x += 0.5f;
					textureAspectAdjust.y += 0.5f;
				}
			}

			_displayMaterial.SetTexture(ShaderProp.FillTex, _texture);
			_displayMaterial.SetTextureScale(ShaderProp.FillTex, new Vector2(textureAspectAdjust.width, textureAspectAdjust.height));
			_displayMaterial.SetTextureOffset(ShaderProp.FillTex, new Vector2(textureAspectAdjust.x, textureAspectAdjust.y));
			_displayMaterial.SetVector(ShaderProp.InvertFactor, _invert ? InvertTrue : InvertFalse);

			// Remap [0..1] range to [-edgeLength..1.0]
			float dissolve = Mathf.LerpUnclamped(-_edgeLength, 1.0f, _dissolve * _strength);
			_displayMaterial.SetVector(ShaderProp.Dissolve, new Vector2(dissolve, _edgeLength));

			switch (_edgeColorMode)
			{
				case DissolveEdgeColorMode.None:
					_displayMaterial.DisableKeyword(ShaderKeyword.EdgeColor);
					_displayMaterial.DisableKeyword(ShaderKeyword.EdgeRamp);
					break;
				case DissolveEdgeColorMode.Color:
					_displayMaterial.EnableKeyword(ShaderKeyword.EdgeColor);
					_displayMaterial.DisableKeyword(ShaderKeyword.EdgeRamp);
					_displayMaterial.SetColor(ShaderProp.EdgeColor, _edgeColor);
					_displayMaterial.SetFloat(ShaderProp.EdgeEmissive, _edgeEmissive + 1f);
					break;
				case DissolveEdgeColorMode.Ramp:
					_displayMaterial.DisableKeyword(ShaderKeyword.EdgeColor);
					_displayMaterial.EnableKeyword(ShaderKeyword.EdgeRamp);
					_displayMaterial.SetTexture(ShaderProp.EdgeTex, _edgeTexture);
					_displayMaterial.SetFloat(ShaderProp.EdgeEmissive, _edgeEmissive + 1f);
					break;
			}

			base.SetupDisplayMaterial(source, result);
		}
	}
}