//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(ColorAdjustFilter), true)]
	[CanEditMultipleObjects]
	internal class ColorAdjustFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Color Adjust Filter\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/color-adjust-filter/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								//new AboutButton("Review <b>UIFX - Color Adjust Filter</b>", "https://assetstore.unity.com/packages/slug/266945?aid=1100lSvNe#reviews"),
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

		private static readonly GUIContent Content_ShowAdvancedOptions = new GUIContent("Show Advanced Options");
		private static readonly GUIContent Content_Adjust = new GUIContent("Adjust");
		private static readonly GUIContent Content_Advanced = new GUIContent("Advanced");
		private static readonly GUIContent Content_Brightness = new GUIContent("Brightness");
		private static readonly GUIContent Content_Contrast = new GUIContent("Contrast");
		private static readonly GUIContent Content_Posterize = new GUIContent("Posterize");
		private static readonly GUIContent Content_Red = new GUIContent("Red");
		private static readonly GUIContent Content_Green = new GUIContent("Green");
		private static readonly GUIContent Content_Blue = new GUIContent("Blue");
		private static readonly GUIContent Content_Alpha = new GUIContent("Alpha");

		private const string PrefKey_ShowAdvancedOptions = "ChocDino.ColorAdjustFilter.ShowAdvancedOptions";

		private SerializedProperty _propHue;
		private SerializedProperty _propSaturation;
		private SerializedProperty _propValue;
		private SerializedProperty _propBrightness;
		private SerializedProperty _propContrast;
		private SerializedProperty _propPosterize;
		private SerializedProperty _propOpacity;
		private SerializedProperty[] _propBrightnessRGBA;
		private SerializedProperty[] _propContrastRGBA;
		private SerializedProperty[] _propPosterizeRGBA;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propStrength;

		private bool _showAdvancedOptions = false;

		protected SerializedProperty[] VerifyFindVector4Property(string fieldName)
		{
			SerializedProperty[] result = new SerializedProperty[4];
			result[0] = VerifyFindProperty(fieldName + ".x");
			result[1] = VerifyFindProperty(fieldName + ".y");
			result[2] = VerifyFindProperty(fieldName + ".z");
			result[3] = VerifyFindProperty(fieldName + ".w");
			return result;
		}

		protected static void PropertyReset_Vector4AsRGBA(SerializedProperty[] prop, GUIContent label, float min, float max, float resetValue)
		{
			EditorGUILayout.PrefixLabel(label);
			EditorGUI.indentLevel++;
			PropertyReset_Slider(prop[0], Content_Red, min, max, resetValue);
			PropertyReset_Slider(prop[1], Content_Green, min, max, resetValue);
			PropertyReset_Slider(prop[2], Content_Blue, min, max, resetValue);
			PropertyReset_Slider(prop[3], Content_Alpha, min, max, resetValue);
			EditorGUI.indentLevel--;
		}

		void OnEnable()
		{
			_propHue = VerifyFindProperty("_hue");
			_propSaturation = VerifyFindProperty("_saturation");
			_propValue = VerifyFindProperty("_value");
			_propBrightness = VerifyFindProperty("_brightness");
			_propContrast = VerifyFindProperty("_contrast");
			_propPosterize = VerifyFindProperty("_posterize");
			_propOpacity = VerifyFindProperty("_opacity");

			_propBrightnessRGBA = VerifyFindVector4Property("_brightnessRGBA");
			_propContrastRGBA = VerifyFindVector4Property("_contrastRGBA");
			_propPosterizeRGBA = VerifyFindVector4Property("_posterizeRGBA");

			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propStrength  = VerifyFindProperty("_strength");

			_showAdvancedOptions = EditorPrefs.GetBool(PrefKey_ShowAdvancedOptions, false);
		}

		void OnDisable()
		{
			EditorPrefs.SetBool(PrefKey_ShowAdvancedOptions, _showAdvancedOptions);
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

			GUILayout.Label(Content_Adjust, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;

			PropertyReset_Float(_propHue, 0f);
			PropertyReset_Float(_propSaturation, 0f);
			PropertyReset_Float(_propValue, 0f);
			PropertyReset_Float(_propContrast, 0f);
			PropertyReset_Float(_propBrightness, 0f);
			PropertyReset_Float(_propPosterize, 255f);
			PropertyReset_Float(_propOpacity, 1f);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			_showAdvancedOptions = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvancedOptions, Content_Advanced);
			if (_showAdvancedOptions)
			{
				EditorGUI.indentLevel++;
				PropertyReset_Vector4AsRGBA(_propBrightnessRGBA, Content_Brightness, -1f, 1f, 0f);
				PropertyReset_Vector4AsRGBA(_propContrastRGBA, Content_Contrast, -2f, 2f, 0f);
				PropertyReset_Vector4AsRGBA(_propPosterizeRGBA, Content_Posterize, 1f, 255f, 255f);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
			EditorGUILayout.EndFoldoutHeaderGroup();

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbarCompact(_propRenderSpace);
			DrawStrengthProperty(_propStrength);
			EditorGUI.indentLevel--;

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(filter);

			serializedObject.ApplyModifiedProperties();
		}
	}
}