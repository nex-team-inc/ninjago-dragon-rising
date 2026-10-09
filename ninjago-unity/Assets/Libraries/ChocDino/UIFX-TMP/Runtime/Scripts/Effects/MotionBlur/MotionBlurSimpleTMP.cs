//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//
#if UIFX_TMPRO
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;
using TMPro;

namespace ChocDino.UIFX
{
	// TODO: make the blur factor (transparency in our case) relative to the amount of motion in SCREEN space..(1 / (1 + d))

	/// <summary>
	/// The MotionBlurSimpleTMP component is a visual effect that can be applied to a TextMeshPro Text component
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
	[RequireComponent(typeof(TMP_Text))]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Motion Blur (Simple) TMP")]
	public class MotionBlurSimpleTMP : UIBehaviour
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

		[SerializeField] TargetMesh _targetMesh = TargetMesh.TextMeshPro;

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

		class VertexData
		{
			public Vector3[] positions;
			public Vector2[] uvs0;
			public Vector4[] uvs0_v4;
			public Vector2[] uvs1;
			public Color32[] colors;

			public VertexData(int vertexCount)
			{
				positions = new Vector3[vertexCount];
				if (MotionBlurSimpleTMP.s_isUV0Vector4) { uvs0_v4 = new Vector4[vertexCount]; } else { uvs0 = new Vector2[vertexCount];	}
				uvs1 = new Vector2[vertexCount];
				colors = new Color32[vertexCount];
			}

			private VertexData() {}

			public int VertexCount { get { return positions.Length; } }

			public void CopyTo(VertexData dst)
			{
				Debug.Assert(positions.Length == dst.positions.Length);
				positions.CopyTo(dst.positions, 0);
				if (uvs0 != null) { uvs0.CopyTo(dst.uvs0, 0); }
				if (uvs0_v4 != null) { uvs0_v4.CopyTo(dst.uvs0_v4, 0); }
				uvs1.CopyTo(dst.uvs1, 0);
				colors.CopyTo(dst.colors, 0);
			}

			public void CopyTo(VertexData dst, int dstOffset, int count)
			{
				Debug.Assert(positions.Length >= count);
				Debug.Assert(dst.positions.Length >= (dstOffset + count));
				System.Array.Copy(positions, 0, dst.positions, dstOffset, count);
				if (uvs0 != null) { System.Array.Copy(uvs0, 0, dst.uvs0, dstOffset, count); }
				if (uvs0_v4 != null) { System.Array.Copy(uvs0_v4, 0, dst.uvs0_v4, dstOffset, count); }
				System.Array.Copy(uvs1, 0, dst.uvs1, dstOffset, count);
				System.Array.Copy(colors, 0, dst.colors, dstOffset, count);
			}
		}

		private Graphic _graphic;
		private bool _isPrimed;
		private int _activeVertexCount;
		private VertexData _currVertices;
		private VertexData _prevVertices;
		private VertexData _vertices;
		private Matrix4x4 _prevLocalToWorld;
		private Matrix4x4 _prevWorldToCamera;
		private Camera _trackingCamera;
		private bool _blurredLastFrame;

		private Graphic GraphicComponent { get { if (_graphic == null) _graphic = GetComponent<Graphic>(); return _graphic; } }

		// NOTE: Pre-allocate function delegates to prevent garbage
		private UnityEngine.Events.UnityAction _cachedOnDirtyVertices;

		private TMP_Text _textMeshPro;
		private static bool s_isUV0Vector4;

		// NOTE: Usually it's fine to just modify the TMP mesh, but when there is ANOTHER script that's modifying the TMP mesh then there will be a conflict because this script increases the number of vertices.
		// In that case the CanvasRenderer can be set to render our own mesh.
		public enum TargetMesh
		{
			// Modifies the TMP mesh
			TextMeshPro,

			// Doesn't modify the TMP mesh, instead assigns a new mesh to the CanvasRenderer
			Internal,
		}

		private bool _canCanvasRender;
		private int[] _triangleIndices;
		private Mesh _mesh;

