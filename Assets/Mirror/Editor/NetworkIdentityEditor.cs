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

        readonly GUIContent serverOnlyLabel = new GUIContent("Server Only", "True if the object should only exist on the server.");
        readonly GUIContent spawnLabel = new GUIContent("Spawn Object", "This causes an unspawned server object to be spawned on clients");

        NetworkIdentity networkIdentity;
        bool showObservers;

        void Init()
        {
            if (serverOnlyProperty == null)
            {
                networkIdentity = target as NetworkIdentity;

                serverOnlyProperty = serializedObject.FindProperty("serverOnly");
            }
        }

        public override void OnInspectorGUI()
        {
            Init();

            serializedObject.Update();

            EditorGUILayout.PropertyField(serverOnlyProperty, serverOnlyLabel);

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
                        if (kvp.Value.identity != null)
                            EditorGUILayout.ObjectField(kvp.Value.ToString(), kvp.Value.identity.gameObject, typeof(GameObject), false);
                        else
                            EditorGUILayout.TextField(kvp.Value.ToString());
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
