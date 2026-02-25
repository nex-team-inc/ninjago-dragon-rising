//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(FillGradientFilter), true)]
	[CanEditMultipleObjects]
	internal class FillGradientFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Fill Gradient Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-fill-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/fill-gradient-filter/"),
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
		private static readonly GUIContent Content_Radius = new GUIContent("Radius");
		private static readonly GUIContent Content_Scale = new GUIContent("Repeat");
		private static readonly GUIContent Content_ScaleCenter = new GUIContent("Repeat Pivot");
		private static readonly GUIContent Content_CenterX = new GUIContent("Center X");
		private static readonly GUIContent Content_CenterY = new GUIContent("Center Y");
		private static readonly GUIContent Content_Offset = new GUIContent("Offset");
		private static readonly GUIContent Content_Angle = new GUIContent("Angle");
		private static readonly GUIContent Content_Wrap = new GUIContent("Wrap");
		private static readonly GUIContent Content_Shape = new GUIContent("Shape");
		private static readonly GUIContent Content_Dither = new GUIContent("Dither");
		private static readonly GUIContent Content_Transform = new GUIContent("Transform");
		private static readonly GUIContent Content_ColorLerp = new GUIContent("Color Lerp");
		private static readonly GUIContent Content_ColorSpace = new GUIContent("ColorSpace");
		private static readonly GUIContent Content_BlendGamma = new GUIContent("Blend Gamma");

		private static readonly string s_warnRequiresScreenRenderSpace = "FillSpace of Screen requires RenderSpace also set to Screen.";

		private SerializedProperty _propGradient;
		private SerializedProperty _propGradientShape;
		private SerializedProperty _propDiagonalFlip;
		private SerializedProperty _propGradientCenterX;
		private SerializedProperty _propGradientCenterY;
		private SerializedProperty _propGradientRadius;
		private SerializedProperty _propUseSquareAspectRatio;
		private SerializedProperty _propGradientLinearAngle;
		private SerializedProperty _propFillSpace;
		private SerializedProperty _propGradientScale;
		private SerializedProperty _propGradientScaleCenter;
		private SerializedProperty _propGradientOffset;
		private SerializedProperty _propScrollSpeed;
		private SerializedProperty _propGradientWrap;
		private SerializedProperty _propGradientColorSpace;
		private SerializedProperty _propGradientLerp;
		private SerializedProperty _propGradientDither;
		private SerializedProperty _propBlendMode;
		private SerializedProperty _propBlendModeForceGammaSpace;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;

		public override bool RequiresConstantRepaint()
		{
			var fill = this.target as FillGradientFilter;
			if (!fill.isActiveAndEnabled) return false;
			if (fill.Strength <= 0f) return false;
			if (fill.IsPreviewScroll && fill.HasScrollSpeed())
			{
				EditorApplication.QueuePlayerLoopUpdate();
				return true;
			}
			return false;
		}

		void OnDisable()
		{
			foreach (var obj in this.targets)
			{
				var fill = obj as FillGradientFilter;
				// NOTE: fill can be null if it has been destroyed
				if (fill != null)
				{
					fill.IsPreviewScroll = false;
					fill.ResetScroll();
				}
			}
		}

		void OnEnable()
		{
			_propGradient = VerifyFindProperty("_gradient");
			_propGradientShape = VerifyFindProperty("_gradientShape");
			_propDiagonalFlip = VerifyFindProperty("_diagonalFlip");
			_propGradientCenterX = VerifyFindProperty("_gradientCenterX");
			_propGradientCenterY = VerifyFindProperty("_gradientCenterY");
			_propGradientRadius = VerifyFindProperty("_gradientRadius");
			_propUseSquareAspectRatio = VerifyFindProperty("_useSquareAspectRatio");
			_propFillSpace = VerifyFindProperty("_fillSpace");
			_propGradientScale = VerifyFindProperty("_gradientScale");
			_propGradientScaleCenter = VerifyFindProperty("_gradientScaleCenter");
			_propGradientOffset = VerifyFindProperty("_gradientOffset");
			_propScrollSpeed = VerifyFindProperty("_scrollSpeed");
			_propGradientLinearAngle = VerifyFindProperty("_gradientLinearAngle");
			_propGradientWrap = VerifyFindProperty("_gradientWrap");
			_propGradientLerp = VerifyFindProperty("_gradientLerp");
			_propGradientColorSpace = VerifyFindProperty("_gradientColorSpace");
			_propGradientDither = VerifyFindProperty("_gradientDither");
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
			var fill = this.target as FillGradientFilter;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}

			{
				GUILayout.Label(Content_Shape, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propGradientShape, Content_Shape);
				if ((GradientShape)_propGradientShape.enumValueIndex == GradientShape.Diagonal)
				{
					EditorGUILayout.PropertyField(_propDiagonalFlip);
				}
				if ((GradientShape)_propGradientShape.enumValueIndex == GradientShape.Linear)
				{
					EditorGUILayout.PropertyField(_propGradientLinearAngle, Content_Angle);
				}
				else if ((GradientShape)_propGradientShape.enumValueIndex == GradientShape.Radial ||
						(GradientShape)_propGradientShape.enumValueIndex == GradientShape.Conic)
				{
					EditorGUILayout.PropertyField(_propGradientCenterX, Content_CenterX);
					EditorGUILayout.PropertyField(_propGradientCenterY, Content_CenterY);
				}
				if ((GradientShape)_propGradientShape.enumValueIndex == GradientShape.Radial)
				{
					EditorGUILayout.PropertyField(_propGradientRadius, Content_Radius);
					EditorGUILayout.PropertyField(_propUseSquareAspectRatio);
				}
				EditorGUI.indentLevel--;
			}

			{
				GUILayout.Label(Content_Gradient, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propGradient);
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel(Content_SpaceCharacter);
				if (GUILayout.Button(Content_Reverse))
				{
					var g = GetGradient(_propGradient);
					GradientUtils.Reverse(g);
					SetGradient(_propGradient, g);
				}
				EditorGUILayout.EndHorizontal();
				EnumAsToolbar(_propGradientLerp, Content_ColorLerp);
				EditorGUI.BeginDisabledGroup((GradientLerp)_propGradientLerp.enumValueIndex == GradientLerp.Step);
				EnumAsToolbar(_propGradientColorSpace, Content_ColorSpace);
				EditorGUI.EndDisabledGroup();
				EnumAsToolbar(_propGradientWrap, Content_Wrap);
				EditorGUILayout.PropertyField(_propGradientDither, Content_Dither);
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
				GUILayout.Label(Content_Transform, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propGradientScale, Content_Scale);
				EditorGUILayout.PropertyField(_propGradientScaleCenter, Content_ScaleCenter);
				EditorGUILayout.PropertyField(_propGradientOffset, Content_Offset);
				{
					GUILayout.BeginHorizontal();
					EditorGUILayout.PropertyField(_propScrollSpeed);
					if (!EditorApplication.isPlaying)
					{
						if (ToggleButton(fill.IsPreviewScroll, Content_Stop, Content_Preview))
						{
							if (fill.IsPreviewScroll)
							{
								foreach (var obj in this.targets)
								{
									var fillInstance = obj as FillGradientFilter;
									fillInstance.IsPreviewScroll = false;
									fillInstance.ResetScroll();
								}
							}
							else
							{
								foreach (var obj in this.targets)
								{
									var fillInstance = obj as FillGradientFilter;
									fillInstance.IsPreviewScroll = true;
								}
							}
							EditorApplication.QueuePlayerLoopUpdate();
						}
					}
					GUILayout.EndHorizontal();
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