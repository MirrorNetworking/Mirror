// helper class for NetworkBehaviourInspector to draw all enumerable SyncObjects
// (SyncList/Set/Dictionary)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Mirror
{
    class EnuerableSyncObjectField
    {
        public bool visible;
        public readonly FieldInfo field;
        public readonly string label;

        public EnuerableSyncObjectField(FieldInfo field)
        {
            this.field = field;
            visible = false;
            label = $"{field.Name}  [{field.FieldType.Name}]";
        }
    }

    public class EnumerableSyncObjectDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<EnuerableSyncObjectField> enumerableSyncObjectFields;

        public EnumerableSyncObjectDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            enumerableSyncObjectFields = new List<EnuerableSyncObjectField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.ImplementsInterface<SyncObject>() && field.IsVisibleInInspector())
                {
                    enumerableSyncObjectFields.Add(new EnuerableSyncObjectField(field));
                }
            }
        }

        public void Draw()
        {
            if (enumerableSyncObjectFields.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Objects", EditorStyles.boldLabel);

            for (int i = 0; i < enumerableSyncObjectFields.Count; i++)
            {
                DrawEnumerableSyncObject(enumerableSyncObjectFields[i]);
            }
        }

        void DrawEnumerableSyncObject(EnuerableSyncObjectField enuerableSyncObjectField)
        {
            enuerableSyncObjectField.visible = EditorGUILayout.Foldout(enuerableSyncObjectField.visible, enuerableSyncObjectField.label);
            if (enuerableSyncObjectField.visible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    object fieldValue = enuerableSyncObjectField.field.GetValue(targetObject);
                    if (fieldValue is IEnumerable syncObject)
                    {
                        int index = 0;
                        foreach (object item in syncObject)
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
