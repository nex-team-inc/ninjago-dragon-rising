//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(VideoPlayerSource))]
	[CanEditMultipleObjects]
	internal class VideoPlayerSourceEditor : BaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Video Player Source\n© Chocolate Dinosaur Ltd", "uifx-icon")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/video-player-source/"),
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

#if UIFX_VIDEOPLAYER

		private SerializedProperty _propVideoPlayer;
		private SerializedProperty _propMaterial;
		private SerializedProperty _propColor;
		private SerializedProperty _propRaycastTarget;
		private SerializedProperty _propMaskable;

		void OnEnable()
		{
			_propVideoPlayer = VerifyFindProperty("_videoPlayer");
			_propMaterial = VerifyFindProperty("m_Material");
			_propColor = VerifyFindProperty("m_Color");
			_propRaycastTarget = VerifyFindProperty("m_RaycastTarget");
			_propMaskable = VerifyFindProperty("m_Maskable");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			EditorGUILayout.PropertyField(_propVideoPlayer);

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(_propMaterial);
			EditorGUILayout.PropertyField(_propColor);
			EditorGUILayout.PropertyField(_propRaycastTarget);
			EditorGUILayout.PropertyField(_propMaskable);

			serializedObject.ApplyModifiedProperties();
		}

#else

		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("This component requires enabling the built-in Video module in the Package Manager.", MessageType.Error, true);
		}

#endif
	}
}