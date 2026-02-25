//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(ExtrudeFilter), true)]
	[CanEditMultipleObjects]
	internal class ExtrudeFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Extrude Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-extrude-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/extrude-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/extrude-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/extrude-filter/components/extrude-filter/"),
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
								new AboutButton("Post to Unity Discussions", ForumBundleUrl),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Extrude = new GUIContent("Extrude");
		private static readonly GUIContent Content_Mode = new GUIContent("Mode");
		private static readonly GUIContent Content_BlendMode = new GUIContent("Blend Mode");
		
		private SerializedProperty _propProjection;
		private SerializedProperty _propAngle;
		private SerializedProperty _propDistance;
		private SerializedProperty _propPerspectiveDistance;
		private SerializedProperty _propFillMode;
		private SerializedProperty _propColor1;
		private SerializedProperty _propColor2;
		private SerializedProperty _propGradient;
		private SerializedProperty _propGradientTexture;
		private SerializedProperty _propReverseFill;
		private SerializedProperty _propScrollSpeed;
		private SerializedProperty _propFillBlendMode;
		private SerializedProperty _propSourceAlpha;
		private SerializedProperty _propCompositeMode;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;

		public override bool RequiresConstantRepaint()
		{
			var extrude = this.target as ExtrudeFilter;
			if (!extrude.isActiveAndEnabled) return false;
			if (extrude.Strength <= 0f) return false;
			if (extrude.IsPreviewScroll && extrude.ScrollSpeed != 0f)
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
				var extrude = obj as ExtrudeFilter;
				// NOTE: extrude can be null if it has been destroyed
				if (extrude != null)
				{
					extrude.IsPreviewScroll = false;
					extrude.ResetScroll();
				}
			}
		}

		void OnEnable()
		{
			_propProjection = VerifyFindProperty("_projection");
			_propSourceAlpha = VerifyFindProperty("_sourceAlpha");
			_propAngle = VerifyFindProperty("_angle");
			_propDistance = VerifyFindProperty("_distance");
			_propPerspectiveDistance = VerifyFindProperty("_perspectiveDistance");
			_propColor1 = VerifyFindProperty("_colorFront");
			_propColor2 = VerifyFindProperty("_colorBack");
			_propCompositeMode = VerifyFindProperty("_compositeMode");
			_propFillMode = VerifyFindProperty("_fillMode");
			_propGradient = VerifyFindProperty("_gradient");
			_propGradientTexture = VerifyFindProperty("_gradientTexture");
			_propReverseFill = VerifyFindProperty("_reverseFill");
			_propScrollSpeed = VerifyFindProperty("_scrollSpeed");
			_propFillBlendMode = VerifyFindProperty("_fillBlendMode");
			_propStrength = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propExpand = VerifyFindProperty("_expand");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			var filter = this.target as FilterBase;
			var extrude = this.target as ExtrudeFilter;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}

			{
				GUILayout.Label(Content_Extrude, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbar(_propProjection);
				EditorGUILayout.PropertyField(_propAngle);
				if (_propProjection.enumValueIndex == (int)ExtrudeProjection.Perspective)
				{
					EditorGUILayout.PropertyField(_propPerspectiveDistance);
				}
				EditorGUILayout.PropertyField(_propDistance);
				EditorGUI.indentLevel--;
			}

			{
				GUILayout.Label(Content_Fill, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbar(_propFillMode, Content_Mode);
				EditorGUI.indentLevel++;
				if (_propFillMode.enumValueIndex == (int)ExtrudeFillMode.Color)
				{
					EditorGUILayout.PropertyField(_propColor1, Content_Color);
				}
				else if (_propFillMode.enumValueIndex == (int)ExtrudeFillMode.BiColor)
				{
					GUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel(Content_Colors, EditorStyles.colorField);
					EditorGUI.indentLevel-=2;		// NOTE: This seems to be a bug in Unity where it shows the indentation with ColorFields, so we have to remove it manually...
					EditorGUILayout.PropertyField(_propColor1, GUIContent.none);
					EditorGUILayout.PropertyField(_propColor2, GUIContent.none);
					EditorGUI.indentLevel+=2;
					GUILayout.EndHorizontal();
					
				}
				else if (_propFillMode.enumValueIndex == (int)ExtrudeFillMode.Gradient)
				{
					EditorGUILayout.PropertyField(_propGradient);
				}
				else if (_propFillMode.enumValueIndex == (int)ExtrudeFillMode.Texture)
				{
					EditorGUILayout.PropertyField(_propGradientTexture);
				}
				EditorGUI.indentLevel--;

				if (_propFillMode.enumValueIndex != (int)ExtrudeFillMode.Color)
				{
					EditorGUILayout.PropertyField(_propReverseFill, Content_Reverse);
					GUILayout.BeginHorizontal();
					EditorGUILayout.PropertyField(_propScrollSpeed);
					if (!EditorApplication.isPlaying)
					{
						if (ToggleButton(extrude.IsPreviewScroll, Content_Stop, Content_Preview))
						{
							if (extrude.IsPreviewScroll)
							{
								foreach (var obj in this.targets)
								{
									var extrudeInstance = obj as ExtrudeFilter;
									extrudeInstance.IsPreviewScroll = false;
									extrudeInstance.ResetScroll();
								}
							}
							else
							{
								foreach (var obj in this.targets)
								{
									var extrudeInstance = obj as ExtrudeFilter;
									extrudeInstance.IsPreviewScroll = true;
								}
							}
							EditorApplication.QueuePlayerLoopUpdate();
						}
					}
					GUILayout.EndHorizontal();
				}
				EnumAsToolbar(_propFillBlendMode, Content_BlendMode);
				EditorGUI.indentLevel--;
			}

			{
				GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propSourceAlpha);
				EnumAsToolbar(_propCompositeMode);
				EnumAsToolbarCompact(_propRenderSpace);
				EnumAsToolbarCompact(_propExpand);
				DrawStrengthProperty(_propStrength);
				EditorGUI.indentLevel--;
			}

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(filter);

			serializedObject.ApplyModifiedProperties();
		}

		public void OnSceneGUI()
		{
			if (Event.current.type == EventType.Repaint)
			{
				var extrude = target as ExtrudeFilter;
				var transform = extrude.gameObject.transform;

				if (!extrude.isActiveAndEnabled) return;
				if (extrude.Strength <= 0f) return;

				Handles.matrix = transform.localToWorldMatrix;
				Handles.color = Color.black;

				Vector2 offset = -Extrude.AngleToOffset(extrude.Angle, Vector2.one) * extrude.PerspectiveDistance;
				Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.identity, 100f,  EventType.Repaint);
				Handles.DrawLine(Vector3.zero, new Vector3(offset.x, offset.y, 0f));
				Handles.color = Color.black;
				Handles.ArrowHandleCap(0, new Vector3(offset.x, offset.y, 0f), Quaternion.identity, 20f,  EventType.Repaint);
			}
		}
	}
}