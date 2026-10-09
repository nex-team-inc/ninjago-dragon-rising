#nullable enable

using System.Collections.Generic;
using Nex.KeyboardNavigation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Guards the built UI prefabs (UiViewsBuilder + FlowPrefabsBuilder output): HUD columns never overlap each other or
    // the arena band, the PiP feed overlay sits exactly on the HUD camera screen, pixel-font sizes are multiples of 16
    // (TDD D12) and every interactive view has a key responder (TV remote).
    public class UiLayoutContractTests
    {
        const string ViewsFolder = "Assets/Prefabs/BilliardRogue/Views";
        const float ScreenWidth = 1920f;

        static readonly string[] interactiveViews = { "TitleView", "PlayerModeView", "SettingsView", "PauseView", "RewardView", "SummaryView", "CalibrationView" };

        static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is not built");
            return prefab;
        }

        static RectTransform Child(GameObject root, string path)
        {
            var child = root.transform.Find(path);
            Assert.IsNotNull(child, $"{root.name}/{path} missing");
            return (RectTransform)child;
        }

        [Test]
        public void HudColumnsStayOutsideTheArenaBandAndPanelsDoNotOverlap()
        {
            var hud = Load(UiViewsBuilder.HudPath);
            var left = Child(hud, "LeftColumn");
            var right = Child(hud, "RightColumn");
            Assert.AreEqual(UiHudBuilder.Margin, left.anchoredPosition.x, 0.01f);
            Assert.AreEqual(-UiHudBuilder.Margin, right.anchoredPosition.x, 0.01f);
            Assert.LessOrEqual(UiHudBuilder.Margin + UiHudBuilder.ColumnWidth, ScreenWidth * 0.25f, "left column reaches into the arena band");

            // The camera is a small bottom-right panel, HP hearts sit bottom-left, act/stage/turn is top centre.
            Assert.IsNotNull(hud.transform.Find("CameraPanel/Screen"), "camera screen missing");
            Assert.IsNotNull(hud.transform.Find("HpHearts"), "HP hearts missing");
            Assert.IsNotNull(hud.transform.Find("StageBar/Stage"), "stage text missing");

            foreach (RectTransform panel in right)
            {
                Assert.LessOrEqual(panel.sizeDelta.x, UiHudBuilder.ColumnWidth, panel.name);
            }
        }

        [Test]
        public void PipFeedOverlayCoversTheHudCameraScreen()
        {
            var hud = Load(UiViewsBuilder.HudPath);
            var screen = Child(hud, "CameraPanel/Screen");
            var panel = (RectTransform)screen.parent;
            var screenFromTopLeft = panel.anchoredPosition + screen.anchoredPosition;
            Assert.AreEqual(UiHudBuilder.PipFeedScreenPosition, screenFromTopLeft);
            Assert.AreEqual(UiHudBuilder.PipFeedSize, screen.sizeDelta);

            var feed = Child(Load(FlowPrefabsBuilder.PipPath), "PreviewFrame");
            Assert.AreEqual(new Vector2(0f, 1f), feed.anchorMin);
            Assert.AreEqual(UiHudBuilder.PipFeedScreenPosition, feed.anchoredPosition);
            Assert.AreEqual(UiHudBuilder.PipFeedSize, feed.sizeDelta);
        }

        [Test]
        public void PixelFontSizesAreMultiplesOf16()
        {
            var paths = new List<string> { UiViewsBuilder.HudPath };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ViewsFolder })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var path in paths)
            {
                foreach (var text in Load(path).GetComponentsInChildren<TMP_Text>(true))
                {
                    // Starter prefabs nested in our views (PreviewsManager) keep their own fonts.
                    if (text.font == null || !text.font.name.StartsWith("BilliardPixel")) continue;
                    Assert.AreEqual(0f, text.fontSize % 16f, $"{path}: {text.name} ({text.transform.parent.name}) uses {text.fontSize}");
                }
            }
        }

        [Test]
        public void EveryInteractiveViewHasAKeyResponder()
        {
            foreach (var name in interactiveViews)
            {
                var view = Load($"{ViewsFolder}/{name}.prefab").GetComponent<SimpleView>();
                Assert.IsNotNull(view, name);
                var responder = new SerializedObject(view).FindProperty("keyResponder").objectReferenceValue as KeyResponder;
                Assert.IsNotNull(responder, $"{name} has no key responder: arrows / Enter / Back would do nothing");
            }
        }
    }
}
