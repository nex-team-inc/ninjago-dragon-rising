//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	/// <summary>
	/// A gooey filter for uGUI components
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Gooey Filter")]
	public class GooeyFilter : FilterBase
	{
		[Tooltip("The radius in pixels to dilate the edges of the graphic. Default is 2.0")]
		[Range(0f, 32f)]
		[SerializeField] float _size = 2f;
		
		[Tooltip("The radius of the blur in pixels. Default is 28.0")]
		[Range(0f, 64f)]
		[SerializeField] float _blur = 28f;

		[Tooltip("Threshold controls the value used to clip the alpha channel. Default is 0.35")]
		[Range(0f, 1f)]
		[SerializeField] float _threshold = 0.35f;

		[Tooltip("Threshold falloff controls how soft or hard the threshold is. Default is 0.5")]
		[Range(0f, 1f)]
		[SerializeField] float _thresholdFalloff = 0.5f;

		/// <summary>The radius in pixels to dilate the edges of the graphic. Default is 2.0</summary>
		public float Size { get { return _size; } set { ChangeProperty(ref _size,value); } }

		/// <summary>The radius of the blur in pixels. Default is 28.0</summary>
		public float Blur { get { return _blur; } set { ChangeProperty(ref _blur, value); } }

		/// <summary>Threshold controls the value used to clip the alpha channel. Default is 0.35</summary>
		public float Threshold { get { return _threshold; } set { ChangeProperty(ref _threshold, value); } }

		/// <summary>Threshold falloff controls how soft or hard the threshold is. Default is 0.5</summary>
		public float ThresholdFalloff { get { return _thresholdFalloff; } set { ChangeProperty(ref _thresholdFalloff, value); } }

		private const string DisplayShaderPath = "Hidden/ChocDino/UIFX/Blend-Gooey";

		private ITextureBlur _blurfx = null;
		private ErodeDilate _erodeDilate = null;

		static new class ShaderProp
		{
			public readonly static int ThresholdOffset = Shader.PropertyToID("_ThresholdOffset");
			public readonly static int ThresholdScale = Shader.PropertyToID("_ThresholdScale");
		}

		protected override string GetDisplayShaderPath()
		{
			return DisplayShaderPath;
		}

		internal override bool CanApplyFilter()
		{
			if (_blurfx == null ) return false;
			if (_erodeDilate == null) return false;
			return base.CanApplyFilter();
		}

		protected override bool DoParametersModifySource()
		{
			if (_blur <= 0f && _size <= 0f) return false;
			return base.DoParametersModifySource();
		}

		protected override void OnEnable()
		{
			_blurfx = new BoxBlurReference();
			_erodeDilate = new ErodeDilate();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_erodeDilate != null)
			{
				_erodeDilate.FreeResources();
				_erodeDilate = null;
			}
			if (_blurfx != null)
			{
				_blurfx.FreeResources();
				_blurfx = null;
			}
			base.OnDisable();
		}

		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			// OnValidate is called when the scene is saved, which causes the material (fields without properties) to lose their properties, so we force update them here.
			if (_blurfx != null)
			{
				_blurfx.ForceDirty();
			}
			if (_erodeDilate != null)
			{
				_erodeDilate.ForceDirty();
			}
			
			base.OnValidate();
		}
		#endif
		
		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			if (_blurfx != null && _erodeDilate != null)
			{
				SetupFilterParams();
				_blurfx.AdjustBoundsSize(ref leftDown, ref rightUp);
				_erodeDilate.AdjustBoundsSize(ref leftDown, ref rightUp);
			}
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			if (this.Strength >= 1f)
			{
				_displayMaterial.SetFloat(ShaderProp.ThresholdOffset, _threshold);
				_displayMaterial.SetFloat(ShaderProp.ThresholdScale, Mathf.Lerp(1f, 32f, _thresholdFalloff));
			}
			else
			{
				_displayMaterial.SetFloat(ShaderProp.ThresholdOffset, Mathf.LerpUnclamped(0.5f, _threshold, this.Strength));
				_displayMaterial.SetFloat(ShaderProp.ThresholdScale, Mathf.LerpUnclamped(1f, Mathf.Lerp(1f, 32f, _thresholdFalloff), this.Strength));
			}

			base.SetupDisplayMaterial(source, result);
		}

		private void SetupFilterParams()
		{
			_blurfx.SetBlurSize(_blur * _strength * ResolutionScalingFactor);
			_blurfx.Downsample = Downsample.Half;
			_erodeDilate.AlphaOnly = false;
			_erodeDilate.ErodeSize = 0f;
			_erodeDilate.DilateSize = _size * _strength * ResolutionScalingFactor;
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			SetupFilterParams();
			var dilate = _erodeDilate.Process(source);
			var blurred = _blurfx.Process(dilate);
			if (dilate != blurred)
			{
				_erodeDilate.FreeTextures();
			}
			return blurred;
		}
	}
}