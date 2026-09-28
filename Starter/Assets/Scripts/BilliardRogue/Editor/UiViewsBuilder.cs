#nullable enable

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the Billiard Rogue UI (TDD §9, §17): UiTheme asset (created if missing; empty sprite/font slots filled by
    /// path), the eight view prefabs in Assets/Prefabs/BilliardRogue/Views and GameplayHud in Assets/Prefabs/BilliardRogue/UI.
    /// Idempotent: an existing prefab keeps its root and root components (external references stay valid) and its
    /// children are rebuilt from code. Missing art/fonts degrade to placeholders with a warning.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.UiViewsBuilder.Run();' --project-path …/Starter
    /// </summary>
    public static class UiViewsBuilder
    {
        public const string ThemePath = BuilderAssets.ConfigRoot + "/UiTheme.asset";
        public const string ViewsFolder = "Assets/Prefabs/BilliardRogue/Views";
        public const string UiFolder = "Assets/Prefabs/BilliardRogue/UI";
        public const string HudPath = UiFolder + "/GameplayHud.prefab";

        const string SpritesUi = "Assets/Sprites/BilliardRogue/UI/";
        const string SpritesIcons = "Assets/Sprites/BilliardRogue/Icons/";
        const string Fonts = "Assets/Fonts/BilliardRogue/";

        static readonly (string Field, string Path)[] themeAssets =
        {
            ("regularFont", Fonts + "BilliardPixel_TMP.asset"), ("boldFont", Fonts + "BilliardPixelBold_TMP.asset"),
            ("panel", SpritesUi + "Frame_Panel.png"), ("card", SpritesUi + "Frame_Card.png"),
            ("button", SpritesUi + "Frame_Button.png"), ("buttonFocused", SpritesUi + "Frame_ButtonFocused.png"),
            ("banner", SpritesUi + "Frame_Banner.png"), ("slot", SpritesUi + "Frame_Slot.png"),
            ("slotActive", SpritesUi + "Frame_SlotActive.png"), ("barBackground", SpritesUi + "Bar_Bg.png"),
            ("hpFill", SpritesUi + "Bar_Fill_Hp.png"), ("bossFill", SpritesUi + "Bar_Fill_Boss.png"),
            ("chip", SpritesUi + "Chip.png"), ("heart", SpritesUi + "Icon_Heart.png"), ("ball", SpritesUi + "Icon_Ball.png"),
            ("skull", SpritesUi + "Icon_Skull.png"), ("turn", SpritesUi + "Icon_Turn.png"), ("arrowRight", SpritesUi + "Arrow.png"),
            ("arrowLeft", SpritesUi + "Arrow_Left.png"), ("cursor", SpritesUi + "Cursor.png"), ("dim", SpritesUi + "Overlay_Dim.png"),
            ("vignette", SpritesUi + "Overlay_Vignette.png"), ("logo", SpritesUi + "Logo_BilliardRogue.png"),
            ("portraitP1", SpritesUi + "Portrait_CatP1.png"), ("portraitP2", SpritesUi + "Portrait_CatP2.png"),
            ("rewardHeal", SpritesIcons + "Reward_Heal.png"), ("rewardMaxHp", SpritesIcons + "Reward_MaxHp.png"),
        };

        [MenuItem("Nex/Billiard Rogue/UI Views", priority = 60)]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var theme = EnsureTheme(out var themeWarnings);
            var kit = new UiPrefabKit(theme);
            kit.Warnings.AddRange(themeWarnings);
            BuilderAssets.EnsureFolder(ViewsFolder);
            BuilderAssets.EnsureFolder(UiFolder);

            var built = new List<string>();
            var settings = UiMenuViewsBuilder.BuildSettings(kit, ViewPath("SettingsView"));
            built.Add(UiMenuViewsBuilder.BuildTitle(kit, ViewPath("TitleView")));
            built.Add(UiMenuViewsBuilder.BuildPlayerMode(kit, ViewPath("PlayerModeView")));
            built.Add(ViewPath("SettingsView"));
            built.Add(UiMenuViewsBuilder.BuildPause(kit, ViewPath("PauseView"), settings));
            built.Add(UiOverlayViewsBuilder.BuildStageIntro(kit, ViewPath("StageIntroView")));
            built.Add(UiOverlayViewsBuilder.BuildReward(kit, ViewPath("RewardView")));
            built.Add(UiOverlayViewsBuilder.BuildTrackingLost(kit, ViewPath("TrackingLostView")));
            built.Add(UiOverlayViewsBuilder.BuildSummary(kit, ViewPath("SummaryView")));
            built.Add(UiHudBuilder.Build(kit, HudPath));
            AssetDatabase.SaveAssets();

            var report = new StringBuilder($"[UiViewsBuilder] built {built.Count} prefabs");
            if (kit.MissingKeys.Count > 0)
            {
                report.Append($"; {kit.MissingKeys.Count} keys not in the string table yet (run LocalizationSeeder)");
            }

            foreach (var warning in kit.Warnings) report.Append("\n  warning: ").Append(warning);
            foreach (var warning in kit.Warnings) Debug.LogWarning("[UiViewsBuilder] " + warning);
            return report.ToString();
        }

        #region Prefab roots

        static readonly HashSet<GameObject> loadedRoots = new();

        public static string ViewPath(string name) => $"{ViewsFolder}/{name}.prefab";

        /// <summary>Existing prefab contents with its children removed (root + root components kept), or a fresh root.</summary>
        public static GameObject OpenRoot(UiPrefabKit kit, string path, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return kit.Ui(name, null);
            var root = PrefabUtility.LoadPrefabContents(path);
            loadedRoots.Add(root);
            for (var i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            return root;
        }

        public static string SaveRoot(GameObject root, string path)
        {
            var existing = loadedRoots.Remove(root);
            PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            if (existing) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            if (!success) throw new System.InvalidOperationException($"[UiViewsBuilder] saving {path} failed");
            return path;
        }

        /// <summary>Canvas (screen-space camera, set by ViewManager) + 1920x1080 scaler (match 0.5) + raycaster + view + Content group.</summary>
        public static TView ViewRoot<TView>(UiPrefabKit kit, GameObject root, out CanvasGroup content) where TView : RogueView
        {
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = GetOrAdd<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 0;
            canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)27;
            var scaler = GetOrAdd<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            GetOrAdd<GraphicRaycaster>(root);
            var view = GetOrAdd<TView>(root);
            var contentGo = kit.Ui("Content", root.transform);
            UiPrefabKit.Stretch(contentGo);
            content = contentGo.AddComponent<CanvasGroup>();
            UiPrefabKit.Set(view, "theme", kit.Theme);
            UiPrefabKit.Set(view, "content", content);
            UiPrefabKit.Set(view, "entryAnimator", null);
            UiPrefabKit.Set(view, "toBackgroundAnimator", null);
            return view;
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        #endregion

        #region Theme

        static UiTheme EnsureTheme(out List<string> warnings)
        {
            warnings = new List<string>();
            var theme = BuilderAssets.LoadOrCreate<UiTheme>(ThemePath, out _);
            var so = new SerializedObject(theme);
            foreach (var (field, path) in themeAssets)
            {
                var property = so.FindProperty(field);
                if (property.objectReferenceValue != null) continue;
                Object? asset = field.EndsWith("Font") ? AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path) : AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (asset == null)
                {
                    warnings.Add($"{path} missing (not built or not imported as a sprite): '{field}' stays empty");
                    continue;
                }

                property.objectReferenceValue = asset;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        #endregion
    }
}
