//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
#if UIFX_UITK && UNITY_2021_2_OR_NEWER
using UnityEngine.TextCore.Text;
#endif

namespace ChocDino.UIFX
{
	/// <summary>
	/// UIToolkitTextSource uses UI Toolkit to handle advanced text rendering (eg Arabic) and brings the result to Unity UI (UGUI)
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Sources/UIFX - UITK Text Source")]
	public class UIToolkitTextSource : MaskableGraphic, IMaterialModifier, IMeshModifier
	{
#if UIFX_UITK && UNITY_2021_2_OR_NEWER

		[SerializeField, MultilineAttribute(4)] string _text;
		[SerializeField] Font _font;
		[SerializeField] FontAsset _fontAsset;
		[SerializeField] float _fontSize = 14f;
		[SerializeField] Color _color = Color.white;
		[SerializeField] TextAnchor _textAnchor = TextAnchor.MiddleCenter;
		[SerializeField] WhiteSpace _wrap = WhiteSpace.NoWrap;
		[SerializeField] TextOverflow _overflow = TextOverflow.Clip;
		[SerializeField] FontStyle _style = FontStyle.Normal;
		[SerializeField] bool _outline = false;
		[SerializeField] float _outlineWidth = 2f;
		[SerializeField] Color _outlineColor = Color.black;
		[SerializeField] bool _shadow = false;
		[SerializeField] Color _shadowColor = Color.black;
		[SerializeField] Vector2 _shadowOffset = new Vector2(4f, 4f);
		[SerializeField] float _shadowBlurRadius = 4f;
		[SerializeField] float _letterSpacing = 0f;
		[SerializeField] float _wordSpacing = 0f;
		[SerializeField] float _paragraphSpacing = 0f;
		[SerializeField] int _edgePadding = 16;

		#if UNITY_6000_0_OR_NEWER
		[SerializeField] TextGeneratorType _textGeneratorType;
		#endif
		[SerializeField] ThemeStyleSheet _styleSheet;

