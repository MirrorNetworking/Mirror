using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SyncVarAttribute))]
    public class SyncVarAttributeDrawer : PropertyDrawer
    {
        static readonly GUIContent syncVarIndicatorContent = new GUIContent("SyncVar", "This variable has been marked with the [SyncVar] attribute.");

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            Vector2 syncVarIndicatorRect = EditorStyles.miniLabel.CalcSize(syncVarIndicatorContent);
            float valueWidth = rect.width - syncVarIndicatorRect.x;

            Rect valueRect = new Rect(rect.x, rect.y, valueWidth, rect.height);
            Rect labelRect = new Rect(rect.x + valueWidth, rect.y, syncVarIndicatorRect.x, rect.height);

            EditorGUI.PropertyField(valueRect, property, true);
            GUI.Label(labelRect, syncVarIndicatorContent, EditorStyles.miniLabel);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }
    }
} //namespace
