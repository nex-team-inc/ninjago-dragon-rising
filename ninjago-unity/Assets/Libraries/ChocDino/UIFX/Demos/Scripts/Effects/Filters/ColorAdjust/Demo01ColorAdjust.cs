//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX.Demos
{
	public class Demo01ColorAdjust : MonoBehaviour
	{
		[SerializeField] ColorAdjustFilter _filterHue = null;
		[SerializeField] ColorAdjustFilter _filterRandom = null;
		[SerializeField] ColorAdjustFilter _filterPoster = null;
		[SerializeField] ColorAdjustFilter _filterSaturation = null;
		[SerializeField] float _duration = 1f;

		private float _time;
		private float _hue;
		private float _saturation;
		private float _value;
		private float _brightness;
		private float _contrast;
		private float _posterize;
		private float _opacity;
		private Vector4 _brightnessRGBA;
		private Vector4 _contrastRGBA;

		private float _hueRotate;

		void Awake()
		{
			Debug.Assert(_filterHue != null);
			Debug.Assert(_filterRandom != null);
			Debug.Assert(_filterPoster != null);
			Debug.Assert(_filterSaturation != null);
			NextTarget();
		}

		void NextTarget()
		{
			_hue = Random.Range(0f, 360f);
			_saturation = Random.Range(-1f, 1f);
			_value = Random.Range(-0.15f, 0.5f);
			_brightness = Random.Range(-0.25f, 0.25f);
			_contrast = Random.Range(0.0f, 1f);
			_posterize = Random.Range(255f, 255f);
			_opacity = Random.Range(1f, 1f);
			_brightnessRGBA = new Vector4(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), 0f);
			_contrastRGBA = new Vector4(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), 0f);
		}

		void Update()
		{
			_time += Time.deltaTime;

			float t = _time / _duration;
			{
				float tt = DemoUtils.InOutCubic(t);

				var filter = _filterRandom;

				float hue = Quaternion.Slerp(Quaternion.Euler(new Vector3(0f, 0f, filter.Hue)), Quaternion.Euler(new Vector3(0f, 0f, _hue)), tt).eulerAngles.z;

				filter.Hue = hue;
				filter.Saturation = Mathf.Lerp(filter.Saturation, _saturation, tt);
				filter.Value = Mathf.Lerp(filter.Value, _value, tt);
				filter.Brightness = Mathf.Lerp(filter.Brightness, _brightness, tt);
				filter.Contrast = Mathf.Lerp(filter.Contrast, _contrast, tt);
				filter.Posterize = Mathf.Lerp(filter.Posterize, _posterize, tt);	
				filter.Opacity = Mathf.Lerp(filter.Opacity, _opacity, tt);
				filter.BrightnessRGBA = Vector4.Lerp(filter.BrightnessRGBA, _brightnessRGBA, tt);
				filter.ContrastRGBA = Vector4.Lerp(filter.ContrastRGBA, _contrastRGBA, tt);

				if (_time >= _duration)
				{
					NextTarget();
					_time -= _duration;
					_time %= _duration;
				}
			}

			{
				_hueRotate += Time.deltaTime * 200f;
				_filterHue.Hue = _hueRotate % 360f;
			}

			{
				float tp = Mathf.PingPong(t, 0.5f) * 2f;
				_filterPoster.PosterizeRGBA = new Vector4(Mathf.Lerp(1f, 8f, tp), Mathf.Lerp(1f, 8f, tp), Mathf.Lerp(1f, 8f, tp), 255f);
				_filterSaturation.Saturation = Mathf.Lerp(-1f, 1f, tp);
			}
		}
	}
}