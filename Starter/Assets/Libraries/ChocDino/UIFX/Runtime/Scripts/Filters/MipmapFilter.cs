//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;

namespace ChocDino.UIFX
{
	/// <summary>
	/// Generates mipmap texture for UI component allowing less aliasing when scaling down.
	/// This is most useful for world-space rendering.
	/// </summary>
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Filters/UIFX - Mipmap Filter", 100)]
	public class MipmapFilter : FilterBase
	{
		[SerializeField] bool _generateMipMap = true;
		[SerializeField, Range(-12f, 12f)] float _mipMapBias = 0f;
		[SerializeField, Range(0f, 16f)] int _anisoLevel = 4;
		
		/// <summary></summary>
		public bool GenerateMipMap { get { return _generateMipMap; } set { ChangeProperty(ref _generateMipMap, value); } }

		/// <summary></summary>
		public float MipMapBias { get { return _mipMapBias; } set { ChangeProperty(ref _mipMapBias, value); } }

		/// <summary></summary>
		public int AnisoLevel { get { return _anisoLevel; } set { ChangeProperty(ref _anisoLevel, Mathf.Clamp(value, 0, 16)); } }

		private RenderTexture _rt;

		protected override bool DoParametersModifySource()
		{
			if (base.DoParametersModifySource())
			{
				if (!_generateMipMap && _anisoLevel <= 1) return false;
				return true;
			}
			return false;
		}

		protected override void OnEnable()
		{
			_expand = FilterExpand.None;
			base.OnEnable();
			_renderSpace = FilterRenderSpace.Canvas;
		}

		protected override void OnDisable()
		{
			if (_rt)
			{
				RenderTextureHelper.ReleaseTemporary(ref _rt);
			}
			base.OnDisable();
		}

		protected override RenderTexture RenderFilters(RenderTexture source)
		{
			if (_rt && (_rt.width != source.width || _rt.height != source.height || _rt.format != source.format || _rt.autoGenerateMips != _generateMipMap))
			{
				RenderTextureHelper.ReleaseTemporary(ref _rt);
			}
			if (!_rt)
			{
				RenderTextureDescriptor desc = new RenderTextureDescriptor(source.width, source.height, source.format, 0);
				if (_generateMipMap)
				{
					desc.mipCount = Texture.GenerateAllMips;
					desc.autoGenerateMips = true;
					desc.useMipMap = true;
				}
				else
				{
					desc.mipCount = 0;
					desc.autoGenerateMips = false;
					desc.useMipMap = false;
				}
				_rt = RenderTexture.GetTemporary(desc);
				#if UNITY_EDITOR
				_rt.name = "Mipmap Filter";
				#endif
				_rt.filterMode = FilterMode.Trilinear;
			}
			_rt.anisoLevel = _anisoLevel; 
			if (_generateMipMap)
			{
				_rt.mipMapBias = _mipMapBias;
			}
			Graphics.Blit(source, _rt);
			return _rt;
		}
	}
}