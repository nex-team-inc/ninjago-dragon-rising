//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChocDino.UIFX
{
	/// <summary>
	/// Displays a VideoPlayer via UGUI.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Sources/UIFX - Video Player Source")]
	public class VideoPlayerSource : MaskableGraphic
	{
#if UIFX_VIDEOPLAYER
		[SerializeField] VideoPlayer _videoPlayer = null;

		public VideoPlayer VideoPlayer { get => _videoPlayer; set { _videoPlayer = value; ForceUpdate(); } }

		public override Texture mainTexture { get { if (HasVideoTexture()) { return _videoPlayer.texture; } return null; } }

		internal class VideoShader
		{
			internal const string Id = "Hidden/ChocDino/UIFX/Blend-VideoPlayer";

			internal static class Prop
			{
				internal static readonly int AlphaParam = Shader.PropertyToID("_AlphaParam");
				internal static readonly int RightEyeUVOffset = Shader.PropertyToID("_RightEyeUVOffset");
			}
		}

		private static readonly Vector2 SideBySidePackingOffset = Vector2.zero;
		private static readonly Vector2 SidebySidePackingScale = new Vector2(0.5f, 1f);
		private static readonly Vector4 SideBySideUVOffset = Vector3.right * 0.5f;

		private static readonly Vector2 OverUnderPackingOffset = new Vector2(0f, 0.5f);
		private static readonly Vector2 OverUnderPackingScale = new Vector2(1f, 0.5f); 
		private static readonly Vector4 OverUnder3DUVOffset = Vector3.down * 0.5f;

		private Material _material;

		protected override void OnEnable()
		{
			var shader = Shader.Find(VideoShader.Id);
			if (shader)
			{
				_material = new Material(shader);
			}
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			ObjectHelper.Destroy(ref _material);
			base.OnDisable();
		}

		/// <summary>
		/// NOTE: OnDidApplyAnimationProperties() is called when the Animator is used to keyframe properties
		/// </summary>
		protected override void OnDidApplyAnimationProperties()
		{
			ForceUpdate();
			base.OnDidApplyAnimationProperties();
		}
		
		/// <summary>
		/// OnCanvasHierarchyChanged() is called when the Canvas is enabled/disabled
		/// </summary>
		protected override void OnCanvasHierarchyChanged()
		{
			ForceUpdate();
			base.OnCanvasHierarchyChanged();
		}

		/// <summary>
		/// OnTransformParentChanged() is called when a parent is changed, in which case we may need to get a new Canvas
		/// </summary>
		protected override void OnTransformParentChanged()
		{
			ForceUpdate();
			base.OnTransformParentChanged();
		}

		/// <summary>
		/// Forces the filter to update.  Usually this happens automatically, but in some cases you may want to force an update.
		/// </summary>
		public void ForceUpdate(bool force = false)
		{
			if (force || this.isActiveAndEnabled)
			{
				// There is no point setting the graphic dirty if it is not active/enabled (because SetMaterialDirty() will just return causing _forceUpdate to cleared prematurely)
				if (this.isActiveAndEnabled)
				{
					// We have to force the parent graphic to update so that the GetModifiedMaterial() and ModifyMesh() are called
					// TOOD: This wasteful, so ideally find a way to prevent this
					this.SetMaterialDirty();
					this.SetVerticesDirty();
				}
			}
		}

#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

			// NOTE: Have to ForceUpdate() otherwise mesh doesn't update due to ModifyMesh being called multiple times a frame in this path and _lastModifyMeshFrame preventing update
			ForceUpdate();
		}
		
		protected override void OnValidate()
		{
			base.OnValidate();

			// NOTE: Have to ForceUpdate() otherwise the Game View sometimes doesn't update the rendering, even though the Scene View does...
			ForceUpdate();
		}
#endif

		void Update()
		{
			ForceUpdate();
			//UpdateMaterial();
		}

		bool HasVideoTexture()
		{
			return _videoPlayer && _videoPlayer.texture != null;
		}

		protected override void UpdateMaterial()
		{
			if (_material)
			{
				if (HasVideoTexture())
				{
					_material.SetFloat(VideoShader.Prop.AlphaParam, 1f);

					Vector2 stereoPackingOffset = Vector2.zero;
					Vector2 stereoPackingScale = Vector2.one;
					Vector4 rightEyeUVOffset = Vector4.zero;
					if (_videoPlayer.targetCamera3DLayout != Video3DLayout.No3D)
					{
						if (_videoPlayer.targetCamera3DLayout == Video3DLayout.SideBySide3D)
						{
							stereoPackingOffset = SideBySidePackingOffset;
							stereoPackingScale = SidebySidePackingScale;
							rightEyeUVOffset = SideBySideUVOffset;
						}
						else if (_videoPlayer.targetCamera3DLayout == Video3DLayout.OverUnder3D)
						{
							stereoPackingOffset = OverUnderPackingOffset;
							stereoPackingScale = OverUnderPackingScale;
							rightEyeUVOffset = OverUnder3DUVOffset;
						}
					}
					_material.mainTextureOffset = stereoPackingOffset;
					_material.mainTextureScale = stereoPackingScale;
					_material.SetVector(VideoShader.Prop.RightEyeUVOffset, rightEyeUVOffset);
				}
				else
				{
#if UNITY_EDITOR
					if (!Application.isPlaying)
					{
						// In the editor while not palying, show a transparent rectangle so the area is visible to the user.
						_material.SetFloat(VideoShader.Prop.AlphaParam, 0.5f);
					}
					else
#endif
					{
						_material.SetFloat(VideoShader.Prop.AlphaParam, 0f);
					}
				}
			}
			base.UpdateMaterial();
		}

		public override Material GetModifiedMaterial(Material baseMaterial)
		{
			if (_material)
			{
				return _material;
			}
			return baseMaterial;
		}
#endif
	}
}