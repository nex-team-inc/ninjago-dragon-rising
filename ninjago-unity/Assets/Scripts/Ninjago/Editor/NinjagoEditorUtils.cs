#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Nex.KeyboardNavigation;
using Nex.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nex.Ninjago.Editor
{
    /// <summary>Shared helpers for the Ninjago builders (assets are authored through Unity APIs, never YAML).</summary>
    public static class NinjagoEditorUtils
    {
        public const string PrefabsRoot = "Assets/Prefabs/Ninjago";
        public const string ConfigsRoot = "Assets/Configs/Ninjago";
        public const string MaterialsRoot = "Assets/Materials/Ninjago";
        public const string SpritesRoot = "Assets/Sprites/Ninjago";
        public const string TableName = "LocalizationTable";
        public const string FontPath = "Assets/Fonts/BrainParty/Play Chickens SDF.asset";

        #region Assets

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Missing asset {path}");
            return asset;
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                if (!success) throw new InvalidOperationException($"Could not save prefab {path}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static T LoadComponent<T>(string prefabPath) where T : Component
        {
            return Load<GameObject>(prefabPath).GetComponent<T>();
        }

        #endregion

        #region Serialized Fields

        public static void Set(Object target, string field, Object? value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, Action<SerializedProperty> write)
        {
            var serialized = new SerializedObject(target);
            write(Property(serialized, field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var array = Property(serialized, field);
            array.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty Property(SerializedObject serialized, string field)
        {
            return serialized.FindProperty(field) ?? throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} has no field '{field}'");
        }

        #endregion

        #region Localization

        public static long KeyId(string key)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            return collection.SharedData.GetEntry(key)?.Id ?? throw new InvalidOperationException($"Missing localization key '{key}' (run the seeder)");
        }

        public static void WriteLocalized(SerializedProperty property, string key)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            property.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue =
                "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            property.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            property.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
        }

        public static void SetLocalized(Object target, string field, string key)
        {
            Set(target, field, property => WriteLocalized(property, key));
        }

        #endregion

        #region UI

        /// <param name="parent">Null for the root of a widget prefab.</param>
        public static RectTransform NewRect(string name, Transform? parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            return Place(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        /// <summary>
        /// TMP label with a NexLocalizedString bound to key (English preview written into the text). Pass a null key
        /// for smart strings: their owner assigns the reference together with its arguments at runtime.
        /// </summary>
        public static NexLocalizedString Label(Transform parent, string name, string? key, string preview, float size, Color color,
            Material? material = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = PlainText(parent, name, preview, size, color, material, alignment);
            var label = text.gameObject.AddComponent<NexLocalizedString>();
            if (key != null) SetLocalized(label, "m_StringReference", key);
            return label;
        }

        public static TextMeshProUGUI PlainText(Transform parent, string name, string text, float size, Color color,
            Material? material = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Load<TMP_FontAsset>(FontPath);
            if (material != null) tmp.fontSharedMaterial = material;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static UnityEngine.UI.Image NewImage(Transform parent, string name, Sprite? sprite, Color color)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Key responder for views without buttons: Enter/Escape press the top-level Back button.</summary>
        public static KeyResponder BackProxy(RectTransform ui)
        {
            var proxy = ui.gameObject.AddComponent<TopLevelControlProxyKeyResponder>();
            Set(proxy, "control", property => property.intValue = (int)TopLevelControlPanel.ControlConfig.Back);
            return proxy;
        }

        /// <summary>Raw image showing a world camera's render texture over the given screen part.</summary>
        public static UnityEngine.UI.RawImage Feed(RectTransform ui, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = NewRect(name, ui);
            Place(rect, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var raw = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            raw.raycastTarget = false;
            return raw;
        }

        #endregion

        #region 3D

        public static GameObject Block(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material,
            Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            return go;
        }

        public static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        #endregion
    }
}
