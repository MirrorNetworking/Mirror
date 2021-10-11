// SyncVar<T> looks like this in the Inspector:
//   Health
//     Value: 42
// instead, let's draw ._Value directly so it looks like this:
//   Health: 42
//
// BUG: Unity also doesn't show custom drawer for readonly fields (#1368395)
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SyncVar<>))]
    public class SyncVarDrawer : PropertyDrawer
    {
        static readonly GUIContent syncVarIndicatorContent = new GUIContent("SyncVar<T>", "This variable is a SyncVar<T>.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Vector2 syncVarIndicatorRect = EditorStyles.miniLabel.CalcSize(syncVarIndicatorContent);
            float valueWidth = position.width - syncVarIndicatorRect.x;

            Rect valueRect = new Rect(position.x, position.y, valueWidth, position.height);
            Rect labelRect = new Rect(position.x + valueWidth, position.y, syncVarIndicatorRect.x, position.height);

            EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("_Value"), label, true);
            GUI.Label(labelRect, syncVarIndicatorContent, EditorStyles.miniLabel);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_Value"));
        }
    }
}