		static MotionBlurSimpleTMP()
		{
			// Note: Most "preview" TMP versions have changed MeshInfo.UVS0 from Vector2 to Vector4
			s_isUV0Vector4 = typeof(TMP_MeshInfo).GetField("uvs0").FieldType == typeof(Vector4[]);
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void Awake()
		{
			_textMeshPro = GetComponent<TMP_Text>();
			Debug.Assert(_textMeshPro != null);
			base.Awake();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnEnable()
		{
			if (_textMeshPro != null)  { _textMeshPro = GetComponent<TMP_Text>(); Debug.Assert(_textMeshPro != null); }

			if (_targetMesh == TargetMesh.Internal)
			{
				Canvas.willRenderCanvases += WillRenderCanvases;
			}
			else
			{
				_textMeshPro.renderMode = TextRenderFlags.DontRender;
			}

			_isPrimed = false;
			if (_cachedOnDirtyVertices == null) _cachedOnDirtyVertices = new UnityEngine.Events.UnityAction(OnDirtyVertices);
			GraphicComponent.RegisterDirtyVerticesCallback(_cachedOnDirtyVertices);
			ForceMeshModify();
			base.OnEnable();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnDisable()
		{	
			Canvas.willRenderCanvases -= WillRenderCanvases;

			ObjectHelper.Destroy(ref _mesh);

			if (_textMeshPro) _textMeshPro.renderMode = TextRenderFlags.Render;
			GraphicComponent.UnregisterDirtyVerticesCallback(_cachedOnDirtyVertices);
			ForceMeshModify();

			// Forces the mesh to regenerate to the orignal (pre-motion blur) state
			if (_textMeshPro)
			{
				ForceMeshBackToOriginal();
				_textMeshPro.ForceMeshUpdate(false, false);
			}
		
			base.OnDisable();
		}

		private void ForceMeshBackToOriginal()
		{
			// This sets the mesh back to the original state (without motion blur)
			// NOTE: We have to do this since we're modifying the size of the mesh, which TMP doesn't expect...Otherwise we get out of bounds errors for the triangle/vertex arrays.
			if (_targetMesh == TargetMesh.TextMeshPro)
			{
				for (int i = 0; i < _textMeshPro.textInfo.materialCount; i++)
				{
					if (_textMeshPro.textInfo.meshInfo[i].vertexCount > 0)
					{
						_textMeshPro.textInfo.meshInfo[i].ResizeMeshInfo(_textMeshPro.textInfo.meshInfo[i].vertexCount / 4, false);
					}
				}
			}
		}

		/*
		#if UNITY_EDITOR
		protected override void OnValidate()
		{
			Debug.Log("validate");
			ForceMeshModify();
			ForceMeshBackToOriginal();
			base.OnValidate();
		}
		#endif
		*/

		private void ForceMeshModify()
		{
			//GraphicComponent.SetVerticesDirty();
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

		void ModifyGeometry(TMP_TextInfo textInfo)
		{
			if (_textMeshPro == null || !_textMeshPro.IsActive()) return;

			VertexData outputVerts = ModifyMesh(textInfo);

			if (_targetMesh == TargetMesh.TextMeshPro)
			{
				if (outputVerts != null)
				{
					if (textInfo.materialCount == 1)
					{
						TMP_MeshInfo meshInfo = textInfo.meshInfo[0];
						int newQuadCount = (_activeVertexCount * _sampleCount) / 4;

						// Resize geometry arrays and mesh
						// TODO: this can be optimised so we don't need to resize (generages garbage)
						// We just need to store own own Mesh and set the CanvasRenderer to use this
						// The tricky part will be the submeshes as there is no direct way to access them,
						// we would have to search and store them which isn't fast - or perhaps there is a
						// submesh create/destroy event we could hook into?
						meshInfo.ResizeMeshInfo(newQuadCount);

						// Assign to the mesh
						meshInfo.mesh.triangles = meshInfo.triangles;
						meshInfo.mesh.vertices = outputVerts.positions;
						meshInfo.mesh.SetColors(outputVerts.colors);
						if (s_isUV0Vector4) { meshInfo.mesh.SetUVs(0, outputVerts.uvs0_v4); }
						else { meshInfo.mesh.SetUVs(0, outputVerts.uvs0); }
						meshInfo.mesh.SetUVs(1, outputVerts.uvs1);

						_textMeshPro.UpdateGeometry(meshInfo.mesh, 0);
					}
					else if (textInfo.materialCount > 1)
					{
						// TODO: add support for multi-material text
					}
				}
			}
			else
			{
				_canCanvasRender = (outputVerts != null);
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
			
			if (MotionBlurSimple.GlobalDebugFreeze)
			{
			//	IsDirtyTransform = true;
			//	ForceMeshModify();
			}

			if (_textMeshPro)
			{
				if (CanApply())
				{
					IsDirtyTransform = true;
					//IsDirtyVertices = true;

					// Detected changes to vertex count or effective sample count
					if (_textMeshPro.havePropertiesChanged)
					{
						ForceMeshBackToOriginal();
					}
					else if (_vertices != null)
					{
						int textVertexCount = 0;
						for (int i = 0; i < _textMeshPro.textInfo.materialCount; i++)
						{
							textVertexCount += _textMeshPro.textInfo.meshInfo[i].vertexCount;
						}
						int outputVertexCount = textVertexCount * _sampleCount;
						if (outputVertexCount != _vertices.VertexCount)
						{
							ForceMeshBackToOriginal();
						}
					}

					if (_targetMesh == TargetMesh.TextMeshPro)
					{
						// Force the text mesh to be regenerated
						_textMeshPro.renderMode = TextRenderFlags.DontRender;
						_textMeshPro.ForceMeshUpdate(false, false);
					}

					// NOTE: We call this from LateUpdate() instead of from the OnPreRenderText action as otherwise
					// adjusting the number of triangles causes an error to be thrown, I think because TMP
					// doesn't call SetTriangles() on its main mesh before calling SetVertices().
					ModifyGeometry(_textMeshPro.textInfo);
				}
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

		private bool PrepareBuffers(TMP_TextInfo textInfo)
		{
			int totalVertexCount = 0;
			{
				for (int i = 0; i < textInfo.materialCount; i++)
				{
					TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
					totalVertexCount += meshInfo.vertexCount;
				}
			}
			
			bool isPrepared = _isPrimed;
			if (_currVertices == null || totalVertexCount != _currVertices.VertexCount)
			{
				// If the number of vertices has changed, we need to prime
				_currVertices = new VertexData(totalVertexCount);
				_prevVertices = new VertexData(totalVertexCount);
				_activeVertexCount = totalVertexCount;
				isPrepared = false;
			}
			// TODO: add this optimisation back in.. but currently it breaks things
			/*else if (totalVertexCount < _activeVertexCount)
			{
				// If the number of vertices has decreased, then don't reallocate the list, just use a portion of it
				_activeVertexCount = totalVertexCount;
				isPrepared = false;
			}	*/

			int motionBlurVertexCount = _activeVertexCount * _sampleCount;
			if (_vertices == null || motionBlurVertexCount != _vertices.VertexCount)
			{
				_vertices = new VertexData(motionBlurVertexCount);
				isPrepared = false;
			}

			int vertexOffset = 0;
			for (int i = 0; i < textInfo.materialCount; i++)
			{
				TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
				int vertexCount = meshInfo.vertexCount;
				// NOTE: that meshInfo.vertices (etc) can be larger than meshInfo.vertexCount because TMP over allocates (power-of-2 sizes) to prevent frequent reallocation.
				if (vertexCount > 0)
				{
					System.Array.Copy(meshInfo.vertices, 0, _currVertices.positions, vertexOffset, vertexCount);
					if (s_isUV0Vector4) { System.Array.Copy(meshInfo.uvs0, 0, _currVertices.uvs0_v4, vertexOffset, vertexCount); }
					else { System.Array.Copy(meshInfo.uvs0, 0, _currVertices.uvs0, vertexOffset, vertexCount); }
					System.Array.Copy(meshInfo.uvs2, 0, _currVertices.uvs1, vertexOffset, vertexCount);
					System.Array.Copy(meshInfo.colors32, 0, _currVertices.colors, vertexOffset, vertexCount);
					vertexOffset += vertexCount;
				}
			}

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
						Debug.LogWarning("[MotionBlurSimple] No world camera specified on Canvas.");
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
				_currVertices.CopyTo(_prevVertices, 0, _activeVertexCount);
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

		private VertexData CreateMotionBlurMesh()
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

			int vertIdx = 0;

			// Generate the in-inbetween samples
			if (_mode == VertexModifierSource.Transform)
			{
				for (int j = 0; j < _sampleCount - 1; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					for (int i = 0; i < _activeVertexCount; i++)
					{
						// Xform the previous position to world space
						Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_currVertices.positions[i]);
						// Xform the current position to world space
						Vector3 v1 = localToWorld.MultiplyPoint3x4(_currVertices.positions[i]);
						// Lerp between the world space positions and xform back to current local space
						_vertices.positions[vertIdx] = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));

						// Copy UVs
						_vertices.uvs1[vertIdx] = _currVertices.uvs1[i];

						// Copy and fade color
						_vertices.colors[vertIdx] = _currVertices.colors[i];
						_vertices.colors[vertIdx].a = (byte)Mathf.RoundToInt(((float)_currVertices.colors[i].a * alpha));

						vertIdx++;
					}
					// Copy UV0
					if (s_isUV0Vector4)
					{
						vertIdx -= _activeVertexCount;
						for (int i = 0; i < _activeVertexCount; i++)
						{
							_vertices.uvs0_v4[vertIdx] = _currVertices.uvs0_v4[i];
							vertIdx++;
						}
					}
					else
					{
						vertIdx -= _activeVertexCount;
						for (int i = 0; i < _activeVertexCount; i++)
						{
							_vertices.uvs0[vertIdx] = _currVertices.uvs0[i];
							vertIdx++;
						}
					}
					t += stepSize;
				}
			}
			else if (_mode == VertexModifierSource.Vertex)
			{
				if (_trackingCamera == null)
				{
					for (int j = 0; j < _sampleCount - 1; j++)
					{
						float tScaled = GetLerpFactorUnclamped(t);
						float tScaledClamped = Mathf.Clamp01(tScaled);
						for (int i = 0; i < _activeVertexCount; i++)
						{
							_vertices.positions[vertIdx] = Vector3.LerpUnclamped(_prevVertices.positions[i], _currVertices.positions[i], tScaled);
							_vertices.uvs1[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs1[i], _currVertices.uvs1[i], tScaledClamped);
							_vertices.colors[vertIdx] = Color.LerpUnclamped(_prevVertices.colors[i], _currVertices.colors[i], tScaledClamped) * alphaColor;

							vertIdx++;
						}
						if (_lerpUV)
						{
							if (s_isUV0Vector4)
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0_v4[vertIdx] = Vector4.LerpUnclamped(_prevVertices.uvs0_v4[i], _currVertices.uvs0_v4[i], tScaledClamped);
									vertIdx++;
								}
							}
							else
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs0[i], _currVertices.uvs0[i], tScaledClamped);
									vertIdx++;
								}
							}
						}
						else
						{
							if (s_isUV0Vector4)
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0_v4[vertIdx] = _currVertices.uvs0_v4[i];
									vertIdx++;
								}
							}
							else
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0[vertIdx] = _currVertices.uvs0[i];
									vertIdx++;
								}
							}
						}
						t += stepSize;
					}
				}
				else
				{
					for (int j = 0; j < _sampleCount - 1; j++)
					{
						float tScaled = GetLerpFactorUnclamped(t);
						float tScaledClamped = Mathf.Clamp01(tScaled);
						for (int i = 0; i < _activeVertexCount; i++)
						{
							// Xform the previous position to world space
							Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices.positions[i]);
							// Xform the current position to world space
							Vector3 v1 = localToWorld.MultiplyPoint3x4(_currVertices.positions[i]);
							// Lerp between the world space positions and xform back to current local space
							_vertices.positions[vertIdx] = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));

							_vertices.uvs1[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs1[i], _currVertices.uvs1[i], tScaledClamped);
							_vertices.colors[vertIdx] = Color.LerpUnclamped(_prevVertices.colors[i], _currVertices.colors[i], tScaledClamped) * alphaColor;

							vertIdx++;
						}
						if (_lerpUV)
						{
							if (s_isUV0Vector4)
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0_v4[vertIdx] = Vector4.LerpUnclamped(_prevVertices.uvs0_v4[i], _currVertices.uvs0_v4[i], tScaledClamped);
									vertIdx++;
								}
							}
							else
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs0[i], _currVertices.uvs0[i], tScaledClamped);
									vertIdx++;
								}
							}
						}
						else
						{
							if (s_isUV0Vector4)
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0_v4[vertIdx] = _currVertices.uvs0_v4[i];
									vertIdx++;
								}
							}
							else
							{
								vertIdx -= _activeVertexCount;
								for (int i = 0; i < _activeVertexCount; i++)
								{
									_vertices.uvs0[vertIdx] = _currVertices.uvs0[i];
									vertIdx++;
								}
							}
						}						
						t += stepSize;
					}
				}
			}
			else if (_mode == VertexModifierSource.TranformAndVertex)
			{		
				for (int j = 0; j < _sampleCount - 1; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					float tScaledClamped = Mathf.Clamp01(tScaled);
					for (int i = 0; i < _activeVertexCount; i++)
					{
						// Xform the previous position to world space
						Vector3 v0 = prevLocalToWorld.MultiplyPoint3x4(_prevVertices.positions[i]);
						// Xform the current position to world space
						Vector3 v1 = localToWorld.MultiplyPoint3x4(_currVertices.positions[i]);
						// Lerp between the world space positions and xform back to current local space
						_vertices.positions[vertIdx] = worldToLocal.MultiplyPoint3x4(Vector3.LerpUnclamped(v0, v1, tScaled));

						_vertices.uvs1[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs1[i], _currVertices.uvs1[i], tScaledClamped);
						_vertices.colors[vertIdx] = Color.LerpUnclamped(_prevVertices.colors[i], _currVertices.colors[i], tScaledClamped) * alphaColor;

						vertIdx++;
					}
					if (_lerpUV)
					{
						if (s_isUV0Vector4)
						{
							vertIdx -= _activeVertexCount;
							for (int i = 0; i < _activeVertexCount; i++)
							{
								_vertices.uvs0_v4[vertIdx] = Vector4.LerpUnclamped(_prevVertices.uvs0_v4[i], _currVertices.uvs0_v4[i], tScaledClamped);
								vertIdx++;
							}
						}
						else
						{
							vertIdx -= _activeVertexCount;
							for (int i = 0; i < _activeVertexCount; i++)
							{
								_vertices.uvs0[vertIdx] = Vector2.LerpUnclamped(_prevVertices.uvs0[i], _currVertices.uvs0[i], tScaledClamped);
								vertIdx++;
							}
						}
					}
					else
					{
						if (s_isUV0Vector4)
						{
							vertIdx -= _activeVertexCount;
							for (int i = 0; i < _activeVertexCount; i++)
							{
								_vertices.uvs0_v4[vertIdx] = _currVertices.uvs0_v4[i];
								vertIdx++;
							}
						}
						else
						{
							vertIdx -= _activeVertexCount;
							for (int i = 0; i < _activeVertexCount; i++)
							{
								_vertices.uvs0[vertIdx] = _currVertices.uvs0[i];
								vertIdx++;
							}
						}
					}
					t += stepSize;
				}
			}

			// The last sample is the current vertices
			int lastSampleOffset = _activeVertexCount * (_sampleCount - 1);
			_currVertices.CopyTo(_vertices, lastSampleOffset, _activeVertexCount);
			for (int i = 0; i < _activeVertexCount; i++)
			{
				_vertices.colors[lastSampleOffset].a = (byte)Mathf.RoundToInt(((float)_vertices.colors[lastSampleOffset].a * alpha));
				lastSampleOffset++;
			}

			if (MotionBlurSimple.GlobalDebugTint)
			{
				int vertexCount = _vertices.VertexCount;
				for (int i = 0; i < vertexCount; i++)
				{
					_vertices.colors[i] = Color.magenta * alphaColor;
				}
			}

			return _vertices;
		}

		private bool CanApply()
		{
			if (!IsActive()) return false;
			if (_sampleCount <= 1) return false;
			if (_strength <= 0f) return false;
			if (MotionBlurSimple.GlobalDisabled) return false;
			return true;
		}

		private VertexData ModifyMesh(TMP_TextInfo textInfo)
		{
			// In freeze mode simply return the previous mesh
			if (MotionBlurSimple.GlobalDebugFreeze && _blurredLastFrame && _vertices != null)
			{
				return _vertices;
			}

			VertexData result = null;

			_blurredLastFrame = false;

			if (CanApply())
			{
				bool isForcedLastFrame = (IsDirtySelfForced && !IsDirtyVertices && !IsDirtyTransform);
				_dirtySource = DirtySource.None;

				if (!isForcedLastFrame)
				{
					if (PrepareBuffers(textInfo))
					{
						// NOTE: despite its name, VertexHelper.AddUIVertexTriangleStream() actually replaces the vertices, it doesn't add to them
						result = CreateMotionBlurMesh();

						_blurredLastFrame = true;
					}
					else
					{
						// This is the first frame of this component, so we can't generate any motion blur on this frame, 
						// so just collect the current state, ready to render motion blur on the next frame.
						//Debug.Log(Time.frameCount.ToString() + " PRIME");
					}
					CacheState();
				}
				else
				{
					// TODO: when the motion blur is not being applied, we need to fade it out so there is no jarring switch
					PrepareBuffers(textInfo);
					CacheState();
				}
			}
			else
			{
				_isPrimed = false;
			}
			return result;
		}


		private int _lastRenderFrame = -1;

		void WillRenderCanvases()
		{
			if (_textMeshPro == null || !_textMeshPro.IsActive()) return;
			if (_targetMesh != TargetMesh.Internal) return;

			if (CanApply() && _isPrimed && _canCanvasRender && _vertices != null && _activeVertexCount > 0)
			{
				if (_lastRenderFrame != Time.frameCount)
				{
					// Prevent re-rendering unnecessarily
					_lastRenderFrame = Time.frameCount;

					ApplyOutputMeshAndMaterial(_textMeshPro.textInfo);
				}
			}
		}

		private void PrepareMesh(TMP_TextInfo textInfo)
		{
			if (_mesh == null)
			{
				_mesh = new Mesh();
			}

			{
				int newQuadCount = (_activeVertexCount * _sampleCount) / 4;
				if (_triangleIndices == null || _triangleIndices.Length != (newQuadCount * 6))
				{
					_mesh.Clear();
					_mesh.MarkDynamic();
					_triangleIndices = new int[newQuadCount * 6];
					for (int i = 0; i < newQuadCount; i++)
					{
						int index_X4 = i * 4;
						int index_X6 = i * 6;
						_triangleIndices[0 + index_X6] = 0 + index_X4;
						_triangleIndices[1 + index_X6] = 1 + index_X4;
						_triangleIndices[2 + index_X6] = 2 + index_X4;
						_triangleIndices[3 + index_X6] = 2 + index_X4;
						_triangleIndices[4 + index_X6] = 3 + index_X4;
						_triangleIndices[5 + index_X6] = 0 + index_X4;
					}
				}
			}
		}

		private void ApplyOutputMeshAndMaterial(TMP_TextInfo textInfo)
		{
			// Update the mesh
			{
				PrepareMesh(textInfo);
				_mesh.vertices = _vertices.positions;
				_mesh.triangles = _triangleIndices;
				_mesh.SetColors(_vertices.colors);
				if (s_isUV0Vector4)
				{
					_mesh.SetUVs(0, _vertices.uvs0_v4);
				}
				else
				{
					_mesh.SetUVs(0, _vertices.uvs0);
				}
				_mesh.SetUVs(1, _vertices.uvs1);
				_mesh.RecalculateBounds();
			}
			
			var cr = _textMeshPro.canvasRenderer;
			cr.SetMesh(_mesh);
			//cr.materialCount = 1;
			//cr.SetMaterial(_textMeshPro.GetModifiedMaterial(_textMeshPro.fontSharedMaterial), 0);
		}
	}
}
#endif