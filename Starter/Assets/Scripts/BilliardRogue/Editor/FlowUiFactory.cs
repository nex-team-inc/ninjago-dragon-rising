#nullable enable

using System;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Nex.KeyboardNavigation;
using Nex.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// UI construction helpers shared by the Flow prefab builders: view roots, labels bound to localization keys,
    /// pixel frames, fade animators, Back-proxy key responders and tolerant asset loading (missing content from
    /// other modules degrades to a placeholder plus a warning).
    /// </summary>
    public static class FlowUiFactory
    {
        public const string FontPath = "Assets/Fonts/BilliardRogue/BilliardPixel_TMP.asset";
        public const string UiSpriteFolder = "Assets/Sprites/BilliardRogue/UI";
        public const string ViewsFolder = "Assets/Prefabs/BilliardRogue/Views";
        public const string FlowFolder = "Assets/Prefabs/BilliardRogue/Flow";
        /// <summary>UI pixel art is drawn at 3x (Tools/Textures ui_slices.json).</summary>
        public const int PixelScale = 3;

        static readonly Color placeholderColor = new(0.16f, 0.13f, 0.2f, 0.9f);

        #region Objects & layout

        public static GameObject CreateUIObject(string name, Transform? parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Stretch(GameObject go, float inset = 0f)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static GameObject CreateViewRoot<TView>(string name, out TView view) where TView : SimpleCanvasView
        {
            var go = CreateUIObject(name, null);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // ViewManager re-applies it and sets the camera at push
            canvas.planeDistance = 100f;
            canvas.sortingOrder = 0;
            canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)27; // TMP, same as WelcomeScreenView
            AddScaler(go);
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<CanvasGroup>();
            view = go.AddComponent<TView>();
            return go;
        }

        public static Canvas CreateOverlayCanvasRoot(string name, int sortingOrder)
        {
            var go = CreateUIObject(name, null);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            AddScaler(go);
            return canvas;
        }

        static void AddScaler(GameObject go)
        {
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
        }

        #endregion

        #region Widgets

        /// <summary>Unscaled canvas-group fade (MMF); the default MMF_CanvasGroup curve is a 0→1→0 bump, so it is set explicitly.</summary>
        public static MMF_Player CreateFadeAnimator(string name, Transform parent, CanvasGroup target, float from, float to)
        {
            var go = CreateUIObject(name, parent);
            var player = go.AddComponent<MMF_Player>();
            player.PlayerTimescaleMode = TimescaleModes.Unscaled;
            var fade = (MMF_CanvasGroup)player.AddFeedback(typeof(MMF_CanvasGroup));
            fade.TargetCanvasGroup = target;
            fade.Mode = MMF_FeedbackBase.Modes.OverTime;
            fade.Duration = 0.35f;
            fade.AlphaCurve = new MMTweenType(AnimationCurve.Linear(0f, 0f, 1f, 1f));
            fade.RemapZero = from;
            fade.RemapOne = to;
            fade.Timing.TimescaleMode = TimescaleModes.Unscaled;
            return player;
        }

        /// <summary>TMP label in the pixel font; a localization key binds a NexLocalizedString, editorText is the placeholder shown in prefabs.</summary>
        public static TextMeshProUGUI CreateLabel(string name, Transform parent, int fontSize, TextAlignmentOptions alignment, string? locKey, string editorText)
        {
            var go = CreateUIObject(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            var font = LoadFont();
            if (font != null) label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = editorText;
            if (locKey != null) BindLocalizedKey(go.AddComponent<NexLocalizedString>(), locKey);
            return label;
        }

        /// <summary>Image using a UI sprite when it is imported as a sprite (9-sliced when it has a border), a tinted plain image otherwise.</summary>
        public static Image CreateImage(string name, Transform parent, string? spriteName, Color color, bool raycastTarget = false)
        {
            var go = CreateUIObject(name, parent);
            var image = go.AddComponent<Image>();
            image.raycastTarget = raycastTarget;
            var sprite = spriteName != null ? LoadSprite(UiSpriteFolder, spriteName) : null;
            if (sprite == null)
            {
                image.color = spriteName != null ? placeholderColor : color;
                return image;
            }

            image.sprite = sprite;
            image.color = color;
            image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        public static Sprite? LoadSprite(string folder, string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[FlowPrefabs] Sprite {folder}/{name}.png missing or not imported as a sprite; using a placeholder.");
            return sprite;
        }

        public static TMP_FontAsset? LoadFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        /// <summary>Escape / top-level Back reaches the view through a proxy node (DebugSettingsView pattern).</summary>
        public static GraphKeyResponder CreateGraphWithBackProxy(Transform root, KeyResponder? content, TopLevelControlPanel.ControlConfig backControl)
        {
            var graphGo = CreateUIObject("KeyResponder", root);
            var graph = graphGo.AddComponent<GraphKeyResponder>();
            var proxyGo = CreateUIObject("BackProxy", graphGo.transform);
            var proxy = proxyGo.AddComponent<TopLevelControlProxyKeyResponder>();
            var proxyNode = proxyGo.AddComponent<KeyResponderGraphNode>();
            var proxySo = new SerializedObject(proxy);
            proxySo.FindProperty("control").intValue = (int)backControl;
            proxySo.ApplyModifiedPropertiesWithoutUndo();
            SetReference(proxyNode, "keyResponder", proxy);

            KeyResponderGraphNode initialNode = proxyNode;
            if (content != null)
            {
                var contentNode = CreateUIObject("ContentNode", graphGo.transform).AddComponent<KeyResponderGraphNode>();
                SetReference(contentNode, "keyResponder", content);
                initialNode = contentNode;
            }

            var graphSo = new SerializedObject(graph);
            graphSo.FindProperty("fetchChildNodes").boolValue = true;
            graphSo.FindProperty("initialGraphNode").objectReferenceValue = initialNode;
            graphSo.FindProperty("backButtonResponder").objectReferenceValue = proxy;
            graphSo.ApplyModifiedPropertiesWithoutUndo();
            return graph;
        }

        #endregion

        #region Serialization & assets

        public static void SetReference(Object target, string field, Object? value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Binds the string reference by table collection GUID + key name, so it resolves once LocalizationSeeder adds the entry.</summary>
        public static void BindLocalizedKey(NexLocalizedString label, string key)
        {
            var so = new SerializedObject(label);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = TableCollectionReference();
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = key;
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static string TableCollectionReference()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocKeys.Table);
            return collection != null ? "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N") : LocKeys.Table;
        }

        /// <summary>Prefab of another module by path; null plus a warning when it is not built yet.</summary>
        public static GameObject? LoadPrefab(string path, string owner)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogWarning($"[FlowPrefabs] {path} is missing ({owner} module not built yet); slot left empty.");
            return prefab;
        }

        public static T? LoadPrefabComponent<T>(string path, string owner) where T : Component
        {
            var prefab = LoadPrefab(path, owner);
            if (prefab == null) return null;
            var component = prefab.GetComponent<T>();
            if (component == null) Debug.LogWarning($"[FlowPrefabs] {path} has no {typeof(T).Name} on its root; slot left empty.");
            return component;
        }

        /// <summary>Nested prefab instance under parent, or null (with a warning) when the prefab is not built yet.</summary>
        public static GameObject? InstantiateNested(string path, Transform parent, string owner)
        {
            var prefab = LoadPrefab(path, owner);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return instance;
        }

        /// <summary>Saves over an existing path (the asset GUID survives) and destroys the scene object.</summary>
        public static GameObject SavePrefab(GameObject root, string path)
        {
            BuilderAssets.EnsureFolder(System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            Object.DestroyImmediate(root);
            if (!success) throw new InvalidOperationException($"Saving {path} failed");
            return asset;
        }

        #endregion
    }
}
