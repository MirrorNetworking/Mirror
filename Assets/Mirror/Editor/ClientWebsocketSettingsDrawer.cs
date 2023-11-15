using Mirror.SimpleWeb;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ClientWebsocketSettings))]
    public class ClientWebsocketSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            property.isExpanded = true;

            var portOptionHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientWebsocketSettings.websocketPortOption)));
            var portHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientWebsocketSettings.CustomPort)));
            var pathOptionHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientWebsocketSettings.websocketPathOption)));
            var pathHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ClientWebsocketSettings.CustomPath)));
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            return portOptionHeight + spacing + portHeight + spacing + pathOptionHeight + spacing + pathHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var portOptionProp = property.FindPropertyRelative(nameof(ClientWebsocketSettings.websocketPortOption));
            var portProp = property.FindPropertyRelative(nameof(ClientWebsocketSettings.CustomPort));
            var pathOptionProp = property.FindPropertyRelative(nameof(ClientWebsocketSettings.websocketPathOption));
            var pathProp = property.FindPropertyRelative(nameof(ClientWebsocketSettings.CustomPath));

            var portOptionHeight = EditorGUI.GetPropertyHeight(portOptionProp);
            var portHeight = EditorGUI.GetPropertyHeight(portProp);
            var pathOptionHeight = EditorGUI.GetPropertyHeight(pathOptionProp);
            var pathHeight = EditorGUI.GetPropertyHeight(pathProp);
            var spacing = EditorGUIUtility.standardVerticalSpacing;

            var wasEnabled = GUI.enabled;
            

            position.height = portOptionHeight;
            EditorGUI.PropertyField(position, portOptionProp);
            position.y += spacing + portOptionHeight;
            position.height = portHeight;

            var portOption = (WebsocketPortOption)portOptionProp.enumValueIndex;
            if (portOption == WebsocketPortOption.MatchWebpageProtocol || portOption == WebsocketPortOption.DefaultSameAsServer)
            {
                var port = 0;
                if (property.serializedObject.targetObject is SimpleWebTransport swt)
                {
                    if (portOption == WebsocketPortOption.MatchWebpageProtocol)
                        port = swt.clientUseWss ? 443 : 80;
                    else
                        port = swt.port;
                }

                GUI.enabled = false;
                EditorGUI.IntField(position, new GUIContent("Websocket Port"), port);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, portProp);
            position.y += spacing + portHeight;

            position.height = pathOptionHeight;
            EditorGUI.PropertyField(position, pathOptionProp);
            position.y += spacing + pathOptionHeight;
            position.height = pathHeight;

            var pathOption = (WebsocketPathOption)pathOptionProp.enumValueIndex;
            var path = pathOption == WebsocketPathOption.DefaultWebsocketPath ? "/" : pathProp.stringValue;
            if (pathOption == WebsocketPathOption.DefaultWebsocketPath)
            {
                GUI.enabled = false;
                EditorGUI.TextField(position, new GUIContent("Websocket Path"), path);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, pathProp);
            
        }
    }
#endif
}
