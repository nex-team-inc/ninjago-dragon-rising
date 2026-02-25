//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	// TODO: make the blur factor (transparency in our case) relative to the amount of motion in SCREEN space..(1 / (1 + d))

	/// <summary>
	/// The MotionBlurSimple component is a visual effect that can be applied to any UI (uGUI) components 
	/// to create an approximate motion blur effect when the UI components are in motion.
	/// </summary>
	/// <remark>
	/// How it works:
	/// 1. Store the mesh and transforms for a UI component for the previous and current frames.
	/// 2. Generates a new mesh containing multiple copies of the stored meshes interpolated from previous to current mesh.
	/// 3. Replace the UI component mesh with the new motion blur mesh with a reduced per-vertex alpha (BlendStrength).
	/// 5. If no motion is detected then the effect is disabled.
	///
	/// Comparison between MotionBlurSimple and MotionBlurReal:
	/// 1. MotionBlurSimple is much less expensive to render than MotionBlurReal.
	/// 2. MotionBlurReal produces a much more accurate motion blur than MotionBlurSimple.
	/// 3. MotionBlurReal handles transparency much better than MotionBlurSimple.
	/// 4. MotionBlurReal can become very slow when the motion traveled in a single frame is very large on screen.
	/// 5. MotionBlurReal renders with 1 frame of latency, MotionBlurSimple renders immediately with no latency.
	///
	/// Since this is just an approximation, care must be taken to get the best results.  Some notes:
	/// 1. BlendStrength needs to be set based on the brightness of the object being rendered and the color of the background. 
	/// 2. The above is more important when rendering transparent UI objects, requiring a lower value for BlendStrength
	/// 3. When using the built-in Shadow component, it will cause problems due to dark transparent layer under opaque layer, which causes
	///    flickering.  Therefore when using Shadow component it's better to put it after this component.
	/// </remark>
	//[ExecuteAlways]
	[RequireComponent(typeof(Graphic))]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Motion Blur (Simple)")]
	public class MotionBlurSimple : UIBehaviour, IMeshModifier
	{
		[Tooltip("Which vertex modifiers are used to calculate the motion blur.")]
		[SerializeField] VertexModifierSource _mode = VertexModifierSource.Transform;

		[Tooltip("The number of motion blur steps to calculate.  The higher the number the more expensive the effect.  Set to 1 means the effect is not applied.")]
		[SerializeField, Range(1f, 64f)] int _sampleCount = 16;

		[Tooltip("How transparent the motion blur is.")]
		[SerializeField, Range(1f, 6f)] float _blendStrength = 2.5f;

		[Tooltip("Interpolate texture coordinates. Disable this if you are changing the characters of text.")]
		[SerializeField] bool _lerpUV = false;

		[Tooltip("Allows frame-rate independent blur length.  This is unrealistic but may be more artistically pleasing as the visual appearance of the motion blur remains consistent across frame rates.")]
		[SerializeField] bool _frameRateIndependent = true;

		[Tooltip("The strength of the effect. Zero means the effect is not applied.  Greater than one means the effect is exagerated.")]
		[SerializeField, Range(0f, 4f)] float _strength = 1f;

		/// <summary>Property <c>UpdateMode</c> sets which vertex modifiers are used to calculate the motion blur</summary>
		public VertexModifierSource UpdateMode { get { return _mode; } set { _mode = value; ForceMeshModify(); } }

		/// <summary>Property <c>SampleCount</c> sets the number of motion blur steps to calculate.  The higher the number the more expensive the effect.</summary>
		public int SampleCount { get { return _sampleCount; } set { _sampleCount = value; ForceMeshModify(); } }

		/// <summary>Property <c>BlendStrength</c> controls how transparent the motion blur is.</summary>
		public float BlendStrength { get { return _blendStrength; } set { _blendStrength = value; ForceMeshModify(); } }

		/// <summary>Interpolate texture coordinates. Disable this if you are changing the characters of text.</summary>
		public bool LerpUV { get { return _lerpUV; } set { _lerpUV = value; } }

		/// <summary>Property <c>FrameRateIndependent</c> allows frame-rate independent blur length.  This is unrealistic but may be more artistically pleasing as the visual appearance of the motion blur remains consistent across frame rates.</summary>
		public bool FrameRateIndependent { get { return _frameRateIndependent; } set { _frameRateIndependent = value; } }

		/// <summary>Property <c>Strength</c> controls how large the motion blur effect is.</summary>
		/// <value>Set to 1.0 by default.  Zero means the effect is not applied.  Greater than one means the effect is exagerated.</value>
		public float Strength { get { return _strength; } set { _strength = value; ForceMeshModify(); } }

		private Graphic _graphic;
		private bool _isPrimed;
		private int _activeVertexCount;
		private List<UIVertex> _currVertices;
		private List<UIVertex> _vertices;
		private UIVertex[] _prevVertices;
		private Matrix4x4 _prevLocalToWorld;
		private Matrix4x4 _prevWorldToCamera;
		private Camera _trackingCamera;
		private bool _blurredLastFrame;

		private Graphic GraphicComponent { get { if (_graphic == null) { _graphic = GetComponent<Graphic>(); } return _graphic; } }

		private MaskableGraphic _maskableGraphic;
		private MaskableGraphic MaskableGraphicComponent { get { if (_maskableGraphic == null) { _maskableGraphic = GraphicComponent as MaskableGraphic; } return _maskableGraphic; } }

		private CanvasRenderer _canvasRenderer;
		private CanvasRenderer CanvasRenderComponent { get { if (_canvasRenderer == null) { if (GraphicComponent) { _canvasRenderer = _graphic.canvasRenderer; } else { _canvasRenderer = GetComponent<CanvasRenderer>(); } } return _canvasRenderer; } }

		/// <summary>Global debugging option to tint the colour of the motion blur mesh to magenta.  Can be used to tell when the effect is being applied</summary>
		public static bool GlobalDebugTint = false;

		/// <summary>Global option to freeze updating of the mesh, useful for seeing the motion blur</summary>
		public static bool GlobalDebugFreeze = false;

		/// <summary>Global option to disable this effect from being applied</summary>
		public static bool GlobalDisabled = false;

		// NOTE: Pre-allocate function delegates to prevent garbage
		private UnityEngine.Events.UnityAction _cachedOnDirtyVertices;

		[UnityInternal.ExcludeFromDocs]
		protected override void OnEnable()
		{
			_isPrimed = false;
			if (_cachedOnDirtyVertices == null) _cachedOnDirtyVertices = new UnityEngine.Events.UnityAction(OnDirtyVertices);
			GraphicComponent.RegisterDirtyVerticesCallback(_cachedOnDirtyVertices);
			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.AddListener(OnCullingChanged);
			}
			ForceMeshModify();
			base.OnEnable();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnDisable()
		{
			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.RemoveListener(OnCullingChanged);
			}
			GraphicComponent.UnregisterDirtyVerticesCallback(_cachedOnDirtyVertices);
			ForceMeshModify();
			base.OnDisable();
		}

		private void OnCullingChanged(bool culled)
		{
			// If a Rect2DMask is used, culling occurs on the MaskableGraphic which will cause culling
			// which causes the trail logic to stop running, so we have to force disable culling if it is applied.
			// TODO: Find a more elegant solution for this
			if (culled)
			{
				CanvasRenderComponent.cull = false;
			}
		}

		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			ForceMeshModify();
			base.OnValidate();
		}
		#endif

		protected override void OnDidApplyAnimationProperties()
		{
			ForceMeshModify();
			base.OnDidApplyAnimationProperties();
		}

		/// <summary>
		/// OnCanvasHierarchyChanged() is called when the Canvas is enabled/disabled
		/// </summary>
		protected override void OnCanvasHierarchyChanged()
		{
			ResetMotion();
			ForceMeshModify();
			base.OnCanvasHierarchyChanged();
		}

		private void ForceMeshModify()
		{
			GraphicComponent.SetVerticesDirty();
		}

		private enum DirtySource : byte
		{
			None = 0,
			Transform = 0x01,
			Vertices = 0x02,
			SelfForced = 0x04,
		}

		private DirtySource _dirtySource = DirtySource.None;

		private bool IsDirtyTransform { get { return (_dirtySource & DirtySource.Transform) != 0; } set { _dirtySource |= DirtySource.Transform; } }
		private bool IsDirtyVertices { get { return (_dirtySource & DirtySource.Vertices) != 0; } set { _dirtySource |= DirtySource.Vertices; } }
		private bool IsDirtySelfForced { get { return (_dirtySource & DirtySource.SelfForced) != 0; } set { _dirtySource |= DirtySource.SelfForced; } }

		void OnDirtyVertices()
		{
			if (!IsDirtyTransform && !IsDirtySelfForced)
			{
				IsDirtyVertices = true;
			}
		}

		void LateUpdate()
		{
			// If blur was applied to the last frame, then force a new frame to render in case there has been no motion
			// in which case it needs to be rendered without any motion blur.
			if (_blurredLastFrame)
			{
				IsDirtySelfForced = true;
				ForceMeshModify();
			}

			// Detect changes to the transform or the camera
			if ((IsTrackingTransform() && _prevLocalToWorld != this.transform.localToWorldMatrix) ||
				(_trackingCamera != null && _prevWorldToCamera != _trackingCamera.transform.worldToLocalMatrix))
			{
				IsDirtyTransform = true;
				ForceMeshModify();
			}
			
			if (GlobalDebugFreeze)
			{
			//	IsDirtyTransform = true;
			//	ForceMeshModify();
			}
		}

		/// <summary>
		/// Reset the motion blur to begin again at the current state (transform/vertex positions).
		/// This is useful when reseting the transform to prevent motion blur drawing erroneously between
		/// the last position and the new position.
		/// </summary>
		public void ResetMotion()
		{
			_isPrimed = false;
		}

		private bool PrepareBuffers(VertexHelper vh)
		{
			bool isPrepared = _isPrimed;
			if (_currVertices == null || vh.currentIndexCount > _currVertices.Count)
			{
				// If the number of vertices has changed, we need to prime
				// NOTE: We compare with the number of indices, as this is really how many vertices will be returned, as GetUIVertexStream() returns triangle indices
				_currVertices = new List<UIVertex>(vh.currentIndexCount);
				_prevVertices = new UIVertex[vh.currentIndexCount];
				_activeVertexCount = vh.currentIndexCount;
				isPrepared = false;
			}
			else if (vh.currentIndexCount < _activeVertexCount)
			{
				// If the number of vertices has decreased, then don't reallocate the list, just use a portion of it
				_activeVertexCount = vh.currentIndexCount;
				isPrepared = false;
			}

			int motionBlurVertexCount = _activeVertexCount * _sampleCount;
			if (_vertices == null || motionBlurVertexCount > _vertices.Capacity)
			{
				_vertices = new List<UIVertex>(motionBlurVertexCount);
				isPrepared = false;
			}

			vh.GetUIVertexStream(_currVertices);

			return isPrepared;
		}

		private bool IsTrackingTransform()
		{
			return (_mode != VertexModifierSource.Vertex);
		}

		private bool IsTrackingVertices()
		{
			return (_mode != VertexModifierSource.Transform);
		}

		private void UpdateCanvasCamera()
		{
			_trackingCamera = null;
			// If we're rendering to world-space, then the camera can move relative to the UI, so we must also track the camera movement.
			if (GraphicComponent.canvas.renderMode == RenderMode.WorldSpace) 
			{
				_trackingCamera = GraphicComponent.canvas.worldCamera;
				if (_trackingCamera == null)
				{
					_trackingCamera = Camera.main;
					if (_trackingCamera == null)
					{
						Debug.LogWarning("[MotionBlurSimpleTMP] No world camera specified on Canvas.");
					}
				}
			}
		}

		private void CacheState()
		{
			UpdateCanvasCamera();

			if (IsTrackingTransform())
			{
				_prevLocalToWorld = this.transform.localToWorldMatrix;
			}
			if (IsTrackingVertices())
			{
				_currVertices.CopyTo(0, _prevVertices, 0, _activeVertexCount);
			}
			if (_trackingCamera != null)
			{
				_prevWorldToCamera = _trackingCamera.transform.worldToLocalMatrix;
			}

			_isPrimed = true;
		}

		private float GetLerpFactorUnclamped(float t)
		{
			float trailLengthScale = _strength;

			// Frame-rate independent motion blur length
			if (_frameRateIndependent)
			{
				// Sometimes deltaTime can become zero (eg when Time.timeScale is zero), so we must account for this, otherwise we'll get divide by zero and other issues will result
				float deltaTime = Time.deltaTime;
				if (deltaTime <= 0f)
				{
					deltaTime = 1f / 60f;
				}
				float targetFps = 60f;
				float currentFps = 1f / deltaTime;
				float fpsRatio = (currentFps / targetFps);
				trailLengthScale *= fpsRatio;
			}

			// t usually goes from 0.0 (head and current time) to 1.0 (tail and previous time)
			// we can adjust these points to stretch the motion trail
			if (trailLengthScale != 1.0f)
			{
				float trailLength = 1f * trailLengthScale;
				float headT = 1f;
				float tailT = headT - trailLength;
				t = Mathf.Lerp(tailT, headT, t);
			}

			return t;
		}

		private List<UIVertex> CreateMotionBlurMesh(VertexHelper vh)
		{
			float alpha = Mathf.Clamp01(_blendStrength / (float)_sampleCount);
			Color alphaColor = new Color(1f, 1f, 1f, alpha);

			float stepSize = 1f / (_sampleCount - 1f);
			float t = 0.0f;

			Matrix4x4 prevLocalToWorld = _prevLocalToWorld;
			Matrix4x4 localToWorld = this.transform.localToWorldMatrix;
			Matrix4x4 worldToLocal = this.transform.worldToLocalMatrix;
			
			// If we're tracking camera motion, then modify the matrix to convert to camera-space instead of world-space.
			if (_trackingCamera != null)
			{
				if (IsTrackingTransform())
				{
					prevLocalToWorld = _prevWorldToCamera * prevLocalToWorld;
				}
				else
				{
					// In Vertex mode we only use the current localToWorld transform, not the previous one
					prevLocalToWorld = _prevWorldToCamera * localToWorld;
				}

				Matrix4x4 worldToCamera = _trackingCamera.transform.worldToLocalMatrix;
				localToWorld = worldToCamera * localToWorld;

				Matrix4x4 cameraToWorld = _trackingCamera.transform.localToWorldMatrix;
				worldToLocal = worldToLocal * cameraToWorld;
			}

			_vertices.Clear();

			if (_mode == VertexModifierSource.Transform)
			{
				// Lerp in-between samples
				for (int j = 0; j < _sampleCount - 1; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					for (int i = 0; i < _activeVertexCount; i++)
					{
						UIVertex vv = _currVertices[i];

						// Xform the previous position to world space
						Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(vv.position);
						// Xform the current position to world space
						Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position);
						// Lerp between the world space positions and xform back to current local space
						vv.position = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));
						vv.color.a = (byte)Mathf.RoundToInt(((float)vv.color.a * alpha));

						_vertices.Add(vv);
					}
					t += stepSize;
				}
			}
			else if (_mode == VertexModifierSource.Vertex)
			{
				if (_trackingCamera == null)
				{
					// Lerp in-between samples
					for (int j = 0; j < _sampleCount - 1; j++)
					{
						float tScaled = GetLerpFactorUnclamped(t);
						float tScaledClamped = Mathf.Clamp01(tScaled);
						if (_lerpUV)
						{
							for (int i = 0; i < _activeVertexCount; i++)
							{
								UIVertex vv = _currVertices[i];

								vv.position = Vector3.LerpUnclamped(_prevVertices[i].position, vv.position, tScaled);
								vv.uv0 = Vector4.LerpUnclamped(_prevVertices[i].uv0, vv.uv0, tScaledClamped);
								vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;
								
								_vertices.Add(vv);
							}
						}
						else
						{
							for (int i = 0; i < _activeVertexCount; i++)
							{
								UIVertex vv = _currVertices[i];

								vv.position = Vector3.LerpUnclamped(_prevVertices[i].position, vv.position, tScaled);
								vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;
								
								_vertices.Add(vv);
							}
						}
						t += stepSize;
					}
				}
				else
				{
					// Lerp in-between samples
					for (int j = 0; j < _sampleCount - 1; j++)
					{
						float tScaled = GetLerpFactorUnclamped(t);
						float tScaledClamped = Mathf.Clamp01(tScaled);
						if (_lerpUV)
						{
							for (int i = 0; i < _activeVertexCount; i++)
							{
								UIVertex vv = _currVertices[i];

								// Xform the previous position to world space
								Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices[i].position);
								// Xform the current position to world space
								Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position);
								// Lerp between the world space positions and xform back to current local space
								vv.position = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));
								vv.uv0 = Vector4.LerpUnclamped(_prevVertices[i].uv0, vv.uv0, tScaledClamped);
								vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;
								
								_vertices.Add(vv);
							}
						}
						else
						{
							for (int i = 0; i < _activeVertexCount; i++)
							{
								UIVertex vv = _currVertices[i];

								// Xform the previous position to world space
								Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices[i].position);
								// Xform the current position to world space
								Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position);
								// Lerp between the world space positions and xform back to current local space
								vv.position = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));
								vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;
								
								_vertices.Add(vv);
							}
						}
						t += stepSize;
					}
				}
			}
			else if (_mode == VertexModifierSource.TranformAndVertex)
			{
				// Lerp in-between samples
				for (int j = 0; j < _sampleCount - 1; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					float tScaledClamped = Mathf.Clamp01(tScaled);
					if (_lerpUV)
					{				
						for (int i = 0; i < _activeVertexCount; i++)
						{
							UIVertex vv = _currVertices[i];

							// Xform the previous position to world space
							Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices[i].position);
							// Xform the current position to world space
							Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position);
							// Lerp between the world space positions and xform back to current local space
							vv.position = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));

							vv.uv0 = Vector4.LerpUnclamped(_prevVertices[i].uv0, vv.uv0, tScaledClamped);
							vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;

							_vertices.Add(vv);
						}
					}
					else
					{
						for (int i = 0; i < _activeVertexCount; i++)
						{
							UIVertex vv = _currVertices[i];

							// Xform the previous position to world space
							Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices[i].position);
							// Xform the current position to world space
							Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position);
							// Lerp between the world space positions and xform back to current local space
							vv.position = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));

							vv.color = Color.LerpUnclamped(_prevVertices[i].color, vv.color, tScaledClamped) * alphaColor;

							_vertices.Add(vv);
						}
					}
					t += stepSize;
				}
			}

			// The last sample is the current vertices
			for (int i = 0; i < _activeVertexCount; i++)
			{
				UIVertex vv = _currVertices[i];
				vv.color.a = (byte)Mathf.RoundToInt(((float)vv.color.a * alpha));
				_vertices.Add(vv);
			}

			if (GlobalDebugTint)
			{
				for (int i = 0; i < _vertices.Count; i++)
				{
					UIVertex v = _vertices[i];
					v.color = Color.magenta * alphaColor;
					_vertices[i] = v;
				}
			}

			return _vertices;
		}

		private bool CanApply()
		{
			if (!IsActive()) return false;
			if (_sampleCount <= 1) return false;
			if (_strength <= 0f) return false;
			if (GlobalDisabled) return false;

			// NOTE: GraphicsComponent.canvas can become null when deleting the GameObject
			if (GraphicComponent != null && GraphicComponent.canvas == null)
			{
				return false;
			}

			return true;
		}

		[UnityInternal.ExcludeFromDocs]
		public void ModifyMesh(VertexHelper vh)
		{
			// In freeze mode simply return the previous mesh
			if (GlobalDebugFreeze && _blurredLastFrame && _vertices != null)
			{
				vh.AddUIVertexTriangleStream(_vertices);
				return;
			}

			_blurredLastFrame = false;

			if (CanApply())
			{
				bool isForcedLastFrame = (IsDirtySelfForced && !IsDirtyVertices && !IsDirtyTransform);
				_dirtySource = DirtySource.None;

				if (!isForcedLastFrame)
				{
					if (PrepareBuffers(vh))
					{
						// NOTE: despite its name, VertexHelper.AddUIVertexTriangleStream() actually replaces the vertices, it doesn't add to them
						vh.AddUIVertexTriangleStream(CreateMotionBlurMesh((vh)));

						_blurredLastFrame = true;
					}
					else
					{
						// This is the first frame of this component, so we can't generate any motion blur on this frame, 
						// so just collect the current state, ready to render motion blur on the next frame.
					}
					CacheState();
				}
				else
				{
					// TODO: when the motion blur is not being applied, we need to fade it out so there is no jarring switch
					PrepareBuffers(vh);
					CacheState();
				}
			}
			else
			{
				_isPrimed = false;
			}
		}

		[UnityInternal.ExcludeFromDocs]
		[System.Obsolete("use IMeshModifier.ModifyMesh (VertexHelper verts) instead", false)]
		public void ModifyMesh(Mesh mesh)
		{
			throw new System.NotImplementedException("use IMeshModifier.ModifyMesh (VertexHelper verts) instead");
		}
	}
}