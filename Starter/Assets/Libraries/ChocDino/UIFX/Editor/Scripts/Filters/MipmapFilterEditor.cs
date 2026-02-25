//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(MipmapFilter), true)]
	[CanEditMultipleObjects]
	internal class MipmapFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Mipmap Filter\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/mip-map-filter/"),
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

		private SerializedProperty _propGenerateMipMap;
		private SerializedProperty _propMipMapBias;
		private SerializedProperty _propAnisoLevel;
		private int _incompatibleFilterCount;

		void OnEnable()
		{
			_propGenerateMipMap = VerifyFindProperty("_generateMipMap");
			_propMipMapBias = VerifyFindProperty("_mipMapBias");
			_propAnisoLevel = VerifyFindProperty("_anisoLevel");
			CheckFiltersState();
		}

		void CheckFiltersState()
		{
			_incompatibleFilterCount = 0;
			var filterComponents = (this.target as Component).gameObject.GetComponents<FilterBase>();
			foreach (var filter in filterComponents)
			{
				//if (filter.enabled)
				{
					if (filter.RenderSpace != FilterRenderSpace.Canvas)
					{
						_incompatibleFilterCount++;
					}
				}
			}
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			CheckFiltersState();
			if (_incompatibleFilterCount > 0)
			{
				EditorGUILayout.HelpBox(string.Format("Found {0} filters are not compatible with MipmapFitler. All filters must have their `RenderSpace` property set to `Canvas` for this filter to work correctly.", _incompatibleFilterCount), MessageType.Error);
			}

			EditorGUILayout.PropertyField(_propGenerateMipMap);
			if (_propGenerateMipMap.boolValue)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propMipMapBias);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.PropertyField(_propAnisoLevel);

			if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable && _propAnisoLevel.intValue > 1)
			{
				EditorGUILayout.HelpBox("Anisotropic Filtering is disabled in current Quality Settings", MessageType.Warning);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}