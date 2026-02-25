//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ChocDino.UIFX;

namespace ChocDino.UIFX.Demos
{
	public class Demo01Dissolve : MonoBehaviour
	{
		[SerializeField] GameObject _examplesParent = null;
		[SerializeField] DissolveFilter _dissolve = null;
		[SerializeField] float _duration = 2f;
		[SerializeField] bool _loop = true;

		private DissolveFilter[] _examples;
		private int[] _indices;
		private int _exampleIndex = 0;
		private float _time = 0f;
		private float _direction = -1f;

		void Awake()
		{
			Debug.Assert(_dissolve != null);	
			_examples = _examplesParent.GetComponentsInChildren<DissolveFilter>(true);
		}

		void OnEnable()
		{
			MathUtils.CreateRandomIndices(ref _indices, _examples.Length);
			_exampleIndex = 0;
			int exampleIndex = _indices[_exampleIndex];
			CopyDissolve(_examples[exampleIndex], _dissolve);
			StartDissolve();
		}


		void Update()
		{
			_time += Time.deltaTime;
			if (_direction < 0f)
			{
				_dissolve.Strength = 1f - (Mathf.Clamp01(_time / _duration));
			}
			else
			{
				_dissolve.Strength = (Mathf.Clamp01(_time / _duration));
			}

			if (_time > _duration)
			{
				_exampleIndex = (_exampleIndex + 1);
				if (!_loop && _exampleIndex >= _examples.Length)
				{
					Application.Quit();
					Debug.Break();
				}
				_exampleIndex %= _examples.Length;
				//_exampleIndex = Random.Range(0, _examples.Length);
				int exampleIndex = _indices[_exampleIndex];
				CopyDissolve(_examples[exampleIndex], _dissolve);
				StartDissolve();
				_direction *= -1f;
			}
		}

		private void CopyDissolve(DissolveFilter src, DissolveFilter dst)
		{
			dst.Dissolve = src.Dissolve;
			dst.Texture = src.Texture;
			dst.TextureScaleMode = src.TextureScaleMode;
			dst.TextureScale = src.TextureScale;
			dst.TextureInvert = src.TextureInvert;
			dst.EdgeLength = src.EdgeLength;
			dst.EdgeColorMode = src.EdgeColorMode;
			dst.EdgeColor = src.EdgeColor;
			dst.EdgeTexture = src.EdgeTexture;
		}

		private void StartDissolve()
		{
			_dissolve.Dissolve = 1f;
			_dissolve.TextureInvert = (Random.value < 0.5f) ? true : false;
			_time = 0f;
		}
	}
}