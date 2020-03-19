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
        bool initialized;
        bool syncsAnything;
        SyncListDrawer syncListDrawer;
        /// <summary>
        /// List of all visable syncVars in target class
        /// </summary>
        protected List<string> syncVarNames = new List<string>();

        [System.Obsolete("Override OnInspectorGUI instead")]
        internal virtual bool HideScriptField => false;

        void OnEnable()
        {
            Init();
        }

        void Init()
        {
            serializedObject.Update();
            UnityEngine.Object target = serializedObject.targetObject;
            if (target == null) { Debug.LogWarningFormat("NetworkBehaviourInspector had no target object", serializedObject.context); return; }

            initialized = true;
            Type targetClass = target.GetType();

            // find visable SyncVars to show (user doesn't want protected ones to be shown in inspector)
            syncVarNames = new List<string>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetClass, typeof(NetworkBehaviour)))
            {
                if (field.IsSyncVar() && field.IsVisableInInspector())
                {
                    syncVarNames.Add(field.Name);
                }
            }

            syncListDrawer = new SyncListDrawer(serializedObject.targetObject);

            syncsAnything = SyncsAnything(targetClass);
        }

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

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DrawDefaultSyncLists();
            DrawDefaultSyncSettings();
        }

        protected void DrawDefaultSyncLists()
        {
            syncListDrawer.Draw();
        }
        protected void DrawDefaultSyncSettings()
        {
            // does it sync anything? then show extra properties
            // (no need to show it if the class only has Cmds/Rpcs and no sync)
            if (!syncsAnything)
            {
                return;
            }

            EditorGUILayout.LabelField("Sync Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncMode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncInterval"));

            // apply
            serializedObject.ApplyModifiedProperties();
        }
    }
    public class SyncListDrawer
    {
        private readonly UnityEngine.Object targetObject;
        private readonly List<SyncListField> syncListFields;

        public SyncListDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            syncListFields = new List<SyncListField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.IsSyncList() && field.IsVisableInInspector())
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
                drawSyncList(i);
            }
        }

        void drawSyncList(int i)
        {
            syncListFields[i].visable = EditorGUILayout.Foldout(syncListFields[i].visable, syncListFields[i].label);
            if (syncListFields[i].visable)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (syncListFields[i].field.GetValue(targetObject) is IEnumerable synclist)
                    {
                        int index = 0;
                        foreach (object item in synclist)
                        {
                            EditorGUILayout.LabelField("Element " + index, item.ToString());

                            index += 1;
                        }
                    }
                }
            }
        }

        class SyncListField
        {
            public bool visable;
            public readonly FieldInfo field;
            public readonly string label;

            public SyncListField(FieldInfo field)
            {
                this.field = field;
                visable = false;
                label = field.Name + "  [" + field.FieldType.Name + "]";
            }
        }
    }
}
