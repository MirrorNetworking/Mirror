// helper class for NetworkBehaviourInspector to draw all enumerable SyncObjects
// (SyncList/Set/Dictionary)
// 'SyncObjectCollectionsDrawer' is a nicer name than 'IEnumerableSyncObjectsDrawer'
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
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

            // field.FieldType.Name has a backtick and number at the end, e.g. SyncList`1
            // so we split it and only take the first part so it looks nicer.
            // e.g. SyncList`1 -> SyncList
            // Better to do it one time here than in hot path in OnInspectorGUI
            label = $"{field.Name} [{Regex.Replace(field.FieldType.Name, @"`\d+", "")}]";
        }
    }

    public class SyncObjectCollectionsDrawer
    {
        readonly UnityEngine.Object targetObject;
        readonly List<SyncObjectCollectionField> syncObjectCollectionFields;

        public SyncObjectCollectionsDrawer(UnityEngine.Object targetObject)
        {
            this.targetObject = targetObject;
            syncObjectCollectionFields = new List<SyncObjectCollectionField>();
            foreach (FieldInfo field in InspectorHelper.GetAllFields(targetObject.GetType(), typeof(NetworkBehaviour)))
            {
                // only draw SyncObjects that are IEnumerable (SyncList/Set/Dictionary)
                if (field.IsVisibleSyncObject() &&
                    field.ImplementsInterface<SyncObject>() &&
                    field.ImplementsInterface<IEnumerable>())
                {
                    syncObjectCollectionFields.Add(new SyncObjectCollectionField(field));
                }
            }
        }

        public void Draw()
        {
            if (syncObjectCollectionFields.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sync Collections", EditorStyles.boldLabel);

            for (int i = 0; i < syncObjectCollectionFields.Count; i++)
            {
                DrawSyncObjectCollection(syncObjectCollectionFields[i]);
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
