//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public class ColorAdjust
	{
		internal const string ShaderId = "Hidden/ChocDino/UIFX/ColorAdjust";

		static class ShaderProp
		{
			public static readonly int ColorMatrix = Shader.PropertyToID("_ColorMatrix");
			public static readonly int BCPO = Shader.PropertyToID("_BCPO");
			public static readonly int BrightnessRGBA = Shader.PropertyToID("_BrightnessRGBA");
			public static readonly int ContrastRGBA = Shader.PropertyToID("_ContrastRGBA");
			public static readonly int PosterizeRGBA = Shader.PropertyToID("_PosterizeRGBA");
		}
		private static class ShaderKeyword
		{
			public const string Posterize = "POSTERIZE";
		}

		private float _hue = 0.0f;
		private float _saturation = 0.0f;
		private float _value = 0.0f;
		private float _brightness = 0.0f;
		private float _contrast = 0.0f;
		private float _posterize = 255.0f;
		private float _opacity = 1f;
		private Vector4 _brightnessRGBA = Vector4.zero;
		private Vector4 _contrastRGBA = Vector4.zero;
		private Vector4 _posterizeRGBA = new Vector4(255f, 255f, 255, 255f);

		public float Hue { get { return _hue; } set { value = Mathf.Clamp(value, 0f, 360f); if (_hue != value) { _hue = value; _matrixDirty = true; } } }
		public float Saturation { get { return _saturation; } set { value = Mathf.Clamp(value, -2f, 2f); if (_saturation != value) { _saturation = value; _matrixDirty = true; } } }
		public float Value { get { return _value; } set { value = Mathf.Clamp(value, -1f, 1f); if (_value != value) { _value = value; _matrixDirty = true; } } }
		public float Brightness { get { return _brightness; } set { value = Mathf.Clamp(value, -2f, 2f); if (_brightness != value) { _brightness = value; _materialsDirty = true; } } }
		public float Contrast { get { return _contrast; } set { value = Mathf.Clamp(value, -2f, 2f); if (_contrast != value) { _contrast = value; _materialsDirty = true; } } }
		public float Posterize { get { return _posterize; } set { value = Mathf.Clamp(value, 0.01f, 255f); if (_posterize != value) { _posterize = value; _materialsDirty = true; } } }
		public float Opacity { get { return _opacity; } set { value = Mathf.Clamp01(value); if (_opacity != value) { _opacity = value; _materialsDirty = true; } } }
		public Vector4 BrightnessRGBA { get { return _brightnessRGBA; } set { if (_brightnessRGBA != value) { _brightnessRGBA = value; _materialsDirty = true; } } }
		public Vector4 ContrastRGBA { get { return _contrastRGBA; } set { if (_contrastRGBA != value) { _contrastRGBA = value; _materialsDirty = true; } } }
		public Vector4 PosterizeRGBA { get { return _posterizeRGBA; } set { if (_posterizeRGBA != value) { _posterizeRGBA = value; _materialsDirty = true; } } }

		private float _strength = 1f;
		public float Strength { get { return _strength; } set { value = Mathf.Clamp01(value); if (_strength != value) { _strength = value; _matrixDirty = true; } } }

		private Material _material;
		private RenderTexture _resultTexture;
		private RenderTexture _sourceTexture;
		private Matrix4x4 _colorMatrix;

		private bool _matrixDirty = true;
		#pragma warning disable 0414		// suppress warnings for "The field XYZ is assigned but its value is never used"
		private bool _materialsDirty = true;
		#pragma warning restore 0414

		public RenderTexture Process(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture != null);

			SetupResources(sourceTexture);

			if (_matrixDirty)
			{
				_matrixDirty = false;
				BuildHSVMatrix(_hue, _saturation * _strength, _value * _strength, ref _colorMatrix);
				_materialsDirty = true;
			}

			// Note: we just update the material every frame in the editor, because when ColorSpace is toggled (rare case I know)
			// the non-property materials get zero'ed which in this case is the color matrix which can't be a property.
			#if !UNITY_EDITOR
			if (_materialsDirty)
			#endif
			{
				UpdateMaterials();
				_materialsDirty = false;
			}

			Graphics.Blit(_sourceTexture, _resultTexture, _material, 0);

			return _resultTexture;
		}

		public void FreeResources()
		{
			FreeShaders();
			FreeTextures();
		}

		void SetupResources(RenderTexture sourceTexture)
		{
			if (_sourceTexture != null)
			{
				bool sourceTextureSizeChanged = (sourceTexture == null) || ((_sourceTexture.width != sourceTexture.width) || (_sourceTexture.height != sourceTexture.height));
				if (sourceTextureSizeChanged)
				{
					FreeTextures();
					_materialsDirty = true;
				}
			}
			if (_sourceTexture == null && sourceTexture != null)
			{
				CreateTextures(sourceTexture);
			}
			if (_sourceTexture != sourceTexture)
			{
				_materialsDirty = true;
				_sourceTexture = sourceTexture;
			}
			if (_material == null)
			{
				CreateShaders();
			}
		}

		void UpdateMaterials()
		{
			if (_posterize < 255f || _posterizeRGBA.x < 255f || _posterizeRGBA.y < 255f || _posterizeRGBA.z < 255f || _posterizeRGBA.w < 255f)
			{
				_material.EnableKeyword(ShaderKeyword.Posterize);
			}
			else
			{
				_material.DisableKeyword(ShaderKeyword.Posterize);
			}

			if (_strength >= 1f)
			{
				_material.SetMatrix(ShaderProp.ColorMatrix, _colorMatrix);
				_material.SetVector(ShaderProp.BCPO, new Vector4(_brightness, _contrast, Mathf.Ceil(_posterize), _opacity));
				_material.SetVector(ShaderProp.BrightnessRGBA, _brightnessRGBA);
				_material.SetVector(ShaderProp.ContrastRGBA, _contrastRGBA);
				_material.SetVector(ShaderProp.PosterizeRGBA, _posterizeRGBA);
			}
			else
			{
				_material.SetMatrix(ShaderProp.ColorMatrix, MathUtils.LerpUnclamped(Matrix4x4.identity, _colorMatrix, _strength, true));
				_material.SetVector(ShaderProp.BCPO, new Vector4(Mathf.LerpUnclamped(0f, _brightness, _strength), Mathf.LerpUnclamped(0f, _contrast, _strength), Mathf.Ceil(Mathf.LerpUnclamped(255f, _posterize, _strength)), Mathf.LerpUnclamped(1f, _opacity, _strength)));
				_material.SetVector(ShaderProp.BrightnessRGBA, Vector4.LerpUnclamped(Vector4.zero, _brightnessRGBA, _strength));
				_material.SetVector(ShaderProp.ContrastRGBA, Vector4.LerpUnclamped(Vector4.zero, _contrastRGBA, _strength));
				_material.SetVector(ShaderProp.PosterizeRGBA, Vector4.LerpUnclamped(Vector4.one * 255f, _posterizeRGBA, _strength));
			}
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
			_material = CreateMaterialFromShader(ShaderId);
			Debug.Assert(_material != null);
		}

		void CreateTextures(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture.width > 0 && sourceTexture.height > 0);

			int w = sourceTexture.width / 1;
			int h = sourceTexture.height / 1;
			RenderTextureFormat format = sourceTexture.format;

			_resultTexture = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);
			#if UNITY_EDITOR
			_resultTexture.name = "ColorAdjust";
			#endif
		}

		void FreeShaders()
		{
			ObjectHelper.Destroy(ref _material);
		}

		void FreeTextures()
		{
			RenderTextureHelper.ReleaseTemporary(ref _resultTexture);
			_sourceTexture = null;
		}

		private static void BuildHSVMatrix(float hue, float saturation, float value, ref Matrix4x4 result)
		{
			// HSV matrix from HannesH post at https://stackoverflow.com/questions/8507885/shift-hue-of-an-rgb-color/30488508#30488508
			float cosA = (saturation + 1f) * Mathf.Cos(hue * Mathf.Deg2Rad);
			float sinA = (saturation + 1f) * Mathf.Sin(hue * Mathf.Deg2Rad);

			const float aThird = 1.0f/3.0f;
			float rootThird = Mathf.Sqrt(aThird);
			float oneMinusCosA = (1.0f - cosA);
			float aThirdOfOneMinusCosA = aThird * oneMinusCosA;
			float rootThirdTimesSinA =  rootThird * sinA;
			float plus = aThirdOfOneMinusCosA + rootThirdTimesSinA;
			float minus = aThirdOfOneMinusCosA - rootThirdTimesSinA;

			Matrix4x4 mtxHueSat = Matrix4x4.identity;
			mtxHueSat[0,0] = cosA + oneMinusCosA / 3f;
			mtxHueSat[0,1] = minus;
			mtxHueSat[0,2] = plus;
			mtxHueSat[1,0] = plus;
			mtxHueSat[1,1] = cosA + aThirdOfOneMinusCosA;
			mtxHueSat[1,2] = minus;
			mtxHueSat[2,0] = minus;
			mtxHueSat[2,1] = plus;
			mtxHueSat[2,2] = cosA + aThirdOfOneMinusCosA;

			// Allows value to range from -1.0 to 8.0
			float valueRanged = value + 1.0f;
			if (value > 0f)
			{
				valueRanged = Mathf.Lerp(1f, 4f, value);
			}

			result = mtxHueSat;
			result *= Matrix4x4.Scale(new Vector3(valueRanged, valueRanged, valueRanged));

			/*
			// NOTE: We don't apply contrast or brightness, as they don't look correct for linear color space
			// So instead this is done in the shader as a separate calculation.
			Matrix4x4 mtxContrast = Matrix4x4.Scale(new Vector3(_contrast + 1.0f, _contrast + 1.0f, _contrast + 1.0f));
			float c = (1.0f - (_contrast+1.0f)) * 0.5f;
			Matrix4x4 mtxContrastAdjust = Matrix4x4.Translate(new Vector3(c, c, c));
			result *= ((mtxContrastAdjust * mtxContrast));

			Matrix4x4 mtxBrightness = Matrix4x4.Translate(new Vector3(_brightness, _brightness, _brightness));
			result *= (mtxBrightness);
			*/
		}
	}
}