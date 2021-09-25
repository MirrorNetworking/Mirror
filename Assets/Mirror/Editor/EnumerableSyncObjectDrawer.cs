// helper class for NetworkBehaviourInspector to draw all enumerable SyncObjects
// (SyncList/Set/Dictionary)
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Mirror
{
    class EnumerableSyncObjectField
    {
        public bool visible;
        public readonly FieldInfo field;
        public readonly string label;

        public EnumerableSyncObjectField(FieldInfo field)
        {
            this.field = field;
            visible = false;
            label = $"{field.Name}  [{field.FieldType.Name}]";
        }
    }

    public class EnumerableSyncObjectDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<EnumerableSyncObjectField> enumerableSyncObjectFields;

        public EnumerableSyncObjectDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            enumerableSyncObjectFields = new List<EnumerableSyncObjectField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.ImplementsInterface<SyncObject>() && field.IsVisibleInInspector())
                {
                    enumerableSyncObjectFields.Add(new EnumerableSyncObjectField(field));
                }
            }
        }

        public void Draw()
        {
            if (enumerableSyncObjectFields.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Collections", EditorStyles.boldLabel);

            for (int i = 0; i < enumerableSyncObjectFields.Count; i++)
            {
                DrawEnumerableSyncObject(enumerableSyncObjectFields[i]);
            }
        }

        void DrawEnumerableSyncObject(EnumerableSyncObjectField enumerableSyncObjectField)
        {
            enumerableSyncObjectField.visible = EditorGUILayout.Foldout(enumerableSyncObjectField.visible, enumerableSyncObjectField.label);
            if (enumerableSyncObjectField.visible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    object fieldValue = enumerableSyncObjectField.field.GetValue(targetObject);
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
