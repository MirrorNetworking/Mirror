// helper class for NetworkBehaviourInspector
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Mirror
{
    class SyncObjectField
    {
        public bool visible;
        public readonly FieldInfo field;
        public readonly string label;

        public SyncObjectField(FieldInfo field)
        {
            this.field = field;
            visible = false;
            label = $"{field.Name}  [{field.FieldType.Name}]";
        }
    }

    public class SyncObjectDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<SyncObjectField> syncObjectFields;

        public SyncObjectDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            syncObjectFields = new List<SyncObjectField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.ImplementsInterface<SyncObject>() && field.IsVisibleInInspector())
                {
                    syncObjectFields.Add(new SyncObjectField(field));
                }
            }
        }

        public void Draw()
        {
            if (syncObjectFields.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Objects", EditorStyles.boldLabel);

            for (int i = 0; i < syncObjectFields.Count; i++)
            {
                DrawSyncObject(syncObjectFields[i]);
            }
        }

        void DrawSyncObject(SyncObjectField syncObjectField)
        {
            syncObjectField.visible = EditorGUILayout.Foldout(syncObjectField.visible, syncObjectField.label);
            if (syncObjectField.visible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    object fieldValue = syncObjectField.field.GetValue(targetObject);
                    if (fieldValue is IEnumerable synclist)
                    {
                        int index = 0;
                        foreach (object item in synclist)
                        {
                            string itemValue = item != null ? item.ToString() : "NULL";
                            string itemLabel = $"Element {index}";
                            EditorGUILayout.LabelField(itemLabel, itemValue);

                            index++;
                        }
                    }
                }
            }
        }
    }
}
