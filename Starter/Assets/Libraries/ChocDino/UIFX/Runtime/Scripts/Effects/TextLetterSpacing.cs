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
	/// <summary>
	/// Adjust letter spacing (kerning) on Text UGUI component
	/// Supports multi-lines, spaces and richtext
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(Text))]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu("UI/Chocolate Dinosaur UIFX/Effects/UIFX - Text Letter Spacing")]
	public class TextLetterSpacing : UIBehaviour, IMeshModifier
	{
		[SerializeField] float _letterSpacing = 0f;
		[SerializeField, Range(0f, 1f)] float _strength = 1f;

		private Text _text;
		private static UIVertex[] s_vt = new UIVertex[4];
		private List<UICharInfo> _charList = new List<UICharInfo>(32);
		private List<UILineInfo> _lineList = new List<UILineInfo>(4);

		protected Text TextComponent
		{
			get	{ if (_text == null) { _text = GetComponent<Text>(); } return _text; }
		}

		public float LetterSpacing
		{
			get { return _letterSpacing; }
			set { if (value != _letterSpacing) { _letterSpacing = value; ForceUpdate(); } }
		}

		public float Strength
		{
			get { return _strength; }
			set { value = Mathf.Clamp01(value); if (value != _strength) { _strength = value; ForceUpdate(); } }
		}

		private void ForceUpdate()
		{
			var textComponent = TextComponent;
			textComponent.SetVerticesDirty();
			textComponent.SetMaterialDirty();
		}

		#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();
			ForceUpdate();
		}
		protected override void OnValidate()
		{
			ForceUpdate();
			base.OnValidate();
		}
		#endif

		protected override void OnDisable()
		{
			ForceUpdate();
			base.OnDisable();
		}

		protected override void OnDidApplyAnimationProperties()
		{
			ForceUpdate();
			base.OnDidApplyAnimationProperties();
		}

		public void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive()) return;

			float spacingFactor = _letterSpacing * _strength;

			if (spacingFactor == 0f) return;
		
			TextComponent.cachedTextGenerator.GetCharacters(_charList);
			TextComponent.cachedTextGenerator.GetLines(_lineList);

			float horizontalAligment = 0.5f;
			switch (TextComponent.alignment)
			{
				case TextAnchor.UpperLeft:
				case TextAnchor.MiddleLeft:
				case TextAnchor.LowerLeft:
				horizontalAligment = 0.0f;
				break;
				case TextAnchor.UpperCenter:
				case TextAnchor.MiddleCenter:
				case TextAnchor.LowerCenter:
				horizontalAligment = 0.5f;
				break;
				case TextAnchor.UpperRight:
				case TextAnchor.MiddleRight:
				case TextAnchor.LowerRight:
				horizontalAligment = 1.0f;
				break;
			}
		
			float spaceSize = spacingFactor * TextComponent.fontSize / 100f;
			string text = TextComponent.text;

			// For each line
			int vertexIdx = 0;
			for (int lineIdx = 0; lineIdx < _lineList.Count; lineIdx++)
			{
				UILineInfo line = _lineList[lineIdx];

				int lineAllCharCount = 0;
				{
					if (lineIdx + 1 < _lineList.Count)
					{
						lineAllCharCount = (_lineList[lineIdx + 1].startCharIdx - line.startCharIdx) - 1;
					}
					else
					{
						lineAllCharCount = (TextComponent.cachedTextGenerator.characterCountVisible - line.startCharIdx);
					}
				}

				int lineVisibleCharCount = 0;
				{
					for (int i = line.startCharIdx; i < (line.startCharIdx + lineAllCharCount); i++)
					{
						char c = text[i];
						bool isNonRichControlChar = (_charList[i].charWidth > 0f);
						if (isNonRichControlChar)
						{
							lineVisibleCharCount++;
						}
					}
				}
				
				int spaceCount = (lineVisibleCharCount - 1);
				float lineOffset = spaceCount * spaceSize * horizontalAligment;
				int visibleCharIndex = 0;

				// For each character in this line
				for (int i = line.startCharIdx; i < (line.startCharIdx + lineAllCharCount); i++)
				{
					char c = text[i];
					bool isGeometryChar = !char.IsWhiteSpace(c);
					bool isNonRichControlChar = (_charList[i].charWidth > 0f);
					// Check if this is a visible character
					if (isNonRichControlChar)
					{
						if (isGeometryChar)
						{
							int charIdx = i - line.startCharIdx;

							Vector3 offset = Vector3.right * (spaceSize * visibleCharIndex - lineOffset);

							// Update the position of each quad vertex
							for (int k = 0 ; k < 4; k++)
							{
								vh.PopulateUIVertex(ref s_vt[k], vertexIdx + k);
								s_vt[k].position += offset;
								vh.SetUIVertex(s_vt[k], vertexIdx + k);
							}

							vertexIdx += 4;
						}
						visibleCharIndex++;
					}
				}
			}
		}

		[UnityInternal.ExcludeFromDocs]
		[System.Obsolete("use IMeshModifier.ModifyMesh (VertexHelper verts) instead, or set useLegacyMeshGeneration to false", false)]
		public void ModifyMesh(Mesh mesh)
		{
			throw new System.NotImplementedException("use IMeshModifier.ModifyMesh (VertexHelper verts) instead, or set useLegacyMeshGeneration to false");
		}
	}
}