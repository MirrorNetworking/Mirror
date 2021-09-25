// helper class for NetworkBehaviourInspector to draw all enumerable SyncObjects
// (SyncList/Set/Dictionary)
// 'SyncObjectCollectionsDrawer' is a nicer name than 'IEnumerableSyncObjectsDrawer'
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Mirror
{
    class SyncObjectCollectionField
    {
        public bool visible;
        public readonly FieldInfo field;
        public readonly string label;

        public SyncObjectCollectionField(FieldInfo field)
        {
            this.field = field;
            visible = false;
            label = $"{field.Name}  [{field.FieldType.Name}]";
        }
    }

    public class SyncObjectCollectionDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<SyncObjectCollectionField> enumerableSyncObjectFields;

        public SyncObjectCollectionDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            enumerableSyncObjectFields = new List<SyncObjectCollectionField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                if (field.ImplementsInterface<SyncObject>() && field.IsVisibleInInspector())
                {
                    enumerableSyncObjectFields.Add(new SyncObjectCollectionField(field));
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
                DrawSyncObjectCollection(enumerableSyncObjectFields[i]);
            }
        }

        void DrawSyncObjectCollection(SyncObjectCollectionField syncObjectCollectionField)
        {
            syncObjectCollectionField.visible = EditorGUILayout.Foldout(syncObjectCollectionField.visible, syncObjectCollectionField.label);
            if (syncObjectCollectionField.visible)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    object fieldValue = syncObjectCollectionField.field.GetValue(targetObject);
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
