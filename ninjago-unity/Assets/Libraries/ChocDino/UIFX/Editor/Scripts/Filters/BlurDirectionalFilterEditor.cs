//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(BlurDirectionalFilter), true)]
	[CanEditMultipleObjects]
	internal class BlurDirectionalFilterEditor : FilterBaseEditor
	{
	internal static readonly AboutInfo s_aboutInfo =
				new AboutInfo(s_aboutHelp, "UIFX - Blur Directional Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-blur-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/blur-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/blur-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/blur-filter/components/blur-directional-filter/"),
								new AboutButton("API Reference", "https://www.chocdino.com/products/uifx/blur-filter/API/ChocDino.UIFX/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX - Blur Filter</b>", "https://assetstore.unity.com/packages/slug/268262?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", "https://discussions.unity.com/t/released-uifx-blur-filter/936189"),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};
				
		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, BlurFilterEditor.s_aboutInfo });

		private static readonly GUIContent Content_FadeCurve = new GUIContent("Fade Curve");
		private static readonly GUIContent Content_Blur = new GUIContent("Blur");

		private SerializedProperty _propAngle;
		private SerializedProperty _propLength;
		private SerializedProperty _propSide;
		private SerializedProperty _propWeights;
		private SerializedProperty _propWeightsPower;
		private SerializedProperty _propDither;
		private SerializedProperty _propApplyAlphaCurve;
		private SerializedProperty _propAlphaCurve;
		private SerializedProperty _propTintColor;
		private SerializedProperty _propPower;
		private SerializedProperty _propIntensity;
		private SerializedProperty _propBlend;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;

		protected virtual void OnEnable()
		{
			_propAngle = VerifyFindProperty("_angle");
			_propLength = VerifyFindProperty("_length");
			_propSide = VerifyFindProperty("_side");
			_propWeights = VerifyFindProperty("_weights");
			_propWeightsPower = VerifyFindProperty("_weightsPower");
			_propDither = VerifyFindProperty("_dither");
			_propApplyAlphaCurve = VerifyFindProperty("_applyAlphaCurve");
			_propAlphaCurve = VerifyFindProperty("_alphaCurve");
			_propTintColor = VerifyFindProperty("_tintColor");
			_propPower = VerifyFindProperty("_power");
			_propIntensity = VerifyFindProperty("_intensity");
			_propBlend = VerifyFindProperty("_blend");
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

			GUILayout.Label(Content_Blur, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propAngle);
			EditorGUILayout.PropertyField(_propLength);
			EnumAsToolbar(_propSide);
			EnumAsToolbar(_propWeights);
			if (_propWeights.enumValueIndex == (int)BlurDirectionalWeighting.Falloff)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propWeightsPower);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.PropertyField(_propDither);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propApplyAlphaCurve, Content_FadeCurve);
			if (_propApplyAlphaCurve.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propAlphaCurve);
				// Show a warning if curve key values of out of sensible range.
				// NOTE: We have to detect whether the curve window has focus currently, otherwise if the HelpBox() appears
				// while dragging curve keys, it can cause the keys to not update.
				if (!EditorHelper.IsEditingCurve())
				{
					if (_propAlphaCurve.animationCurveValue != null && _propAlphaCurve.animationCurveValue.HasOutOfRangeValues(0f, 1f, 0f, 1f))
					{
						EditorGUILayout.HelpBox("Some curve points are outside of the range [0..1]. This might be fine, or could lead to unexpected results.", MessageType.Warning, true);
					}
				}
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.PropertyField(_propTintColor);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propPower);
			EditorGUILayout.PropertyField(_propIntensity);
			EditorGUI.indentLevel--;
			EnumAsToolbar(_propBlend);
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