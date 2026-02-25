//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum LongShadowCompositeMode
	{
		Normal,
		Cutout,
		Shadow,
	}

	public enum LongShadowMethod
	{
		Normal,
		DistanceMap,
	}

	public class LongShadow
	{
		public float Angle { get { return _angle; } set { ChangeProperty(ref _angle, value); } }
		public float Distance { get { return _distance; } set { ChangeProperty(ref _distance, value); } }
		public float StepSize { get { return _stepSize; } set { ChangeProperty(ref _stepSize, value); } }
		public float Pivot { get { return _pivot; } set { ChangeProperty(ref _pivot, value); } }
		public Color Color1 { get { return _color1; } set { ChangeProperty(ref _color1, value); } }
		public Color Color2 { get { return _color2; } set { ChangeProperty(ref _color2, value); } }
		public LongShadowMethod Method { get { return _method; } set { ChangeProperty(ref _method, value); } }
		public RenderTexture DistanceTexture { get { return _distanceTexture; } set { ChangePropertyRef(ref _distanceTexture, value); } }

		private float _angle = 135f;
		private float _distance = 8f;
		private float _stepSize = 1f;
		private float _pivot = 0f;
		private Color _color1 = Color.black;
		private Color _color2 = Color.black;
		private LongShadowMethod _method = LongShadowMethod.Normal;
		private RenderTexture _distanceTexture;

		private Material _material;
		private RenderTexture _rt;
		private RenderTexture _sourceTexture;

		private bool _materialsDirty = true;
		private FilterBase _parentFilter = null;
		
		internal const string ShaderId = "Hidden/ChocDino/UIFX/LongShadow";

		static class ShaderProp
		{
			public static readonly int SourceAlpha = Shader.PropertyToID("_SourceAlpha");
			public static readonly int OffsetStart = Shader.PropertyToID("_OffsetStart");
			public static readonly int Length = Shader.PropertyToID("_Length");
			public static readonly int PixelStep = Shader.PropertyToID("_PixelStep");
			public static readonly int ColorFront = Shader.PropertyToID("_ColorFront");
			public static readonly int ColorBack = Shader.PropertyToID("_ColorBack");
			public static readonly int DistanceTex = Shader.PropertyToID("_DistanceTex");
		}
		private static class ShaderPass
		{
			internal const int Normal = 0;
			internal const int DistanceMap = 1;
		}

		private LongShadow() { }

		public LongShadow(FilterBase parentFilter)
		{
			Debug.Assert(parentFilter != null);
			_parentFilter = parentFilter;
		}

		private void ChangeProperty<T>(ref T backing, T value) 	where T : struct
		{ 
			if (ObjectHelper.ChangeProperty(ref backing, value))
			{
				backing = value;
				_materialsDirty = true;
			}
		}
		
		protected void ChangePropertyRef<T>(ref T backing, T value) where T : class
		{
			if (backing != value)
			{
				backing = value;
				_materialsDirty = true;
			}
		}

		private static Vector2 AngleToOffset(float angle, Vector2 scale)
		{
			return new Vector2(Mathf.Sin(-angle * Mathf.Deg2Rad) * scale.x, Mathf.Cos(-angle * Mathf.Deg2Rad + Mathf.PI) * scale.y);
		}

		public bool GetAdjustedBounds(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			float maxDistance = _distance * _parentFilter.ResolutionScalingFactor;
			if (maxDistance != 0f)
			{
				// NOTE: We negate the offsets because they are going from the end of the shadow to the start
				Vector2 offset = -AngleToOffset(_angle, new Vector2(maxDistance, maxDistance));
				leftDown += new Vector2Int(Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.x))), Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.y))));
				rightUp += new Vector2Int(Mathf.CeilToInt(Mathf.Max(0f, offset.x)), Mathf.CeilToInt(Mathf.Max(0f, offset.y)));

				// NOTE: When the Graphic is a solid color, then we need to add padding otherwise it will keep sampling that texture when
				// the sampling wraps, which leads to incorrect rendering
				if (_method == LongShadowMethod.Normal)
				{
					leftDown += new Vector2Int(1, 1);
					rightUp += new Vector2Int(1, 1);
				}

				return true;
			}
			return false;
		}

		public RenderTexture Process(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture != null);

			RenderTexture prevRT = RenderTexture.active;

			SetupResources(sourceTexture);

			if (_materialsDirty)
			{
				UpdateMaterials();
			}

			int pass = (_method == LongShadowMethod.Normal) ? ShaderPass.Normal : ShaderPass.DistanceMap;
			Graphics.Blit(_sourceTexture, _rt, _material, pass);
			_rt.IncrementUpdateCount();

			RenderTexture.active = prevRT;

			return _rt;
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

		private float GetScaledDistance(float distance)
		{
			float resolutionScale = _parentFilter.RenderSpace == FilterRenderSpace.Canvas ? 1f : _parentFilter.ResolutionScalingFactor;
			resolutionScale = _parentFilter.ResolutionScalingFactor;
			return distance * resolutionScale;
		}

		internal Vector2 GetTextureOffset()
		{
			float maxDistance = Mathf.Abs(GetScaledDistance(_distance));
			float pivotNorm = (_pivot + 1.0f) * 0.5f;
			float startT = MathUtils.Lerp3(1f, 0f, 0f, pivotNorm);
			float endT = MathUtils.Lerp3(1f, 1f, 0f, pivotNorm);
			float startDistance = maxDistance * startT;

			Vector2 unitTexelOffset = Mathf.Sign(_distance) * AngleToOffset(_angle, new Vector2(1.0f / _sourceTexture.width, 1.0f / _sourceTexture.height));
			Vector2 offsetStart = unitTexelOffset * startDistance;
			return offsetStart;
		}

		void UpdateMaterials()
		{
			float resolutionScale = _parentFilter.ResolutionScalingFactor;//_parentFilter.RenderSpace == FilterRenderSpace.Canvas ? 1f : _parentFilter.ResolutionScalingFactor;
			float maxDistance = Mathf.Abs(_distance * resolutionScale);
			float pivotNorm = (_pivot + 1.0f) * 0.5f;
			float startT = MathUtils.Lerp3(1f, 0f, 0f, pivotNorm);
			float endT = MathUtils.Lerp3(1f, 1f, 0f, pivotNorm);
			float startDistance = maxDistance * startT;
			float endDistance = maxDistance * endT;
			float distance = (endDistance - startDistance);
			float stepSize = 1f;
			if (_stepSize != 1f)
			{
				stepSize = _stepSize * resolutionScale;
			}

			Vector2 unitTexelOffset = Mathf.Sign(_distance) * AngleToOffset(_angle, new Vector2(1.0f / _sourceTexture.width, 1.0f / _sourceTexture.height));
			Vector2 offsetStart = unitTexelOffset * startDistance;

			_material.SetVector(ShaderProp.OffsetStart, offsetStart);
			_material.SetInt(ShaderProp.Length, Mathf.CeilToInt(distance / stepSize));
			_material.SetVector(ShaderProp.PixelStep, unitTexelOffset * stepSize);
			_material.SetColor(ShaderProp.ColorFront, _color1);
			_material.SetColor(ShaderProp.ColorBack, _color2);

			if (_method == LongShadowMethod.DistanceMap)
			{
				_material.SetTexture(ShaderProp.DistanceTex, _distanceTexture);
			}

			_materialsDirty = false;
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
			_materialsDirty = true;
		}

		void CreateTextures(RenderTexture sourceTexture)
		{
			Debug.Assert(sourceTexture.width > 0 && sourceTexture.height > 0);
			int w = sourceTexture.width / 1;
			int h = sourceTexture.height / 1;

			RenderTextureFormat format = sourceTexture.format;

			_rt = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);

			#if UNITY_EDITOR
			_rt.name = "LongShadow";
			#endif
		}

		void FreeShaders()
		{
			ObjectHelper.Destroy(ref _material);
		}

		void FreeTextures()
		{
			RenderTextureHelper.ReleaseTemporary(ref _rt);
			_sourceTexture = null;
		}
	}
}