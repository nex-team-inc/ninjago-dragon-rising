//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(BlurFilter), true)]
	[CanEditMultipleObjects]
	internal class BlurFilterEditor : FilterBaseEditor
	{
		internal static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Blur Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-blur-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/blur-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/blur-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/blur-filter/components/blur-filter/"),
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

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_FadeCurve = new GUIContent("Fade Curve");
		private static readonly GUIContent Content_Axes = new GUIContent("Axes");
		private static readonly GUIContent Content_Blur = new GUIContent("Blur");
		private static readonly GUIContent Content_Falloff = new GUIContent("Falloff");
		private static readonly GUIContent Content_Global = new GUIContent("Global");

		private SerializedProperty _propBlurAlgorithm;
		private SerializedProperty _propBlurAxes2D;
		private SerializedProperty _propDownsample;
		private SerializedProperty _propBlur;
		private SerializedProperty _propTentFalloff;
		private SerializedProperty _propApplyAlphaCurve;
		private SerializedProperty _propAlphaCurve;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;
		//protected SerializedProperty _propIncludeChildren;

		protected virtual void OnEnable()
		{
			_propBlurAlgorithm = VerifyFindProperty("_algorithm");
			_propBlurAxes2D = VerifyFindProperty("_blurAxes2D");
			_propDownsample = VerifyFindProperty("_downSample");
			_propBlur = VerifyFindProperty("_blur");
			_propTentFalloff = VerifyFindProperty("_tentFalloff");
			_propApplyAlphaCurve = VerifyFindProperty("_applyAlphaCurve");
			_propAlphaCurve = VerifyFindProperty("_alphaCurve");
			_propStrength = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propExpand = VerifyFindProperty("_expand");
			//_propIncludeChildren = VerifyFindProperty("_includeChildren");
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
			EditorGUILayout.PropertyField(_propBlurAlgorithm);
			EnumAsToolbar(_propDownsample);
			EnumAsToolbar(_propBlurAxes2D, Content_Axes);
			EditorGUILayout.PropertyField(_propBlur);
			if (_propBlurAlgorithm.intValue == (int)BlurAlgorithm.Tent)
			{
				EditorGUILayout.PropertyField(_propTentFalloff, Content_Falloff);
			}

			//GUI.enabled = false;
			//EditorGUILayout.PropertyField(_propIncludeChildren);
			//GUI.enabled = true;
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propApplyAlphaCurve, Content_FadeCurve);
			if (_propApplyAlphaCurve.boolValue)
			{
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
			}
			EnumAsToolbarCompact(_propRenderSpace);
			EnumAsToolbarCompact(_propExpand);
			DrawStrengthProperty(_propStrength);
			EditorGUI.indentLevel--;

			EditorGUILayout.Space();
			EditorGUILayout.Space();
			GUILayout.Label(Content_Global, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			if (DrawStrengthProperty(ref BlurFilter.GlobalStrength))
			{
				#if UNITY_2022_3_OR_NEWER
				BlurFilter[] blurs = Object.FindObjectsByType<BlurFilter>(FindObjectsSortMode.None);
				#else
				BlurFilter[] blurs = Object.FindObjectsOfType<BlurFilter>();
				#endif
				foreach (var blur in blurs)
				{
					EditorUtility.SetDirty(blur);
				}
			}
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