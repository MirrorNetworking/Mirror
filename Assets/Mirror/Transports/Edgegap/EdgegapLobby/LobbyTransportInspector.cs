using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
namespace Edgegap
{
    [CustomEditor(typeof(EdgegapLobbyKcpTransport))]
    public class EncryptionTransportInspector : UnityEditor.Editor
    {
        SerializedProperty lobbyUrlProperty;


        // Assuming proper SerializedProperty definitions for properties
        // Add more SerializedProperty fields related to different modes as needed

        void OnEnable()
        {
            lobbyUrlProperty = serializedObject.FindProperty("lobbyUrl");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(lobbyUrlProperty);
            if (GUILayout.Button("Create&Deploy Lobby"))
            {
                var input = CreateInstance<LobbyServiceCreateDialogue>();
                input.onLobby = (url) =>
                {
                    lobbyUrlProperty.stringValue = url;
                    serializedObject.ApplyModifiedProperties();
                };
                input.ShowUtility();
            }
            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }
    }
}

#endif
