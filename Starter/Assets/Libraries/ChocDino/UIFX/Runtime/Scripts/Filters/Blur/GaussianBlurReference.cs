//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public class GaussianBlurReference : ITextureBlur
	{
		internal class BlurShader
		{
			internal const string Id = "Hidden/ChocDino/UIFX/GaussianBlur-Reference";

			internal static class Prop
			{
				internal static readonly int KernelRadius = Shader.PropertyToID("_KernelRadius");
				internal static readonly int Weights = Shader.PropertyToID("_Weights");

			}
			internal static class Pass
			{
				internal const int Horizontal = 0;
				internal const int Vertical = 1;
			}
		}

		public BlurAxes2D BlurAxes2D { get { return _blurAxes2D; } set { _blurAxes2D = value; } }
		public Downsample Downsample { get { return _downSample; } set { if (_downSample != value) { _downSample = value; _kernelDirty = _materialDirty = true; } } }

		private Downsample _downSample = Downsample.Auto;
		private float _blurSize = 0.05f;
		private Material _material;
		private RenderTexture _sourceTexture;
		private RenderTexture _rtBlurH;
		private RenderTexture _rtBlurV;
		private BlurAxes2D _blurAxes2D = BlurAxes2D.Default;
		private const int MaxRadius = 512;
		private float[] _weights;

		private bool _kernelDirty = true;
		private bool _materialDirty = true;

		public void ForceDirty()
		{
			_kernelDirty = _materialDirty = true;
		}

		public void SetBlurSize(float diagonalPercent)
		{
			if (diagonalPercent != _blurSize)
			{
				_blurSize = diagonalPercent;
				_kernelDirty = _materialDirty = true;
			}
		}

		public void AdjustBoundsSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			// Get radius for box blur
			float radius = GetScaledRadius();

			// NOTE: This size is based off a solid white box which is the worst case, if
			// the contents of the image is dark then this radius could be significantly shrunk,
			// but there is no easy way to detect this. Also if the image is HDR then this expand
			// may be too small - perhaps expose option to the user.
			radius *= GetDownsampleFactor();

			int x = Mathf.CeilToInt(radius);
			if (x > 0)
			{
				Vector2Int result = new Vector2Int(x, x);
				if (_blurAxes2D == BlurAxes2D.Horizontal)
				{
					result.y = 0;
				}
				else if (_blurAxes2D == BlurAxes2D.Vertical)
				{
					result.x = 0;
				}
				leftDown += result;
				rightUp += result;
			}
		}

		public RenderTexture Process(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture != null);

			// Early-out
			if (GetScaledRadius() <= 0f)
			{
				FreeTextures();
				return sourceTexture;
			}

			RenderTexture prevRT = RenderTexture.active;

			SetupResources(sourceTexture);

			if (_kernelDirty)
			{
				UpdateKernel();
			}
			if (_materialDirty)
			{
				UpdateMaterial();
			}

			RenderTexture src = _sourceTexture;
			if (GetScaledRadius() > 0f)
			{
				// Have to downsample first otherwise it will be biased in the first blur pass direction
				// leading to slightly stretched result
				if (GetDownsampleFactor() > 1)
				{
					Graphics.Blit(src, _rtBlurV);
					_rtBlurV.IncrementUpdateCount();
					src = _rtBlurV;
				}

				// Blur
				{
					if (_blurAxes2D == BlurAxes2D.Default)
					{
						Graphics.Blit(src, _rtBlurH, _material, BlurShader.Pass.Horizontal);
						_rtBlurH.IncrementUpdateCount();
						Graphics.Blit(_rtBlurH, _rtBlurV, _material, BlurShader.Pass.Vertical);
						_rtBlurV.IncrementUpdateCount();
						src = _rtBlurV;
					}
					else
					{
						int pass = (_blurAxes2D == BlurAxes2D.Horizontal) ? BlurShader.Pass.Horizontal : BlurShader.Pass.Vertical;
						var dst = _rtBlurH;

						Graphics.Blit(src, dst, _material, pass);
						dst.IncrementUpdateCount();
						src = dst;
					}
				}

				// Free intermediate textures
				if (src == _rtBlurH)
				{
					RenderTextureHelper.ReleaseTemporary(ref _rtBlurV);
				}
				else
				{
					RenderTextureHelper.ReleaseTemporary(ref _rtBlurH);
				}
			}
			else
			{
				FreeTextures();
			}

			RenderTexture.active = prevRT;

			return src;
		}

		public void FreeResources()
		{
			FreeShaders();
			FreeTextures();
		}

		private static uint CreateTextureHash(int width, int height)
		{
			uint hash = 0;
			hash = (hash << 0) | (uint)width;
			hash = (hash << 13) | (uint)height;
			return hash;
		}

		private void RecreateTexture(ref RenderTexture rt, uint desiredHash, RenderTexture sourceTexture)
		{
			// Release the texture if it isn't suitable
			if (rt != null)
			{
				uint hash = CreateTextureHash(rt.width, rt.height);
				if (hash != desiredHash)
				{
					RenderTextureHelper.ReleaseTemporary(ref rt);
					_materialDirty = true;
				}
			}

			// Create texture
			if (rt == null && sourceTexture != null)
			{
				Debug.Assert(sourceTexture.width > 0 && sourceTexture.height > 0);
				int w = Mathf.Max(1, sourceTexture.width / GetDownsampleFactor());
				int h = Mathf.Max(1, sourceTexture.height / GetDownsampleFactor());

				RenderTextureFormat format = sourceTexture.format;
				if ((Filters.PerfHint & PerformanceHint.UseMorePrecision) != 0)
				{
					// TODO: create based on the input texture format, but just with more precision
					format = RenderTextureFormat.ARGBHalf;
				}

				rt = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);
			}
		}

		void SetupResources(RenderTexture sourceTexture)
		{
			uint desiredTextureHash = 0;
			if (sourceTexture != null)
			{
				desiredTextureHash = CreateTextureHash(sourceTexture.width / GetDownsampleFactor(), sourceTexture.height / GetDownsampleFactor());
			}

			RecreateTexture(ref _rtBlurH, desiredTextureHash, sourceTexture);
			RecreateTexture(ref _rtBlurV, desiredTextureHash, sourceTexture);

			#if UNITY_EDITOR
			_rtBlurH.name = "GaussianBlurH";
			_rtBlurV.name = "GaussianBlurV";
			#endif
	
			if (_sourceTexture != sourceTexture)
			{
				_materialDirty = true;
				_sourceTexture = sourceTexture;
			}
			if (_material == null)
			{
				CreateShaders();
			}
		}

		private float GetScaledRadius()
		{
			return _blurSize / (float)GetDownsampleFactor();
		}

		void UpdateMaterial()
		{
			_material.SetInt(BlurShader.Prop.KernelRadius, _weights.Length);
			_material.SetFloatArray(BlurShader.Prop.Weights, _weights);
			_materialDirty = false;
		}

		static Material CreateMaterialFromShader(string shaderName)
		{
			Material result = null;
			Shader shader = Shader.Find(shaderName);
			if (shader != null)
			{
				result = new Material(shader);
			}
			return result;
		}

		void CreateShaders()
		{
			_material = CreateMaterialFromShader(BlurShader.Id);
			Debug.Assert(_material != null);
			_material.SetFloatArray(BlurShader.Prop.Weights, new float[MaxRadius]);
			_materialDirty = true;
		}

		void FreeShaders()
		{
			ObjectHelper.Destroy(ref _material);
		}

		void FreeTextures()
		{
			RenderTextureHelper.ReleaseTemporary(ref _rtBlurV);
			RenderTextureHelper.ReleaseTemporary(ref _rtBlurH);
			_sourceTexture = null;
		}

		private int GetDownsampleFactor()
		{
			int result = 1;
			if (_downSample == Downsample.Auto)
			{
				if ((Filters.PerfHint & PerformanceHint.AllowDownsampling) != 0)
				{
					result = 2;
				}
			}
			else
			{
				result = (int)_downSample;
			}

			if (_blurSize > 120f)
			{
				result *= 4;
			}
			else if (_blurSize > 60f)
			{
				result *= 2;
			}
			
			return result;
		}

		// NOTE full kernel size is double this, plus one for the center coordinate
		static int GetHalfKernelSize(float sigma)
		{
			return Mathf.CeilToInt(3.0f * sigma);
		}

		static float GetSigmaFromKernelRadius(float radius)
		{
			return radius / 3f;
		}

		static float GetWeight(int x, float sigma)
		{
			return 1.0f / (Mathf.Sqrt(Mathf.PI * 2.0f) * sigma) * Mathf.Exp(-(x * x) / (2.0f * sigma * sigma));
		}

		void UpdateKernel()
		{
			float radius = GetScaledRadius();
			float sigma = GetSigmaFromKernelRadius(radius);

			// Allocate weights
			int size = 1 + GetHalfKernelSize(sigma);
			Debug.Assert(size <= MaxRadius);
			if (_weights == null || _weights.Length != size)
			{
				_weights = new float[size];
			}

			// Generate weights
			_weights[0] = GetWeight(0, sigma);
			float total = _weights[0];
			for (int i = 1; i < size; i++)
			{
				_weights[i] = GetWeight(i, sigma);
				total += _weights[i] * 2f;
			}

			// Normalise weights
			for (int i = 0; i < size; i++)
			{
				_weights[i] /= total;
			}
		
			_kernelDirty = false;
			_materialDirty = true;
		}
	}
}