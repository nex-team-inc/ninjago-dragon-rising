#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>SerializedObject writes for private [SerializeField] fields of the UI components the builders wire.</summary>
    public static class UiFields
    {
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

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
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
    }
}
