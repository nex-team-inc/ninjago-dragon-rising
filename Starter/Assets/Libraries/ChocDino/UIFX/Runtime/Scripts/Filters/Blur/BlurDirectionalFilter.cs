//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	public enum BlurDirectionalBlend
	{
		Replace,
		Behind,
		Over,
		Additive,
	}

	public enum BlurDirectionalWeighting
	{
		Linear,
		Falloff,
	}

	public enum BlurDirectionalSide
	{
		One,
		Both,
	}

	/// <summary>
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Blur Directional Filter")]
	public class BlurDirectionalFilter : FilterBase
	{
		[Tooltip("The clockwise angle. Range is [0..360]. Default is 135.0")]
		[Range(0f, 360f)]
		[SerializeField] float _angle = 135f;

		[Tooltip("The length of the blur in pixels. Range is [-512..512]. Default is 32.0")]
		[Range(-512f, 512f)]
		[SerializeField] float _length = 32f;

		[Tooltip("The type of weights to use for the blur, this changes the visual appearance with Falloff looking higher quality, for little extra cost.")]
		[SerializeField] BlurDirectionalWeighting _weights = BlurDirectionalWeighting.Falloff;

		[Tooltip("")]
		[Range(0f, 8f)]
		[SerializeField] float _weightsPower = 1f;

		[Tooltip("")]
		[SerializeField] BlurDirectionalSide _side = BlurDirectionalSide.Both;

		[Tooltip("The amount of dithering to apply, useful to hide banding artifacts and also for styling. Range is [0..1]. Default is 0.0")]
		[Range(0f, 1f)]
		[SerializeField] float _dither = 0f;

		[Tooltip("Toggle the use of the alpha curve to fade to transparent as Strength increases.")]
		[SerializeField] bool _applyAlphaCurve = false;

		[Tooltip("An optional curve to allow the Graphic to fade to transparent as the Strength property increases.")]
		[SerializeField] AnimationCurve _alphaCurve = new AnimationCurve(new Keyframe(0f, 1f, -1f, -1f), new Keyframe(1f, 0f, -1f, -1f));

		[Tooltip("Tint (multiply) the blurred color by this for styling.")]
		[SerializeField] Color _tintColor = Color.white;

		[Tooltip("")]
		[Range(0f, 4f)]
		[SerializeField] float _power = 1f;

		[Tooltip("")]
		[Range(0f, 8f)]
		[SerializeField] float _intensity = 1f;

		[Tooltip("How the source graphic and the blurred graphic are blended/composited together.")]
		[SerializeField] BlurDirectionalBlend _blend = BlurDirectionalBlend.Replace;

		/// <summary>The clockwise angle. Range is [0..360]. Default is 135.0</summary>
		public float Angle { get { return _angle; } set { ChangeProperty(ref _angle, value); } }

		/// <summary>The length of the blur in pixels. Range is [-256..256]. Default is 16.0</summary>
		public float Length { get { return _length; } set { ChangeProperty(ref _length, value); } }

		/// <summary>The type of weights to use for the blur, this changes the visual appearance with Falloff looking higher quality, for little extra cost.</summary>
		public BlurDirectionalWeighting Weights { get { return _weights; } set { ChangeProperty(ref _weights, value); } }

		/// <summary></summary>
		public float WeightsPower { get { return _weightsPower; } set { ChangeProperty(ref _weightsPower, value); } }

		/// <summary></summary>
		public BlurDirectionalSide Side { get { return _side; } set { ChangeProperty(ref _side, value); } }

		/// <summary>The amount of dithering to apply, useful to hide banding artifacts and also for styling. Range is [0..1]. Default is 0.0</summary>
		public float Dither { get { return _dither; } set { ChangeProperty(ref _dither, value); } }

		/// <summary>Toggle the use of the alpha curve to fade to transparent as Strength increases.</summary>
		public bool ApplyAlphaCurve { get { return _applyAlphaCurve; } set { ChangeProperty(ref _applyAlphaCurve, value); } }

		/// <summary>An optional curve to allow the Graphic to fade to transparent as the Strength property increases.</summary>
		public AnimationCurve AlphaCurve { get { return _alphaCurve; } set { ChangePropertyRef(ref _alphaCurve, value); } }

		/// <summary>Tint (multiply) the blurred color by this for styling.</summary>
		public Color TintColor { get { return _tintColor; } set { ChangeProperty(ref _tintColor, value); } }

		/// <summary></summary>
		public float Power { get { return _power; } set { ChangeProperty(ref _power, value); } }

		/// <summary></summary>
		public float Intensity { get { return _intensity; } set { ChangeProperty(ref _intensity, value); } }

		/// <summary>How the source graphic and the blurred graphic are blended/composited together.</summary>
		public BlurDirectionalBlend Blend { get { return _blend; } set { ChangeProperty(ref _blend, value); } }

		class BlurShader
		{
			internal const string Path = "Hidden/ChocDino/UIFX/Blur-Directional";

			static class Prop
			{
				public readonly static int TexelStep = Shader.PropertyToID("_TexelStep");
				public readonly static int KernelRadius = Shader.PropertyToID("_KernelRadius");
				public readonly static int Dither = Shader.PropertyToID("_Dither");
				public readonly static int WeightsPower = Shader.PropertyToID("_WeightsPower");
			}
			static class Pass
			{
				public const int Linear = 0;
				public const int Falloff = 1;
			}
			static class Keyword
			{
				public const string UseDither = "USE_DITHER";
				public const string DirBoth = "DIR_BOTH";
			}

			private RenderTexture _rt;
			private Material _material;

			void CreateResources()
			{
				if (_material == null)
				{
					Shader shader = Shader.Find(Path);
					if (shader != null)
					{
						_material = new Material(shader);
					}
				}
			}

			internal void FreeResources()
			{
				ObjectHelper.Destroy(ref _material);
				RenderTextureHelper.ReleaseTemporary(ref _rt);
			}

			internal RenderTexture Render(RenderTexture sourceTexture, float radius, Vector2 texelStep, bool weightsLinear, float weightsPower, float dither, BlurDirectionalSide side)
			{
				int width = sourceTexture.width;
				int height = sourceTexture.height;
				if (_rt != null && (_rt.width != width || _rt.height != height))
				{
					RenderTextureHelper.ReleaseTemporary(ref _rt);
				}
				if (_rt == null)
				{
					RenderTextureFormat format = sourceTexture.format;
					if ((Filters.PerfHint & PerformanceHint.UseLessPrecision) != 0)
					{
						// TODO: create based on the input texture format, but just with less precision
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Default))
						{
							format = RenderTextureFormat.Default;
						}
					}
					else if ((Filters.PerfHint & PerformanceHint.UseMorePrecision) != 0)
					{
						// TODO: create based on the input texture format, but just with more precision
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
						{
							format = RenderTextureFormat.ARGBHalf;
						}
					}
					_rt = RenderTexture.GetTemporary(width, height, 0, format, RenderTextureReadWrite.Linear);
				}

				CreateResources();

				_material.SetVector(Prop.TexelStep, texelStep);
				_material.SetFloat(Prop.KernelRadius, radius);
				
				if (dither > 0f)
				{
					_material.SetFloat(Prop.Dither, dither);
					_material.EnableKeyword(Keyword.UseDither);
				}
				else
				{
					_material.DisableKeyword(Keyword.UseDither);
				}

				switch (side)
				{
					case BlurDirectionalSide.One:
					_material.DisableKeyword(Keyword.DirBoth);
					break;
					case BlurDirectionalSide.Both:
					_material.EnableKeyword(Keyword.DirBoth);
					break;
				}

				if (!weightsLinear)
				{
					_material.SetFloat(Prop.WeightsPower, weightsPower);
				}

				Graphics.Blit(sourceTexture, _rt, _material, weightsLinear ? Pass.Linear : Pass.Falloff);
				_rt.IncrementUpdateCount();

				return _rt;
			}
		}

		internal class CompositeShader
		{
			internal const string Path = "Hidden/ChocDino/UIFX/Blend-Composite";

			static class Prop
			{
				public readonly static int TintColor = Shader.PropertyToID("_TintColor");
				public readonly static int PowerIntensity = Shader.PropertyToID("_PowerIntensity");
			}
			static class Keyword
			{
				public const string BlendBehind = "BLEND_BEHIND";
				public const string BlendOver = "BLEND_OVER";
				public const string BlendAdditive = "BLEND_ADDITIVE";
			}

			public static void Apply(Material material, float strength, Color tintColor, float power, float intensity, BlurDirectionalBlend blendMode)
			{
				material.SetFloat(FilterBase.ShaderProp.Strength, strength);
				material.SetVector(Prop.PowerIntensity, new Vector2(power, intensity));
				material.SetColor(Prop.TintColor, tintColor);
				switch (blendMode)
				{
					default:
					case BlurDirectionalBlend.Replace:
					material.DisableKeyword(Keyword.BlendBehind);
					material.DisableKeyword(Keyword.BlendOver);
					material.DisableKeyword(Keyword.BlendAdditive);
					break;
					case BlurDirectionalBlend.Behind:
					material.EnableKeyword(Keyword.BlendBehind);
					material.DisableKeyword(Keyword.BlendOver);
					material.DisableKeyword(Keyword.BlendAdditive);
					break;
					case BlurDirectionalBlend.Over:
					material.DisableKeyword(Keyword.BlendBehind);
					material.EnableKeyword(Keyword.BlendOver);
					material.DisableKeyword(Keyword.BlendAdditive);
					break;
					case BlurDirectionalBlend.Additive:
					material.DisableKeyword(Keyword.BlendBehind);
					material.DisableKeyword(Keyword.BlendOver);
					material.EnableKeyword(Keyword.BlendAdditive);
					break;
				}
			}
		}

		private BlurShader _blurShader;

		protected override string GetDisplayShaderPath()
		{
			return CompositeShader.Path;
		}

		protected override bool DoParametersModifySource()
		{
			if (base.DoParametersModifySource())
			{
				if (_length == 0f && _tintColor == Color.white) return false;
				return true;
			}
			return false;
		}

		protected override void OnEnable()
		{
			_rectAdjustOptions.padding = 2;
			_rectAdjustOptions.roundToNextMultiple = 16;
			_blurShader = new BlurShader();
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			if (_blurShader != null)
			{
				_blurShader.FreeResources();
				_blurShader = null;
			}
			base.OnDisable();
		}

		internal override float GetAlpha()
		{
			float alpha = 1f;
			if (_alphaCurve != null && _applyAlphaCurve)
			{
				if (_alphaCurve.length > 0)
				{
					alpha = _alphaCurve.Evaluate(_strength);
				}
			}
			return alpha;
		}

		private Vector2 GetDirection()
		{
			return new Vector2(Mathf.Sin(-_angle * Mathf.Deg2Rad), Mathf.Cos(-_angle * Mathf.Deg2Rad + Mathf.PI));
		}

		protected override void GetFilterAdjustSize(ref Vector2Int leftDown, ref Vector2Int rightUp)
		{
			float maxDistance = _length * ResolutionScalingFactor * _strength;
			if (maxDistance != 0f)
			{
				Vector2 offset = GetDirection() * maxDistance;

				// With falloff weights the blur doesn't visibly reach as far, so we apply some heuristic logic to shrink the rectangle size
				if (_weights == BlurDirectionalWeighting.Falloff)
				{
					float w = 1f;
					if (_weightsPower > 1f)
					{
						w = (_weightsPower - 1f) / 7f;
						w = Mathf.Lerp(1f, 1.5f, w);
					}
					offset.x /= w;
					offset.y /= w;
				}

				if (_side == BlurDirectionalSide.Both)
				{
					int x = Mathf.CeilToInt(Mathf.Abs(offset.x));
					int y = Mathf.CeilToInt(Mathf.Abs(offset.y));

					leftDown += new Vector2Int(x, y);
					rightUp += new Vector2Int(x, y);
				}
				else
				{
					offset *= -1f;
					leftDown += new Vector2Int(Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.x))), Mathf.CeilToInt(Mathf.Abs(Mathf.Min(0f, offset.y))));
					rightUp += new Vector2Int(Mathf.CeilToInt(Mathf.Max(0f, offset.x)), Mathf.CeilToInt(Mathf.Max(0f, offset.y)));
				}
			}
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			if (GetAlpha() > 0f)
			{
				Vector2 texelStep = GetDirection() * new Vector2(1.0f / source.width, 1.0f / source.height);

				return _blurShader.Render(source, Mathf.Abs(_length) * ResolutionScalingFactor * _strength, texelStep * Mathf.Sign(_length), _weights == BlurDirectionalWeighting.Linear || _weightsPower == 0f, _weightsPower, _dither * _strength * 0.1f, _side);
			}
			return null;
		}

		protected override void SetupDisplayMaterial(Texture source, Texture result)
		{
			CompositeShader.Apply(_displayMaterial, _strength, Color.Lerp(Color.white, _tintColor, _strength), Mathf.Lerp(1f, _power, _strength), Mathf.Lerp(1f, _intensity, _strength), _blend);
			base.SetupDisplayMaterial(source, result);
		}
	}
}