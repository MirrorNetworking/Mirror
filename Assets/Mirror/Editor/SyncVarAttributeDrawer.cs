using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SyncVarAttribute))]
    public class SyncVarAttributeDrawer : PropertyDrawer
    {
        static readonly GUIContent syncVarIndicatorContent = new GUIContent("SyncVar", "This variable has been marked with the [SyncVar] attribute.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Vector2 syncVarIndicatorRect = EditorStyles.miniLabel.CalcSize(syncVarIndicatorContent);
            float valueWidth = position.width - syncVarIndicatorRect.x;

            Rect valueRect = new Rect(position.x, position.y, valueWidth, position.height);
            Rect labelRect = new Rect(position.x + valueWidth, position.y, syncVarIndicatorRect.x, position.height);

            EditorGUI.PropertyField(valueRect, property, true);
            GUI.Label(labelRect, syncVarIndicatorContent, EditorStyles.miniLabel);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property);
        }
    }
}
