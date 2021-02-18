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
        /// <summary>
        /// List of all visible syncVars in target class
        /// </summary>
        protected List<string> syncVarNames = new List<string>();
        bool syncsAnything;
        SyncListDrawer syncListDrawer;

        // does this type sync anything? otherwise we don't need to show syncInterval
        bool SyncsAnything(Type scriptClass)
        {
            // check for all SyncVar fields, they don't have to be visible
            foreach (FieldInfo field in InspectorHelper.GetAllFields(scriptClass, typeof(NetworkBehaviour)))
            {
                if (field.IsSyncVar())
                {
                    return true;
                }
            }

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
            // object so we can check length of list so see if sync objects exists
            FieldInfo syncObjectsField = scriptClass.GetField("syncObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            List<SyncObject> syncObjects = (List<SyncObject>)syncObjectsField.GetValue(serializedObject.targetObject);

            return syncObjects.Count > 0;
        }

        void OnEnable()
        {
            if (target == null) { Debug.LogWarning("NetworkBehaviourInspector had no target object"); return; }

            // If target's base class is changed from NetworkBehaviour to MonoBehaviour
            // then Unity temporarily keep using this Inspector causing things to break
            if (!(target is NetworkBehaviour)) { return; }

            Type scriptClass = target.GetType();

            syncVarNames = new List<string>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(scriptClass, typeof(NetworkBehaviour)))
            {
                if (field.IsSyncVar() && field.IsVisibleField())
                {
                    syncVarNames.Add(field.Name);
                }
            }

            syncListDrawer = new SyncListDrawer(serializedObject.targetObject);

            syncsAnything = SyncsAnything(scriptClass);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawDefaultSyncLists();
            DrawDefaultSyncSettings();
        }

        /// <summary>
        /// Draws Sync Objects that are IEnumerable
        /// </summary>
        protected void DrawDefaultSyncLists()
        {
            // Need this check in case OnEnable returns early
            if (syncListDrawer == null) { return; }

            syncListDrawer.Draw();
        }

        /// <summary>
        /// Draws SyncSettings if the NetworkBehaviour has anything to sync
        /// </summary>
        protected void DrawDefaultSyncSettings()
        {
            // does it sync anything? then show extra properties
            // (no need to show it if the class only has Cmds/Rpcs and no sync)
            if (!syncsAnything)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncInterval"));

            // apply
            serializedObject.ApplyModifiedProperties();
        }
    }
    public class SyncListDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<SyncListField> syncListFields;

        public SyncListDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            syncListFields = new List<SyncListField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.IsSyncObject() && field.IsVisibleSyncObject())
                {
                    syncListFields.Add(new SyncListField(field));
                }
            }
        }

        public void Draw()
        {
            if (syncListFields.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Lists", EditorStyles.boldLabel);

            for (int i = 0; i < syncListFields.Count; i++)
            {
                DrawSyncList(syncListFields[i]);
            }
        }

        void DrawSyncList(SyncListField syncListField)
        {
            syncListField.visible = EditorGUILayout.Foldout(syncListField.visible, syncListField.label);
            if (syncListField.visible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    object fieldValue = syncListField.field.GetValue(targetObject);
                    if (fieldValue is IEnumerable synclist)
                    {
                        int index = 0;
                        foreach (object item in synclist)
                        {
                            string itemValue = item != null ? item.ToString() : "NULL";
                            string itemLabel = "Element " + index;
                            EditorGUILayout.LabelField(itemLabel, itemValue);

                            index++;
                        }
                    }
                }
            }
        }

        class SyncListField
        {
            public bool visible;
            public readonly FieldInfo field;
            public readonly string label;

            public SyncListField(FieldInfo field)
            {
                this.field = field;
                visible = false;
                label = field.Name + "  [" + field.FieldType.Name + "]";
            }
        }
    }
}
