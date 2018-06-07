#if ENABLE_UNET
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityEditor
{
    [CustomEditor(typeof(NetworkDiscovery), true)]
    [CanEditMultipleObjects]
    public class NetworkDiscoveryEditor : Editor
    {
        bool m_Initialized;
        NetworkDiscovery m_Discovery;

        SerializedProperty m_BroadcastPortProperty;
        SerializedProperty m_BroadcastKeyProperty;
        SerializedProperty m_BroadcastVersionProperty;
        SerializedProperty m_BroadcastSubVersionProperty;
        SerializedProperty m_BroadcastIntervalProperty;
        SerializedProperty m_UseNetworkManagerProperty;
        SerializedProperty m_BroadcastDataProperty;
        SerializedProperty m_ShowGUIProperty;
        SerializedProperty m_OffsetXProperty;
        SerializedProperty m_OffsetYProperty;

        GUIContent m_BroadcastPortLabel;
        GUIContent m_BroadcastKeyLabel;
        GUIContent m_BroadcastVersionLabel;
        GUIContent m_BroadcastSubVersionLabel;
        GUIContent m_BroadcastIntervalLabel;
        GUIContent m_UseNetworkManagerLabel;
        GUIContent m_BroadcastDataLabel;
        GUIContent m_ShowGUILabel;
        GUIContent m_OffsetXLabel;
        GUIContent m_OffsetYLabel;

        void Init()
        {
            if (m_Initialized)
            {
                if (m_BroadcastPortProperty == null)
                {
                    // need to re-init
                }
                else
                {
                    return;
                }
            }

            m_Initialized = true;
            m_Discovery = target as NetworkDiscovery;

            m_BroadcastPortProperty = serializedObject.FindProperty("m_BroadcastPort");
            m_BroadcastKeyProperty = serializedObject.FindProperty("m_BroadcastKey");
            m_BroadcastVersionProperty = serializedObject.FindProperty("m_BroadcastVersion");
            m_BroadcastSubVersionProperty = serializedObject.FindProperty("m_BroadcastSubVersion");
            m_BroadcastIntervalProperty = serializedObject.FindProperty("m_BroadcastInterval");
            m_UseNetworkManagerProperty = serializedObject.FindProperty("m_UseNetworkManager");
            m_BroadcastDataProperty = serializedObject.FindProperty("m_BroadcastData");
            m_ShowGUIProperty = serializedObject.FindProperty("m_ShowGUI");
            m_OffsetXProperty = serializedObject.FindProperty("m_OffsetX");
            m_OffsetYProperty = serializedObject.FindProperty("m_OffsetY");

            m_BroadcastPortLabel = new GUIContent("Broadcast Port", "The network port to broadcast to, and listen on.");
            m_BroadcastKeyLabel = new GUIContent("Broadcast Key", "The key to broadcast. This key typically identifies the application.");
            m_BroadcastVersionLabel = new GUIContent("Broadcast Version", "The version of the application to broadcast. This is used to match versions of the same application.");
            m_BroadcastSubVersionLabel = new GUIContent("Broadcast SubVersion", "The sub-version of the application to broadcast.");
            m_BroadcastIntervalLabel = new GUIContent("Broadcast Interval", "How often in milliseconds to broadcast when running as a server.");
            m_UseNetworkManagerLabel = new GUIContent("Use NetworkManager", "Broadcast information from the NetworkManager, and auto-join matching games using the NetworkManager.");
            m_BroadcastDataLabel = new GUIContent("Broadcast Data", "The data to broadcast when not using the NetworkManager");
            m_ShowGUILabel = new GUIContent("Show GUI", "Enable to draw the default broadcast control UI.");
            m_OffsetXLabel = new GUIContent("Offset X", "The horizonal offset of the GUI.");
            m_OffsetYLabel = new GUIContent("Offset Y", "The vertical offset of the GUI.");
        }

        public override void OnInspectorGUI()
        {
            Init();
            serializedObject.Update();
            DrawControls();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawControls()
        {
            if (m_Discovery == null)
                return;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_BroadcastPortProperty, m_BroadcastPortLabel);

            EditorGUILayout.PropertyField(m_BroadcastKeyProperty, m_BroadcastKeyLabel);
            EditorGUILayout.PropertyField(m_BroadcastVersionProperty, m_BroadcastVersionLabel);
            EditorGUILayout.PropertyField(m_BroadcastSubVersionProperty, m_BroadcastSubVersionLabel);
            EditorGUILayout.PropertyField(m_BroadcastIntervalProperty, m_BroadcastIntervalLabel);
            EditorGUILayout.PropertyField(m_UseNetworkManagerProperty, m_UseNetworkManagerLabel);
            if (m_Discovery.useNetworkManager)
            {
                EditorGUILayout.LabelField(m_BroadcastDataLabel, new GUIContent(m_BroadcastDataProperty.stringValue));
            }
            else
            {
                EditorGUILayout.PropertyField(m_BroadcastDataProperty, m_BroadcastDataLabel);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.PropertyField(m_ShowGUIProperty, m_ShowGUILabel);
            if (m_Discovery.showGUI)
            {
                EditorGUILayout.PropertyField(m_OffsetXProperty, m_OffsetXLabel);
                EditorGUILayout.PropertyField(m_OffsetYProperty, m_OffsetYLabel);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("hostId", m_Discovery.hostId.ToString());
                EditorGUILayout.LabelField("running", m_Discovery.running.ToString());
                EditorGUILayout.LabelField("isServer", m_Discovery.isServer.ToString());
                EditorGUILayout.LabelField("isClient", m_Discovery.isClient.ToString());
            }
        }
    }
}
#endif
