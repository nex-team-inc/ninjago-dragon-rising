#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Nex.KeyboardNavigation;
using Nex.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Building blocks for the Billiard Rogue UI prefabs (UiViewsBuilder): pixel-kit images, drop-shadow TMP labels bound
    /// to LocKeys through NexLocalizedString, focusable buttons, key-responder groups and prefab saving that keeps the
    /// root components (and so every external reference to them) stable across rebuilds.
    /// </summary>
    public sealed class UiPrefabKit
    {
        public enum Fill
        {
            Simple,
            Sliced,
            Tiled,
            Horizontal,
        }

        const string TableName = "LocalizationTable";
        static readonly Regex englishLine = new(@"""(br\.[^""]+)""[^/]*//\s*en:\s*(.*)$", RegexOptions.Compiled);
        static readonly Regex placeholder = new(@"\{\d[^}]*\}", RegexOptions.Compiled);
        static readonly Regex keyLiteral = new(@"""(br\.[^""]+)""", RegexOptions.Compiled);
        static readonly Regex englishOnly = new(@"^\s*//\s*en:\s*(.*)$", RegexOptions.Compiled);

        readonly Dictionary<string, string> english = new();
        readonly HashSet<string> knownKeys = new();
        readonly string tableReference;
        readonly int uiLayer;

        public UiTheme Theme { get; }
        public TMP_FontAsset Font { get; }
        public TMP_FontAsset BoldFont { get; }
        public SortedSet<string> Warnings { get; } = new();

        public UiPrefabKit(UiTheme theme)
        {
            Theme = theme;
            Font = theme.RegularFont != null ? theme.RegularFont : TMP_Settings.defaultFontAsset;
            BoldFont = theme.BoldFont != null ? theme.BoldFont : Font;
            if (theme.RegularFont == null) Warnings.Add("pixel TMP font missing: labels use the TMP default font");
            uiLayer = LayerMask.NameToLayer("UI");
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            if (collection != null)
            {
                tableReference = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
                foreach (var entry in collection.SharedData.Entries) knownKeys.Add(entry.Key);
            }
            else
            {
                tableReference = TableName;
                Warnings.Add($"string table collection '{TableName}' not found: labels bound by table name");
            }

            ReadEnglish();
        }

        #region Objects & layout

        public GameObject Ui(string name, Transform? parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = uiLayer };
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Anchors and pivot at anchor (0..1), positioned at pos (units, from the anchor), sized size.</summary>
        public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(GameObject go, float inset = 0f)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-2f * inset, -2f * inset);
            return rect;
        }

        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Vector2 Top = new(0.5f, 1f);
        public static readonly Vector2 Bottom = new(0.5f, 0f);
        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 Left = new(0f, 0.5f);
        public static readonly Vector2 Right = new(1f, 0.5f);

        #endregion

        #region Images

        public Image Image(Transform parent, string name, Sprite? sprite, Vector2 anchor, Vector2 pos, Vector2 size,
            Fill fill = Fill.Simple, Color? color = null)
        {
            var go = Ui(name, parent);
            Place(go, anchor, pos, size);
            return Configure(go.AddComponent<Image>(), sprite, fill, color ?? Color.white);
        }

        public Image StretchImage(Transform parent, string name, Sprite? sprite, float inset = 0f, Fill fill = Fill.Simple,
            Color? color = null)
        {
            var go = Ui(name, parent);
            Stretch(go, inset);
            return Configure(go.AddComponent<Image>(), sprite, fill, color ?? Color.white);
        }

        public Image Configure(Image image, Sprite? sprite, Fill fill, Color color)
        {
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var bordered = sprite != null && sprite.border != Vector4.zero;
            switch (fill)
            {
                case Fill.Sliced:
                    image.type = UnityEngine.UI.Image.Type.Sliced;
                    break;
                case Fill.Tiled:
                    // Tiling a borderless sprite would repeat the whole frame; fall back to slicing until borders exist.
                    image.type = bordered ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Sliced;
                    break;
                case Fill.Horizontal:
                    image.type = UnityEngine.UI.Image.Type.Filled;
                    image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                    image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                    image.fillAmount = 1f;
                    break;
                default:
                    image.type = UnityEngine.UI.Image.Type.Simple;
                    image.preserveAspect = true;
                    break;
            }

            return image;
        }

        /// <summary>Full-screen dim + vignette pair for overlays.</summary>
        public void DimLayers(Transform content)
        {
            StretchImage(content, "Dim", Theme.Dim, 0f, Fill.Simple, Theme.DimColor).preserveAspect = false;
            StretchImage(content, "Vignette", Theme.Vignette, 0f, Fill.Simple, Theme.VignetteColor).preserveAspect = false;
        }

        #endregion

        #region Text

        /// <summary>
        /// Label with a drop-shadow copy (one font pixel down-right) behind the face; key null = numbers-only label.
        /// Font size must be a multiple of 16.
        /// </summary>
        public TextLabel Label(Transform parent, string name, string? key, int size, Color color, Vector2 anchor, Vector2 pos,
            Vector2 rectSize, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false, bool shadow = true,
            bool bold = false, string numbersPreview = "0")
        {
            if (size % 16 != 0) Warnings.Add($"{name}: font size {size} is not a multiple of 16");
            var go = Ui(name, parent);
            Place(go, anchor, pos, rectSize);
            var preview = key == null ? numbersPreview : English(key);
            var layers = new List<Object>();
            var localized = new List<Object>();
            if (shadow)
            {
                var offset = size / 16f;
                var shadowText = Text(go.transform, "Shadow", preview, size, Theme.TextShadow, align, wrap, bold);
                ((RectTransform)shadowText.transform).anchoredPosition = new Vector2(offset, -offset);
                layers.Add(shadowText);
                if (key != null) localized.Add(Bind(shadowText, key));
            }

            var face = Text(go.transform, "Face", preview, size, color, align, wrap, bold);
            layers.Add(face);
            if (key != null) localized.Add(Bind(face, key));
            var label = go.AddComponent<TextLabel>();
            SetArray(label, "layers", layers);
            SetArray(label, "localized", localized);
            return label;
        }

        TextMeshProUGUI Text(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align,
            bool wrap, bool bold)
        {
            var go = Ui(name, parent);
            Stretch(go);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = bold ? BoldFont : Font;
            tmp.fontSize = size;
            tmp.enableAutoSizing = false;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.text = text;
            return tmp;
        }

        NexLocalizedString Bind(TextMeshProUGUI tmp, string key)
        {
            var localized = tmp.gameObject.AddComponent<NexLocalizedString>();
            var so = new SerializedObject(localized);
            var reference = so.FindProperty("m_StringReference");
            reference.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = tableReference;
            reference.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = 0;
            reference.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!knownKeys.Contains(key)) MissingKeys.Add(key);
            return localized;
        }

        public HashSet<string> MissingKeys { get; } = new();

        /// <summary>English copy from the LocKeys "// en:" comments, placeholders shown as 1 (prefab preview only).</summary>
        public string English(string key)
        {
            if (!english.TryGetValue(key, out var text))
            {
                Warnings.Add($"no English copy for {key}");
                return key;
            }

            return placeholder.Replace(text, "1");
        }

        void ReadEnglish()
        {
            var root = Path.Combine(Application.dataPath, "Scripts/BilliardRogue");
            foreach (var file in Directory.GetFiles(root, "LocKeys*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = englishLine.Match(lines[i]);
                    if (match.Success)
                    {
                        english[match.Groups[1].Value] = Clean(match.Groups[2].Value);
                        continue;
                    }

                    // Per-level description arrays: keys on one line, "// en: a | b | c" on the next.
                    var keys = keyLiteral.Matches(lines[i]);
                    if (keys.Count < 2 || i + 1 >= lines.Length) continue;
                    var next = englishOnly.Match(lines[i + 1]);
                    if (!next.Success) continue;
                    var parts = next.Groups[1].Value.Split('|');
                    for (var k = 0; k < keys.Count && k < parts.Length; k++)
                    {
                        english[keys[k].Groups[1].Value] = Clean(parts[k]);
                    }
                }
            }
        }

        static string Clean(string text)
        {
            text = text.Trim();
            return text.EndsWith("(smart)") ? text[..^"(smart)".Length].TrimEnd() : text;
        }

        #endregion

        #region Buttons & responders

        /// <summary>Frame_Button with a 48 label, paw cursor, focus highlight and UiButtonKeyResponder (navigation None).</summary>
        public Button MenuButton(Transform parent, string name, string key, Vector2 size, Vector2 labelOffset = default)
        {
            var go = Ui(name, parent);
            Place(go, Center, Vector2.zero, size);
            var frame = Configure(go.AddComponent<Image>(), Theme.Button, Fill.Sliced, Color.white);
            frame.raycastTarget = true;
            var button = PlainButton(go, frame);
            Label(go.transform, "Label", key, 48, Theme.TextPrimary, Center, labelOffset + new Vector2(0f, 3f), new Vector2(size.x - 48f, 64f));
            var cursor = Cursor(go.transform, size.x);
            var highlight = Highlight(go, go.transform, frame, Theme.ButtonFocused, null, cursor);
            Responder(go, button, highlight);
            return button;
        }

        public Button PlainButton(GameObject go, Graphic target)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public RectTransform Cursor(Transform parent, float elementWidth)
        {
            var cursor = Image(parent, "Cursor", Theme.Cursor, Center, new Vector2(-elementWidth * 0.5f - 20f, 0f), new Vector2(48f, 48f));
            cursor.gameObject.SetActive(false);
            return cursor.rectTransform;
        }

        public FocusHighlight Highlight(GameObject go, Transform pulseTarget, Image? frame, Sprite? focusedSprite, GameObject? glow,
            RectTransform? cursor, params Graphic[] accentGraphics)
        {
            var highlight = go.AddComponent<FocusHighlight>();
            Set(highlight, "theme", Theme);
            Set(highlight, "pulseTarget", pulseTarget);
            Set(highlight, "frame", frame);
            Set(highlight, "focusedSprite", focusedSprite);
            Set(highlight, "glow", glow);
            Set(highlight, "cursor", cursor);
            SetArray(highlight, "accentGraphics", new List<Object>(accentGraphics));
            return highlight;
        }

        public UiButtonKeyResponder Responder(GameObject go, Button button, FocusHighlight highlight)
        {
            var responder = go.AddComponent<UiButtonKeyResponder>();
            Set(responder, "button", button);
            Set(responder, "highlight", highlight);
            return responder;
        }

        /// <summary>Linear responder over the direct children (vertical = Up/Down), clamped at the ends.</summary>
        public GroupKeyResponder Group(GameObject go, bool vertical, int initialIndex = 0)
        {
            var group = go.AddComponent<GroupKeyResponder>();
            var so = new SerializedObject(group);
            so.FindProperty("axis").enumValueIndex = vertical ? 1 : 0;
            so.FindProperty("fetchChildResponders").boolValue = true;
            so.FindProperty("initialActiveIndex").intValue = initialIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
            return group;
        }

        /// <summary>Graph root with a top-level control proxy node (Escape → Back/Exit) and a node for the content responder.</summary>
        public GraphKeyResponder GraphWithControlProxy(Transform root, KeyResponder content, TopLevelControlPanel.ControlConfig control)
        {
            var graphGo = Ui("KeyResponder", root);
            Stretch(graphGo);
            var graph = graphGo.AddComponent<GraphKeyResponder>();
            var proxyGo = Ui("ControlProxy", graphGo.transform);
            var proxy = proxyGo.AddComponent<TopLevelControlProxyKeyResponder>();
            var proxyNode = proxyGo.AddComponent<KeyResponderGraphNode>();
            var contentNode = Ui("ContentNode", graphGo.transform).AddComponent<KeyResponderGraphNode>();
            var proxySo = new SerializedObject(proxy);
            proxySo.FindProperty("control").intValue = (int)control;
            proxySo.ApplyModifiedPropertiesWithoutUndo();
            Set(proxyNode, "keyResponder", proxy);
            Set(contentNode, "keyResponder", content);
            var graphSo = new SerializedObject(graph);
            graphSo.FindProperty("fetchChildNodes").boolValue = true;
            graphSo.FindProperty("initialGraphNode").objectReferenceValue = contentNode;
            graphSo.FindProperty("backButtonResponder").objectReferenceValue = proxy;
            graphSo.ApplyModifiedPropertiesWithoutUndo();
            return graph;
        }

        #endregion

        #region Serialized fields

        public static void Set(Object target, string field, Object? value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new System.InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new System.InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
