//--------------------------------------------------------------------------//
// Copyright 2023 Chocolate Dinosaur Ltd. All rights reserved.              //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(FillColorFilter), true)]
	[CanEditMultipleObjects]
	internal class FillColorFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Fill Color Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-fill-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/fill-color-filter/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								//new AboutButton("Review <b>UIFX - Fill Filter</b>", "https://assetstore.unity.com/packages/slug/274847?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", "https://assetstore.unity.com/packages/slug/266945?aid=1100lSvNe#reviews"),
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

		private static readonly GUIContent Content_Area = new GUIContent("Area");
		private static readonly GUIContent Content_Space = new GUIContent("Space");
		private static readonly GUIContent Content_BlendGamma = new GUIContent("Blend Gamma");

		private static readonly string s_warnRequiresScreenRenderSpace = "FillSpace of Screen requires RenderSpace also set to Screen.";

		private SerializedProperty _propMode;
		private SerializedProperty _propColor;
		private SerializedProperty _propColorA;
		private SerializedProperty _propColorB;
		private SerializedProperty _propColorTL;
		private SerializedProperty _propColorTR;
		private SerializedProperty _propColorBL;
		private SerializedProperty _propColorBR;
		private SerializedProperty _propColorScale;
		private SerializedProperty _propColorBias;
		private SerializedProperty _propFillSpace;
		private SerializedProperty _propBlendMode;
		private SerializedProperty _propBlendModeForceGammaSpace;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;

		void OnEnable()
		{
			_propMode = VerifyFindProperty("_mode");
			_propColor = VerifyFindProperty("_color");
			_propColorA = VerifyFindProperty("_colorA");
			_propColorB = VerifyFindProperty("_colorB");
			_propColorTL = VerifyFindProperty("_colorTL");
			_propColorTR = VerifyFindProperty("_colorTR");
			_propColorBL = VerifyFindProperty("_colorBL");
			_propColorBR = VerifyFindProperty("_colorBR");
			_propColorScale = VerifyFindProperty("_colorScale");
			_propColorBias = VerifyFindProperty("_colorBias");
			_propFillSpace = VerifyFindProperty("_fillSpace");
			_propBlendMode = VerifyFindProperty("_blendMode");
			_propBlendModeForceGammaSpace = VerifyFindProperty("_blendModeForceGammaSpace");
			_propStrength  = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			var filter = this.target as FilterBase;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}

			{
				GUILayout.Label(Content_Fill, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbar(_propMode);
				switch ((FillColorMode)_propMode.enumValueIndex)
				{
					case FillColorMode.Solid:
					EditorGUILayout.PropertyField(_propColor);
					break;
					case FillColorMode.Horizontal:
					DrawDualColors(_propColorA, _propColorB, Content_Colors);
					EditorGUILayout.PropertyField(_propColorScale);
					EditorGUILayout.PropertyField(_propColorBias);
					break;
					case FillColorMode.Vertical:
					DrawDualColors(_propColorA, _propColorB, Content_Colors);
					EditorGUILayout.PropertyField(_propColorScale);
					EditorGUILayout.PropertyField(_propColorBias);
					break;
					case FillColorMode.Corners:
					DrawDualColors(_propColorTL, _propColorTR, Content_Colors);
					DrawDualColors(_propColorBL, _propColorBR, Content_SpaceCharacter);
					EditorGUILayout.PropertyField(_propColorScale);
					break;
				}
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Area, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbarCompact(_propFillSpace, Content_Space);
				if (_propRenderSpace.intValue != (int)FilterRenderSpace.Screen && _propFillSpace.intValue == (int)FillSpace.Screen)
				{
					EditorGUILayout.HelpBox(s_warnRequiresScreenRenderSpace, MessageType.Warning, true);
					EditorGUILayout.Space();
				}
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propBlendMode);
				EditorGUI.BeginDisabledGroup(_propBlendMode.intValue < (int)FillGradientBlendMode.Darken);
				EditorGUILayout.PropertyField(_propBlendModeForceGammaSpace, Content_BlendGamma);
				EditorGUI.EndDisabledGroup();
				EnumAsToolbarCompact(_propRenderSpace);
				DrawStrengthProperty(_propStrength);
				EditorGUI.indentLevel--;
			}

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(this.target as FilterBase);

			serializedObject.ApplyModifiedProperties();
		}
	}
}