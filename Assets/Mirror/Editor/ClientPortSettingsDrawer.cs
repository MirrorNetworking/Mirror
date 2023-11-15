using Mirror.SimpleWeb;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ClientPortSettings))]
    public class ClientPortSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            property.isExpanded = true;

            var optionHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientPortSettings.Options)));
            var portHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientPortSettings.CustomPort)));
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            return optionHeight + spacing + portHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var optionProp = property.FindPropertyRelative(nameof(ClientPortSettings.Options));
            var portProp = property.FindPropertyRelative(nameof(ClientPortSettings.CustomPort));

            var optionHeight = EditorGUI.GetPropertyHeight(optionProp);
            var portHeight = EditorGUI.GetPropertyHeight(portProp);
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            position.height = optionHeight;
            EditorGUI.PropertyField(position, optionProp);
            position.y += spacing + optionHeight;
            position.height = portHeight;

            var option = (ClientPortOptions)optionProp.enumValueIndex;
            if (option == ClientPortOptions.DefaultPort || option == ClientPortOptions.SameAsServer)
            {
                var port = 0;
                if (property.serializedObject.targetObject is SimpleWebTransport swt)
                {
                    if (option == ClientPortOptions.DefaultPort)
                        port = swt.clientUseWss ? 443 : 80;
                    else
                        port = swt.port;
                }

                var wasEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUI.IntField(position, new GUIContent("Client Port"), port);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, portProp);
        }
    }
#endif
}
