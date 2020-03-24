using UnityEditor;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Updates fields from SceneAttribute to SceneField
    /// Without this users will have to manually reassign scene's in NetworkManager
    /// </summary>
    public static class SceneFieldFixer
    {
        /// <summary>
        /// works on anything unity serialized as an array, eg arrays/lists
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void FixArrayField(Object target, string oldFieldName, string newFieldName)
        {
#if UNITY_EDITOR
            SerializedObject serializedObject = new SerializedObject(target);

            SerializedProperty oldPropArray = serializedObject.FindProperty(oldFieldName);
            int arraySize = oldPropArray.arraySize;
            if (arraySize == 0)
                return;

            SerializedProperty newPropArray = serializedObject.FindProperty(newFieldName);

            newPropArray.arraySize = arraySize;
            for (int i = 0; i < arraySize; i++)
            {
                SerializedProperty oldEle = oldPropArray.GetArrayElementAtIndex(i);
                SerializedProperty newEle = newPropArray.GetArrayElementAtIndex(i);

                bool success = FindAndSetScene(oldEle, newEle);
                if (success)
                {
                    Debug.LogWarning($"Could not find Scene with name '{oldEle.stringValue}' in Build settings. Field {newEle.propertyPath} in {target.name} needs manually updating", target);
                }
            }

            oldPropArray.arraySize = 0;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
#endif
        }


        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void FixField(Object target, string oldFieldName, string newFieldName)
        {
#if UNITY_EDITOR
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty oldProp = serializedObject.FindProperty(oldFieldName);

            if (string.IsNullOrEmpty(oldProp.stringValue))
                return;

            SerializedProperty newProp = serializedObject.FindProperty(newFieldName);
            bool success = FindAndSetScene(oldProp, newProp);
            if (success)
            {
                Debug.LogWarning($"Could not find Scene with name '{oldProp.stringValue}' in Build settings. Field {newProp.propertyPath} in {target.name} needs manually updating", target);
            }

            oldProp.stringValue = "";

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
#endif
        }

#if UNITY_EDITOR
        static bool FindAndSetScene(SerializedProperty oldProp, SerializedProperty newProp)
        {
            SerializedProperty newPropPath = newProp.FindPropertyRelative("path");
            SerializedProperty newPropGuid = newProp.FindPropertyRelative("assetGuid");


            // only set new prop if is it empty
            if (string.IsNullOrEmpty(newPropGuid.stringValue))
            {
                string path = FindScene(oldProp);

                if (!string.IsNullOrEmpty(path))
                {
                    newPropPath.stringValue = path;
                    newPropGuid.stringValue = AssetDatabase.AssetPathToGUID(path);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        static string FindScene(SerializedProperty oldProp)
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene buildScene in buildScenes)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path);
                if (sceneAsset.name == oldProp.stringValue)
                {
                   return buildScene.path;
                }
            }

            return "";
        }
#endif
    }
}
