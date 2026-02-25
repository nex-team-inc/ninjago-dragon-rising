//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public class Extrude
	{
		public ExtrudeProjection Projection { get { return _projection; } set { ChangeProperty(ref _projection, value); } }
		public float Angle { get { return _angle; } set { ChangeProperty(ref _angle, value); } }
		public float Distance { get { return _distance; } set { ChangeProperty(ref _distance, value); } }
		public float PerspectiveDistance { get { return _perspectiveDistance; } set { ChangeProperty(ref _perspectiveDistance, value); } }
		public Color Color1 { get { return _color1; } set { ChangeProperty(ref _color1, value); } }
		public Color Color2 { get { return _color2; } set { ChangeProperty(ref _color2, value); } }
		public bool UseGradientTexture { get { return _useGradientTexture; } set { _useGradientTexture = value; _materialsDirty = true; } }
		public Texture GradientTexture { get { return _gradientTexture; } set { _gradientTexture = value; _materialsDirty = true; } }
		public bool ReverseFill { get { return _reverseFill; } set { ChangeProperty(ref _reverseFill, value); } }
		public float Scroll { get { return _scroll; } set { ChangeProperty(ref _scroll, value); } }
		public bool MultiplySource { get { return _multiplySource; } set { ChangeProperty(ref _multiplySource, value); } }

		public Rect RectRatio { get { return _rectRatio; } set { ChangeProperty(ref _rectRatio, value); } }

		private ExtrudeProjection _projection = ExtrudeProjection.Perspective;
		private float _angle = 135f;
		private float _distance = 8f;
		private float _perspectiveDistance = 0f;
		private bool _useGradientTexture = false;
		private Color _color1 = Color.black;
		private Color _color2 = Color.black;
		private Texture _gradientTexture;
		private bool _reverseFill = false;
		private float _scroll = 0f;
		private bool _multiplySource = true;
		private Rect _rectRatio;

		private Material _material;
		private RenderTexture _rt;
		private RenderTexture _sourceTexture;

		private bool _materialsDirty = true;
		private FilterBase _parentFilter = null;
		
		internal const string ShaderId = "Hidden/ChocDino/UIFX/Extrude";

		static class ShaderProp
		{
			public static readonly int Length = Shader.PropertyToID("_Length");
			public static readonly int PixelStep = Shader.PropertyToID("_PixelStep");
			public static readonly int ColorFront = Shader.PropertyToID("_ColorFront");
			public static readonly int ColorBack = Shader.PropertyToID("_ColorBack");
			public static readonly int GradientTex = Shader.PropertyToID("_GradientTex");
			public static readonly int VanishingPoint = Shader.PropertyToID("_VanishingPoint");
			public static readonly int Ratio = Shader.PropertyToID("_Ratio");
			public static readonly int ReverseFill = Shader.PropertyToID("_ReverseFill");
			public static readonly int Scroll = Shader.PropertyToID("_Scroll");
		}
		static class ShaderKeyword
		{
			public const string UseGradientTexture = "USE_GRADIENT_TEXTURE";
			public const string MultiplySourceColor = "MULTIPLY_SOURCE_COLOR";
		}
		static class ShaderPass
		{
			public const int Perspective = 0;
			public const int Orthographic = 1;
		}

		private Extrude() { }

		public Extrude(FilterBase parentFilter)
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

		internal static Vector2 AngleToOffset(float angle, Vector2 scale)
		{
			return new Vector2(Mathf.Sin(-angle * Mathf.Deg2Rad) * scale.x, Mathf.Cos(-angle * Mathf.Deg2Rad + Mathf.PI) * scale.y);
		}

		private Rect _sourceRect;

		public bool GetAdjustedBounds(Rect rect, ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			if (_projection == ExtrudeProjection.Orthographic)
			{
				float maxDistance = GetScaledDistance(_distance);
				if (maxDistance != 0f)
				{
					// NOTE: We negate the offsets because they are going from the end of the shadow to the start
					Vector2 offset = -AngleToOffset(_angle, new Vector2(maxDistance, maxDistance));
					leftDown += new Vector2Int(Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.x))), Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.y))));
					rightUp += new Vector2Int(Mathf.CeilToInt(Mathf.Max(0f, offset.x)), Mathf.CeilToInt(Mathf.Max(0f, offset.y)));

					// NOTE: When the Graphic is a solid color, then we need to add padding otherwise it will keep sampling that texture when
					// the sampling wraps, which leads to incorrect rendering
					{
						leftDown += new Vector2Int(1, 1);
						rightUp += new Vector2Int(1, 1);
					}

					return true;
				}
			}
			else if (_projection == ExtrudeProjection.Perspective)
			{
				// Store the rectangle before we have enlarged it
				_sourceRect = rect;

				// p is in pixel units
				Vector2 p = GetScaledDistance(_perspectiveDistance) * -AngleToOffset(_angle, Vector2.one);

				// TODO: Add scaling by _Distance
				// TODO: Changing screen resolution (canvasScale affects how this is rendered when it shouldn't.)

				// convert relative to UV coordinates
				//if (_sourceTexture)
				float rw = rect.width;
				float rh = rect.height;
				{
					if (_parentFilter.RenderSpace == FilterRenderSpace.Canvas)
					{
						rw *= _parentFilter.ResolutionScalingFactor;
						rh *= _parentFilter.ResolutionScalingFactor;
					}
					p.x = (p.x + rw * 0.5f) / rw;
					p.y = (p.y + rh * 0.5f) / rh;
					//p /= _parentFilter.ResolutionScalingFactor;
				}

				float distFromTopEdge = Mathf.Max(0f, (p.y - 1f) * rh);
				float distFromBotEdge = Mathf.Max(0f, (0f - p.y) * rh);

				float distFromRightEdge = Mathf.Max(0f, (p.x - 1f) * rw);
				float distFromLeftEdge = Mathf.Max(0f, (0f - p.x) * rw);

				leftDown += new Vector2Int(Mathf.CeilToInt(distFromLeftEdge+4f), Mathf.CeilToInt(distFromBotEdge+4f));
				rightUp += new Vector2Int(Mathf.CeilToInt(distFromRightEdge+4f), Mathf.CeilToInt(distFromTopEdge+4f));

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

			if (_projection == ExtrudeProjection.Perspective)
			{
				Graphics.Blit(_sourceTexture, _rt, _material, ShaderPass.Perspective);
			}
			else if (_projection == ExtrudeProjection.Orthographic)
			{
				Graphics.Blit(_sourceTexture, _rt, _material, ShaderPass.Orthographic);
			}
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

		void UpdateMaterials()
		{
			float maxDistance = Mathf.Abs(GetScaledDistance(_distance));
			float distance = maxDistance;

			_material.SetInt(ShaderProp.Length, Mathf.CeilToInt(distance));
			if (_projection == ExtrudeProjection.Orthographic)
			{
				Vector2 unitTexelOffset = Mathf.Sign(_distance) * AngleToOffset(_angle, new Vector2(1.0f / _sourceTexture.width, 1.0f / _sourceTexture.height));
				_material.SetVector(ShaderProp.PixelStep, unitTexelOffset);
			}
			else if (_projection == ExtrudeProjection.Perspective)
			{
				// p is in pixel units
				Vector2 p = GetScaledDistance(_perspectiveDistance) * -AngleToOffset(_angle, Vector2.one);

				float sw = _sourceTexture.width;
				float sh = _sourceTexture.height;
				float rh = _sourceRect.height;
				if (_parentFilter.RenderSpace == FilterRenderSpace.Canvas)
				{
					rh *= _parentFilter.ResolutionScalingFactor;
				}

				// When the filter is disabled and _expand != FilterExpand.Expand, _sourceRect doesn't get initialised,
				// so just set it to a sane value.
				if (rh == 0f)
				{
					rh = _rectRatio.height;
				}

				// convert relative to UV coordinates relative to the texture we're rendering (which can have padding)
				p.x = (p.x + (sw * 0.5f)) / sw;
				p.y = (p.y + (sh * 0.5f)) / sh;

				_material.SetVector(ShaderProp.VanishingPoint, new Vector4(p.x, p.y, 0f, 0f));
				_material.SetVector(ShaderProp.Ratio, new Vector4(0f, 0f, sh / rh, 0f));
			}

			if (_useGradientTexture)
			{
				_material.EnableKeyword(ShaderKeyword.UseGradientTexture);
				_material.SetTexture(ShaderProp.GradientTex, _gradientTexture);
			}
			else
			{
				_material.DisableKeyword(ShaderKeyword.UseGradientTexture);
				_material.SetColor(ShaderProp.ColorFront, _color1);
				_material.SetColor(ShaderProp.ColorBack, _color2);
			}
			if (_multiplySource)
			{
				_material.EnableKeyword(ShaderKeyword.MultiplySourceColor);
			}
			else
			{
				_material.DisableKeyword(ShaderKeyword.MultiplySourceColor);
			}

			_material.SetFloat(ShaderProp.ReverseFill, _reverseFill ? 1f: 0f);
			float scroll = (Mathf.Abs(_scroll) % 100f) * Mathf.Sign(_scroll);
			_material.SetFloat(ShaderProp.Scroll, scroll);

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
			int w = sourceTexture.width / 1;
			int h = sourceTexture.height / 1;

			RenderTextureFormat format = sourceTexture.format;

			_rt = RenderTexture.GetTemporary(w, h, 0, format, RenderTextureReadWrite.Linear);

			#if UNITY_EDITOR
			_rt.name = "Extrude";
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