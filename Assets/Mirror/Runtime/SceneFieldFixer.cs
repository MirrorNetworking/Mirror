using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Updates fields from SceneAttribute to SceneField
    /// Without this users will have to manually reassign scene's in NetworkManager
    /// </summary>
    public static class SceneFieldFixer
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void FixField(UnityEngine.Object target, string oldFieldName, string newFieldName)
        {
            UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(target);
            UnityEditor.SerializedProperty oldProp = serializedObject.FindProperty(oldFieldName);

            if (string.IsNullOrEmpty(oldProp.stringValue))
                return;

            UnityEditor.SerializedProperty newProp = serializedObject.FindProperty(newFieldName);
            UnityEditor.SerializedProperty newPropPath = newProp.FindPropertyRelative("path");
            UnityEditor.SerializedProperty newPropGuid = newProp.FindPropertyRelative("assetGuid");


            // only set new prop if is it empty
            if (string.IsNullOrEmpty(newPropGuid.stringValue))
            {
                string path = findScene(oldProp);

                if (!string.IsNullOrEmpty(path))
                {
                    newPropPath.stringValue = path;
                    newPropGuid.stringValue = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                }
                else
                {
                    Debug.LogWarning($"Could not find Scene with name '{oldProp.stringValue}' in Build settings. Field {newFieldName} in {target.name} needs manually updating", target);

                }
            }

            oldProp.stringValue = "";

            serializedObject.ApplyModifiedProperties();
            UnityEditor.EditorUtility.SetDirty(target);
        }
#if UNITY_EDITOR
        private static string findScene(UnityEditor.SerializedProperty oldProp)
        {
            UnityEditor.EditorBuildSettingsScene[] buildScenes = UnityEditor.EditorBuildSettings.scenes;
            foreach (UnityEditor.EditorBuildSettingsScene buildScene in buildScenes)
            {
                UnityEditor.SceneAsset sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(buildScene.path);
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
