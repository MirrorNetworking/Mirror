using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(NetworkBehaviour), true)]
    [CanEditMultipleObjects]
    public class NetworkBehaviourInspector : Editor
    {
        /// <summary>
        /// List of all visible syncVars in target class
        /// </summary>
        protected List<string> syncVarNames = new List<string>();
        bool syncsAnything;
        bool[] showSyncLists;

        // does this type sync anything? otherwise we don't need to show syncInterval
        bool SyncsAnything(Type scriptClass)
        {
            // has OnSerialize that is not in NetworkBehaviour?
            // then it either has a syncvar or custom OnSerialize. either way
            // this means we have something to sync.
            MethodInfo method = scriptClass.GetMethod("OnSerialize");
            if (method != null && method.DeclaringType != typeof(NetworkBehaviour))
            {
                return true;
            }

            // SyncObjects are serialized in NetworkBehaviour.OnSerialize, which
            // is always there even if we don't use SyncObjects. so we need to
            // search for SyncObjects manually.
            // Any SyncObject should be added to syncObjects when unity creates an
            // object so we can cheeck length of list so see if sync objects exists
            FieldInfo syncObjectsField = scriptClass.GetField("syncObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            List<SyncObject> syncObjects = (List<SyncObject>)syncObjectsField.GetValue(serializedObject.targetObject);

            return syncObjects.Count > 0;
        }

        void OnEnable()
        {
            serializedObject.Update();
            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            if (scriptProperty == null)
                return;

            MonoScript targetScript = scriptProperty.objectReferenceValue as MonoScript;


            Type scriptClass = targetScript.GetClass();

            syncVarNames = new List<string>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(scriptClass, typeof(NetworkBehaviour)))
            {
                if (field.IsSyncVar() && field.IsVisibleField())
                {
                    syncVarNames.Add(field.Name);
                }
            }

            int numSyncLists = InspectorHelper.GetAllFields(serializedObject.targetObject.GetType(), typeof(NetworkBehaviour))
                .Count(field => field.IsSyncObject() && field.IsVisibleSyncObject());

            if (numSyncLists > 0)
            {
                showSyncLists = new bool[numSyncLists];
            }

            syncsAnything = SyncsAnything(scriptClass);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (showSyncLists.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sync Lists", EditorStyles.boldLabel);
            }

            // find SyncLists.. they are not properties.
            int syncListIndex = 0;
            foreach (FieldInfo field in InspectorHelper.GetAllFields(serializedObject.targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.IsSyncObject() && field.IsVisibleSyncObject())
                {
                    showSyncLists[syncListIndex] = EditorGUILayout.Foldout(showSyncLists[syncListIndex], "SyncList " + field.Name + "  [" + field.FieldType.Name + "]");
                    if (showSyncLists[syncListIndex])
                    {
                        EditorGUI.indentLevel += 1;
                        if (field.GetValue(serializedObject.targetObject) is IEnumerable synclist)
                        {
                            int index = 0;
                            IEnumerator enu = synclist.GetEnumerator();
                            while (enu.MoveNext())
                            {
                                if (enu.Current != null)
                                {
                                    EditorGUILayout.LabelField("Item:" + index, enu.Current.ToString());
                                }
                                index += 1;
                            }
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                    syncListIndex += 1;
                }
            }

            // does it sync anything? then show extra properties
            // (no need to show it if the class only has Cmds/Rpcs and no sync)
            if (syncsAnything)
            {
                NetworkBehaviour networkBehaviour = target as NetworkBehaviour;
                if (networkBehaviour != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Sync Settings", EditorStyles.boldLabel);

                    // syncMode
                    serializedObject.FindProperty("syncMode").enumValueIndex = (int)(SyncMode)
                        EditorGUILayout.EnumPopup("Network Sync Mode", networkBehaviour.syncMode);

                    // syncInterval
                    // [0,2] should be enough. anything >2s is too laggy anyway.
                    serializedObject.FindProperty("syncInterval").floatValue = EditorGUILayout.Slider(
                        new GUIContent("Network Sync Interval",
                                       "Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)"),
                        networkBehaviour.syncInterval, 0, 2);

                    // apply
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
} //namespace
