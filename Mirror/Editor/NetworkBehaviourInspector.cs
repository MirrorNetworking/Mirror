using System;
using System.Collections;
using System.Collections.Generic;
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
        bool m_HasOnSerialize;
        bool[] m_ShowSyncLists;

        GUIContent m_SyncVarIndicatorContent;
        protected GUIContent m_NetworkSendIntervalLabel;

        internal virtual bool hideScriptField
        {
            get { return false; }
        }

        void Init(MonoScript script)
        {
            m_Initialized = true;
            Type scriptClass = script.GetClass();

            m_SyncVarIndicatorContent = new GUIContent("SyncVar", "This variable has been marked with the [SyncVar] attribute.");
            m_NetworkSendIntervalLabel = new GUIContent("Network Send Interval", "Maximum update rate in seconds. Use the [NetworkSettings] class attribute to change this, or implement GetNetworkSendInterval");

            foreach (var field in scriptClass.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Attribute[] fieldMarkers = (Attribute[])field.GetCustomAttributes(typeof(SyncVarAttribute), true);
                if (fieldMarkers.Length > 0)
                {
                    m_SyncVarNames.Add(field.Name);
                }
            }
            var meth = script.GetClass().GetMethod("OnSerialize");
            if (meth != null)
            {
                if (meth.DeclaringType != typeof(NetworkBehaviour))
                {
                    m_HasOnSerialize = true;
                }
            }

            int numSyncLists = 0;
            foreach (var f in serializedObject.targetObject.GetType().GetFields())
            {
                if (f.FieldType.BaseType != null && f.FieldType.BaseType.Name.Contains("SyncList"))
                {
                    numSyncLists += 1;
                }
            }
            if (numSyncLists > 0)
            {
                m_ShowSyncLists = new bool[numSyncLists];
            }
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
            foreach (var field in serializedObject.targetObject.GetType().GetFields())
            {
                if (field.FieldType.BaseType != null && field.FieldType.BaseType.Name.Contains("SyncList"))
                {
                    m_ShowSyncLists[syncListIndex] = EditorGUILayout.Foldout(m_ShowSyncLists[syncListIndex], "SyncList " + field.Name + "  [" + field.FieldType.Name + "]");
                    if (m_ShowSyncLists[syncListIndex])
                    {
                        EditorGUI.indentLevel += 1;
                        var synclist = field.GetValue(serializedObject.targetObject) as IEnumerable;
                        if (synclist != null)
                        {
                            int index = 0;
                            var enu = synclist.GetEnumerator();
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

            if (m_HasOnSerialize)
            {
                var beh = target as NetworkBehaviour;
                if (beh != null)
                {
                    EditorGUILayout.LabelField(m_NetworkSendIntervalLabel, new GUIContent(beh.GetNetworkSendInterval().ToString()));
                }
            }
        }
    }
} //namespace
