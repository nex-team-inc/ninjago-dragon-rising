//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(DropShadowFilter), true)]
	[CanEditMultipleObjects]
	internal class DropShadowFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Drop Shadow Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-drop-shadow-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/drop-shadow-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/drop-shadow-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/drop-shadow-filter/components/drop-shadow-filter/"),
								new AboutButton("API Reference", "https://www.chocdino.com/products/uifx/drop-shadow-filter/API/ChocDino.UIFX/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX - Drop Shadow Filter</b>", "https://assetstore.unity.com/packages/slug/272733?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", "https://discussions.unity.com/t/released-uifx-drop-shadow-filter/940748"),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Shadow = new GUIContent("Shadow");
		private static readonly GUIContent Content_Blur = new GUIContent("Blur");

		private SerializedProperty _propDownsample;
		private SerializedProperty _propBlur;
		private SerializedProperty _propSourceAlpha;
		private SerializedProperty _propHardness;
		private SerializedProperty _propAngle;
		private SerializedProperty _propDistance;
		private SerializedProperty _propSpread;
		private SerializedProperty _propColor;
		private SerializedProperty _propMode;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;

		void OnEnable()
		{
			_propDownsample = VerifyFindProperty("_downSample");
			_propBlur = VerifyFindProperty("_blur");
			_propSourceAlpha = VerifyFindProperty("_sourceAlpha");
			_propHardness = VerifyFindProperty("_hardness");
			_propAngle = VerifyFindProperty("_angle");
			_propDistance = VerifyFindProperty("_distance");
			_propSpread = VerifyFindProperty("_spread");
			_propColor = VerifyFindProperty("_color");
			_propMode = VerifyFindProperty("_mode");
			_propStrength = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propExpand = VerifyFindProperty("_expand");
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

			GUILayout.Label(Content_Shadow, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;

			EnumAsToolbar(_propMode);
			EditorGUILayout.PropertyField(_propAngle);
			EditorGUILayout.PropertyField(_propDistance);
			EditorGUILayout.PropertyField(_propSpread);
			EditorGUILayout.PropertyField(_propColor);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Blur, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbar(_propDownsample);
			EditorGUILayout.PropertyField(_propBlur);
			EditorGUILayout.PropertyField(_propHardness);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propSourceAlpha);
			EnumAsToolbarCompact(_propRenderSpace);
			EnumAsToolbarCompact(_propExpand);
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