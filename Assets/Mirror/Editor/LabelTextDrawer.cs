using UnityEngine;
using UnityEditor;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(LabelTextAttribute))]
    public class LabelTextDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Get the attribute
            LabelTextAttribute labelTextAttribute =
                (LabelTextAttribute)fieldInfo.GetCustomAttributes(typeof(LabelTextAttribute), false)[0];

            if (labelTextAttribute != null)
            {
                // Change the label text
                label.text = labelTextAttribute.LabelText;
            }

            // Now draw the property
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
