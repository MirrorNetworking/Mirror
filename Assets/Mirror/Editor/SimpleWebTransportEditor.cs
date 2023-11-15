using UnityEditor;
using Mirror.SimpleWeb;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(SimpleWebTransport))]
    public class SimpleWebTransportEditor : Editor
    {

        private SimpleWebTransport swt;
        private SerializedProperty property;
        private SerializedProperty clientPortOptionProp;
        private SerializedProperty specifyPortProp;
        private SerializedProperty serverPortProp;

        private void OnEnable()
        {
            clientPortOptionProp = serializedObject.FindProperty(nameof(SimpleWebTransport.clientPortOption));
            specifyPortProp = serializedObject.FindProperty(nameof(SimpleWebTransport.UserSpecifiedPort));
            serverPortProp = serializedObject.FindProperty(nameof(SimpleWebTransport.port));
        }
        public override void OnInspectorGUI()
        {
            swt = (SimpleWebTransport)target;
            serializedObject.Update();

            property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == nameof(swt.CustomizeClientPort))
                {
                    EditorGUILayout.PropertyField(property);
                    if (property.boolValue)
                    {
                        EditorGUILayout.PropertyField(clientPortOptionProp);
                        ClientPortOption portOption = (ClientPortOption)clientPortOptionProp.enumValueIndex;
                        if (portOption == ClientPortOption.SpecifyClientPort)
                        {
                            if (swt.UserSpecifiedPort == default)
                                specifyPortProp.intValue = serverPortProp.intValue;
                            EditorGUILayout.PropertyField(specifyPortProp, new GUIContent("Client Port"));
                        }
                    }
                    else
                        clientPortOptionProp.enumValueIndex = 0; // Reset enum value
                }
                else
                    EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
