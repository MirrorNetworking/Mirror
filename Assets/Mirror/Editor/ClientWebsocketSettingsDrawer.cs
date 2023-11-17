using Mirror.SimpleWeb;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ClientWebsocketSettings))]
    public class ClientWebsocketSettingsDrawer : PropertyDrawer
    {
        private readonly string websocketPortOptionName = nameof(ClientWebsocketSettings.ClientPortOption);
        private readonly string customPortName = nameof(ClientWebsocketSettings.CustomClientPort);
        private readonly string websocketPathOptionName = nameof(ClientWebsocketSettings.ClientPathOption);
        private readonly string customPathName = nameof(ClientWebsocketSettings.CustomClientPath);
        private readonly GUIContent portOptionLabel =  new ("Client Port Option",
            "Specify what port the client websocket connection uses (default same as server port)");
        private readonly GUIContent pathOptionLabel = new("Client Path Option",
            "Customize which path the client websocket connection uses (default no path)");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            property.isExpanded = true;
            return SumPropertyHeights(property, websocketPortOptionName, customPortName,
                websocketPathOptionName, customPathName);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position = DrawPortSettings(position, property);
            DrawPathSettings(position, property);
        }

        private Rect DrawPortSettings(Rect position, SerializedProperty property)
        {
            var portOptionProp = property.FindPropertyRelative(websocketPortOptionName);
            var portProp = property.FindPropertyRelative(customPortName);
            var portOptionHeight = EditorGUI.GetPropertyHeight(portOptionProp);
            var portHeight = EditorGUI.GetPropertyHeight(portProp);
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var wasEnabled = GUI.enabled;

            position.height = portOptionHeight;

            EditorGUI.PropertyField(position, portOptionProp, portOptionLabel);
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
                EditorGUI.IntField(position, new GUIContent("Client Port"), port);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, portProp);
            position.y += spacing + portHeight;
            return position;
        }

        private Rect DrawPathSettings(Rect position, SerializedProperty property)
        {
            var pathOptionProp = property.FindPropertyRelative(websocketPathOptionName);
            var pathProp = property.FindPropertyRelative(customPathName);
            var pathOptionHeight = EditorGUI.GetPropertyHeight(pathOptionProp);
            var pathHeight = EditorGUI.GetPropertyHeight(pathProp);
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var wasEnabled = GUI.enabled;

            position.height = pathOptionHeight;
            EditorGUI.PropertyField(position, pathOptionProp, pathOptionLabel);
            position.y += spacing + pathOptionHeight;
            position.height = pathHeight;

            var pathOption = (WebsocketPathOption)pathOptionProp.enumValueIndex;
            var path = pathOption == WebsocketPathOption.DefaultNone ? "" : pathProp.stringValue;
            if (pathOption == WebsocketPathOption.DefaultNone)
            {
                GUI.enabled = false;
                EditorGUI.TextField(position, new GUIContent("Client Path"), path);
                GUI.enabled = wasEnabled;
            }
            else
                EditorGUI.PropertyField(position, pathProp);
            return position;
        }

        private float SumPropertyHeights(SerializedProperty property, params string[] propertyNames)
        {
            float totalHeight = 0;
            foreach (var name in propertyNames)
                totalHeight += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name)) + EditorGUIUtility.standardVerticalSpacing;
            return totalHeight;
        }
    }
#endif
}
