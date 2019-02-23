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
        bool m_Initialized;
        protected List<string> m_SyncVarNames = new List<string>();
        bool m_SyncsAnything;
        bool[] m_ShowSyncLists;

        readonly GUIContent m_SyncVarIndicatorContent = new GUIContent("SyncVar", "This variable has been marked with the [SyncVar] attribute.");

        internal virtual bool hideScriptField => false;

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
            // (look for 'Mirror.Sync'. not '.SyncObject' because we'd have to
            //  check base type for that again)
            foreach (FieldInfo field in scriptClass.GetFields())
            {
                if (field.FieldType.BaseType != null &&
                    field.FieldType.BaseType.FullName != null &&
                    field.FieldType.BaseType.FullName.Contains("Mirror.Sync"))
                {
                    return true;
                }
            }

            return false;
        }

        void Init(MonoScript script)
        {
            m_Initialized = true;
            Type scriptClass = script.GetClass();

            // find public SyncVars to show (user doesn't want protected ones to be shown in inspector)
            foreach (FieldInfo field in scriptClass.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Attribute[] fieldMarkers = (Attribute[])field.GetCustomAttributes(typeof(SyncVarAttribute), true);
                if (fieldMarkers.Length > 0)
                {
                    m_SyncVarNames.Add(field.Name);
                }
            }

            int numSyncLists = scriptClass.GetFields().Count(
                field => field.FieldType.BaseType != null &&
                         field.FieldType.BaseType.Name.Contains("SyncList"));
            if (numSyncLists > 0)
            {
                m_ShowSyncLists = new bool[numSyncLists];
            }

            m_SyncsAnything = SyncsAnything(scriptClass);
        }

        public override void OnInspectorGUI()
        {
            if (!m_Initialized)
            {
                serializedObject.Update();
                SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                    return;

                MonoScript targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            EditorGUI.BeginChangeCheck();
            serializedObject.Update();

            // Loop through properties and create one field (including children) for each top level property.
            SerializedProperty property = serializedObject.GetIterator();
            bool expanded = true;
            while (property.NextVisible(expanded))
            {
                bool isSyncVar = m_SyncVarNames.Contains(property.name);
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.name == "m_Script")
                    {
                        if (hideScriptField)
                        {
                            continue;
                        }

                        EditorGUI.BeginDisabledGroup(true);
                    }

                    EditorGUILayout.PropertyField(property, true);

                    if (isSyncVar)
                    {
                        GUILayout.Label(m_SyncVarIndicatorContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_SyncVarIndicatorContent).x));
                    }

                    if (property.name == "m_Script")
                    {
                        EditorGUI.EndDisabledGroup();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.PropertyField(property, true);

                    if (isSyncVar)
                    {
                        GUILayout.Label(m_SyncVarIndicatorContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_SyncVarIndicatorContent).x));
                    }

                    EditorGUILayout.EndHorizontal();
                }
                expanded = false;
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUI.EndChangeCheck();

            // find SyncLists.. they are not properties.
            int syncListIndex = 0;
            foreach (FieldInfo field in serializedObject.targetObject.GetType().GetFields())
            {
                if (field.FieldType.BaseType != null && field.FieldType.BaseType.Name.Contains("SyncList"))
                {
                    m_ShowSyncLists[syncListIndex] = EditorGUILayout.Foldout(m_ShowSyncLists[syncListIndex], "SyncList " + field.Name + "  [" + field.FieldType.Name + "]");
                    if (m_ShowSyncLists[syncListIndex])
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

            // only show SyncInterval if we have an OnSerialize function.
            // No need to show it if the class only has Cmds/Rpcs and no sync.
            if (m_SyncsAnything)
            {
                NetworkBehaviour networkBehaviour = target as NetworkBehaviour;
                if (networkBehaviour != null)
                {
                    // [0,2] should be enough. anything >2s is too laggy anyway.
                    serializedObject.FindProperty("syncInterval").floatValue = EditorGUILayout.Slider(
                        new GUIContent("Network Sync Interval",
                                       "Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)"),
                        networkBehaviour.syncInterval, 0, 2);
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
} //namespace
