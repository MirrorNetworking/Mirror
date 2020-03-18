using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SyncVarAttribute))]
    public class SyncVarAttributeDrawer : PropertyDrawer
    {
        static readonly GUIContent syncVarIndicatorContent = new GUIContent("SyncVar", "This variable has been marked with the [SyncVar] attribute.");

        public override void OnGUI(Rect propRect, SerializedProperty property, GUIContent label)
        {
            Vector2 syncVarIndicatorRect = EditorStyles.miniLabel.CalcSize(syncVarIndicatorContent);
            float valueWidth = propRect.width - syncVarIndicatorRect.x;

            Rect valueRect = new Rect(propRect.x, propRect.y, valueWidth, propRect.height);
            Rect labelRect = new Rect(propRect.x + valueWidth, propRect.y, syncVarIndicatorRect.x, propRect.height);

            EditorGUI.PropertyField(valueRect, property, true);
            GUI.Label(labelRect, syncVarIndicatorContent, EditorStyles.miniLabel);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }
    }
} //namespace