		public string Text { get => _text; set { _text = value; ForceUpdate(); } }
		public Font Font { get => _font; set { _font = value; ForceUpdate(); } }
		public FontAsset FontAsset { get => _fontAsset; set { _fontAsset = value; ForceUpdate(); } }
		public float FontSize { get => _fontSize; set { _fontSize = Mathf.Clamp(value, 0f, 1024f); ForceUpdate(); } }
		public Color Color { get => _color; set { _color = value; ForceUpdate(); } } 
		public TextAnchor TextAnchor { get => _textAnchor; set { _textAnchor = value; ForceUpdate(); } }
		public WhiteSpace Wrap { get => _wrap; set { _wrap = value; ForceUpdate(); } }
		public TextOverflow Overflow { get => _overflow; set { _overflow = value; ForceUpdate(); } }
		public FontStyle Style { get => _style; set { _style = value; ForceUpdate(); } }
		public bool Outline { get => _outline; set { _outline= value; ForceUpdate(); } }
		public float OutlineWidth { get => _outlineWidth; set { _outlineWidth = Mathf.Clamp(value, 0f, 128f); ForceUpdate(); } }
		public Color OutlineColor { get => _outlineColor; set { _outlineColor = value; ForceUpdate(); } } 
		public bool Shadow { get => _shadow; set { _shadow = value; ForceUpdate(); } }
		public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; ForceUpdate(); } }
		public Vector2 ShadowOffset { get => _shadowOffset; set { _shadowOffset = value; ForceUpdate(); } }
		public float ShadowBlurRadius { get => _shadowBlurRadius; set { _shadowBlurRadius = Mathf.Clamp(value, 0f, 128f); ForceUpdate(); } }
		public float LetterSpacing { get => _letterSpacing; set { _letterSpacing = Mathf.Clamp(value, -1024f, 1024f); ForceUpdate(); } }
		public float WordSpacing { get => _wordSpacing; set { _wordSpacing = Mathf.Clamp(value, -1024f, 1024f); ForceUpdate(); } }
		public float ParagraphSpacing { get => _paragraphSpacing; set { _paragraphSpacing = Mathf.Clamp(value, -1024f, 1024f); ForceUpdate(); } }
		public int EdgePadding { get => _edgePadding; set { _edgePadding = Mathf.Clamp(value, 0, 128); ForceUpdate(); } }

		public RenderTexture Texture { get => _renderTexture; }

		private GameObject _gameObject;
		private RenderTexture _renderTexture;
		private Material _material;
		private UIDocument _document;
		private UnityEngine.UIElements.TextElement _textElement;
		private PanelSettings _panelSettings;
		private TextShadow _textShadow = new TextShadow();
		private TextShadow _nullTextShadow = new TextShadow();
		private RectTransform _rectTransform;

		public override Texture mainTexture => _renderTexture;

		private const string DisplayShaderPath = "Hidden/ChocDino/UIFX/Blend-UIToolkit";

		protected override void Awake()
		{
			_rectTransform = this.GetComponent<RectTransform>();
			this.useLegacyMeshGeneration = false;
			base.Awake();
		}

		protected override void Start()
		{
			CreateDocument();
			base.Start();
		}

		private void CreateDocument()
		{
			if (_gameObject == null)
			{
				var xform = this.transform.Find("UIDocument");
				if (xform)
				{
					_gameObject = xform.gameObject;
				}
			}
			if (_gameObject == null)
			{
				_gameObject = new GameObject("UIDocument");
				_gameObject.transform.SetParent(this.transform, false);
			}

			if (_document == null)
			{
				_document = _gameObject.GetComponent<UIDocument>();
				if (_document == null)
				{
					_document = _gameObject.AddComponent<UIDocument>();
				}
				// NOTE: We need to set visualTreeAsset to null because this calls RebuildUI which is needed to guarantee rootVisualElement is initiated.
				_document.visualTreeAsset = null;
			}
			if (_panelSettings == null)
			{
				_panelSettings = _document.panelSettings;
				if (_panelSettings == null)
				{
					_panelSettings = _document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
					_panelSettings.themeStyleSheet = _styleSheet;
					_panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
					_panelSettings.referenceResolution = new Vector2Int(1920, 1080);
					_panelSettings.clearColor = false;
					_panelSettings.colorClearValue = Color.clear;
				}
			}
			if (_textElement == null)
			{
				if (_document.rootVisualElement != null)
				{
					_textElement = _document.rootVisualElement.Query<UnityEngine.UIElements.TextElement>();
					if (_textElement == null)
					{
						_textElement = new UnityEngine.UIElements.TextElement();
						_document.rootVisualElement.Add(_textElement);
					}
				}
			}
			
			ApplyTextSettings();
		}

		private bool IsTextWrap()
		{
			return (_wrap == WhiteSpace.Normal 
				#if UNITY_6000_0_OR_NEWER
					|| _wrap == WhiteSpace.PreWrap
				#endif
					);
		}

		private void ApplyTextSettings()
		{
			if (_textElement == null) return;
			if (_textElement.style == null) return;
			#if UNITY_6000_0_OR_NEWER
			_textElement.style.unityTextGenerator = _textGeneratorType;
			#endif
			_textElement.style.unityTextAlign = _textAnchor;
			_textElement.style.whiteSpace = _wrap;
			_textElement.style.unityFontStyleAndWeight = _style;
			_textElement.style.fontSize = _fontSize;
			_textElement.style.wordSpacing = _wordSpacing;
			_textElement.style.letterSpacing = _letterSpacing;
			_textElement.style.unityParagraphSpacing = _paragraphSpacing;
			_textElement.style.unityTextAlign = _textAnchor;
			//_textElement.style.unityOverflowClipBox = OverflowClipBox.ContentBox;
			//_textElement.style.unityTextOverflowPosition = TextOverflowPosition.Start;
			//_textElement.style.textOverflow = TextOverflow.Ellipsis;
			if (_outline)
			{
				_textElement.style.unityTextOutlineWidth = _outlineWidth;
				_textElement.style.unityTextOutlineColor = _outlineColor;
			}
			else
			{
				_textElement.style.unityTextOutlineWidth = 0f;
			}
			if (_shadow)
			{
				_textShadow.color = _shadowColor;
				_textShadow.offset = _shadowOffset;
				_textShadow.blurRadius = _shadowBlurRadius;
				_textElement.style.textShadow = _textShadow;
			}
			else
			{
				_textElement.style.textShadow = _nullTextShadow;
			}
			_textElement.text = _text;
			_textElement.style.color = _color;
			if (_fontAsset != null)
			{
				_textElement.style.unityFontDefinition = new StyleFontDefinition(_fontAsset);
			}
			else
			{
				_textElement.style.unityFontDefinition = new StyleFontDefinition(_font);
			}

			_textElement.style.width = _rectTransform.sizeDelta.x;
			_textElement.style.height = _rectTransform.sizeDelta.y;
			_textElement.style.position = Position.Relative;
			_textElement.style.left = StyleKeyword.Auto;
			_textElement.style.top = StyleKeyword.Auto;
			_textElement.style.right = StyleKeyword.Auto;
			_textElement.style.bottom = StyleKeyword.Auto;

			if (!IsTextWrap())
			{
				Vector2 size = _textElement.MeasureTextSize(_textElement.text, _rectTransform.sizeDelta.x, VisualElement.MeasureMode.Undefined, _rectTransform.sizeDelta.y, VisualElement.MeasureMode.Undefined);
				switch (_textAnchor)
				{
					case TextAnchor.LowerLeft:
					case TextAnchor.UpperLeft:
					case TextAnchor.MiddleLeft:
					_textElement.style.left = 0f;
					break;
					case TextAnchor.LowerRight:
					case TextAnchor.UpperRight:
					case TextAnchor.MiddleRight:
					_textElement.style.left = Mathf.Min(0f, size.x - _rectTransform.sizeDelta.x);
					break;
					case TextAnchor.LowerCenter:
					case TextAnchor.UpperCenter:
					case TextAnchor.MiddleCenter:
					_textElement.style.left = (size.x - _rectTransform.sizeDelta.x) / 2f;
					break;
				}
				switch (_textAnchor)
				{
					case TextAnchor.LowerLeft:
					case TextAnchor.LowerRight:
					case TextAnchor.LowerCenter:
					_textElement.style.top = Mathf.Max(0f, (size.y - _rectTransform.sizeDelta.y) / 1f);
					break;
					case TextAnchor.MiddleLeft:
					case TextAnchor.MiddleRight:
					case TextAnchor.MiddleCenter:
					_textElement.style.top = Mathf.Max(0f, (size.y - _rectTransform.sizeDelta.y) / 2f);
					break;
					case TextAnchor.UpperLeft:
					case TextAnchor.UpperRight:
					case TextAnchor.UpperCenter:
					_textElement.style.top = 0f;
					break;
				}
				
			}

			_textElement.style.left = _textElement.style.left.value.value + _edgePadding;
		}

		protected override void OnEnable()
		{
			if (_material == null)
			{
				Shader shader = Shader.Find(DisplayShaderPath);
				if (shader)
				{
					_material = new Material(shader);
				}
			}
			CreateDocument();
			if (_textElement != null)
			{
				CreateTexture();
			}
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			ObjectHelper.Destroy(ref _material);
			ReleaseTexture();
			RemoveComponents();
			ForceUpdate();
			base.OnDisable();
		}

		private void RemoveComponents()
		{
			_textElement = null;
			if (_panelSettings != null)
			{
				_panelSettings.themeStyleSheet = null;
				ObjectHelper.Destroy(ref _panelSettings);
			}
			// NOTE: Our demo uses a prefab which can't destroy GameObjects, hency the #if !UNITY_EDITOR below
			if (_document != null)
			{
				_document.panelSettings = null;
				#if !UNITY_EDITOR
				ObjectHelper.Destroy(ref _document);
				#endif
			}
			if (_gameObject)
			{
				#if !UNITY_EDITOR
				ObjectHelper.Destroy(ref _gameObject);
				#endif
			}
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
					ApplyTextSettings();
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

			_fontSize = Mathf.Clamp(_fontSize, 0f, 1024f);
			_outlineWidth = Mathf.Clamp(_outlineWidth, 0f, 128f);
			_shadowOffset.x = Mathf.Clamp(_shadowOffset.x, -128f, 128f);
			_shadowOffset.y = Mathf.Clamp(_shadowOffset.y, -128f, 128f);
			_shadowBlurRadius = Mathf.Clamp(_shadowBlurRadius, 0f, 128f);
			_letterSpacing = Mathf.Clamp(_letterSpacing, -1024f, 1024f);
			_wordSpacing = Mathf.Clamp(_wordSpacing, -1024f, 1024f);
			_paragraphSpacing = Mathf.Clamp(_paragraphSpacing, -1024f, 1024f);
			_edgePadding = Mathf.Clamp(_edgePadding, 0, 128);

			// NOTE: Have to ForceUpdate() otherwise the Game View sometimes doesn't update the rendering, even though the Scene View does...
			ForceUpdate();
		}
		#endif

		protected virtual void Update()
		{
			#if UNITY_EDITOR
			// NOTE: Since we started using [ExecuteAlways] if you have a prefab open in the editor (in Scene view) and then enter Play mode, then
			// exit play mode - Unity will DESTROY all the Object references (materials etc) in the prefab GameObjects, but it will do so
			// WITHOUT calling OnDisable()/OnDestroy() and will not call OnEnable(), so here we detect this case and manually call OnEnable().
			{
				if (this.isActiveAndEnabled && !Application.IsPlaying(this.gameObject))
				{
					if (_material == null)
					{
						OnDisable();
						OnEnable();
						return;
					}
				}
			}
			#endif
			
			if (CreateTexture())
			{
				ForceUpdate();
			}
			// Note: We have to force a second update in case there are UIFX filters attached, as due to 
			// the update order, the filters are rendered BEFORE UITK rendering, so they need
			// to be forced to render again.
			else if (_lastFrameUpdated + 1 == Time.frameCount)
			{
				_lastFrameForcedUpdate = Time.frameCount;
				ForceUpdate();
			}
		}

		bool CreateTexture()
		{
			bool resultContentChanged = false;

			// Build target texture properties
			int targetWidth = 0;
			int targetHeight = 0;
			if (_textElement != null)
			{
				Vector2 size = _rectTransform.sizeDelta;
				if (!IsTextWrap())
				{
					size = _textElement.MeasureTextSize(_textElement.text, size.x, VisualElement.MeasureMode.Undefined, size.y, VisualElement.MeasureMode.Undefined);
				}
				if (float.IsNaN(size.x)) { return false; }
				
				targetWidth = Mathf.CeilToInt(size.x + _edgePadding * 2);
				targetHeight = Mathf.CeilToInt(size.y);
			}

			// Destroy existing texture if not suitable
			if (_renderTexture)
			{
				if (_renderTexture.width != targetWidth || _renderTexture.height != targetHeight)
				{
					ReleaseTexture();
					resultContentChanged = true;
				}
			}

			// Create new texture
			if (_renderTexture == null && targetWidth > 0 && targetHeight > 0)
			{
				_renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
				_renderTexture.name = "UITK Text";

				if (_panelSettings != null)
				{
					_panelSettings.targetTexture = _renderTexture;
					_panelSettings.clearColor = true;
					_panelSettings.referenceResolution = new Vector2Int(targetWidth, targetHeight);
				}

				{
					//_rectTransform.anchorMax = rectTransform.anchorMin;
					//_rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
				}

				// Clear texture
				/*{
					var prevRT = RenderTexture.active;
					RenderTexture.active = _renderTexture;
					//GL.Clear(false, true, Color.clear);
					RenderTexture.active = prevRT;
				}*/
				resultContentChanged = true;
			}

			return resultContentChanged;
		}

		void ReleaseTexture()
		{
			if (_renderTexture)
			{
				if (_panelSettings)
				{
					_panelSettings.targetTexture = null;
					_panelSettings.clearColor = false;
				}
				RenderTextureHelper.ReleaseTemporary(ref _renderTexture);
			}
		}

		private int _lastFrameUpdated = -1;
		private int _lastFrameForcedUpdate = -1;

		public override Material GetModifiedMaterial(Material baseMaterial)
		{
			if (_lastFrameForcedUpdate != Time.frameCount)
			{
				_lastFrameUpdated = Time.frameCount;
			}

			// Note: We have to render using a custom shader, because the default UI
			// does an extra multiply to convert to premultiplied-alpha, but it's already premultiplied, so this produces
			// a nasty outline, so we have to use custom shader.
			if (_material)
			{
				return _material;
			}
			return baseMaterial;
		}

		public void ModifyMesh(VertexHelper vh)
		{
			if (_renderTexture == null)
			{
				vh.Clear();
				return;
			}

			UIVertex v0 = new UIVertex();
			UIVertex v1 = new UIVertex();
			UIVertex v2 = new UIVertex();
			UIVertex v3 = new UIVertex();
			vh.PopulateUIVertex(ref v0, 0); // bottom-left
			vh.PopulateUIVertex(ref v1, 1); // top-left
			vh.PopulateUIVertex(ref v2, 2); // top-right
			vh.PopulateUIVertex(ref v3, 3); // bottom-right

			if (!IsTextWrap())
			{
				float xt = 0f;
				Vector3 uiAnchor = Vector3.zero;
				switch (_textAnchor)
				{
					case TextAnchor.LowerLeft:
					xt = 0f;
					uiAnchor = v0.position;
					break;
					case TextAnchor.UpperLeft:
					xt = 0f;
					uiAnchor = v1.position;
					break;
					case TextAnchor.UpperRight:
					xt = 1f;
					uiAnchor = v2.position;
					break;
					case TextAnchor.LowerRight:
					xt = 1f;
					uiAnchor = v3.position;
					break;
					case TextAnchor.MiddleLeft:
					xt = 0f;
					uiAnchor = (v0.position + v1.position) / 2f;
					break;
					case TextAnchor.MiddleRight:
					xt = 1f;
					uiAnchor = (v2.position + v3.position) / 2f;
					break;
					case TextAnchor.LowerCenter:
					xt = 0.5f;
					uiAnchor = (v0.position + v3.position) / 2f;
					break;
					case TextAnchor.UpperCenter:
					xt = 0.5f;
					uiAnchor = (v1.position + v2.position) / 2f;
					break;
					case TextAnchor.MiddleCenter:
					xt = 0.5f;
					uiAnchor = (v0.position + v1.position + v2.position + v3.position) / 4f;
					break;
				}

				float width = (v2.position.x - v1.position.x);
				float height = (v1.position.y - v0.position.y);
				float ratioW = _renderTexture.width / _rectTransform.sizeDelta.x;
				float ratioH = _renderTexture.height / _rectTransform.sizeDelta.y;
				float newWidth = width * ratioW;
				float newHeight = height * ratioH;
				Vector3 scale = new Vector3(ratioW, ratioH, 1f);

				v0.position -= uiAnchor;
				v1.position -= uiAnchor;
				v2.position -= uiAnchor;
				v3.position -= uiAnchor;

				v0.position.Scale(scale);
				v1.position.Scale(scale);
				v2.position.Scale(scale);
				v3.position.Scale(scale);

				v0.position += uiAnchor;
				v1.position += uiAnchor;
				v2.position += uiAnchor;
				v3.position += uiAnchor;

				if (_edgePadding > 0f)
				{
					Vector3 paddingOffset = new Vector3(Mathf.Lerp(-_edgePadding, _edgePadding, xt), 0f, 0f);
					v0.position += paddingOffset;
					v1.position += paddingOffset;
					v2.position += paddingOffset;
					v3.position += paddingOffset;
				}
			}
			else
			{
				if (_edgePadding > 0f)
				{
					Vector3 paddingOffset = new Vector3(_edgePadding, 0f, 0f);
					v0.position += -paddingOffset;
					v1.position += -paddingOffset;
					v2.position += paddingOffset;
					v3.position += paddingOffset;
				}
			}

			vh.SetUIVertex(v0, 0);
			vh.SetUIVertex(v1, 1);
			vh.SetUIVertex(v2, 2);
			vh.SetUIVertex(v3, 3);
		}

#else

		public void ModifyMesh(VertexHelper vh)
		{
		}
	
#endif

		public void ModifyMesh(Mesh mesh)
		{
		}
	}
}