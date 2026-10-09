#nullable enable

using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Writes a plain C# object into a SerializedProperty tree by matching public field names, so builders can
    /// author private [SerializeField] data (nested rules classes, arrays, lists) from ordinary objects.
    /// </summary>
    public static class SerializedPropertyWriter
    {
        #region Public Methods

        public static void Write(SerializedProperty target, object? value)
        {
            if (value == null)
            {
                if (target.propertyType == SerializedPropertyType.ObjectReference) target.objectReferenceValue = null;
                return;
            }

            switch (target.propertyType)
            {
                case SerializedPropertyType.Integer:
                    target.longValue = Convert.ToInt64(value);
                    break;
                case SerializedPropertyType.Boolean:
                    target.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Float:
                    target.doubleValue = Convert.ToDouble(value);
                    break;
                case SerializedPropertyType.String:
                    target.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    target.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.Enum:
                    // intValue is the underlying enum value (same convention as EnumDictionaryEditorUtils).
                    target.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Vector2:
                    target.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    target.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    target.vector2IntValue = (Vector2Int)value;
                    break;
                case SerializedPropertyType.Rect:
                    target.rectValue = (Rect)value;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    target.animationCurveValue = (AnimationCurve)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    target.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                case SerializedPropertyType.Generic when target.isArray:
                    WriteArray(target, (IList)value);
                    break;
                case SerializedPropertyType.Generic:
                    WriteObject(target, value);
                    break;
                default:
                    throw new NotSupportedException($"{target.propertyPath}: {target.propertyType} is not supported by SerializedPropertyWriter");
            }
        }

        #endregion

        #region Helpers

        static void WriteArray(SerializedProperty target, IList list)
        {
            target.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                Write(target.GetArrayElementAtIndex(i), list[i]);
            }
        }

        static void WriteObject(SerializedProperty target, object value)
        {
            var fields = value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.IsInitOnly || field.IsNotSerialized) continue;
                var child = target.FindPropertyRelative(field.Name);
                if (child == null)
                {
                    throw new InvalidOperationException($"{target.propertyPath} has no serialized field '{field.Name}' ({value.GetType().Name})");
                }

                Write(child, field.GetValue(value));
            }
        }

        #endregion
    }
}
