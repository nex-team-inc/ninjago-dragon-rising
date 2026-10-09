//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#define UIFX_MOTION_BLUR_CLIP_TO_SCREEN
#define UIFX_MOTION_BLUR_SKIP_ZERO_AREA_MESHES
#if UNITY_EDITOR
//#define UIFX_MOTION_BLUR_DEBUG
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	/// <summary>
	/// The MotionBlurReal component is a visual effect that can be applied to any UI (uGUI) components 
	/// to create an accurate motion blur effect when the UI components are in motion.
	/// </summary>
	/// <remark>
	/// How it works:
	/// 1. Store the mesh and transforms for a UI component for the previous and current frames.
	/// 2. Generates a new mesh containing multiple copies of the stored meshes interpolated from previous to current mesh.
	/// 3. Rendered this mesh additively to a RenderTexture.
	/// 4. On the next frame a quad is rendered to the canvas in place of the UI component geometry.  This quad
	///    uses a shader to resolve the previously rendered motion blur mesh.
	/// 5. If no motion is detected then the effect is disabled.
	///
	/// Comparison between MotionBlurSimple and MotionBlurReal:
	/// 1. MotionBlurSimple is much less expensive to render than MotionBlurReal.
	/// 2. MotionBlurReal produces a much more accurate motion blur than MotionBlurSimple.
	/// 3. MotionBlurReal handles transparency much better than MotionBlurSimple.
	/// 4. MotionBlurReal can become very slow when the motion traveled in a single frame is very large on screen.
	/// 5. MotionBlurReal renders with 1 frame of latency, MotionBlurSimple renders immediately with no latency.
	///
	/// Notes:
	/// 1. Masking is supported, but it doesn't motion blur beyond the bounds of the mask
	/// </remark>
	//[ExecuteAlways]
	[RequireComponent(typeof(Graphic))]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Motion Blur (Real)")]
	public class MotionBlurReal : UIBehaviour, IMeshModifier, IMaterialModifier
	{
		[Tooltip("Which vertex modifiers are used to calculate the motion blur.")]
		[SerializeField] VertexModifierSource _mode = VertexModifierSource.Transform;

		[Tooltip("The number of motion blur steps to calculate.  The higher the number the more expensive the effect.  Set to 1 means the effect is not applied.")]
		[SerializeField, Range(1f, 64f)] int _sampleCount = 16;

		[Tooltip("Interpolate texture coordinates. Disable this if you are changing the characters of text.")]
		[SerializeField] bool _lerpUV = false;

		[Tooltip("Allows frame-rate independent blur length.  This is unrealistic but may be more artistically pleasing as the visual appearance of the motion blur remains consistent across frame rates.")]
		[SerializeField] bool _frameRateIndependent = true;

		[Tooltip("The strength of the effect. Zero means the effect is not applied.  Greater than one means the effect is exagerated.")]
		[SerializeField, Range(0f, 4f)] float _strength = 1f;

		[Tooltip("The shader to use for the additive pass")]
		[SerializeField] Shader _shaderAdd = null;

		[Tooltip("The shader to use for the resolve pass")]
		[SerializeField] Shader _shaderResolve = null;

		// Graphic geometry
		private bool _isPrimed;
		private int _graphicActiveVertexCount;
		private List<UIVertex> _graphicVerticesNow;
		private UIVertex[] _graphicVerticesPast;
		private Matrix4x4 _localToWorldPast;
		private Matrix4x4 _worldToCameraPast;
		private Camera _trackingCamera;

		// Blur geometry
		private int _blurVertexCount;
		private Vector3[] _blurVertexPositions;
		private Vector2[] _blurVertexUV0s;
		private Color[] _blurVertexColors;
		private int[] _blurVertexIndices;
		private bool _isBlurredLastFrame;
		private Mesh _blurMesh;
		private Bounds _blurMeshWorldBounds;

		// Rendering params
		private float _screenWidth, _screenHeight;
		private int _textureWidth, _textureHeight;
		private float _worldHeight;
		private Vector3 _worldCenter;
		private Rect _clampedScreenRect;
		private Bounds _screenBounds;
		private Canvas _canvas;
		private Vector3[] _boundsPoint = new Vector3[8];

		// Rendering
		private Material _materialAdd;
		private Material _materialResolve;
		private CommandBuffer _cb;
		private RenderTexture _rt;

		private Graphic _graphic;
		private Graphic GraphicComponent { get { if (_graphic == null) { _graphic = GetComponent<Graphic>(); } return _graphic; } }

		private MaskableGraphic _maskableGraphic;
		private MaskableGraphic MaskableGraphicComponent { get { if (_maskableGraphic == null) { _maskableGraphic = GraphicComponent as MaskableGraphic; } return _maskableGraphic; } }

		private CanvasRenderer _canvasRenderer;
		private CanvasRenderer CanvasRenderComponent { get { if (_canvasRenderer == null) { if (GraphicComponent) { _canvasRenderer = _graphic.canvasRenderer; } else { _canvasRenderer = GetComponent<CanvasRenderer>(); } } return _canvasRenderer; } }

		private readonly static Vector4 Alpha8TextureAdd = new Vector4(1f, 1f, 1f, 0f);
		private readonly static Color32 WhiteColor32 = new Color32(255, 255, 255, 255);

		static class ShaderProp
		{
			public readonly static int MainTex2 = Shader.PropertyToID("_MainTex2");
			public readonly static int InvSampleCount = Shader.PropertyToID("_InvSampleCount");
		}

		/// <summary>Property <c>UpdateMode</c> sets which vertex modifiers are used to calculate the motion blur</summary>
		/// <value>Set to <c>Mode.Transform</c> by default</value>
		public VertexModifierSource UpdateMode { get { return _mode; } set { _mode = value; ForceMeshModify(); } }

		/// <summary>Property <c>SampleCount</c> sets the number of motion blur steps to calculate.  The higher the number the more expensive the effect.</summary>
		/// <value>Set to 16 by default</value>
		public int SampleCount { get { return _sampleCount; } set { _sampleCount = value; ForceMeshModify(); } }

		/// <summary>Interpolate texture coordinates. Disable this if you are changing the characters of text.</summary>
		public bool LerpUV { get { return _lerpUV; } set { _lerpUV = value; } }

		/// <summary>Property <c>FrameRateIndependent</c> allows frame-rate independent blur length.  This is unrealistic but may be more artistically pleasing as the visual appearance of the motion blur remains consistent across frame rates.</summary>
		public bool FrameRateIndependent { get { return _frameRateIndependent; } set { _frameRateIndependent = value; } }

		/// <summary>Property <c>Strength</c> controls how large the motion blur effect is.</summary>
		/// <value>Set to 1.0 by default.  Zero means the effect is not applied.  Greater than one means the effect is exagerated.</value>
		public float Strength { get { return _strength; } set { _strength = value; ForceMeshModify(); } }
	
		/// <summary>Global debugging option to tint the colour of the motion blur mesh to magenta.  Can be used to tell when the effect is being applied</summary>
		/// <value>Set to <c>false</c> by default</value>
		public static bool GlobalDebugTint = false;

		/// <summary>Global option to freeze updating of the mesh, useful for seeing the motion blur</summary>
		/// <value>Set to <c>false</c> by default</value>
		public static bool GlobalDebugFreeze = false;

		/// <summary>Global option to disable this effect from being applied</summary>
		/// <value>Set to <c>false</c> by default</value>
		public static bool GlobalDisabled = false;

		void CreateComponents()
		{
			if (_materialAdd == null && _shaderAdd != null)
			{
				_materialAdd = new Material(_shaderAdd);
			}
			if (_materialResolve == null && _shaderResolve != null)
			{
				_materialResolve = new Material(_shaderResolve);
			}

			if (_blurMesh == null)
			{
				_blurMesh = new Mesh();
				_blurMesh.name = "MotionBlurReal";
				_blurMesh.MarkDynamic();
			}

			if (_cb == null)
			{
				_cb = new CommandBuffer();
				_cb.name = "MotionBlurReal";
			}
		}

		void DestroyComponents()
		{
			if (_cb != null)
			{
				_cb.Release(); _cb = null;
			}

			RenderTextureHelper.ReleaseTemporary(ref _rt);
			ObjectHelper.Destroy(ref _blurMesh);
			ObjectHelper.Destroy(ref _materialAdd);
			ObjectHelper.Destroy(ref _materialResolve);
		}

		private void RenderMeshToTexture()
		{
			#if UIFX_MOTION_BLUR_SKIP_ZERO_AREA_MESHES
			if (_clampedScreenRect.width <= 0f || _clampedScreenRect.height <= 0f)
			{
				return;
			}
			#endif

			int textureWidth = Mathf.NextPowerOfTwo(Mathf.CeilToInt(_clampedScreenRect.width));
			int textureHeight = Mathf.NextPowerOfTwo(Mathf.CeilToInt(_clampedScreenRect.height));
			textureWidth = Mathf.Min(textureWidth, 4096);
			textureHeight = Mathf.Min(textureHeight, 4096);
			if (textureWidth <= 0 || textureHeight <= 0)
			{
				return;
			}

			if (_screenWidth <= 0)
			{
				Debug.LogError("Skipping rendering MotionBlurReal frame because screen has no width");
				return;
			}

			if (_rt)
			{
				if (textureWidth > 0 && textureHeight > 0 && (_rt.width != textureWidth || _rt.height != textureHeight))
				{
					RenderTexture.ReleaseTemporary(_rt); _rt = null;
				}
			}
			if (_rt == null)
			{
				_rt = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
			}

			// NOTE: UI material properties such as:
			// Graphic.Color, CanvasRenderer.Color, CanvasRenderer.InheritedAlpha, and ClipRect.
			// Are not applied during the additive pass, but only in the resolve pass.
			// This is mostly because Unity doesn't set these material properties until it is 
			// rendering and there is no way to retrieve them beforehand.

			// But we do have to set basic properties, such as mainTexture and _TextureAddSample
			{
				_materialAdd.mainTexture = GraphicComponent.mainTexture;

				if (_materialAdd.mainTexture is Texture2D && ((Texture2D)_materialAdd.mainTexture).format == TextureFormat.Alpha8)
				{
					_materialAdd.SetVector(UnityShaderProp.TextureAddSample, Alpha8TextureAdd);
				}
				else
				{
					_materialAdd.SetVector(UnityShaderProp.TextureAddSample, Vector4.zero);
				}
			}

			{
				_cb.Clear();
				_cb.SetRenderTarget(_rt);
				#if UIFX_MOTION_BLUR_DEBUG
				_cb.ClearRenderTarget(false, true, Color.magenta, 1f);
				#endif
				_cb.SetViewport(new Rect(0f, 0f, Mathf.Ceil(_clampedScreenRect.width), Mathf.Ceil(_clampedScreenRect.height)));
				_cb.ClearRenderTarget(false, true, Color.clear, 1f);
				float aspect = (_clampedScreenRect.width / _clampedScreenRect.height);

				//Debug.Log(aspect + " "+ _clampedScreenRect);

				// TODO: when adding support for world-space perspective camera, will need to render using those camera properties

				float w = (_worldHeight * aspect) / 2f;
				float h = (_worldHeight) / 2f;
				_cb.SetProjectionMatrix(Matrix4x4.Ortho(-w, w, -h, h, 0.01f, 1000f));
				//_cb.SetProjectionMatrix(Matrix4x4.Perspective(60f, 16f/9f, 0.3f, 1000f));
				if (_trackingCamera)
				{
				//	_cb.SetProjectionMatrix(Matrix4x4.Perspective(60f, 1.7777f, 0.01f, 1000f));
				}

				// Matrix that looks from camera's position, along the forward axis.
				var lookMatrix = Matrix4x4.LookAt(_worldCenter, _worldCenter + Vector3.forward, Vector3.up);
				// Matrix that mirrors along Z axis, to match the camera space convention.
				var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, -1f));
				if (_trackingCamera)
				{
				//	scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1f, -1f, -1f));
				}
				// Final view matrix is inverse of the LookAt matrix, and then mirrored along Z.
				var viewMatrix = scaleMatrix * lookMatrix.inverse;

				_cb.SetViewMatrix(viewMatrix);
				_cb.DrawMesh(_blurMesh, Matrix4x4.TRS(new Vector3(0f, 0f, 10f), Quaternion.identity, Vector3.one), _materialAdd);
				Graphics.ExecuteCommandBuffer(_cb);
			}

			//GraphicComponent.materialForRendering.CopyPropertiesFromMaterial(_materialAdd);
			_materialResolve.mainTexture = _rt;
			_materialResolve.SetTexture(ShaderProp.MainTex2, _rt);
			_materialResolve.SetFloat(ShaderProp.InvSampleCount, 1f / _sampleCount);
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void Start()
		{
			Debug.Assert(_shaderAdd != null);
			Debug.Assert(_shaderResolve != null);
			CreateComponents();
			base.Start();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnEnable()
		{
			_isPrimed = false;
			_canvas = GetCanvas();
			CreateComponents();
			GraphicComponent.RegisterDirtyVerticesCallback(OnDirtyVertices);
			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.AddListener(OnCullingChanged);
			}
			ForceMeshModify();
			ForceMaterialModify();
			base.OnEnable();
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnDisable()
		{
			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.RemoveListener(OnCullingChanged);
			}
			GraphicComponent.UnregisterDirtyVerticesCallback(OnDirtyVertices);
			DestroyComponents();
			ForceMeshModify();
			ForceMaterialModify();
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
			_canvas = GetCanvas();
			ResetMotion();
			ForceMeshModify();
			ForceMaterialModify();
			base.OnCanvasHierarchyChanged();
		}

		private void ForceMeshModify()
		{
			GraphicComponent.SetVerticesDirty();
		}
		
		private void ForceMaterialModify()
		{
//			Debug.Log("dirty material");
			GraphicComponent.SetMaterialDirty();
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
			// TODO: explain this logic?
			if (!IsDirtyTransform && !IsDirtySelfForced)
			{
				IsDirtyVertices = true;
			}
		}

		void LateUpdate()
		{
			// TODO: only do this when necessary!
			ForceMeshModify();
			ForceMaterialModify();
		}

		void LateUpdate5()
		{
			if (GlobalDebugFreeze)
			{
				//ForceMaterialModify();
				return;
			}


			// Draw the previous frames mesh to the RenderTexture
			if (HasGeneratedMesh())
			{
				RenderMeshToTexture();
			}

			// If blur was applied to the last frame, then force a new frame to render in case there has been no motion
			// in which case it needs to be rendered without any motion blur.
			if (_isBlurredLastFrame)
			{
				IsDirtySelfForced = true;
				//ForceMeshModify();
				//ForceMaterialModify();
			}

			//ForceMeshModify();
			//ForceMaterialModify();
				
			// Detect changes to the transform or the camera
			if ((IsTrackingTransform() && _localToWorldPast != this.transform.localToWorldMatrix) ||
				(_trackingCamera != null && _worldToCameraPast != _trackingCamera.transform.worldToLocalMatrix))
			{
				IsDirtyTransform = true;
				//ForceMeshModify();
			}
			else
			{
				//Debug.Log("NO MOVEMENT");
			}
		}

		private bool HasGeneratedMesh()
		{
			return (_textureWidth > 0);
		}

		private bool HasRendered()
		{
			return (_rt != null);
		}

		private bool CanApply()
		{
			if (!IsActive()) return false;
			if (_sampleCount <= 1) return false;
			if (_strength <= 0f) return false;
			if (!_blurMesh) return false;
			if (_canvas == null) return false;
			if (GlobalDisabled) return false;
			return true;
		}

		private Canvas GetCanvas()
		{
			Canvas result = null;
			if (GraphicComponent)
			{
				result = GraphicComponent.canvas;
			}
			else
			{
				result = GetComponentInParent<Canvas>();
			}
			return result;
		}

		private Camera GetRenderCamera()
		{
			Camera camera = _canvas.worldCamera;
			if (camera == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
			{
				camera = null;
			}
			return camera;
		}

		/// This is where the geometry is gathered
		/// Draw the geometry (amplified) to addivitive RT
		/// Draw a quad to screen sampling this buffer..
		[UnityInternal.ExcludeFromDocs]
		public void ModifyMesh(VertexHelper vh)
		{
			// In freeze mode simply return the previous quad mesh
			if (GlobalDebugFreeze && HasRendered() && _isBlurredLastFrame)
			{
				GenerateQuad(vh, GetRenderCamera());
				return;
			}

			_isBlurredLastFrame = false;

			if (CanApply())
			{
				Camera renderCamera = GetRenderCamera();
				//Debug.Log(Time.frameCount.ToString() + " ModifyMesh");
				//Debug.Log(Time.frameCount.ToString() + " MODMESH");

				bool isForcedLastFrame = (IsDirtySelfForced && !IsDirtyVertices && !IsDirtyTransform);
				_dirtySource = DirtySource.None;

				if (!isForcedLastFrame)
				{
					if (PrepareBuffers(vh))
					{
						// Generate motion blur mesh vertices
						CreateMotionBlurMesh(vh);

						// Copy motion blur vertices to a Mesh
						{
							_blurMesh.Clear();

							#if UNITY_2020_1_OR_NEWER
							// NOTE: for some reason if MeshUpdateFlags.DontRecalculateBounds is added when motion blur is not rendered...
							MeshUpdateFlags flags = MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontResetBoneBounds|MeshUpdateFlags.DontNotifyMeshUsers;
							_blurMesh.SetVertices(_blurVertexPositions, 0, _blurVertexCount, flags);
							_blurMesh.SetUVs(0, _blurVertexUV0s, 0, _blurVertexCount, flags);
							_blurMesh.SetColors(_blurVertexColors, 0, _blurVertexCount, flags);
							#elif UNITY_2019_3_OR_NEWER
							_blurMesh.SetVertices(_blurVertexPositions);
							_blurMesh.SetUVs(0, _blurVertexUV0s);
							_blurMesh.SetColors(_blurVertexColors);
							#else
							_blurMesh.SetVertices(new List<Vector3>(_blurVertexPositions));
							_blurMesh.SetUVs(0, new List<Vector2>(_blurVertexUV0s));
							_blurMesh.SetColors(new List<Color>(_blurVertexColors));
							#endif

							#if UNITY_2020_1_OR_NEWER
							_blurMesh.SetTriangles(_blurVertexIndices, 0, _blurVertexIndices.Length, 0, calculateBounds:true, 0);
							#else
							_blurMesh.SetTriangles(_blurVertexIndices, 0, calculateBounds:false);
							#endif
						}

						// Get mesh world bounds
						{
							#if UNITY_2020_1_OR_NEWER
							_blurMeshWorldBounds = _blurMesh.bounds;
							#else
							// NOTE: We don't use the _blurMesh.bounds property because it is calculated using All the vertices, even if they aren't referecned in the triangle indices
							Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
							Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
							for (int i = 0; i < _blurVertexCount; i++)
							{
								min = Vector3.Min(_blurVertexPositions[i], min);
								max = Vector3.Max(_blurVertexPositions[i], max);
							}
							_blurMeshWorldBounds = new Bounds(min, Vector3.zero);
							_blurMeshWorldBounds.Encapsulate(max);
							#endif
						}

						GenerateRenderMetrics(renderCamera);
						LateUpdate5();

						// Modify vertices to render a quad with the resulting texture
						if (HasRendered())
						{
		#if UIFX_MOTION_BLUR_SKIP_ZERO_AREA_MESHES
							if (_clampedScreenRect.width <= 0f || _clampedScreenRect.height <= 0f)
							{
								vh.Clear();
							}
							else
		#endif
							{
								//Debug.Log("GenerateQuad");
								GenerateQuad(vh, renderCamera);
							}
							_isBlurredLastFrame = true;
						}
						else
						{
							//Debug.Log("!HasRendered");
						}

						//GenerateRenderMetrics(renderCamera);
					}
					else
					{
						// This is the first frame of this component, so we can't generate any motion blur on this frame, 
						// so just collect the current state, ready to render motion blur on the next frame.
						//Debug.Log("FIRST");
					}
					CacheState();
				}
				else
				{
					//Debug.Log("FORCE");
					PrepareBuffers(vh);
					CacheState();
				}
			}
			else
			{
				//Debug.Log("!PRIMED");
				_isPrimed = false;
				//Debug.Log(Time.frameCount.ToString() + " SKIP");
			}
		}

		private void GenerateRenderMetrics(Camera camera)
		{
			// Get world vertex bounds (Note that _blurMesh is already in world space)
			_boundsPoint[0] = _blurMeshWorldBounds.min;
			_boundsPoint[1] = _blurMeshWorldBounds.max;
			_boundsPoint[2] = new Vector3(_boundsPoint[0].x, _boundsPoint[0].y, _boundsPoint[1].z);
			_boundsPoint[3] = new Vector3(_boundsPoint[0].x, _boundsPoint[1].y, _boundsPoint[0].z);
			_boundsPoint[4] = new Vector3(_boundsPoint[1].x, _boundsPoint[0].y, _boundsPoint[0].z);
			_boundsPoint[5] = new Vector3(_boundsPoint[0].x, _boundsPoint[1].y, _boundsPoint[1].z);
			_boundsPoint[6] = new Vector3(_boundsPoint[1].x, _boundsPoint[0].y, _boundsPoint[1].z);
			_boundsPoint[7] = new Vector3(_boundsPoint[1].x, _boundsPoint[1].y, _boundsPoint[0].z);

			if (_trackingCamera != null)
			{
				Matrix4x4 cameraToWorld = _trackingCamera.transform.localToWorldMatrix;
				for (int i = 0; i < _boundsPoint.Length; i++)
				{
					_boundsPoint[i] = cameraToWorld.MultiplyPoint3x4(_boundsPoint[i]);
				}
			}

			_screenBounds = new Bounds();
			for (int i = 0; i < _boundsPoint.Length; i++)
			{
				// convert world bounds to screen space
				if (camera)
				{
					_boundsPoint[i] = camera.WorldToScreenPoint(_boundsPoint[i]);
				}

				if (i == 0)
				{
					// Initialise with the first point
					_screenBounds.center = _boundsPoint[i];
				}
				else
				{
					// Grow with the other points
					_screenBounds.Encapsulate(_boundsPoint[i]);
				}
			}

			#if UIFX_MOTION_BLUR_CLIP_TO_SCREEN
			{
				Vector2 screenMin = Vector2.zero;
				Vector2 screenMax = new Vector2(Screen.width, Screen.height);

				// NOTE: The above calculations is not correct in Overlay canvas mode? Maybe.

				Vector3 clampedMin = Vector3.Max(screenMin, _screenBounds.min);
				Vector3 clampedMax = Vector3.Min(screenMax, _screenBounds.max);
				_clampedScreenRect = Rect.MinMaxRect(clampedMin.x, clampedMin.y, clampedMax.x, clampedMax.y);
			}
			#else
			_clampedScreenRect = Rect.MinMaxRect(_screenBounds.min.x, _screenBounds.min.y, _screenBounds.max.x, _screenBounds.max.y);
			#endif

			if (camera)
			{
				_worldCenter = camera.ScreenToWorldPoint(new Vector3(_clampedScreenRect.center.x, _clampedScreenRect.center.y, _screenBounds.min.z));
			}
			else
			{
				_worldCenter = new Vector3(_clampedScreenRect.center.x, _clampedScreenRect.center.y, _screenBounds.min.z);
			}

			if (camera)
			{
				Vector3 minWorld = camera.ScreenToWorldPoint(new Vector3(_clampedScreenRect.min.x, _clampedScreenRect.min.y, _screenBounds.min.z));
				Vector3 maxWorld = camera.ScreenToWorldPoint(new Vector3(_clampedScreenRect.max.x, _clampedScreenRect.max.y, _screenBounds.min.z));
				_worldHeight = (maxWorld - minWorld).y;
			}
			else
			{
				_worldHeight = _clampedScreenRect.height;
			}
			
			Vector2 size = _screenBounds.max - _screenBounds.min;
			_screenWidth = size.x;
			_screenHeight = size.y;
			_textureWidth = Mathf.NextPowerOfTwo(Mathf.CeilToInt(size.x));
			_textureHeight = Mathf.NextPowerOfTwo(Mathf.CeilToInt(size.y));
		}

		private void GenerateQuad(VertexHelper vh, Camera camera)
		{
			// 4 corners of quad
			Vector3 v0 = (new Vector2(_clampedScreenRect.xMin, _clampedScreenRect.yMax));
			Vector3 v1 = (new Vector2(_clampedScreenRect.xMax, _clampedScreenRect.yMax));
			Vector3 v2 = (new Vector2(_clampedScreenRect.xMax, _clampedScreenRect.yMin));
			Vector3 v3 = (new Vector2(_clampedScreenRect.xMin, _clampedScreenRect.yMin));

			// Set depth
			/*if (_trackingCamera)
			{
				v3.z = v2.z = v1.z = v0.z = -100f;//vertices[0].position.z;
			}*/
			v3.z = v2.z = v1.z = v0.z = _screenBounds.min.z;

			// Convert screen to world space
			if (camera)
			{
				v0 = camera.ScreenToWorldPoint(v0);
				v1 = camera.ScreenToWorldPoint(v1);
				v2 = camera.ScreenToWorldPoint(v2);
				v3 = camera.ScreenToWorldPoint(v3);
			}

			// Convert to local space
			Matrix4x4 worldToLocal = this.transform.worldToLocalMatrix;
			v0 = worldToLocal.MultiplyPoint(v0);
			v1 = worldToLocal.MultiplyPoint(v1);
			v2 = worldToLocal.MultiplyPoint(v2);
			v3 = worldToLocal.MultiplyPoint(v3);

			//v3.z = v2.z = v1.z = v0.z = 0f;//vertices[0].position.z;

			float tx = 1f;
			float ty = 1f;
			// We're rendering into Pow2 sized textures, so they're usually larger than needed, so need to scale the UV coordinates
			{
				tx = _clampedScreenRect.width / (float)_rt.width;
				ty = _clampedScreenRect.height / (float)_rt.height;

				// Add half texel offset because without it there seems to be some rounding error where texels from previous render bounds are visible
				//tx -= 0.5f / (float)_rt.width;
				//ty -= 0.5f / (float)_rt.height;
			}

			// Display the last rendered texture (t+1)
			vh.Clear();
			vh.AddVert(v0, WhiteColor32, new Vector4(0f, ty, 0f, 0f));
			vh.AddVert(v1, WhiteColor32, new Vector4(tx, ty, 0f, 0f));
			vh.AddVert(v2, WhiteColor32, new Vector4(tx, 0f, 0f, 0f));
			vh.AddVert(v3, WhiteColor32, Vector4.zero);
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(0, 2, 3);
		}

		[UnityInternal.ExcludeFromDocs]
		public Material GetModifiedMaterial(Material baseMaterial)
		{
			if (!CanApply() || !HasRendered() || !_isBlurredLastFrame)
			{
				return baseMaterial;
			}

			// Copy material properties (and enabled keywords) for rendering
			_materialAdd.CopyPropertiesFromMaterial(baseMaterial);

			// Copy the stencil properties
			UnityShaderProp.CopyStencilProperties(baseMaterial, _materialResolve);

			return _materialResolve;
		}

		/// <summary>
		/// Reset the motion blur to begin again at the current state (transform/vertex positions).
		/// This is useful when reseting the transform to prevent motion blur drawing erroneously between
		/// the last position and the new position.
		/// </summary>
		public void ResetMotion()
		{
			_isPrimed = false;
			//_blurredLastFrame = false;
			//_dirtySource = DirtySource.None;
		}

		private bool PrepareBuffers(VertexHelper vh)
		{
			bool isPrepared = _isPrimed;
			if (_graphicVerticesNow == null || vh.currentIndexCount > _graphicVerticesNow.Count)
			{
				// If the number of vertices has changed, we need to prime
				// NOTE: We compare with the number of indices, as this is really how many vertices will be returned, as GetUIVertexStream() returns triangle indices
				_graphicVerticesNow = new List<UIVertex>(vh.currentIndexCount);
				_graphicVerticesPast = new UIVertex[vh.currentIndexCount];
				_graphicActiveVertexCount = vh.currentIndexCount;
				this.IsDirtyVertices = true;
				isPrepared = false;
			}
			else if (vh.currentIndexCount < _graphicVerticesNow.Count)
			{
				// If the number of vertices has decreased, then don't reallocate the list, just use a portion of it
				_graphicActiveVertexCount = vh.currentIndexCount;
				this.IsDirtyVertices = true;
				isPrepared = false;
			}

			// Copy graphic vertices
			// TOOD: Only when vertices changed, but need to find a good way to detect this
			//if (IsDirtyVertices)
			{
				vh.GetUIVertexStream(_graphicVerticesNow);
			}

			int motionBlurVertexCount = _graphicActiveVertexCount * _sampleCount;

			if (_blurVertexPositions == null || motionBlurVertexCount > _blurVertexPositions.Length)
			{
				_blurVertexPositions = new Vector3[motionBlurVertexCount];
				_blurVertexUV0s = new Vector2[motionBlurVertexCount];
				_blurVertexColors = new Color[motionBlurVertexCount];
				_blurVertexIndices = new int[motionBlurVertexCount];
				for (int i = 0; i < motionBlurVertexCount; i++)
				{
					_blurVertexIndices[i] = i;
				}
				_blurVertexCount = motionBlurVertexCount;
				isPrepared = false;
			}

			// If the samplecount has been decreased, then we need to invalidate the
			// higher up triangle indices so the geometry doesn't render.
			if (motionBlurVertexCount < _blurVertexCount)
			{
				for (int i = motionBlurVertexCount; i < _blurVertexCount; i++)
				{
					_blurVertexIndices[i] = 0;
				}
			}
			// If the sampleCount has increased, regenerate the triangles
			else if (motionBlurVertexCount > _blurVertexCount)
			{
				for (int i = _blurVertexCount; i < motionBlurVertexCount; i++)
				{
					_blurVertexIndices[i] = i;
				}
			}
			_blurVertexCount = motionBlurVertexCount;

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
						Debug.LogWarning("[MotionBlurReal] No world camera specified on Canvas.");
					}
				}
			}
		}

		private void CacheState()
		{
			UpdateCanvasCamera();

			if (IsTrackingTransform())
			{
				_localToWorldPast = this.transform.localToWorldMatrix;
			}
			if (IsTrackingVertices())
			{
				_graphicVerticesNow.CopyTo(0, _graphicVerticesPast, 0, _graphicActiveVertexCount);
			}
			if (_trackingCamera != null)
			{
				_worldToCameraPast = _trackingCamera.transform.worldToLocalMatrix;
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
				t = Mathf.LerpUnclamped(tailT, headT, t);
			}

			return t;
		}

		private void CreateMotionBlurMesh(VertexHelper vh)
		{
			float stepSize = 1f / (_sampleCount - 1f);
			float t = 0f;

			Matrix4x4 localToWorldPast = _localToWorldPast;
			Matrix4x4 localToWorld = this.transform.localToWorldMatrix;
			Matrix4x4 worldToLocal = this.transform.worldToLocalMatrix;

			// If we're tracking camera motion, then modify the matrix to convert to camera-space instead of world-space.
			if (_trackingCamera != null)
			{
				if (IsTrackingTransform())
				{
					localToWorldPast = _worldToCameraPast * localToWorldPast;
				}
				else
				{
					// In Vertex mode we only use the current localToWorld transform, not the previous one
					localToWorldPast = _worldToCameraPast * localToWorld;
				}

				Matrix4x4 worldToCamera = _trackingCamera.transform.worldToLocalMatrix;
				localToWorld = worldToCamera * localToWorld;

				Matrix4x4 cameraToWorld = _trackingCamera.transform.localToWorldMatrix;
				worldToLocal = worldToLocal * cameraToWorld;
			}
			
			//_vtxVertices.Clear();
			//_vtxUV0.Clear();
			//_vtxColors.Clear();

    		/*Canvas copyOfMainCanvas = GameObject.Find("Canvas").GetComponent <Canvas>();
    			float scaleFactor = copyOfMainCanvas.scaleFactor;
				Vector2 displaySize = copyOfMainCanvas.renderingDisplaySize;

			Debug.Log("mtx " + worldToLocal + " " + localToWorld + " " + scaleFactor + " " + displaySize + " " + copyOfMainCanvas.referencePixelsPerUnit);

			var canvasRect = copyOfMainCanvas.GetComponent<RectTransform>();
       		 var scale = canvasRect.sizeDelta;
			 Debug.Log("SCALE: " + scale);*/

			if (_mode == VertexModifierSource.Transform)
			{
				int idx = 0;
				for (int j = 0; j < _sampleCount; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					
					for (int i = 0; i < _graphicActiveVertexCount; i++)
					{
						UIVertex vv = _graphicVerticesNow[i];

						// Xform the previous position to world space
						Vector3 v0 = localToWorldPast.MultiplyPoint3x4(vv.position);
						// Xform the current position to world space
						Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position); 

						vv.position = Vector3.LerpUnclamped(v0, v1, tScaled);
	
						//_vertices.Add(vv);
						_blurVertexPositions[idx] = vv.position;
						_blurVertexUV0s[idx] = vv.uv0;
						_blurVertexColors[idx] = vv.color;
						idx++;
					}
					t += stepSize;
				}
			}
			else if (_mode == VertexModifierSource.Vertex)
			{
				int idx = 0;
				for (int j = 0; j < _sampleCount; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					float tScaledClamped = Mathf.Clamp01(tScaled);

					if (_lerpUV)
					{
						for (int i = 0; i < _graphicActiveVertexCount; i++)
						{
							UIVertex vv = _graphicVerticesNow[i];

							vv.position = Vector3.LerpUnclamped(_graphicVerticesPast[i].position, vv.position, tScaled);
							// Xform the current position to world space
							vv.position = localToWorld.MultiplyPoint3x4(vv.position); 

							vv.uv0 = Vector4.LerpUnclamped(_graphicVerticesPast[i].uv0, vv.uv0, tScaledClamped);
							vv.color = Color.LerpUnclamped(_graphicVerticesPast[i].color, vv.color, tScaledClamped);
							_blurVertexPositions[idx] = vv.position;
							_blurVertexUV0s[idx] = vv.uv0;
							_blurVertexColors[idx] = vv.color;
							idx++;
						}
					}
					else
					{
						for (int i = 0; i < _graphicActiveVertexCount; i++)
						{
							UIVertex vv = _graphicVerticesNow[i];

							vv.position = Vector3.LerpUnclamped(_graphicVerticesPast[i].position, vv.position, tScaled);
							// Xform the current position to world space
							vv.position = localToWorld.MultiplyPoint3x4(vv.position); 

							vv.color = Color.LerpUnclamped(_graphicVerticesPast[i].color, vv.color, tScaledClamped);
							_blurVertexPositions[idx] = vv.position;
							_blurVertexUV0s[idx] = vv.uv0;
							_blurVertexColors[idx] = vv.color;
							idx++;
						}
					}
					t += stepSize;
				}	
			}
			else if (_mode == VertexModifierSource.TranformAndVertex)
			{
				int idx = 0;
				for (int j = 0; j < _sampleCount; j++)
				{
					float tScaled = GetLerpFactorUnclamped(t);
					float tScaledClamped = Mathf.Clamp01(tScaled);

					if (_lerpUV)
					{
						for (int i = 0; i < _graphicActiveVertexCount; i++)
						{
							UIVertex vv = _graphicVerticesNow[i];

							// Xform the previous position to world space
							Vector3 v0 = localToWorldPast.MultiplyPoint3x4(_graphicVerticesPast[i].position);
							// Xform the current position to world space
							Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position); 

							vv.position = Vector3.LerpUnclamped(v0, v1, tScaled);

							vv.uv0 = Vector4.LerpUnclamped(_graphicVerticesPast[i].uv0, _graphicVerticesNow[i].uv0, tScaledClamped);
							vv.color = Color.LerpUnclamped(_graphicVerticesPast[i].color, _graphicVerticesNow[i].color, tScaledClamped);

							_blurVertexPositions[idx] = vv.position;
							_blurVertexUV0s[idx] = vv.uv0;
							_blurVertexColors[idx] = vv.color;
							idx++;
						}
					}
					else
					{
						for (int i = 0; i < _graphicActiveVertexCount; i++)
						{
							UIVertex vv = _graphicVerticesNow[i];

							// Xform the previous position to world space
							Vector3 v0 = localToWorldPast.MultiplyPoint3x4(_graphicVerticesPast[i].position);
							// Xform the current position to world space
							Vector3 v1 = localToWorld.MultiplyPoint3x4(vv.position); 

							vv.position = Vector3.LerpUnclamped(v0, v1, tScaled);

							vv.color = Color.LerpUnclamped(_graphicVerticesPast[i].color, _graphicVerticesNow[i].color, tScaledClamped);

							_blurVertexPositions[idx] = vv.position;
							_blurVertexUV0s[idx] = vv.uv0;
							_blurVertexColors[idx] = vv.color;
							idx++;
						}
					}
					t += stepSize;
				}
			}

			if (GlobalDebugTint)
			{
				for (int i = 0; i < _blurVertexColors.Length; i++)
				{
					_blurVertexColors[i] = Color.magenta;
				}
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