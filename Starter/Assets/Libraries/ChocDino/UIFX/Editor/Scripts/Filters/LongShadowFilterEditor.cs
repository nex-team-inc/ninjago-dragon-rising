//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(LongShadowFilter), true)]
	[CanEditMultipleObjects]
	internal class LongShadowFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Long Shadow Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-long-shadow-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/extrude-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/extrude-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/extrude-filter/components/long-shadow-filter/"),
								new AboutButton("API Reference", "https://www.chocdino.com/products/uifx/extrude-filter/API/ChocDino.UIFX/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX - Extrude Filter</b>", "https://assetstore.unity.com/packages/slug/276742?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", "https://discussions.unity.com/t/released-uifx-long-shadow-filter/941048"),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_LongShadow = new GUIContent("Long Shadow");
		private static readonly GUIContent Content_Distance = new GUIContent("Distance");

		private SerializedProperty _propMethod;
		private SerializedProperty _propAngle;
		private SerializedProperty _propDistance;
		private SerializedProperty _propStepSize;
		private SerializedProperty _propColor1;
		private SerializedProperty _propUseBackColor;
		private SerializedProperty _propColor2;
		private SerializedProperty _propPivot;
		private SerializedProperty _propSourceAlpha;
		private SerializedProperty _propCompositeMode;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;

		void OnEnable()
		{
			_propMethod = VerifyFindProperty("_method");
			_propSourceAlpha = VerifyFindProperty("_sourceAlpha");
			_propAngle = VerifyFindProperty("_angle");
			_propDistance = VerifyFindProperty("_distance");
			_propStepSize = VerifyFindProperty("_stepSize");
			_propColor1 = VerifyFindProperty("_colorFront");
			_propUseBackColor = VerifyFindProperty("_useBackColor");
			_propColor2 = VerifyFindProperty("_colorBack");
			_propPivot = VerifyFindProperty("_pivot");
			_propCompositeMode = VerifyFindProperty("_compositeMode");
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

			GUILayout.Label(Content_Distance, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbar(_propMethod);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_LongShadow, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;

			EditorGUILayout.PropertyField(_propAngle);
			EditorGUILayout.PropertyField(_propDistance);
			if (_propMethod.enumValueIndex == (int)LongShadowMethod.Normal)
			{
				EditorGUILayout.PropertyField(_propStepSize);
			}

			EditorGUILayout.PropertyField(_propColor1);
			EditorGUILayout.PropertyField(_propUseBackColor);
			if (_propUseBackColor.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propColor2);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.PropertyField(_propPivot);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propSourceAlpha);
			EnumAsToolbar(_propCompositeMode);
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