//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(UIToolkitTextSource))]
	[CanEditMultipleObjects]
	internal class UIToolkitTextSourceEditor : BaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - UIToolkit Text Source\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/uitoolkit-text-source/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", ForumBundleUrl),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

#if UIFX_UITK && UNITY_2021_2_OR_NEWER

		private static readonly GUIContent Content_Text = new GUIContent("Text");
		private static readonly GUIContent Content_Font = new GUIContent("Font");
		private static readonly GUIContent Content_Styling = new GUIContent("Styling");
		private static readonly GUIContent Content_Layout = new GUIContent("Layout");
		private static readonly GUIContent Content_Color = new GUIContent("Color");
		private static readonly GUIContent Content_Width = new GUIContent("Width");
		private static readonly GUIContent Content_Offset = new GUIContent("Offset");
		private static readonly GUIContent Content_BlurRadius = new GUIContent("Blur Radius");
		private static readonly GUIContent Content_GeneratorMode = new GUIContent("Generator Mode");
	
		private SerializedProperty _propText;
		private SerializedProperty _propFont;
		private SerializedProperty _propFontAsset;
		private SerializedProperty _propFontSize;
		private SerializedProperty _propColor;
		private SerializedProperty _propTextAnchor;
		private SerializedProperty _propWrap;
		private SerializedProperty _propOverflow;
		private SerializedProperty _propStyle;
		private SerializedProperty _propOutline;
		private SerializedProperty _propOutlineWidth;
		private SerializedProperty _propOutlineColor;
		private SerializedProperty _propShadow;
		private SerializedProperty _propShadowColor;
		private SerializedProperty _propShadowOffset;
		private SerializedProperty _propShadowBlurRadius;
		private SerializedProperty _propLetterSpacing;
		private SerializedProperty _propWordSpacing;
		private SerializedProperty _propParagraphSpacing;
		private SerializedProperty _propEdgePadding;
		#if UNITY_6000_0_OR_NEWER
		private SerializedProperty _propTextGeneratorType;
		#endif
		private SerializedProperty _propStyleSheet;

		private SerializedProperty _propRaycastTarget;
		//private SerializedProperty _propRaycastPadding;
		private SerializedProperty _propMaskable;

		void OnEnable()
		{
			_propText = VerifyFindProperty("_text");
			_propFont = VerifyFindProperty("_font");
			_propFontAsset = VerifyFindProperty("_fontAsset");
			_propFontSize = VerifyFindProperty("_fontSize");
			_propColor = VerifyFindProperty("_color");
			_propTextAnchor = VerifyFindProperty("_textAnchor");
			_propWrap = VerifyFindProperty("_wrap");
			_propOverflow = VerifyFindProperty("_overflow");
			_propStyle = VerifyFindProperty("_style");
			_propOutline = VerifyFindProperty("_outline");
			_propOutlineWidth = VerifyFindProperty("_outlineWidth");
			_propOutlineColor = VerifyFindProperty("_outlineColor"); 
			_propShadow = VerifyFindProperty("_shadow");
			_propShadowColor = VerifyFindProperty("_shadowColor"); 
			_propShadowOffset = VerifyFindProperty("_shadowOffset"); 
			_propShadowBlurRadius = VerifyFindProperty("_shadowBlurRadius"); 
			_propLetterSpacing = VerifyFindProperty("_letterSpacing");
			_propWordSpacing = VerifyFindProperty("_wordSpacing");
			_propParagraphSpacing = VerifyFindProperty("_paragraphSpacing");
			_propEdgePadding = VerifyFindProperty("_edgePadding");
			#if UNITY_6000_0_OR_NEWER
			_propTextGeneratorType = VerifyFindProperty("_textGeneratorType");
			#endif
			_propStyleSheet = VerifyFindProperty("_styleSheet");

			_propRaycastTarget = VerifyFindProperty("m_RaycastTarget");
			//_propRaycastPadding = VerifyFindProperty("m_RaycastPadding");
			_propMaskable = VerifyFindProperty("m_Maskable");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			{
				GUILayout.Label(Content_Text, EditorStyles.boldLabel);
				EditorGUI.BeginChangeCheck();
				string newText = EditorGUILayout.TextArea(_propText.stringValue, GUILayout.Height(64f));
				// Only assign the value back if it was actually changed by the user.
				// Otherwise a single value will be assigned to all objects when multi-object editing,
				// even when the user didn't touch the control.
				if (EditorGUI.EndChangeCheck())
				{
					_propText.stringValue = newText;
				}
			}

			EditorGUILayout.Space();
			GUILayout.Label(Content_Font, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			#if UNITY_6000_0_OR_NEWER
			EnumAsToolbar(_propTextGeneratorType, Content_GeneratorMode);
			#endif
			EditorGUILayout.PropertyField(_propFont);
			EditorGUILayout.PropertyField(_propFontAsset);
			EditorGUILayout.PropertyField(_propFontSize);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			GUILayout.Label(Content_Styling, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propStyle);
			EditorGUILayout.PropertyField(_propColor);
			EditorGUILayout.PropertyField(_propOutline);
			EditorGUI.BeginDisabledGroup(!_propOutline.boolValue);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propOutlineWidth, Content_Width);
			EditorGUILayout.PropertyField(_propOutlineColor, Content_Color);
			EditorGUI.indentLevel--;
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.PropertyField(_propShadow);
			EditorGUI.BeginDisabledGroup(!_propShadow.boolValue);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propShadowColor, Content_Color);
			EditorGUILayout.PropertyField(_propShadowOffset, Content_Offset);
			EditorGUILayout.PropertyField(_propShadowBlurRadius, Content_BlurRadius);
			EditorGUI.indentLevel--;
			EditorGUI.EndDisabledGroup();
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			GUILayout.Label(Content_Layout, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propTextAnchor);
			EditorGUILayout.PropertyField(_propWrap);
			EnumAsToolbar(_propOverflow);
			EditorGUILayout.PropertyField(_propLetterSpacing);
			EditorGUILayout.PropertyField(_propWordSpacing);
			EditorGUILayout.PropertyField(_propParagraphSpacing);
			EditorGUILayout.PropertyField(_propEdgePadding);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_propStyleSheet);
			EditorGUILayout.PropertyField(_propRaycastTarget);
			if (_propRaycastTarget.boolValue)
			{
			//	EditorGUILayout.PropertyField(_propRaycastPadding);
			}
			EditorGUILayout.PropertyField(_propMaskable);

			serializedObject.ApplyModifiedProperties();
		}

#else

		public override void OnInspectorGUI()
		{
			#if UNITY_2021_2_OR_NEWER
			EditorGUILayout.HelpBox("This component requires enabling UI Toolkit support in the UIFX Settings.", MessageType.Error, true);
			if (GUILayout.Button("Open UIFX Settings"))
			{
				SettingsService.OpenProjectSettings(Preferences.SettingsPath);
			}
			#else
			EditorGUILayout.HelpBox("This component requires Unity 2021.2 and above for UI Toolkit support.", MessageType.Error, true);
			#endif
		}

#endif
	}
}