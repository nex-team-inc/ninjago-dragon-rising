using UnityEngine;
using UnityEditor;

namespace Nex.Utils.Editor
{
    [CustomPropertyDrawer(typeof(FloatRange))]
    public class FloatRangeDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");

            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float labelWidth = EditorGUIUtility.labelWidth;
            bool isNarrow = (viewWidth - labelWidth) < 200f;

            if (isNarrow)
            {
                Rect labelRect = new Rect(position.x, position.y, position.width, singleLineHeight);
                Rect minLabelRect = new Rect(position.x, position.y + singleLineHeight + spacing, 30f, singleLineHeight);
                Rect minFieldRect = new Rect(minLabelRect.xMax + 2f, minLabelRect.y, (position.width - 70f) / 2f, singleLineHeight);
                Rect maxLabelRect = new Rect(minFieldRect.xMax + 6f, minLabelRect.y, 30f, singleLineHeight);
                Rect maxFieldRect = new Rect(maxLabelRect.xMax + 2f, maxLabelRect.y, (position.width - 70f) / 2f, singleLineHeight);

                EditorGUI.LabelField(labelRect, label);
                EditorGUI.LabelField(minLabelRect, "Min");
                EditorGUI.PropertyField(minFieldRect, minProp, GUIContent.none);
                EditorGUI.LabelField(maxLabelRect, "Max");
                EditorGUI.PropertyField(maxFieldRect, maxProp, GUIContent.none);
            }
            else
            {
                float fieldWidth = (position.width - labelWidth - 60f) / 2;
                float labelSpacing = 30f; // Width reserved for "Min"/"Max" labels
                float fieldValueWidth = Mathf.Clamp(fieldWidth - labelSpacing, 0, 100f);
                float valueFieldPadding = 5f;

                Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
                Rect minLabelRect = new Rect(position.x + labelWidth, position.y, labelSpacing, position.height);
                Rect minFieldRect = new Rect(minLabelRect.xMax, position.y, fieldValueWidth, position.height);
                Rect maxLabelRect = new Rect(minFieldRect.xMax + valueFieldPadding, position.y, labelSpacing,
                    position.height);
                Rect maxFieldRect = new Rect(maxLabelRect.xMax, position.y, fieldValueWidth, position.height);


                EditorGUI.LabelField(labelRect, label);
                EditorGUI.LabelField(minLabelRect, new GUIContent("Min"));
                EditorGUI.PropertyField(minFieldRect, minProp, GUIContent.none);
                EditorGUI.LabelField(maxLabelRect, new GUIContent("Max"));
                EditorGUI.PropertyField(maxFieldRect, maxProp, GUIContent.none);

                EditorGUI.EndProperty();
            }
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float labelWidth = EditorGUIUtility.labelWidth;
            float contentWidth = viewWidth - labelWidth;

            bool isNarrow = contentWidth < 200f;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return isNarrow ? lineHeight * 2 + spacing : lineHeight;
        }
    }
}