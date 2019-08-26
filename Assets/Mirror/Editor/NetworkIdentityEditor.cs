using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkIdentity), true)]
    [CanEditMultipleObjects]
    public class NetworkIdentityEditor : Editor
    {
        SerializedProperty serverOnlyProperty;
        SerializedProperty localPlayerAuthorityProperty;

        readonly GUIContent serverOnlyLabel = new GUIContent("Server Only", "True if the object should only exist on the server.");
        readonly GUIContent localPlayerAuthorityLabel = new GUIContent("Local Player Authority", "True if this object will be controlled by a player on a client.");
        readonly GUIContent spawnLabel = new GUIContent("Spawn Object", "This causes an unspawned server object to be spawned on clients");

        NetworkIdentity networkIdentity;
        bool initialized;
        bool showObservers;

        void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            networkIdentity = target as NetworkIdentity;

            serverOnlyProperty = serializedObject.FindProperty("serverOnly");
            localPlayerAuthorityProperty = serializedObject.FindProperty("localPlayerAuthority");
        }

        public override void OnInspectorGUI()
        {
            if (serverOnlyProperty == null)
            {
                initialized = false;
            }

            Init();

            serializedObject.Update();

            if (serverOnlyProperty.boolValue)
            {
                EditorGUILayout.PropertyField(serverOnlyProperty, serverOnlyLabel);
                EditorGUILayout.LabelField("Local Player Authority cannot be set for server-only objects");
            }
            else if (localPlayerAuthorityProperty.boolValue)
            {
                EditorGUILayout.LabelField("Server Only cannot be set for Local Player Authority objects");
                EditorGUILayout.PropertyField(localPlayerAuthorityProperty, localPlayerAuthorityLabel);
            }
            else
            {
                EditorGUILayout.PropertyField(serverOnlyProperty, serverOnlyLabel);
                EditorGUILayout.PropertyField(localPlayerAuthorityProperty, localPlayerAuthorityLabel);
            }

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            // Runtime actions below here

            EditorGUILayout.Separator();

            if (networkIdentity.observers != null && networkIdentity.observers.Count > 0)
            {
                showObservers = EditorGUILayout.Foldout(showObservers, "Observers");
                if (showObservers)
                {
                    EditorGUI.indentLevel += 1;
                    foreach (KeyValuePair<int, NetworkConnection> kvp in networkIdentity.observers)
                    {
                        if (kvp.Value.playerController != null)
                            EditorGUILayout.ObjectField("Connection " + kvp.Value.connectionId, kvp.Value.playerController.gameObject, typeof(GameObject), false);
                        else
                            EditorGUILayout.TextField("Connection " + kvp.Value.connectionId);
                    }
                    EditorGUI.indentLevel -= 1;
                }
            }

            if (PrefabUtility.IsPartOfPrefabAsset(networkIdentity.gameObject))
                return;

            if (networkIdentity.gameObject.activeSelf && networkIdentity.netId == 0 && NetworkServer.active)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(spawnLabel);
                if (GUILayout.Toggle(false, "Spawn", EditorStyles.miniButtonLeft))
                {
                    NetworkServer.Spawn(networkIdentity.gameObject);
                    EditorUtility.SetDirty(target);  // preview window STILL doens't update immediately..
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
