using UnityEditor;
using UnityEngine;

namespace Mirror.SimpleWeb.Editor
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ClientWebsocketSettings))]
    public class ClientWebsocketSettingsDrawer : PropertyDrawer
    {
        readonly string websocketPortOptionName = nameof(ClientWebsocketSettings.ClientPortOption);
        readonly string customPortName = nameof(ClientWebsocketSettings.CustomClientPort);
        readonly GUIContent portOptionLabel =  new GUIContent("Client Port Option",
            "Specify what port the client websocket connection uses (default same as server port)");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            property.isExpanded = true;
            return SumPropertyHeights(property, websocketPortOptionName, customPortName);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawPortSettings(position, property);
        }

        void DrawPortSettings(Rect position, SerializedProperty property)
        {
            SerializedProperty portOptionProp = property.FindPropertyRelative(websocketPortOptionName);
            SerializedProperty portProp = property.FindPropertyRelative(customPortName);
            float portOptionHeight = EditorGUI.GetPropertyHeight(portOptionProp);
            float portHeight = EditorGUI.GetPropertyHeight(portProp);
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            bool wasEnabled = GUI.enabled;

            position.height = portOptionHeight;

            EditorGUI.PropertyField(position, portOptionProp, portOptionLabel);
            position.y += spacing + portOptionHeight;
            position.height = portHeight;

            WebsocketPortOption portOption = (WebsocketPortOption)portOptionProp.enumValueIndex;
            if (portOption == WebsocketPortOption.MatchWebpageProtocol || portOption == WebsocketPortOption.DefaultSameAsServer)
            {
                int port = 0;
                if (property.serializedObject.targetObject is SimpleWebTransport swt)
                    if (portOption == WebsocketPortOption.MatchWebpageProtocol)
                        port = swt.clientUseWss ? 443 : 80;
                    else
                        port = swt.port;

                GUI.enabled = false;
                EditorGUI.IntField(position, new GUIContent("Client Port"), port);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, portProp);

            position.y += spacing + portHeight;
        }

        float SumPropertyHeights(SerializedProperty property, params string[] propertyNames)
        {
            float totalHeight = 0;
            foreach (var name in propertyNames)
                totalHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name)) + EditorGUIUtility.standardVerticalSpacing;

            return totalHeight;
        }
    }
#endif
}
