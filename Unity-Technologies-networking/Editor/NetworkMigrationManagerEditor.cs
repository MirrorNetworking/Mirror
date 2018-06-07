#if ENABLE_UNET
#if ENABLE_UNET_HOST_MIGRATION
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityEditor
{
    [CustomEditor(typeof(NetworkMigrationManager), true)]
    public class NetworkMigrationManagerEditor : Editor
    {
        bool m_Initialized;
        NetworkMigrationManager m_Manager;

        SerializedProperty m_HostMigrationProperty;
        SerializedProperty m_ShowGUIProperty;
        SerializedProperty m_OffsetXProperty;
        SerializedProperty m_OffsetYProperty;

        GUIContent m_HostMigrationLabel;
        GUIContent m_ShowGUILabel;
        GUIContent m_OffsetXLabel;
        GUIContent m_OffsetYLabel;

        bool m_ShowPeers;
        bool m_ShowPlayers;

        void Init()
        {
            if (m_Initialized)
            {
                if (m_HostMigrationProperty == null)
                {
                    // need to re-init. don't return
                }
                else
                {
                    return;
                }
            }

            m_Initialized = true;
            m_Manager = target as NetworkMigrationManager;

            m_HostMigrationProperty = serializedObject.FindProperty("m_HostMigration");
            m_ShowGUIProperty = serializedObject.FindProperty("m_ShowGUI");
            m_OffsetXProperty = serializedObject.FindProperty("m_OffsetX");
            m_OffsetYProperty = serializedObject.FindProperty("m_OffsetY");

            m_ShowGUILabel = new GUIContent("Show GUI", "Enable to display the default UI.");
            m_OffsetXLabel = new GUIContent("Offset X", "The horizonal offset of the GUI.");
            m_OffsetYLabel = new GUIContent("Offset Y", "The vertical offset of the GUI.");

            m_HostMigrationLabel = new GUIContent("Use Host Migration", "Enable to use host migration.");
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
            if (m_Manager == null)
                return;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_HostMigrationProperty, m_HostMigrationLabel);
            EditorGUILayout.PropertyField(m_ShowGUIProperty, m_ShowGUILabel);
            if (m_Manager.showGUI)
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

                //runtime data
                EditorGUILayout.LabelField("Disconnected From Host", m_Manager.disconnectedFromHost.ToString());
                EditorGUILayout.LabelField("Waiting to become New Host", m_Manager.waitingToBecomeNewHost.ToString());
                EditorGUILayout.LabelField("Waitingto Reconnect to New Host", m_Manager.waitingReconnectToNewHost.ToString());
                EditorGUILayout.LabelField("Your ConnectionId", m_Manager.oldServerConnectionId.ToString());
                EditorGUILayout.LabelField("New Host Address", m_Manager.newHostAddress);

                if (m_Manager.peers != null)
                {
                    m_ShowPeers = EditorGUILayout.Foldout(m_ShowPeers, "Peers");
                    if (m_ShowPeers)
                    {
                        EditorGUI.indentLevel += 1;
                        foreach (var peer in m_Manager.peers)
                        {
                            EditorGUILayout.LabelField("Peer: ", peer.ToString());
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                }

                if (m_Manager.pendingPlayers != null)
                {
                    m_ShowPlayers = EditorGUILayout.Foldout(m_ShowPlayers, "Pending Players");
                    if (m_ShowPlayers)
                    {
                        EditorGUI.indentLevel += 1;
                        foreach (var connId in m_Manager.pendingPlayers.Keys)
                        {
                            EditorGUILayout.LabelField("Connection: ", connId.ToString());
                            EditorGUI.indentLevel += 1;
                            var players = m_Manager.pendingPlayers[connId].players;
                            foreach (var p in players)
                            {
                                EditorGUILayout.ObjectField("Player netId:" + p.netId + " contId:" + p.playerControllerId, p.obj, typeof(GameObject), false);
                            }
                            EditorGUI.indentLevel -= 1;
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                }
            }
        }
    }
}
#endif
#endif
