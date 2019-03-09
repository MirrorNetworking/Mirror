using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    [CanEditMultipleObjects]
    public class NetworkIdentityEditor : Editor
    {
        SerializedProperty m_ServerOnlyProperty;
        SerializedProperty m_LocalPlayerAuthorityProperty;

        GUIContent m_ServerOnlyLabel = new GUIContent("Server Only", "True if the object should only exist on the server.");
        GUIContent m_LocalPlayerAuthorityLabel = new GUIContent("Local Player Authority", "True if this object will be controlled by a player on a client.");
        GUIContent m_SpawnLabel = new GUIContent("Spawn Object", "This causes an unspawned server object to be spawned on clients");

        NetworkIdentity m_NetworkIdentity;
        bool m_Initialized;
        bool m_ShowObservers;

        void Init()
        {
            if (m_Initialized)
            {
                return;
            }
            m_Initialized = true;
            m_NetworkIdentity = target as NetworkIdentity;

            m_ServerOnlyProperty = serializedObject.FindProperty("m_ServerOnly");
            m_LocalPlayerAuthorityProperty = serializedObject.FindProperty("m_LocalPlayerAuthority");
        }

        public override void OnInspectorGUI()
        {
            if (m_ServerOnlyProperty == null)
            {
                m_Initialized = false;
            }

            Init();

            serializedObject.Update();

            if (m_ServerOnlyProperty.boolValue)
            {
                EditorGUILayout.PropertyField(m_ServerOnlyProperty, m_ServerOnlyLabel);
                EditorGUILayout.LabelField("Local Player Authority cannot be set for server-only objects");
            }
            else if (m_LocalPlayerAuthorityProperty.boolValue)
            {
                EditorGUILayout.LabelField("Server Only cannot be set for Local Player Authority objects");
                EditorGUILayout.PropertyField(m_LocalPlayerAuthorityProperty, m_LocalPlayerAuthorityLabel);
            }
            else
            {
                EditorGUILayout.PropertyField(m_ServerOnlyProperty, m_ServerOnlyLabel);
                EditorGUILayout.PropertyField(m_LocalPlayerAuthorityProperty, m_LocalPlayerAuthorityLabel);
            }

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            // Runtime actions below here

            EditorGUILayout.Separator();

            if (m_NetworkIdentity.observers != null && m_NetworkIdentity.observers.Count > 0)
            {
                m_ShowObservers = EditorGUILayout.Foldout(m_ShowObservers, "Observers");
                if (m_ShowObservers)
                {
                    EditorGUI.indentLevel += 1;
                    foreach (KeyValuePair<int, NetworkConnection> kvp in m_NetworkIdentity.observers)
                    {
                        if (kvp.Value.playerController != null)
                            EditorGUILayout.ObjectField("Connection " + kvp.Value.connectionId, kvp.Value.playerController.gameObject, typeof(GameObject), false);
                        else
                            EditorGUILayout.TextField("Connection " + kvp.Value.connectionId);
                    }
                    EditorGUI.indentLevel -= 1;
                }
            }

            if (PrefabUtility.IsPartOfPrefabAsset(m_NetworkIdentity.gameObject))
                return;

            if (m_NetworkIdentity.gameObject.activeSelf && m_NetworkIdentity.netId == 0 && NetworkServer.active)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(m_SpawnLabel);
                if (GUILayout.Toggle(false, "Spawn", EditorStyles.miniButtonLeft))
                {
                    NetworkServer.Spawn(m_NetworkIdentity.gameObject);
                    EditorUtility.SetDirty(target);  // preview window STILL doens't update immediately..
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
