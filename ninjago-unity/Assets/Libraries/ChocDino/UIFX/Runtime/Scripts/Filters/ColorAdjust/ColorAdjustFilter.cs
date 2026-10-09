//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	/// <summary>
	/// A color adjustment filter for uGUI
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Color Adjust Filter")]
	public class ColorAdjustFilter : FilterBase
	{
		[Range(0f, 360f)]
		[SerializeField] float _hue = 0.0f;
		[Range(-1f, 1f)]
		[SerializeField] float _saturation = 0.0f;
		[Range(-1f, 1f)]
		[SerializeField] float _value = 0.0f;
		[Range(-1f, 1f)]
		[SerializeField] float _brightness = 0.0f;
		[Range(-2f, 2f)]
		[SerializeField] float _contrast = 0.0f;
		[Range(1f, 255f)]
		[SerializeField] float _posterize = 255f;
		[Range(0f, 1f)]
		[SerializeField] float _opacity = 1f;

		[SerializeField] Vector4 _brightnessRGBA = Vector4.zero;
		[SerializeField] Vector4 _contrastRGBA = Vector4.zero;
		[SerializeField] Vector4 _posterizeRGBA = new Vector4(255f, 255f, 255f, 255f);

		public float Hue { get { return _hue; } set { ChangeProperty(ref _hue, Mathf.Clamp(value, 0f, 360f)); } }
		public float Saturation { get { return _saturation; } set { ChangeProperty(ref _saturation, Mathf.Clamp(value, -2f, 2f)); } }
		public float Value { get { return _value; } set { ChangeProperty(ref _value, Mathf.Clamp(value, -1f, 1f)); } }
		public float Brightness { get { return _brightness; } set { ChangeProperty(ref _brightness, Mathf.Clamp(value, -2f, 2f)); } }
		public float Contrast { get { return _contrast; } set { ChangeProperty(ref _contrast, Mathf.Clamp(value, -2f, 2f)); } }
		public float Posterize { get { return _posterize; } set { ChangeProperty(ref _posterize, Mathf.Clamp(value, 1f, 255f)); } }
		public float Opacity { get { return _opacity; } set { ChangeProperty(ref _opacity, Mathf.Clamp01(value)); } }
		public Vector4 BrightnessRGBA { get { return _brightnessRGBA; } set { ChangeProperty(ref _brightnessRGBA, value); } }
		public Vector4 ContrastRGBA { get { return _contrastRGBA; } set { ChangeProperty(ref _contrastRGBA, value); } }
		public Vector4 PosterizeRGBA { get { return _posterizeRGBA; } set { ChangeProperty(ref _posterizeRGBA, value); } }

		private ColorAdjust _filter;

		protected override bool DoParametersModifySource()
		{
			if (!base.DoParametersModifySource()) return false;

			if (_hue > 0f) return true;
			if (_saturation != 0f) return true;
			if (_value != 0f) return true;
			if (_brightness != 0f) return true;
			if (_contrast != 0f) return true;
			if (_posterize < 255f) return true;
			if (_opacity < 1f) return true;
			if (_brightnessRGBA != Vector4.zero) return true;
			if (_contrastRGBA != Vector4.zero) return true;
			if (_posterizeRGBA != new Vector4(255f, 255f, 255f, 255f)) return true;

			return false;
		}

		protected override void OnEnable()
		{
			_expand = FilterExpand.None;
			_filter = new ColorAdjust();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_filter != null)
			{
				_filter.FreeResources();
				_filter = null;
			}
			base.OnDisable();
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			_filter.Hue = _hue;
			_filter.Saturation = _saturation;
			_filter.Value = _value;
			_filter.Brightness = _brightness;
			_filter.Contrast = _contrast;
			_filter.Posterize = _posterize;
			_filter.Opacity = _opacity;
			_filter.BrightnessRGBA = _brightnessRGBA;
			_filter.ContrastRGBA = _contrastRGBA;
			_filter.PosterizeRGBA = _posterizeRGBA;
			_filter.Strength = _strength;
			return _filter.Process(source);
		}
	}
}